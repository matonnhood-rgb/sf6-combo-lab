using System.Text.Json.Serialization;

namespace ComboLab.Models;

public sealed class ComboStep : ObservableObject
{
    private TimelineNodeType _nodeType = TimelineNodeType.Action;
    private int? _inputFrame;
    private string _actionName = string.Empty;
    private string _memoText = string.Empty;
    private Guid? _calledComboId;
    private string _calledComboName = string.Empty;

    public TimelineNodeType NodeType
    {
        get => _nodeType;
        set
        {
            if (SetProperty(ref _nodeType, value))
            {
                OnPropertyChanged(nameof(NodeTypeDisplay));
                OnPropertyChanged(nameof(Content));
            }
        }
    }

    [JsonIgnore]
    public string NodeTypeDisplay => NodeType switch
    {
        TimelineNodeType.Action => "アクション",
        TimelineNodeType.Note => "メモ",
        TimelineNodeType.ComboCall => "コンボ呼び出し",
        _ => NodeType.ToString()
    };

    public int? InputFrame
    {
        get => _inputFrame;
        set => SetProperty(
            ref _inputFrame,
            value is null ? null : Math.Max(0, value.Value));
    }

    public string ActionName
    {
        get => _actionName;
        set
        {
            if (SetProperty(ref _actionName, value))
            {
                OnPropertyChanged(nameof(Content));
            }
        }
    }

    public string MemoText
    {
        get => _memoText;
        set
        {
            if (SetProperty(ref _memoText, value))
            {
                OnPropertyChanged(nameof(Content));
            }
        }
    }

    public Guid? CalledComboId
    {
        get => _calledComboId;
        set => SetProperty(ref _calledComboId, value);
    }

    public string CalledComboName
    {
        get => _calledComboName;
        set
        {
            if (SetProperty(ref _calledComboName, value))
            {
                OnPropertyChanged(nameof(Content));
            }
        }
    }

    [JsonIgnore]
    public string Content
    {
        get => NodeType switch
        {
            TimelineNodeType.Action => ActionName,
            TimelineNodeType.Note => MemoText,
            TimelineNodeType.ComboCall => CalledComboName,
            _ => string.Empty
        };
        set
        {
            switch (NodeType)
            {
                case TimelineNodeType.Action:
                    ActionName = value;
                    break;
                case TimelineNodeType.Note:
                    MemoText = value;
                    break;
                case TimelineNodeType.ComboCall:
                    CalledComboName = value;
                    break;
            }
        }
    }

    // schemaVersion 5以前の読み込み互換用です。
    [JsonPropertyName("stepType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ComboStepType? LegacyStepType { get; set; }

    [JsonPropertyName("inputName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyInputName { get; set; }

    [JsonPropertyName("waitFrames")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int LegacyWaitFrames { get; set; }

    [JsonPropertyName("holdFrames")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int LegacyHoldFrames { get; set; }

    [JsonPropertyName("memo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyMemo { get; set; }
}
