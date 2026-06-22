/*
    Fidelis Financial Management - split "Cheques" out of the Egresos feature
    -----------------------------------------------------------------------------
    Adds a dedicated 'checks' feature key so the Basic plan can keep Egresos
    without Cheques/Formato de cheques. Standard, Pro, Multi-Iglesia and Founder
    get 'checks' enabled; Basic does not.

    Safe to re-run: every insert is guarded with NOT EXISTS.
*/

INSERT INTO dbo.PlanFeatures (PlanKey, FeatureKey, Enabled)
SELECT 'founder', 'checks', 1
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PlanFeatures WHERE PlanKey = 'founder' AND FeatureKey = 'checks'
);

INSERT INTO dbo.PlanFeatures (PlanKey, FeatureKey, Enabled)
SELECT 'standard', 'checks', 1
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PlanFeatures WHERE PlanKey = 'standard' AND FeatureKey = 'checks'
);

INSERT INTO dbo.PlanFeatures (PlanKey, FeatureKey, Enabled)
SELECT 'pro', 'checks', 1
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PlanFeatures WHERE PlanKey = 'pro' AND FeatureKey = 'checks'
);

INSERT INTO dbo.PlanFeatures (PlanKey, FeatureKey, Enabled)
SELECT 'multi_church', 'checks', 1
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PlanFeatures WHERE PlanKey = 'multi_church' AND FeatureKey = 'checks'
);
