using System.Windows;
using System.Windows.Controls;
using GameSubRelay.App.ViewModels;

namespace GameSubRelay.App.Settings;

public partial class SpeechRecognitionSettingsView : UserControl
{
    public SpeechRecognitionSettingsView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SpeechRecognitionSettingsViewModel viewModel &&
            SecretAccessKeyBox.Password != viewModel.SecretAccessKey)
        {
            SecretAccessKeyBox.Password = viewModel.SecretAccessKey;
        }
    }

    private void SecretAccessKeyPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SpeechRecognitionSettingsViewModel viewModel &&
            sender is PasswordBox passwordBox)
        {
            viewModel.SecretAccessKey = passwordBox.Password;
        }
    }
}
