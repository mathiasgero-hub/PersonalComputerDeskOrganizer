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

                _desktops.MoveWindowToDesktop(hwnd, virtualDesktop);
                _placement.PlaceWindow(hwnd, regions[j]);
            }
        }

        onProgress?.Invoke("Profil lancé.");
    }
}
