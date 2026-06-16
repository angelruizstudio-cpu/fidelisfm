-- ============================================================
-- CFS AI Usage Quota Migration
-- Tracks AI assistant requests per tenant per calendar month so
-- plan-based monthly quotas can be enforced before calling OpenAI.
-- Idempotent: safe to run multiple times.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AiUsageMonthly' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.AiUsageMonthly
    (
        ID_Tenant_FK INT NOT NULL,
        YearMonth CHAR(7) NOT NULL,  -- e.g. '2026-06'
        RequestCount INT NOT NULL DEFAULT 0,
        UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_AiUsageMonthly PRIMARY KEY (ID_Tenant_FK, YearMonth)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AiUsageMonthly_Tenant'
)
BEGIN
    ALTER TABLE dbo.AiUsageMonthly
        ADD CONSTRAINT FK_AiUsageMonthly_Tenant
        FOREIGN KEY (ID_Tenant_FK) REFERENCES dbo.Tenants(ID_Tenant);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AiUsageMonthly_Tenant' AND object_id = OBJECT_ID('dbo.AiUsageMonthly'))
    CREATE INDEX IX_AiUsageMonthly_Tenant ON dbo.AiUsageMonthly (ID_Tenant_FK);
GO
