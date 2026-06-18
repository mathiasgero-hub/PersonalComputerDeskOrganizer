namespace PersonalComputerDeskOrganizer.Models;

/// <summary>What kind of content a screen division should open.</summary>
public enum DivisionType
{
    Empty,
    App,
    File,
    Url
}

/// <summary>
/// One rectangular zone of a virtual desktop. A desktop split into N divisions
/// has N of these; an undivided desktop has exactly one with index 0.
/// </summary>
public class DivisionConfig
{
    public DivisionType Type { get; set; } = DivisionType.Empty;

    /// <summary>Friendly text shown in the editor (app name, file name, or host name of a URL).</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// What actually gets launched:
    /// - App: full path to the executable (or shell:AppsFolder\{AUMID} for a packaged/UWP app)
    /// - File: full path to the file to open with its associated application
    /// - Url: the address to open in the default browser
    /// </summary>
    public string? LaunchTarget { get; set; }

    /// <summary>Optional command-line arguments, only meaningful for Type == App.</summary>
    public string? Arguments { get; set; }

    /// <summary>True if this division currently has something assigned to it.</summary>
    public bool IsFilled => Type != DivisionType.Empty && !string.IsNullOrWhiteSpace(LaunchTarget);

    public DivisionConfig Clone() => new()
    {
        Type = Type,
        DisplayName = DisplayName,
        LaunchTarget = LaunchTarget,
        Arguments = Arguments
    };
}
