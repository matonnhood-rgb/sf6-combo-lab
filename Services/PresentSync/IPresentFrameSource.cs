namespace ComboLab.Services.PresentSync;

public interface IPresentFrameSource : IAsyncDisposable
{
    event EventHandler<PresentFrameEvent>? FramePresented;

    event EventHandler<string>? CaptureError;

    Task StartAsync(
        PresentCaptureOptions options,
        CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}

public sealed record PresentCaptureOptions(
    int? ProcessId,
    string? ProcessName,
    string PresentMonExePath,
    string SessionName);

public sealed record PresentFrameEvent(
    long Sequence,
    long PresentQpc,
    double PresentMs,
    int ProcessId,
    string Application,
    string? PresentMode,
    double? MsBetweenPresents,
    bool Dropped);
