/*
    Links dbo.Usuarios to dbo.Tenants so each login resolves to its own tenant
    instead of always defaulting to Tenant 1. Additive only:
      - Existing users get ID_Tenant_FK = 1 (the founder tenant), preserving
        today's behavior exactly.
      - No existing columns are renamed or removed.
*/

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Usuarios') AND name = 'ID_Tenant_FK'
)
BEGIN
    ALTER TABLE dbo.Usuarios
        ADD ID_Tenant_FK INT NOT NULL CONSTRAINT DF_Usuarios_Tenant DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Usuarios_Tenant'
)
BEGIN
    ALTER TABLE dbo.Usuarios
        ADD CONSTRAINT FK_Usuarios_Tenant
        FOREIGN KEY (ID_Tenant_FK) REFERENCES dbo.Tenants(ID_Tenant);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_Usuarios_Tenant' AND object_id = OBJECT_ID('dbo.Usuarios')
)
    CREATE INDEX IX_Usuarios_Tenant ON dbo.Usuarios (ID_Tenant_FK);

UPDATE dbo.Usuarios SET ID_Tenant_FK = 1 WHERE ID_Tenant_FK IS NULL OR ID_Tenant_FK = 0;
