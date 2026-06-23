using System.Data;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlAuditLogRepository(SqlConnectionFactory connectionFactory, ITenantContext tenantContext) : IAuditLogRepository
{
    private readonly int _tenantId = tenantContext.TenantId;

    public async Task LogAsync(
        string action,
        string entityType,
        string? entityReference,
        string detail,
        string userName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO dbo.CFS_AuditLog (ID_Tenant_FK, UserName, Action, EntityType, EntityReference, Detail)
            VALUES (@tenantId, @userName, @action, @entityType, @entityReference, @detail);
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@tenantId", SqlDbType.Int).Value = _tenantId;
        command.Parameters.Add("@userName", SqlDbType.NVarChar, 150).Value = userName;
        command.Parameters.Add("@action", SqlDbType.NVarChar, 50).Value = action;
        command.Parameters.Add("@entityType", SqlDbType.NVarChar, 50).Value = entityType;
        command.Parameters.Add("@entityReference", SqlDbType.NVarChar, 100).Value = (object?)entityReference ?? DBNull.Value;
        command.Parameters.Add("@detail", SqlDbType.NVarChar, 500).Value = detail;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int take = 200, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP (@take) ID_AuditLog, CreatedAt, UserName, Action, EntityType, EntityReference, Detail
              FROM dbo.CFS_AuditLog
             WHERE ID_Tenant_FK = @tenantId
             ORDER BY CreatedAt DESC;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@take", SqlDbType.Int).Value = take;
        command.Parameters.Add("@tenantId", SqlDbType.Int).Value = _tenantId;

        var result = new List<AuditLogEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new AuditLogEntry(
                reader.GetInt32(0),
                reader.GetDateTime(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(6)));
        }

        return result;
    }
}
