using System.Windows;
using System.Windows.Controls;

namespace SensorPanel;

internal enum ExpanseGraphicsMode { FullFidelity, Efficient }

internal sealed partial class PanelWindow
{
    private ExpanseGraphicsMode graphicsMode=PanelPreferences.GraphicsMode;
    private string aorusTheme=PanelPreferences.ExperienceTheme(false),expanseTheme=PanelPreferences.ExperienceTheme(true);

    private MenuItem ExperienceCommands()
    {
        var group=new MenuItem { Header="Experience" };
        foreach(bool expanse in new[]{false,true})
        {
            var item=new MenuItem { Header=expanse?"The Expanse Experience":"AORUS · Lite Mode",IsCheckable=true };
            group.SubmenuOpened+=(_,_)=>item.IsChecked=theme.ShipConsole==expanse;
            item.Click+=(_,_)=>
            {
                if(theme.ShipConsole==expanse)return;
                if(theme.ShipConsole)expanseTheme=theme.Id;else aorusTheme=theme.Id;
                ChooseTheme(PanelTheme.Resolve(expanse?expanseTheme:aorusTheme));
            };
            group.Items.Add(item);
        }
        return group;
    }
    private MenuItem GraphicsCommands()
    {
        var group=new MenuItem { Header="Rendering mode" };
        foreach(var mode in new[]{ExpanseGraphicsMode.FullFidelity,ExpanseGraphicsMode.Efficient})
        {
            var item=new MenuItem { Header=mode==ExpanseGraphicsMode.FullFidelity?"Full Fidelity":"Efficient",IsCheckable=true };
            group.SubmenuOpened+=(_,_)=>item.IsChecked=graphicsMode==mode;
            item.Click+=(_,_)=>ApplyGraphicsMode(mode,true);
            group.Items.Add(item);
        }
        return group;
    }
    private void ChooseTheme(PanelTheme choice)
    {
        if(theme.ShipConsole)expanseTheme=theme.Id;else aorusTheme=theme.Id;
        theme=choice;
        if(choice.ShipConsole)expanseTheme=choice.Id;else aorusTheme=choice.Id;
        if(choice.Showcase)
        {
            showcaseCycle=true;
            if(previewPath is null)PanelPreferences.SaveExpanseCycle(true);
        }
        if(previewPath is null)PanelPreferences.SaveTheme(theme.Id);
        ApplyTheme();SaveDisplayState();
    }
    private void ApplyGraphicsMode(ExpanseGraphicsMode mode,bool persist=false)
    {
        graphicsMode=mode;shipPanel?.SetGraphicsMode(mode);
        if(persist&&previewPath is null)PanelPreferences.SaveGraphicsMode(mode);
        settings?.ApplyTheme();if(IsLoaded)SaveDisplayState();
    }
}

internal sealed partial class ShipPanel
{
    private ExpanseGraphicsMode graphicsMode=PanelPreferences.GraphicsMode;
    internal void SetGraphicsMode(ExpanseGraphicsMode mode)
    {graphicsMode=mode;shipViewport?.SetGraphicsMode(mode);projection?.SetGraphicsMode(mode);}
}
