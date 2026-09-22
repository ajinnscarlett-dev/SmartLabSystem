using System;
using System.Net.Http;
using System.Windows;

namespace SmartLab.Client
{
    public partial class StudentDashboard : Window
    {
        private readonly HttpClient _httpClient;
        private readonly string _username;

        public StudentDashboard()
        {
            InitializeComponent();

            _username = string.IsNullOrWhiteSpace(AuthSession.Username)
                ? "Student"
                : AuthSession.Username;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(SmartLabServerConfig.BaseUrl)
            };
            AuthSession.Apply(_httpClient);

            if (WelcomeText != null)
                WelcomeText.Text = $"Welcome, {_username}";}

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                $"Student Profile\n\nUsername: {_username}\nRole: Student",
                "My Profile",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ChangePasswordWindow(_httpClient, _username)
                {
                    Owner = this
                };
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to open Change Password.\n\n{ex.Message}",
                    "SmartLab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to log out?",
                "Log Out",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            AuthSession.Clear();
            AuthSession.Apply(_httpClient);

            MainWindow loginWindow = new MainWindow();
            loginWindow.Show();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _httpClient.Dispose();
            base.OnClosed(e);
        }
    }
}