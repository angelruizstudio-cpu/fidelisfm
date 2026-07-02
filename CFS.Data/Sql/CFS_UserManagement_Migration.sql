/*
    CFS User Management Migration
    - Adds Email, IsActive, MustChangePassword columns to dbo.Usuarios
    - Creates dbo.PasswordResetTokens for secure password-reset / welcome flows
    - Inserts Tesorero and Auditor roles into dbo.Roles (idempotent)
    Additive only — no existing columns are changed or removed.
*/

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Usuarios') AND name = 'Email')
    ALTER TABLE dbo.Usuarios ADD Email NVARCHAR(256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Usuarios') AND name = 'IsActive')
    ALTER TABLE dbo.Usuarios ADD IsActive BIT NOT NULL CONSTRAINT DF_Usuarios_IsActive DEFAULT 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Usuarios') AND name = 'MustChangePassword')
    ALTER TABLE dbo.Usuarios ADD MustChangePassword BIT NOT NULL CONSTRAINT DF_Usuarios_MustChangePassword DEFAULT 0;
GO

IF OBJECT_ID('dbo.PasswordResetTokens', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetTokens (
        ID_Token      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PasswordResetTokens PRIMARY KEY,
        ID_Usuario_FK INT NOT NULL CONSTRAINT FK_PasswordResetTokens_Usuario FOREIGN KEY REFERENCES dbo.Usuarios(ID_Usuario),
        ID_Tenant_FK  INT NOT NULL,
        Token         NVARCHAR(128) NOT NULL,
        ExpiresAt     DATETIME2(0)  NOT NULL,
        UsedAt        DATETIME2(0)  NULL,
        CreatedAt     DATETIME2(0)  NOT NULL CONSTRAINT DF_PasswordResetTokens_CreatedAt DEFAULT SYSUTCDATETIME()
    );
    CREATE UNIQUE INDEX UX_PasswordResetTokens_Token ON dbo.PasswordResetTokens(Token);
    CREATE INDEX IX_PasswordResetTokens_Tenant ON dbo.PasswordResetTokens(ID_Tenant_FK);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE NombreRol = 'Tesorero')
    INSERT INTO dbo.Roles (NombreRol) VALUES ('Tesorero');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE NombreRol = 'Auditor')
    INSERT INTO dbo.Roles (NombreRol) VALUES ('Auditor');
GO
