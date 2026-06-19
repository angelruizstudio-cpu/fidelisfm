using CFS.Core.Services;

namespace CFS.Web.Services;

public sealed class DemoSignupRepository : ISignupRepository
{
    public Task RecordPendingSignupAsync(PendingSignup signup, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
