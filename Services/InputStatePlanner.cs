using ComboLab.Models;

namespace ComboLab.Services;

public sealed class InputStatePlanner
{
    public IReadOnlyList<InputBoundaryEvent> Build(
        ActionDefinition actionDefinition,
        KeyPressDuration holdDuration)
    {
        var operations = new List<StateOperation>();
        foreach (var inputEvent in (actionDefinition.InputEvents ?? [])
                     .OrderBy(item => item.Frame))
        {
            var keys = ParseKeys(inputEvent.LogicalInputs);
            var releaseFrame = inputEvent.Frame
                + holdDuration.Value.TotalSeconds
                * ActionTestPlaybackService.FramesPerSecond;
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

    private static IReadOnlyList<PhysicalKey> ParseKeys(
        IEnumerable<string>? logicalInputs) =>
        (logicalInputs ?? [])
            .Select(input =>
                PhysicalKeyNameParser.TryParse(input, out var key)
                    ? key
                    : (PhysicalKey?)null)
            .OfType<PhysicalKey>()
            .Distinct()
            .ToArray();

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
