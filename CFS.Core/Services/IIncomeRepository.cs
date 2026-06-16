using CFS.Core.Models;

namespace CFS.Core.Services;

public interface IIncomeRepository
{
    Task<IncomeLookups> GetLookupsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncomeTransaction>> GetRecentAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    Task<IncomeTransaction?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IncomeSaveResult> SaveAsync(
        IncomeEntry entry,
        string userName,
        bool ignorePossibleDuplicate = false,
        CancellationToken cancellationToken = default);

    Task<MemberSaveResult> CreateMemberAsync(
        MemberQuickEntry entry,
        CancellationToken cancellationToken = default);

    Task<IncomeSaveResult> VoidAsync(
        int id,
        string reason,
        string userName,
        CancellationToken cancellationToken = default);
}
