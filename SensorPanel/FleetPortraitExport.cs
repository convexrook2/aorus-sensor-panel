using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rookline.AssetProtection;

namespace SensorPanel;

// Explicit developer asset preparation. No collector, elevation or hidden window.
// The normal monitor never starts a renderer to create a missing portrait.
internal static class FleetPortraitExport
{
    internal static int Run(string destination,string? onlyShip=null)
    {
        if(AppAssets.Protected)throw new InvalidOperationException("Export editable source portraits from a development build, not an installed archive.");
        Directory.CreateDirectory(destination);
        using var catalog=JsonDocument.Parse(AppAssets.Read("FleetViewer/catalog.json"));
        var entries=catalog.RootElement.EnumerateArray()
            .Where(row=>onlyShip is null || row.GetProperty("id").GetString()==ShipProfile.CanonicalId(onlyShip))
            .Select(row=>row.Clone()).ToArray();
        if(onlyShip is not null && entries.Length!=1)throw new ArgumentException("Unknown portrait ship: "+onlyShip);
        var application=new Application { ShutdownMode=ShutdownMode.OnMainWindowClose };
        var label=new TextBlock { Text="Preparing full-quality ship portraits…",Foreground=Brushes.LightBlue,Margin=new Thickness(12) };
        var viewport=new FleetViewport { Width=1024,Height=2048 };
        viewport.SetMotion(false);
        var layout=new DockPanel { Background=new SolidColorBrush(Color.FromRgb(4,12,19)) };
        DockPanel.SetDock(label,Dock.Top);layout.Children.Add(label);
        layout.Children.Add(new Viewbox { Stretch=Stretch.Uniform,Child=viewport });
        var window=new Window { Title="X890 · Prepare static fleet portraits (no sensors)",Width=540,Height=850,Content=layout };
        int exitCode=1;
        window.Closed+=(_,_)=>viewport.Dispose();
        window.Loaded+=async(_,_)=>
        {
            var receipts=new List<object>();
            try
            {
                foreach(var entry in entries)
                {
                    string id=entry.GetProperty("id").GetString()!;
                    label.Text=$"{receipts.Count+1}/{entries.Length} · {id} · full geometry, PBR, shadows, AO and light glow";
                    viewport.SetShip(id);await viewport.WaitForReadyAsync();
                    window.UpdateLayout();viewport.Reset();await Task.Delay(180);
                    var diagnostic=await viewport.DiagnoseAsync();
                    if(diagnostic.GetProperty("id").GetString()!=id||diagnostic.GetProperty("motion").GetBoolean()
                        ||diagnostic.GetProperty("holotable").GetBoolean())throw new InvalidOperationException("Portrait pose did not settle: "+id);
                    string file=Path.Combine(destination,id+".png"),temporary=file+".new";
                    await viewport.CaptureAsync(temporary);
                    byte[] bytes=await File.ReadAllBytesAsync(temporary);
                    using var stream=new MemoryStream(bytes,false);
                    var bitmap=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
                    if(bitmap.PixelWidth<1024||bitmap.PixelHeight<2048)throw new InvalidOperationException("Portrait capture resolution is too low: "+id);
                    File.Move(temporary,file,true);
                    receipts.Add(new { id,sourceSha256=diagnostic.GetProperty("sourceSha256").GetString(),
                        sha256=Convert.ToHexStringLower(SHA256.HashData(bytes)),width=bitmap.PixelWidth,height=bitmap.PixelHeight,
                        nativeRender=diagnostic });
                    Console.WriteLine($"PORTRAIT: {id} · {bitmap.PixelWidth}x{bitmap.PixelHeight} · {diagnostic.GetProperty("sourceSha256").GetString()}");
                }
                string index=Path.Combine(destination,"index.json");
                await File.WriteAllTextAsync(index+".new",JsonSerializer.Serialize(new {schemaVersion=1,view="three-quarter",portraits=receipts},new JsonSerializerOptions {WriteIndented=true})+"\n");
                File.Move(index+".new",index,true);
                exitCode=0;window.Close();
            }
            catch(Exception ex)
            {
                viewport.Dispose();label.Text="EXPORT FAILED · "+ex.Message;
                File.WriteAllText(Path.Combine(PanelPaths.Artifacts,"fleet-portrait-export-error.txt"),ex.ToString());
                MessageBox.Show(window,ex.Message,"Ship portrait export failed");window.Close();
            }
        };
        application.Run(window);return exitCode;
    }
}
