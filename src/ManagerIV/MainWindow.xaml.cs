using System.Windows;
using ManagerIV.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ManagerIV;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Current.Services.GetRequiredService<MainViewModel>();
    }
}