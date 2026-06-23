using CFS.Core.Models;

namespace CFS.Core.Services;

public interface IDashboardRepository
{
    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MonthlyTrendPoint>> GetMonthlyTrendsAsync(int months = 6, CancellationToken cancellationToken = default);
}

