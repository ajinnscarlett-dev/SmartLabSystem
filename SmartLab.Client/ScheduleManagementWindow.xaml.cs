using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;

namespace SmartLab.Client;

public partial class ScheduleManagementWindow : Window
{
    private readonly HttpClient _httpClient;
    private List<TeacherScheduleRow> _schedules = new();
    private List<TeacherOption> _teachers = new();
    private List<LaboratoryOption> _laboratories = new();

    public ScheduleManagementWindow(Window owner)
    {
        InitializeComponent();
        Owner = owner;
        _httpClient = new HttpClient { BaseAddress = new Uri(SmartLabServerConfig.BaseUrl) };
        AuthSession.Apply(_httpClient);
        ScheduleDatePicker.SelectedDate = DateTime.Today;
        ScheduleDatePicker.SelectedDateChanged += async (_, _) => await LoadSchedulesAsync();
        Loaded += async (_, _) =>
        {
            await LoadOptionsAsync();
            await LoadSchedulesAsync();
        };
    }

    private async Task LoadOptionsAsync()
    {
        try
        {
            List<AccountRow> accounts =
                await _httpClient.GetFromJsonAsync<List<AccountRow>>("api/User/management") ?? new();

            _teachers = accounts
                .Where(a =>
                    a.Role.Equals("Teacher", StringComparison.OrdinalIgnoreCase) &&
                    a.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                .Select(a => new TeacherOption
                {
                    UserId = a.UserId,
                    DisplayName = string.IsNullOrWhiteSpace(a.FullName)
                        ? (a.Username ?? string.Empty)
                        : $"{a.FullName} ({a.Username})"
                })
                .OrderBy(a => a.DisplayName)
                .ToList();

            _laboratories =
                await _httpClient.GetFromJsonAsync<List<LaboratoryOption>>("api/Laboratory") ?? new();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Option load error: {ex.Message}";
        }
    }

    private async Task LoadSchedulesAsync()
    {
        try
        {
            DateTime date = ScheduleDatePicker.SelectedDate?.Date ?? DateTime.Today;
            _schedules =
                await _httpClient.GetFromJsonAsync<List<TeacherScheduleRow>>(
                    $"api/Schedule?date={date:yyyy-MM-dd}") ?? new();

            foreach (TeacherScheduleRow row in _schedules)
                row.TimeText = $"{row.StartTime.ToString(@"hh\:mm")}–{row.EndTime.ToString(@"hh\:mm")}";

            ScheduleGrid.ItemsSource = _schedules;
            StatusText.Text = $"{_schedules.Count:N0} schedule(s) loaded for {date:yyyy-MM-dd}.";
        }
        catch (Exception ex)
        {
            ScheduleGrid.ItemsSource = new List<TeacherScheduleRow>();
            StatusText.Text = $"Schedule load error: {ex.Message}";
        }
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e) =>
        await EditScheduleAsync(null);

    private async void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TeacherScheduleRow row })
            await EditScheduleAsync(row);
    }

    private async Task EditScheduleAsync(TeacherScheduleRow? existing)
    {
        if (_teachers.Count == 0 || _laboratories.Count == 0)
        {
            await LoadOptionsAsync();

            if (_teachers.Count == 0 || _laboratories.Count == 0)
            {
                MessageBox.Show(
                    "An active Teacher and at least one laboratory are required before a schedule can be created.",
                    "Schedule Management",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        Window dialog = new()
        {
            Title = existing == null ? "Add Schedule" : "Edit Schedule",
            Width = 600,
            Height = 650,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = (System.Windows.Media.Brush)FindResource("SmartLabPageBackground")
        };

        Grid root = new() { Margin = new Thickness(20) };
        for (int i = 0; i < 9; i++)
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock
        {
            Text = existing == null ? "ADD SCHEDULE" : "EDIT SCHEDULE",
            FontSize = 19,
            FontWeight = FontWeights.SemiBold
        });

        ComboBox teacher = new()
        {
            ItemsSource = _teachers,
            DisplayMemberPath = "DisplayName"
        };
        AddField(root, 1, "Teacher", teacher);

        ComboBox laboratory = new()
        {
            ItemsSource = _laboratories,
            DisplayMemberPath = "LabName"
        };
        AddField(root, 2, "Laboratory", laboratory);

        TextBox subject = new();
        AddField(root, 3, "Subject", subject);

        DatePicker date = new()
        {
            SelectedDate = existing?.ScheduleDate ?? ScheduleDatePicker.SelectedDate ?? DateTime.Today,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        AddField(root, 4, "Date", date);

        StackPanel times = new()
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal
        };
        TextBox start = new()
        {
            Width = 105,
            Text = existing == null ? "08:00" : existing.StartTime.ToString(@"hh:mm")
        };
        TextBox end = new()
        {
            Width = 105,
            Margin = new Thickness(8, 0, 0, 0),
            Text = existing == null ? "09:00" : existing.EndTime.ToString(@"hh:mm")
        };
        times.Children.Add(new TextBlock
        {
            Text = "Start",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (System.Windows.Media.Brush)FindResource("SmartLabMuted")
        });
        times.Children.Add(start);
        times.Children.Add(new TextBlock
        {
            Text = "End",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (System.Windows.Media.Brush)FindResource("SmartLabMuted"),
            Margin = new Thickness(12, 0, 0, 0)
        });
        times.Children.Add(end);
        AddField(root, 5, "Time", times);

        TextBox className = new()
        {
            Text = existing?.ClassName ?? string.Empty
        };
        AddField(root, 6, "Class", className);

        ComboBox status = new();
        status.Items.Add("Scheduled");
        status.Items.Add("Completed");
        status.Items.Add("Cancelled");
        status.SelectedItem = existing?.Status ?? "Scheduled";
        AddField(root, 7, "Status", status);

        if (existing != null)
        {
            teacher.SelectedValue = existing.TeacherUserId;
            laboratory.SelectedValue = existing.LaboratoryId;
            subject.Text = existing.SubjectName;
        }
        else
        {
            teacher.SelectedIndex = 0;
            laboratory.SelectedIndex = 0;
        }

        StackPanel footer = new()
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };

        Button cancel = new()
        {
            Content = "CANCEL",
            Width = 90,
            Height = 34,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Button save = new()
        {
            Content = existing == null ? "CREATE" : "SAVE",
            Width = 90,
            Height = 34,
            Style = (System.Windows.Style)FindResource("SmartLabPrimaryButton")
        };

        footer.Children.Add(cancel);
        footer.Children.Add(save);
        Grid.SetRow(footer, 8);
        root.Children.Add(footer);

        cancel.Click += (_, _) => dialog.Close();

        save.Click += async (_, _) =>
        {
            if (teacher.SelectedItem is not TeacherOption selectedTeacher ||
                laboratory.SelectedItem is not LaboratoryOption selectedLab ||
                date.SelectedDate == null ||
                string.IsNullOrWhiteSpace(subject.Text) ||
                string.IsNullOrWhiteSpace(className.Text) ||
                !TimeSpan.TryParse(start.Text.Trim(), out TimeSpan startTime) ||
                !TimeSpan.TryParse(end.Text.Trim(), out TimeSpan endTime) ||
                endTime <= startTime)
            {
                MessageBox.Show(
                    "Enter a Teacher, laboratory, subject, class, valid date, and start/end time where end is later than start.",
                    "Schedule Management",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var payload = new
            {
                TeacherUserId = selectedTeacher.UserId,
                LaboratoryId = selectedLab.LaboratoryId,
                SubjectName = subject.Text.Trim(),
                ClassName = className.Text.Trim(),
                ScheduleDate = date.SelectedDate.Value.Date,
                StartTime = startTime,
                EndTime = endTime,
                Status = status.SelectedItem?.ToString() ?? "Scheduled"
            };

            save.IsEnabled = false;

            try
            {
                HttpResponseMessage response = existing == null
                    ? await _httpClient.PostAsJsonAsync("api/Schedule", payload)
                    : await _httpClient.PutAsJsonAsync($"api/Schedule/{existing.ScheduleId}", payload);

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        await response.Content.ReadAsStringAsync(),
                        "Schedule Management",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                dialog.Close();
                await LoadSchedulesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Schedule Management",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                save.IsEnabled = true;
            }
        };

        dialog.Content = root;
        dialog.ShowDialog();
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TeacherScheduleRow row })
            return;

        MessageBoxResult confirm = MessageBox.Show(
            $"Delete the schedule for {row.TeacherName} in {row.LaboratoryName} at {row.TimeText}?",
            "Schedule Management",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            HttpResponseMessage response =
                await _httpClient.DeleteAsync($"api/Schedule/{row.ScheduleId}");

            if (!response.IsSuccessStatusCode)
            {
                MessageBox.Show(
                    await response.Content.ReadAsStringAsync(),
                    "Schedule Management",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            await LoadSchedulesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Schedule Management",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        await LoadSchedulesAsync();

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        Close();

    private void AddField(Grid root, int row, string label, FrameworkElement control)
    {
        StackPanel field = new();
        field.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = (System.Windows.Media.Brush)FindResource("SmartLabMuted"),
            Margin = new Thickness(0, 0, 0, 3)
        });
        field.Children.Add(control);
        field.Margin = new Thickness(0, 8, 0, 0);
        Grid.SetRow(field, row);
        root.Children.Add(field);
    }

    private sealed class AccountRow
    {
        public int UserId { get; set; }
        public string? Username { get; set; }
        public string? FullName { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    private sealed class TeacherOption
    {
        public int UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    private sealed class LaboratoryOption
    {
        public int LaboratoryId { get; set; }
        public string LabName { get; set; } = string.Empty;
    }

    private sealed class TeacherScheduleRow
    {
        public int ScheduleId { get; set; }
        public int TeacherUserId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public int LaboratoryId { get; set; }
        public string LaboratoryName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public DateTime ScheduleDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Status { get; set; } = "Scheduled";
        public string TimeText { get; set; } = string.Empty;
    }
}
