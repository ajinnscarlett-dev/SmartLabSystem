using System;
using System.Windows;
using System.Windows.Threading;

namespace SmartLab.Client
{
    public partial class App : Application
    {
        private MachinePresenceService? _machinePresenceService;
        private DispatcherTimer? _windowLifetimeTimer;

        protected override void OnStartup(StartupEventArgs e)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);

            _machinePresenceService = new MachinePresenceService();
            _machinePresenceService.Start();

            _windowLifetimeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };

            _windowLifetimeTimer.Tick += WindowLifetimeTimer_Tick;
            _windowLifetimeTimer.Start();
        }

        private void WindowLifetimeTimer_Tick(
            object? sender,
            EventArgs e)
        {
            bool hasVisibleWindow = false;

            foreach (Window window in Windows)
            {
                if (window.IsVisible)
                {
                    hasVisibleWindow = true;
                    break;
                }
            }

            if (!hasVisibleWindow)
            {
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _windowLifetimeTimer?.Stop();
            _windowLifetimeTimer = null;

            _machinePresenceService?.Dispose();
            _machinePresenceService = null;

            base.OnExit(e);
        }
    }
}
