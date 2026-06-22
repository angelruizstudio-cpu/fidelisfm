using System.Data;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlExternalInvoiceRepository(SqlConnectionFactory connectionFactory) : IExternalInvoiceRepository
{
    public async Task<TenantApiKeyLookup?> FindTenantByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP 1 ID_Tenant_FK
              FROM dbo.TenantApiKeys
             WHERE ApiKeyHash = @apiKeyHash AND RevokedAt IS NULL;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@apiKeyHash", SqlDbType.NVarChar, 128).Value = apiKeyHash;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : new TenantApiKeyLookup((int)result);
    }

    public async Task<(int Id, bool AlreadyExisted)> CreateInvoiceRequestAsync(ExternalInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.ExternalReference))
        {
            const string existingSql = """
                SELECT ID_ExternalInvoiceRequest
                  FROM dbo.ExternalInvoiceRequests
                 WHERE ID_Tenant_FK = @tenantId AND ExternalReference = @externalReference;
                """;
            await using var existingCommand = new SqlCommand(existingSql, connection);
            existingCommand.Parameters.Add("@tenantId", SqlDbType.Int).Value = request.TenantId;
            existingCommand.Parameters.Add("@externalReference", SqlDbType.NVarChar, 100).Value = request.ExternalReference;
            var existing = await existingCommand.ExecuteScalarAsync(cancellationToken);
            if (existing is int existingId)
            {
                return (existingId, true);
            }
        }

        const string insertSql = """
            INSERT INTO dbo.ExternalInvoiceRequests
                (ID_Tenant_FK, RecipientName, RecipientEmail, AmountCents, Currency, Description, ExternalReference)
            OUTPUT INSERTED.ID_ExternalInvoiceRequest
            VALUES (@tenantId, @recipientName, @recipientEmail, @amountCents, @currency, @description, @externalReference);
            """;
        await using var command = new SqlCommand(insertSql, connection);
        command.Parameters.Add("@tenantId", SqlDbType.Int).Value = request.TenantId;
        command.Parameters.Add("@recipientName", SqlDbType.NVarChar, 150).Value = request.RecipientName;
        command.Parameters.Add("@recipientEmail", SqlDbType.NVarChar, 256).Value = request.RecipientEmail;
        command.Parameters.Add("@amountCents", SqlDbType.Int).Value = request.AmountCents;
        command.Parameters.Add("@currency", SqlDbType.NVarChar, 3).Value = request.Currency;
        command.Parameters.Add("@description", SqlDbType.NVarChar, 500).Value = request.Description;
        command.Parameters.Add("@externalReference", SqlDbType.NVarChar, 100).Value = (object?)request.ExternalReference ?? DBNull.Value;

        var id = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        return (id, false);
    }

    public async Task MarkInvoiceSucceededAsync(
        int id,
        string stripeCustomerId,
        string stripeInvoiceId,
        string? stripeHostedInvoiceUrl,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.ExternalInvoiceRequests
               SET Status = 'Sent',
                   StripeCustomerId = @stripeCustomerId,
                   StripeInvoiceId = @stripeInvoiceId,
                   StripeHostedInvoiceUrl = @stripeHostedInvoiceUrl,
                   ErrorMessage = NULL,
                   UpdatedAt = SYSUTCDATETIME()
             WHERE ID_ExternalInvoiceRequest = @id;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@stripeCustomerId", SqlDbType.NVarChar, 100).Value = stripeCustomerId;
        command.Parameters.Add("@stripeInvoiceId", SqlDbType.NVarChar, 100).Value = stripeInvoiceId;
        command.Parameters.Add("@stripeHostedInvoiceUrl", SqlDbType.NVarChar, 500).Value = (object?)stripeHostedInvoiceUrl ?? DBNull.Value;
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkInvoiceFailedAsync(int id, string errorMessage, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.ExternalInvoiceRequests
               SET Status = 'Failed',
                   ErrorMessage = @errorMessage,
                   UpdatedAt = SYSUTCDATETIME()
             WHERE ID_ExternalInvoiceRequest = @id;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@errorMessage", SqlDbType.NVarChar, 1000).Value = errorMessage;
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CreateApiKeyAsync(int tenantId, string apiKeyHash, string? label, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO dbo.TenantApiKeys (ID_Tenant_FK, ApiKeyHash, Label)
            OUTPUT INSERTED.ID_TenantApiKey
            VALUES (@tenantId, @apiKeyHash, @label);
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@tenantId", SqlDbType.Int).Value = tenantId;
        command.Parameters.Add("@apiKeyHash", SqlDbType.NVarChar, 128).Value = apiKeyHash;
        command.Parameters.Add("@label", SqlDbType.NVarChar, 100).Value = (object?)label ?? DBNull.Value;

        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
