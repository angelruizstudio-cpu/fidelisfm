using CFS.Core.Models;

namespace CFS.Core.Services;

public interface IDashboardRepository
{
    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

