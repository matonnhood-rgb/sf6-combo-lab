using System.IO;

namespace ComboLab.Services;

public sealed class RecentComboFileService
{
    private const string SettingsFileName = "last-opened.txt";

    private static string SettingsFilePath =>
        Path.Combine(
            AppDataPathService.EnsureDataDirectory(),
            SettingsFileName);

    public async Task RememberAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!AppDataPathService.IsInDataDirectory(filePath))
        {
            throw new InvalidOperationException(
                "最終使用ファイルはDataフォルダー内にある必要があります。");
        }

        await File.WriteAllTextAsync(
            SettingsFilePath,
            Path.GetFileName(filePath),
            cancellationToken);
    }

    public async Task<string?> FindStartupFileAsync(
        CancellationToken cancellationToken = default)
    {
        var dataDirectory = AppDataPathService.EnsureDataDirectory();
        if (File.Exists(SettingsFilePath))
        {
            var savedFileName = (await File.ReadAllTextAsync(
                SettingsFilePath,
                cancellationToken)).Trim();
            if (!string.IsNullOrWhiteSpace(savedFileName)
                && string.Equals(
                    savedFileName,
                    Path.GetFileName(savedFileName),
                    StringComparison.Ordinal))
            {
                var savedPath = Path.Combine(dataDirectory, savedFileName);
                if (File.Exists(savedPath))
                {
                    return savedPath;
                }
            }
        }

        return Directory
            .EnumerateFiles(dataDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }
}
