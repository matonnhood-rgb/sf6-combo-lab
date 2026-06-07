using System.Globalization;

namespace ComboLab.Services.PresentSync;

public sealed class PresentMonCsvParser
{
    private Dictionary<string, int>? _columns;

    public IReadOnlyList<string> AvailableColumns =>
        _columns?.Keys.ToArray() ?? [];

    public bool HasPresentQpc =>
        _columns?.ContainsKey("QPCTime") == true;

    public bool TrySetHeader(string line)
    {
        var values = ParseCsvLine(line);
        if (!values.Any(value =>
                value.Equals("Application", StringComparison.OrdinalIgnoreCase))
            || !values.Any(value =>
                value.Equals("ProcessID", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        _columns = values
            .Select((name, index) => new
            {
                Name = name.Trim(),
                Index = index
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Index,
                StringComparer.OrdinalIgnoreCase);
        return true;
    }

    public bool TryParse(
        string line,
        long sequence,
        out PresentFrameEvent presentFrame)
    {
        presentFrame = default!;
        if (_columns is null || !HasPresentQpc)
        {
            return false;
        }

        var values = ParseCsvLine(line);
        if (!TryGet(values, "QPCTime", out var qpcText)
            || !long.TryParse(
                qpcText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var presentQpc)
            || !TryGet(values, "ProcessID", out var processText)
            || !int.TryParse(
                processText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var processId))
        {
            return false;
        }

        TryGet(values, "Application", out var application);
        TryGet(values, "PresentMode", out var presentMode);
        var msBetween = TryGetDouble(
            values,
            "msBetweenPresents",
            "MsBetweenPresents");
        var dropped = TryGet(values, "Dropped", out var droppedText)
            && (droppedText == "1"
                || bool.TryParse(droppedText, out var parsedDropped)
                && parsedDropped);

        presentFrame = new PresentFrameEvent(
            sequence,
            presentQpc,
            presentQpc * 1000d / System.Diagnostics.Stopwatch.Frequency,
            processId,
            application ?? string.Empty,
            string.IsNullOrWhiteSpace(presentMode) ? null : presentMode,
            msBetween,
            dropped);
        return true;
    }

    private double? TryGetDouble(
        IReadOnlyList<string> values,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGet(values, name, out var text)
                && double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                return value;
            }
        }

        return null;
    }

    private bool TryGet(
        IReadOnlyList<string> values,
        string name,
        out string? value)
    {
        value = null;
        if (_columns is null
            || !_columns.TryGetValue(name, out var index)
            || index < 0
            || index >= values.Count)
        {
            return false;
        }

        value = values[index].Trim();
        return true;
    }

    internal static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var value = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes
                    && index + 1 < line.Length
                    && line[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(character);
            }
        }

        values.Add(value.ToString());
        return values;
    }
}
