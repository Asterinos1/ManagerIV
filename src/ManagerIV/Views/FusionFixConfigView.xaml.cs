using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ManagerIV.Views;

/// <summary>
/// Interaction logic for FusionFixConfigView.xaml
/// </summary>
public partial class FusionFixConfigView : UserControl
{
    public FusionFixConfigView()
    {
        InitializeComponent();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string query = SearchBox.Text?.Trim() ?? "";
        FilterSettings(query);
    }

    private void FilterSettings(string query)
    {
        if (SettingsStackPanel == null) return;

        bool isQueryEmpty = string.IsNullOrWhiteSpace(query);

        // Track section visibility
        TextBlock? currentSectionHeader = null;
        int visibleSectionItemsCount = 0;

        foreach (var child in SettingsStackPanel.Children)
        {
            if (child is TextBlock textBlock && textBlock.FontSize >= 15)
            {
                // If there was a previous section, show/hide its header based on visible items
                if (currentSectionHeader != null)
                {
                    currentSectionHeader.Visibility = (visibleSectionItemsCount > 0) ? Visibility.Visible : Visibility.Collapsed;
                }
                
                currentSectionHeader = textBlock;
                visibleSectionItemsCount = 0;
                continue;
            }

            if (child is FrameworkElement element)
            {
                // Separators should always show/hide in tandem with search visibility
                if (element is Separator)
                {
                    // Treat separator as secondary detail or ignore for sections
                    continue;
                }

                bool matches = IsElementMatch(element, query);
                element.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                if (matches)
                {
                    visibleSectionItemsCount++;
                }
            }
        }

        // Show/hide the last section header
        if (currentSectionHeader != null)
        {
            currentSectionHeader.Visibility = (visibleSectionItemsCount > 0) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private bool IsElementMatch(FrameworkElement element, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;

        // Try getting text from CardControl or Card elements recursively
        string elementText = FindAllText(element);
        return elementText.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private string FindAllText(DependencyObject obj)
    {
        var sb = new System.Text.StringBuilder();
        FindAllTextInternal(obj, sb);
        return sb.ToString();
    }

    private void FindAllTextInternal(DependencyObject obj, System.Text.StringBuilder sb)
    {
        if (obj is TextBlock tb)
        {
            sb.Append(" ").Append(tb.Text);
        }
        
        // Also look into Content of controls or Headers if they aren't hit by visual tree
        if (obj is HeaderedContentControl hcc)
        {
            if (hcc.Header != null)
            {
                if (hcc.Header is string s) sb.Append(" ").Append(s);
                else if (hcc.Header is DependencyObject dobjHeader) FindAllTextInternal(dobjHeader, sb);
            }
        }

        int count = VisualTreeHelper.GetChildrenCount(obj);
        for (int i = 0; i < count; i++)
        {
            FindAllTextInternal(VisualTreeHelper.GetChild(obj, i), sb);
        }
    }
}
