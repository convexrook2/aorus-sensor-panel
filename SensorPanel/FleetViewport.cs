using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace SensorPanel;

// One shared GPU viewport for the left showcase and open holotable. It consumes only
// local artwork; telemetry remains bound by ShipPanel and Holotable in WPF.
internal sealed class FleetViewport : Grid, IDisposable
{
    private const string Host = "x890-fleet.example";
    private const string Origin = "https://" + Host + "/";
    private readonly TextBlock status = new() { Text="LOADING SHIP…", FontSize=10,
        Foreground=Brushes.LightBlue, TextWrapping=TextWrapping.Wrap,
        HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center };
    private WebView2CompositionControl? browser;
    private Canvas? browserSurface;
    private Window? owner;
    private Task? initialization;
    private TaskCompletionSource modelReady = Completion();
    private bool engineReady, disposed, failed, loaded, motion=true, interactive, holotableMode, layoutPending, resetOnLayout;
    private (bool Active,bool Motion)? activity;
    private string ship="Rocinante";
    private string stageColour="#dd315e";
    private ExpanseGraphicsMode graphicsMode=PanelPreferences.GraphicsMode;
    internal event Action? CloseRequested;
    internal bool Interactive => interactive;
    internal bool IsHolotable => holotableMode;
    internal bool IsDisposed => disposed;
    internal bool MotionActive => motion && loaded && IsVisible && IsEnabled
        && owner?.WindowState!=WindowState.Minimized && owner?.IsVisible==true
        && SystemParameters.ClientAreaAnimation && !failed && !disposed && modelReady.Task.IsCompletedSuccessfully;
    internal string ShipId => ship;
    private static TaskCompletionSource Completion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal FleetViewport()
    {
        ClipToBounds=true; UseLayoutRounding=false;
        Children.Add(status);
        Loaded+=OnLoaded;
        SizeChanged+=(_,_)=>RefreshLayout();
        Unloaded+=(_,_)=>{loaded=false; PublishActivity();};
        IsVisibleChanged+=(_,_)=>PublishActivity();
        IsEnabledChanged+=(_,_)=>PublishActivity();
    }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        loaded=true;
        if(owner is null)
        {
            owner=Window.GetWindow(this);
            if(owner is not null)
            {
                owner.Closed+=OwnerClosed;
                owner.SizeChanged+=OwnerSizeChanged;
                owner.DpiChanged+=OwnerDpiChanged;
            }
            SystemParameters.StaticPropertyChanged+=AnimationPreferenceChanged;
        }
        initialization??=InitializeAsync();
        PublishActivity();
        RefreshLayout();
    }
    private void OwnerClosed(object? sender, EventArgs e)=>Dispose();
    private void OwnerSizeChanged(object sender,SizeChangedEventArgs e)=>RefreshLayout();
    private void OwnerDpiChanged(object sender,DpiChangedEventArgs e)=>RefreshLayout();
    private void AnimationPreferenceChanged(object? sender, PropertyChangedEventArgs e)
    { if(e.PropertyName is null or nameof(SystemParameters.ClientAreaAnimation)) PublishActivity(); }

    private async Task InitializeAsync()
    {
        try
        {
            if(!Rookline.AssetProtection.AppAssets.Exists("FleetViewer/index.html")) throw new FileNotFoundException("The full ship artwork bundle is missing.");
            string profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"X890Sensor","FleetWebView2");
            var environment=await CoreWebView2Environment.CreateAsync(userDataFolder:profile);
            if(disposed)return;
            browser=new WebView2CompositionControl { DefaultBackgroundColor=System.Drawing.Color.Transparent, UseLayoutRounding=false };
            // Canvas keeps the larger raster surface out of parent measurement.
            // Its inverse transform preserves the established layout and input.
            browserSurface=new Canvas { ClipToBounds=true };
            browserSurface.Children.Add(browser);Children.Insert(0,browserSurface);
            UpdateCaptureSize();
            await browser.EnsureCoreWebView2Async(environment);
            if(disposed)return;
            var core=browser.CoreWebView2;
            Rookline.AssetProtection.AppAssets.Host(core,Host,"FleetViewer");
            core.Settings.AreDefaultContextMenusEnabled=false;
            core.Settings.IsStatusBarEnabled=false;
            core.Settings.AreBrowserAcceleratorKeysEnabled=false;
            core.Settings.IsZoomControlEnabled=false;
            core.Settings.IsSwipeNavigationEnabled=false;
            core.IsMuted=true;
            core.NavigationStarting+=(_,e)=>
            { if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!=Host)e.Cancel=true; };
            core.NewWindowRequested+=(_,e)=>e.Handled=true;
            core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
            core.DownloadStarting+=(_,e)=>e.Cancel=true;
            core.WebMessageReceived+=MessageReceived;
            core.ProcessFailed+=(_,e)=>Fail(new InvalidOperationException("Ship renderer stopped: "+e.ProcessFailedKind));
            core.NavigationCompleted+=(_,e)=>{if(!e.IsSuccess)Fail(new InvalidOperationException("Ship renderer could not load: "+e.WebErrorStatus));};
            core.Navigate(Origin+"index.html");
        }
        catch(Exception ex){Fail(ex);}
    }
    private void MessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if(disposed||!e.Source.StartsWith(Origin,StringComparison.Ordinal))return;
        try
        {
            using var message=JsonDocument.Parse(e.WebMessageAsJson);
            var root=message.RootElement;
            switch(root.GetProperty("type").GetString())
            {
                case "engine-ready":
                    engineReady=true; SendScene(); PublishActivity(); RefreshLayout(); break;
                case "model-ready":
                    if(root.GetProperty("id").GetString()!=ship)return;
                    ValidateModel(root);
                    status.Visibility=Visibility.Collapsed;
                    modelReady.TrySetResult(); break;
                case "close":
                    if(interactive)CloseRequested?.Invoke(); break;
                case "error":
                    Fail(new InvalidOperationException(root.GetProperty("message").GetString())); break;
            }
        }
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or KeyNotFoundException){Fail(ex);}
    }
    private static void ValidateModel(JsonElement data)
    {
        if(!data.GetProperty("modelReady").GetBoolean()
            ||data.GetProperty("triangles").GetInt32()!=data.GetProperty("expectedTriangles").GetInt32()
            ||data.GetProperty("sourceSha256").GetString()!=data.GetProperty("expectedSha256").GetString()
            ||data.GetProperty("geometryReduced").GetBoolean()||data.GetProperty("texturesResampled").GetBoolean()
            ||!data.GetProperty("shadows").GetBoolean()||!data.GetProperty("ambientOcclusion").GetBoolean())
            throw new InvalidOperationException("The ship viewport did not retain the reviewed model and full shading.");
        foreach(var expected in data.GetProperty("expectedMaps").EnumerateObject())
            if(data.GetProperty("maps").GetProperty(expected.Name).GetInt32()!=expected.Value.GetInt32())
                throw new InvalidOperationException("A ship PBR texture map did not reach the renderer: "+expected.Name);
    }
    private void Fail(Exception ex)
    {
        if(failed||disposed)return;
        failed=true;modelReady.TrySetException(ex);
        status.Text="SHIP VIEW UNAVAILABLE · "+ex.Message;status.Visibility=Visibility.Visible;
        try{File.WriteAllText(Path.Combine(PanelPaths.Artifacts,"fleet-view-error.txt"),ex.ToString());}catch(IOException){}
        browser?.Dispose();browser=null;
        Children.Clear();Children.Add(status);
    }
    private void Send(object message)
    {
        if(!engineReady||disposed||failed||browser?.CoreWebView2 is not {} core)return;
        try{core.PostWebMessageAsJson(JsonSerializer.Serialize(message));}
        catch(InvalidOperationException ex){Fail(ex);}
    }
    private void SendScene()=>Send(new{type="scene",id=ship,interactive,holotable=holotableMode,stageColour,graphicsMode=graphicsMode.ToString()});
    internal void SetGraphicsMode(ExpanseGraphicsMode mode)
    {if(graphicsMode==mode)return;graphicsMode=mode;SendScene();}
    private void RefreshLayout()
    {
        if(layoutPending||disposed)return;
        layoutPending=true;
        // Reparenting and arranging the composition surface finish asynchronously.
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>
        {
            layoutPending=false;
            if(disposed||!IsLoaded)return;
            UpdateCaptureSize();
            SendScene();PublishActivity();Send(new{type="resize"});
            // Do not retain framing from the narrow portrait during reparenting.
            // Apply once after the new WPF layout; ordinary resizes retain orbit/zoom.
            if(resetOnLayout && engineReady){Reset();resetOnLayout=false;}
        }));
    }
    private void UpdateCaptureSize()
    {
        if(browser is null||owner is null||ActualWidth<=0||ActualHeight<=0)return;
        // WebView2CompositionControl captures a bitmap at its logical size.
        // A surrounding Viewbox can enlarge that bitmap without increasing its
        // pixel count. Render at the ancestor scale, then map back into the same
        // logical rectangle. WebView2 already accounts for monitor DPI itself.
        var screen=TransformToAncestor(owner).TransformBounds(new Rect(0,0,ActualWidth,ActualHeight));
        double width=Math.Ceiling(ActualWidth*Math.Max(1,screen.Width/ActualWidth));
        double height=Math.Ceiling(ActualHeight*Math.Max(1,screen.Height/ActualHeight));
        if(browser.Width==width&&browser.Height==height)return;
        browser.Width=width;browser.Height=height;
        var inverse=new ScaleTransform(ActualWidth/width,ActualHeight/height);inverse.Freeze();
        browser.RenderTransform=inverse;
    }
    private void PublishActivity()
    {
        if (!engineReady || disposed || failed) return;
        bool active=loaded&&IsVisible&&IsEnabled&&owner?.WindowState!=WindowState.Minimized&&owner?.IsVisible==true;
        var current=(Active:active,Motion:motion&&SystemParameters.ClientAreaAnimation);
        if (activity==current) return;
        activity=current;
        if(browser?.CoreWebView2 is {} core)
            core.MemoryUsageTargetLevel=active ? CoreWebView2MemoryUsageTargetLevel.Normal : CoreWebView2MemoryUsageTargetLevel.Low;
        Send(new{type="activity",motion=current.Motion,active});
    }
    internal void SetShip(string id)
    {
        id=ShipProfile.CanonicalId(id);
        if(!Supports(id))throw new ArgumentException("Unknown ship artwork.",nameof(id));
        if(ship!=id)
        {
            modelReady.TrySetCanceled();modelReady=Completion();ship=id;
            status.Text="LOADING "+ShipProfile.Resolve(id).Name.ToUpperInvariant()+"…";status.Visibility=Visibility.Visible;
        }
        SendScene();
    }
    internal void SetInteractive(bool enabled)
    {
        resetOnLayout|=interactive!=enabled;
        interactive=enabled;IsHitTestVisible=enabled;Focusable=enabled;
        RefreshLayout();PublishActivity();
    }
    internal static bool Supports(string? id)=>id=="SolRing" || ShipPortrait.Supports(id);
    internal void SetHolotableMode(bool enabled,Color? accent=null)
    {
        if(accent is {} c)stageColour=$"#{c.R:X2}{c.G:X2}{c.B:X2}";
        resetOnLayout|=holotableMode!=enabled;holotableMode=enabled;
        SetInteractive(enabled);
    }
    internal void SetMotion(bool enabled){motion=enabled;PublishActivity();}
    internal void Reset()=>Send(new{type="reset"});
    internal void Inspect(double x,double y,double factor)
    { Send(new{type="orbit",x,y});Send(new{type="zoom",factor}); }
    internal async Task WaitForReadyAsync()=>await modelReady.Task.WaitAsync(TimeSpan.FromSeconds(60));
    internal async Task<JsonElement> DiagnoseAsync()
    {
        await WaitForReadyAsync();
        string json=await browser!.CoreWebView2.ExecuteScriptAsync("window.x890Fleet.diagnose()");
        using var data=JsonDocument.Parse(json);ValidateModel(data.RootElement);return data.RootElement.Clone();
    }
    internal object PixelDiagnostics()
    {
        var first=PointToScreen(new Point());
        var last=PointToScreen(new Point(ActualWidth,ActualHeight));
        var image=browser?.Template?.FindName("PART_image",browser) as Image;
        var captured=image?.Source;
        return new
        {
            LogicalWidth=ActualWidth,LogicalHeight=ActualHeight,
            ScreenWidth=Math.Abs(last.X-first.X),ScreenHeight=Math.Abs(last.Y-first.Y),
            BrowserWidth=browser?.ActualWidth,BrowserHeight=browser?.ActualHeight,
            CaptureType=captured?.GetType().FullName,
            CaptureWidth=captured is System.Windows.Interop.D3DImage d3d?d3d.PixelWidth:captured?.Width,
            CaptureHeight=captured is System.Windows.Interop.D3DImage d3dHeight?d3dHeight.PixelHeight:captured?.Height
        };
    }
    internal async Task StartRenderProfileAsync()
    { await WaitForReadyAsync();await browser!.CoreWebView2.ExecuteScriptAsync("window.x890Fleet.profile(30)"); }
    internal async Task CaptureAsync(string path)
    {
        await WaitForReadyAsync();
        using var stream=File.Create(path);
        await browser!.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
    }
    internal void Check()
    {
        if(failed||disposed)throw new InvalidOperationException("Full ship renderer unavailable; inspect fleet-view-error.txt.");
        if(!Rookline.AssetProtection.AppAssets.Exists("FleetViewer/catalog.json"))
            throw new InvalidOperationException("The full ship catalogue is missing.");
    }
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        modelReady.TrySetCanceled();
        if(owner is not null)
        {
            owner.Closed-=OwnerClosed;
            owner.SizeChanged-=OwnerSizeChanged;
            owner.DpiChanged-=OwnerDpiChanged;
        }
        SystemParameters.StaticPropertyChanged-=AnimationPreferenceChanged;
        browser?.Dispose();browser=null;browserSurface=null;
        Children.Clear();
    }
}
