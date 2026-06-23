/*
    Fidelis Financial Management - "Auditoria avanzada" feature
    -----------------------------------------------------------------
    Creates a dedicated, tenant-scoped audit trail table used by the new
    Auditoria page. Populated going forward by Ingresos, Egresos, Depositos
    and Cheques whenever an entry is created, edited, voided or printed.

    Safe to re-run: guarded with IF OBJECT_ID(...) IS NULL.
*/

IF OBJECT_ID('dbo.CFS_AuditLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CFS_AuditLog
    (
        ID_AuditLog INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CFS_AuditLog PRIMARY KEY,
        ID_Tenant_FK INT NOT NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_CFS_AuditLog_CreatedAt DEFAULT SYSUTCDATETIME(),
        UserName NVARCHAR(150) NOT NULL,
        Action NVARCHAR(50) NOT NULL,
        EntityType NVARCHAR(50) NOT NULL,
        EntityReference NVARCHAR(100) NULL,
        Detail NVARCHAR(500) NOT NULL
    );

    CREATE INDEX IX_CFS_AuditLog_Tenant_CreatedAt ON dbo.CFS_AuditLog(ID_Tenant_FK, CreatedAt DESC);
END;
