using ComboLab.Models;

namespace ComboLab.Services;

public sealed class InputStatePlanner
{
    private readonly InputNotationParser _parser = new();
    private readonly KeyMapResolver _resolver;

    public InputStatePlanner()
        : this(new KeyMapResolver(KeyMapDefaults.CreateLibrary().Profiles[0]))
    {
    }

    public InputStatePlanner(KeyMapResolver resolver)
    {
        _resolver = resolver;
    }

    public IReadOnlyList<InputBoundaryEvent> Build(
        ActionDefinition actionDefinition,
        KeyPressDuration holdDuration)
    {
        var operations = new List<StateOperation>();
        foreach (var inputEvent in (actionDefinition.InputEvents ?? [])
                     .OrderBy(item => item.Frame))
        {
            var keys = ParseKeys(inputEvent);
            var releaseFrame = inputEvent.Frame
                + (inputEvent.DurationFrames
                    ?? holdDuration.Value.TotalSeconds
                    * ActionTestPlaybackService.FramesPerSecond);
            foreach (var key in keys)
            {
                operations.Add(new StateOperation(inputEvent.Frame, key, true));
                operations.Add(new StateOperation(releaseFrame, key, false));
            }
        }

        var activeCounts = new Dictionary<PhysicalKey, int>();
        var boundaries = new List<InputBoundaryEvent>();
        foreach (var group in operations
                     .GroupBy(item => NormalizeFrame(item.Frame))
                     .OrderBy(item => item.Key))
        {
            var releases = CountByKey(group.Where(item => !item.IsKeyDown));
            var presses = CountByKey(group.Where(item => item.IsKeyDown));
            var affectedKeys = releases.Keys
                .Concat(presses.Keys)
                .Distinct()
                .ToArray();
            var keysToRelease = new List<PhysicalKey>();
            var keysToPress = new List<PhysicalKey>();
            var keysToKeep = new List<PhysicalKey>();

            foreach (var key in affectedKeys)
            {
                activeCounts.TryGetValue(key, out var before);
                releases.TryGetValue(key, out var releaseCount);
                presses.TryGetValue(key, out var pressCount);
                var after = Math.Max(
                    0,
                    before - releaseCount + pressCount);

                if (before == 0 && after > 0)
                {
                    keysToPress.Add(key);
                }
                else if (before > 0 && after == 0)
                {
                    keysToRelease.Add(key);
                }
                else if (before > 0 && after > 0)
                {
                    keysToKeep.Add(key);
                }

                if (after == 0)
                {
                    activeCounts.Remove(key);
                }
                else
                {
                    activeCounts[key] = after;
                }
            }

            boundaries.Add(new InputBoundaryEvent(
                group.Key,
                keysToRelease,
                keysToPress,
                keysToKeep,
                activeCounts.Keys.ToArray()));
        }

        return boundaries;
    }

    private static Dictionary<PhysicalKey, int> CountByKey(
        IEnumerable<StateOperation> operations) =>
        operations
            .GroupBy(item => item.Key)
            .ToDictionary(item => item.Key, item => item.Count());

    public IReadOnlyList<InputResolutionIssue> Validate(
        ActionDefinition actionDefinition)
    {
        var issues = new List<InputResolutionIssue>();
        foreach (var inputEvent in actionDefinition.InputEvents ?? [])
        {
            var parseResult = _parser.ParseMany(inputEvent.LogicalInputs);
            issues.AddRange(parseResult.Errors.Select(error =>
                new InputResolutionIssue(
                    inputEvent.Frame,
                    error.Source,
                    error.Message)));

            var resolveResult = _resolver.Resolve(parseResult.Tokens);
            issues.AddRange(resolveResult.Errors.Select(error =>
                new InputResolutionIssue(
                    inputEvent.Frame,
                    error.Source,
                    error.Message)));
        }

        return issues;
    }

    public IReadOnlyList<string> FormatResolvedInputEvents(
        ActionDefinition actionDefinition) =>
        (actionDefinition.InputEvents ?? [])
            .OrderBy(item => item.Frame)
            .Select(item =>
            {
                var parseResult = _parser.ParseMany(item.LogicalInputs);
                var resolveResult = _resolver.Resolve(parseResult.Tokens);
                var tokens = parseResult.Tokens.Count == 0
                    ? "none"
                    : string.Join(" + ", parseResult.Tokens.Select(token =>
                        token.Kind == InputTokenKind.Physical
                            ? $"key:{token.Value}"
                            : token.Value));
                var keys = resolveResult.Keys.Count == 0
                    ? "none"
                    : string.Join(", ", resolveResult.Keys.Select(key => key.DisplayName));
                return $"{item.DisplayFrame}F display / {item.Frame}F internal: "
                    + $"{item.LogicalInputsText} -> {tokens} -> {keys}";
            })
            .ToArray();

    private IReadOnlyList<PhysicalKey> ParseKeys(InputEvent inputEvent)
    {
        var parseResult = _parser.ParseMany(inputEvent.LogicalInputs);
        var resolveResult = _resolver.Resolve(parseResult.Tokens);
        var issues = parseResult.Errors
            .Select(error => new InputResolutionIssue(
                inputEvent.Frame,
                error.Source,
                error.Message))
            .Concat(resolveResult.Errors.Select(error =>
                new InputResolutionIssue(
                    inputEvent.Frame,
                    error.Source,
                    error.Message)))
            .ToArray();
        if (issues.Length > 0)
        {
            throw new InputResolutionException(issues);
        }

        return resolveResult.Keys;
    }

    private static double NormalizeFrame(double frame) =>
        Math.Round(frame, 5, MidpointRounding.AwayFromZero);

    private readonly record struct StateOperation(
        double Frame,
        PhysicalKey Key,
        bool IsKeyDown);
}

public sealed record InputBoundaryEvent(
    double LogicalFrame,
    IReadOnlyList<PhysicalKey> KeysToRelease,
    IReadOnlyList<PhysicalKey> KeysToPress,
    IReadOnlyList<PhysicalKey> KeysToKeep,
    IReadOnlyList<PhysicalKey> StateAfter);

public sealed record InputResolutionIssue(
    int Frame,
    string Source,
    string Message)
{
    public override string ToString() => $"{Frame}F: {Source}: {Message}";
}

public sealed class InputResolutionException : Exception
{
    public InputResolutionException(IReadOnlyList<InputResolutionIssue> issues)
        : base("入力表記またはキーマップを解決できません。")
    {
        Issues = issues;
    }

    public IReadOnlyList<InputResolutionIssue> Issues { get; }
}
