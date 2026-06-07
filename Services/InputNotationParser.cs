namespace ComboLab.Services;

public sealed class InputNotationParser
{
    public InputParseResult ParseMany(IEnumerable<string>? inputTexts)
    {
        var tokens = new List<InputToken>();
        var errors = new List<InputParseError>();
        foreach (var inputText in inputTexts ?? [])
        {
            var result = Parse(inputText);
            tokens.AddRange(result.Tokens);
            errors.AddRange(result.Errors);
        }

        return new InputParseResult(tokens, errors);
    }

    public InputParseResult Parse(string? inputText)
    {
        var tokens = new List<InputToken>();
        var errors = new List<InputParseError>();
        foreach (var segment in SplitSegments(inputText))
        {
            ParseSegment(segment, tokens, errors);
        }

        return new InputParseResult(tokens, errors);
    }

    private static IEnumerable<string> SplitSegments(string? inputText) =>
        (inputText ?? string.Empty)
            .Split(new[] { ',', '、' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item));

    private static void ParseSegment(
        string segment,
        List<InputToken> tokens,
        List<InputParseError> errors)
    {
        if (segment.StartsWith("key:", StringComparison.OrdinalIgnoreCase))
        {
            var physicalName = segment[4..].Trim();
            if (string.IsNullOrWhiteSpace(physicalName))
            {
                errors.Add(new InputParseError(
                    segment,
                    "key: の後に物理キー名を入力してください。"));
                return;
            }

            tokens.Add(InputToken.Physical(segment, physicalName));
            return;
        }

        var normalized = segment.ToLowerInvariant();
        var index = 0;
        var directionCount = 0;
        while (index < normalized.Length
               && normalized[index] is >= '1' and <= '9')
        {
            directionCount++;
            if (directionCount > 1)
            {
                errors.Add(new InputParseError(
                    segment,
                    $"`{segment}` はコマンド表記です。入力イベントでは `2` / `3` / `6mp` のようにフレームごとに分けてください。"));
                return;
            }

            tokens.Add(InputToken.Logical(
                segment,
                normalized[index].ToString()));
            index++;
        }

        if (index < normalized.Length)
        {
            tokens.Add(InputToken.Logical(segment, normalized[index..]));
        }
    }
}

public sealed record InputParseResult(
    IReadOnlyList<InputToken> Tokens,
    IReadOnlyList<InputParseError> Errors);

public sealed record InputParseError(string Source, string Message);

public sealed record InputToken(
    string Source,
    string Value,
    InputTokenKind Kind)
{
    public static InputToken Logical(string source, string value) =>
        new(source, value, InputTokenKind.Logical);

    public static InputToken Physical(string source, string value) =>
        new(source, value, InputTokenKind.Physical);
}

public enum InputTokenKind
{
    Logical,
    Physical
}

