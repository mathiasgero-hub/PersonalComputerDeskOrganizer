using PersonalComputerDeskOrganizer.Models;

namespace PersonalComputerDeskOrganizer.Services;

/// <summary>
/// Executes a saved <see cref="Profile"/>: makes sure enough virtual desktops
/// exist, then for each desktop launches every filled division's target, moves
/// the resulting window onto the right desktop, and resizes it to exactly fill
/// its zone.
/// </summary>
public class ProfileLaunchOrchestrator
{
    private readonly VirtualDesktopService _desktops = new();
    private readonly WindowPlacementService _placement = new();
    private readonly AppLauncherService _launcher = new();

    /// <param name="onProgress">Called with a short human-readable status after each step, for the launch overlay UI.</param>
    public async Task RunAsync(Profile profile, Action<string>? onProgress = null)
    {
        if (profile.Desktops.Count == 0)
        {
            onProgress?.Invoke("Ce profil ne contient aucun bureau à créer.");
            return;
        }

        var virtualDesktops = _desktops.GetOrCreateDesktops(profile.Desktops.Count);

        for (int i = 0; i < profile.Desktops.Count; i++)
        {
            var deskConfig = profile.Desktops[i];
            var virtualDesktop = virtualDesktops[i];
            string deskLabel = string.IsNullOrWhiteSpace(deskConfig.Name) ? $"Bureau {i + 1}" : deskConfig.Name!;

            // Switch to this desktop FIRST, before launching anything on it. New windows are
            // created on whichever desktop is active at that moment — switching afterward
            // (once the window already exists) is too late and causes exactly the symptom of
            // "the app opens wherever I happened to be," which gets worse the slower the app is.
            _desktops.SwitchTo(virtualDesktop);

            var regions = _placement.ComputeRegions(deskConfig.Layout);

            for (int j = 0; j < deskConfig.Divisions.Count && j < regions.Count; j++)
            {
                var division = deskConfig.Divisions[j];
                if (!division.IsFilled) continue;

                onProgress?.Invoke($"{deskLabel} — ouverture de « {division.DisplayName} »…");

                IntPtr hwnd = await _launcher.LaunchAndWaitForWindowAsync(division);

                if (hwnd == IntPtr.Zero)
                {
                    onProgress?.Invoke($"{deskLabel} — « {division.DisplayName} » n'a pas pu être positionné (délai dépassé).");
                    continue;
                }

                // Belt-and-suspenders: the window should already be on the right desktop since
                // it was active when the window was created, but this is a harmless no-op if so.
                _desktops.MoveWindowToDesktop(hwnd, virtualDesktop);
                _placement.PlaceWindow(hwnd, regions[j]);
            }
        }

        // Return to the first desktop once everything is set up, rather than leaving the
        // user on whichever desktop happened to be processed last.
        if (virtualDesktops.Count > 0)
            _desktops.SwitchTo(virtualDesktops[0]);

        onProgress?.Invoke("Profil lancé.");
    }
}
