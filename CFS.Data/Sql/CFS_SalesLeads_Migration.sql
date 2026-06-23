/*
    Fidelis Financial Management - sales leads
    -----------------------------------------------------------
    Additive only. Captures Multi-Iglesia "Contactar ventas" leads
    from the public marketing site mini-asistente, including a
    basic needs profile. Does not touch any existing table.
*/

IF OBJECT_ID('dbo.SalesLeads', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesLeads
    (
        ID_SalesLead INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesLeads PRIMARY KEY,
        OrganizationName NVARCHAR(150) NOT NULL,
        ContactName NVARCHAR(150) NOT NULL,
        Email NVARCHAR(256) NOT NULL,
        Phone NVARCHAR(50) NOT NULL,
        ChurchCount INT NOT NULL,
        KeyFeatures NVARCHAR(500) NOT NULL,
        Timeline NVARCHAR(50) NOT NULL,
        Comments NVARCHAR(1000) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_SalesLeads_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END;
