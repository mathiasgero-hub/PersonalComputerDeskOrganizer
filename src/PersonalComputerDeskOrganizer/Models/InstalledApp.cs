namespace PersonalComputerDeskOrganizer.Models;

/// <summary>Source an installed application was discovered from, kept mainly for diagnostics.</summary>
public enum InstalledAppSource
{
    StartMenuShortcut,
    RegistryUninstallEntry,
    PackagedApp
}

/// <summary>One entry in the searchable "installed applications" picker list.</summary>
public class InstalledApp
{
    public string Name { get; set; } = "";

    /// <summary>
    /// What to hand to <c>Process.Start</c>. For packaged (UWP/Store) apps this is
    /// "shell:AppsFolder\{AppUserModelId}", which Explorer resolves and activates
    /// correctly without needing the full WinRT activation API.
    /// </summary>
    public string LaunchTarget { get; set; } = "";

    public InstalledAppSource Source { get; set; }

    public override string ToString() => Name;
}
