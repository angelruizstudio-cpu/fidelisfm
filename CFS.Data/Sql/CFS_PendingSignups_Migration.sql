/*
    Fidelis Financial Management - pending self-serve signups
    -----------------------------------------------------------
    Additive only. Records completed Stripe Checkout payments so they can be
    reviewed and manually provisioned into a tenant/user account. Does not
    touch dbo.Tenants, dbo.Usuarios, or any existing table.
*/

IF OBJECT_ID('dbo.PendingSignups', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PendingSignups
    (
        ID_PendingSignup INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PendingSignups PRIMARY KEY,
        OrganizationName NVARCHAR(150) NOT NULL,
        Email NVARCHAR(256) NOT NULL,
        Phone NVARCHAR(50) NULL,
        PlanKey NVARCHAR(50) NOT NULL,
        BillingCycle NVARCHAR(20) NOT NULL,
        StripeSessionId NVARCHAR(100) NOT NULL,
        StripeCustomerId NVARCHAR(100) NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_PendingSignups_Status DEFAULT 'PendingProvisioning',
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_PendingSignups_CreatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX UX_PendingSignups_StripeSessionId ON dbo.PendingSignups(StripeSessionId);
END;
