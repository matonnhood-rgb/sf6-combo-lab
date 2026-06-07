using System.Diagnostics;
using System.IO;
using System.Text;

namespace ComboLab.Services.PresentSync;

public sealed class PresentMonConsolePresentFrameSource
    : IPresentFrameSource
{
    private readonly PresentMonCsvParser _parser = new();
    private Process? _process;
    private CancellationTokenSource? _readCancellation;
    private Task? _stdoutTask;
    private Task? _stderrTask;
    private long _sequence;
    private bool _headerReported;

    public event EventHandler<PresentFrameEvent>? FramePresented;

    public event EventHandler<string>? CaptureError;

    public async Task StartAsync(
        PresentCaptureOptions options,
        CancellationToken cancellationToken)
    {
        if (_process is not null)
        {
            throw new InvalidOperationException(
                "PresentMonはすでに起動しています。");
        }

        if (string.IsNullOrWhiteSpace(options.PresentMonExePath)
            || !File.Exists(options.PresentMonExePath))
        {
            throw new FileNotFoundException(
                "PresentMonの実行ファイルが見つかりません。パスを確認してください。",
                options.PresentMonExePath);
        }

        if (options.ProcessId is null
            && string.IsNullOrWhiteSpace(options.ProcessName))
        {
            throw new InvalidOperationException(
                "対象プロセスIDまたはプロセス名を指定してください。");
        }

        var optionPrefix = await DetectOptionPrefixAsync(
            options.PresentMonExePath,
            cancellationToken);
        var arguments = BuildArguments(options, optionPrefix);
        var startInfo = new ProcessStartInfo
        {
            FileName = options.PresentMonExePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "PresentMonを起動できませんでした。");
        _readCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        _stdoutTask = ReadStdoutAsync(
            _process.StandardOutput,
            _readCancellation.Token);
        _stderrTask = ReadStderrAsync(
            _process.StandardError,
            _readCancellation.Token);

        await Task.Delay(100, cancellationToken);
        if (_process.HasExited)
        {
            var error = await _process.StandardError.ReadToEndAsync(
                cancellationToken);
            throw new InvalidOperationException(
                "PresentMonが起動直後に終了しました。"
                + (string.IsNullOrWhiteSpace(error)
                    ? string.Empty
                    : Environment.NewLine + error.Trim()));
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _readCancellation?.Cancel();
        if (_process is { HasExited: false } process)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
        }

        if (_stdoutTask is not null)
        {
            await IgnoreCancellationAsync(_stdoutTask);
        }

        if (_stderrTask is not null)
        {
            await IgnoreCancellationAsync(_stderrTask);
        }

        _readCancellation?.Dispose();
        _readCancellation = null;
        _process?.Dispose();
        _process = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
    }

    private async Task ReadStdoutAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (_parser.TrySetHeader(line))
            {
                if (!_parser.HasPresentQpc && !_headerReported)
                {
                    _headerReported = true;
                    CaptureError?.Invoke(
                        this,
                        "PresentMonのCSVにQPCTime列がありません。"
                        + "CPUStartQPC/CPUStartQPCTimeはPresent時刻として使用しません。"
                        + $" 利用可能列: {string.Join(", ", _parser.AvailableColumns)}");
                }

                continue;
            }

            if (_parser.TryParse(
                    line,
                    Interlocked.Increment(ref _sequence),
                    out var presentFrame))
            {
                FramePresented?.Invoke(this, presentFrame);
            }
        }
    }

    private async Task ReadStderrAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (line.Contains("access denied", StringComparison.OrdinalIgnoreCase)
                || line.Contains("error", StringComparison.OrdinalIgnoreCase)
                || line.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                CaptureError?.Invoke(this, line.Trim());
            }
        }
    }

    private static async Task<string> DetectOptionPrefixAsync(
        string executablePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "--help",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return "--";
            }

            var output = await process.StandardOutput.ReadToEndAsync(
                cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(
                cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var help = output + Environment.NewLine + error;
            return help.Contains("--process_id", StringComparison.Ordinal)
                ? "--"
                : "-";
        }
        catch
        {
            return "--";
        }
    }

    private static string BuildArguments(
        PresentCaptureOptions options,
        string prefix)
    {
        var arguments = new List<string>();
        if (options.ProcessId is int processId)
        {
            arguments.Add($"{prefix}process_id {processId}");
        }
        else
        {
            arguments.Add(
                $"{prefix}process_name "
                + Quote(options.ProcessName!));
        }

        arguments.Add($"{prefix}output_stdout");
        arguments.Add($"{prefix}v1_metrics");
        arguments.Add($"{prefix}qpc_time");
        arguments.Add($"{prefix}exclude_dropped");
        arguments.Add($"{prefix}no_console_stats");
        arguments.Add(
            $"{prefix}session_name {Quote(options.SessionName)}");
        arguments.Add($"{prefix}stop_existing_session");
        arguments.Add($"{prefix}terminate_on_proc_exit");
        return string.Join(" ", arguments);
    }

    private static string Quote(string value) =>
        "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
