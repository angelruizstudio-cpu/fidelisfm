-- OBSOLETO: no ejecutar en la base real.
-- La base existente ya tiene dbo.Depositos y Transacciones.ID_Deposito_FK.
-- El repositorio actual usa esas tablas existentes.

IF OBJECT_ID('dbo.CFS_Depositos', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CFS_Depositos
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CFS_Depositos PRIMARY KEY,
        FechaDeposito DATE NOT NULL,
        CuentaBancariaId INT NOT NULL,
        TotalEsperado MONEY NOT NULL,
        TotalReal MONEY NOT NULL,
        Notas NVARCHAR(300) NULL,
        Estado NVARCHAR(20) NOT NULL CONSTRAINT DF_CFS_Depositos_Estado DEFAULT ('Registrado'),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_CFS_Depositos_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NOT NULL,
        VoidedAt DATETIME2 NULL,
        VoidedBy NVARCHAR(100) NULL,
        VoidReason NVARCHAR(300) NULL,
        CONSTRAINT FK_CFS_Depositos_CuentasBancarias
            FOREIGN KEY (CuentaBancariaId) REFERENCES dbo.CuentasBancarias(ID_Cuenta),
        CONSTRAINT CK_CFS_Depositos_TotalEsperado CHECK (TotalEsperado >= 0),
        CONSTRAINT CK_CFS_Depositos_TotalReal CHECK (TotalReal >= 0),
        CONSTRAINT CK_CFS_Depositos_Estado CHECK (Estado IN ('Registrado', 'Anulado'))
    );
END
GO

IF OBJECT_ID('dbo.CFS_DepositoItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CFS_DepositoItems
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CFS_DepositoItems PRIMARY KEY,
        DepositoId INT NOT NULL,
        TransaccionId INT NOT NULL,
        Monto MONEY NOT NULL,
        CONSTRAINT FK_CFS_DepositoItems_Depositos
            FOREIGN KEY (DepositoId) REFERENCES dbo.CFS_Depositos(Id),
        CONSTRAINT FK_CFS_DepositoItems_Transacciones
            FOREIGN KEY (TransaccionId) REFERENCES dbo.Transacciones(ID_Transaccion),
        CONSTRAINT CK_CFS_DepositoItems_Monto CHECK (Monto > 0)
    );

    CREATE UNIQUE INDEX UX_CFS_DepositoItems_Transaccion
        ON dbo.CFS_DepositoItems(TransaccionId);
END
GO
