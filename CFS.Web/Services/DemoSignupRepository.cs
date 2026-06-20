using CFS.Core.Services;

namespace CFS.Web.Services;

public sealed class DemoSignupRepository : ISignupRepository
{
    public Task CreatePendingSignupAsync(PendingSignup signup, string password, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<int?> CompleteSignupAndProvisionTenantAsync(
        string stripeSessionId,
        string? stripeCustomerId,
        string? stripeSubscriptionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<int?>(null);

    public Task<IReadOnlyList<PendingSignupRecord>> ListRecentAsync(int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PendingSignupRecord>>([]);
}
