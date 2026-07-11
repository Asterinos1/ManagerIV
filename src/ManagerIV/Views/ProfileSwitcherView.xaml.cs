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
        // Responsive reflow for narrow windows (< 760px) to prevent element overlapping or squeeze
        bool isNarrow = e.NewSize.Width < 760;

        // Reflow Tab 1 (Mod Profiles & Paths)
        if (Tab1Grid != null && Tab1LeftPanel != null && Tab1RightPanel != null)
        {
            if (isNarrow)
            {
                Tab1Grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                Tab1Grid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Pixel);

                Grid.SetColumn(Tab1LeftPanel, 0);
                Grid.SetRow(Tab1LeftPanel, 0);
                Tab1LeftPanel.Margin = new Thickness(0, 0, 0, 15);

                Grid.SetColumn(Tab1RightPanel, 0);
                Grid.SetRow(Tab1RightPanel, 1);
                Tab1RightPanel.Margin = new Thickness(0, 0, 0, 0);
            }
            else
            {
                Tab1Grid.ColumnDefinitions[0].Width = new GridLength(320, GridUnitType.Pixel);
                Tab1Grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);

                Grid.SetColumn(Tab1LeftPanel, 0);
                Grid.SetRow(Tab1LeftPanel, 0);
                Tab1LeftPanel.Margin = new Thickness(0, 0, 15, 0);

                Grid.SetColumn(Tab1RightPanel, 1);
                Grid.SetRow(Tab1RightPanel, 0);
                Tab1RightPanel.Margin = new Thickness(0, 0, 0, 0);
            }
        }

        // Reflow Tab 3 (Save Game States)
        if (Tab3Grid != null && Tab3LeftPanel != null && Tab3RightPanel != null)
        {
            if (isNarrow)
            {
                Tab3Grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                Tab3Grid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Pixel);

                Grid.SetColumn(Tab3LeftPanel, 0);
                Grid.SetRow(Tab3LeftPanel, 0);
                Tab3LeftPanel.Margin = new Thickness(0, 0, 0, 15);

                Grid.SetColumn(Tab3RightPanel, 0);
                Grid.SetRow(Tab3RightPanel, 1);
                Tab3RightPanel.Margin = new Thickness(0, 0, 0, 0);
            }
            else
            {
                Tab3Grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                Tab3Grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);

                Grid.SetColumn(Tab3LeftPanel, 0);
                Grid.SetRow(Tab3LeftPanel, 0);
                Tab3LeftPanel.Margin = new Thickness(0, 0, 8, 0);

                Grid.SetColumn(Tab3RightPanel, 1);
                Grid.SetRow(Tab3RightPanel, 0);
                Tab3RightPanel.Margin = new Thickness(8, 0, 0, 0);
            }
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

    private void SaveListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is SaveProfile saveProfile)
        {
            if (DataContext is MainViewModel vm && !saveProfile.IsActive)
            {
                if (vm.ActivateSaveProfileCommand.CanExecute(saveProfile))
                {
                    vm.ActivateSaveProfileCommand.Execute(saveProfile);
                }
            }
        }
    }
}
