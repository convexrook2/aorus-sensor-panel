using System.Windows;
using System.Windows.Controls;

namespace SensorPanel;

internal static class NavigationControls
{
    private static readonly ResourceDictionary styles = new()
        { Source=new Uri("pack://application:,,,/NavigationStyles.xaml") };
    internal static void Apply(ComboBox selector) => selector.Style=(Style)styles["NavigationSelector"];
}
