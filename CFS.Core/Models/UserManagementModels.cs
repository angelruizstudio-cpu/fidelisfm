namespace CFS.Core.Models;

public sealed record TenantUser(
    int Id,
    string UserName,
    string FullName,
    string? Email,
    bool IsActive,
    bool MustChangePassword,
    IReadOnlyList<string> Roles);

public sealed class UserCreateEntry
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Tesorero";
}

public sealed record UserSaveResult(bool Saved, int? UserId, string? ResetToken, string? ErrorMessage);

public sealed record PasswordResetTokenInfo(int UserId, string UserName, string? Email, bool IsExpired, bool IsUsed)
{
    public bool IsValid => !IsExpired && !IsUsed;
}
