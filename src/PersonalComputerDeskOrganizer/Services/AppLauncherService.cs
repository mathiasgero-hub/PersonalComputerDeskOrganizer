using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using PersonalComputerDeskOrganizer.Models;

namespace PersonalComputerDeskOrganizer.Services;

public class AppLauncherService
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan StabilizationPeriod = TimeSpan.FromMilliseconds(1200);

    public async Task<IntPtr> LaunchAndWaitForWindowAsync(DivisionConfig division)
    {
        if (!division.IsFilled || division.LaunchTarget is null)
            return IntPtr.Zero;

        var before = NativeMethods.SnapshotVisibleWindows();
        Process? tracked;

        try
        {
            tracked = Start(division);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to launch '{division.LaunchTarget}': {ex.Message}");
            return IntPtr.Zero;
        }

        return await WaitForStableWindowAsync(before, tracked);
    }

    private static Process? Start(DivisionConfig division)
    {
        switch (division.Type)
        {
            case DivisionType.Url:
                StartUrlInNewWindow(division.LaunchTarget!);
                return null;

            case DivisionType.File:
                Process.Start(new ProcessStartInfo
                {
                    FileName = division.LaunchTarget,
                    UseShellExecute = true
                });
                return null;

            case DivisionType.App:
            default:
                if (division.LaunchTarget!.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = division.LaunchTarget,
                        UseShellExecute = true
                    });
                    return null;
                }
                else
                {
                    return Process.Start(new ProcessStartInfo
                    {
                        FileName = division.LaunchTarget,
                        Arguments = division.Arguments ?? "",
                        UseShellExecute = true
                    });
                }
        }
    }

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

    private static async Task<IntPtr> WaitForStableWindowAsync(HashSet<IntPtr> before, Process? trackedProcess)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;
        IntPtr ownHwnd = Process.GetCurrentProcess().MainWindowHandle;

        var firstSeenAt = new Dictionary<IntPtr, DateTime>();
        IntPtr currentBest = IntPtr.Zero;
        DateTime currentBestSince = DateTime.MinValue;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollInterval);

            bool processGone = trackedProcess is not null && trackedProcess.HasExited;
            var candidates = (trackedProcess is not null && !processGone)
                ? GetVisibleWindowsForProcess(trackedProcess.Id)
                : NativeMethods.SnapshotVisibleWindows().Except(before).Where(h => h != ownHwnd).ToHashSet();

            foreach (var seenHwnd in firstSeenAt.Keys.ToList())
            {
                if (!candidates.Contains(seenHwnd))
                    firstSeenAt.Remove(seenHwnd);
            }

            foreach (var hwnd in candidates)
            {
                if (!firstSeenAt.ContainsKey(hwnd))
                    firstSeenAt[hwnd] = DateTime.UtcNow;
            }

            if (candidates.Count == 0)
            {
                currentBest = IntPtr.Zero;
                continue;
            }

            IntPtr newest = candidates.OrderByDescending(h => firstSeenAt[h]).First();

            if (newest != currentBest)
            {
                currentBest = newest;
                currentBestSince = DateTime.UtcNow;
            }
            else if (DateTime.UtcNow - currentBestSince >= StabilizationPeriod)
            {
                return currentBest;
            }
        }

        return currentBest;
    }

    private static HashSet<IntPtr> GetVisibleWindowsForProcess(int processId)
    {
        var result = new HashSet<IntPtr>();
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd) || NativeMethods.GetWindowTextLength(hWnd) <= 0)
                return true;

            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == (uint)processId)
                result.Add(hWnd);

            return true;
        }, IntPtr.Zero);
        return result;
    }
}
