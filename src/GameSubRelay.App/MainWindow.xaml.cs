using System.Windows;
using GameSubRelay.App.ViewModels;

namespace GameSubRelay.App;

public partial class MainWindow : Window
{
    public MainWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
