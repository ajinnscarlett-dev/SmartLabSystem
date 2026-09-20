using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SmartLab.Client
{
    // ==========================================================
    // ADMIN DASHBOARD - PC SIDEBAR ON CLICK
    // ==========================================================
    //
    // IMPORTANT:
    // AdminDashboard.Laboratory.cs already owns the single
    // AdminDashboard static constructor / Loaded class handler.
    //
    // Therefore this partial class MUST NOT declare another
    // static constructor.
    //
    // The laboratory partial now calls:
    // InitializePcSidebar()
    //
    // ==========================================================

    public partial class AdminDashboard
    {
        private bool _pcSidebarHooked;

        private const double PcSidebarWidth = 330;

        // ==========================================================
        // INITIALIZE SIDEBAR
        // ==========================================================

        private void InitializePcSidebar()
        {
            HidePcDetailsPanelUntilSelection();
            HookPcGridClick();
        }

        // ==========================================================
        // HIDE PANEL
        // ==========================================================

        private void HidePcDetailsPanelUntilSelection()
        {
            RightSidebarColumn.Width =
                new GridLength(0);

            RightSidebarColumn.MinWidth = 0;
            RightSidebarColumn.MaxWidth =
                double.PositiveInfinity;
        }

        // ==========================================================
        // SHOW PANEL
        // ==========================================================

        private void ShowPcDetailsPanel()
        {
            if (SelectedPcSidebar != null)
            {
                SelectedPcSidebar.Visibility = Visibility.Visible;
            }

            // The redesigned dashboard keeps the details panel in the main
            // operations workspace rather than relying on the legacy right column.
            RightSidebarColumn.Width = new GridLength(0);
        }

        // ==========================================================
        // HOOK PC GRID
        // ==========================================================

        private void HookPcGridClick()
        {
            if (_pcSidebarHooked)
            {
                return;
            }

            _pcSidebarHooked = true;

            PcGrid.AddHandler(
                UIElement.MouseLeftButtonUpEvent,
                new MouseButtonEventHandler(
                    PcGrid_MouseLeftButtonUp),
                true);
        }

        // ==========================================================
        // PC CARD CLICK
        // ==========================================================

        private void PcGrid_MouseLeftButtonUp(
            object sender,
            MouseButtonEventArgs e)
        {
            DependencyObject? source =
                e.OriginalSource as DependencyObject;

            if (source == null)
            {
                return;
            }

            DependencyObject? card =
                FindDirectChildOfPcGrid(source);

            if (card == null)
            {
                return;
            }

            // The existing dashboard code is responsible for
            // populating _selectedPc and updating the existing
            // right-side panel contents.
            //
            // We only control visibility here.
            ShowPcDetailsPanel();
        }

        // ==========================================================
        // FIND DIRECT CARD UNDER PC GRID
        // ==========================================================

        private DependencyObject?
            FindDirectChildOfPcGrid(
                DependencyObject source)
        {
            DependencyObject? current =
                source;

            while (current != null)
            {
                DependencyObject? parent =
                    GetParent(current);

                if (parent == PcGrid)
                {
                    return current;
                }

                if (current == PcGrid)
                {
                    return null;
                }

                current = parent;
            }

            return null;
        }

        // ==========================================================
        // GET PARENT
        // ==========================================================

        private static DependencyObject?
            GetParent(DependencyObject element)
        {
            if (element is FrameworkElement frameworkElement)
            {
                DependencyObject? logicalParent =
                    frameworkElement.Parent;

                if (logicalParent != null)
                {
                    return logicalParent;
                }
            }

            if (element is FrameworkContentElement contentElement)
            {
                DependencyObject? logicalParent =
                    contentElement.Parent;

                if (logicalParent != null)
                {
                    return logicalParent;
                }
            }

            return System.Windows.Media.VisualTreeHelper
                .GetParent(element);
        }

        // ==========================================================
        // OPTIONAL ESCAPE CLOSE
        // ==========================================================

        private void ClosePcSidebarOnEscape(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HidePcDetailsPanelUntilSelection();
                e.Handled = true;
            }
        }
    }
}
