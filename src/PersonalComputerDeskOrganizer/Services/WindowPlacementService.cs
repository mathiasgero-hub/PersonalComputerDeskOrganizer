namespace PersonalComputerDeskOrganizer.Services;

/// <summary>
/// Computes the rectangle for each division of a desktop layout, and moves a
/// window's HWND to fill that rectangle exactly. The 1/2/3/4 layouts mirror the
/// ones validated in the configuration screen mockup:
///   1 -> single full-area zone
///   2 -> two equal columns
///   3 -> one full-width band on top, two equal columns below
///   4 -> 2x2 grid
///
/// NOTE: v1 targets the primary monitor's work area (taskbar excluded). Spanning
/// divisions across multiple physical monitors is a natural follow-up enhancement
/// (see README "idées d'amélioration").
/// </summary>
public class WindowPlacementService
{
    public List<RECT> ComputeRegions(int layout)
    {
        var area = NativeMethods.GetPrimaryWorkArea();
        int left = area.Left, top = area.Top, w = area.Width, h = area.Height;
        int halfW = w / 2, halfH = h / 2;

        return layout switch
        {
            1 => new List<RECT>
            {
                Rect(left, top, w, h)
            },
            2 => new List<RECT>
            {
                Rect(left, top, halfW, h),
                Rect(left + halfW, top, w - halfW, h)
            },
            3 => new List<RECT>
            {
                Rect(left, top, w, halfH),                              // top band, full width
                Rect(left, top + halfH, halfW, h - halfH),               // bottom-left
                Rect(left + halfW, top + halfH, w - halfW, h - halfH)    // bottom-right
            },
            4 => new List<RECT>
            {
                Rect(left, top, halfW, halfH),
                Rect(left + halfW, top, w - halfW, halfH),
                Rect(left, top + halfH, halfW, h - halfH),
                Rect(left + halfW, top + halfH, w - halfW, h - halfH)
            },
            _ => new List<RECT> { Rect(left, top, w, h) }
        };
    }

    private static RECT Rect(int x, int y, int width, int height) => new()
    {
        Left = x, Top = y, Right = x + width, Bottom = y + height
    };

    /// <summary>Removes any maximized state, then moves and resizes the window to exactly fill <paramref name="region"/>.</summary>
    public void PlaceWindow(IntPtr hwnd, RECT region)
    {
        if (hwnd == IntPtr.Zero) return;

        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);

        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            region.Left, region.Top, region.Width, region.Height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }
}
