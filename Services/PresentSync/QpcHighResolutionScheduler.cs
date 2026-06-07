using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ComboLab.Services.PresentSync;

public sealed class QpcHighResolutionScheduler : IDisposable
{
    private const uint HighResolutionFlag = 0x00000002;
    private const uint TimerAllAccess = 0x001F0003;
    private const uint WaitObject0 = 0;
    private readonly nint _timerHandle;

    public QpcHighResolutionScheduler()
    {
        _timerHandle = CreateWaitableTimerEx(
            0,
            null,
            HighResolutionFlag,
            TimerAllAccess);
        if (_timerHandle == 0)
        {
            _timerHandle = CreateWaitableTimer(0, false, null);
        }
    }

    public void WaitUntil(
        long targetQpc,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remainingQpc = targetQpc - Stopwatch.GetTimestamp();
            if (remainingQpc <= 0)
            {
                return;
            }

            var remainingMs =
                remainingQpc * 1000d / Stopwatch.Frequency;
            if (remainingMs <= 0.6)
            {
                Thread.SpinWait(64);
                continue;
            }

            var waitMs = Math.Min(10, remainingMs - 0.5);
            if (_timerHandle != 0)
            {
                var dueTime = -(long)Math.Max(1, waitMs * 10_000);
                if (SetWaitableTimer(
                        _timerHandle,
                        ref dueTime,
                        0,
                        0,
                        0,
                        false)
                    && WaitForSingleObject(
                        _timerHandle,
                        (uint)Math.Ceiling(waitMs + 2)) == WaitObject0)
                {
                    continue;
                }
            }

            cancellationToken.WaitHandle.WaitOne(
                TimeSpan.FromMilliseconds(Math.Max(0.1, waitMs)));
        }
    }

    public void Dispose()
    {
        if (_timerHandle != 0)
        {
            CloseHandle(_timerHandle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateWaitableTimerEx(
        nint timerAttributes,
        string? timerName,
        uint flags,
        uint desiredAccess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateWaitableTimer(
        nint timerAttributes,
        bool manualReset,
        string? timerName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetWaitableTimer(
        nint timer,
        ref long dueTime,
        int period,
        nint completionRoutine,
        nint argument,
        bool resume);

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(
        nint handle,
        uint milliseconds);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);
}
