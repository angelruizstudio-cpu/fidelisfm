using CFS.Core.Models;

namespace CFS.Core.Services;

public interface IUserManagementRepository
{
    Task<IReadOnlyList<TenantUser>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<int> GetUserCountAsync(CancellationToken cancellationToken = default);
    Task<UserSaveResult> CreateUserAsync(UserCreateEntry entry, CancellationToken cancellationToken = default);
    Task<bool> SetActiveAsync(int userId, bool active, CancellationToken cancellationToken = default);
    Task<bool> DeleteUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<string?> GenerateResetTokenAsync(int userId, CancellationToken cancellationToken = default);
    Task<PasswordResetTokenInfo?> ValidateResetTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<bool> ConsumeResetTokenAsync(string token, string newPassword, CancellationToken cancellationToken = default);
    Task<bool> ChangePasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default);
}
