using System.Windows;
using System.Windows.Controls;

namespace ManagerIV.Views;

/// <summary>
/// Interaction logic for ProfileSwitcherView.xaml
/// </summary>
public partial class ProfileSwitcherView : UserControl
{
    public ProfileSwitcherView()
    {
        InitializeComponent();
    }

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ContentGrid == null || LeftPanel == null || ConfigCard == null || StatusPanel == null)
            return;

        // If the window width is wide enough (>= 1150px), use a 3-column layout where each card gets its own column.
        // This brings the Install/Uninstall buttons up to the top level, eliminating vertical scrolling.
        if (e.NewSize.Width >= 1150)
        {
            // Set grid to 3 equal/star columns
            ContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            ContentGrid.ColumnDefinitions[1].Width = new GridLength(1.1, GridUnitType.Star);
            ContentGrid.ColumnDefinitions[2].Width = new GridLength(1.1, GridUnitType.Star);

            // LeftPanel stays in Column 0, spanning 1 row
            Grid.SetRowSpan(LeftPanel, 1);

            // ConfigCard stays in Column 1, Row 0
            Grid.SetColumn(ConfigCard, 1);
            Grid.SetRow(ConfigCard, 0);

            // StatusPanel moves to Column 2, Row 0
            Grid.SetColumn(StatusPanel, 2);
            Grid.SetRow(StatusPanel, 0);

            // Refine margins for 3-column flow
            LeftPanel.Margin = new Thickness(0, 0, 10, 15);
            ConfigCard.Margin = new Thickness(10, 0, 10, 15);
            StatusPanel.Margin = new Thickness(10, 0, 0, 15);
        }
        else
        {
            // Narrow layout: 2 columns, with StatusPanel stacked below ConfigCard in Column 1
            ContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            ContentGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            ContentGrid.ColumnDefinitions[2].Width = new GridLength(0, GridUnitType.Pixel);

            // LeftPanel in Column 0, spanning both rows to align with stacked cards in Column 1
            Grid.SetRowSpan(LeftPanel, 2);

            // ConfigCard in Column 1, Row 0
            Grid.SetColumn(ConfigCard, 1);
            Grid.SetRow(ConfigCard, 0);

            // StatusPanel in Column 1, Row 1
            Grid.SetColumn(StatusPanel, 1);
            Grid.SetRow(StatusPanel, 1);

            // Refine margins for 2-column vertical stack
            LeftPanel.Margin = new Thickness(0, 0, 10, 15);
            ConfigCard.Margin = new Thickness(10, 0, 0, 15);
            StatusPanel.Margin = new Thickness(10, 0, 0, 15);
        }
    }
}
