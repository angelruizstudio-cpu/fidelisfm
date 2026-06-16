-- ============================================================
-- CFS Tenant Isolation Migration
-- Adds ID_Tenant_FK to all transactional tables.
-- Idempotent: safe to run multiple times.
-- ============================================================

-- 1. dbo.Transacciones
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Transacciones') AND name = 'ID_Tenant_FK'
)
BEGIN
    ALTER TABLE dbo.Transacciones
        ADD ID_Tenant_FK INT NOT NULL DEFAULT 1;
END
GO

-- 2. dbo.Depositos
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Depositos') AND name = 'ID_Tenant_FK'
)
BEGIN
    ALTER TABLE dbo.Depositos
        ADD ID_Tenant_FK INT NOT NULL DEFAULT 1;
END
GO

-- 3. dbo.CuentasBancarias
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.CuentasBancarias') AND name = 'ID_Tenant_FK'
)
BEGIN
    ALTER TABLE dbo.CuentasBancarias
        ADD ID_Tenant_FK INT NOT NULL DEFAULT 1;
END
GO

-- 4. dbo.Miembros
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Miembros') AND name = 'ID_Tenant_FK'
)
BEGIN
    ALTER TABLE dbo.Miembros
        ADD ID_Tenant_FK INT NOT NULL DEFAULT 1;
END
GO

-- 5. dbo.CFS_Cheques
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.CFS_Cheques') AND name = 'ID_Tenant_FK'
)
BEGIN
    ALTER TABLE dbo.CFS_Cheques
        ADD ID_Tenant_FK INT NOT NULL DEFAULT 1;
END
GO

-- 6. dbo.Conciliaciones
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Conciliaciones') AND name = 'ID_Tenant_FK'
)
BEGIN
    ALTER TABLE dbo.Conciliaciones
        ADD ID_Tenant_FK INT NOT NULL DEFAULT 1;
END
GO

-- 7. dbo.Categorias
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Categorias') AND name = 'ID_Tenant_FK'
)
BEGIN
    ALTER TABLE dbo.Categorias
        ADD ID_Tenant_FK INT NOT NULL DEFAULT 1;
END
GO

-- 8. dbo.Subcategorias
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Subcategorias') AND name = 'ID_Tenant_FK'
)
BEGIN
    ALTER TABLE dbo.Subcategorias
        ADD ID_Tenant_FK INT NOT NULL DEFAULT 1;
END
GO

-- 9. dbo.CFS_CheckPrintSettings
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.CFS_CheckPrintSettings') AND name = 'ID_Tenant_FK'
)
BEGIN
    ALTER TABLE dbo.CFS_CheckPrintSettings
        ADD ID_Tenant_FK INT NOT NULL DEFAULT 1;
END
GO

-- ============================================================
-- Foreign key constraints (referencing dbo.Tenants)
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Transacciones_Tenant'
)
BEGIN
    ALTER TABLE dbo.Transacciones
        ADD CONSTRAINT FK_Transacciones_Tenant
        FOREIGN KEY (ID_Tenant_FK) REFERENCES dbo.Tenants(ID_Tenant);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Depositos_Tenant'
)
BEGIN
    ALTER TABLE dbo.Depositos
        ADD CONSTRAINT FK_Depositos_Tenant
        FOREIGN KEY (ID_Tenant_FK) REFERENCES dbo.Tenants(ID_Tenant);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CuentasBancarias_Tenant'
)
BEGIN
    ALTER TABLE dbo.CuentasBancarias
        ADD CONSTRAINT FK_CuentasBancarias_Tenant
        FOREIGN KEY (ID_Tenant_FK) REFERENCES dbo.Tenants(ID_Tenant);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Miembros_Tenant'
)
BEGIN
    ALTER TABLE dbo.Miembros
        ADD CONSTRAINT FK_Miembros_Tenant
        FOREIGN KEY (ID_Tenant_FK) REFERENCES dbo.Tenants(ID_Tenant);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CFS_Cheques_Tenant'
)
BEGIN
    ALTER TABLE dbo.CFS_Cheques
        ADD CONSTRAINT FK_CFS_Cheques_Tenant
        FOREIGN KEY (ID_Tenant_FK) REFERENCES dbo.Tenants(ID_Tenant);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Conciliaciones_Tenant'
)
BEGIN
    ALTER TABLE dbo.Conciliaciones
        ADD CONSTRAINT FK_Conciliaciones_Tenant
        FOREIGN KEY (ID_Tenant_FK) REFERENCES dbo.Tenants(ID_Tenant);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Categorias_Tenant'
)
BEGIN
    ALTER TABLE dbo.Categorias
        ADD CONSTRAINT FK_Categorias_Tenant
        FOREIGN KEY (ID_Tenant_FK) REFERENCES dbo.Tenants(ID_Tenant);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Subcategorias_Tenant'
)
BEGIN
    ALTER TABLE dbo.Subcategorias
        ADD CONSTRAINT FK_Subcategorias_Tenant
        FOREIGN KEY (ID_Tenant_FK) REFERENCES dbo.Tenants(ID_Tenant);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CFS_CheckPrintSettings_Tenant'
)
BEGIN
    ALTER TABLE dbo.CFS_CheckPrintSettings
        ADD CONSTRAINT FK_CFS_CheckPrintSettings_Tenant
        FOREIGN KEY (ID_Tenant_FK) REFERENCES dbo.Tenants(ID_Tenant);
END
GO

-- ============================================================
-- Performance indexes on ID_Tenant_FK
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Transacciones_Tenant' AND object_id = OBJECT_ID('dbo.Transacciones'))
    CREATE INDEX IX_Transacciones_Tenant ON dbo.Transacciones (ID_Tenant_FK);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Depositos_Tenant' AND object_id = OBJECT_ID('dbo.Depositos'))
    CREATE INDEX IX_Depositos_Tenant ON dbo.Depositos (ID_Tenant_FK);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CuentasBancarias_Tenant' AND object_id = OBJECT_ID('dbo.CuentasBancarias'))
    CREATE INDEX IX_CuentasBancarias_Tenant ON dbo.CuentasBancarias (ID_Tenant_FK);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Miembros_Tenant' AND object_id = OBJECT_ID('dbo.Miembros'))
    CREATE INDEX IX_Miembros_Tenant ON dbo.Miembros (ID_Tenant_FK);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CFS_Cheques_Tenant' AND object_id = OBJECT_ID('dbo.CFS_Cheques'))
    CREATE INDEX IX_CFS_Cheques_Tenant ON dbo.CFS_Cheques (ID_Tenant_FK);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Conciliaciones_Tenant' AND object_id = OBJECT_ID('dbo.Conciliaciones'))
    CREATE INDEX IX_Conciliaciones_Tenant ON dbo.Conciliaciones (ID_Tenant_FK);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Categorias_Tenant' AND object_id = OBJECT_ID('dbo.Categorias'))
    CREATE INDEX IX_Categorias_Tenant ON dbo.Categorias (ID_Tenant_FK);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Subcategorias_Tenant' AND object_id = OBJECT_ID('dbo.Subcategorias'))
    CREATE INDEX IX_Subcategorias_Tenant ON dbo.Subcategorias (ID_Tenant_FK);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CFS_CheckPrintSettings_Tenant' AND object_id = OBJECT_ID('dbo.CFS_CheckPrintSettings'))
    CREATE INDEX IX_CFS_CheckPrintSettings_Tenant ON dbo.CFS_CheckPrintSettings (ID_Tenant_FK);
GO

-- ============================================================
-- Backfill existing rows to Tenant 1 (already covered by DEFAULT 1,
-- but explicit UPDATE ensures any pre-existing NULLs are covered
-- if the column was added as nullable first).
-- ============================================================

UPDATE dbo.Transacciones     SET ID_Tenant_FK = 1 WHERE ID_Tenant_FK IS NULL OR ID_Tenant_FK = 0;
UPDATE dbo.Depositos         SET ID_Tenant_FK = 1 WHERE ID_Tenant_FK IS NULL OR ID_Tenant_FK = 0;
UPDATE dbo.CuentasBancarias  SET ID_Tenant_FK = 1 WHERE ID_Tenant_FK IS NULL OR ID_Tenant_FK = 0;
UPDATE dbo.Miembros          SET ID_Tenant_FK = 1 WHERE ID_Tenant_FK IS NULL OR ID_Tenant_FK = 0;
UPDATE dbo.CFS_Cheques       SET ID_Tenant_FK = 1 WHERE ID_Tenant_FK IS NULL OR ID_Tenant_FK = 0;
UPDATE dbo.Conciliaciones    SET ID_Tenant_FK = 1 WHERE ID_Tenant_FK IS NULL OR ID_Tenant_FK = 0;
UPDATE dbo.Categorias        SET ID_Tenant_FK = 1 WHERE ID_Tenant_FK IS NULL OR ID_Tenant_FK = 0;
UPDATE dbo.Subcategorias     SET ID_Tenant_FK = 1 WHERE ID_Tenant_FK IS NULL OR ID_Tenant_FK = 0;

IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.CFS_CheckPrintSettings'))
BEGIN
    UPDATE dbo.CFS_CheckPrintSettings SET ID_Tenant_FK = 1 WHERE ID_Tenant_FK IS NULL OR ID_Tenant_FK = 0;
END
GO
