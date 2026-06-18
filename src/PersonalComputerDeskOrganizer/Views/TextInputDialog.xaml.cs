using System.Windows;
using System.Windows.Input;

namespace PersonalComputerDeskOrganizer.Views;

public partial class TextInputDialog : Window
{
    public string Value => ValueTextBox.Text.Trim();

    public TextInputDialog(string prompt, string defaultValue = "")
    {
        InitializeComponent();
        PromptText.Text = prompt;
        ValueTextBox.Text = defaultValue;
        Loaded += (_, _) => { ValueTextBox.Focus(); ValueTextBox.SelectAll(); };
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) DialogResult = true;
        else if (e.Key == Key.Escape) DialogResult = false;
    }
}
