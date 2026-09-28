using System.Windows;
using System.Windows.Threading;

namespace SmartLab.Client;

public partial class SmartLabWidget
{
    private static readonly bool _remotePerformanceHandlersRegistered = RegisterRemotePerformanceHandlers();
    private DispatcherTimer? _remotePerformanceWatchTimer;

    private static bool RegisterRemotePerformanceHandlers()
    {
        EventManager.RegisterClassHandler(
            typeof(SmartLabWidget),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(RemotePerformanceLoaded));

        EventManager.RegisterClassHandler(
            typeof(SmartLabWidget),
            FrameworkElement.UnloadedEvent,
            new RoutedEventHandler(RemotePerformanceUnloaded));

        return true;
    }

    private static void RemotePerformanceLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is SmartLabWidget widget)
        {
            widget.Dispatcher.BeginInvoke(
                new Action(widget.InitializeRemotePerformanceWatch),
                DispatcherPriority.Background);
        }
    }

    private static void RemotePerformanceUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is SmartLabWidget widget)
        {
            widget._remotePerformanceWatchTimer?.Stop();
            widget._remotePerformanceWatchTimer = null;
        }
    }

    private void InitializeRemotePerformanceWatch()
    {
        if (_remotePerformanceWatchTimer != null)
            return;

        _remotePerformanceWatchTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };

        _remotePerformanceWatchTimer.Tick += (_, _) =>
        {
            // Remote control needs prompt command pickup. Normal operation
            // keeps a slower polling rate to avoid unnecessary LAN traffic.
            if (_pcCommandTimer != null)
            {
                _pcCommandTimer.Interval = _remoteControlActive
                    ? TimeSpan.FromMilliseconds(100)
                    : TimeSpan.FromMilliseconds(500);
            }

            if (_screenUploadTimer != null)
            {
                _screenUploadTimer.Interval = _remoteControlActive
                    ? TimeSpan.FromMilliseconds(300)
                    : TimeSpan.FromMilliseconds(400);
            }
        };

        _remotePerformanceWatchTimer.Start();
    }
}
