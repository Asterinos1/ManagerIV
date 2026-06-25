using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ManagerIV.ViewModels;

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
        var position = e.GetPosition(this);
        if (position.X < 0 || position.Y < 0 || position.X > ActualWidth || position.Y > ActualHeight)
        {
            HideDragOverlay();
        }
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
                    int newIndex = listBox.Items.IndexOf(targetMod);
                    if (DataContext is MainViewModel vm)
                    {
                        vm.ReorderModCommand.Execute(new Tuple<ModViewModel, int>(droppedMod, newIndex + 1));
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
                if (ext == ".zip" || ext == ".rar" || ext == ".7z")
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
        if (LibraryContentGrid == null || LibraryListCard == null || InspectorCard == null)
            return;

        // If the view width is narrow (< 800px), switch to stacked rows.
        if (e.NewSize.Width < 800)
        {
            // Set 1-column layout
            LibraryContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            LibraryContentGrid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Pixel);

            Grid.SetColumn(LibraryListCard, 0);
            Grid.SetRow(LibraryListCard, 0);
            Grid.SetRowSpan(LibraryListCard, 1);

            Grid.SetColumn(InspectorCard, 0);
            Grid.SetRow(InspectorCard, 1);
            Grid.SetRowSpan(InspectorCard, 1);

            // Refine margins for 1-column stack
            LibraryListCard.Margin = new Thickness(0, 0, 0, 15);
            InspectorCard.Margin = new Thickness(0, 0, 0, 0);
        }
        else
        {
            // Set 2-column layout (3* and 2* width)
            LibraryContentGrid.ColumnDefinitions[0].Width = new GridLength(3, GridUnitType.Star);
            LibraryContentGrid.ColumnDefinitions[1].Width = new GridLength(2, GridUnitType.Star);

            Grid.SetColumn(LibraryListCard, 0);
            Grid.SetRow(LibraryListCard, 0);
            Grid.SetRowSpan(LibraryListCard, 2);

            Grid.SetColumn(InspectorCard, 1);
            Grid.SetRow(InspectorCard, 0);
            Grid.SetRowSpan(InspectorCard, 2);

            // Refine margins for 2-column side-by-side
            LibraryListCard.Margin = new Thickness(0, 0, 10, 0);
            InspectorCard.Margin = new Thickness(10, 0, 0, 0);
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
