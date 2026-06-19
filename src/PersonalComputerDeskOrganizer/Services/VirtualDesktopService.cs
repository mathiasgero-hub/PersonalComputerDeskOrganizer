using WindowsDesktop;

namespace PersonalComputerDeskOrganizer.Services;

/// <summary>
/// Thin wrapper around the Slions.VirtualDesktop library (namespace WindowsDesktop).
/// Isolated behind this class so that if a future Windows update breaks the
/// underlying COM interop and the library's API needs adjusting or swapping,
/// only this one file has to change.
///
/// IMPORTANT: this relies on undocumented Windows interfaces. Slions.VirtualDesktop
/// mitigates breakage across Windows versions by recompiling its interop layer
/// against the running build at first use, but a brand-new Windows release can
/// still require a library update. If desktop creation throws here, check for a
/// newer Slions.VirtualDesktop release before assuming the app itself is broken.
/// </summary>
public class VirtualDesktopService
{
    /// <summary>
    /// Returns exactly <paramref name="count"/> virtual desktops, reusing whatever
    /// already exists and creating new ones (appended to the right) as needed.
    /// </summary>
    public List<VirtualDesktop> GetOrCreateDesktops(int count)
    {
        var existing = VirtualDesktop.GetDesktops().ToList();

        while (existing.Count < count)
            existing.Add(VirtualDesktop.Create());

        return existing.Take(count).ToList();
    }

    /// <summary>
    /// Switches to the given desktop and waits until Windows confirms the switch
    /// actually took effect (VirtualDesktop.Current matches), retrying the switch
    /// if needed. A bare "fire and forget" Switch() call can occasionally be a
    /// no-op visually (e.g. if called before the previous switch's animation
    /// settled), which is what caused windows to land on the wrong desktop.
    /// </summary>
    public async Task<bool> SwitchToAsync(VirtualDesktop desktop)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                desktop.Switch();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Desktop switch attempt failed: {ex.Message}");
            }

            await Task.Delay(150);

            try
            {
                if (VirtualDesktop.Current.Id == desktop.Id)
                    return true;
            }
            catch
            {
                // VirtualDesktop.Current can occasionally throw right after a switch;
                // just retry on the next loop iteration.
            }
        }

        return false;
    }

    /// <summary>Moves a window belonging to any process onto the given virtual desktop.</summary>
    public void MoveWindowToDesktop(IntPtr hwnd, VirtualDesktop desktop)
    {
        if (hwnd == IntPtr.Zero) return;

        try
        {
            VirtualDesktop.MoveToDesktop(hwnd, desktop);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to move window to desktop: {ex.Message}");
        }
    }
}
