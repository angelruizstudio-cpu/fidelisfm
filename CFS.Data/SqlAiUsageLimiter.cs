using System.Data;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlAiUsageLimiter(SqlConnectionFactory connectionFactory, ITenantContext tenantContext) : IAiUsageLimiter
{
    private readonly int _tenantId = tenantContext.TenantId;

    public async Task<AiUsageStatus> CheckAndIncrementAsync(string planKey, CancellationToken cancellationToken = default)
    {
        var limit = CfsAiQuotas.GetMonthlyLimit(planKey);
        var yearMonth = DateTime.UtcNow.ToString("yyyy-MM");

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            const string selectSql = """
                SELECT RequestCount
                FROM dbo.AiUsageMonthly WITH (UPDLOCK, HOLDLOCK)
                WHERE ID_Tenant_FK = @tenantId AND YearMonth = @yearMonth;
                """;

            int currentCount;
            await using (var command = new SqlCommand(selectSql, connection, transaction))
            {
                command.Parameters.Add("@tenantId", SqlDbType.Int).Value = _tenantId;
                command.Parameters.Add("@yearMonth", SqlDbType.Char, 7).Value = yearMonth;
                var result = await command.ExecuteScalarAsync(cancellationToken);
                currentCount = result is null or DBNull ? -1 : Convert.ToInt32(result);
            }

            if (currentCount >= limit)
            {
                await transaction.CommitAsync(cancellationToken);
                return new AiUsageStatus(Math.Max(currentCount, 0), limit, IsAllowed: false);
            }

            if (currentCount < 0)
            {
                const string insertSql = """
                    INSERT INTO dbo.AiUsageMonthly (ID_Tenant_FK, YearMonth, RequestCount, UpdatedAt)
                    VALUES (@tenantId, @yearMonth, 1, SYSUTCDATETIME());
                    """;
                await using var insertCommand = new SqlCommand(insertSql, connection, transaction);
                insertCommand.Parameters.Add("@tenantId", SqlDbType.Int).Value = _tenantId;
                insertCommand.Parameters.Add("@yearMonth", SqlDbType.Char, 7).Value = yearMonth;
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return new AiUsageStatus(1, limit, IsAllowed: true);
            }

            const string updateSql = """
                UPDATE dbo.AiUsageMonthly
                SET RequestCount = RequestCount + 1,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE ID_Tenant_FK = @tenantId AND YearMonth = @yearMonth;
                """;
            await using (var updateCommand = new SqlCommand(updateSql, connection, transaction))
            {
                updateCommand.Parameters.Add("@tenantId", SqlDbType.Int).Value = _tenantId;
                updateCommand.Parameters.Add("@yearMonth", SqlDbType.Char, 7).Value = yearMonth;
                await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new AiUsageStatus(currentCount + 1, limit, IsAllowed: true);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
