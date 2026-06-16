using CFS.Core.Models;

namespace CFS.Core.Services;

public interface IUserAuthenticationRepository
{
    Task<AuthenticatedUser?> ValidateCredentialsAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default);
}
