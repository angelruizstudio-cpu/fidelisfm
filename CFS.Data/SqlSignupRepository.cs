using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlSignupRepository(SqlConnectionFactory connectionFactory) : ISignupRepository
{
    private const int Iterations = 100000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public async Task CreatePendingSignupAsync(PendingSignup signup, string password, CancellationToken cancellationToken = default)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO dbo.PendingSignups
                (OrganizationName, Email, Phone, PlanKey, BillingCycle, StripeSessionId, PasswordSalt, PasswordHash)
            VALUES
                (@organizationName, @email, @phone, @planKey, @billingCycle, @stripeSessionId, @passwordSalt, @passwordHash);
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@organizationName", SqlDbType.NVarChar, 150).Value = signup.OrganizationName;
        command.Parameters.Add("@email", SqlDbType.NVarChar, 256).Value = signup.Email;
        command.Parameters.Add("@phone", SqlDbType.NVarChar, 50).Value = (object?)signup.Phone ?? DBNull.Value;
        command.Parameters.Add("@planKey", SqlDbType.NVarChar, 50).Value = signup.PlanKey;
        command.Parameters.Add("@billingCycle", SqlDbType.NVarChar, 20).Value = signup.BillingCycle;
        command.Parameters.Add("@stripeSessionId", SqlDbType.NVarChar, 100).Value = signup.StripeSessionId;
        command.Parameters.Add("@passwordSalt", SqlDbType.VarBinary, SaltSize).Value = salt;
        command.Parameters.Add("@passwordHash", SqlDbType.VarBinary, HashSize).Value = hash;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int?> CompleteSignupAndProvisionTenantAsync(string stripeSessionId, string? stripeCustomerId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            int pendingId;
            string organizationName, email, planKey;
            byte[]? passwordSalt, passwordHash;
            int? existingTenantId;

            const string selectSql = """
                SELECT ID_PendingSignup, OrganizationName, Email, PlanKey, PasswordSalt, PasswordHash, ProvisionedTenantId
                FROM dbo.PendingSignups WITH (UPDLOCK, HOLDLOCK)
                WHERE StripeSessionId = @sessionId;
                """;

            await using (var command = new SqlCommand(selectSql, connection, transaction))
            {
                command.Parameters.Add("@sessionId", SqlDbType.NVarChar, 100).Value = stripeSessionId;
                await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    await transaction.CommitAsync(cancellationToken);
                    return null;
                }

                pendingId = reader.GetInt32(reader.GetOrdinal("ID_PendingSignup"));
                organizationName = reader.GetString(reader.GetOrdinal("OrganizationName"));
                email = reader.GetString(reader.GetOrdinal("Email"));
                planKey = reader.GetString(reader.GetOrdinal("PlanKey"));
                passwordSalt = reader["PasswordSalt"] as byte[];
                passwordHash = reader["PasswordHash"] as byte[];
                existingTenantId = reader["ProvisionedTenantId"] is DBNull ? null : reader.GetInt32(reader.GetOrdinal("ProvisionedTenantId"));
            }

            if (existingTenantId is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return existingTenantId;
            }

            if (passwordSalt is null || passwordHash is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            var slug = $"{Slugify(organizationName)}-{Guid.NewGuid().ToString("N")[..6]}";

            int tenantId;
            const string insertTenantSql = """
                INSERT INTO dbo.Tenants (NombreTenant, Slug) VALUES (@name, @slug);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """;
            await using (var command = new SqlCommand(insertTenantSql, connection, transaction))
            {
                command.Parameters.Add("@name", SqlDbType.NVarChar, 150).Value = organizationName;
                command.Parameters.Add("@slug", SqlDbType.NVarChar, 100).Value = slug;
                tenantId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            }

            const string insertSubscriptionSql = """
                INSERT INTO dbo.TenantSubscriptions (ID_Tenant_FK, PlanKey, BillingRequired, IsFounderAccount, Status)
                VALUES (@tenantId, @planKey, 1, 0, 'Active');
                """;
            await using (var command = new SqlCommand(insertSubscriptionSql, connection, transaction))
            {
                command.Parameters.Add("@tenantId", SqlDbType.Int).Value = tenantId;
                command.Parameters.Add("@planKey", SqlDbType.NVarChar, 50).Value = planKey;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            int userId;
            const string insertUserSql = """
                INSERT INTO dbo.Usuarios (Nombre, Apellido, NombreUsuario, ContrasenaSalt, ContrasenaHash, ID_Tenant_FK)
                VALUES (@nombre, '', @userName, @salt, @hash, @tenantId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """;
            await using (var command = new SqlCommand(insertUserSql, connection, transaction))
            {
                command.Parameters.Add("@nombre", SqlDbType.VarChar, 100).Value = organizationName;
                command.Parameters.Add("@userName", SqlDbType.VarChar, 100).Value = email;
                command.Parameters.Add("@salt", SqlDbType.VarBinary, SaltSize).Value = passwordSalt;
                command.Parameters.Add("@hash", SqlDbType.VarBinary, HashSize).Value = passwordHash;
                command.Parameters.Add("@tenantId", SqlDbType.Int).Value = tenantId;
                userId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            }

            const string insertRoleSql = "INSERT INTO dbo.UsuarioRoles (ID_Usuario_FK, ID_Rol_FK) VALUES (@userId, 1);";
            await using (var command = new SqlCommand(insertRoleSql, connection, transaction))
            {
                command.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            const string updateSignupSql = """
                UPDATE dbo.PendingSignups
                SET Status = 'Provisioned',
                    ProvisionedTenantId = @tenantId,
                    ProvisionedAt = SYSUTCDATETIME(),
                    StripeCustomerId = @customerId
                WHERE ID_PendingSignup = @pendingId;
                """;
            await using (var command = new SqlCommand(updateSignupSql, connection, transaction))
            {
                command.Parameters.Add("@tenantId", SqlDbType.Int).Value = tenantId;
                command.Parameters.Add("@customerId", SqlDbType.NVarChar, 100).Value = (object?)stripeCustomerId ?? DBNull.Value;
                command.Parameters.Add("@pendingId", SqlDbType.Int).Value = pendingId;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return tenantId;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<PendingSignupRecord>> ListRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP (@take)
                ID_PendingSignup, OrganizationName, Email, PlanKey, BillingCycle, Status,
                StripeSessionId, ProvisionedTenantId, CreatedAt, ProvisionedAt
            FROM dbo.PendingSignups
            ORDER BY CreatedAt DESC;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@take", SqlDbType.Int).Value = take;

        var results = new List<PendingSignupRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PendingSignupRecord(
                reader.GetInt32(reader.GetOrdinal("ID_PendingSignup")),
                reader.GetString(reader.GetOrdinal("OrganizationName")),
                reader.GetString(reader.GetOrdinal("Email")),
                reader.GetString(reader.GetOrdinal("PlanKey")),
                reader.GetString(reader.GetOrdinal("BillingCycle")),
                reader.GetString(reader.GetOrdinal("Status")),
                reader.GetString(reader.GetOrdinal("StripeSessionId")),
                reader["ProvisionedTenantId"] is DBNull ? null : reader.GetInt32(reader.GetOrdinal("ProvisionedTenantId")),
                reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                reader["ProvisionedAt"] is DBNull ? null : reader.GetDateTime(reader.GetOrdinal("ProvisionedAt"))));
        }

        return results;
    }

    private static string Slugify(string value)
    {
        var lowered = value.Trim().ToLowerInvariant();
        var slug = Regex.Replace(lowered, "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "tenant" : slug;
    }
}
