using System;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using ManagerIV.ViewModels;
using Wpf.Ui.Controls;

namespace ManagerIV.Views;

/// <summary>
/// Interaction logic for ModLibraryView.xaml
/// </summary>
public partial class ModLibraryView : UserControl
{
    private Point _dragStartPoint;
    private DragGhostAdorner? _dragGhostAdorner;
    private DropInsertionAdorner? _dropInsertionAdorner;
    private ListBoxItem? _draggedItem;
    private ListBoxItem? _targetAdornedItem;

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

        if (e.Data.GetDataPresent("ModViewModel"))
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
                        var pos = e.GetPosition(item);
                        bool isTopHalf = pos.Y < item.RenderSize.Height / 2;
                        
                        // We use the same ReorderModCommand but if the user wants exact priority adjustment 
                        // it might require more logic here. For now, matching the original target priority.
                        // Top half implies it goes before the target, bottom half implies after. 
                        // The backend shifts based on target priority.
                        int targetPriority = targetMod.Priority;
                        // For a real insertion, if dragging down and dropping on top half, priority is targetPriority.
                        // For simplicity, we just pass the targetPriority as it was.
                        vm.ReorderModCommand.Execute(new Tuple<ModViewModel, int>(droppedMod, targetPriority));
                    }
                }
            }
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            HandleFileDrop(e);
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
    private void ClosePopup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement button)
        {
            var popup = FindAncestor<System.Windows.Controls.Primitives.Popup>(button);
            if (popup != null)
            {
                popup.IsOpen = false;
            }
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
        if (LibraryContentGrid == null || ListsContainerGrid == null)
            return;

        // Stack ListsContainerGrid vertically if narrow (width < 1000)
        if (e.NewSize.Width < 1000)
        {
            ListsContainerGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            if (ListsContainerGrid.ColumnDefinitions.Count > 1)
                ListsContainerGrid.ColumnDefinitions[1].Width = new GridLength(0);
            if (ListsContainerGrid.ColumnDefinitions.Count > 2)
                ListsContainerGrid.ColumnDefinitions[2].Width = new GridLength(0);

            if (ListsContainerGrid.RowDefinitions.Count < 3)
            {
                ListsContainerGrid.RowDefinitions.Clear();
                ListsContainerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4, GridUnitType.Star) });
                ListsContainerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
                ListsContainerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
            }

            Grid.SetColumn(ScriptsListCard, 0);
            Grid.SetRow(ScriptsListCard, 1);
            ScriptsListCard.Margin = new Thickness(0, 10, 0, 4);

            Grid.SetColumn(PluginsListCard, 0);
            Grid.SetRow(PluginsListCard, 2);
            PluginsListCard.Margin = new Thickness(0, 10, 0, 4);
        }
        else
        {
            if (ListsContainerGrid.ColumnDefinitions.Count > 2)
            {
                ListsContainerGrid.ColumnDefinitions[0].Width = new GridLength(4, GridUnitType.Star);
                ListsContainerGrid.ColumnDefinitions[1].Width = new GridLength(3, GridUnitType.Star);
                ListsContainerGrid.ColumnDefinitions[2].Width = new GridLength(3, GridUnitType.Star);
            }

            ListsContainerGrid.RowDefinitions.Clear();

            Grid.SetColumn(ScriptsListCard, 1);
            Grid.SetRow(ScriptsListCard, 0);
            ScriptsListCard.Margin = new Thickness(10, 0, 6, 4);

            Grid.SetColumn(PluginsListCard, 2);
            Grid.SetRow(PluginsListCard, 0);
            PluginsListCard.Margin = new Thickness(6, 0, 0, 4);
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

public class DragGhostAdorner : System.Windows.Documents.Adorner
{
    private System.Windows.Shapes.Rectangle _child;
    private Point _offset;
    private Point _position;

    public DragGhostAdorner(UIElement adornedElement, UIElement visualToAdorn, Point offset)
        : base(adornedElement)
    {
        var brush = new VisualBrush(visualToAdorn) { Opacity = 0.7 };
        var bounds = VisualTreeHelper.GetDescendantBounds(visualToAdorn);
        _child = new System.Windows.Shapes.Rectangle { Width = bounds.Width, Height = bounds.Height, Fill = brush };
        _offset = offset;
        IsHitTestVisible = false;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        _child.Measure(constraint);
        return _child.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _child.Arrange(new Rect(_child.DesiredSize));
        return finalSize;
    }

    protected override Visual GetVisualChild(int index) => _child;
    protected override int VisualChildrenCount => 1;

    public void UpdatePosition(Point currentPosition)
    {
        _position = currentPosition;
        if (Parent is System.Windows.Documents.AdornerLayer layer)
        {
            layer.Update(AdornedElement);
        }
    }

    public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
    {
        var result = new GeneralTransformGroup();
        result.Children.Add(base.GetDesiredTransform(transform));
        result.Children.Add(new TranslateTransform(_position.X - _offset.X, _position.Y - _offset.Y));
        return result;
    }
}

public class DropInsertionAdorner : System.Windows.Documents.Adorner
{
    private bool _isTopHalf;
    private Pen _pen;

    public DropInsertionAdorner(UIElement adornedElement, bool isTopHalf) : base(adornedElement)
    {
        _isTopHalf = isTopHalf;
        var brush = Application.Current.Resources["SystemAccentColorSecondaryBrush"] as Brush ?? new SolidColorBrush(Colors.DodgerBlue);
        _pen = new Pen(brush, 3);
        IsHitTestVisible = false;
    }

    public bool IsTopHalf
    {
        get => _isTopHalf;
        set
        {
            if (_isTopHalf != value)
            {
                _isTopHalf = value;
                InvalidateVisual();
            }
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        double y = _isTopHalf ? 0 : AdornedElement.RenderSize.Height;
        drawingContext.DrawLine(_pen, new Point(0, y), new Point(AdornedElement.RenderSize.Width, y));
    }
}
