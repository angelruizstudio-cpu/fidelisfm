using System.Data;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlSalesLeadRepository(SqlConnectionFactory connectionFactory) : ISalesLeadRepository
{
    public async Task CreateAsync(SalesLead lead, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO dbo.SalesLeads
                (OrganizationName, ContactName, Email, Phone, ChurchCount, KeyFeatures, Timeline, Comments)
            VALUES
                (@organizationName, @contactName, @email, @phone, @churchCount, @keyFeatures, @timeline, @comments);
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@organizationName", SqlDbType.NVarChar, 150).Value = lead.OrganizationName;
        command.Parameters.Add("@contactName", SqlDbType.NVarChar, 150).Value = lead.ContactName;
        command.Parameters.Add("@email", SqlDbType.NVarChar, 256).Value = lead.Email;
        command.Parameters.Add("@phone", SqlDbType.NVarChar, 50).Value = lead.Phone;
        command.Parameters.Add("@churchCount", SqlDbType.Int).Value = lead.ChurchCount;
        command.Parameters.Add("@keyFeatures", SqlDbType.NVarChar, 500).Value = string.Join(", ", lead.KeyFeatures);
        command.Parameters.Add("@timeline", SqlDbType.NVarChar, 50).Value = lead.Timeline;
        command.Parameters.Add("@comments", SqlDbType.NVarChar, 1000).Value = (object?)lead.Comments ?? DBNull.Value;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
