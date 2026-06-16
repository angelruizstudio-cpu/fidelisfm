using System.Data;
using System.Security.Cryptography;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlUserAuthenticationRepository(SqlConnectionFactory connectionFactory) : IUserAuthenticationRepository
{
    private const int Iterations = 100000;

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

        if (!hasCurrentColumns && !hasLegacyColumns)
        {
            return null;
        }

        var passwordColumns = $"""
            {(hasCurrentColumns ? "ContrasenaSalt" : "CAST(NULL AS VARBINARY(MAX)) AS ContrasenaSalt")},
            {(hasCurrentColumns ? "ContrasenaHash" : "CAST(NULL AS VARBINARY(MAX)) AS ContrasenaHash")},
            {(hasLegacyColumns ? "Salt" : "CAST(NULL AS VARBINARY(MAX)) AS Salt")},
            {(hasLegacyColumns ? "Hash" : "CAST(NULL AS VARBINARY(MAX)) AS Hash")}
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
        }

        if (!VerifyPassword(password, salt, expectedHash))
        {
            return null;
        }

        var roles = await LoadRolesAsync(connection, userId, cancellationToken);
        return new AuthenticatedUser(userId, storedUserName, fullName, roles);
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

    private static bool VerifyPassword(string password, byte[]? salt, byte[]? expectedHash)
    {
        if (salt is not { Length: > 0 } || expectedHash is not { Length: > 0 })
        {
            return false;
        }

        var calculatedHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(calculatedHash, expectedHash);
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
