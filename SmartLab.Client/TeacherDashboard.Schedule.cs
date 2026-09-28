using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SmartLab.Client;

public partial class TeacherDashboard
{
    private static readonly bool ScheduleHandlerRegistered = RegisterScheduleHandler();
    private TextBlock? _currentScheduleText;
    private DispatcherTimer? _scheduleSummaryTimer;

    private static bool RegisterScheduleHandler()
    {
        EventManager.RegisterClassHandler(
            typeof(TeacherDashboard),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(TeacherDashboardLoadedForSchedule));
        return true;
    }

    private static void TeacherDashboardLoadedForSchedule(object sender, RoutedEventArgs e)
    {
        if (sender is TeacherDashboard dashboard)
        {
            dashboard.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(async () => await dashboard.InitializeScheduleSummaryAsync()));
        }
    }

    private async Task InitializeScheduleSummaryAsync()
    {
        if (_currentScheduleText == null && TeacherNameText.Parent is StackPanel header)
        {
            _currentScheduleText = new TextBlock
            {
                Text = "Current schedule: loading...",
                FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondary"),
                Margin = new Thickness(0, 3, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            int index = header.Children.IndexOf(TeacherNameText);
            header.Children.Insert(Math.Max(0, index + 1), _currentScheduleText);
        }

        _scheduleSummaryTimer ??= CreateScheduleSummaryTimer();
        _scheduleSummaryTimer.Start();
        await LoadCurrentScheduleSummaryAsync();
    }

    private DispatcherTimer CreateScheduleSummaryTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        timer.Tick += async (_, _) => await LoadCurrentScheduleSummaryAsync();
        return timer;
    }

    private async Task LoadCurrentScheduleSummaryAsync()
    {
        if (_currentScheduleText == null)
            return;

        try
        {
            List<TeacherCurrentSchedule> schedules =
                await _httpClient.GetFromJsonAsync<List<TeacherCurrentSchedule>>("api/Schedule/current") ?? new();

            TeacherCurrentSchedule? current = schedules.OrderBy(s => s.StartTime).FirstOrDefault();
            _currentScheduleText.Text = current == null
                ? "Current schedule: no scheduled class right now"
                : $"Current schedule: {current.LaboratoryName} | {current.SubjectName} | {current.ClassName} | {current.StartTime:hh\\:mm}–{current.EndTime:hh\\:mm}";
        }
        catch
        {
            _currentScheduleText.Text = "Current schedule: unavailable";
        }
    }

    private sealed class TeacherCurrentSchedule
    {
        public int ScheduleId { get; set; }
        public string LaboratoryName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }
}
