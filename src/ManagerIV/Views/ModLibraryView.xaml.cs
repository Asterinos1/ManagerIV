using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ManagerIV.ViewModels;
using Wpf.Ui.Controls;

namespace ManagerIV.Views;

/// <summary>
/// Interaction logic for ModLibraryView.xaml
/// </summary>
public partial class ModLibraryView : UserControl
{
    private Point _dragStartPoint;

    public ModLibraryView()
    {
        InitializeComponent();
    }

    private void UserControl_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void UserControl_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            DragOverOverlay.Visibility = Visibility.Visible;
            if (Resources["FadeInOverlay"] is System.Windows.Media.Animation.Storyboard fadeStoryboard)
            {
                fadeStoryboard.Begin(this, true);
            }
            if (Resources["MarchingAnts"] is System.Windows.Media.Animation.Storyboard storyboard)
            {
                storyboard.Begin(this, true);
            }
        }
    }

    private void UserControl_DragLeave(object sender, DragEventArgs e)
    {
        HideDragOverlay();
    }

    private void UserControl_Drop(object sender, DragEventArgs e)
    {
        HideDragOverlay();
        HandleFileDrop(e);
    }

    private void HideDragOverlay()
    {
        DragOverOverlay.Visibility = Visibility.Collapsed;
        DragOverOverlay.Opacity = 0;
        if (Resources["MarchingAnts"] is System.Windows.Media.Animation.Storyboard storyboard)
        {
            storyboard.Stop(this);
        }
    }

    private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void ListBox_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var diff = _dragStartPoint - e.GetPosition(null);
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var listBox = (ListBox)sender;
                var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (item != null)
                {
                    var modVm = (ModViewModel)listBox.ItemContainerGenerator.ItemFromContainer(item);
                    if (modVm != null)
                    {
                        var dragData = new DataObject("ModViewModel", modVm);
                        DragDrop.DoDragDrop(item, dragData, DragDropEffects.Move);
                    }
                }
            }
        }
    }

    private void ListBox_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("ModViewModel"))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }
    }

    private void ListBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("ModViewModel"))
        {
            var droppedMod = (ModViewModel)e.Data.GetData("ModViewModel");
            var listBox = (ListBox)sender;
            var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (item != null)
            {
                var targetMod = (ModViewModel)listBox.ItemContainerGenerator.ItemFromContainer(item);
                if (targetMod != null)
                {
                    if (DataContext is MainViewModel vm)
                    {
                        vm.ReorderModCommand.Execute(new Tuple<ModViewModel, int>(droppedMod, targetMod.Priority));
                    }
                }
            }
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            HandleFileDrop(e);
        }
    }

    private void HandleFileDrop(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var validFiles = new System.Collections.Generic.List<string>();
            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".asi")
                {
                    validFiles.Add(file);
                }
            }

            if (validFiles.Count > 0 && DataContext is MainViewModel vm)
            {
                _ = vm.ImportArchivesAsync(validFiles);
            }
        }
    }

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (LibraryContentGrid == null || ListsContainerGrid == null || RightPanelGrid == null)
            return;

        // If the view width is narrow, switch to stacked rows.
        if (e.NewSize.Width < 980)
        {
            // Set 1-column layout
            LibraryContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            LibraryContentGrid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Pixel);
            
            LibraryContentGrid.RowDefinitions[0].Height = new GridLength(3, GridUnitType.Star);
            LibraryContentGrid.RowDefinitions[1].Height = new GridLength(2, GridUnitType.Star);

            Grid.SetColumn(ListsContainerGrid, 0);
            Grid.SetRow(ListsContainerGrid, 0);
            Grid.SetRowSpan(ListsContainerGrid, 1);

            Grid.SetColumn(RightPanelGrid, 0);
            Grid.SetRow(RightPanelGrid, 1);
            Grid.SetRowSpan(RightPanelGrid, 1);

            // Refine margins for 1-column stack
            ListsContainerGrid.Margin = new Thickness(0, 0, 0, 10);
            RightPanelGrid.Margin = new Thickness(0);
        }
        else
        {
            // Set 2-column layout with a wider list area.
            LibraryContentGrid.ColumnDefinitions[0].Width = new GridLength(7, GridUnitType.Star);
            LibraryContentGrid.ColumnDefinitions[1].Width = new GridLength(3, GridUnitType.Star);
            
            LibraryContentGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            LibraryContentGrid.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Pixel);

            Grid.SetColumn(ListsContainerGrid, 0);
            Grid.SetRow(ListsContainerGrid, 0);
            Grid.SetRowSpan(ListsContainerGrid, 2);

            Grid.SetColumn(RightPanelGrid, 1);
            Grid.SetRow(RightPanelGrid, 0);
            Grid.SetRowSpan(RightPanelGrid, 2);

            // Refine margins for 2-column side-by-side
            ListsContainerGrid.Margin = new Thickness(0, 0, 10, 0);
            RightPanelGrid.Margin = new Thickness(10, 0, 0, 0);
        }

        if (HeaderActionsPanel != null)
        {
            if (e.NewSize.Width < 720)
            {
                Grid.SetColumn(HeaderActionsPanel, 0);
                Grid.SetRow(HeaderActionsPanel, 1);
                HeaderActionsPanel.Margin = new Thickness(0, 10, 0, 0);
                HeaderActionsPanel.HorizontalAlignment = HorizontalAlignment.Left;
            }
            else
            {
                Grid.SetColumn(HeaderActionsPanel, 1);
                Grid.SetRow(HeaderActionsPanel, 0);
                HeaderActionsPanel.Margin = new Thickness(0);
                HeaderActionsPanel.HorizontalAlignment = HorizontalAlignment.Right;
            }
        }
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        do
        {
            if (current is T ancestor) return ancestor;
            current = VisualTreeHelper.GetParent(current);
        } while (current != null);
        return null;
    }
}

public class SeverityToInfoBarSeverityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string severityStr)
        {
            if (severityStr.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                return InfoBarSeverity.Warning;
            if (severityStr.Equals("Danger", StringComparison.OrdinalIgnoreCase))
                return InfoBarSeverity.Error;
        }
        return InfoBarSeverity.Informational;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
