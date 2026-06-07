using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using ComboLab.Models;

namespace ComboLab.Services;

public sealed class KeyMapProfileStorageService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string FilePath =>
        Path.Combine(AppDataPathService.EnsureDataDirectory(), "keymaps.json");

    public async Task<KeyMapProfileLibrary> LoadOrCreateAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            var created = KeyMapDefaults.CreateLibrary();
            await SaveAsync(created, cancellationToken);
            return created;
        }

        await using var stream = File.OpenRead(FilePath);
        var library = await JsonSerializer.DeserializeAsync<KeyMapProfileLibrary>(
            stream,
            SerializerOptions,
            cancellationToken);

        return Normalize(library ?? KeyMapDefaults.CreateLibrary());
    }

    public async Task SaveAsync(
        KeyMapProfileLibrary library,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(library);
        await using var stream = File.Create(FilePath);
        await JsonSerializer.SerializeAsync(
            stream,
            normalized,
            SerializerOptions,
            cancellationToken);
    }

    public KeyMapProfileLibrary Normalize(KeyMapProfileLibrary library)
    {
        library.SchemaVersion = 1;
        library.Profiles ??= [];
        if (library.Profiles.Count == 0)
        {
            library.Profiles.Add(KeyMapDefaults.CreateLibrary().Profiles[0]);
        }

        foreach (var profile in library.Profiles)
        {
            profile.Bindings ??= [];
            foreach (var action in KeyMapDefaults.DefaultActions)
            {
                if (!profile.Bindings.ContainsKey(action))
                {
                    profile.Bindings[action] = [];
                }
            }

            foreach (var key in profile.Bindings.Keys.ToArray())
            {
                var normalizedKey = KeyMapDefaults.NormalizeActionName(key);
                if (KeyMapDefaults.GeneratedActions.Contains(
                        normalizedKey,
                        StringComparer.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(normalizedKey))
                {
                    profile.Bindings.Remove(key);
                    continue;
                }

                var targetKey = key;
                if (!string.Equals(key, normalizedKey, StringComparison.Ordinal))
                {
                    var values = profile.Bindings[key];
                    profile.Bindings.Remove(key);
                    profile.Bindings[normalizedKey] = values;
                    targetKey = normalizedKey;
                }

                profile.Bindings[targetKey] = new ObservableCollection<string>(
                    profile.Bindings[targetKey]
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Select(item => item.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase));
            }
        }

        if (!library.Profiles.Any(profile => profile.Id == library.ActiveProfileId))
        {
            library.ActiveProfileId = library.Profiles[0].Id;
        }

        return library;
    }
}
