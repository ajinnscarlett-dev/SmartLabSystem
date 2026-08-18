using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SmartLab.Client
{
    public partial class AdminDashboard
    {
        // ==========================================================
        // BLANK SCREEN BUTTON ROUTING
        // ==========================================================
        // AdminDashboard.Laboratory.cs already owns the single
        // static AdminDashboard() constructor.
        //
        // This uses a static field initializer instead, so the
        // Blank Screen handler can be registered without creating
        // a second static AdminDashboard() constructor.
        // ==========================================================

        private static readonly bool
            _blankScreenClassHandlerRegistered =
                RegisterBlankScreenClassHandler();

        private static bool RegisterBlankScreenClassHandler()
        {
            EventManager.RegisterClassHandler(
                typeof(Button),
                ButtonBase.ClickEvent,
                new RoutedEventHandler(
                    AdminButtonClickClassHandler),
                true);

            return true;
        }

        private static void AdminButtonClickClassHandler(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                !string.Equals(
                    button.Name,
                    "BlankScreenButton",
                    System.StringComparison.Ordinal))
            {
                return;
            }

            if (Window.GetWindow(button)
                is not AdminDashboard dashboard)
            {
                return;
            }

            // The XAML keeps its original click binding.
            // This handler intercepts only BlankScreenButton.
            e.Handled = true;

            dashboard.BlankScreenButton_Click(
                button,
                e);
        }
    }
}
