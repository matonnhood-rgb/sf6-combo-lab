namespace ComboLab.Services.PresentSync;

public enum PresentDropBehavior
{
    Continue,
    Stop,
    Resynchronize
}

public sealed record PresentSyncPlaybackOptions(
    long MacroBasePresentIndex,
    int StartDelayFrames,
    double InputPhaseOffsetMs,
    Func<bool>? ConsumeResyncRequest = null);
