using System.Windows;

namespace PersonalComputerDeskOrganizer.Views;

public partial class LaunchOverlayWindow : Window
{
    public LaunchOverlayWindow()
    {
        InitializeComponent();
    }

    public void SetStatus(string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetStatus(text));
            return;
        }

        StatusText.Text = text;
    }
}
