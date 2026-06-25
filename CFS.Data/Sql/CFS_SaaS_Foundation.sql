/*
    CFS SaaS foundation
    -------------------
    Script revisable para habilitar iglesias/tenants, planes y feature flags.
    No ejecutar sin validar nombres, datos existentes y estrategia multi-tenant.
*/

IF OBJECT_ID('dbo.TenantFeatureOverrides', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantFeatureOverrides
    (
        ID_TenantFeatureOverride INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TenantFeatureOverrides PRIMARY KEY,
        ID_Tenant_FK INT NOT NULL,
        FeatureKey NVARCHAR(100) NOT NULL,
        Enabled BIT NOT NULL,
        UpdatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_TenantFeatureOverrides_UpdatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedBy NVARCHAR(100) NULL
    );
END;

IF OBJECT_ID('dbo.PlanFeatures', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlanFeatures
    (
        ID_PlanFeature INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PlanFeatures PRIMARY KEY,
        PlanKey NVARCHAR(50) NOT NULL,
        FeatureKey NVARCHAR(100) NOT NULL,
        Enabled BIT NOT NULL CONSTRAINT DF_PlanFeatures_Enabled DEFAULT 1
    );
END;

IF OBJECT_ID('dbo.TenantSubscriptions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantSubscriptions
    (
        ID_TenantSubscription INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TenantSubscriptions PRIMARY KEY,
        ID_Tenant_FK INT NOT NULL,
        PlanKey NVARCHAR(50) NOT NULL,
        BillingRequired BIT NOT NULL CONSTRAINT DF_TenantSubscriptions_BillingRequired DEFAULT 1,
        IsFounderAccount BIT NOT NULL CONSTRAINT DF_TenantSubscriptions_IsFounderAccount DEFAULT 0,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_TenantSubscriptions_Status DEFAULT 'Active',
        StartedAt DATETIME2(0) NOT NULL CONSTRAINT DF_TenantSubscriptions_StartedAt DEFAULT SYSUTCDATETIME(),
        CurrentPeriodEndsAt DATETIME2(0) NULL
    );
END;

IF OBJECT_ID('dbo.Planes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Planes
    (
        PlanKey NVARCHAR(50) NOT NULL CONSTRAINT PK_Planes PRIMARY KEY,
        NombrePlan NVARCHAR(100) NOT NULL,
        PrecioMensual DECIMAL(10,2) NOT NULL CONSTRAINT DF_Planes_PrecioMensual DEFAULT 0,
        Activo BIT NOT NULL CONSTRAINT DF_Planes_Activo DEFAULT 1
    );
END;

IF OBJECT_ID('dbo.Tenants', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tenants
    (
        ID_Tenant INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY,
        NombreTenant NVARCHAR(150) NOT NULL,
        Slug NVARCHAR(100) NOT NULL,
        Activo BIT NOT NULL CONSTRAINT DF_Tenants_Activo DEFAULT 1,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Tenants_CreatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX UX_Tenants_Slug ON dbo.Tenants(Slug);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Planes WHERE PlanKey = 'founder')
    INSERT INTO dbo.Planes (PlanKey, NombrePlan, PrecioMensual) VALUES ('founder', 'Founder', 0);

IF NOT EXISTS (SELECT 1 FROM dbo.Planes WHERE PlanKey = 'basic')
    INSERT INTO dbo.Planes (PlanKey, NombrePlan, PrecioMensual) VALUES ('basic', 'Basic', 0);

IF NOT EXISTS (SELECT 1 FROM dbo.Planes WHERE PlanKey = 'standard')
    INSERT INTO dbo.Planes (PlanKey, NombrePlan, PrecioMensual) VALUES ('standard', 'Standard', 0);

IF NOT EXISTS (SELECT 1 FROM dbo.Planes WHERE PlanKey = 'pro')
    INSERT INTO dbo.Planes (PlanKey, NombrePlan, PrecioMensual) VALUES ('pro', 'Pro', 0);

IF NOT EXISTS (SELECT 1 FROM dbo.Planes WHERE PlanKey = 'multi_church')
    INSERT INTO dbo.Planes (PlanKey, NombrePlan, PrecioMensual) VALUES ('multi_church', 'Multi-Iglesia', 0);

IF NOT EXISTS (SELECT 1 FROM dbo.Tenants WHERE Slug = 'icp-founder')
BEGIN
    INSERT INTO dbo.Tenants (NombreTenant, Slug)
    VALUES ('Iglesia Cristiana Pentecostes Inc', 'icp-founder');
END;

DECLARE @FounderTenantId INT = (SELECT ID_Tenant FROM dbo.Tenants WHERE Slug = 'icp-founder');

IF NOT EXISTS (SELECT 1 FROM dbo.TenantSubscriptions WHERE ID_Tenant_FK = @FounderTenantId)
BEGIN
    INSERT INTO dbo.TenantSubscriptions
        (ID_Tenant_FK, PlanKey, BillingRequired, IsFounderAccount, Status)
    VALUES
        (@FounderTenantId, 'founder', 0, 1, 'Active');
END;

DECLARE @Features TABLE (FeatureKey NVARCHAR(100) NOT NULL);
INSERT INTO @Features (FeatureKey)
VALUES
    ('dashboard'),
    ('income'),
    ('expenses'),
    ('deposits'),
    ('reconciliation'),
    ('reports.basic'),
    ('reports.advanced'),
    ('ai.insights'),
    ('ai.assistant.basic'),
    ('ai.advisor'),
    ('ai.monthly_review'),
    ('ai.anomaly_detection'),
    ('multi_church'),
    ('audit'),
    ('reports.trend_analysis'),
    ('automation.recurring'),
    ('checks'),
    ('checks.approvals'),
    ('documents');

INSERT INTO dbo.PlanFeatures (PlanKey, FeatureKey, Enabled)
SELECT 'founder', FeatureKey, 1
FROM @Features f
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PlanFeatures pf WHERE pf.PlanKey = 'founder' AND pf.FeatureKey = f.FeatureKey
);

INSERT INTO dbo.PlanFeatures (PlanKey, FeatureKey, Enabled)
SELECT 'basic', FeatureKey, 1
FROM @Features f
WHERE f.FeatureKey IN ('dashboard', 'income', 'expenses', 'deposits', 'reports.basic')
  AND NOT EXISTS (
      SELECT 1 FROM dbo.PlanFeatures pf WHERE pf.PlanKey = 'basic' AND pf.FeatureKey = f.FeatureKey
  );

INSERT INTO dbo.PlanFeatures (PlanKey, FeatureKey, Enabled)
SELECT 'standard', FeatureKey, 1
FROM @Features f
WHERE f.FeatureKey IN ('dashboard', 'income', 'expenses', 'checks', 'deposits', 'reconciliation', 'reports.basic', 'reports.advanced')
  AND NOT EXISTS (
      SELECT 1 FROM dbo.PlanFeatures pf WHERE pf.PlanKey = 'standard' AND pf.FeatureKey = f.FeatureKey
  );

INSERT INTO dbo.PlanFeatures (PlanKey, FeatureKey, Enabled)
SELECT 'pro', FeatureKey, 1
FROM @Features f
WHERE f.FeatureKey IN ('dashboard', 'income', 'expenses', 'checks', 'deposits', 'reconciliation', 'reports.basic', 'reports.advanced', 'ai.insights', 'ai.assistant.basic', 'audit', 'reports.trend_analysis', 'automation.recurring', 'checks.approvals', 'documents')
  AND NOT EXISTS (
      SELECT 1 FROM dbo.PlanFeatures pf WHERE pf.PlanKey = 'pro' AND pf.FeatureKey = f.FeatureKey
  );

INSERT INTO dbo.PlanFeatures (PlanKey, FeatureKey, Enabled)
SELECT 'multi_church', FeatureKey, 1
FROM @Features f
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PlanFeatures pf WHERE pf.PlanKey = 'multi_church' AND pf.FeatureKey = f.FeatureKey
);
