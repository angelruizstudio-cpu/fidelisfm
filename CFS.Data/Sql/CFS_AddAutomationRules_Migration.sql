/*
    Fidelis Financial Management - "Automatizaciones" feature
    -----------------------------------------------------------------
    Creates the tenant-scoped recurring-transaction rules table used by the
    new Automatizaciones page. Each rule generates a real Ingreso/Egreso row
    in dbo.Transacciones when it is due, then advances NextRunDate.

    Safe to re-run: guarded with IF OBJECT_ID(...) IS NULL.
*/

IF OBJECT_ID('dbo.CFS_AutomationRules', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CFS_AutomationRules
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CFS_AutomationRules PRIMARY KEY,
        ID_Tenant_FK INT NOT NULL,
        Name NVARCHAR(150) NOT NULL,
        TransactionType NVARCHAR(20) NOT NULL,
        AccountId INT NOT NULL,
        SubcategoryId INT NOT NULL,
        Amount DECIMAL(18, 2) NOT NULL,
        Frequency NVARCHAR(20) NOT NULL,
        NextRunDate DATE NOT NULL,
        Description NVARCHAR(300) NULL,
        Active BIT NOT NULL CONSTRAINT DF_CFS_AutomationRules_Active DEFAULT 1,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_CFS_AutomationRules_CreatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_CFS_AutomationRules_Tenant_NextRun ON dbo.CFS_AutomationRules(ID_Tenant_FK, NextRunDate);
END;
