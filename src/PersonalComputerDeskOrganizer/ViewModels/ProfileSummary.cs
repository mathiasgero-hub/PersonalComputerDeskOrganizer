using PersonalComputerDeskOrganizer.Models;

namespace PersonalComputerDeskOrganizer.ViewModels;

/// <summary>Lightweight wrapper used purely for data-binding the profile tiles on the home screen.</summary>
public class ProfileSummary
{
    public required Profile Profile { get; init; }
    public string Name => Profile.Name;
    public string Initial => Profile.Initial;
}
