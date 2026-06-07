using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ComboLab.Models;

namespace ComboLab.Services;

public sealed class JsonComboStorageService : IComboStorageService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task SaveAsync(
        string filePath,
        ComboLibrary library,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(
            stream,
            library,
            SerializerOptions,
            cancellationToken);
    }

    public async Task<ComboLibrary> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var library = await JsonSerializer.DeserializeAsync<ComboLibrary>(
            stream,
            SerializerOptions,
            cancellationToken);

        return library
            ?? throw new InvalidDataException("JSONファイルの内容が空です。");
    }
}
