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

    public void SwitchTo(VirtualDesktop desktop) => desktop.Switch();

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
