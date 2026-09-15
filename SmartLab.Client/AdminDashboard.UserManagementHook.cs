using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SmartLab.Client;

public partial class AdminDashboard
{
    private static readonly bool UserManagementHookRegistered = RegisterUserManagementHook();

    private static bool RegisterUserManagementHook()
    {
        EventManager.RegisterClassHandler(
            typeof(Button),
            Button.ClickEvent,
            new RoutedEventHandler(HandleUserManagementNavigation),
            true);
        return true;
    }

    private static void HandleUserManagementNavigation(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Button button)
            return;

        if (button.Content is not Panel panel || !ContainsUsersText(panel))
            return;

        DependencyObject? current = button;
        while (current != null && current is not AdminDashboard)
            current = VisualTreeHelper.GetParent(current);

        if (current is not AdminDashboard dashboard)
            return;

        e.Handled = true;
        var window = new UserManagementWindow(dashboard);
        window.ShowDialog();
    }

    private static bool ContainsUsersText(DependencyObject element)
    {
        int count = VisualTreeHelper.GetChildrenCount(element);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(element, i);
            if (child is TextBlock text && text.Text == "Users")
                return true;
            if (ContainsUsersText(child))
                return true;
        }
        return false;
    }
}
