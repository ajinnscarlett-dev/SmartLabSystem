using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SmartLab.Client
{
    // ==========================================================
    // ADMIN DASHBOARD - COMLAB SELECTOR / FILTER REFRESH
    // ==========================================================
    //
    // This partial class owns the laboratory refresh timer.
    //
    // Important live-screen fix:
    // The PC grid is NOT rebuilt every 5 seconds when the PC set/state
    // has not changed. This prevents the actual screen thumbnail from
    // disappearing and being replaced by the green placeholder.
    //
    // ==========================================================

    public partial class AdminDashboard
    {
        private int? _selectedLaboratoryId;

        private bool _laboratorySelectorAttached;

        private ComboBox? _laboratorySelector;

        private DispatcherTimer? _laboratoryRefreshTimer;

        private bool _laboratoryRefreshInProgress;

        // Last state that was actually rendered into the PC grid.
        private readonly Dictionary<int, string>
            _lastRenderedPcStates =
                new Dictionary<int, string>();

        static AdminDashboard()
        {
            EventManager.RegisterClassHandler(
                typeof(AdminDashboard),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(
                    AdminDashboardLoadedClassHandler));
        }

        private static void AdminDashboardLoadedClassHandler(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not AdminDashboard dashboard)
            {
                return;
            }

            dashboard.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(async () =>
                {
                    dashboard.InitializePcSidebar();
                    dashboard.InitializeScreenMonitoring();

                    await dashboard.AttachLaboratorySelectorAsync();
                }));
        }

        // ==========================================================
        // ATTACH SELECTOR
        // ==========================================================

        private async Task AttachLaboratorySelectorAsync()
        {
            if (_laboratorySelectorAttached)
            {
                return;
            }

            _laboratorySelector =
                FindHeaderLaboratoryComboBox();

            if (_laboratorySelector == null)
            {
                ApiStatusText.Text =
                    "Laboratory selector could not be found.";

                return;
            }

            _laboratorySelectorAttached = true;

            _refreshTimer.Stop();

            _laboratoryRefreshTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromSeconds(5)
                };

            _laboratoryRefreshTimer.Tick +=
                LaboratoryRefreshTimer_Tick;

            _laboratoryRefreshTimer.Start();

            _laboratorySelector.Items.Clear();

            _laboratorySelector.SelectionChanged +=
                LaboratorySelector_SelectionChanged;

            await LoadLaboratoriesIntoSelectorAsync();
        }

        // ==========================================================
        // FIND HEADER COMBOBOX
        // ==========================================================

        private ComboBox? FindHeaderLaboratoryComboBox()
        {
            return FindVisualChildren<ComboBox>(this)
                .FirstOrDefault();
        }

        private static IEnumerable<T>
            FindVisualChildren<T>(
                DependencyObject dependencyObject)
            where T : DependencyObject
        {
            if (dependencyObject == null)
            {
                yield break;
            }

            int childCount =
                VisualTreeHelper.GetChildrenCount(
                    dependencyObject);

            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child =
                    VisualTreeHelper.GetChild(
                        dependencyObject,
                        i);

                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (T descendant in
                    FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        // ==========================================================
        // LOAD LABORATORIES
        // ==========================================================

        private async Task
            LoadLaboratoriesIntoSelectorAsync()
        {
            try
            {
                var laboratories =
                    await _httpClient
                        .GetFromJsonAsync<
                            List<LaboratorySelectorItem>>(
                                "api/Laboratory");

                if (laboratories == null)
                {
                    ApiStatusText.Text =
                        "No laboratories found.";

                    return;
                }

                _laboratorySelector!.Items.Clear();

                _laboratorySelector.Items.Add(
                    new LaboratorySelectorItem
                    {
                        LaboratoryId = null,
                        LabName = "All Laboratories"
                    });

                foreach (LaboratorySelectorItem laboratory
                    in laboratories)
                {
                    _laboratorySelector.Items.Add(
                        laboratory);
                }

                LaboratorySelectorItem?
                    comlab601 =
                        laboratories.FirstOrDefault(
                            l =>
                                string.Equals(
                                    l.LabName,
                                    "ComLab 601",
                                    StringComparison
                                        .OrdinalIgnoreCase));

                if (comlab601 != null)
                {
                    _selectedLaboratoryId =
                        comlab601.LaboratoryId;

                    _laboratorySelector.SelectedItem =
                        comlab601;

                    await LoadSelectedLaboratoryPCsAsync(
                        _selectedLaboratoryId);
                }
                else
                {
                    _selectedLaboratoryId = null;

                    _laboratorySelector.SelectedIndex = 0;

                    await LoadAllPCsForLaboratoryRefreshAsync();
                }
            }
            catch (Exception ex)
            {
                ApiStatusText.Text =
                    $"Laboratory load error: {ex.Message}";
            }
        }

        // ==========================================================
        // SELECTOR CHANGED
        // ==========================================================

        private async void
            LaboratorySelector_SelectionChanged(
                object sender,
                SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
            {
                return;
            }

            if (e.AddedItems[0] is not
                LaboratorySelectorItem selectedLaboratory)
            {
                return;
            }

            _selectedLaboratoryId =
                selectedLaboratory.LaboratoryId;

            _lastRenderedPcStates.Clear();

            if (_laboratoryRefreshTimer == null)
            {
                return;
            }

            if (_laboratoryRefreshInProgress)
            {
                return;
            }

            if (_selectedLaboratoryId.HasValue)
            {
                await LoadSelectedLaboratoryPCsAsync(
                    _selectedLaboratoryId);
            }
            else
            {
                await LoadAllPCsForLaboratoryRefreshAsync();
            }
        }

        // ==========================================================
        // REFRESH TIMER
        // ==========================================================

        private async void
            LaboratoryRefreshTimer_Tick(
                object? sender,
                EventArgs e)
        {
            if (_laboratoryRefreshInProgress)
            {
                return;
            }

            if (!IsLoaded)
            {
                return;
            }

            if (_selectedLaboratoryId.HasValue)
            {
                await LoadSelectedLaboratoryPCsAsync(
                    _selectedLaboratoryId);
            }
            else
            {
                await LoadAllPCsForLaboratoryRefreshAsync();
            }
        }

        // ==========================================================
        // REFRESH ALL PCS
        // ==========================================================

        private async Task
            LoadAllPCsForLaboratoryRefreshAsync()
        {
            if (_laboratoryRefreshInProgress)
            {
                return;
            }

            _laboratoryRefreshInProgress = true;

            try
            {
                var pcs =
                    await _httpClient
                        .GetFromJsonAsync<
                            List<PCInfo>>(
                                "api/PC");

                RenderFilteredPCGrid(
                    pcs ?? new List<PCInfo>());

                ApiStatusText.Text =
                    $"All laboratories • " +
                    $"Updated {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                ApiStatusText.Text =
                    $"PC load error: {ex.Message}";
            }
            finally
            {
                _laboratoryRefreshInProgress = false;
            }
        }

        // ==========================================================
        // REFRESH SELECTED LABORATORY
        // ==========================================================

        private async Task
            LoadSelectedLaboratoryPCsAsync(
                int? laboratoryId)
        {
            if (!laboratoryId.HasValue)
            {
                await LoadAllPCsForLaboratoryRefreshAsync();
                return;
            }

            if (_laboratoryRefreshInProgress)
            {
                return;
            }

            _laboratoryRefreshInProgress = true;

            try
            {
                var pcs =
                    await _httpClient
                        .GetFromJsonAsync<
                            List<PCInfo>>(
                                $"api/Laboratory/{laboratoryId.Value}/pcs");

                RenderFilteredPCGrid(
                    pcs ?? new List<PCInfo>());

                ApiStatusText.Text =
                    $"ComLab filter active • " +
                    $"Updated {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                ApiStatusText.Text =
                    $"Laboratory PC load error: {ex.Message}";
            }
            finally
            {
                _laboratoryRefreshInProgress = false;
            }
        }

        // ==========================================================
        // CHECK WHETHER THE GRID REALLY NEEDS REBUILDING
        // ==========================================================

        private bool NeedsPcGridRebuild(
            List<PCInfo> pcs)
        {
            if (_lastRenderedPcStates.Count !=
                pcs.Count)
            {
                return true;
            }

            foreach (PCInfo pc in pcs)
            {
                string state =
                    BuildPcRenderState(pc);

                if (!_lastRenderedPcStates.TryGetValue(
                        pc.PcId,
                        out string? previousState))
                {
                    return true;
                }

                if (!string.Equals(
                        previousState,
                        state,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildPcRenderState(
            PCInfo pc)
        {
            return string.Join(
                "|",
                pc.PcId,
                pc.PcNumber,
                pc.Status,
                pc.CurrentUserId?.ToString() ?? "",
                pc.IsEnabled);
        }

        // ==========================================================
        // RENDER PC GRID
        // ==========================================================

        private void RenderFilteredPCGrid(
            List<PCInfo> pcs)
        {
            int occupied = 0;
            int available = 0;
            int maintenance = 0;
            int offline = 0;

            foreach (PCInfo pc in pcs)
            {
                if (pc.Status.Equals(
                    "Available",
                    StringComparison.OrdinalIgnoreCase))
                {
                    available++;
                }
                else if (
                    pc.Status.Equals(
                        "Occupied",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    pc.Status.Equals(
                        "In Use",
                        StringComparison.OrdinalIgnoreCase))
                {
                    occupied++;

                    if (!pc.LastSeen.HasValue ||
                        (DateTime.Now -
                         pc.LastSeen.Value).TotalSeconds > 10)
                    {
                        offline++;
                    }
                }
                else if (
                    pc.Status.Equals(
                        "Maintenance",
                        StringComparison.OrdinalIgnoreCase))
                {
                    maintenance++;
                }
                else
                {
                    offline++;
                }
            }

            // ------------------------------------------------------
            // IMPORTANT:
            // Do not clear/rebuild the visual tree if only LastSeen
            // changed. The live screenshot remains inside the card.
            // ------------------------------------------------------

            bool rebuild =
                NeedsPcGridRebuild(pcs);

            if (rebuild)
            {
                PcGrid.Children.Clear();

                foreach (PCInfo pc in pcs)
                {
                    Border card =
                        CreatePcCard(pc);

                    PcGrid.Children.Add(card);

                    // Re-apply cached screenshot immediately after
                    // the card is recreated.
                    ApplyCachedScreenToCard(
                        card,
                        pc.PcId);

                    _lastRenderedPcStates[pc.PcId] =
                        BuildPcRenderState(pc);
                }
            }

            TotalPcText.Text =
                pcs.Count.ToString();

            OccupiedPcText.Text =
                occupied.ToString();

            AvailablePcText.Text =
                available.ToString();

            MaintenancePcText.Text =
                maintenance.ToString();

            OfflinePcText.Text =
                offline.ToString();
        }
    }

    // ==========================================================
    // LABORATORY SELECTOR ITEM
    // ==========================================================

    public class LaboratorySelectorItem
    {
        public int? LaboratoryId { get; set; }

        public string LabName { get; set; } =
            string.Empty;

        public override string ToString()
        {
            return LabName;
        }
    }
}
