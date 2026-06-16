/*
    CFS - Fecha de inicio del saldo en libros por cuenta bancaria

    Proposito:
    - SaldoInicial representa el balance oficial con el que CFS empieza.
    - FechaInicioSaldo indica desde que fecha se deben sumar depositos,
      ingresos directos y egresos al SaldoInicial.
    - Esto evita mezclar transacciones historicas/importadas con el saldo
      inicial operacional.

    Ejecutar una vez. Luego ajustar las fechas por cuenta.
*/

IF COL_LENGTH('dbo.CuentasBancarias', 'FechaInicioSaldo') IS NULL
BEGIN
    ALTER TABLE dbo.CuentasBancarias
        ADD FechaInicioSaldo date NULL;
END;
GO

/*
    Ajusta estas fechas segun el comienzo oficial de cada cuenta en CFS.
    Si todas comenzaron el 1 de enero de 2026, puedes usar el bloque de abajo.
*/

-- UPDATE dbo.CuentasBancarias
-- SET FechaInicioSaldo = '2026-01-01'
-- WHERE NombreCuenta IN ('Ahorros-8698', 'Checking-6163', 'Misiones-9012');

/*
    Verificacion:
*/

SELECT ID_Cuenta,
       NombreCuenta,
       SaldoInicial,
       FechaInicioSaldo,
       SaldoActual
FROM dbo.CuentasBancarias
WHERE NombreCuenta NOT LIKE '%(OLD-ID-%'
ORDER BY NombreCuenta;
