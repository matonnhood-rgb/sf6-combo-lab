using ComboLab.Models;

namespace ComboLab.Services;

public static class TimelineService
{
    public static int GetNextInputFrame(IReadOnlyList<ComboStep> nodes) =>
        nodes.Count == 0 ? 0 : nodes.Max(node => node.InputFrame ?? 0) + 1;

    public static bool HasReferenceCycle(
        ComboLibrary library,
        Guid sourceComboId,
        Guid targetComboId)
    {
        if (sourceComboId == targetComboId)
        {
            return true;
        }

        return CanReach(library, targetComboId, sourceComboId, []);
    }

    private static bool CanReach(
        ComboLibrary library,
        Guid currentId,
        Guid targetId,
        HashSet<Guid> visited)
    {
        if (!visited.Add(currentId))
        {
            return false;
        }

        var combo = library.Combos.FirstOrDefault(item => item.Id == currentId);
        if (combo is null)
        {
            return false;
        }

        foreach (var calledId in combo.Steps
                     .Where(node => node.NodeType == TimelineNodeType.ComboCall)
                     .Select(node => node.CalledComboId)
                     .OfType<Guid>())
        {
            if (calledId == targetId
                || CanReach(library, calledId, targetId, visited))
            {
                return true;
            }
        }

        return false;
    }
}
