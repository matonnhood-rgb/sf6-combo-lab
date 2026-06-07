using System.Collections.ObjectModel;

namespace ComboLab.Models;

public sealed class KeyMapProfile : ObservableObject
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = "Keyboard Default";
    private ObservableDictionary<string, ObservableCollection<string>> _bindings =
        KeyMapDefaults.CreateDefaultBindings();

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, string.IsNullOrWhiteSpace(value)
            ? Guid.NewGuid().ToString("N")
            : value.Trim());
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, string.IsNullOrWhiteSpace(value)
            ? "Keyboard Default"
            : value.Trim());
    }

    public ObservableDictionary<string, ObservableCollection<string>> Bindings
    {
        get => _bindings;
        set => SetProperty(ref _bindings, value ?? []);
    }
}

public sealed class KeyMapProfileLibrary
{
    public int SchemaVersion { get; set; } = 1;

    public string ActiveProfileId { get; set; } = "default-keyboard";

    public ObservableCollection<KeyMapProfile> Profiles { get; set; } = [];
}

public static class KeyMapDefaults
{
    public static readonly IReadOnlyList<string> DefaultActions =
    [
        "2", "4", "6", "8", "lp", "mp", "hp", "lk", "mk", "hk"
    ];

    public static readonly IReadOnlyList<string> GeneratedActions =
    [
        "1", "3", "5", "7", "9"
    ];

    public static readonly IReadOnlyList<string> PreviewDirections =
    [
        "1", "3", "5", "7", "9"
    ];

    public static KeyMapProfileLibrary CreateLibrary()
    {
        var profile = new KeyMapProfile
        {
            Id = "default-keyboard",
            Name = "Keyboard Default",
            Bindings = CreateDefaultBindings()
        };

        return new KeyMapProfileLibrary
        {
            SchemaVersion = 1,
            ActiveProfileId = profile.Id,
            Profiles = [profile]
        };
    }

    public static ObservableDictionary<string, ObservableCollection<string>>
        CreateDefaultBindings() =>
        new()
        {
            ["2"] = ["S"],
            ["4"] = ["A"],
            ["6"] = ["D"],
            ["8"] = ["W"],
            ["lp"] = ["J"],
            ["mp"] = ["K"],
            ["hp"] = ["L"],
            ["lk"] = ["U"],
            ["mk"] = ["I"],
            ["hk"] = ["O"]
        };

    public static string NormalizeActionName(string? action) =>
        (action ?? string.Empty).Trim().ToLowerInvariant();
}
