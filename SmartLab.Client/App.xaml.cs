using System;
using System.Windows;

namespace SmartLab.Client
{
    public partial class App : Application
    {
        private MachinePresenceService? _machinePresenceService;

        protected override void OnStartup(StartupEventArgs e)
        {
            ShutdownMode = ShutdownMode.OnLastWindowClose;

            base.OnStartup(e);

            _machinePresenceService = new MachinePresenceService();
            _machinePresenceService.Start();

            MainWindow loginWindow = new MainWindow();
            MainWindow = loginWindow;
            loginWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _machinePresenceService?.Dispose();
            _machinePresenceService = null;
            base.OnExit(e);
        }
    }
}
