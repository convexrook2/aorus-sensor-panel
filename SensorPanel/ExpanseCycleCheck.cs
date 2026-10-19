using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SensorPanel;

internal sealed partial class PanelWindow
{
    // Explicit, visible saved-data diagnostic. Never writes owner preferences.
    private async Task CheckExpanseCycleAsync()
    {
        string output=Path.Combine(PanelPaths.Artifacts,"expanse-cycle-check");
        Directory.CreateDirectory(output);
        var scenes=new List<object>();
        try
        {
            if(previewPath is null || !windowed || settings is null || shipPanel is null)
                throw new InvalidOperationException("Cycle check requires a windowed saved-data preview.");
            await Task.Delay(400);
            var readings=text.ToDictionary(pair=>pair.Key,pair=>pair.Value.Text);
            var peaks=maxima.ToDictionary();
            // Reproduce an old manual hold, then explicitly reselect The Expanse.
            showcaseCycle=false;
            settings.Open();
            settings.SelectChoice("Theme",PanelTheme.Resolve("MCRN").Name);
            settings.SelectChoice("Theme",PanelTheme.Resolve("Expanse").Name);
            settings.SelectChoice("Change ship every","15 seconds");
            if(!showcaseCycle || showcaseSeconds!=15)
                throw new InvalidOperationException("Choosing The Expanse failed to resume cycling or expose timing.");
            await Task.Delay(300); CaptureStudy("settings"); settings.Close();
            foreach(string ship in ExpanseMedia.Ships)
            {
                SelectShowcaseShip(ship,false);
                await Task.Delay(80);
                shipPanel.CheckBindings();
                if(shipPanel.FeaturedShipId!=ship || readings.Any(pair=>pair.Key is not ("date" or "status" or "uptime") && text[pair.Key].Text!=pair.Value)
                    || peaks.Any(pair=>maxima[pair.Key]!=pair.Value))
                    throw new InvalidOperationException("Cycling lost a ship or altered readings/maxima: "+ship);
                scenes.Add(new{Ship=ship,Theme=shipPanel.PresentationThemeId});
                if(shipPanel.PresentationThemeId!=PanelTheme.All.Single(item=>!item.Showcase && item.ShipId==ship).Id)
                    throw new InvalidOperationException("Ship changed without its theme: "+ship);
                if(ship is "Pella" or "Rocinante")
                {
                    await shipPanel.ShipViewport.WaitForReadyAsync(); await Task.Delay(400);
                    CaptureStudy(ship.ToLowerInvariant());
                }
            }
            SelectShowcaseShip("Rocinante",true);
            if(showcaseClock.IsEnabled)throw new InvalidOperationException("Manual hold did not pause cycling.");
            settings.Open();
            var cycle=shipOptions!.Items.OfType<System.Windows.Controls.MenuItem>().Single(item=>item.Header?.ToString()=="Expanse showcase")
                .Items.OfType<System.Windows.Controls.MenuItem>().Single(item=>item.Header?.ToString()=="Auto-cycle ships and personnel");
            cycle.IsChecked=true; cycle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
            settings.Close();
            WindowState=WindowState.Minimized; await Task.Delay(200);
            if(showcaseClock.IsEnabled)throw new InvalidOperationException("Cycle continued while minimized.");
            WindowState=WindowState.Normal; await Task.Delay(300);
            if(SystemParameters.ClientAreaAnimation)
            {
                string? before=shipPanel.FeaturedShipId;
                await Task.Delay(15700);
                if(before==shipPanel.FeaturedShipId)throw new InvalidOperationException("Real 15-second timer did not advance.");
            }
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=true,SavedData=true,
                IntervalSeconds=showcaseSeconds,TimedAdvance=SystemParameters.ClientAreaAnimation,Scenes=scenes},JsonOptions));
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString()},JsonOptions));
            Environment.ExitCode=1;
        }
        finally { Close(); }

        void CaptureStudy(string name)
        {
            UpdateLayout();
            var image=new RenderTargetBitmap((int)Math.Ceiling(ActualWidth),(int)Math.Ceiling(ActualHeight),96,96,PixelFormats.Pbgra32);
            image.Render((Visual)Content);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream=File.Create(Path.Combine(output,name+".png"));encoder.Save(stream);
        }
    }
}
