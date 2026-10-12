using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace SensorPanel;

internal sealed class PollingDialog : Window
{
    public PollingDialog(PanelWindow owner, int current, Action<int> apply)
    {
        Title = "Sensor polling — FAN MADE"; Icon = PanelBranding.WindowIcon;
        Owner = owner; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 490; Height = 310; ResizeMode = ResizeMode.NoResize;
        var content = new StackPanel { Margin = new Thickness(24) }; Content = content;
        content.Children.Add(new TextBlock { Text = "Sensor polling interval", FontSize = 22, FontWeight = FontWeights.SemiBold });
        content.Children.Add(new TextBlock { Text = "Milliseconds · 500–5000", Margin = new Thickness(0,12,0,6) });
        var input = new TextBox { Text = current.ToString(CultureInfo.InvariantCulture), FontSize = 20, Padding = new Thickness(6) };
        content.Children.Add(input);
        content.Children.Add(new TextBlock { Text = "500–1000 ms recommended. Lower intervals increase sensor access. Artwork keeps its own animation timing.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,6) });
        var error = new TextBlock { Foreground = System.Windows.Media.Brushes.DarkRed, Height = 22 }; content.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var reset = new Button { Content = "1000 ms default", Padding = new Thickness(10,5,10,5), Margin = new Thickness(0,0,8,0) };
        reset.Click += (_, _) => input.Text = "1000"; buttons.Children.Add(reset);
        var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(14,5,14,5), Margin = new Thickness(0,0,8,0) }; buttons.Children.Add(cancel);
        var save = new Button { Content = "Apply", IsDefault = true, Padding = new Thickness(14,5,14,5) };
        save.Click += (_, _) =>
        {
            if (!int.TryParse(input.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value is < 500 or > 5000) { error.Text = "Enter a whole number from 500 to 5000."; return; }
            try { apply(value); DialogResult = true; }
            catch (Exception ex) { error.Text = ex.Message; }
        };
        buttons.Children.Add(save); content.Children.Add(buttons);
        Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
    }
}
