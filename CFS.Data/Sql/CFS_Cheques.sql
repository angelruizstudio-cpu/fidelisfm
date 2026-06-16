IF OBJECT_ID('dbo.CFS_Cheques', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CFS_Cheques
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CFS_Cheques PRIMARY KEY,
        EgresoId INT NULL,
        CuentaBancariaId INT NOT NULL,
        NumeroCheque NVARCHAR(50) NOT NULL,
        FechaCheque DATE NOT NULL,
        Beneficiario NVARCHAR(200) NOT NULL,
        DireccionBeneficiario NVARCHAR(300) NULL,
        Monto MONEY NOT NULL,
        Memo NVARCHAR(300) NULL,
        Estado NVARCHAR(20) NOT NULL CONSTRAINT DF_CFS_Cheques_Estado DEFAULT ('Borrador'),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_CFS_Cheques_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NOT NULL,
        PrintedAt DATETIME2 NULL,
        PrintedBy NVARCHAR(100) NULL,
        VoidedAt DATETIME2 NULL,
        VoidedBy NVARCHAR(100) NULL,
        VoidReason NVARCHAR(300) NULL,
        CONSTRAINT FK_CFS_Cheques_CuentasBancarias
            FOREIGN KEY (CuentaBancariaId) REFERENCES dbo.CuentasBancarias(ID_Cuenta),
        CONSTRAINT CK_CFS_Cheques_Monto
            CHECK (Monto > 0),
        CONSTRAINT CK_CFS_Cheques_Estado
            CHECK (Estado IN ('Borrador', 'Impreso', 'Anulado'))
    );

    CREATE UNIQUE INDEX UX_CFS_Cheques_Cuenta_Numero_Activo
        ON dbo.CFS_Cheques(CuentaBancariaId, NumeroCheque)
        WHERE Estado <> 'Anulado';
END
GO
