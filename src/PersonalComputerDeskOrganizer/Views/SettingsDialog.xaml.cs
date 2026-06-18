using System.Windows;
using PersonalComputerDeskOrganizer.Services;

namespace PersonalComputerDeskOrganizer.Views;

public partial class SettingsDialog : Window
{
    private readonly StartupService _startup = new();

    public SettingsDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => StartupCheckBox.IsChecked = _startup.IsEnabled();
    }

    private void StartupCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (StartupCheckBox.IsChecked == true)
            _startup.Enable();
        else
            _startup.Disable();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
