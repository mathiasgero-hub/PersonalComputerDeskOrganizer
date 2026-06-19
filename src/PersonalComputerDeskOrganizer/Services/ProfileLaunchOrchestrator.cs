using PersonalComputerDeskOrganizer.Models;

namespace PersonalComputerDeskOrganizer.Services;

public class ProfileLaunchOrchestrator
{
    private readonly VirtualDesktopService _desktops = new();
    private readonly WindowPlacementService _placement = new();
    private readonly AppLauncherService _launcher = new();

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

            bool switched = await _desktops.SwitchToAsync(virtualDesktop);
            if (!switched)
                onProgress?.Invoke($"{deskLabel} — le changement de bureau n'a pas pu être confirmé, on continue quand même.");

            var regions = _placement.ComputeRegions(deskConfig.Layout);

            for (int j = 0; j < deskConfig.Divisions.Count && j < regions.Count; j++)
            {
                var division = deskConfig.Divisions[j];
                if (!division.IsFilled) continue;

                onProgress?.Invoke($"{deskLabel} — ouverture de « {division.DisplayName} »…");

                await _desktops.SwitchToAsync(virtualDesktop);

                IntPtr hwnd = await _launcher.LaunchAndWaitForWindowAsync(division);

                if (hwnd == IntPtr.Zero)
                {
                    onProgress?.Invoke($"{deskLabel} — « {division.DisplayName} » n'a pas pu être positionné (délai dépassé).");
                    continue;
                }

                _desktops.MoveWindowToDesktop(hwnd, virtualDesktop);
                _placement.PlaceWindow(hwnd, regions[j]);

                _ = ReassertPlacementAsync(hwnd, regions[j]);
            }
        }

        if (virtualDesktops.Count > 0)
            await _desktops.SwitchToAsync(virtualDesktops[0]);

        onProgress?.Invoke("Profil lancé.");
    }

    private async Task ReassertPlacementAsync(IntPtr hwnd, RECT region)
    {
        foreach (var delay in new[] { 1500, 3000, 5000 })
        {
            await Task.Delay(delay);
            _placement.PlaceWindow(hwnd, region);
        }
    }
}
