using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SmartLab.Client
{
    public partial class AdminDashboard
    {
        private static readonly bool ThemeBrushSafetyRegistered = RegisterThemeBrushSafety();

        private static bool RegisterThemeBrushSafety()
        {
            EventManager.RegisterClassHandler(
                typeof(AdminDashboard),
                Button.ClickEvent,
                new RoutedEventHandler(PrepareThemeBrushes),
                handledEventsToo: false);

            return true;
        }

        private static void PrepareThemeBrushes(
            object sender,
            RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button button ||
                button.Name != "ThemeToggleButton")
            {
                return;
            }

            if (Window.GetWindow(button) is not AdminDashboard dashboard)
            {
                return;
            }

            string[] keys =
            {
                "Bg", "HeaderBg", "Panel", "Panel2", "Border", "Text",
                "Muted", "ButtonBg", "ButtonBorder", "InputBg", "GridBg", "GridAlt"
            };

            foreach (string key in keys)
            {
                if (dashboard.Resources[key] is SolidColorBrush brush)
                {
                    // DynamicResource usage may freeze the shared brush. Replace it
                    // with a writable equivalent before the existing theme handler
                    // changes its Color property.
                    dashboard.Resources[key] =
                        new SolidColorBrush(brush.Color);
                }
            }
        }
    }
}
