using System.IO;

namespace ComboLab.Services;

public static class AppDataPathService
{
    public static string DataDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Data");

    public static string EnsureDataDirectory()
    {
        Directory.CreateDirectory(DataDirectory);
        return DataDirectory;
    }

    public static bool IsInDataDirectory(string filePath)
    {
        var dataDirectory = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(EnsureDataDirectory()));
        var targetDirectory = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(Path.GetDirectoryName(filePath) ?? string.Empty));

        return string.Equals(
            dataDirectory,
            targetDirectory,
            StringComparison.OrdinalIgnoreCase);
    }
}
