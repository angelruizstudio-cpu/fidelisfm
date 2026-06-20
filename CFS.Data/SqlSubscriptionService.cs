using System.Data;
using System.Security.Claims;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlSubscriptionService(SqlConnectionFactory connectionFactory) : ISubscriptionService
{
    private static readonly IReadOnlyDictionary<string, string> FeatureNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [CfsFeatures.Dashboard] = "Dashboard",
        [CfsFeatures.Income] = "Ingresos",
        [CfsFeatures.Expenses] = "Egresos",
        [CfsFeatures.Deposits] = "Depósitos",
        [CfsFeatures.Reconciliation] = "Conciliación bancaria",
        [CfsFeatures.ReportsBasic] = "Reportes básicos",
        [CfsFeatures.ReportsAdvanced] = "Reportes avanzados",
        [CfsFeatures.AiInsights] = "AI Insights",
        [CfsFeatures.AiAssistantBasic] = "AI Assistant básico",
        [CfsFeatures.AiAdvisor] = "AI Advisor add-on",
        [CfsFeatures.AiMonthlyReview] = "AI Monthly Review add-on",
        [CfsFeatures.AiAnomalyDetection] = "AI Anomaly Detection add-on",
        [CfsFeatures.MultiChurch] = "Multi-iglesia",
        [CfsFeatures.Audit] = "Auditoría",
        [CfsFeatures.CheckApprovals] = "Aprobación de cheques",
        [CfsFeatures.Documents] = "Documentos adjuntos",
        [CfsFeatures.ExcelExport] = "Integración Excel automatizado",
        [CfsFeatures.DonationStatements] = "Declaración de donativos",
    };

    public async Task<TenantSubscription> GetCurrentAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var tenantId = ReadIntClaim(user, "TenantId") ?? 1;
        var tenantName = user.FindFirst("TenantName")?.Value ?? "Iglesia Cristiana Pentecostes Inc";

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var (planKey, isFounder) = await LoadCurrentPlanAsync(connection, tenantId, cancellationToken);
        var planFeatures = await LoadPlanFeaturesAsync(connection, planKey, cancellationToken);
        var overrides = await LoadOverridesAsync(connection, tenantId, cancellationToken);

        var enabled = isFounder
            ? FeatureNames.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : planFeatures;

        foreach (var (featureKey, isEnabled) in overrides)
        {
            if (isEnabled)
            {
                enabled.Add(featureKey);
            }
            else
            {
                enabled.Remove(featureKey);
            }
        }

        var features = FeatureNames
            .Select(feature => new SubscriptionFeature(feature.Key, feature.Value, enabled.Contains(feature.Key)))
            .ToList();

        return new TenantSubscription(
            tenantId,
            tenantName,
            planKey,
            GetPlanName(planKey),
            BillingRequired: !isFounder,
            IsFounderAccount: isFounder,
            features);
    }

    public async Task<bool> HasFeatureAsync(ClaimsPrincipal user, string featureKey, CancellationToken cancellationToken = default)
    {
        var subscription = await GetCurrentAsync(user, cancellationToken);
        return subscription.HasFeature(featureKey);
    }

    private static async Task<(string PlanKey, bool IsFounder)> LoadCurrentPlanAsync(
        SqlConnection connection, int tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP 1 PlanKey, IsFounderAccount
            FROM dbo.TenantSubscriptions
            WHERE ID_Tenant_FK = @tenantId
            ORDER BY StartedAt DESC;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@tenantId", SqlDbType.Int).Value = tenantId;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return (reader.GetString(0), reader.GetBoolean(1));
        }

        return (CfsPlans.Basic, false);
    }

    private static async Task<HashSet<string>> LoadPlanFeaturesAsync(
        SqlConnection connection, string planKey, CancellationToken cancellationToken)
    {
        const string sql = "SELECT FeatureKey FROM dbo.PlanFeatures WHERE PlanKey = @planKey AND Enabled = 1;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@planKey", SqlDbType.NVarChar, 50).Value = planKey;

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    private static async Task<List<(string FeatureKey, bool Enabled)>> LoadOverridesAsync(
        SqlConnection connection, int tenantId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT FeatureKey, Enabled FROM dbo.TenantFeatureOverrides WHERE ID_Tenant_FK = @tenantId;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@tenantId", SqlDbType.Int).Value = tenantId;

        var result = new List<(string FeatureKey, bool Enabled)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add((reader.GetString(0), reader.GetBoolean(1)));
        }

        return result;
    }

    private static int? ReadIntClaim(ClaimsPrincipal user, string claimType) =>
        int.TryParse(user.FindFirst(claimType)?.Value, out var value) ? value : null;

    private static string GetPlanName(string planKey) =>
        planKey.ToLowerInvariant() switch
        {
            CfsPlans.Basic => "Basic",
            CfsPlans.Standard => "Standard",
            CfsPlans.Pro => "Pro",
            CfsPlans.MultiChurch => "Multi-Iglesia",
            _ => "Founder"
        };
}
