using System.Data;
using System.Security.Cryptography;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlUserManagementRepository(SqlConnectionFactory connectionFactory, ITenantContext tenantContext) : IUserManagementRepository
{
    private const int TokenExpiryHours = 48;
    private const int MinPasswordLength = 8;

    private readonly int _tenantId = tenantContext.TenantId;

    public async Task<IReadOnlyList<TenantUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT U.ID_Usuario, U.NombreUsuario, U.Nombre, U.Apellido,
                   U.Email, U.IsActive, U.MustChangePassword
              FROM dbo.Usuarios U
             WHERE U.ID_Tenant_FK = @tenantId
             ORDER BY U.Nombre, U.Apellido, U.NombreUsuario;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@tenantId", SqlDbType.Int).Value = _tenantId;

        var users = new List<TenantUser>();
        var ids = new List<int>();
        var map = new Dictionary<int, (string UserName, string FullName, string? Email, bool IsActive, bool MustChange)>();

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetInt32(0);
                var nombre = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var apellido = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                var fullName = $"{nombre} {apellido}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = reader.GetString(1);

                ids.Add(id);
                map[id] = (
                    reader.GetString(1),
                    fullName,
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetBoolean(5),
                    reader.GetBoolean(6));
            }
        }

        if (ids.Count == 0) return users;

        var roles = await LoadRolesForUsersAsync(connection, ids, cancellationToken);

        foreach (var id in ids)
        {
            var (userName, fullName, email, isActive, mustChange) = map[id];
            users.Add(new TenantUser(id, userName, fullName, email, isActive, mustChange,
                roles.GetValueOrDefault(id, [])));
        }

        return users;
    }

    public async Task<int> GetUserCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT COUNT(*) FROM dbo.Usuarios WHERE ID_Tenant_FK = @tenantId;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@tenantId", SqlDbType.Int).Value = _tenantId;
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<UserSaveResult> CreateUserAsync(UserCreateEntry entry, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        // Check for duplicate username globally
        const string checkSql = "SELECT COUNT(*) FROM dbo.Usuarios WHERE NombreUsuario = @userName;";
        await using (var check = new SqlCommand(checkSql, connection))
        {
            check.Parameters.Add("@userName", SqlDbType.NVarChar, 100).Value = entry.UserName.Trim();
            var count = (int)(await check.ExecuteScalarAsync(cancellationToken))!;
            if (count > 0)
            {
                return new UserSaveResult(false, null, null, "El nombre de usuario ya está en uso.");
            }
        }

        var roleId = await GetRoleIdAsync(connection, entry.Role, cancellationToken);
        if (roleId is null)
        {
            return new UserSaveResult(false, null, null, $"El rol '{entry.Role}' no existe en el sistema.");
        }

        var tempPassword = GenerateTempPassword();
        var (salt, hash, iterations) = PasswordHasher.Hash(tempPassword);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            const string insertSql = """
                INSERT INTO dbo.Usuarios
                    (Nombre, Apellido, NombreUsuario, Email, ContrasenaSalt, ContrasenaHash, ContrasenaIteraciones,
                     ID_Tenant_FK, IsActive, MustChangePassword)
                VALUES
                    (@nombre, @apellido, @userName, @email, @salt, @hash, @iterations,
                     @tenantId, 1, 1);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """;

            int userId;
            await using (var insert = new SqlCommand(insertSql, connection, transaction))
            {
                insert.Parameters.Add("@nombre", SqlDbType.VarChar, 100).Value = entry.FirstName.Trim();
                insert.Parameters.Add("@apellido", SqlDbType.VarChar, 100).Value = entry.LastName.Trim();
                insert.Parameters.Add("@userName", SqlDbType.VarChar, 100).Value = entry.UserName.Trim();
                insert.Parameters.Add("@email", SqlDbType.NVarChar, 256).Value = (object?)entry.Email.Trim() ?? DBNull.Value;
                insert.Parameters.Add("@salt", SqlDbType.VarBinary, salt.Length).Value = salt;
                insert.Parameters.Add("@hash", SqlDbType.VarBinary, hash.Length).Value = hash;
                insert.Parameters.Add("@iterations", SqlDbType.Int).Value = iterations;
                insert.Parameters.Add("@tenantId", SqlDbType.Int).Value = _tenantId;
                userId = Convert.ToInt32(await insert.ExecuteScalarAsync(cancellationToken));
            }

            const string roleSql = "INSERT INTO dbo.UsuarioRoles (ID_Usuario_FK, ID_Rol_FK) VALUES (@userId, @roleId);";
            await using (var roleCmd = new SqlCommand(roleSql, connection, transaction))
            {
                roleCmd.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
                roleCmd.Parameters.Add("@roleId", SqlDbType.Int).Value = roleId.Value;
                await roleCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            var token = await InsertResetTokenAsync(connection, transaction, userId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new UserSaveResult(true, userId, token, null);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> SetActiveAsync(int userId, bool active, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = "UPDATE dbo.Usuarios SET IsActive = @active WHERE ID_Usuario = @userId AND ID_Tenant_FK = @tenantId;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@active", SqlDbType.Bit).Value = active;
        command.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@tenantId", SqlDbType.Int).Value = _tenantId;
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string deleteRoles = "DELETE FROM dbo.UsuarioRoles WHERE ID_Usuario_FK = @userId;";
            await using (var cmd = new SqlCommand(deleteRoles, connection, transaction))
            {
                cmd.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string deleteTokens = "DELETE FROM dbo.PasswordResetTokens WHERE ID_Usuario_FK = @userId;";
            await using (var cmd = new SqlCommand(deleteTokens, connection, transaction))
            {
                cmd.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string deleteUser = "DELETE FROM dbo.Usuarios WHERE ID_Usuario = @userId AND ID_Tenant_FK = @tenantId;";
            int affected;
            await using (var cmd = new SqlCommand(deleteUser, connection, transaction))
            {
                cmd.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
                cmd.Parameters.Add("@tenantId", SqlDbType.Int).Value = _tenantId;
                affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return affected > 0;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<string?> GenerateResetTokenAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        // Verify user belongs to this tenant
        const string checkSql = "SELECT COUNT(*) FROM dbo.Usuarios WHERE ID_Usuario = @userId AND ID_Tenant_FK = @tenantId;";
        await using (var check = new SqlCommand(checkSql, connection))
        {
            check.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
            check.Parameters.Add("@tenantId", SqlDbType.Int).Value = _tenantId;
            if ((int)(await check.ExecuteScalarAsync(cancellationToken))! == 0) return null;
        }

        return await InsertResetTokenAsync(connection, null, userId, cancellationToken);
    }

    public async Task<PasswordResetTokenInfo?> ValidateResetTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT T.ID_Usuario_FK, U.NombreUsuario, U.Email,
                   CASE WHEN T.ExpiresAt < SYSUTCDATETIME() THEN 1 ELSE 0 END AS IsExpired,
                   CASE WHEN T.UsedAt IS NOT NULL THEN 1 ELSE 0 END AS IsUsed
              FROM dbo.PasswordResetTokens T
              JOIN dbo.Usuarios U ON U.ID_Usuario = T.ID_Usuario_FK
             WHERE T.Token = @token;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@token", SqlDbType.NVarChar, 128).Value = token;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new PasswordResetTokenInfo(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetBoolean(3),
            reader.GetBoolean(4));
    }

    public async Task<bool> ConsumeResetTokenAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        if (newPassword.Length < MinPasswordLength) return false;

        var info = await ValidateResetTokenAsync(token, cancellationToken);
        if (info is null || !info.IsValid) return false;

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var (salt, hash, iterations) = PasswordHasher.Hash(newPassword);

            // IsActive = 1 in the WHERE (not the SET): a deactivated user must not be able
            // to re-enable their own account with a leftover reset link.
            const string updateUser = """
                UPDATE dbo.Usuarios
                   SET ContrasenaSalt = @salt, ContrasenaHash = @hash, ContrasenaIteraciones = @iterations,
                       MustChangePassword = 0
                 WHERE ID_Usuario = @userId AND IsActive = 1;
                """;

            await using (var cmd = new SqlCommand(updateUser, connection, transaction))
            {
                cmd.Parameters.Add("@salt", SqlDbType.VarBinary, salt.Length).Value = salt;
                cmd.Parameters.Add("@hash", SqlDbType.VarBinary, hash.Length).Value = hash;
                cmd.Parameters.Add("@iterations", SqlDbType.Int).Value = iterations;
                cmd.Parameters.Add("@userId", SqlDbType.Int).Value = info.UserId;
                if (await cmd.ExecuteNonQueryAsync(cancellationToken) == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return false;
                }
            }

            const string markUsed = "UPDATE dbo.PasswordResetTokens SET UsedAt = SYSUTCDATETIME() WHERE Token = @token;";
            await using (var cmd = new SqlCommand(markUsed, connection, transaction))
            {
                cmd.Parameters.Add("@token", SqlDbType.NVarChar, 128).Value = token;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> ChangePasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default)
    {
        if (newPassword.Length < MinPasswordLength) return false;

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var (salt, hash, iterations) = PasswordHasher.Hash(newPassword);

        const string sql = """
            UPDATE dbo.Usuarios
               SET ContrasenaSalt = @salt, ContrasenaHash = @hash, ContrasenaIteraciones = @iterations, MustChangePassword = 0
             WHERE ID_Usuario = @userId AND ID_Tenant_FK = @tenantId;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@salt", SqlDbType.VarBinary, salt.Length).Value = salt;
        command.Parameters.Add("@hash", SqlDbType.VarBinary, hash.Length).Value = hash;
        command.Parameters.Add("@iterations", SqlDbType.Int).Value = iterations;
        command.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@tenantId", SqlDbType.Int).Value = _tenantId;
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static async Task<string> InsertResetTokenAsync(
        SqlConnection connection, SqlTransaction? transaction, int userId, CancellationToken cancellationToken)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(48));
        var expiresAt = DateTime.UtcNow.AddHours(TokenExpiryHours);

        const string sql = """
            INSERT INTO dbo.PasswordResetTokens (ID_Usuario_FK, ID_Tenant_FK, Token, ExpiresAt)
            SELECT @userId, ID_Tenant_FK, @token, @expiresAt
              FROM dbo.Usuarios WHERE ID_Usuario = @userId;
            """;

        await using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);

        command.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@token", SqlDbType.NVarChar, 128).Value = token;
        command.Parameters.Add("@expiresAt", SqlDbType.DateTime2).Value = expiresAt;
        await command.ExecuteNonQueryAsync(cancellationToken);
        return token;
    }

    private static async Task<int?> GetRoleIdAsync(SqlConnection connection, string roleName, CancellationToken cancellationToken)
    {
        const string sql = "SELECT TOP 1 ID_Rol FROM dbo.Roles WHERE NombreRol = @roleName;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@roleName", SqlDbType.NVarChar, 100).Value = roleName;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is DBNull or null ? null : Convert.ToInt32(result);
    }

    private static async Task<Dictionary<int, List<string>>> LoadRolesForUsersAsync(
        SqlConnection connection, List<int> userIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, List<string>>();
        if (userIds.Count == 0) return result;

        var inClause = string.Join(",", userIds.Select((_, i) => $"@id{i}"));
        var sql = $"""
            SELECT UR.ID_Usuario_FK, R.NombreRol
              FROM dbo.UsuarioRoles UR
              JOIN dbo.Roles R ON R.ID_Rol = UR.ID_Rol_FK
             WHERE UR.ID_Usuario_FK IN ({inClause})
             ORDER BY UR.ID_Usuario_FK, R.NombreRol;
            """;

        await using var command = new SqlCommand(sql, connection);
        for (var i = 0; i < userIds.Count; i++)
        {
            command.Parameters.Add($"@id{i}", SqlDbType.Int).Value = userIds[i];
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var userId = reader.GetInt32(0);
            var role = reader.GetString(1);
            if (!result.TryGetValue(userId, out var list))
            {
                list = [];
                result[userId] = list;
            }

            list.Add(role);
        }

        return result;
    }

    private static string GenerateTempPassword()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$";
        return new string(RandomNumberGenerator.GetItems<char>(chars, 16));
    }
}
