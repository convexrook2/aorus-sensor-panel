using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SensorPanel;

internal sealed partial class PanelWindow
{
    private async Task CheckExperienceAsync()
    {
        string folder=Path.Combine(PanelPaths.Artifacts,"experience-check");Directory.CreateDirectory(folder);
        var rows=new List<object>();
        string preferences=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"X890Sensor","settings.json");
        byte[]? saved=File.Exists(preferences)?File.ReadAllBytes(preferences):null;
        try
        {
            if(previewPath is null||!windowed||settings is null||shipPanel is null)
                throw new InvalidOperationException("Experience check requires a visible saved-data preview.");
            PanelPreferences.CheckExperiencePersistence();await Task.Delay(500);Activate();
            var readings=text.ToDictionary(p=>p.Key,p=>p.Value.Text);var peaks=maxima.ToDictionary();
            settings.Open();settings.SelectChoice("Experience","AORUS · Lite Mode");
            await themeChange;
            await Task.Delay(300);
            if(theme.ShipConsole||shipPanel?.HasShipViewport==true||shipPanel?.NavigationView?.MotionActive==true)
                throw new InvalidOperationException("AORUS Lite retained active Expanse graphics.");
            settings.SelectChoice("Theme","AORUS Dark");UpdateLayout();settings.CheckExperienceControls(false);SaveFrame("aorus-lite");
            settings.SelectChoice("Experience","The Expanse Experience");
            settings.SelectChoice("Theme","Expanse · Rocinante");
            settings.SelectChoice("Rendering","Full Fidelity");
            if(shipPanel is null)throw new InvalidOperationException("Expanse did not rebuild its optional graphics.");
            var viewport=shipPanel.ShipViewport;await viewport.WaitForReadyAsync();await Task.Delay(500);
            var fullBefore=await viewport.DiagnoseAsync();var solarBefore=shipPanel.NavigationView!.Diagnose();await Task.Delay(1300);
            var full=await viewport.DiagnoseAsync();var solar=shipPanel.NavigationView.Diagnose();
            double fullElapsed=full.GetProperty("animationSeconds").GetDouble()-fullBefore.GetProperty("animationSeconds").GetDouble();
            double fullRate=fullElapsed>0?(full.GetProperty("frames").GetInt64()-fullBefore.GetProperty("frames").GetInt64())/fullElapsed:0;
            if(full.GetProperty("frameLimit").ValueKind!=JsonValueKind.Null||solar.GetProperty("frameLimit").ValueKind!=JsonValueKind.Null)
                throw new InvalidOperationException("Full Fidelity still has an artificial animation cap.");
            rows.Add(new{Phase="full-fidelity",DrawsPerAnimationSecond=fullRate,Renderer=full,SolarBefore=solarBefore,Solar=solar});SaveFrame("expanse-full");
            settings.SelectChoice("Rendering","Efficient");await Task.Delay(300);
            var before=await viewport.DiagnoseAsync();await Task.Delay(1800);var efficient=await viewport.DiagnoseAsync();
            double elapsed=efficient.GetProperty("animationSeconds").GetDouble()-before.GetProperty("animationSeconds").GetDouble();
            double rate=elapsed>0?(efficient.GetProperty("frames").GetInt64()-before.GetProperty("frames").GetInt64())/elapsed:0;
            solar=shipPanel.NavigationView.Diagnose();
            if(!ReferenceEquals(viewport,shipPanel.ShipViewport)||efficient.GetProperty("sourceSha256").GetString()!=full.GetProperty("sourceSha256").GetString()
                ||efficient.GetProperty("frameLimit").GetInt32()!=30||solar.GetProperty("frameLimit").GetInt32()!=10
                ||SystemParameters.ClientAreaAnimation&&(rate<=0||rate>32))
                throw new InvalidOperationException("Efficient mode lost full model identity or its drawing budget.");
            rows.Add(new{Phase="efficient",DrawsPerAnimationSecond=rate,Renderer=efficient,Solar=solar});SaveFrame("expanse-efficient");
            settings.Close();shipPanel.OpenHolotable();await viewport.WaitForReadyAsync();await Task.Delay(350);
            settings.Open(shipOptions);settings.SelectChoice("Rendering","Full Fidelity");await Task.Delay(250);
            var table=await viewport.DiagnoseAsync();shipPanel.CheckHolotableFraming();
            if(!table.GetProperty("holotable").GetBoolean()||table.GetProperty("frameLimit").ValueKind!=JsonValueKind.Null||shipPanel.NavigationView.MotionActive)
                throw new InvalidOperationException("Full holotable mode restarted the covered map or retained a cap.");
            rows.Add(new{Phase="full-holotable",Renderer=table,Solar=shipPanel.NavigationView.Diagnose()});SaveFrame("ship-options");
            settings.Close();shipPanel.CloseHolotable();settings.Open();
            settings.SelectChoice("Experience","AORUS · Lite Mode");
            await themeChange;
            if(theme.Id!="Dark"||shipPanel?.HasShipViewport==true)throw new InvalidOperationException("Experience switching lost the AORUS palette or retained the renderer.");
            settings.SelectChoice("Experience","The Expanse Experience");
            if(theme.Id!="Rocinante")throw new InvalidOperationException("Experience switching lost the selected ship theme.");
            if(shipPanel is null)throw new InvalidOperationException("Expanse did not rebuild its optional graphics after Lite.");
            settings.SelectChoice("Rendering","Efficient");await shipPanel.ShipViewport.WaitForReadyAsync();await Task.Delay(400);
            if(readings.Any(p=>p.Key is not ("date" or "status" or "uptime")&&text[p.Key].Text!=p.Value)||peaks.Any(p=>maxima[p.Key]!=p.Value))
                throw new InvalidOperationException("Experience controls changed sensor readings or peaks.");
            if(saved is null?File.Exists(preferences):!File.ReadAllBytes(preferences).SequenceEqual(saved))
                throw new InvalidOperationException("The saved-data review changed owner preferences.");
            foreach(var size in new[]{new Size(480,800),new Size(1280,800)})
            {Width=size.Width;Height=size.Height;UpdateLayout();await Task.Delay(350);settings.Open();UpdateLayout();settings.CheckExperienceControls(true);SaveFrame(size.Width<600?"narrow":"review");}
            SaveDisplayState();
            File.WriteAllText(Path.Combine(folder,"result.json"),JsonSerializer.Serialize(new{Passed=true,SavedData=true,ProcessId=Environment.ProcessId,
                Scope="Visible native settings selection, AORUS unload, Full Fidelity uncapped scheduling, Efficient budget/full model identity, covered-map stop, theme memory, unchanged readings/peaks/preferences. Not LIVE GPU or installed-package proof.",Rows=rows},JsonOptions));
        }
        catch(Exception ex){Environment.ExitCode=1;File.WriteAllText(Path.Combine(folder,"result.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),Rows=rows},JsonOptions));}
        finally
        {
            if(Environment.ExitCode!=0&&Environment.GetCommandLineArgs().Contains("--hold-review-failure"))await Task.Delay(30000);
            if(!Environment.GetCommandLineArgs().Contains("--leave-review-open")||Environment.ExitCode!=0)Close();
        }
        void SaveFrame(string name)
        {
            UpdateLayout();var image=new RenderTargetBitmap((int)Math.Ceiling(ActualWidth),(int)Math.Ceiling(ActualHeight),96,96,PixelFormats.Pbgra32);
            image.Render((Visual)Content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream=File.Create(Path.Combine(folder,name+".png"));encoder.Save(stream);
        }
    }
}
