namespace PersonalComputerDeskOrganizer.Services;

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
                Rect(left, top, halfW, h),
                Rect(left + halfW, top, w - halfW, halfH),
                Rect(left + halfW, top + halfH, w - halfW, h - halfH)
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
