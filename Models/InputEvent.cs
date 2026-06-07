using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace ComboLab.Models;

public sealed class InputEvent : ObservableObject
{
    private int _frame;
    private int? _durationFrames;
    private ObservableCollection<string> _logicalInputs = [];

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

