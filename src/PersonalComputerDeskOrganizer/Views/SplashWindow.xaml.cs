using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using PersonalComputerDeskOrganizer.Models;
using PersonalComputerDeskOrganizer.Services;
using PersonalComputerDeskOrganizer.ViewModels;

namespace PersonalComputerDeskOrganizer.Views;

public partial class SplashWindow : Window
{
    private readonly ProfileStorageService _storage = new();

    public SplashWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ReloadProfiles();
    }

    private void ReloadProfiles()
    {
        var profiles = _storage.LoadAll();
        ProfilesItemsControl.ItemsSource = profiles.Select(p => new ProfileSummary { Profile = p }).ToList();
    }

    private async void ProfileAvatar_Click(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ProfileSummary summary) return;

        var result = MessageBox.Show(
            $"Lancer le profil « {summary.Name} » ?\nLes bureaux et applications configurés vont être ouverts.",
            "Confirmer le lancement",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        Hide();

        var overlay = new LaunchOverlayWindow();
        overlay.Show();

        var orchestrator = new ProfileLaunchOrchestrator();
        await orchestrator.RunAsync(summary.Profile, status => overlay.SetStatus(status));

        overlay.Close();
        Close();
    }

    private void ProfileMenu_Click(object sender, RoutedEventArgs e)
    {
        var button = (System.Windows.Controls.Button)sender;

        if (button.DataContext is ProfileSummary summary && button.ContextMenu != null)
        {
            foreach (var item in button.ContextMenu.Items)
            {
                if (item is System.Windows.Controls.MenuItem menuItem)
                    menuItem.Tag = summary;
            }

            button.ContextMenu.IsOpen = true;
        }

        e.Handled = true;
    }

    private void EditProfile_Click(object sender, RoutedEventArgs e)
    {
        if (((System.Windows.Controls.MenuItem)sender).Tag is not ProfileSummary summary) return;

        var editor = new ProfileEditorWindow(summary.Profile.Clone());
        editor.ShowDialog();
        ReloadProfiles();
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (((System.Windows.Controls.MenuItem)sender).Tag is not ProfileSummary summary) return;

        var result = MessageBox.Show(
            $"Supprimer définitivement le profil « {summary.Name} » ?\nCette action est irréversible.",
            "Confirmer la suppression",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        _storage.Delete(summary.Profile.Id);
        ReloadProfiles();
    }

    private void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        if (((System.Windows.Controls.MenuItem)sender).Tag is not ProfileSummary summary) return;

        string safeName = string.Join("_", summary.Name.Split(System.IO.Path.GetInvalidFileNameChars()));
        var dialog = new SaveFileDialog
        {
            Title = "Exporter le profil",
            Filter = "Profil PersonalComputerDeskOrganizer (*.json)|*.json",
            FileName = $"{safeName}.json"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            _storage.ExportToFile(summary.Profile, dialog.FileName);
            MessageBox.Show(
                $"Profil « {summary.Name} » exporté.\n\nCopiez ce fichier sur l'autre ordinateur puis utilisez " +
                "« Importer un profil » sur l'écran d'accueil.\n\nNote : si les applications ne sont pas installées " +
                "au même endroit sur l'autre PC, certaines zones devront être réassignées après import.",
                "Export réussi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Échec de l'export :\n{ex.Message}", "Erreur",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importer un profil",
            Filter = "Profil PersonalComputerDeskOrganizer (*.json)|*.json"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var profile = _storage.ImportFromFile(dialog.FileName);
            _storage.Save(profile);
            ReloadProfiles();

            MessageBox.Show(
                $"Profil « {profile.Name} » importé.\n\nSi certaines applications, fichiers ou URL ne s'ouvrent pas " +
                "correctement, c'est probablement parce que leur emplacement diffère sur cet ordinateur — modifiez " +
                "ces zones via « Éditer ».",
                "Import réussi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Échec de l'import :\n{ex.Message}", "Erreur",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        var countDialog = new DesktopCountDialog();
        if (countDialog.ShowDialog() != true) return;

        var profile = new Profile { Name = "" };
        for (int i = 0; i < countDialog.DesktopCount; i++)
            profile.Desktops.Add(new DesktopConfig());

        var editor = new ProfileEditorWindow(profile);
        editor.ShowDialog();
        ReloadProfiles();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => new SettingsDialog { Owner = this }.ShowDialog();

    private void Quit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
