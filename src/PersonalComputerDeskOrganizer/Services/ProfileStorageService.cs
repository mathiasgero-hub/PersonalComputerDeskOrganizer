using System.IO;
using System.Text.Json;
using PersonalComputerDeskOrganizer.Models;

namespace PersonalComputerDeskOrganizer.Services;

/// <summary>
/// Reads and writes <see cref="Profile"/> objects as individual JSON files under
/// %AppData%\PersonalComputerDeskOrganizer\Profiles. Plain files were chosen over a
/// database because the data is small, rarely written, and benefits from being
/// human-readable / easy to back up or hand-edit.
/// </summary>
public class ProfileStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _profilesFolder;

    public ProfileStorageService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _profilesFolder = Path.Combine(appData, "PersonalComputerDeskOrganizer", "Profiles");
        Directory.CreateDirectory(_profilesFolder);
    }

    private string PathFor(string profileId) => Path.Combine(_profilesFolder, $"{profileId}.json");

    public List<Profile> LoadAll()
    {
        var profiles = new List<Profile>();

        foreach (var file in Directory.EnumerateFiles(_profilesFolder, "*.json"))
        {
            try
            {
                string json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<Profile>(json, JsonOptions);
                if (profile is not null)
                    profiles.Add(profile);
            }
            catch (Exception ex)
            {
                // A single corrupt profile file should never prevent the rest from loading.
                System.Diagnostics.Debug.WriteLine($"Failed to load profile '{file}': {ex.Message}");
            }
        }

        return profiles.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public void Save(Profile profile)
    {
        profile.LastModifiedAt = DateTime.Now;
        string json = JsonSerializer.Serialize(profile, JsonOptions);
        File.WriteAllText(PathFor(profile.Id), json);
    }

    public void Delete(string profileId)
    {
        string path = PathFor(profileId);
        if (File.Exists(path))
            File.Delete(path);
    }
}
