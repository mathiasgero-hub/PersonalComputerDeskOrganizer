namespace PersonalComputerDeskOrganizer.Models;

/// <summary>
/// Configuration for a single virtual desktop: how many zones it is split into,
/// and what each zone should open.
/// </summary>
public class DesktopConfig
{
    /// <summary>1 = not divided (single zone), 2/3/4 = number of zones.</summary>
    public int Layout { get; set; } = 1;

    public List<DivisionConfig> Divisions { get; set; } = new() { new DivisionConfig() };

    /// <summary>Optional friendly label, e.g. "Bureau 1". Defaults to the desktop's position if empty.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// Resizes <see cref="Divisions"/> to match <see cref="Layout"/>, keeping existing
    /// assignments where possible and padding with empty divisions otherwise.
    /// </summary>
    public void EnsureDivisionCount()
    {
        int target = Layout switch { 1 => 1, 2 => 2, 3 => 3, 4 => 4, _ => 1 };

        if (Divisions.Count > target)
            Divisions = Divisions.Take(target).ToList();

        while (Divisions.Count < target)
            Divisions.Add(new DivisionConfig());
    }

    public DesktopConfig Clone()
    {
        var clone = new DesktopConfig { Layout = Layout, Name = Name };
        clone.Divisions = Divisions.Select(d => d.Clone()).ToList();
        return clone;
    }
}
