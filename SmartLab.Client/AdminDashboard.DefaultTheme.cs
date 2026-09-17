using System;
using System.Windows;

namespace SmartLab.Client;

public partial class AdminDashboard
{
    private static readonly bool DefaultThemeHandlerRegistered = RegisterDefaultThemeHandler();

    private static bool RegisterDefaultThemeHandler()
    {
        EventManager.RegisterClassHandler(
            typeof(AdminDashboard),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(ApplyLightDefaultTheme));
        return true;
    }

    private static void ApplyLightDefaultTheme(object sender, RoutedEventArgs e)
    {
        if (sender is not AdminDashboard dashboard)
            return;

        dashboard._isDarkTheme = false;
        if (dashboard.ThemeToggleButton != null)
            dashboard.ThemeToggleButton.Content = "DARK";
    }
}
