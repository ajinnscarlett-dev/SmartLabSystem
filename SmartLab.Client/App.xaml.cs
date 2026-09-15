using System;
using System.Windows;

namespace SmartLab.Client
{
    public partial class App : Application
    {
        private readonly MachinePresenceService _machinePresenceService =
            new MachinePresenceService();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            _machinePresenceService.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _machinePresenceService.Dispose();
            base.OnExit(e);
        }
    }
}
