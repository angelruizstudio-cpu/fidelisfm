using System.Data;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

/// <summary>
/// Best-effort audit writer shared by the income/expense/deposit/check repositories.
/// Uses its own short-lived connection (not the caller's transaction) and swallows
/// failures so that a not-yet-migrated CFS_AuditLog table never breaks a real save.
/// </summary>
internal static class AuditLogger
{
    public static async Task TryLogAsync(
        SqlConnectionFactory connectionFactory,
        int tenantId,
        string userName,
        string action,
        string entityType,
        string? entityReference,
        string detail,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = connectionFactory.Create();
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                INSERT INTO dbo.CFS_AuditLog (ID_Tenant_FK, UserName, Action, EntityType, EntityReference, Detail)
                VALUES (@tenantId, @userName, @action, @entityType, @entityReference, @detail);
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@tenantId", SqlDbType.Int).Value = tenantId;
            command.Parameters.Add("@userName", SqlDbType.NVarChar, 150).Value = userName;
            command.Parameters.Add("@action", SqlDbType.NVarChar, 50).Value = action;
            command.Parameters.Add("@entityType", SqlDbType.NVarChar, 50).Value = entityType;
            command.Parameters.Add("@entityReference", SqlDbType.NVarChar, 100).Value = (object?)entityReference ?? DBNull.Value;
            command.Parameters.Add("@detail", SqlDbType.NVarChar, 500).Value = detail;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException)
        {
            // CFS_AuditLog migration not applied yet (or transient SQL error) - never let
            // audit logging take down a real income/expense/deposit/check save.
        }
    }
}
