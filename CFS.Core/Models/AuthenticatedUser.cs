namespace CFS.Core.Models;

public sealed record AuthenticatedUser(
    int Id,
    string UserName,
    string FullName,
    IReadOnlyList<string> Roles,
    int TenantId = 1,
    string TenantName = "Iglesia Cristiana Pentecostes Inc",
    string PlanKey = CfsPlans.Founder);
