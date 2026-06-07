using System.Diagnostics;

namespace ComboLab.Services.PresentSync;

public sealed class PresentClockEstimator
{
    private readonly object _sync = new();
    private readonly Queue<long> _samples = new();
    private readonly int _capacity;
    private int _requiredSamples;
    private long _latestPresentIndex = -1;

    public PresentClockEstimator(
        int requiredSamples = 10,
        int capacity = 120)
    {
        _requiredSamples = Math.Max(3, requiredSamples);
        _capacity = Math.Clamp(capacity, 60, 120);
    }

    public bool IsLocked { get; private set; }

    public double EstimatedFrameMs { get; private set; }

    public long LatestPresentQpc { get; private set; }

    public long LatestPresentIndex
    {
        get
        {
            lock (_sync)
            {
                return _latestPresentIndex;
            }
        }
    }

    public double LatestDeltaMs { get; private set; }

    public bool LastPresentWasIrregular { get; private set; }

    public void SetRequiredSamples(int requiredSamples)
    {
        lock (_sync)
        {
            _requiredSamples = Math.Max(3, requiredSamples);
            Recalculate();
        }
    }

    public void AddPresent(long presentQpc)
    {
        lock (_sync)
        {
            if (LatestPresentQpc > 0)
            {
                LatestDeltaMs =
                    (presentQpc - LatestPresentQpc)
                    * 1000d / Stopwatch.Frequency;
                LastPresentWasIrregular =
                    EstimatedFrameMs > 0
                    && LatestDeltaMs > EstimatedFrameMs * 1.5;
            }

            LatestPresentQpc = presentQpc;
            _latestPresentIndex++;
            _samples.Enqueue(presentQpc);
            while (_samples.Count > _capacity)
            {
                _samples.Dequeue();
            }

            Recalculate();
        }
    }

    public long PredictPresentQpc(long presentIndex)
    {
        lock (_sync)
        {
            if (!IsLocked || EstimatedFrameMs <= 0)
            {
                throw new InvalidOperationException(
                    "Presentクロックはまだ同期ロックされていません。");
            }

            var frameTicks = MsToQpcTicks(EstimatedFrameMs);
            return LatestPresentQpc
                + (presentIndex - _latestPresentIndex) * frameTicks;
        }
    }

    public long MsToQpcTicks(double milliseconds) =>
        (long)Math.Round(
            milliseconds * Stopwatch.Frequency / 1000d,
            MidpointRounding.AwayFromZero);

    private void Recalculate()
    {
        if (_samples.Count < 2)
        {
            IsLocked = false;
            return;
        }

        var sampleArray = _samples.ToArray();
        var deltas = sampleArray
            .Zip(sampleArray.Skip(1), (before, after) =>
                (after - before) * 1000d / Stopwatch.Frequency)
            .Where(delta => delta > 0)
            .OrderBy(delta => delta)
            .ToArray();
        if (deltas.Length == 0)
        {
            IsLocked = false;
            return;
        }

        var median = Median(deltas);
        var filtered = deltas
            .Where(delta => delta >= median * 0.5
                && delta <= median * 1.5)
            .OrderBy(delta => delta)
            .ToArray();
        EstimatedFrameMs = filtered.Length == 0
            ? median
            : Median(filtered);

        var deviations = filtered
            .Select(delta => Math.Abs(delta - EstimatedFrameMs))
            .OrderBy(value => value)
            .ToArray();
        var medianDeviation = deviations.Length == 0
            ? double.MaxValue
            : Median(deviations);
        IsLocked = _samples.Count >= _requiredSamples
            && EstimatedFrameMs > 0
            && medianDeviation / EstimatedFrameMs <= 0.08;
    }

    private static double Median(IReadOnlyList<double> values)
    {
        var middle = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2
            : values[middle];
    }
}
