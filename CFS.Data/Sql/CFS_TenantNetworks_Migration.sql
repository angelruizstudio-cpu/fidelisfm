/*
    Fidelis Financial Management - multi-church networks (Fase 1: esquema base)
    -----------------------------------------------------------------------------
    Lets several churches (Tenants) be grouped under one paying "network"
    (e.g. a council or denomination) for the Multi-Iglesia plan, with a single
    Stripe subscription and users who can access more than one church.

    Named "TenantNetworks" (not "Organizations") to avoid colliding with the
    existing dbo.OrganizationSettings table, which configures per-tenant
    terminology (Church/Ministry/etc.) and is unrelated to this feature.

    Additive only. Does not change behavior for existing single-church tenants:
    ID_TenantNetwork_FK is nullable and defaults to NULL.
*/

IF OBJECT_ID('dbo.TenantNetworks', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantNetworks
    (
        ID_TenantNetwork INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TenantNetworks PRIMARY KEY,
        Name NVARCHAR(150) NOT NULL,
        Slug NVARCHAR(100) NOT NULL,
        MaxChurches INT NOT NULL CONSTRAINT DF_TenantNetworks_MaxChurches DEFAULT 10,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_TenantNetworks_CreatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX UX_TenantNetworks_Slug ON dbo.TenantNetworks(Slug);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Tenants') AND name = 'ID_TenantNetwork_FK'
)
    ALTER TABLE dbo.Tenants ADD ID_TenantNetwork_FK INT NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Tenants_TenantNetwork'
)
BEGIN
    ALTER TABLE dbo.Tenants
        ADD CONSTRAINT FK_Tenants_TenantNetwork
        FOREIGN KEY (ID_TenantNetwork_FK) REFERENCES dbo.TenantNetworks(ID_TenantNetwork);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tenants_TenantNetwork' AND object_id = OBJECT_ID('dbo.Tenants'))
    CREATE INDEX IX_Tenants_TenantNetwork ON dbo.Tenants(ID_TenantNetwork_FK) WHERE ID_TenantNetwork_FK IS NOT NULL;
GO

IF OBJECT_ID('dbo.TenantNetworkSubscriptions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantNetworkSubscriptions
    (
        ID_TenantNetworkSubscription INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TenantNetworkSubscriptions PRIMARY KEY,
        ID_TenantNetwork_FK INT NOT NULL,
        StripeCustomerId NVARCHAR(100) NULL,
        StripeSubscriptionId NVARCHAR(100) NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_TenantNetworkSubscriptions_Status DEFAULT 'Active',
        StartedAt DATETIME2(0) NOT NULL CONSTRAINT DF_TenantNetworkSubscriptions_StartedAt DEFAULT SYSUTCDATETIME(),
        CurrentPeriodEndsAt DATETIME2(0) NULL,
        CONSTRAINT FK_TenantNetworkSubscriptions_TenantNetwork
            FOREIGN KEY (ID_TenantNetwork_FK) REFERENCES dbo.TenantNetworks(ID_TenantNetwork)
    );

    CREATE INDEX IX_TenantNetworkSubscriptions_TenantNetwork ON dbo.TenantNetworkSubscriptions(ID_TenantNetwork_FK);
END;

IF OBJECT_ID('dbo.UserTenantAccess', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserTenantAccess
    (
        ID_UserTenantAccess INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserTenantAccess PRIMARY KEY,
        ID_User_FK INT NOT NULL,
        ID_Tenant_FK INT NOT NULL,
        RoleKeys NVARCHAR(200) NOT NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_UserTenantAccess_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_UserTenantAccess_Tenant
            FOREIGN KEY (ID_Tenant_FK) REFERENCES dbo.Tenants(ID_Tenant),
        CONSTRAINT FK_UserTenantAccess_Usuario
            FOREIGN KEY (ID_User_FK) REFERENCES dbo.Usuarios(ID_Usuario)
    );

    CREATE UNIQUE INDEX UX_UserTenantAccess_User_Tenant ON dbo.UserTenantAccess(ID_User_FK, ID_Tenant_FK);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Planes WHERE PlanKey = 'multi_church')
    INSERT INTO dbo.Planes (PlanKey, NombrePlan, PrecioMensual) VALUES ('multi_church', 'Multi-Iglesia', 250);
ELSE
    UPDATE dbo.Planes SET PrecioMensual = 250 WHERE PlanKey = 'multi_church';
