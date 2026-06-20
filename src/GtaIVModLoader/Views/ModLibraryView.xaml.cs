using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GtaIVModLoader.ViewModels;

namespace GtaIVModLoader.Views;

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

    private void UserControl_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".zip" || ext == ".rar" || ext == ".7z")
                {
                    if (DataContext is MainViewModel vm)
                    {
                        _ = vm.ImportArchiveAsync(file);
                    }
                }
            }
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
