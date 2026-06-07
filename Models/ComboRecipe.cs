using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace ComboLab.Models;

public sealed class ComboRecipe : ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private Guid _folderId;
    private string _gameName = "Street Fighter 6";
    private string _characterName = string.Empty;
    private string _comboName = "新しいコンボ";
    private string _screenPosition = string.Empty;
    private string _starterCondition = string.Empty;
    private string _meterUsage = string.Empty;
    private string _purpose = string.Empty;
    private string _freeCategory = string.Empty;
    private int? _damage;
    private string _notes = string.Empty;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Guid FolderId
    {
        get => _folderId;
        set => SetProperty(ref _folderId, value);
    }

    public string GameName
    {
        get => _gameName;
        set => SetProperty(ref _gameName, value);
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string CharacterName
    {
        get => _characterName;
        set => SetProperty(ref _characterName, value);
    }

    public string ComboName
    {
        get => _comboName;
        set => SetProperty(ref _comboName, value);
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string ScreenPosition
    {
        get => _screenPosition;
        set => SetProperty(ref _screenPosition, value);
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string StarterCondition
    {
        get => _starterCondition;
        set => SetProperty(ref _starterCondition, value);
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string MeterUsage
    {
        get => _meterUsage;
        set => SetProperty(ref _meterUsage, value);
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Purpose
    {
        get => _purpose;
        set => SetProperty(ref _purpose, value);
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string FreeCategory
    {
        get => _freeCategory;
        set => SetProperty(ref _freeCategory, value);
    }

    public int? Damage
    {
        get => _damage;
        set => SetProperty(ref _damage, value is < 0 ? 0 : value);
    }

    [JsonConverter(typeof(FlexibleStringCollectionConverter))]
    public ObservableCollection<string> Tags { get; set; } = [];

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public ObservableCollection<ComboStep> Steps { get; set; } = [];
}
