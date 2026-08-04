/*
    Fidelis Financial Management - reconciliation membership tracking
    -----------------------------------------------------------------------------------------------
    Adds ID_Conciliacion_FK to dbo.Depositos and dbo.Transacciones so that each cleared item records
    which reconciliation cleared it. This lets us reverse (anular) ANY reconciliation precisely by
    un-clearing exactly its own items.

    - Nullable column: additive and non-breaking. Existing rows get NULL.
    - New reconciliations populate it on close. Reconciliations closed before this migration have
      NULL membership; those are reversed by a date-window fallback in code.
    - Safe to re-run (guarded with NOT EXISTS).

    Run this against the PRODUCTION database before/with the deploy that adds "Anular conciliación".
*/

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Depositos') AND name = 'ID_Conciliacion_FK'
)
BEGIN
    ALTER TABLE dbo.Depositos ADD ID_Conciliacion_FK INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Transacciones') AND name = 'ID_Conciliacion_FK'
)
BEGIN
    ALTER TABLE dbo.Transacciones ADD ID_Conciliacion_FK INT NULL;
END
GO
