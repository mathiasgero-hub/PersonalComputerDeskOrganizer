using System.Windows;

namespace PersonalComputerDeskOrganizer.Views;

public partial class DesktopCountDialog : Window
{
    public int DesktopCount { get; private set; } = 4;

    public DesktopCountDialog()
    {
        InitializeComponent();
    }

    private void Minus_Click(object sender, RoutedEventArgs e) => SetCount(DesktopCount - 1);
    private void Plus_Click(object sender, RoutedEventArgs e) => SetCount(DesktopCount + 1);

    private void SetCount(int value)
    {
        DesktopCount = Math.Max(1, Math.Min(12, value));
        CountText.Text = DesktopCount.ToString();
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
