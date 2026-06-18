namespace PersonalComputerDeskOrganizer.Models;

/// <summary>
/// A complete, saved configuration: a name, an icon initial, and the list of
/// virtual desktops (each with its own layout and division assignments) that
/// get recreated when the profile is launched.
/// </summary>
public class Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime LastModifiedAt { get; set; } = DateTime.Now;

    public List<DesktopConfig> Desktops { get; set; } = new();

    /// <summary>First letter of <see cref="Name"/>, shown on the profile tile on the home screen.</summary>
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : Name.Trim()[0].ToString().ToUpperInvariant();

    public Profile Clone() => new()
    {
        Id = Id,
        Name = Name,
        CreatedAt = CreatedAt,
        LastModifiedAt = LastModifiedAt,
        Desktops = Desktops.Select(d => d.Clone()).ToList()
    };
}
