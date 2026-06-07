using System.Diagnostics;
using ComboLab.Models;
using ComboLab.Services.PresentSync;

namespace ComboLab.Services;

public sealed class ActionTestPlaybackService
{
    public const double FramesPerSecond = 60;
    public const double MillisecondsPerFrame = 1000 / FramesPerSecond;

    private readonly IKeyboardInputSender _keyboardInputSender;
    private KeyMapResolver _keyMapResolver;
    private readonly object _activeKeysLock = new();
    private readonly HashSet<PhysicalKey> _activeKeys = [];

    public ActionTestPlaybackService(
        IKeyboardInputSender keyboardInputSender,
        KeyMapResolver? keyMapResolver = null)
    {
        _keyboardInputSender = keyboardInputSender;
        _keyMapResolver = keyMapResolver
            ?? new KeyMapResolver(KeyMapDefaults.CreateLibrary().Profiles[0]);
    }

    public void SetKeyMapResolver(KeyMapResolver keyMapResolver)
    {
        _keyMapResolver = keyMapResolver;
    }

    public Task PlayAsync(
        ActionDefinition actionDefinition,
        KeyPressDuration holdDuration,
        CancellationToken cancellationToken = default,
        Action<string>? playbackLog = null)
    {
        var plan = BuildPlaybackPlan(
            actionDefinition,
            holdDuration,
            _keyMapResolver);
        if (plan.Count == 0)
        {
            throw new InvalidOperationException(
                "再生できる物理キー名が入力イベントにありません。");
        }

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var playbackThread = new Thread(() =>
        {
            try
            {
                RunPlaybackPlan(plan, cancellationToken, playbackLog);
                completion.SetResult();
            }
            catch (OperationCanceledException exception)
            {
                completion.SetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "ComboLab Action Playback",
            Priority = ThreadPriority.Highest
        };
        playbackThread.Start();
        return completion.Task;
    }

    public Task PlayPresentSynchronizedAsync(
        ActionDefinition actionDefinition,
        KeyPressDuration holdDuration,
        PresentClockEstimator estimator,
        PresentSyncPlaybackOptions options,
        CancellationToken cancellationToken = default,
        Action<string>? playbackLog = null)
    {
        var plan = BuildPlaybackPlan(
            actionDefinition,
            holdDuration,
            _keyMapResolver);
        if (plan.Count == 0)
        {
            throw new InvalidOperationException(
                "再生できる物理キー名が入力イベントにありません。");
        }

        if (!estimator.IsLocked)
        {
            throw new InvalidOperationException(
                "Presentクロックが同期ロックされていません。");
        }

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var playbackThread = new Thread(() =>
        {
            try
            {
                RunPresentSynchronizedPlan(
                    plan,
                    estimator,
                    options,
                    cancellationToken,
                    playbackLog);
                completion.SetResult();
            }
            catch (OperationCanceledException exception)
            {
                completion.SetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "ComboLab Present Sync Playback",
            Priority = ThreadPriority.Highest
        };
        playbackThread.Start();
        return completion.Task;
    }

    public static IReadOnlyList<PlaybackPlanItem> BuildPlaybackPlan(
        ActionDefinition actionDefinition,
        KeyPressDuration holdDuration,
        KeyMapResolver? keyMapResolver = null)
    {
        var planner = new InputStatePlanner(
            keyMapResolver
            ?? new KeyMapResolver(KeyMapDefaults.CreateLibrary().Profiles[0]));
        return planner.Build(actionDefinition, holdDuration)
            .Select(boundary => new PlaybackPlanItem(
                FrameToTime(boundary.LogicalFrame),
                boundary.LogicalFrame,
                boundary.KeysToRelease,
                boundary.KeysToPress,
                boundary.KeysToKeep,
                boundary.StateAfter))
            .ToArray();
    }

    public static IReadOnlyList<InputResolutionIssue> ValidateInputEvents(
        ActionDefinition actionDefinition,
        KeyMapResolver? keyMapResolver = null)
    {
        var planner = new InputStatePlanner(
            keyMapResolver
            ?? new KeyMapResolver(KeyMapDefaults.CreateLibrary().Profiles[0]));
        return planner.Validate(actionDefinition);
    }

    public static IReadOnlyList<string> FormatResolvedInputEvents(
        ActionDefinition actionDefinition,
        KeyMapResolver? keyMapResolver = null)
    {
        var planner = new InputStatePlanner(
            keyMapResolver
            ?? new KeyMapResolver(KeyMapDefaults.CreateLibrary().Profiles[0]));
        return planner.FormatResolvedInputEvents(actionDefinition);
    }

    public static IReadOnlyList<string> FormatInputEvents(
        ActionDefinition actionDefinition) =>
        (actionDefinition.InputEvents ?? [])
            .OrderBy(item => item.Frame)
            .Select(item =>
                $"{item.DisplayFrame}F display / {item.Frame}F internal: "
                + FormatInputNames(item.LogicalInputs))
            .ToArray();

    public static IReadOnlyList<string> FormatPlaybackPlan(
        IReadOnlyList<PlaybackPlanItem> plan) =>
        plan.Select(item =>
            $"{FormatPlanTime(item.Frame)}: "
            + FormatPlanOperations(item))
        .ToArray();

    public void ReleaseAllKeys(Action<string>? playbackLog = null)
    {
        PhysicalKey[] keys;
        lock (_activeKeysLock)
        {
            keys = _activeKeys.ToArray();
        }

        if (keys.Length == 0)
        {
            return;
        }

        var result = _keyboardInputSender.SendBatch(
            keys.Select(key => new KeyboardInputChange(key, false)).ToArray());
        lock (_activeKeysLock)
        {
            foreach (var key in keys)
            {
                _activeKeys.Remove(key);
            }
        }

        playbackLog?.Invoke(
            $"[End] ReleaseAll: {FormatKeys(keys)} "
            + $"SendInput={result.SentCount}/{result.RequestedCount}");
    }

    public static IReadOnlyList<string> FindUnsupportedKeyNames(
        ActionDefinition actionDefinition,
        KeyMapResolver? keyMapResolver = null) =>
        ValidateInputEvents(actionDefinition, keyMapResolver)
            .Select(issue => issue.ToString())
            .ToArray();

    private KeyboardSendResult SendChanges(
        IReadOnlyList<KeyboardInputChange> changes)
    {
        if (changes.Count == 0)
        {
            return new KeyboardSendResult(0, 0);
        }

        lock (_activeKeysLock)
        {
            foreach (var change in changes.Where(change => change.IsKeyDown))
            {
                _activeKeys.Add(change.Key);
            }
        }

        var result = _keyboardInputSender.SendBatch(changes);
        lock (_activeKeysLock)
        {
            foreach (var change in changes)
            {
                if (change.IsKeyDown)
                {
                    _activeKeys.Add(change.Key);
                }
                else
                {
                    _activeKeys.Remove(change.Key);
                }
            }
        }

        return result;
    }

    private void RunPlaybackPlan(
        IReadOnlyList<PlaybackPlanItem> plan,
        CancellationToken cancellationToken,
        Action<string>? playbackLog)
    {
        HighResolutionTimer.Begin();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            foreach (var item in plan)
            {
                WaitUntil(stopwatch, item.At, cancellationToken);
                var actualElapsed = stopwatch.Elapsed;
                var changes = item.KeysToRelease
                    .Select(key => new KeyboardInputChange(key, false))
                    .Concat(item.KeysToPress.Select(
                        key => new KeyboardInputChange(key, true)))
                    .ToArray();
                var result = SendChanges(changes);
                if (changes.Length > 0)
                {
                    var timingError =
                        (actualElapsed - item.At).TotalMilliseconds;
                    playbackLog?.Invoke(
                        $"{FormatPlanTime(item.Frame)} Boundary: "
                        + FormatSendOperations(
                            item.KeysToRelease,
                            item.KeysToPress)
                        + $" / stateAfter={FormatKeys(item.StateAfter)}"
                        + $" / result={result.SentCount}/{result.RequestedCount}"
                        + $" / timestamp={result.Timestamp}"
                        + $" / targetMs={item.At.TotalMilliseconds:0.###}"
                        + $" / actualMs={actualElapsed.TotalMilliseconds:0.###}"
                        + $" / lateMs={timingError:0.###}");
                }
            }
        }
        finally
        {
            try
            {
                ReleaseAllKeys(playbackLog);
            }
            finally
            {
                HighResolutionTimer.End();
            }
        }
    }

    private void RunPresentSynchronizedPlan(
        IReadOnlyList<PlaybackPlanItem> plan,
        PresentClockEstimator estimator,
        PresentSyncPlaybackOptions options,
        CancellationToken cancellationToken,
        Action<string>? playbackLog)
    {
        HighResolutionTimer.Begin();
        using var scheduler = new QpcHighResolutionScheduler();
        var basePresentIndex = options.MacroBasePresentIndex;
        var baseLogicalFrame = 0d;
        try
        {
            foreach (var item in plan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (options.ConsumeResyncRequest?.Invoke() == true)
                {
                    WaitForPresentLock(estimator, cancellationToken);
                    basePresentIndex = estimator.LatestPresentIndex
                        + options.StartDelayFrames;
                    baseLogicalFrame = item.Frame;
                    playbackLog?.Invoke(
                        $"[PresentSync] 再同期: logicalFrame="
                        + $"{FormatPlanTime(item.Frame)} "
                        + $"basePresent=#{basePresentIndex}");
                }

                var baseQpc = estimator.PredictPresentQpc(basePresentIndex);
                var relativeFrame = item.Frame - baseLogicalFrame;
                var targetQpc = baseQpc
                    + estimator.MsToQpcTicks(
                        relativeFrame * estimator.EstimatedFrameMs
                        + options.InputPhaseOffsetMs);
                scheduler.WaitUntil(targetQpc, cancellationToken);
                var actualQpc = Stopwatch.GetTimestamp();
                var changes = item.KeysToRelease
                    .Select(key => new KeyboardInputChange(key, false))
                    .Concat(item.KeysToPress.Select(
                        key => new KeyboardInputChange(key, true)))
                    .ToArray();
                var result = SendChanges(changes);
                if (changes.Length > 0)
                {
                    var lateMs = (actualQpc - targetQpc)
                        * 1000d / Stopwatch.Frequency;
                    var targetPresent = basePresentIndex + relativeFrame;
                    playbackLog?.Invoke(
                        $"{FormatPlanTime(item.Frame)} Boundary: "
                        + FormatSendOperations(
                            item.KeysToRelease,
                            item.KeysToPress)
                        + $" / stateAfter={FormatKeys(item.StateAfter)}"
                        + $" / targetPresent=#{targetPresent:0.###}"
                        + $" / qpcTarget={targetQpc}"
                        + $" / qpcActual={actualQpc}"
                        + $" / targetMs="
                        + $"{targetQpc * 1000d / Stopwatch.Frequency:0.###}"
                        + $" / actualMs="
                        + $"{actualQpc * 1000d / Stopwatch.Frequency:0.###}"
                        + $" / lateMs={lateMs:0.###}"
                        + $" / result={result.SentCount}/{result.RequestedCount}");
                }
            }
        }
        finally
        {
            try
            {
                ReleaseAllKeys(playbackLog);
            }
            finally
            {
                HighResolutionTimer.End();
            }
        }
    }

    private static void WaitForPresentLock(
        PresentClockEstimator estimator,
        CancellationToken cancellationToken)
    {
        while (!estimator.IsLocked)
        {
            cancellationToken.WaitHandle.WaitOne(
                TimeSpan.FromMilliseconds(10));
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static TimeSpan FrameToTime(double frame) =>
        TimeSpan.FromSeconds(frame / FramesPerSecond);

    private static string FormatInputNames(IEnumerable<string>? inputs)
    {
        var names = (inputs ?? [])
            .Where(input => !string.IsNullOrWhiteSpace(input))
            .ToArray();
        return names.Length == 0 ? "なし" : string.Join(", ", names);
    }

    private static string FormatKeys(IEnumerable<PhysicalKey> keys)
    {
        var names = keys.Select(key => key.DisplayName).ToArray();
        return names.Length == 0 ? "なし" : string.Join(", ", names);
    }

    private static string FormatPlanOperations(PlaybackPlanItem item)
    {
        var operations = new List<string>();
        if (item.KeysToKeep.Count > 0)
        {
            operations.Add($"Keep {FormatKeys(item.KeysToKeep)}");
        }

        if (item.KeysToRelease.Count > 0)
        {
            operations.Add($"Up {FormatKeys(item.KeysToRelease)}");
        }

        if (item.KeysToPress.Count > 0)
        {
            operations.Add($"Down {FormatKeys(item.KeysToPress)}");
        }

        return operations.Count == 0
            ? "送信なし"
            : string.Join(" / ", operations);
    }

    private static string FormatSendOperations(
        IReadOnlyList<PhysicalKey> keysToRelease,
        IReadOnlyList<PhysicalKey> keysToPress)
    {
        var operations = new List<string>();
        if (keysToRelease.Count > 0)
        {
            operations.Add($"Up {FormatKeys(keysToRelease)}");
        }

        if (keysToPress.Count > 0)
        {
            operations.Add($"Down {FormatKeys(keysToPress)}");
        }

        return operations.Count == 0
            ? "送信なし"
            : string.Join(" / ", operations);
    }

    private static string FormatPlanTime(double frame) =>
        Math.Abs(frame - Math.Round(frame)) < 0.0001
            ? $"{Math.Round(frame):0}F"
            : $"{frame:0.###}F";

    private static void WaitUntil(
        Stopwatch stopwatch,
        TimeSpan target,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var remaining = target - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            if (remaining > TimeSpan.FromMilliseconds(2))
            {
                cancellationToken.WaitHandle.WaitOne(
                    remaining - TimeSpan.FromMilliseconds(1));
                cancellationToken.ThrowIfCancellationRequested();
            }
            else
            {
                Thread.SpinWait(64);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

}

internal static class HighResolutionTimer
{
    private const uint OneMillisecond = 1;

    public static void Begin() => timeBeginPeriod(OneMillisecond);

    public static void End() => timeEndPeriod(OneMillisecond);

    [System.Runtime.InteropServices.DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint period);

    [System.Runtime.InteropServices.DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint period);
}

public sealed record PlaybackPlanItem(
    TimeSpan At,
    double Frame,
    IReadOnlyList<PhysicalKey> KeysToRelease,
    IReadOnlyList<PhysicalKey> KeysToPress,
    IReadOnlyList<PhysicalKey> KeysToKeep,
    IReadOnlyList<PhysicalKey> StateAfter);
