using ComboLab.Models;

namespace ComboLab.Services;

public interface IComboStorageService
{
    Task SaveAsync(string filePath, ComboLibrary library, CancellationToken cancellationToken = default);
    Task<ComboLibrary> LoadAsync(string filePath, CancellationToken cancellationToken = default);
}
