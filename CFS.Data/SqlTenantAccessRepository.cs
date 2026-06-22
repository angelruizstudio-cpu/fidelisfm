using System.Data;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlTenantAccessRepository(SqlConnectionFactory connectionFactory) : ITenantAccessRepository
{
    public async Task<IReadOnlyList<TenantAccessOption>> GetAccessibleTenantsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var options = new List<TenantAccessOption>();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT T.ID_Tenant AS TenantId,
                       T.NombreTenant AS TenantName,
                       (SELECT TOP 1 TS.PlanKey FROM dbo.TenantSubscriptions TS
                        WHERE TS.ID_Tenant_FK = T.ID_Tenant ORDER BY TS.StartedAt DESC) AS PlanKey
                FROM dbo.Usuarios U
                INNER JOIN dbo.Tenants T ON T.ID_Tenant = U.ID_Tenant_FK
                WHERE U.ID_Usuario = @userId;
                """;
            command.Parameters.Add("@userId", SqlDbType.Int).Value = userId;

            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var tenantId = reader.GetInt32(reader.GetOrdinal("TenantId"));
                var tenantName = reader.GetString(reader.GetOrdinal("TenantName"));
                var planKey = reader["PlanKey"] is DBNull ? CfsPlans.Basic : reader.GetString(reader.GetOrdinal("PlanKey"));
                options.Add(new TenantAccessOption(tenantId, tenantName, planKey, await LoadHomeRolesAsync(connection, userId, cancellationToken), true));
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT T.ID_Tenant AS TenantId,
                       T.NombreTenant AS TenantName,
                       UTA.RoleKeys,
                       (SELECT TOP 1 TS.PlanKey FROM dbo.TenantSubscriptions TS
                        WHERE TS.ID_Tenant_FK = T.ID_Tenant ORDER BY TS.StartedAt DESC) AS PlanKey
                FROM dbo.UserTenantAccess UTA
                INNER JOIN dbo.Tenants T ON T.ID_Tenant = UTA.ID_Tenant_FK
                WHERE UTA.ID_User_FK = @userId
                ORDER BY T.NombreTenant;
                """;
            command.Parameters.Add("@userId", SqlDbType.Int).Value = userId;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var tenantId = reader.GetInt32(reader.GetOrdinal("TenantId"));
                if (options.Any(o => o.TenantId == tenantId))
                {
                    continue;
                }

                var tenantName = reader.GetString(reader.GetOrdinal("TenantName"));
                var planKey = reader["PlanKey"] is DBNull ? CfsPlans.Basic : reader.GetString(reader.GetOrdinal("PlanKey"));
                var roleKeys = reader.GetString(reader.GetOrdinal("RoleKeys"))
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
                options.Add(new TenantAccessOption(tenantId, tenantName, planKey, roleKeys, false));
            }
        }

        return options;
    }

    private static async Task<IReadOnlyList<string>> LoadHomeRolesAsync(
        SqlConnection connection,
        int userId,
        CancellationToken cancellationToken)
    {
        var roles = new List<string>();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT R.NombreRol
            FROM dbo.UsuarioRoles UR
            INNER JOIN dbo.Roles R ON R.ID_Rol = UR.ID_Rol_FK
            WHERE UR.ID_Usuario_FK = @userId
            ORDER BY R.NombreRol;
            """;
        command.Parameters.Add("@userId", SqlDbType.Int).Value = userId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(reader.GetString(0));
        }

        return roles;
    }
}
