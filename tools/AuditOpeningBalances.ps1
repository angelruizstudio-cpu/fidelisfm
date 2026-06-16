$ErrorActionPreference = "Stop"

$secretsPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\cfs-web-local-dev\secrets.json"
$connectionString = (Get-Content $secretsPath | ConvertFrom-Json)."ConnectionStrings:CfsDatabase"

$connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
$connection.Open()

try {
    $command = $connection.CreateCommand()
    $command.CommandText = @"
SELECT COLUMN_NAME AS ColumnName, DATA_TYPE AS DataType, IS_NULLABLE AS IsNullable
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = 'CuentasBancarias'
ORDER BY ORDINAL_POSITION;
"@
    $reader = $command.ExecuteReader()
    $columns = [System.Data.DataTable]::new()
    $columns.Load($reader)
    "CUENTAS_COLUMNS"
    $columns | Format-Table -AutoSize

    $command = $connection.CreateCommand()
    $command.CommandText = @"
SELECT
    Cta.ID_Cuenta,
    Cta.NombreCuenta,
    T.ID_Transaccion,
    T.Fecha,
    CAST(T.Monto AS decimal(18,2)) AS Monto,
    Cat.TipoCategoria,
    S.NombreSubcategoria,
    ISNULL(T.Descripcion, '') AS Descripcion,
    T.MetodoPago,
    ISNULL(T.Anulada, 0) AS Anulada
FROM dbo.Transacciones T
JOIN dbo.CuentasBancarias Cta ON Cta.ID_Cuenta = T.ID_Cuenta_FK
JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
JOIN dbo.Categorias Cat ON Cat.ID_Categoria = S.ID_Categoria_FK
WHERE (
    LOWER(ISNULL(S.NombreSubcategoria, '')) LIKE '%saldo%'
    OR LOWER(ISNULL(T.Descripcion, '')) LIKE '%saldo%'
    OR LOWER(ISNULL(S.NombreSubcategoria, '')) LIKE '%inicial%'
    OR LOWER(ISNULL(T.Descripcion, '')) LIKE '%inicial%'
)
ORDER BY Cta.NombreCuenta, T.Fecha, T.ID_Transaccion;
"@
    $reader = $command.ExecuteReader()
    $opening = [System.Data.DataTable]::new()
    $opening.Load($reader)
    "SALDO_INICIAL_CANDIDATES"
    $opening | Format-Table -AutoSize

    $command = $connection.CreateCommand()
    $command.CommandText = @"
DECLARE @StartDate date = '2026-01-01';

SELECT
    Cta.ID_Cuenta,
    Cta.NombreCuenta,
    CAST(ISNULL(Cta.SaldoInicial, 0) AS decimal(18,2)) AS SaldoInicial,
    CAST(ISNULL(Cta.SaldoActual, 0) AS decimal(18,2)) AS SaldoActualGuardado,
    CAST(ISNULL(SUM(CASE
        WHEN T.Fecha >= @StartDate AND Cat.TipoCategoria = 'Ingreso' THEN T.Monto
        WHEN T.Fecha >= @StartDate AND Cat.TipoCategoria = 'Egreso' THEN -T.Monto
        ELSE 0
    END), 0) AS decimal(18,2)) AS NetoDesdeEnero,
    CAST(ISNULL(Cta.SaldoInicial, 0) + ISNULL(SUM(CASE
        WHEN T.Fecha >= @StartDate AND Cat.TipoCategoria = 'Ingreso' THEN T.Monto
        WHEN T.Fecha >= @StartDate AND Cat.TipoCategoria = 'Egreso' THEN -T.Monto
        ELSE 0
    END), 0) AS decimal(18,2)) AS SaldoCalculado,
    CAST(ISNULL(SUM(CASE
        WHEN T.Fecha < @StartDate AND Cat.TipoCategoria = 'Ingreso' THEN T.Monto
        WHEN T.Fecha < @StartDate AND Cat.TipoCategoria = 'Egreso' THEN -T.Monto
        ELSE 0
    END), 0) AS decimal(18,2)) AS NetoAntesEnero,
    COUNT(CASE WHEN T.Fecha >= @StartDate THEN 1 END) AS MovimientosDesdeEnero
FROM dbo.CuentasBancarias Cta
LEFT JOIN dbo.Transacciones T ON T.ID_Cuenta_FK = Cta.ID_Cuenta AND ISNULL(T.Anulada, 0) = 0
LEFT JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
LEFT JOIN dbo.Categorias Cat ON Cat.ID_Categoria = S.ID_Categoria_FK
GROUP BY Cta.ID_Cuenta, Cta.NombreCuenta, Cta.SaldoInicial, Cta.SaldoActual
ORDER BY Cta.NombreCuenta;
"@
    $reader = $command.ExecuteReader()
    $rollforward = [System.Data.DataTable]::new()
    $rollforward.Load($reader)
    "ROLLFORWARD_SIN_SALDO_INICIAL_SEPARADO"
    $rollforward | Format-Table -AutoSize
}
finally {
    $connection.Close()
}
