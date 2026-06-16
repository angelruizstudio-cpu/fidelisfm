using CFS.Core.Models;

namespace CFS.Core.Services;

public interface ICheckPrintSettingsRepository
{
    Task<CheckPrintSettings> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        CheckPrintSettingsEntry entry,
        string userName,
        CancellationToken cancellationToken = default);
}
