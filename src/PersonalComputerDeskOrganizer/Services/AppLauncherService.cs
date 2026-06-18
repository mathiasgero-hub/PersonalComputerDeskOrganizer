using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using PersonalComputerDeskOrganizer.Models;

namespace PersonalComputerDeskOrganizer.Services;

/// <summary>
/// Launches whatever a division is configured to open, and waits for the
/// resulting top-level window to appear so it can be moved/resized.
///
/// Rather than relying solely on Process.MainWindowHandle — which is unreliable
/// for apps that launch via a separate host process (packaged/UWP apps) or that
/// spawn helper processes before showing their real window (many Electron/Chromium
/// based apps) — this takes a snapshot of all visible windows just before
/// launching, then polls for a new visible window to appear afterward.
/// </summary>
public class AppLauncherService
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>Starts the division's target and returns the HWND of its new window, or IntPtr.Zero on failure/timeout.</summary>
    public async Task<IntPtr> LaunchAndWaitForWindowAsync(DivisionConfig division)
    {
        if (!division.IsFilled || division.LaunchTarget is null)
            return IntPtr.Zero;

        var before = NativeMethods.SnapshotVisibleWindows();

        try
        {
            Start(division);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to launch '{division.LaunchTarget}': {ex.Message}");
            return IntPtr.Zero;
        }

        return await WaitForNewWindowAsync(before);
    }

    private static void Start(DivisionConfig division)
    {
        switch (division.Type)
        {
            case DivisionType.Url:
                StartUrlInNewWindow(division.LaunchTarget!);
                break;

            case DivisionType.File:
                Process.Start(new ProcessStartInfo
                {
                    FileName = division.LaunchTarget,
                    UseShellExecute = true
                });
                break;

            case DivisionType.App:
            default:
                // "shell:AppsFolder\{aumid}" (packaged apps) must go through explorer.exe;
                // regular .exe paths can be started directly.
                if (division.LaunchTarget!.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = division.LaunchTarget,
                        UseShellExecute = true
                    });
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = division.LaunchTarget,
                        Arguments = division.Arguments ?? "",
                        UseShellExecute = true
                    });
                }
                break;
        }
    }

    /// <summary>
    /// Launches a URL in a brand-new browser window rather than as a new tab in
    /// whichever browser window is already open. Browsers reuse an existing window
    /// by default for plain shell-execute requests, which breaks the "one window per
    /// zone" assumption — this detects the default browser and passes its
    /// "new window" command-line flag explicitly. Falls back to a normal shell-execute
    /// (which may still group into an existing window) if the default browser or its
    /// flag can't be determined.
    /// </summary>
    private static void StartUrlInNewWindow(string url)
    {
        string? browserExe = GetDefaultBrowserExecutable();
        string? newWindowArg = browserExe is null ? null : GetNewWindowArgument(browserExe);

        if (browserExe is not null && newWindowArg is not null)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = browserExe,
                Arguments = $"{newWindowArg} \"{url}\"",
                UseShellExecute = false
            });
        }
        else
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }

    private static string? GetDefaultBrowserExecutable()
    {
        try
        {
            using var userChoiceKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");
            string? progId = userChoiceKey?.GetValue("ProgId") as string;
            if (string.IsNullOrWhiteSpace(progId)) return null;

            using var commandKey = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
            string? command = commandKey?.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(command)) return null;

            string exePath = command.StartsWith("\"")
                ? command.Substring(1, command.IndexOf('"', 1) - 1)
                : command.Split(' ')[0];

            return File.Exists(exePath) ? exePath : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetNewWindowArgument(string browserExePath)
    {
        string name = Path.GetFileNameWithoutExtension(browserExePath).ToLowerInvariant();
        return name switch
        {
            "chrome" or "msedge" or "brave" or "opera" or "vivaldi" => "--new-window",
            "firefox" => "-new-window",
            _ => null
        };
    }

    private static async Task<IntPtr> WaitForNewWindowAsync(HashSet<IntPtr> before)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;
        IntPtr ownHwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollInterval);

            var after = NativeMethods.SnapshotVisibleWindows();
            var newWindows = after.Except(before).Where(h => h != ownHwnd).ToList();

            if (newWindows.Count > 0)
                return newWindows[0];
        }

        return IntPtr.Zero;
    }
}
