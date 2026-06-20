using System.Data;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlBillingRepository(SqlConnectionFactory connectionFactory) : IBillingRepository
{
    public async Task GrantAddonFeaturesAsync(
        int tenantId,
        IReadOnlyList<string> featureKeys,
        string? stripeCustomerId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        foreach (var featureKey in featureKeys)
        {
            const string sql = """
                UPDATE dbo.TenantFeatureOverrides
                   SET Enabled = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = 'stripe-webhook'
                 WHERE ID_Tenant_FK = @tenantId AND FeatureKey = @featureKey;

                IF @@ROWCOUNT = 0
                    INSERT INTO dbo.TenantFeatureOverrides (ID_Tenant_FK, FeatureKey, Enabled, UpdatedBy)
                    VALUES (@tenantId, @featureKey, 1, 'stripe-webhook');
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@tenantId", SqlDbType.Int).Value = tenantId;
            command.Parameters.Add("@featureKey", SqlDbType.NVarChar, 100).Value = featureKey;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(stripeCustomerId))
        {
            const string updateCustomerSql = """
                UPDATE dbo.TenantSubscriptions
                   SET StripeCustomerId = @stripeCustomerId
                 WHERE ID_Tenant_FK = @tenantId AND StripeCustomerId IS NULL;
                """;
            await using var command = new SqlCommand(updateCustomerSql, connection);
            command.Parameters.Add("@tenantId", SqlDbType.Int).Value = tenantId;
            command.Parameters.Add("@stripeCustomerId", SqlDbType.NVarChar, 100).Value = stripeCustomerId;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<string?> CompleteUpgradeAsync(
        int tenantId,
        string newPlanKey,
        string? newStripeSubscriptionId,
        string? stripeCustomerId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        string? previousSubscriptionId;
        const string selectSql = """
            SELECT TOP 1 StripeSubscriptionId
            FROM dbo.TenantSubscriptions
            WHERE ID_Tenant_FK = @tenantId
            ORDER BY StartedAt DESC;
            """;
        await using (var command = new SqlCommand(selectSql, connection))
        {
            command.Parameters.Add("@tenantId", SqlDbType.Int).Value = tenantId;
            var result = await command.ExecuteScalarAsync(cancellationToken);
            previousSubscriptionId = result is null or DBNull ? null : (string)result;
        }

        const string updateSql = """
            UPDATE dbo.TenantSubscriptions
               SET PlanKey = @planKey,
                   StripeSubscriptionId = @newSubscriptionId,
                   StripeCustomerId = ISNULL(@stripeCustomerId, StripeCustomerId)
             WHERE ID_Tenant_FK = @tenantId;
            """;
        await using (var command = new SqlCommand(updateSql, connection))
        {
            command.Parameters.Add("@planKey", SqlDbType.NVarChar, 50).Value = newPlanKey;
            command.Parameters.Add("@newSubscriptionId", SqlDbType.NVarChar, 100).Value = (object?)newStripeSubscriptionId ?? DBNull.Value;
            command.Parameters.Add("@stripeCustomerId", SqlDbType.NVarChar, 100).Value = (object?)stripeCustomerId ?? DBNull.Value;
            command.Parameters.Add("@tenantId", SqlDbType.Int).Value = tenantId;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return previousSubscriptionId == newStripeSubscriptionId ? null : previousSubscriptionId;
    }
}
