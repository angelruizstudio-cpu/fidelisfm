using System.Data;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlSignupRepository(SqlConnectionFactory connectionFactory) : ISignupRepository
{
    public async Task RecordPendingSignupAsync(PendingSignup signup, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.PendingSignups WHERE StripeSessionId = @stripeSessionId)
            BEGIN
                INSERT INTO dbo.PendingSignups
                    (OrganizationName, Email, Phone, PlanKey, BillingCycle, StripeSessionId, StripeCustomerId)
                VALUES
                    (@organizationName, @email, @phone, @planKey, @billingCycle, @stripeSessionId, @stripeCustomerId);
            END;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@organizationName", SqlDbType.NVarChar, 150).Value = signup.OrganizationName;
        command.Parameters.Add("@email", SqlDbType.NVarChar, 256).Value = signup.Email;
        command.Parameters.Add("@phone", SqlDbType.NVarChar, 50).Value = (object?)signup.Phone ?? DBNull.Value;
        command.Parameters.Add("@planKey", SqlDbType.NVarChar, 50).Value = signup.PlanKey;
        command.Parameters.Add("@billingCycle", SqlDbType.NVarChar, 20).Value = signup.BillingCycle;
        command.Parameters.Add("@stripeSessionId", SqlDbType.NVarChar, 100).Value = signup.StripeSessionId;
        command.Parameters.Add("@stripeCustomerId", SqlDbType.NVarChar, 100).Value = (object?)signup.StripeCustomerId ?? DBNull.Value;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
