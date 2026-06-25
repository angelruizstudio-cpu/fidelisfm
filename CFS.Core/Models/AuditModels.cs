namespace CFS.Core.Models;

public sealed record AuditLogEntry(
    int Id,
    DateTime CreatedAt,
    string UserName,
    string Action,
    string EntityType,
    string? EntityReference,
    string Detail);
