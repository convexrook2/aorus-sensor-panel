using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SensorPanel;

internal sealed partial class ShipPanel
{
    private bool showcaseRotation=PanelPreferences.ShowcaseRotation;
    private Button? rotationControl;
    internal Task GraphicsRetirement { get; private set; }=Task.CompletedTask;
    internal event Action<bool>? ShowcaseRotationChanged;
    internal bool ShowcaseRotationEnabled=>showcaseRotation;
    internal bool ShowcaseRotationActive=>holotable is null&&shipViewport?.MotionActive==true;

    private void AddShowcaseRotationControl(PanelTheme theme,bool portrait)
    {
        rotationControl=new Button { FontSize=portrait?7.5:10,Foreground=theme.Foreground,
            Background=theme.Surface,BorderBrush=theme.Divider,Cursor=Cursors.Hand,
            Padding=new Thickness(4,3,4,3) };
        // Keep the dark console colours on hover; the Windows button chrome
        // otherwise washes the label out against its bright hover fill.
        var frame=new FrameworkElementFactory(typeof(Border),"RotationFrame");
        frame.SetValue(Border.BackgroundProperty,theme.Surface);
        frame.SetValue(Border.BorderBrushProperty,theme.Divider);
        frame.SetValue(Border.BorderThicknessProperty,new Thickness(1));
        frame.SetValue(Border.CornerRadiusProperty,new CornerRadius(2));
        var content=new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(HorizontalAlignmentProperty,HorizontalAlignment.Center);
        content.SetValue(VerticalAlignmentProperty,VerticalAlignment.Center);frame.AppendChild(content);
        var template=new ControlTemplate(typeof(Button)) { VisualTree=frame };
        var hover=new Trigger { Property=IsMouseOverProperty,Value=true };
        hover.Setters.Add(new Setter(Border.BorderBrushProperty,theme.Accent,"RotationFrame"));template.Triggers.Add(hover);
        rotationControl.Template=template;
        rotationControl.Click+=(_,e)=>
        {
            e.Handled=true;SetShowcaseRotation(!showcaseRotation);
            ShowcaseRotationChanged?.Invoke(showcaseRotation);
        };
        Put(this,rotationControl,new(sidebarBounds.X+10,sidebarBounds.Y+40,sidebarBounds.Width-20,26));
        UpdateShowcaseRotation();
    }
    internal void SetShowcaseRotation(bool enabled)
    {showcaseRotation=enabled;UpdateShowcaseRotation();}
    private void UpdateShowcaseRotation()
    {
        if(holotable is null)shipViewport?.SetMotion(showcaseRotation&&presentationActive);
        if(rotationControl is null)return;
        string action=showcaseRotation?"Pause rotation":"Resume rotation";
        rotationControl.Content=action.ToUpperInvariant();rotationControl.ToolTip=action;
        System.Windows.Automation.AutomationProperties.SetName(rotationControl,action);
        rotationControl.IsEnabled=FleetViewport.Supports(FeaturedShipId);
    }
    internal void SuspendShowcase()
    {
        SetActive(false);
        CloseHolotable();shipViewport?.SetMotion(false);
        if(shipViewport?.Parent is Panel parent)parent.Children.Remove(shipViewport);
        shipViewport?.Dispose();
        GraphicsRetirement=shipViewport?.Retirement??Task.CompletedTask;
        shipViewport=null;configuration="";
        projection?.Dispose();projection=null;
        showcaseBackdrop?.SetMotion(false);showcaseCrew?.SetMotion(false);
        Children.Clear();bound.Clear();
        shipPoster=null;shipMedia=null;holotableAccess=null;
        showcaseBackdrop=null;showcaseCrew=null;heading=faction=mediaCredit=null;
        shipChoices=null;rotationControl=null;activeTheme=null;selectedShip=null;
        SolarBodyAssets.Release();SolarGateModel.Release();SolarSky.Release();
        ShipPoster.ReleaseCache();ShipArtwork.ReleaseCache();ShipModel.ReleaseCache();
    }
    internal void CheckRotationControl()
    {
        if(rotationControl is not { IsEnabled:true,IsVisible:true })throw new InvalidOperationException("The showcase rotation control is unavailable.");
        var hit=InputHitTest(new Point(GetLeft(rotationControl)+rotationControl.Width/2,GetTop(rotationControl)+rotationControl.Height/2)) as DependencyObject;
        if(hit is null||(hit!=rotationControl&&!rotationControl.IsAncestorOf(hit)))throw new InvalidOperationException("The showcase rotation control is covered.");
    }
    internal void ToggleRotationControlForReview()
    {CheckRotationControl();rotationControl!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
}
