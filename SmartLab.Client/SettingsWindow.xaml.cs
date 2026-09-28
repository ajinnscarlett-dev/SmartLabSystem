using System.Reflection;
using System.Windows;

namespace SmartLab.Client;

public partial class SettingsWindow : Window
{
    private readonly string _username;

    public SettingsWindow(Window owner, string username)
    {
        InitializeComponent();
        Owner = owner;
        _username = username;

        UserText.Text = username;
        PcText.Text = string.IsNullOrWhiteSpace(PCConfig.PCNumber) ? "Not assigned" : PCConfig.PCNumber;
        RuntimeText.Text = $".NET {Environment.Version} • {Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Application build"}";
        ServerUrlText.Text = SmartLabServerConfig.BaseUrl;
    }

    private async void RefreshConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        ConnectionStatusText.Text = "Checking server...";
        StatusText.Text = "Resolving connection...";

        try
        {
            bool reachable = await SmartLabServerConfig.ResolveServerAsync();
            ServerUrlText.Text = SmartLabServerConfig.BaseUrl;
            ConnectionStatusText.Text = reachable ? "Server reachable" : "Server not reachable";
            StatusText.Text = reachable ? "Connection check completed." : "Connection could not be resolved.";
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = "Connection check failed";
            StatusText.Text = ex.Message;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
