using System.Collections.ObjectModel;

namespace ComboLab.Models;

public sealed class InputNodePreset : ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _name = "新しいプリセット";
    private int _durationFrames = 1;
    private string _displayLabel = string.Empty;
    private string _colorTag = string.Empty;
    private ObservableCollection<string> _logicalInputs = [];
    private ObservableCollection<InputEventMarker> _markers = [];

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value == Guid.Empty ? Guid.NewGuid() : value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, string.IsNullOrWhiteSpace(value) ? "プリセット" : value);
    }

    public ObservableCollection<string> LogicalInputs
    {
        get => _logicalInputs;
        set => SetProperty(ref _logicalInputs, value ?? []);
    }

    public int DurationFrames
    {
        get => _durationFrames;
        set => SetProperty(ref _durationFrames, Math.Max(1, value));
    }

    public string DisplayLabel
    {
        get => _displayLabel;
        set => SetProperty(ref _displayLabel, value ?? string.Empty);
    }

    public string ColorTag
    {
        get => _colorTag;
        set => SetProperty(ref _colorTag, value ?? string.Empty);
    }

    public ObservableCollection<InputEventMarker> Markers
    {
        get => _markers;
        set => SetProperty(ref _markers, value ?? []);
    }
}
