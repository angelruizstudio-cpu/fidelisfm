-- Revisar antes de ejecutar.
-- Establece los saldos iniciales 2026 de las cuentas activas.
-- No toca las cuentas marcadas como OLD-ID.

BEGIN TRANSACTION;

UPDATE dbo.CuentasBancarias
SET SaldoInicial = 15573.49
WHERE NombreCuenta = 'Ahorros-8698';

UPDATE dbo.CuentasBancarias
SET SaldoInicial = 2371.73
WHERE NombreCuenta = 'Misiones-9012';

UPDATE dbo.CuentasBancarias
SET SaldoInicial = 12503.76
WHERE NombreCuenta = 'Checking-6163';

SELECT
    ID_Cuenta,
    NombreCuenta,
    SaldoInicial,
    SaldoActual
FROM dbo.CuentasBancarias
WHERE NombreCuenta IN ('Ahorros-8698', 'Misiones-9012', 'Checking-6163')
ORDER BY NombreCuenta;

-- Si el SELECT se ve correcto:
-- COMMIT TRANSACTION;

-- Si algo no se ve correcto:
ROLLBACK TRANSACTION;
