/*
    Fidelis Financial Management - "Presupuesto" feature
    -----------------------------------------------------------------
    Creates the tenant-scoped annual budget table used by the new
    Presupuesto page. One row per category per year: the planned annual
    amount. Actuals are NOT stored here — they are computed on demand from
    dbo.Transacciones (same source as the Profit & Loss report).

    Safe to re-run: guarded with IF OBJECT_ID(...) IS NULL.
*/

IF OBJECT_ID('dbo.CFS_Presupuestos', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CFS_Presupuestos
    (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CFS_Presupuestos PRIMARY KEY,
        ID_Tenant_FK    INT NOT NULL,
        ID_Categoria_FK INT NOT NULL,
        Anio            INT NOT NULL,
        MontoAnual      DECIMAL(18, 2) NOT NULL CONSTRAINT DF_CFS_Presupuestos_MontoAnual DEFAULT 0,
        CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_CFS_Presupuestos_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2(0) NULL
    );

    -- One budget row per category per year, per tenant.
    CREATE UNIQUE INDEX UX_CFS_Presupuestos_Tenant_Cat_Anio
        ON dbo.CFS_Presupuestos(ID_Tenant_FK, ID_Categoria_FK, Anio);
END;
