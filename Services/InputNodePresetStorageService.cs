using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ComboLab.Models;

namespace ComboLab.Services;

public sealed class InputNodePresetStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public string PresetFilePath =>
        Path.Combine(
            AppDataPathService.EnsureDataDirectory(),
            "input-node-presets.json");

    public ObservableCollection<InputNodePreset> Load()
    {
        if (!File.Exists(PresetFilePath))
        {
            return [];
        }

        var presets = JsonSerializer.Deserialize<ObservableCollection<InputNodePreset>>(
            File.ReadAllText(PresetFilePath),
            JsonOptions);
        return presets ?? [];
    }

    public void Save(IEnumerable<InputNodePreset> presets)
    {
        var directory = AppDataPathService.EnsureDataDirectory();
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            PresetFilePath,
            JsonSerializer.Serialize(presets.ToArray(), JsonOptions));
    }
}
