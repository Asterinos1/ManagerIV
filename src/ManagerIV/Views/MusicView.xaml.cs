using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using ManagerIV.ViewModels;
using ManagerIV.Core;

namespace ManagerIV.Views;

public partial class MusicView : UserControl
{
    private Point _dragStartPoint;
    private DragGhostAdorner? _dragGhostAdorner;
    private ListBoxItem? _draggedItem;
    private ListBoxItem? _targetAdornedItem;
    private DropInsertionAdorner? _dropInsertionAdorner;

    public MusicView()
    {
        InitializeComponent();
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
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void UserControl_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void UserControl_DragLeave(object sender, DragEventArgs e)
    {
        HideDragOverlay();
        e.Handled = true;
    }

    private async void UserControl_Drop(object sender, DragEventArgs e)
    {
        HideDragOverlay();
        
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            
            // Filter audio files
            var audioFiles = files.Where(f => 
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                return ext == ".mp3" || ext == ".wma" || ext == ".m4a";
            }).ToArray();

            if (audioFiles.Length > 0 && DataContext is MainViewModel mainVm)
            {
                await mainVm.Music.ImportFilesAsync(audioFiles);
            }
        }
        e.Handled = true;
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
                    var track = (MusicTrack)listBox.ItemContainerGenerator.ItemFromContainer(item);
                    if (track != null)
                    {
                        var dragData = new DataObject("MusicTrack", track);
                        
                        _draggedItem = item;
                        _draggedItem.Opacity = 0.5;

                        var layer = AdornerLayer.GetAdornerLayer(listBox);
                        if (layer != null)
                        {
                            var offset = e.GetPosition(item);
                            _dragGhostAdorner = new DragGhostAdorner(listBox, item, offset);
                            layer.Add(_dragGhostAdorner);
                        }

                        DragDrop.DoDragDrop(item, dragData, DragDropEffects.Move);

                        if (_draggedItem != null)
                        {
                            _draggedItem.Opacity = 1.0;
                            _draggedItem = null;
                        }
                        if (_dragGhostAdorner != null && layer != null)
                        {
                            layer.Remove(_dragGhostAdorner);
                            _dragGhostAdorner = null;
                        }
                        RemoveInsertionAdorner();
                    }
                }
            }
        }
    }

    private void ListBox_DragOver(object sender, DragEventArgs e)
    {
        var listBox = (ListBox)sender;

        if (_dragGhostAdorner != null)
        {
            var pos = e.GetPosition(listBox);
            _dragGhostAdorner.UpdatePosition(pos);
        }

        if (e.Data.GetDataPresent("MusicTrack"))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;

            var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (item != null && item != _draggedItem)
            {
                var pos = e.GetPosition(item);
                bool isTopHalf = pos.Y < item.RenderSize.Height / 2;

                if (_targetAdornedItem != item)
                {
                    RemoveInsertionAdorner();
                    _targetAdornedItem = item;
                    var layer = AdornerLayer.GetAdornerLayer(_targetAdornedItem);
                    if (layer != null)
                    {
                        _dropInsertionAdorner = new DropInsertionAdorner(_targetAdornedItem, isTopHalf);
                        layer.Add(_dropInsertionAdorner);
                    }
                }
                else if (_dropInsertionAdorner != null)
                {
                    _dropInsertionAdorner.IsTopHalf = isTopHalf;
                }
            }
            else
            {
                RemoveInsertionAdorner();
            }
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
        RemoveInsertionAdorner();

        if (e.Data.GetDataPresent("MusicTrack"))
        {
            var droppedTrack = (MusicTrack)e.Data.GetData("MusicTrack");
            var listBox = (ListBox)sender;
            var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (item != null)
            {
                var targetTrack = (MusicTrack)listBox.ItemContainerGenerator.ItemFromContainer(item);
                if (targetTrack != null)
                {
                    if (DataContext is MainViewModel vm)
                    {
                        int targetIndex = vm.Music.AllTracks.IndexOf(targetTrack);
                        if (targetIndex != -1)
                        {
                            var pos = e.GetPosition(item);
                            bool isTopHalf = pos.Y < item.RenderSize.Height / 2;
                            int newIndex = isTopHalf ? targetIndex : targetIndex + 1;
                            
                            // Adjust index for dragging down
                            int oldIndex = vm.Music.AllTracks.IndexOf(droppedTrack);
                            if (oldIndex != -1 && oldIndex < newIndex)
                            {
                                newIndex--;
                            }

                            if (newIndex >= 0 && newIndex <= vm.Music.AllTracks.Count)
                            {
                                vm.Music.ReorderTrack(droppedTrack, newIndex);
                            }
                        }
                    }
                }
            }
            e.Handled = true;
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            UserControl_Drop(sender, e);
        }
    }

    private void RemoveInsertionAdorner()
    {
        if (_dropInsertionAdorner != null && _targetAdornedItem != null)
        {
            var layer = AdornerLayer.GetAdornerLayer(_targetAdornedItem);
            layer?.Remove(_dropInsertionAdorner);
            _dropInsertionAdorner = null;
        }
        _targetAdornedItem = null;
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        do
        {
            if (current is T ancestor) return ancestor;
            current = VisualTreeHelper.GetParent(current);
        }
        while (current != null);
        return null;
    }
}

