using CFS.Core.Models;

namespace CFS.Core.Services;

public interface IAutomationRepository
{
    Task<AutomationLookups> GetLookupsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AutomationRule>> GetRulesAsync(CancellationToken cancellationToken = default);

    Task<AutomationSaveResult> SaveAsync(AutomationRuleEntry entry, string userName, CancellationToken cancellationToken = default);

    Task<bool> SetActiveAsync(int id, bool active, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<AutomationRunResult> RunDueRulesAsync(string userName, CancellationToken cancellationToken = default);
}
