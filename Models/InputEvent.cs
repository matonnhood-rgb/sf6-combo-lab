using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace ComboLab.Models;

public sealed class InputEvent : ObservableObject
{
    private int _frame;
    private int? _durationFrames;
    private string _displayLabel = string.Empty;
    private string _colorTag = string.Empty;
    private int? _visualLane;
    private ObservableCollection<string> _logicalInputs = [];
    private ObservableCollection<InputEventMarker> _markers = [];

    public int Frame
    {
        get => _frame;
        set
        {
            if (SetProperty(ref _frame, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(DisplayFrame));
            }
        }
    }

    [JsonIgnore]
    public int DisplayFrame
    {
        get => Frame + 1;
        set => Frame = Math.Max(1, value) - 1;
    }

    public int? DurationFrames
    {
        get => _durationFrames;
        set
        {
            int? normalized = value is null
                ? null
                : Math.Max(1, value.Value);
            if (SetProperty(ref _durationFrames, normalized))
            {
                OnPropertyChanged(nameof(DisplayDurationFrames));
            }
        }
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

    public int? VisualLane
    {
        get => _visualLane;
        set => SetProperty(ref _visualLane, value is null ? null : Math.Max(0, value.Value));
    }

    [JsonIgnore]
    public int DisplayDurationFrames
    {
        get => DurationFrames ?? 1;
        set => DurationFrames = Math.Max(1, value);
    }

    public ObservableCollection<string> LogicalInputs
    {
        get => _logicalInputs;
        set
        {
            if (SetProperty(ref _logicalInputs, value ?? []))
            {
                OnPropertyChanged(nameof(LogicalInputsText));
            }
        }
    }

    public ObservableCollection<InputEventMarker> Markers
    {
        get => _markers;
        set => SetProperty(ref _markers, value ?? []);
    }

    [JsonIgnore]
    public string LogicalInputsText
    {
        get => string.Join(", ", LogicalInputs);
        set
        {
            var normalized = (value ?? string.Empty)
                .Split(new[] { ',', '、' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(input => input.Trim())
                .Where(input => !string.IsNullOrWhiteSpace(input))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            LogicalInputs.Clear();
            foreach (var input in normalized)
            {
                LogicalInputs.Add(input);
            }

            OnPropertyChanged();
        }
    }
}

public sealed class InputEventMarker : ObservableObject
{
    private int _offsetFrame;
    private string _label = string.Empty;
    private string _kind = string.Empty;

    public int OffsetFrame
    {
        get => _offsetFrame;
        set => SetProperty(ref _offsetFrame, Math.Max(0, value));
    }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value ?? string.Empty);
    }

    public string Kind
    {
        get => _kind;
        set => SetProperty(ref _kind, value ?? string.Empty);
    }
}

