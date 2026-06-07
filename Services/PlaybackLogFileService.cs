using System.IO;
using System.Text;

namespace ComboLab.Services;

public sealed class PlaybackLogFileService
{
    public string LogsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "logs");

    public async Task<string> WriteAsync(
        string actionName,
        IReadOnlyList<string> lines,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(LogsDirectory);
        var safeActionName = MakeSafeFileName(actionName);
        var filePath = Path.Combine(
            LogsDirectory,
            $"{DateTime.Now:yyyyMMdd-HHmmssfff}-{safeActionName}.log");
        await File.WriteAllLinesAsync(
            filePath,
            lines,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
        return filePath;
    }

    private static string MakeSafeFileName(string value)
    {
        var source = string.IsNullOrWhiteSpace(value)
            ? "名称未設定"
            : value.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(source
            .Select(character =>
                invalidChars.Contains(character) ? '_' : character)
            .ToArray());
        return cleaned.Length <= 48 ? cleaned : cleaned[..48];
    }
}
