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

// One reusable GPU viewport for the portrait and holotable. It consumes only
// local artwork; telemetry remains bound by ShipPanel and Holotable in WPF.
internal sealed class FleetViewport : Grid, IDisposable
{
    private const string Host = "x890-fleet.example";
    private const string Origin = "https://" + Host + "/";
    private readonly TextBlock status = new() { Text="LOADING SHIP…", FontSize=10,
        Foreground=Brushes.LightBlue, TextWrapping=TextWrapping.Wrap,
        HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center };
    private WebView2CompositionControl? browser;
    private Window? owner;
    private Task? initialization;
    private TaskCompletionSource modelReady = Completion();
    private bool engineReady, disposed, failed, loaded, motion=true, interactive, layoutPending;
    private string ship="Rocinante";
    internal event Action? CloseRequested;
    internal bool Interactive => interactive;
    internal bool MotionActive => !interactive && motion && loaded && IsVisible && IsEnabled
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
            if(owner is not null) owner.Closed+=OwnerClosed;
            SystemParameters.StaticPropertyChanged+=AnimationPreferenceChanged;
        }
        initialization??=InitializeAsync();
        PublishActivity();
        RefreshLayout();
    }
    private void OwnerClosed(object? sender, EventArgs e)=>Dispose();
    private void AnimationPreferenceChanged(object? sender, PropertyChangedEventArgs e)
    { if(e.PropertyName is null or nameof(SystemParameters.ClientAreaAnimation)) PublishActivity(); }

    private async Task InitializeAsync()
    {
        try
        {
            string assets=Path.Combine(AppContext.BaseDirectory,"FleetViewer");
            if(!File.Exists(Path.Combine(assets,"index.html"))) throw new FileNotFoundException("The full ship artwork bundle is missing.");
            string profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"X890Sensor","FleetWebView2");
            var environment=await CoreWebView2Environment.CreateAsync(userDataFolder:profile);
            if(disposed)return;
            browser=new WebView2CompositionControl { DefaultBackgroundColor=System.Drawing.Color.Transparent, UseLayoutRounding=false };
            Children.Insert(0,browser);
            await browser.EnsureCoreWebView2Async(environment);
            if(disposed)return;
            var core=browser.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping(Host,assets,CoreWebView2HostResourceAccessKind.Deny);
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
    private void SendScene()=>Send(new{type="scene",id=ship,interactive});
    private void RefreshLayout()
    {
        if(layoutPending||disposed)return;
        layoutPending=true;
        // Reparenting and arranging the composition surface finish asynchronously.
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>
        {
            layoutPending=false;
            if(disposed||!IsLoaded)return;
            SendScene();PublishActivity();Send(new{type="resize"});
        }));
    }
    private void PublishActivity()=>Send(new{type="activity",motion=motion&&SystemParameters.ClientAreaAnimation,
        active=loaded&&IsVisible&&IsEnabled});
    internal void SetShip(string id)
    {
        id=ShipProfile.CanonicalId(id);
        if(!ShipPortrait.Supports(id))throw new ArgumentException("Unknown ship artwork.",nameof(id));
        if(ship!=id)
        {
            modelReady.TrySetCanceled();modelReady=Completion();ship=id;
            status.Text="LOADING "+ShipProfile.Resolve(id).Name.ToUpperInvariant()+"…";status.Visibility=Visibility.Visible;
        }
        SendScene();
    }
    internal void SetInteractive(bool enabled)
    { interactive=enabled;IsHitTestVisible=enabled;Focusable=enabled;SendScene();PublishActivity(); }
    internal void SetMotion(bool enabled){motion=enabled;PublishActivity();}
    internal void Reset()=>Send(new{type="reset"});
    internal async Task WaitForReadyAsync()=>await modelReady.Task.WaitAsync(TimeSpan.FromSeconds(60));
    internal async Task<JsonElement> DiagnoseAsync()
    {
        await WaitForReadyAsync();
        string json=await browser!.CoreWebView2.ExecuteScriptAsync("window.x890Fleet.diagnose()");
        using var data=JsonDocument.Parse(json);ValidateModel(data.RootElement);return data.RootElement.Clone();
    }
    internal async Task CaptureAsync(string path)
    {
        await WaitForReadyAsync();
        using var stream=File.Create(path);
        await browser!.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
    }
    internal void Check()
    {
        if(failed||disposed)throw new InvalidOperationException("Full ship renderer unavailable; inspect fleet-view-error.txt.");
        if(!File.Exists(Path.Combine(AppContext.BaseDirectory,"FleetViewer","catalog.json")))
            throw new InvalidOperationException("The full ship catalogue is missing.");
    }
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        if(owner is not null)owner.Closed-=OwnerClosed;
        SystemParameters.StaticPropertyChanged-=AnimationPreferenceChanged;
        browser?.Dispose();browser=null;
    }
}
