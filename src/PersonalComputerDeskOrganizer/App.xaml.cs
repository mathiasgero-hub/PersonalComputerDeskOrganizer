using System.Windows;
using System.Windows.Threading;

namespace PersonalComputerDeskOrganizer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A single failed launch step (e.g. one app in a profile not found) must never
        // crash the whole organizer — surface it instead of letting WPF tear the app down.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"Une erreur inattendue est survenue :\n{args.Exception.Message}",
                "PersonalComputerDeskOrganizer",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };
    }
}
