/*
    Adds Stripe identifiers to dbo.TenantSubscriptions so add-on purchases and plan
    upgrades can later manage (and cancel) the underlying Stripe subscription.
    Additive only.

    NOTE: Run this whole script at once (GO batch separators ensure each
    ALTER TABLE is compiled before later statements reference the new column).
*/

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TenantSubscriptions') AND name = 'StripeCustomerId'
)
    ALTER TABLE dbo.TenantSubscriptions ADD StripeCustomerId NVARCHAR(100) NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TenantSubscriptions') AND name = 'StripeSubscriptionId'
)
    ALTER TABLE dbo.TenantSubscriptions ADD StripeSubscriptionId NVARCHAR(100) NULL;
GO
