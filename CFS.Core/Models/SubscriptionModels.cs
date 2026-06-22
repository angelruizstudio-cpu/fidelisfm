namespace CFS.Core.Models;

public static class CfsFeatures
{
    public const string Dashboard = "dashboard";
    public const string Income = "income";
    public const string Expenses = "expenses";
    public const string Deposits = "deposits";
    public const string Reconciliation = "reconciliation";
    public const string ReportsBasic = "reports.basic";
    public const string ReportsAdvanced = "reports.advanced";
    public const string AiInsights = "ai.insights";
    public const string AiAssistantBasic = "ai.assistant.basic";
    public const string AiAdvisor = "ai.advisor";
    public const string AiMonthlyReview = "ai.monthly_review";
    public const string AiAnomalyDetection = "ai.anomaly_detection";
    public const string MultiChurch = "multi_church";
    public const string Audit = "audit";
    public const string Checks = "checks";
    public const string CheckApprovals = "checks.approvals";
    public const string Documents = "documents";
    public const string ExcelExport = "integrations.excel_export";
    public const string DonationStatements = "reports.donation_statements";
}

public static class CfsAddons
{
    public const string AiBundle = "addon.ai_bundle";
    public const string ExcelExport = "addon.excel_export";
    public const string DonationStatements = "addon.donation_statements";

    public static readonly IReadOnlyDictionary<string, string[]> FeatureKeysByAddon = new Dictionary<string, string[]>
    {
        [AiBundle] = [CfsFeatures.AiInsights, CfsFeatures.AiAdvisor],
        [ExcelExport] = [CfsFeatures.ExcelExport],
        [DonationStatements] = [CfsFeatures.DonationStatements],
    };
}

public static class CfsPlans
{
    public const string Founder = "founder";
    public const string Basic = "basic";
    public const string Standard = "standard";
    public const string Pro = "pro";
    public const string MultiChurch = "multi_church";
}

public static class CfsAiQuotas
{
    /// <summary>
    /// Maximum number of AI assistant questions a tenant may ask per calendar month, by plan.
    /// Keeps OpenAI API costs predictable per plan tier.
    /// </summary>
    public static int GetMonthlyLimit(string? planKey) => planKey?.ToLowerInvariant() switch
    {
        CfsPlans.Founder => int.MaxValue,
        CfsPlans.Basic => 10,
        CfsPlans.Standard => 50,
        CfsPlans.Pro => 200,
        CfsPlans.MultiChurch => 500,
        _ => 10
    };
}

public static class CfsUserLimits
{
    public static int GetMaxUsers(string? planKey) => planKey?.ToLowerInvariant() switch
    {
        CfsPlans.Founder => int.MaxValue,
        CfsPlans.Basic => 1,
        CfsPlans.Standard => 3,
        CfsPlans.Pro => 5,
        CfsPlans.MultiChurch => int.MaxValue,
        _ => 1
    };
}

public sealed record SubscriptionFeature(string Key, string Name, bool Enabled);

public sealed record TenantSubscription(
    int TenantId,
    string TenantName,
    string PlanKey,
    string PlanName,
    bool BillingRequired,
    bool IsFounderAccount,
    IReadOnlyList<SubscriptionFeature> Features)
{
    public bool HasFeature(string featureKey) =>
        Features.Any(feature => feature.Key.Equals(featureKey, StringComparison.OrdinalIgnoreCase) && feature.Enabled);
}
