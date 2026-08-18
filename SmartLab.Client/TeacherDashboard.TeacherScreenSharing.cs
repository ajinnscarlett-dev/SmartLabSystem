using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SmartLab.Client
{
    public partial class TeacherDashboard
    {
        private Button? _teacherShareButton;
        private DispatcherTimer? _teacherShareUploadTimer;
        private ScreenCaptureService? _teacherShareCaptureService;
        private bool _teacherScreenSharingActive;
        private bool _teacherShareUploading;
        private int _teacherShareLaboratoryId;

        private static readonly bool
            _teacherScreenShareBootstrap =
                RegisterTeacherScreenShareHandler();

        private static bool
            RegisterTeacherScreenShareHandler()
        {
            EventManager.RegisterClassHandler(
                typeof(TeacherDashboard),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(
                    TeacherDashboardTeacherShareLoaded));

            return true;
        }

        private static void
            TeacherDashboardTeacherShareLoaded(
                object sender,
                RoutedEventArgs e)
        {
            if (sender is not TeacherDashboard dashboard)
            {
                return;
            }

            dashboard.InitializeTeacherScreenShareUi();

            dashboard.Closed -=
                dashboard.TeacherDashboardClosedForScreenShare;

            dashboard.Closed +=
                dashboard.TeacherDashboardClosedForScreenShare;
        }

        private void InitializeTeacherScreenShareUi()
        {
            if (_teacherShareButton != null)
            {
                return;
            }

            if (NotificationButton.Parent
                    is not Grid notificationGrid ||
                notificationGrid.Parent
                    is not StackPanel actionPanel)
            {
                return;
            }

            _teacherShareButton =
                new Button
                {
                    Content =
                        "SHARE MY SCREEN",

                    Width = 135,
                    Height = 36,

                    Margin =
                        new Thickness(
                            0,
                            0,
                            10,
                            0),

                    FontWeight =
                        FontWeights.Bold
                };

            _teacherShareButton.Click +=
                TeacherShareButton_Click;

            int insertIndex =
                Math.Min(
                    1,
                    actionPanel.Children.Count);

            actionPanel.Children.Insert(
                insertIndex,
                _teacherShareButton);
        }

        private async void TeacherShareButton_Click(
            object? sender,
            RoutedEventArgs e)
        {
            if (_teacherScreenSharingActive)
            {
                await StopTeacherScreenShareAsync();
                return;
            }

            await StartTeacherScreenShareAsync();
        }

        private async Task
            StartTeacherScreenShareAsync()
        {
            try
            {
                List<TeacherAuthorizedLaboratory> labs =
                    await _httpClient.GetFromJsonAsync<
                        List<TeacherAuthorizedLaboratory>>(
                            "api/TeacherAuthorization/my-laboratories")
                    ?? new List<TeacherAuthorizedLaboratory>();

                if (labs.Count == 0)
                {
                    MessageBox.Show(
                        "You are not authorized for any COMLAB.",
                        "SmartLab - Teacher Screen Sharing",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                TeacherAuthorizedLaboratory? selectedLab =
                    ShowLaboratoryPicker(
                        labs);

                if (selectedLab == null)
                {
                    return;
                }

                var response =
                    await _httpClient.PostAsJsonAsync(
                        $"api/TeacherScreenShare/" +
                        $"{selectedLab.LaboratoryId}/start",
                        new { });

                string responseText =
                    await response.Content
                        .ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        responseText,
                        "SmartLab - Teacher Screen Sharing",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                _teacherShareLaboratoryId =
                    selectedLab.LaboratoryId;

                _teacherScreenSharingActive =
                    true;

                _teacherShareCaptureService =
                    new ScreenCaptureService();

                _teacherShareUploadTimer =
                    new DispatcherTimer
                    {
                        Interval =
                            TimeSpan.FromMilliseconds(
                                800)
                    };

                _teacherShareUploadTimer.Tick +=
                    TeacherShareUploadTimer_Tick;

                _teacherShareUploadTimer.Start();

                _teacherShareButton!.Content =
                    "STOP SCREEN SHARING";

                _teacherShareButton.IsEnabled =
                    true;

                StatusText.Text =
                    $"Sharing teacher screen to " +
                    $"{selectedLab.LabName}";

                await UploadTeacherScreenFrameAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to start teacher screen sharing.\n\n" +
                    ex.Message,
                    "SmartLab - Teacher Screen Sharing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void
            TeacherShareUploadTimer_Tick(
                object? sender,
                EventArgs e)
        {
            await UploadTeacherScreenFrameAsync();
        }

        private async Task
            UploadTeacherScreenFrameAsync()
        {
            if (!_teacherScreenSharingActive ||
                _teacherShareUploading ||
                _teacherShareCaptureService == null ||
                _teacherShareLaboratoryId <= 0)
            {
                return;
            }

            _teacherShareUploading =
                true;

            try
            {
                byte[] image =
                    _teacherShareCaptureService
                        .CaptureScreen();

                using ByteArrayContent content =
                    new ByteArrayContent(
                        image);

                content.Headers.ContentType =
                    new System.Net.Http.Headers
                        .MediaTypeHeaderValue(
                            "image/jpeg");

                var response =
                    await _httpClient.PostAsync(
                        $"api/TeacherScreenShare/" +
                        $"{_teacherShareLaboratoryId}/frame",
                        content);

                if (!response.IsSuccessStatusCode)
                {
                    string message =
                        await response.Content
                            .ReadAsStringAsync();

                    StatusText.Text =
                        $"Teacher share upload failed: {message}";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    $"Teacher share upload failed: {ex.Message}";
            }
            finally
            {
                _teacherShareUploading =
                    false;
            }
        }

        private async Task
            StopTeacherScreenShareAsync()
        {
            if (!_teacherScreenSharingActive)
            {
                return;
            }

            _teacherScreenSharingActive =
                false;

            _teacherShareUploadTimer?.Stop();

            try
            {
                if (_teacherShareLaboratoryId > 0)
                {
                    await _httpClient.PostAsJsonAsync(
                        $"api/TeacherScreenShare/" +
                        $"{_teacherShareLaboratoryId}/stop",
                        new { });
                }
            }
            catch
            {
                // Keep UI responsive even if server is unavailable.
            }

            _teacherShareButton!.Content =
                "SHARE MY SCREEN";

            _teacherShareLaboratoryId =
                0;

            _teacherShareUploadTimer =
                null;

            _teacherShareCaptureService =
                null;

            StatusText.Text =
                $"Connected • Updated " +
                $"{DateTime.Now:HH:mm:ss}";
        }

        private async void
            TeacherDashboardClosedForScreenShare(
                object? sender,
                EventArgs e)
        {
            await StopTeacherScreenShareAsync();
        }

        private static
            TeacherAuthorizedLaboratory?
            ShowLaboratoryPicker(
                List<TeacherAuthorizedLaboratory> labs)
        {
            if (labs.Count == 1)
            {
                return labs[0];
            }

            Window dialog =
                new Window
                {
                    Title =
                        "SmartLab - Select COMLAB",

                    Width = 420,
                    Height = 230,

                    WindowStartupLocation =
                        WindowStartupLocation.CenterOwner,

                    ResizeMode =
                        ResizeMode.NoResize,

                    Background =
                        Brushes.White
                };

            Grid root =
                new Grid
                {
                    Margin =
                        new Thickness(
                            20)
                };

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });

            TextBlock title =
                new TextBlock
                {
                    Text =
                        "Share your screen to:",

                    FontSize = 18,

                    FontWeight =
                        FontWeights.Bold,

                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(
                                24,
                                37,
                                54)),

                    Margin =
                        new Thickness(
                            0,
                            0,
                            0,
                            12)
                };

            Grid.SetRow(
                title,
                0);

            root.Children.Add(
                title);

            ComboBox combo =
                new ComboBox
                {
                    ItemsSource =
                        labs,

                    DisplayMemberPath =
                        "LabName",

                    SelectedIndex =
                        0,

                    Height =
                        34
                };

            Grid.SetRow(
                combo,
                1);

            root.Children.Add(
                combo);

            StackPanel buttons =
                new StackPanel
                {
                    Orientation =
                        Orientation.Horizontal,

                    HorizontalAlignment =
                        HorizontalAlignment.Right,

                    Margin =
                        new Thickness(
                            0,
                            16,
                            0,
                            0)
                };

            TeacherAuthorizedLaboratory?
                selected = null;

            Button cancel =
                new Button
                {
                    Content =
                        "CANCEL",

                    Width = 90,
                    Height = 34,

                    Margin =
                        new Thickness(
                            0,
                            0,
                            8,
                            0)
                };

            cancel.Click +=
                (s, e) =>
                    dialog.Close();

            Button share =
                new Button
                {
                    Content =
                        "START SHARING",

                    Width = 125,
                    Height = 34
                };

            share.Click +=
                (s, e) =>
                {
                    selected =
                        combo.SelectedItem
                            as TeacherAuthorizedLaboratory;

                    dialog.DialogResult =
                        true;
                };

            buttons.Children.Add(cancel);
            buttons.Children.Add(share);

            Grid.SetRow(
                buttons,
                2);

            root.Children.Add(
                buttons);

            dialog.Content =
                root;

            dialog.ShowDialog();

            return selected;
        }

        private sealed class
            TeacherAuthorizedLaboratory
        {
            public int LaboratoryId { get; set; }

            public string LabName { get; set; } =
                string.Empty;

            public string? Description { get; set; }

            public override string ToString()
            {
                return LabName;
            }
        }
    }
}
