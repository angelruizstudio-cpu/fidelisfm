/*
    Fidelis Financial Management - external invoicing API
    -------------------------------------------------------
    Lets a tenant's external system (e.g. a partner organization's own
    software) call POST /api/invoices to have Fidelis create and send a
    Stripe invoice to a third-party recipient, authenticated with a
    per-tenant API key. Additive only.
*/

IF OBJECT_ID('dbo.TenantApiKeys', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantApiKeys
    (
        ID_TenantApiKey INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TenantApiKeys PRIMARY KEY,
        ID_Tenant_FK INT NOT NULL,
        Label NVARCHAR(100) NULL,
        ApiKeyHash NVARCHAR(128) NOT NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_TenantApiKeys_CreatedAt DEFAULT SYSUTCDATETIME(),
        RevokedAt DATETIME2(0) NULL
    );

    CREATE UNIQUE INDEX UX_TenantApiKeys_ApiKeyHash ON dbo.TenantApiKeys(ApiKeyHash);
END;

IF OBJECT_ID('dbo.ExternalInvoiceRequests', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExternalInvoiceRequests
    (
        ID_ExternalInvoiceRequest INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExternalInvoiceRequests PRIMARY KEY,
        ID_Tenant_FK INT NOT NULL,
        RecipientName NVARCHAR(150) NOT NULL,
        RecipientEmail NVARCHAR(256) NOT NULL,
        AmountCents INT NOT NULL,
        Currency NVARCHAR(3) NOT NULL CONSTRAINT DF_ExternalInvoiceRequests_Currency DEFAULT 'usd',
        Description NVARCHAR(500) NOT NULL,
        ExternalReference NVARCHAR(100) NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_ExternalInvoiceRequests_Status DEFAULT 'Pending',
        StripeCustomerId NVARCHAR(100) NULL,
        StripeInvoiceId NVARCHAR(100) NULL,
        StripeHostedInvoiceUrl NVARCHAR(500) NULL,
        ErrorMessage NVARCHAR(1000) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_ExternalInvoiceRequests_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_ExternalInvoiceRequests_UpdatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX UX_ExternalInvoiceRequests_Tenant_ExternalReference
        ON dbo.ExternalInvoiceRequests(ID_Tenant_FK, ExternalReference)
        WHERE ExternalReference IS NOT NULL;
END;
