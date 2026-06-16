$ErrorActionPreference = "Stop"

$secretsPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\cfs-web-local-dev\secrets.json"
$connectionString = (Get-Content $secretsPath | ConvertFrom-Json)."ConnectionStrings:CfsDatabase"

$connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
$connection.Open()

try {
    $command = $connection.CreateCommand()
    $command.CommandText = @"
SELECT
    Cta.ID_Cuenta,
    Cta.NombreCuenta,
    CAST(ISNULL(Cta.SaldoActual, 0) AS decimal(18,2)) AS SaldoLibro,
    CAST(ISNULL(D.TotalDepositosPendientes, 0) AS decimal(18,2)) AS DepositosNoConciliados,
    CAST(ISNULL(T.TotalTransaccionesPendientes, 0) AS decimal(18,2)) AS TransaccionesNoConciliadas,
    CAST(ISNULL(Cta.SaldoActual, 0)
        - ISNULL(D.TotalDepositosPendientes, 0)
        - ISNULL(T.TotalTransaccionesPendientes, 0) AS decimal(18,2)) AS SaldoAjustadoEstimado
FROM dbo.CuentasBancarias Cta
OUTER APPLY (
    SELECT SUM(MontoTotal) AS TotalDepositosPendientes
    FROM dbo.Depositos
    WHERE ID_Cuenta_FK = Cta.ID_Cuenta
      AND ISNULL(Anulado, 0) = 0
      AND ISNULL(Conciliado, 0) = 0
) D
OUTER APPLY (
    SELECT SUM(CASE
        WHEN Cat.TipoCategoria = 'Egreso' THEN -T.Monto
        WHEN Cat.TipoCategoria = 'Ingreso' THEN T.Monto
        ELSE 0
    END) AS TotalTransaccionesPendientes
    FROM dbo.Transacciones T
    JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
    JOIN dbo.Categorias Cat ON Cat.ID_Categoria = S.ID_Categoria_FK
    WHERE T.ID_Cuenta_FK = Cta.ID_Cuenta
      AND ISNULL(T.Anulada, 0) = 0
      AND ISNULL(T.Conciliada, 0) = 0
      AND (
            Cat.TipoCategoria = 'Egreso'
            OR (
                Cat.TipoCategoria = 'Ingreso'
                AND T.ID_Deposito_FK IS NULL
                AND T.MetodoPago NOT IN ('Efectivo', 'Cheque')
            )
      )
) T
ORDER BY Cta.NombreCuenta;
"@

    $reader = $command.ExecuteReader()
    $table = [System.Data.DataTable]::new()
    $table.Load($reader)
    "RECONCILIATION_GAP"
    $table | Format-Table -AutoSize

    $command = $connection.CreateCommand()
    $command.CommandText = @"
SELECT
    Cta.ID_Cuenta,
    Cta.NombreCuenta,
    MAX(R.FechaConciliacion) AS UltimaConciliacion,
    CAST(MAX(R.SaldoEstadoCuenta) AS decimal(18,2)) AS UltimoSaldoConciliado
FROM dbo.CuentasBancarias Cta
LEFT JOIN dbo.Conciliaciones R ON R.ID_Cuenta_FK = Cta.ID_Cuenta
GROUP BY Cta.ID_Cuenta, Cta.NombreCuenta
ORDER BY Cta.NombreCuenta;
"@

    $reader = $command.ExecuteReader()
    $recent = [System.Data.DataTable]::new()
    $recent.Load($reader)
    "LAST_RECONCILIATION"
    $recent | Format-Table -AutoSize
}
finally {
    $connection.Close()
}
