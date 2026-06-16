using System.Security.Claims;
using CFS.Core.Models;
using CFS.Core.Services;

namespace CFS.Data;

public sealed class StaticSubscriptionService : ISubscriptionService
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
        [CfsFeatures.Documents] = "Documentos adjuntos"
    };

    private static readonly IReadOnlyDictionary<string, HashSet<string>> PlanFeatures = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
    {
        [CfsPlans.Basic] =
        [
            CfsFeatures.Dashboard,
            CfsFeatures.Income,
            CfsFeatures.Expenses,
            CfsFeatures.Deposits,
            CfsFeatures.ReportsBasic
        ],
        [CfsPlans.Standard] =
        [
            CfsFeatures.Dashboard,
            CfsFeatures.Income,
            CfsFeatures.Expenses,
            CfsFeatures.Deposits,
            CfsFeatures.Reconciliation,
            CfsFeatures.ReportsBasic,
            CfsFeatures.ReportsAdvanced
        ],
        [CfsPlans.Pro] =
        [
            CfsFeatures.Dashboard,
            CfsFeatures.Income,
            CfsFeatures.Expenses,
            CfsFeatures.Deposits,
            CfsFeatures.Reconciliation,
            CfsFeatures.ReportsBasic,
            CfsFeatures.ReportsAdvanced,
            CfsFeatures.AiInsights,
            CfsFeatures.AiAssistantBasic,
            CfsFeatures.Audit,
            CfsFeatures.CheckApprovals,
            CfsFeatures.Documents
        ],
        [CfsPlans.MultiChurch] =
        [
            CfsFeatures.Dashboard,
            CfsFeatures.Income,
            CfsFeatures.Expenses,
            CfsFeatures.Deposits,
            CfsFeatures.Reconciliation,
            CfsFeatures.ReportsBasic,
            CfsFeatures.ReportsAdvanced,
            CfsFeatures.AiInsights,
            CfsFeatures.AiAssistantBasic,
            CfsFeatures.AiAdvisor,
            CfsFeatures.AiMonthlyReview,
            CfsFeatures.AiAnomalyDetection,
            CfsFeatures.Audit,
            CfsFeatures.CheckApprovals,
            CfsFeatures.Documents,
            CfsFeatures.MultiChurch
        ]
    };

    public Task<TenantSubscription> GetCurrentAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ReadIntClaim(user, "TenantId") ?? 1;
        var tenantName = user.FindFirst("TenantName")?.Value ?? "Iglesia Cristiana Pentecostes Inc";
        var planKey = user.FindFirst("PlanKey")?.Value ?? CfsPlans.Founder;
        var isFounder = planKey.Equals(CfsPlans.Founder, StringComparison.OrdinalIgnoreCase);

        var enabledFeatures = isFounder
            ? FeatureNames.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : PlanFeatures.GetValueOrDefault(planKey, PlanFeatures[CfsPlans.Basic]);

        var features = FeatureNames
            .Select(feature => new SubscriptionFeature(
                feature.Key,
                feature.Value,
                enabledFeatures.Contains(feature.Key)))
            .ToList();

        return Task.FromResult(new TenantSubscription(
            tenantId,
            tenantName,
            planKey,
            GetPlanName(planKey),
            BillingRequired: !isFounder,
            IsFounderAccount: isFounder,
            features));
    }

    public async Task<bool> HasFeatureAsync(
        ClaimsPrincipal user,
        string featureKey,
        CancellationToken cancellationToken = default)
    {
        var subscription = await GetCurrentAsync(user, cancellationToken);
        return subscription.HasFeature(featureKey);
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
