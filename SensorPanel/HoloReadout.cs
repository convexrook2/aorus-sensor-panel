using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace SensorPanel;

// Animate the presentation, never the reported number or sensor cadence.
internal sealed class HoloReadout : TextBlock
{
    private Brush ink;
    private bool pulseEnabled;
    private string previous = "";

    public HoloReadout(Brush ink, bool pulse)
    {
        this.ink = ink; pulseEnabled = pulse;
        Foreground = ink;
        if (ink is SolidColorBrush colour)
            Effect = new DropShadowEffect { Color = colour.Color, BlurRadius = 8, ShadowDepth = 0, Opacity = .28 };
        TargetUpdated += (_, _) => Refresh();
        IsVisibleChanged += (_, _) => { if (!IsVisible) Stop(); };
        Unloaded += (_, _) => Stop();
    }

    public void SetPulse(bool enabled) { pulseEnabled = enabled; if (!enabled) Stop(); }
    public void SetInk(Brush value)
    {
        ink=value; Foreground=Text.StartsWith('—') ? Brushes.SlateGray : ink;
        if (Effect is DropShadowEffect glow && value is SolidColorBrush colour) glow.Color=colour.Color;
    }
    private void Stop()
    {
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
    }
    private void Refresh()
    {
        bool unavailable = Text.StartsWith('—');
        Foreground = unavailable ? Brushes.SlateGray : ink;
        if (Text == previous) return;
        previous = Text;
        if (!pulseEnabled || !SystemParameters.ClientAreaAnimation || !IsVisible || unavailable) { Stop(); return; }
        var pulse = new DoubleAnimation(.62, 1, TimeSpan.FromMilliseconds(240)) { FillBehavior = FillBehavior.Stop };
        Timeline.SetDesiredFrameRate(pulse, 20);
        BeginAnimation(OpacityProperty, pulse, HandoffBehavior.SnapshotAndReplace);
    }
}
