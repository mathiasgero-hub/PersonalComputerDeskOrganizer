using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using PersonalComputerDeskOrganizer.Models;

namespace PersonalComputerDeskOrganizer.Services;

/// <summary>
/// Builds the searchable list of "every application on this PC" shown in the
/// division picker. Two sources are combined:
///
///   1. PowerShell's built-in "Get-StartApps" cmdlet — returns every entry that
///      appears in the Start Menu / Windows Search, classic Win32 apps AND
///      Microsoft Store (packaged/UWP) apps alike, each with a Name and an
///      AppID. For classic apps the AppID is a direct executable path; for
///      packaged apps it's an AppUserModelId, launched later via the
///      "shell:AppsFolder\{AppUserModelId}" trick that Explorer resolves
///      without needing the WinRT activation APIs. This single source covers
///      apps like WhatsApp Desktop, which many users now have installed as a
///      Store package rather than a classic installer.
///   2. Registry "Uninstall" keys — a fallback for the rare classic app that,
///      for some reason, has no Start Menu entry at all.
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

        await ScanStartAppsAsync(apps);
        await Task.Run(() => ScanRegistryUninstallKeys(apps));

        _cache = apps.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        return _cache;
    }

    // ---- 1. Get-StartApps (covers classic AND Microsoft Store apps) ---------------

    private static async Task ScanStartAppsAsync(Dictionary<string, InstalledApp> apps)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -Command \"Get-StartApps | ConvertTo-Json\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return;

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (string.IsNullOrWhiteSpace(output)) return;

            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            // Get-StartApps returns a single JSON object instead of an array when there's
            // only one matching app; normalize both shapes to a flat list.
            var elements = root.ValueKind == JsonValueKind.Array
                ? root.EnumerateArray().ToList()
                : new List<JsonElement> { root };

            foreach (var el in elements)
            {
                string? name = el.TryGetProperty("Name", out var n) ? n.GetString() : null;
                string? appId = el.TryGetProperty("AppID", out var a) ? a.GetString() : null;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(appId)) continue;
                if (apps.ContainsKey(name)) continue;

                bool isExePath = appId.Length > 3 && appId[1] == ':' && appId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

                if (isExePath && !File.Exists(appId)) continue;

                apps[name] = new InstalledApp
                {
                    Name = name,
                    LaunchTarget = isExePath ? appId : $"shell:AppsFolder\\{appId}",
                    Source = isExePath ? InstalledAppSource.StartMenuShortcut : InstalledAppSource.PackagedApp
                };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Get-StartApps scan failed: {ex.Message}");
        }
    }

    // ---- 2. Registry uninstall keys (fallback) -------------------------------------

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
                    if (apps.ContainsKey(displayName)) continue; // Get-StartApps already found a better path
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
}
