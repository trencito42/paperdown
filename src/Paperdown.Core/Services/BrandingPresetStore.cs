using System.Text.Json;
using Paperdown.Core.Models;

namespace Paperdown.Core.Services;

public sealed class BrandingPresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _directory;

    public BrandingPresetStore()
    {
        _directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Paperdown",
            "branding-presets");
        Directory.CreateDirectory(_directory);
    }

    public IReadOnlyList<string> ListPresetNames()
    {
        return Directory.EnumerateFiles(_directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .Cast<string>()
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Save(string name, BrandingSettings branding)
    {
        var path = Path.Combine(_directory, Sanitize(name) + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(branding, JsonOptions));
    }

    public BrandingSettings? Load(string name)
    {
        var path = Path.Combine(_directory, Sanitize(name) + ".json");
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<BrandingSettings>(File.ReadAllText(path), JsonOptions);
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Trim();
    }
}
