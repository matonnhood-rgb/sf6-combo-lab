using ComboLab.Models;

namespace ComboLab.Services;

public sealed class InputNodeLaneLayoutService
{
    public IReadOnlyDictionary<InputEvent, int> AssignLanes(
        IEnumerable<InputEvent> inputEvents)
    {
        var result = new Dictionary<InputEvent, int>();
        var laneEndFrames = new List<int>();
        foreach (var inputEvent in inputEvents
                     .OrderBy(item => item.Frame)
                     .ThenBy(item => item.DurationFrames ?? 1))
        {
            var start = inputEvent.Frame;
            var end = start + Math.Max(1, inputEvent.DurationFrames ?? 1);
            if (inputEvent.VisualLane is { } preferredLane)
            {
                EnsureLane(laneEndFrames, preferredLane);
                if (laneEndFrames[preferredLane] <= start)
                {
                    laneEndFrames[preferredLane] = end;
                    result[inputEvent] = preferredLane;
                    continue;
                }
            }

            var lane = 0;
            while (lane < laneEndFrames.Count && laneEndFrames[lane] > start)
            {
                lane++;
            }

            EnsureLane(laneEndFrames, lane);
            laneEndFrames[lane] = end;
            result[inputEvent] = lane;
        }

        return result;
    }

    private static void EnsureLane(List<int> laneEndFrames, int lane)
    {
        while (laneEndFrames.Count <= lane)
        {
            laneEndFrames.Add(0);
        }
    }
}
