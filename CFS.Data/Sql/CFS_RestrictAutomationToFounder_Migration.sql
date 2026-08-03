/*
    Fidelis Financial Management - restrict "Automatizaciones" to Founder only (for now)
    -----------------------------------------------------------------------------------------------
    Automations (automation.recurring) were previously enabled for the Pro and Multi-Iglesia plans.
    For now they are reserved for the Founder account only. This disables the feature for the
    'pro' and 'multi_church' plans in dbo.PlanFeatures.

    - Founder is unaffected: founder accounts receive every feature in code, regardless of this table.
    - Reversible: we set Enabled = 0 (not DELETE), so re-enabling later is a one-line UPDATE back to 1.
    - Safe to re-run.

    Run this against the PRODUCTION database. Without it, existing Pro / Multi-Iglesia tenants would
    keep automation access even though the code and pricing page no longer offer it.
*/

UPDATE dbo.PlanFeatures
SET Enabled = 0
WHERE FeatureKey = 'automation.recurring'
  AND PlanKey IN ('pro', 'multi_church')
  AND Enabled = 1;

-- To re-enable in the future:
-- UPDATE dbo.PlanFeatures SET Enabled = 1
-- WHERE FeatureKey = 'automation.recurring' AND PlanKey IN ('pro', 'multi_church');
