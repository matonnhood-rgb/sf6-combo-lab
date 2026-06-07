using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace ComboLab.Models;

public sealed class ComboLibrary
{
    public int SchemaVersion { get; set; } = 7;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ObservableCollection<ComboFolder>? Folders { get; set; }

    public ObservableCollection<ComboRecipe> Combos { get; set; } = [];

    public ObservableCollection<ActionDefinition> ActionDefinitions { get; set; } = [];
}
