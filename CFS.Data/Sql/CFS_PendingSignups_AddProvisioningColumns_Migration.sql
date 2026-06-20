/*
    Extends dbo.PendingSignups (created by CFS_PendingSignups_Migration.sql) with the
    columns needed to auto-provision a tenant/user after a successful Stripe payment.
    Additive only — no existing columns are changed.

    NOTE: Run this whole script at once (GO batch separators ensure each
    ALTER TABLE is compiled before later statements reference the new column).
*/

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PendingSignups') AND name = 'PasswordSalt'
)
    ALTER TABLE dbo.PendingSignups ADD PasswordSalt VARBINARY(64) NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PendingSignups') AND name = 'PasswordHash'
)
    ALTER TABLE dbo.PendingSignups ADD PasswordHash VARBINARY(64) NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PendingSignups') AND name = 'ProvisionedTenantId'
)
    ALTER TABLE dbo.PendingSignups ADD ProvisionedTenantId INT NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PendingSignups') AND name = 'ProvisionedAt'
)
    ALTER TABLE dbo.PendingSignups ADD ProvisionedAt DATETIME2(0) NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PendingSignups_ProvisionedTenant'
)
BEGIN
    ALTER TABLE dbo.PendingSignups
        ADD CONSTRAINT FK_PendingSignups_ProvisionedTenant
        FOREIGN KEY (ProvisionedTenantId) REFERENCES dbo.Tenants(ID_Tenant);
END;
GO
