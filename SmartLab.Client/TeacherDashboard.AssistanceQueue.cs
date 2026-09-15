using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SmartLab.Client
{
    public partial class TeacherDashboard
    {
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Loaded += AddAssistanceQueueButton;
        }

        private void AddAssistanceQueueButton(object? sender, RoutedEventArgs e)
        {
            Loaded -= AddAssistanceQueueButton;

            foreach (Grid grid in FindVisualChildren<Grid>(this))
            {
                if (grid.RowDefinitions.Count != 0 || grid.ColumnDefinitions.Count != 0)
                    continue;

                if (grid.Children.Count != 2)
                    continue;
            }

            // The existing header contains a horizontal StackPanel in column 1.
            // Add one live queue button there without disturbing the established dashboard layout.
            StackPanel? headerActions = null;
            foreach (StackPanel panel in FindVisualChildren<StackPanel>(this))
            {
                if (panel.Orientation == Orientation.Horizontal && panel.Children.Count >= 3)
                {
                    headerActions = panel;
                    break;
                }
            }

            if (headerActions == null)
                return;

            var button = new Button
            {
                Content = "ASSISTANCE QUEUE",
                Width = 145,
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0),
                FontWeight = FontWeights.Bold
            };
            button.Click += AssistanceQueueButton_Click;
            headerActions.Children.Insert(Math.Max(0, headerActions.Children.Count - 1), button);
        }

        private void AssistanceQueueButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is TeacherAssistanceWindow existing && existing.IsVisible)
                {
                    existing.Activate();
                    return;
                }
            }

            new TeacherAssistanceWindow
            {
                Owner = this
            }.Show();
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null)
                yield break;

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is T typed)
                    yield return typed;

                foreach (T descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }
    }
}
