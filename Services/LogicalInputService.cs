namespace ComboLab.Services;

public static class LogicalInputService
{
    private static readonly IReadOnlyDictionary<string, string[]> CompositeDirections =
        new Dictionary<string, string[]>
        {
            ["左下"] = ["左", "下"],
            ["右下"] = ["右", "下"],
            ["左上"] = ["左", "上"],
            ["右上"] = ["右", "上"]
        };

    public static IReadOnlyList<string> ExpandDirections(
        IEnumerable<string> logicalInputs) =>
        logicalInputs
            .SelectMany(input => CompositeDirections.TryGetValue(input, out var expanded)
                ? expanded
                : [input])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
