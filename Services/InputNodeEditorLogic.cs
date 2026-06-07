using System.Collections.ObjectModel;
using ComboLab.Models;

namespace ComboLab.Services;

public static class InputNodeEditorLogic
{
    private static readonly string[] AttackOrder =
    [
        "lp", "mp", "hp", "lk", "mk", "hk"
    ];

    public static InputEvent CreateNode(int internalFrame) =>
        new()
        {
            Frame = Math.Max(0, internalFrame),
            DurationFrames = 1
        };

    public static InputEvent CreateNodeAfter(
        IReadOnlyCollection<InputEvent> inputEvents,
        InputEvent? selected)
    {
        var internalFrame = selected is not null
            ? selected.Frame + 1
            : inputEvents.Count == 0
                ? 0
                : inputEvents.Max(item => item.Frame) + 1;
        return CreateNode(internalFrame);
    }

    public static InputEvent DuplicateNode(
        IReadOnlyCollection<InputEvent> inputEvents,
        InputEvent source)
    {
        var usedFrames = inputEvents
            .Select(item => item.Frame)
            .ToHashSet();
        var frame = source.Frame + 1;
        while (usedFrames.Contains(frame))
        {
            frame++;
        }

        return new InputEvent
        {
            Frame = frame,
            DurationFrames = source.DurationFrames ?? 1,
            LogicalInputs = new ObservableCollection<string>(
                source.LogicalInputs)
        };
    }

    public static void SetDirection(InputEvent inputEvent, string direction)
    {
        var state = ReadState(inputEvent);
        state.Direction = direction == "5" ? null : direction;
        WriteState(inputEvent, state);
    }

    public static void ToggleAttack(InputEvent inputEvent, string attack)
    {
        var state = ReadState(inputEvent);
        attack = attack.Trim().ToLowerInvariant();
        if (state.Attacks.Contains(attack, StringComparer.OrdinalIgnoreCase))
        {
            state.Attacks.RemoveAll(item => string.Equals(
                item,
                attack,
                StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            state.Attacks.Add(attack);
        }

        WriteState(inputEvent, state);
    }

    public static void Clear(InputEvent inputEvent)
    {
        inputEvent.LogicalInputs = [];
    }

    public static string FormatNode(InputEvent inputEvent) =>
        string.IsNullOrWhiteSpace(inputEvent.LogicalInputsText)
            ? "(なし)"
            : inputEvent.LogicalInputsText;

    private static NodeInputState ReadState(InputEvent inputEvent)
    {
        var state = new NodeInputState();
        foreach (var input in inputEvent.LogicalInputs)
        {
            var text = (input ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(text)
                || text.StartsWith("key:", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(input))
                {
                    state.Extras.Add(input);
                }
                continue;
            }

            var index = 0;
            if (text.Length > 0 && text[0] is >= '1' and <= '9')
            {
                state.Direction = text[0] == '5' ? null : text[0].ToString();
                index = 1;
            }

            var rest = text[index..];
            if (AttackOrder.Contains(rest, StringComparer.OrdinalIgnoreCase))
            {
                state.Attacks.Add(rest);
            }
            else if (!string.IsNullOrWhiteSpace(rest))
            {
                state.Extras.Add(input!);
            }
        }

        state.Attacks = state.Attacks
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => Array.IndexOf(AttackOrder, item))
            .ToList();
        return state;
    }

    private static void WriteState(InputEvent inputEvent, NodeInputState state)
    {
        var values = new List<string>();
        var orderedAttacks = state.Attacks
            .Where(item => AttackOrder.Contains(
                item,
                StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => Array.IndexOf(AttackOrder, item))
            .ToArray();

        if (!string.IsNullOrWhiteSpace(state.Direction)
            && orderedAttacks.Length == 0)
        {
            values.Add(state.Direction!);
        }
        else if (!string.IsNullOrWhiteSpace(state.Direction)
                 && orderedAttacks.Length == 1)
        {
            values.Add(state.Direction! + orderedAttacks[0]);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(state.Direction))
            {
                values.Add(state.Direction!);
            }

            values.AddRange(orderedAttacks);
        }

        values.AddRange(state.Extras.Where(item =>
            !string.IsNullOrWhiteSpace(item)));
        inputEvent.LogicalInputs = new ObservableCollection<string>(values);
    }

    private sealed class NodeInputState
    {
        public string? Direction { get; set; }

        public List<string> Attacks { get; set; } = [];

        public List<string> Extras { get; } = [];
    }
}
