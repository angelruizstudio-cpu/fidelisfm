using System.Data;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlUserAuthenticationRepository(SqlConnectionFactory connectionFactory) : IUserAuthenticationRepository
{
    public async Task<AuthenticatedUser?> ValidateCredentialsAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var hasCurrentColumns = await HasColumnsAsync(connection, "ContrasenaSalt", "ContrasenaHash", cancellationToken);
        var hasLegacyColumns = await HasColumnsAsync(connection, "Salt", "Hash", cancellationToken);
        var hasTenantColumn = await HasColumnAsync(connection, "ID_Tenant_FK", cancellationToken);
        var hasMustChange = await HasColumnAsync(connection, "MustChangePassword", cancellationToken);
        var hasEmail = await HasColumnAsync(connection, "Email", cancellationToken);
        var hasIsActive = await HasColumnAsync(connection, "IsActive", cancellationToken);
        var hasIterations = await HasColumnAsync(connection, "ContrasenaIteraciones", cancellationToken);

        if (!hasCurrentColumns && !hasLegacyColumns)
        {
            return null;
        }

        // Before the iterations column exists, every stored hash was created at the legacy count.
        var iterationsColumn = hasIterations
            ? "ContrasenaIteraciones"
            : $"CAST({PasswordHasher.LegacyIterations} AS INT) AS ContrasenaIteraciones";

        var passwordColumns = $"""
            {(hasCurrentColumns ? "ContrasenaSalt" : "CAST(NULL AS VARBINARY(MAX)) AS ContrasenaSalt")},
            {(hasCurrentColumns ? "ContrasenaHash" : "CAST(NULL AS VARBINARY(MAX)) AS ContrasenaHash")},
            {(hasLegacyColumns ? "Salt" : "CAST(NULL AS VARBINARY(MAX)) AS Salt")},
            {(hasLegacyColumns ? "Hash" : "CAST(NULL AS VARBINARY(MAX)) AS Hash")},
            {iterationsColumn},
            {(hasTenantColumn ? "ID_Tenant_FK" : "CAST(1 AS INT) AS ID_Tenant_FK")},
            {(hasMustChange ? "MustChangePassword" : "CAST(0 AS BIT) AS MustChangePassword")},
            {(hasEmail ? "Email" : "CAST(NULL AS NVARCHAR(256)) AS Email")},
            {(hasIsActive ? "IsActive" : "CAST(1 AS BIT) AS IsActive")}
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT ID_Usuario,
                   Nombre,
                   Apellido,
                   NombreUsuario,
                   {passwordColumns}
            FROM dbo.Usuarios
            WHERE NombreUsuario = @userName;
            """;
        command.Parameters.Add("@userName", SqlDbType.NVarChar, 100).Value = userName.Trim();

        int userId;
        string storedUserName;
        string fullName;
        byte[]? salt;
        byte[]? expectedHash;
        int tenantId;
        bool mustChangePassword;
        string? email;
        bool isActive;
        int iterations;

        await using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            userId = reader.GetInt32(reader.GetOrdinal("ID_Usuario"));
            storedUserName = reader.GetString(reader.GetOrdinal("NombreUsuario"));
            fullName = JoinName(
                reader["Nombre"] as string,
                reader["Apellido"] as string,
                storedUserName);

            var currentSalt = ReadBytes(reader, "ContrasenaSalt");
            var currentHash = ReadBytes(reader, "ContrasenaHash");
            var legacySalt = ReadBytes(reader, "Salt");
            var legacyHash = ReadBytes(reader, "Hash");

            salt = HasValue(currentSalt) && HasValue(currentHash) ? currentSalt : legacySalt;
            expectedHash = HasValue(currentSalt) && HasValue(currentHash) ? currentHash : legacyHash;
            tenantId = reader.GetInt32(reader.GetOrdinal("ID_Tenant_FK"));
            mustChangePassword = reader.GetBoolean(reader.GetOrdinal("MustChangePassword"));
            email = reader["Email"] as string;
            isActive = reader.GetBoolean(reader.GetOrdinal("IsActive"));
            iterations = reader["ContrasenaIteraciones"] is DBNull
                ? PasswordHasher.LegacyIterations
                : reader.GetInt32(reader.GetOrdinal("ContrasenaIteraciones"));
        }

        if (!isActive || !PasswordHasher.Verify(password, salt, expectedHash, iterations))
        {
            return null;
        }

        // Login succeeded: opportunistically upgrade an old hash to the current cost using the
        // plaintext we were just given. Best-effort — a failure here must never block the login.
        if (PasswordHasher.NeedsRehash(iterations))
        {
            await TryUpgradeHashAsync(connection, userId, password, cancellationToken);
        }

        var roles = await LoadRolesAsync(connection, userId, cancellationToken);
        var (tenantName, planKey) = await LoadTenantInfoAsync(connection, tenantId, cancellationToken);
        return new AuthenticatedUser(userId, storedUserName, fullName, roles, tenantId, tenantName, planKey, mustChangePassword, email);
    }

    private static async Task<bool> HasColumnsAsync(
        SqlConnection connection,
        string saltColumn,
        string hashColumn,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE
                WHEN COL_LENGTH('dbo.Usuarios', @saltColumn) IS NOT NULL
                 AND COL_LENGTH('dbo.Usuarios', @hashColumn) IS NOT NULL
                THEN 1 ELSE 0 END;
            """;
        command.Parameters.Add("@saltColumn", SqlDbType.NVarChar, 128).Value = saltColumn;
        command.Parameters.Add("@hashColumn", SqlDbType.NVarChar, 128).Value = hashColumn;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) == 1;
    }

    private static async Task<bool> HasColumnAsync(SqlConnection connection, string columnName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN COL_LENGTH('dbo.Usuarios', @columnName) IS NOT NULL THEN 1 ELSE 0 END;";
        command.Parameters.Add("@columnName", SqlDbType.NVarChar, 128).Value = columnName;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) == 1;
    }

    private static async Task<(string TenantName, string PlanKey)> LoadTenantInfoAsync(
        SqlConnection connection,
        int tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT T.NombreTenant,
                   (SELECT TOP 1 TS.PlanKey FROM dbo.TenantSubscriptions TS
                    WHERE TS.ID_Tenant_FK = T.ID_Tenant ORDER BY TS.StartedAt DESC) AS PlanKey
            FROM dbo.Tenants T
            WHERE T.ID_Tenant = @tenantId;
            """;
        command.Parameters.Add("@tenantId", SqlDbType.Int).Value = tenantId;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var tenantName = reader.GetString(reader.GetOrdinal("NombreTenant"));
            var planKey = reader["PlanKey"] is DBNull ? CfsPlans.Basic : reader.GetString(reader.GetOrdinal("PlanKey"));
            return (tenantName, planKey);
        }

        return ("Iglesia Cristiana Pentecostes Inc", CfsPlans.Founder);
    }

    private static async Task<IReadOnlyList<string>> LoadRolesAsync(
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
            roles.Add(reader.GetString(reader.GetOrdinal("NombreRol")));
        }

        return roles;
    }

    private static async Task TryUpgradeHashAsync(
        SqlConnection connection,
        int userId,
        string password,
        CancellationToken cancellationToken)
    {
        try
        {
            var (salt, hash, iterations) = PasswordHasher.Hash(password);

            // Single statement: salt, hash and iterations are updated together atomically.
            const string sql = """
                UPDATE dbo.Usuarios
                   SET ContrasenaSalt = @salt, ContrasenaHash = @hash, ContrasenaIteraciones = @iterations
                 WHERE ID_Usuario = @userId;
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@salt", SqlDbType.VarBinary, salt.Length).Value = salt;
            command.Parameters.Add("@hash", SqlDbType.VarBinary, hash.Length).Value = hash;
            command.Parameters.Add("@iterations", SqlDbType.Int).Value = iterations;
            command.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // Never block a successful login because the transparent rehash failed
            // (e.g. the ContrasenaIteraciones column is not present yet).
        }
    }

    private static byte[]? ReadBytes(SqlDataReader reader, string columnName)
    {
        var value = reader[columnName];
        return value is DBNull ? null : (byte[])value;
    }

    private static bool HasValue(byte[]? value) => value is { Length: > 0 };

    private static string JoinName(string? name, string? lastName, string fallback)
    {
        var fullName = string.Join(" ", new[] { name, lastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(fullName) ? fallback : fullName;
    }
}
