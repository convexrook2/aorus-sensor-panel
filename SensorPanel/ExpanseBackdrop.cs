using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SensorPanel;

// A pair of decorative layers; sensor controls are never inside their fade.
internal sealed class ExpanseBackdrop : Grid
{
    private readonly Image[] layers = [new(), new()];
    private int visible;
    private bool motion = true;
    private string? ship;
    internal bool HasImage => layers[visible].Source is not null;
    internal double ActiveOpacity => layers[visible].Opacity;
    public ExpanseBackdrop()
    {
        ClipToBounds = true; IsHitTestVisible = false;
        Background = Space();
        foreach (var layer in layers)
        {
            layer.Stretch = Stretch.UniformToFill; layer.Opacity = 0;
            RenderOptions.SetBitmapScalingMode(layer, BitmapScalingMode.HighQuality);
            Children.Add(layer);
        }
        Children.Add(new Rectangle { Fill = new LinearGradientBrush(new GradientStopCollection
        {
            new(Color.FromArgb(100, 2, 7, 14), 0), new(Color.FromArgb(140, 2, 7, 14), .55), new(Color.FromArgb(185, 2, 7, 14), 1)
        }, new Point(0, 0), new Point(1, 1)) });
        Unloaded += (_, _) => ClearFades();
        IsVisibleChanged += (_, _) => { if (!IsVisible) ClearFades(); };
    }
    public void SetMotion(bool enabled) { motion = enabled; if (!enabled || !SystemParameters.ClientAreaAnimation) ClearFades(); }
    private void ClearFades()
    {
        for (int i = 0; i < layers.Length; i++)
        { layers[i].BeginAnimation(OpacityProperty, null); layers[i].Opacity = i == visible ? .85 : 0; }
        layers[1-visible].Source = null;
    }
    public void Show(string shipId)
    {
        if (ship == shipId) return;
        bool first = ship is null; ship = shipId;
        var scene = ExpanseMedia.ForShip(shipId);
        var next = layers[1-visible]; var old = layers[visible];
        double oldOpacity = old.Opacity;
        next.BeginAnimation(OpacityProperty, null); old.BeginAnimation(OpacityProperty, null);
        next.Source = ExpanseMedia.Image(scene.Background, 1600); next.Opacity = .85; old.Opacity = 0;
        visible = 1-visible;
        if (!first && motion && IsVisible && SystemParameters.ClientAreaAnimation)
        {
            var fadeIn = new DoubleAnimation(0, .85, TimeSpan.FromSeconds(1.5)) { FillBehavior = FillBehavior.Stop };
            var fadeOut = new DoubleAnimation(oldOpacity, 0, TimeSpan.FromSeconds(1.5)) { FillBehavior = FillBehavior.Stop };
            Timeline.SetDesiredFrameRate(fadeIn, 20); Timeline.SetDesiredFrameRate(fadeOut, 20);
            fadeOut.Completed += (_, _) => { if (layers[1-visible] == old) old.Source = null; };
            next.BeginAnimation(OpacityProperty, fadeIn); old.BeginAnimation(OpacityProperty, fadeOut);
        }
        else ClearFades();
    }
    public void Reload(string shipId) { ship = null; Show(shipId); }

    private static Brush Space()
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(2, 7, 15), Color.FromRgb(12, 29, 42), 28), null, new Rect(0, 0, 1600, 900));
            var random = new Random(661);
            for (int i = 0; i < 300; i++)
            {
                byte light = (byte)random.Next(80, 210);
                var ink = new SolidColorBrush(Color.FromArgb(light, 192, 224, 241)); ink.Freeze();
                double radius = random.NextDouble() * 1.1 + .25;
                dc.DrawEllipse(ink, null, new Point(random.NextDouble()*1600, random.NextDouble()*900), radius, radius);
            }
            var limb = new RadialGradientBrush(new GradientStopCollection { new(Color.FromRgb(11, 32, 49), 0), new(Color.FromRgb(4, 15, 27), .92), new(Color.FromRgb(55, 135, 164), .985), new(Colors.Transparent, 1) });
            dc.DrawEllipse(limb, null, new Point(1460, 820), 520, 520);
        }
        var brush = new DrawingBrush(group) { Stretch = Stretch.UniformToFill }; brush.Freeze(); return brush;
    }
}

internal sealed class ExpanseCrew : Grid
{
    private readonly Image portrait = new() { Stretch = Stretch.UniformToFill };
    private readonly Grid photo = new() { ClipToBounds = true, Background = new SolidColorBrush(Color.FromArgb(100, 10, 27, 39)) };
    private bool motion = true;
    internal bool HasImage => portrait.Source is not null;
    public ExpanseCrew()
    {
        ClipToBounds = true; IsHitTestVisible = false;
        photo.Children.Add(portrait);
        photo.Children.Add(new Rectangle { Fill = new LinearGradientBrush(Colors.Transparent, Color.FromArgb(200, 3, 10, 17), 90) });
        Children.Add(photo);
        Unloaded += (_, _) => { portrait.BeginAnimation(OpacityProperty, null); portrait.Opacity = 1; };
        IsVisibleChanged += (_, _) => { if (!IsVisible) Settle(); };
    }
    private void Settle() { portrait.BeginAnimation(OpacityProperty, null); portrait.Opacity = 1; }
    public void SetMotion(bool enabled) { motion = enabled; if (!enabled || !SystemParameters.ClientAreaAnimation) Settle(); }
    public void Show(string ship)
    {
        var scene = ExpanseMedia.ForShip(ship);
        portrait.BeginAnimation(OpacityProperty, null); portrait.Opacity = 1;
        portrait.Source = ExpanseMedia.Image(scene.Portrait, 350);
        Visibility = HasImage ? Visibility.Visible : Visibility.Collapsed;
        ToolTip = scene.Character + "\n" + scene.Credit + "\n" + scene.Source;
        if (motion && IsVisible && SystemParameters.ClientAreaAnimation)
        {
            var fade = new DoubleAnimation(.25, 1, TimeSpan.FromSeconds(1)); Timeline.SetDesiredFrameRate(fade, 20);
            portrait.BeginAnimation(OpacityProperty, fade);
        }
    }
}
