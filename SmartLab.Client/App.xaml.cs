using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SmartLab.Client
{
    public partial class App : Application
    {
        private MachinePresenceService? _machinePresenceService;

        protected override void OnStartup(StartupEventArgs e)
        {
            ShutdownMode = ShutdownMode.OnLastWindowClose;

            DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += App_UnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += App_UnhandledException;

            base.OnStartup(e);

            _machinePresenceService = new MachinePresenceService();
            _machinePresenceService.Start();

            MainWindow loginWindow = new MainWindow();
            MainWindow = loginWindow;
            loginWindow.Show();
        }

        private void App_DispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            LogClientError("DispatcherUnhandledException", e.Exception);

            MessageBox.Show(
                "SmartLab encountered an unexpected error and remained open.\n\n" +
                e.Exception.Message +
                "\n\nSee the client error log for details.",
                "SmartLab Client Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;
        }

        private static void App_UnobservedTaskException(
            object? sender,
            UnobservedTaskExceptionEventArgs e)
        {
            LogClientError("UnobservedTaskException", e.Exception);
            e.SetObserved();
        }

        private static void App_UnhandledException(
            object sender,
            UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                LogClientError("AppDomainUnhandledException", exception);
            }
        }

        private static void LogClientError(
            string source,
            Exception exception)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SmartLab");

                Directory.CreateDirectory(folder);

                string path = Path.Combine(
                    folder,
                    "ClientError.log");

                File.AppendAllText(
                    path,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n" +
                    exception +
                    "\n\n");
            }
            catch
            {
                // Never let error logging terminate the client.
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _machinePresenceService?.Dispose();
            _machinePresenceService = null;
            base.OnExit(e);
        }
    }
}
