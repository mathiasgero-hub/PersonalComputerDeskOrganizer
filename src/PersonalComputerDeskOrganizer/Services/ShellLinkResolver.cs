using System.Runtime.InteropServices;
using System.Text;

namespace PersonalComputerDeskOrganizer.Services;

/// <summary>
/// Resolves the real target executable of a Windows .lnk shortcut using the
/// standard IShellLinkW / IPersistFile COM interfaces. This is the same mechanism
/// Explorer itself uses, and it is the most reliable way to know what a Start
/// Menu shortcut actually launches (registry uninstall entries often don't say).
/// </summary>
internal static class ShellLinkResolver
{
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath(StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription(StringBuilder pszName, int cchMaxName);
        void SetDescription(string pszName);
        void GetWorkingDirectory(StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory(string pszDir);
        void GetArguments(StringBuilder pszArgs, int cchMaxPath);
        void SetArguments(string pszArgs);
        void GetHotkey(out short wHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int iShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation(StringBuilder pszIconPath, int cchIconPath, out int iIcon);
        void SetIconLocation(string pszIconPath, int iIcon);
        void SetRelativePath(string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath(string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load(string pszFileName, int dwMode);
        void Save(string pszFileName, bool fRemember);
        void SaveCompleted(string pszFileName);
        void GetCurFile(out string ppszFileName);
    }

    /// <summary>Returns the resolved target path of a .lnk file, or null if it cannot be read.</summary>
    public static string? ResolveTarget(string lnkPath)
    {
        try
        {
            var link = (IShellLinkW)new ShellLink();
            ((IPersistFile)link).Load(lnkPath, 0 /* STGM_READ */);

            var pathBuilder = new StringBuilder(260);
            link.GetPath(pathBuilder, pathBuilder.Capacity, IntPtr.Zero, 0);

            string target = pathBuilder.ToString();
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch
        {
            return null;
        }
    }

    public static string? ResolveArguments(string lnkPath)
    {
        try
        {
            var link = (IShellLinkW)new ShellLink();
            ((IPersistFile)link).Load(lnkPath, 0);

            var argsBuilder = new StringBuilder(1024);
            link.GetArguments(argsBuilder, argsBuilder.Capacity);
            return argsBuilder.ToString();
        }
        catch
        {
            return null;
        }
    }
}
