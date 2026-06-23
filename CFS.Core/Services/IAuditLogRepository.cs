using CFS.Core.Models;

namespace CFS.Core.Services;

public interface IAuditLogRepository
{
    Task LogAsync(
        string action,
        string entityType,
        string? entityReference,
        string detail,
        string userName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int take = 200, CancellationToken cancellationToken = default);
}
