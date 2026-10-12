using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SensorPanel;

internal static class PanelBranding
{
    public const string Name = "AORUS Sensor Panel — FAN MADE";
    private static readonly Uri IconUri = new("pack://application:,,,/Branding/AorusFanMade.ico");
    public static ImageSource WindowIcon => BitmapFrame.Create(IconUri);
    public static System.Drawing.Icon TrayIcon()
    {
        using var stream = Application.GetResourceStream(IconUri)?.Stream ?? throw new FileNotFoundException("Missing FAN MADE icon.");
        using var icon = new System.Drawing.Icon(stream, 32, 32);
        return (System.Drawing.Icon)icon.Clone();
    }
    public static void ShowReadme()
    {
        string file = Path.Combine(AppContext.BaseDirectory, "README.txt");
        var window = new Window
        {
            Title = Name + " — README", Icon = WindowIcon, Width = 820, Height = 680,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new TextBox { Text = File.Exists(file) ? File.ReadAllText(file) : "The installed README.txt is missing.",
                IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Segoe UI"), FontSize = 15, Padding = new Thickness(24), BorderThickness = new Thickness(0) }
        };
        window.Show();
    }
}

internal sealed class PanelTray : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon icon;
    private readonly System.Drawing.Icon artwork;
    private readonly System.Windows.Forms.ContextMenuStrip menu = new();
    public PanelTray(PanelWindow window)
    {
        artwork = PanelBranding.TrayIcon();
        void Restore() => window.ShowPanel();
        menu.Items.Add("Show panel", null, (_, _) => window.Dispatcher.Invoke(Restore));
        menu.Items.Add("Fit to display", null, (_, _) => window.Dispatcher.Invoke(window.FitToDisplay));
        menu.Items.Add("Hide to tray", null, (_, _) => window.Dispatcher.Invoke(window.Hide));
        menu.Items.Add("Open README", null, (_, _) => window.Dispatcher.Invoke(PanelBranding.ShowReadme));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => window.Dispatcher.Invoke(window.Close));
        icon = new System.Windows.Forms.NotifyIcon { Text = PanelBranding.Name, Icon = artwork, ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (_, _) => window.Dispatcher.Invoke(Restore);
    }
    public void Dispose() { icon.Visible = false; icon.Dispose(); menu.Dispose(); artwork.Dispose(); }
}
