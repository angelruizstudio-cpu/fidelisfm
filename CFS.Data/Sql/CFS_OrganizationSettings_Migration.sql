-- ============================================================
-- CFS Organization Settings Migration
-- Stores per-tenant organization type and optional custom UI labels,
-- so non-church organizations (nonprofits, ministries, schools, etc.)
-- can see terminology relevant to them instead of church-specific wording.
-- This table is OPTIONAL per tenant: absence of a row means "use Church
-- defaults," which preserves current behavior for all existing tenants.
-- Idempotent: safe to run multiple times.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrganizationSettings' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.OrganizationSettings
    (
        ID_Tenant_FK INT NOT NULL,
        OrganizationType NVARCHAR(50) NOT NULL DEFAULT 'Church',
        DisplayName NVARCHAR(200) NULL,
        ContactsLabel NVARCHAR(100) NULL,
        IncomeLabel NVARCHAR(100) NULL,
        ContributionsLabel NVARCHAR(100) NULL,
        DepartmentsLabel NVARCHAR(100) NULL,
        FundsLabel NVARCHAR(100) NULL,
        ReportsLabel NVARCHAR(100) NULL,
        LeadershipReportLabel NVARCHAR(100) NULL,
        PrimaryOfficerLabel NVARCHAR(100) NULL,
        SecondaryOfficerLabel NVARCHAR(100) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_OrganizationSettings PRIMARY KEY (ID_Tenant_FK)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrganizationSettings_Tenant'
)
BEGIN
    ALTER TABLE dbo.OrganizationSettings
        ADD CONSTRAINT FK_OrganizationSettings_Tenant
        FOREIGN KEY (ID_Tenant_FK) REFERENCES dbo.Tenants(ID_Tenant);
END
GO
