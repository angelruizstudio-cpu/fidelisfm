using System.Data;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlOrganizationLabelService(SqlConnectionFactory connectionFactory, ITenantContext tenantContext) : IOrganizationLabelService
{
    private readonly int _tenantId = tenantContext.TenantId;

    public async Task<OrganizationLabels> GetLabelsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (settings is null)
        {
            return OrganizationLabelDefaults.GetDefaults(OrganizationType.Church);
        }

        var defaults = OrganizationLabelDefaults.GetDefaults(settings.OrganizationType);
        return settings.Labels with
        {
            ContactsLabel = settings.Labels.ContactsLabel ?? defaults.ContactsLabel,
            IncomeLabel = settings.Labels.IncomeLabel ?? defaults.IncomeLabel,
            ContributionsLabel = settings.Labels.ContributionsLabel ?? defaults.ContributionsLabel,
            DepartmentsLabel = settings.Labels.DepartmentsLabel ?? defaults.DepartmentsLabel,
            FundsLabel = settings.Labels.FundsLabel ?? defaults.FundsLabel,
            ReportsLabel = settings.Labels.ReportsLabel ?? defaults.ReportsLabel,
            LeadershipReportLabel = settings.Labels.LeadershipReportLabel ?? defaults.LeadershipReportLabel,
            PrimaryOfficerLabel = settings.Labels.PrimaryOfficerLabel ?? defaults.PrimaryOfficerLabel,
            SecondaryOfficerLabel = settings.Labels.SecondaryOfficerLabel ?? defaults.SecondaryOfficerLabel
        };
    }

    public async Task<OrganizationSettings?> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, cancellationToken))
        {
            return null;
        }

        const string sql = """
            SELECT TOP 1
                   OrganizationType,
                   DisplayName,
                   ContactsLabel,
                   IncomeLabel,
                   ContributionsLabel,
                   DepartmentsLabel,
                   FundsLabel,
                   ReportsLabel,
                   LeadershipReportLabel,
                   PrimaryOfficerLabel,
                   SecondaryOfficerLabel,
                   CreatedAt,
                   UpdatedAt
            FROM dbo.OrganizationSettings
            WHERE ID_Tenant_FK = @tenantId;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadSettings(reader)
            : null;
    }

    private static async Task<bool> TableExistsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID('dbo.OrganizationSettings');",
            connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private OrganizationSettings ReadSettings(SqlDataReader reader)
    {
        var typeText = reader["OrganizationType"] is DBNull ? null : reader.GetString(reader.GetOrdinal("OrganizationType"));
        var organizationType = Enum.TryParse<OrganizationType>(typeText, ignoreCase: true, out var parsed)
            ? parsed
            : OrganizationType.Church;

        var defaults = OrganizationLabelDefaults.GetDefaults(organizationType);

        var labels = new OrganizationLabels(
            ContactsLabel: GetString(reader, "ContactsLabel") ?? defaults.ContactsLabel,
            IncomeLabel: GetString(reader, "IncomeLabel") ?? defaults.IncomeLabel,
            ContributionsLabel: GetString(reader, "ContributionsLabel") ?? defaults.ContributionsLabel,
            DepartmentsLabel: GetString(reader, "DepartmentsLabel") ?? defaults.DepartmentsLabel,
            FundsLabel: GetString(reader, "FundsLabel") ?? defaults.FundsLabel,
            ReportsLabel: GetString(reader, "ReportsLabel") ?? defaults.ReportsLabel,
            LeadershipReportLabel: GetString(reader, "LeadershipReportLabel") ?? defaults.LeadershipReportLabel,
            PrimaryOfficerLabel: GetString(reader, "PrimaryOfficerLabel") ?? defaults.PrimaryOfficerLabel,
            SecondaryOfficerLabel: GetString(reader, "SecondaryOfficerLabel") ?? defaults.SecondaryOfficerLabel);

        return new OrganizationSettings(
            TenantId: _tenantId,
            OrganizationType: organizationType,
            DisplayName: GetString(reader, "DisplayName"),
            Labels: labels,
            CreatedAt: reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt: reader.GetDateTime(reader.GetOrdinal("UpdatedAt")));
    }

    private static string? GetString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
