using System.IO;
using System.Text.Json;
using PersonalComputerDeskOrganizer.Models;

namespace PersonalComputerDeskOrganizer.Services;

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

    public void ExportToFile(Profile profile, string filePath)
    {
        string json = JsonSerializer.Serialize(profile, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    public Profile ImportFromFile(string filePath)
    {
        string json = File.ReadAllText(filePath);
        var profile = JsonSerializer.Deserialize<Profile>(json, JsonOptions)
            ?? throw new InvalidOperationException("Fichier de profil invalide ou corrompu.");

        profile.Id = Guid.NewGuid().ToString("N");
        profile.CreatedAt = DateTime.Now;
        profile.LastModifiedAt = DateTime.Now;
        return profile;
    }
}
