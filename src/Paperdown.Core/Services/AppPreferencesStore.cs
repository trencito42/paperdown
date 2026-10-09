using System.Text.Json;
using Paperdown.Core.Interfaces;
using Paperdown.Core.Models;

namespace Paperdown.Core.Services;

public sealed class AppPreferencesStore : IAppPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public AppPreferencesStore(string? filePath = null)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Paperdown");
        Directory.CreateDirectory(root);
        _filePath = filePath ?? Path.Combine(root, "preferences.json");
    }

    public AppPreferences Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppPreferences();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppPreferences>(json, JsonOptions) ?? new AppPreferences();
        }
        catch
        {
            return new AppPreferences();
        }
    }

    public void Save(AppPreferences preferences)
    {
        var json = JsonSerializer.Serialize(preferences, JsonOptions);
        File.WriteAllText(_filePath, json);
    }
}
