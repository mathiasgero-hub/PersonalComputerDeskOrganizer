using System.Diagnostics;
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
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);
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
                Process.Start(new ProcessStartInfo
                {
                    FileName = division.LaunchTarget,
                    UseShellExecute = true
                });
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
