namespace ComboLab.Services;

public static class PhysicalKeyNameParser
{
    private static readonly HashSet<string> NeutralNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ニュートラル",
            "Neutral",
            "なし",
            "None"
        };

    private static readonly IReadOnlyDictionary<char, ushort> LetterScanCodes =
        new Dictionary<char, ushort>
        {
            ['A'] = 0x1E, ['B'] = 0x30, ['C'] = 0x2E, ['D'] = 0x20,
            ['E'] = 0x12, ['F'] = 0x21, ['G'] = 0x22, ['H'] = 0x23,
            ['I'] = 0x17, ['J'] = 0x24, ['K'] = 0x25, ['L'] = 0x26,
            ['M'] = 0x32, ['N'] = 0x31, ['O'] = 0x18, ['P'] = 0x19,
            ['Q'] = 0x10, ['R'] = 0x13, ['S'] = 0x1F, ['T'] = 0x14,
            ['U'] = 0x16, ['V'] = 0x2F, ['W'] = 0x11, ['X'] = 0x2D,
            ['Y'] = 0x15, ['Z'] = 0x2C
        };

    private static readonly IReadOnlyDictionary<char, ushort> DigitScanCodes =
        new Dictionary<char, ushort>
        {
            ['0'] = 0x0B, ['1'] = 0x02, ['2'] = 0x03, ['3'] = 0x04,
            ['4'] = 0x05, ['5'] = 0x06, ['6'] = 0x07, ['7'] = 0x08,
            ['8'] = 0x09, ['9'] = 0x0A
        };

    private static readonly IReadOnlyDictionary<string, KeyCodePair> NamedKeys =
        new Dictionary<string, KeyCodePair>(StringComparer.OrdinalIgnoreCase)
        {
            ["Space"] = new(0x20, 0x39),
            ["スペース"] = new(0x20, 0x39),
            ["Enter"] = new(0x0D, 0x1C),
            ["Return"] = new(0x0D, 0x1C),
            ["Tab"] = new(0x09, 0x0F),
            ["Escape"] = new(0x1B, 0x01),
            ["Esc"] = new(0x1B, 0x01),
            ["Backspace"] = new(0x08, 0x0E),
            ["Delete"] = new(0x2E, 0x53, true),
            ["Insert"] = new(0x2D, 0x52, true),
            ["Home"] = new(0x24, 0x47, true),
            ["End"] = new(0x23, 0x4F, true),
            ["PageUp"] = new(0x21, 0x49, true),
            ["PageDown"] = new(0x22, 0x51, true),
            ["Up"] = new(0x26, 0x48, true),
            ["Down"] = new(0x28, 0x50, true),
            ["Left"] = new(0x25, 0x4B, true),
            ["Right"] = new(0x27, 0x4D, true),
            ["Shift"] = new(0x10, 0x2A),
            ["Ctrl"] = new(0x11, 0x1D),
            ["Control"] = new(0x11, 0x1D),
            ["Alt"] = new(0x12, 0x38)
        };

    public static bool IsNeutral(string? keyName) =>
        string.IsNullOrWhiteSpace(keyName)
        || NeutralNames.Contains(keyName.Trim());

    public static bool TryParse(string? keyName, out PhysicalKey key)
    {
        key = default;
        if (IsNeutral(keyName))
        {
            return false;
        }

        var normalized = keyName!.Trim();
        if (normalized.Length == 1)
        {
            var character = char.ToUpperInvariant(normalized[0]);
            if (character is >= 'A' and <= 'Z'
                || character is >= '0' and <= '9')
            {
                var scanCode = character is >= 'A' and <= 'Z'
                    ? LetterScanCodes[character]
                    : DigitScanCodes[character];
                key = new PhysicalKey(
                    character,
                    scanCode,
                    normalized.ToUpperInvariant());
                return true;
            }
        }

        if (NamedKeys.TryGetValue(normalized, out var codePair))
        {
            key = new PhysicalKey(
                codePair.VirtualKey,
                codePair.ScanCode,
                normalized,
                codePair.IsExtended);
            return true;
        }

        if (normalized.Length is 2 or 3
            && normalized[0] is 'F' or 'f'
            && int.TryParse(normalized[1..], out var functionNumber)
            && functionNumber is >= 1 and <= 24)
        {
            key = new PhysicalKey(
                (ushort)(0x70 + functionNumber - 1),
                GetFunctionScanCode(functionNumber),
                $"F{functionNumber}");
            return true;
        }

        return false;
    }

    private static ushort GetFunctionScanCode(int functionNumber) =>
        functionNumber switch
        {
            <= 10 => (ushort)(0x3A + functionNumber),
            11 => 0x57,
            12 => 0x58,
            <= 23 => (ushort)(0x57 + functionNumber - 12),
            24 => 0x76,
            _ => 0
        };

    private readonly record struct KeyCodePair(
        ushort VirtualKey,
        ushort ScanCode,
        bool IsExtended = false);
}
