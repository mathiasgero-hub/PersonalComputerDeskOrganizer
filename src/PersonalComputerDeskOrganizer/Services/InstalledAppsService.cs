using System.IO;
using Microsoft.Win32;
using PersonalComputerDeskOrganizer.Models;

namespace PersonalComputerDeskOrganizer.Services;

/// <summary>
/// Builds the searchable list of "every application on this PC" shown in the
/// division picker. Windows has no single source of truth for this, so three
/// sources are combined:
///
///   1. Start Menu shortcuts (.lnk) — the most reliable source of an actually
///      launchable path, resolved via <see cref="ShellLinkResolver"/>.
///   2. Registry "Uninstall" keys — catches classic desktop apps that, for
///      whatever reason, have no Start Menu shortcut. The DisplayIcon value is
///      used as a best-effort executable path.
///   3. Packaged / Microsoft Store (UWP) apps — enumerated through
///      Windows.Management.Deployment.PackageManager and launched later via
///      the "shell:AppsFolder\{AppUserModelId}" trick, which Explorer resolves
///      without needing full WinRT activation plumbing.
///
/// Results are cached in memory; call <see cref="GetInstalledAppsAsync"/> with
/// forceRefresh = true (wired to the "actualiser la liste" button) to rescan.
/// </summary>
public class InstalledAppsService
{
    private List<InstalledApp>? _cache;

    public async Task<List<InstalledApp>> GetInstalledAppsAsync(bool forceRefresh = false)
    {
        if (_cache is not null && !forceRefresh)
            return _cache;

        var apps = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);

        await Task.Run(() => ScanStartMenuShortcuts(apps));
        await Task.Run(() => ScanRegistryUninstallKeys(apps));
        await ScanPackagedAppsAsync(apps);

        _cache = apps.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        return _cache;
    }

    // ---- 1. Start Menu shortcuts -------------------------------------------------

    private static void ScanStartMenuShortcuts(Dictionary<string, InstalledApp> apps)
    {
        string[] roots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
        };

        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;

            IEnumerable<string> shortcuts;
            try { shortcuts = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories); }
            catch { continue; }

            foreach (var lnk in shortcuts)
            {
                string name = Path.GetFileNameWithoutExtension(lnk);

                if (LooksLikeNoise(name)) continue;
                if (apps.ContainsKey(name)) continue;

                string? target = ShellLinkResolver.ResolveTarget(lnk);
                if (string.IsNullOrWhiteSpace(target) || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!File.Exists(target)) continue;

                apps[name] = new InstalledApp
                {
                    Name = name,
                    LaunchTarget = target,
                    Source = InstalledAppSource.StartMenuShortcut
                };
            }
        }
    }

    private static bool LooksLikeNoise(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("uninstall")
            || lower.Contains("readme")
            || lower.Contains("read me")
            || lower.Contains("website")
            || lower.Contains("help")
            || lower.Contains("licence")
            || lower.Contains("license");
    }

    // ---- 2. Registry uninstall keys -----------------------------------------------

    private static readonly string[] UninstallKeyPaths =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    private static void ScanRegistryUninstallKeys(Dictionary<string, InstalledApp> apps)
    {
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var keyPath in UninstallKeyPaths)
            {
                using var uninstallRoot = hive.OpenSubKey(keyPath);
                if (uninstallRoot is null) continue;

                foreach (var subKeyName in uninstallRoot.GetSubKeyNames())
                {
                    using var entry = uninstallRoot.OpenSubKey(subKeyName);
                    if (entry is null) continue;

                    string? displayName = entry.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName)) continue;
                    if (apps.ContainsKey(displayName)) continue; // Start Menu scan already found a better path
                    if ((entry.GetValue("SystemComponent") as int?) == 1) continue; // hide OS components

                    string? exePath = TryGetExecutableFromUninstallEntry(entry);
                    if (exePath is null) continue;

                    apps[displayName] = new InstalledApp
                    {
                        Name = displayName,
                        LaunchTarget = exePath,
                        Source = InstalledAppSource.RegistryUninstallEntry
                    };
                }
            }
        }
    }

    private static string? TryGetExecutableFromUninstallEntry(RegistryKey entry)
    {
        string? iconValue = entry.GetValue("DisplayIcon") as string;
        if (!string.IsNullOrWhiteSpace(iconValue))
        {
            // DisplayIcon is often "C:\Path\App.exe,0" (icon index suffix) or just the exe path.
            string path = iconValue.Split(',')[0].Trim('"');
            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                return path;
        }

        // Fall back to scanning InstallLocation for a single obvious executable.
        string? installLocation = entry.GetValue("InstallLocation") as string;
        if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
        {
            try
            {
                var exes = Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly).ToList();
                if (exes.Count == 1) return exes[0];
            }
            catch { /* ignore inaccessible folders */ }
        }

        return null;
    }

    // ---- 3. Packaged / Microsoft Store apps ----------------------------------------

    private static async Task ScanPackagedAppsAsync(Dictionary<string, InstalledApp> apps)
    {
        try
        {
            var packageManager = new Windows.Management.Deployment.PackageManager();

            foreach (var package in packageManager.FindPackagesForUser(string.Empty))
            {
                if (package.IsFramework || package.IsResourcePackage) continue;

                IReadOnlyList<Windows.ApplicationModel.AppListEntry> entries;
                try { entries = await package.GetAppListEntriesAsync(); }
                catch { continue; }

                foreach (var entry in entries)
                {
                    string name = entry.DisplayInfo?.DisplayName ?? package.DisplayName;
                    if (string.IsNullOrWhiteSpace(name) || apps.ContainsKey(name)) continue;

                    apps[name] = new InstalledApp
                    {
                        Name = name,
                        LaunchTarget = $"shell:AppsFolder\\{entry.AppUserModelId}",
                        Source = InstalledAppSource.PackagedApp
                    };
                }
            }
        }
        catch (Exception ex)
        {
            // Packaged-app enumeration can be unavailable in some environments (e.g. locked-down
            // corporate images); the rest of the app must keep working without it.
            System.Diagnostics.Debug.WriteLine($"Packaged app scan failed: {ex.Message}");
        }
    }
}
