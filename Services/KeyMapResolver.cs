using ComboLab.Models;

namespace ComboLab.Services;

public sealed class KeyMapResolver
{
    private static readonly IReadOnlyDictionary<string, string[]> DiagonalDirections =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = ["4", "2"],
            ["3"] = ["6", "2"],
            ["7"] = ["4", "8"],
            ["9"] = ["6", "8"]
        };

    private readonly KeyMapProfile _profile;

    public KeyMapResolver(KeyMapProfile profile)
    {
        _profile = profile;
    }

    public KeyResolveResult Resolve(IEnumerable<InputToken> tokens)
    {
        var keys = new List<PhysicalKey>();
        var errors = new List<KeyResolveError>();
        foreach (var token in tokens)
        {
            if (token.Kind == InputTokenKind.Physical)
            {
                AddPhysicalKey(token.Value, token.Source, keys, errors);
                continue;
            }

            ResolveLogicalToken(token.Value, token.Source, keys, errors);
        }

        return new KeyResolveResult(
            keys.Distinct().ToArray(),
            errors);
    }

    public IReadOnlyList<string> FormatPreviewDirections()
    {
        var previews = new List<string>();
        foreach (var direction in KeyMapDefaults.PreviewDirections)
        {
            var result = Resolve([InputToken.Logical(direction, direction)]);
            var keys = result.Errors.Count == 0
                ? string.Join(" + ", result.Keys.Select(key => key.DisplayName))
                : "未設定";
            var label = direction == "5" ? "ニュートラル" : direction;
            previews.Add($"{direction} {label} = {keys}");
        }

        return previews;
    }

    private void ResolveLogicalToken(
        string logicalToken,
        string source,
        List<PhysicalKey> keys,
        List<KeyResolveError> errors)
    {
        if (logicalToken == "5")
        {
            return;
        }

        if (DiagonalDirections.TryGetValue(logicalToken, out var baseDirections))
        {
            foreach (var baseDirection in baseDirections)
            {
                ResolveMappedToken(baseDirection, source, keys, errors);
            }

            return;
        }

        ResolveMappedToken(logicalToken, source, keys, errors);
    }

    private void ResolveMappedToken(
        string logicalToken,
        string source,
        List<PhysicalKey> keys,
        List<KeyResolveError> errors)
    {
        if (!_profile.Bindings.TryGetValue(logicalToken, out var names)
            || names.Count == 0)
        {
            errors.Add(new KeyResolveError(
                source,
                $"{logicalToken} is not mapped."));
            return;
        }

        foreach (var name in names)
        {
            AddPhysicalKey(name, source, keys, errors);
        }
    }

    private static void AddPhysicalKey(
        string keyName,
        string source,
        List<PhysicalKey> keys,
        List<KeyResolveError> errors)
    {
        if (PhysicalKeyNameParser.IsNeutral(keyName))
        {
            return;
        }

        if (PhysicalKeyNameParser.TryParse(keyName, out var key))
        {
            keys.Add(key);
            return;
        }

        errors.Add(new KeyResolveError(
            source,
            $"{keyName} is not a physical key."));
    }
}

public sealed record KeyResolveResult(
    IReadOnlyList<PhysicalKey> Keys,
    IReadOnlyList<KeyResolveError> Errors);

public sealed record KeyResolveError(string Source, string Message);
