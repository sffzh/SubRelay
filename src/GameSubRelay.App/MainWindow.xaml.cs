using GameSubRelay.App.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace GameSubRelay.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        ApplicationThemeManager.Apply(this);
        DataContext = viewModel;
    }
}
