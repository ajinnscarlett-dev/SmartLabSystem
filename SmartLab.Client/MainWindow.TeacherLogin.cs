using System.Windows;

namespace SmartLab.Client;

public partial class MainWindow
{
    private void TeacherLoginButton_Click(object sender, RoutedEventArgs e)
    {
        var loginWindow = new TeacherLoginWindow(this);
        loginWindow.ShowDialog();
    }
}
