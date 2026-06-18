using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using PersonalComputerDeskOrganizer.Models;
using PersonalComputerDeskOrganizer.Services;

namespace PersonalComputerDeskOrganizer.Views;

public partial class ProfileEditorWindow : Window
{
    private readonly Profile _profile;
    private readonly ProfileStorageService _storage = new();
    private readonly InstalledAppsService _installedApps = new();
    private List<InstalledApp> _appsCache = new();
    private Popup? _currentPopup;

    public ProfileEditorWindow(Profile profile)
    {
        InitializeComponent();

        _profile = profile;
        if (_profile.Desktops.Count == 0)
            _profile.Desktops.Add(new DesktopConfig());
        foreach (var desk in _profile.Desktops)
            desk.EnsureDivisionCount();

        Loaded += (_, _) =>
        {
            ProfileNameTextBox.Text = _profile.Name;
            DeskCountText.Text = _profile.Desktops.Count.ToString();
            RenderAll();
            _ = _installedApps.GetInstalledAppsAsync().ContinueWith(t =>
            {
                if (!t.IsFaulted) _appsCache = t.Result;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        };
    }

    // ---- layout helpers shared between the real division grid and the tiny layout-picker icons ----

    private static (int rows, int cols) GetGridSize(int layout) => layout switch
    {
        1 => (1, 1),
        2 => (1, 2),
        3 => (2, 2),
        4 => (2, 2),
        _ => (1, 1)
    };

    private static List<(int row, int col, int colSpan)> GetCellPlacements(int layout) => layout switch
    {
        1 => new() { (0, 0, 1) },
        2 => new() { (0, 0, 1), (0, 1, 1) },
        3 => new() { (0, 0, 2), (1, 0, 1), (1, 1, 1) },
        4 => new() { (0, 0, 1), (0, 1, 1), (1, 0, 1), (1, 1, 1) },
        _ => new() { (0, 0, 1) }
    };

    // ---- top-level rendering --------------------------------------------------------------------

    private void RenderAll()
    {
        DeskGridPanel.Children.Clear();
        for (int i = 0; i < _profile.Desktops.Count; i++)
            DeskGridPanel.Children.Add(BuildDeskCard(_profile.Desktops[i], i));
    }

    private Border BuildDeskCard(DesktopConfig desk, int deskIndex)
    {
        var outer = new Border
        {
            Width = 460,
            Margin = new Thickness(14),
            Background = (Brush)FindResource("DecoCharcoal"),
            BorderBrush = (Brush)FindResource("DecoGoldDim"),
            BorderThickness = new Thickness(1)
        };

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // header
        var header = new Grid { Margin = new Thickness(16, 12, 16, 12) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var title = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(desk.Name) ? $"BUREAU {deskIndex + 1}" : desk.Name!.ToUpperInvariant(),
            FontFamily = (FontFamily)FindResource("DecoDisplayFont"),
            FontSize = 13,
            Foreground = (Brush)FindResource("DecoGoldLight"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(title, 0);

        var layoutPicker = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (int n in new[] { 1, 2, 3, 4 })
            layoutPicker.Children.Add(BuildLayoutIconButton(n, desk.Layout == n, desk));
        Grid.SetColumn(layoutPicker, 1);

        header.Children.Add(title);
        header.Children.Add(layoutPicker);

        var headerBorder = new Border
        {
            BorderBrush = (Brush)FindResource("DecoGoldDim"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = header
        };
        Grid.SetRow(headerBorder, 0);

        // body (the actual divisions, fixed 16:9-ish height matching the validated mockup)
        var bodyHost = new Border { Padding = new Thickness(3) };
        bodyHost.Child = BuildDivisionsGrid(desk, deskIndex, height: 240);
        Grid.SetRow(bodyHost, 1);

        rootGrid.Children.Add(headerBorder);
        rootGrid.Children.Add(bodyHost);
        outer.Child = rootGrid;
        return outer;
    }

    private Button BuildLayoutIconButton(int n, bool active, DesktopConfig desk)
    {
        var stroke = (Brush)FindResource(active ? "DecoGoldLight" : "DecoTextMuted");

        var icon = new Grid { Width = 20, Height = 20 };
        var (rows, cols) = GetGridSize(n);
        for (int r = 0; r < rows; r++) icon.RowDefinitions.Add(new RowDefinition());
        for (int c = 0; c < cols; c++) icon.ColumnDefinitions.Add(new ColumnDefinition());

        foreach (var (row, col, colSpan) in GetCellPlacements(n))
        {
            var rect = new System.Windows.Shapes.Rectangle
            {
                Stroke = stroke,
                StrokeThickness = 1.4,
                Margin = new Thickness(1)
            };
            Grid.SetRow(rect, row);
            Grid.SetColumn(rect, col);
            Grid.SetColumnSpan(rect, colSpan);
            icon.Children.Add(rect);
        }

        var button = new Button
        {
            Content = icon,
            Width = 28,
            Height = 28,
            Margin = new Thickness(3, 0, 0, 0),
            Background = (Brush)FindResource(active ? "DecoCharcoal3" : "DecoCharcoal2"),
            BorderBrush = (Brush)FindResource(active ? "DecoGold" : "DecoGoldDim"),
            BorderThickness = new Thickness(1)
        };
        button.Click += (_, _) =>
        {
            desk.Layout = n;
            desk.EnsureDivisionCount();
            RenderAll();
        };
        return button;
    }

    private Grid BuildDivisionsGrid(DesktopConfig desk, int deskIndex, double height)
    {
        var grid = new Grid { Height = height };
        var (rows, cols) = GetGridSize(desk.Layout);
        for (int r = 0; r < rows; r++) grid.RowDefinitions.Add(new RowDefinition());
        for (int c = 0; c < cols; c++) grid.ColumnDefinitions.Add(new ColumnDefinition());

        var placements = GetCellPlacements(desk.Layout);
        for (int j = 0; j < placements.Count && j < desk.Divisions.Count; j++)
        {
            var (row, col, colSpan) = placements[j];
            var cell = BuildDivisionCell(desk, deskIndex, j);
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, col);
            Grid.SetColumnSpan(cell, colSpan);
            grid.Children.Add(cell);
        }

        return grid;
    }

    private Border BuildDivisionCell(DesktopConfig desk, int deskIndex, int divIndex)
    {
        var division = desk.Divisions[divIndex];

        var cell = new Border
        {
            Margin = new Thickness(2),
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)FindResource(division.IsFilled ? "DecoGold" : "DecoGoldDim"),
            Background = (Brush)FindResource(division.IsFilled ? "DecoCharcoal3" : "DecoCharcoal2")
        };

        if (!division.IsFilled)
        {
            var prompt = new TextBlock
            {
                Text = "+ choisir une application",
                Foreground = (Brush)FindResource("DecoTextMuted"),
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            prompt.MouseLeftButtonUp += (_, _) => OpenAppPicker(desk, deskIndex, divIndex, prompt);
            cell.Child = prompt;
        }
        else
        {
            var content = new Grid();
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = division.DisplayName,
                FontFamily = (FontFamily)FindResource("DecoDisplayFont"),
                FontSize = 13,
                Foreground = (Brush)FindResource("DecoGoldLight"),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 180
            });
            string metaLabel = division.Type switch
            {
                DivisionType.File => "fichier",
                DivisionType.Url => "url",
                _ => "application"
            };
            stack.Children.Add(new TextBlock
            {
                Text = metaLabel,
                FontSize = 10,
                Foreground = (Brush)FindResource("DecoTextMuted"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });

            var clearButton = new Button
            {
                Content = "×",
                Width = 20,
                Height = 20,
                FontSize = 11,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 4, 0),
                Style = (Style)FindResource("DecoIconButton")
            };
            clearButton.Click += (_, _) =>
            {
                division.Type = DivisionType.Empty;
                division.DisplayName = null;
                division.LaunchTarget = null;
                division.Arguments = null;
                RenderAll();
            };

            content.Children.Add(stack);
            content.Children.Add(clearButton);
            cell.Child = content;
        }

        return cell;
    }

    // ---- application / file / url picker ---------------------------------------------------------

    private async void OpenAppPicker(DesktopConfig desk, int deskIndex, int divIndex, FrameworkElement anchor)
    {
        if (_currentPopup != null) _currentPopup.IsOpen = false;

        if (_appsCache.Count == 0)
            _appsCache = await _installedApps.GetInstalledAppsAsync();

        var popup = new Popup
        {
            PlacementTarget = anchor,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true
        };

        var container = new Border
        {
            Width = 260,
            Background = (Brush)FindResource("DecoCharcoal3"),
            BorderBrush = (Brush)FindResource("DecoGold"),
            BorderThickness = new Thickness(1)
        };

        var stack = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };

        var search = new TextBox
        {
            Style = (Style)FindResource("DecoTextBox"),
            Margin = new Thickness(10, 0, 10, 8),
            FontSize = 12
        };

        var resultsPanel = new StackPanel();
        var resultsScroll = new ScrollViewer { MaxHeight = 220, Content = resultsPanel };

        void RenderResults(string filter)
        {
            resultsPanel.Children.Clear();
            var matches = _appsCache
                .Where(a => string.IsNullOrWhiteSpace(filter) || a.Name.ToLowerInvariant().Contains(filter.ToLowerInvariant()))
                .Take(60);

            bool any = false;
            foreach (var app in matches)
            {
                any = true;
                var item = new Button
                {
                    Content = app.Name,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = (Brush)FindResource("DecoText"),
                    Padding = new Thickness(10, 6, 10, 6),
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                item.Click += (_, _) =>
                {
                    desk.Divisions[divIndex].Type = DivisionType.App;
                    desk.Divisions[divIndex].DisplayName = app.Name;
                    desk.Divisions[divIndex].LaunchTarget = app.LaunchTarget;
                    popup.IsOpen = false;
                    RenderAll();
                };
                resultsPanel.Children.Add(item);
            }

            if (!any)
            {
                resultsPanel.Children.Add(new TextBlock
                {
                    Text = "Aucune application trouvée",
                    Foreground = (Brush)FindResource("DecoTextMuted"),
                    FontSize = 12,
                    Margin = new Thickness(10, 6, 10, 6)
                });
            }
        }

        search.TextChanged += (_, _) => RenderResults(search.Text);

        var refreshButton = new Button
        {
            Content = "↻ actualiser la liste",
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = (Brush)FindResource("DecoTextMuted"),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 4, 10, 4)
        };
        refreshButton.Click += async (_, _) =>
        {
            refreshButton.Content = "Analyse en cours…";
            _appsCache = await _installedApps.GetInstalledAppsAsync(forceRefresh: true);
            refreshButton.Content = "↻ actualiser la liste";
            RenderResults(search.Text);
        };

        var fileButton = new Button
        {
            Content = "📄 Fichier spécifique…",
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = (Brush)FindResource("DecoText"),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 6, 10, 6)
        };
        fileButton.Click += (_, _) =>
        {
            var dialog = new OpenFileDialog { Title = "Choisir un fichier" };
            if (dialog.ShowDialog() == true)
            {
                desk.Divisions[divIndex].Type = DivisionType.File;
                desk.Divisions[divIndex].DisplayName = System.IO.Path.GetFileName(dialog.FileName);
                desk.Divisions[divIndex].LaunchTarget = dialog.FileName;
            }
            popup.IsOpen = false;
            RenderAll();
        };

        var urlButton = new Button
        {
            Content = "🔗 URL spécifique…",
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = (Brush)FindResource("DecoText"),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 6, 10, 6)
        };
        urlButton.Click += (_, _) =>
        {
            popup.IsOpen = false;
            var dialog = new TextInputDialog("Adresse URL à ouvrir :", "https://") { Owner = this };
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Value))
            {
                string url = dialog.Value;
                desk.Divisions[divIndex].Type = DivisionType.Url;
                desk.Divisions[divIndex].DisplayName = url.Replace("https://", "").Replace("http://", "");
                desk.Divisions[divIndex].LaunchTarget = url;
            }
            RenderAll();
        };

        stack.Children.Add(search);
        stack.Children.Add(resultsScroll);
        stack.Children.Add(refreshButton);
        stack.Children.Add(new Separator { Margin = new Thickness(8, 4, 8, 4) });
        stack.Children.Add(fileButton);
        stack.Children.Add(urlButton);

        container.Child = stack;
        popup.Child = container;

        RenderResults("");

        _currentPopup = popup;
        popup.IsOpen = true;
        search.Focus();
    }

    // ---- top bar actions ----------------------------------------------------------------------

    private void DeskCountMinus_Click(object sender, RoutedEventArgs e) => ChangeDeskCount(-1);
    private void DeskCountPlus_Click(object sender, RoutedEventArgs e) => ChangeDeskCount(1);

    private void ChangeDeskCount(int delta)
    {
        int target = Math.Max(1, Math.Min(12, _profile.Desktops.Count + delta));

        while (_profile.Desktops.Count < target)
            _profile.Desktops.Add(new DesktopConfig());
        while (_profile.Desktops.Count > target)
            _profile.Desktops.RemoveAt(_profile.Desktops.Count - 1);

        DeskCountText.Text = _profile.Desktops.Count.ToString();
        RenderAll();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = ProfileNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Merci de donner un nom à ce profil avant de l'enregistrer.",
                "Nom du profil requis", MessageBoxButton.OK, MessageBoxImage.Information);
            ProfileNameTextBox.Focus();
            return;
        }

        _profile.Name = name;
        _storage.Save(_profile);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Back_Click(object sender, RoutedEventArgs e) => Cancel_Click(sender, e);
}
