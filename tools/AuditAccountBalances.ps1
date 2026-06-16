$ErrorActionPreference = "Stop"

$secretsPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\cfs-web-local-dev\secrets.json"
$connectionString = (Get-Content $secretsPath | ConvertFrom-Json)."ConnectionStrings:CfsDatabase"

$connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
$connection.Open()

try {
    $command = $connection.CreateCommand()
    $command.CommandText = @"
SELECT
    ID_Cuenta,
    NombreCuenta,
    CAST(ISNULL(SaldoActual, 0) AS decimal(18,2)) AS SaldoActualGuardado
FROM dbo.CuentasBancarias
ORDER BY NombreCuenta;
"@

    $reader = $command.ExecuteReader()
    $accounts = [System.Data.DataTable]::new()
    $accounts.Load($reader)
    "CUENTAS_BANCARIAS"
    $accounts | Format-Table -AutoSize

    $command = $connection.CreateCommand()
    $command.CommandText = @"
SELECT
    Cta.ID_Cuenta,
    Cta.NombreCuenta,
    CAST(ISNULL(Cta.SaldoActual, 0) AS decimal(18,2)) AS SaldoActualGuardado,
    CAST(ISNULL(SUM(CASE
        WHEN Cat.TipoCategoria = 'Ingreso' THEN T.Monto
        WHEN Cat.TipoCategoria = 'Egreso' THEN -T.Monto
        ELSE 0
    END), 0) AS decimal(18,2)) AS NetoTransacciones,
    COUNT(T.ID_Transaccion) AS CantidadTransacciones
FROM dbo.CuentasBancarias Cta
LEFT JOIN dbo.Transacciones T
    ON T.ID_Cuenta_FK = Cta.ID_Cuenta
   AND ISNULL(T.Anulada, 0) = 0
LEFT JOIN dbo.Subcategorias S
    ON S.ID_Subcategoria = T.ID_Subcategoria_FK
LEFT JOIN dbo.Categorias Cat
    ON Cat.ID_Categoria = S.ID_Categoria_FK
GROUP BY Cta.ID_Cuenta, Cta.NombreCuenta, Cta.SaldoActual
ORDER BY Cta.NombreCuenta;
"@

    $reader = $command.ExecuteReader()
    $ledger = [System.Data.DataTable]::new()
    $ledger.Load($reader)
    "NETO_TRANSACCIONES"
    $ledger | Format-Table -AutoSize

    $command = $connection.CreateCommand()
    $command.CommandText = @"
SELECT
    Cta.ID_Cuenta,
    Cta.NombreCuenta,
    CAST(ISNULL(SUM(CASE WHEN ISNULL(D.Anulado, 0) = 0 THEN D.MontoTotal ELSE 0 END), 0) AS decimal(18,2)) AS TotalDepositosActivos,
    COUNT(D.ID_Deposito) AS CantidadDepositos
FROM dbo.CuentasBancarias Cta
LEFT JOIN dbo.Depositos D
    ON D.ID_Cuenta_FK = Cta.ID_Cuenta
GROUP BY Cta.ID_Cuenta, Cta.NombreCuenta
ORDER BY Cta.NombreCuenta;
"@

    $reader = $command.ExecuteReader()
    $deposits = [System.Data.DataTable]::new()
    $deposits.Load($reader)
    "DEPOSITOS"
    $deposits | Format-Table -AutoSize
}
finally {
    $connection.Close()
}
