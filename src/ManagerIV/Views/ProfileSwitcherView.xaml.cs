using System.Windows;
using System.Windows.Controls;
using ManagerIV.Core;
using ManagerIV.ViewModels;

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

        // Apply scale transform if screen is small
        double scale = 1.0;
        if (e.NewSize.Width < 950)
        {
            scale = Math.Min(scale, Math.Max(0.85, e.NewSize.Width / 950.0));
        }
        if (e.NewSize.Height < 650)
        {
            scale = Math.Min(scale, Math.Max(0.85, e.NewSize.Height / 650.0));
        }

        if (RootScale != null)
        {
            RootScale.ScaleX = scale;
            RootScale.ScaleY = scale;
        }

        // 1. Wide layout (>= 1150px): 3 columns, each card gets its own column
        if (e.NewSize.Width >= 1150)
        {
            ContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            ContentGrid.ColumnDefinitions[1].Width = new GridLength(1.1, GridUnitType.Star);
            ContentGrid.ColumnDefinitions[2].Width = new GridLength(1.1, GridUnitType.Star);

            // LeftPanel in Column 0, span 1
            Grid.SetColumn(LeftPanel, 0);
            Grid.SetRow(LeftPanel, 0);
            Grid.SetRowSpan(LeftPanel, 1);

            // ConfigCard in Column 1, Row 0
            Grid.SetColumn(ConfigCard, 1);
            Grid.SetRow(ConfigCard, 0);

            // StatusPanel in Column 2, Row 0
            Grid.SetColumn(StatusPanel, 2);
            Grid.SetRow(StatusPanel, 0);

            // Margins for 3-column flow
            LeftPanel.Margin = new Thickness(0, 0, 10, 15);
            ConfigCard.Margin = new Thickness(10, 0, 10, 15);
            StatusPanel.Margin = new Thickness(10, 0, 0, 15);
        }
        // 2. Medium layout (720px to 1149px): 2 columns, StatusPanel stacked below ConfigCard
        else if (e.NewSize.Width >= 720)
        {
            ContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            ContentGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            ContentGrid.ColumnDefinitions[2].Width = new GridLength(0, GridUnitType.Pixel);

            // LeftPanel in Column 0, spanning 2 rows
            Grid.SetColumn(LeftPanel, 0);
            Grid.SetRow(LeftPanel, 0);
            Grid.SetRowSpan(LeftPanel, 2);

            // ConfigCard in Column 1, Row 0
            Grid.SetColumn(ConfigCard, 1);
            Grid.SetRow(ConfigCard, 0);

            // StatusPanel in Column 1, Row 1
            Grid.SetColumn(StatusPanel, 1);
            Grid.SetRow(StatusPanel, 1);

            // Margins for 2-column flow
            LeftPanel.Margin = new Thickness(0, 0, 10, 15);
            ConfigCard.Margin = new Thickness(10, 0, 0, 15);
            StatusPanel.Margin = new Thickness(10, 0, 0, 15);
        }
        // 3. Narrow layout (< 720px): 1 column, all panels stacked vertically
        else
        {
            ContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            ContentGrid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Pixel);
            ContentGrid.ColumnDefinitions[2].Width = new GridLength(0, GridUnitType.Pixel);

            // LeftPanel in Column 0, Row 0, span 1
            Grid.SetColumn(LeftPanel, 0);
            Grid.SetRow(LeftPanel, 0);
            Grid.SetRowSpan(LeftPanel, 1);

            // ConfigCard in Column 0, Row 1
            Grid.SetColumn(ConfigCard, 0);
            Grid.SetRow(ConfigCard, 1);

            // StatusPanel in Column 0, Row 2
            Grid.SetColumn(StatusPanel, 0);
            Grid.SetRow(StatusPanel, 2);

            // Margins for 1-column stack
            LeftPanel.Margin = new Thickness(0, 0, 0, 15);
            ConfigCard.Margin = new Thickness(0, 0, 0, 15);
            StatusPanel.Margin = new Thickness(0, 0, 0, 15);
        }
    }

    private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is Profile profile)
        {
            if (DataContext is MainViewModel vm && vm.ActiveProfile != profile)
            {
                if (vm.SwitchProfileCommand.CanExecute(profile))
                {
                    vm.SwitchProfileCommand.Execute(profile);
                }
            }
        }
    }
}
