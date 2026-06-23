/*
    Fidelis Financial Management - add "Analisis de tendencias" and "Automatizaciones" features
    -----------------------------------------------------------------------------------------------
    Backfills the new 'reports.trend_analysis' and 'automation.recurring' feature keys into
    dbo.PlanFeatures for already-provisioned databases. Founder, Pro and Multi-Iglesia get both
    features enabled; Basic and Standard do not.

    Safe to re-run: every insert is guarded with NOT EXISTS.
*/

INSERT INTO dbo.PlanFeatures (PlanKey, FeatureKey, Enabled)
SELECT 'founder', 'reports.trend_analysis', 1
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PlanFeatures WHERE PlanKey = 'founder' AND FeatureKey = 'reports.trend_analysis'
);

INSERT INTO dbo.PlanFeatures (PlanKey, FeatureKey, Enabled)
SELECT 'founder', 'automation.recurring', 1
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PlanFeatures WHERE PlanKey = 'founder' AND FeatureKey = 'automation.recurring'
);

INSERT INTO dbo.PlanFeatures (PlanKey, FeatureKey, Enabled)
SELECT 'pro', 'reports.trend_analysis', 1
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PlanFeatures WHERE PlanKey = 'pro' AND FeatureKey = 'reports.trend_analysis'
);

INSERT INTO dbo.PlanFeatures (PlanKey, FeatureKey, Enabled)
SELECT 'pro', 'automation.recurring', 1
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PlanFeatures WHERE PlanKey = 'pro' AND FeatureKey = 'automation.recurring'
);

INSERT INTO dbo.PlanFeatures (PlanKey, FeatureKey, Enabled)
SELECT 'multi_church', 'reports.trend_analysis', 1
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PlanFeatures WHERE PlanKey = 'multi_church' AND FeatureKey = 'reports.trend_analysis'
);

INSERT INTO dbo.PlanFeatures (PlanKey, FeatureKey, Enabled)
SELECT 'multi_church', 'automation.recurring', 1
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PlanFeatures WHERE PlanKey = 'multi_church' AND FeatureKey = 'automation.recurring'
);
