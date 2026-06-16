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
    CAST(CASE
        WHEN Cta.NombreCuenta = 'Ahorros-8698' THEN 15573.49
        WHEN Cta.NombreCuenta = 'Misiones-9012' THEN 2371.73
        WHEN Cta.NombreCuenta = 'Checking-6163' THEN 12503.76
        ELSE Cta.SaldoInicial
    END AS decimal(18,2)) AS SaldoInicialPropuesto,
    CAST(ISNULL(Cta.SaldoActual, 0) AS decimal(18,2)) AS SaldoActualGuardado,
    CAST(ISNULL(D.TotalDepositos, 0) AS decimal(18,2)) AS DepositosActivos,
    CAST(ISNULL(I.IngresosDirectos, 0) AS decimal(18,2)) AS IngresosBancariosDirectos,
    CAST(ISNULL(E.Egresos, 0) AS decimal(18,2)) AS Egresos,
    CAST(
        CASE
            WHEN Cta.NombreCuenta = 'Ahorros-8698' THEN 15573.49
            WHEN Cta.NombreCuenta = 'Misiones-9012' THEN 2371.73
            WHEN Cta.NombreCuenta = 'Checking-6163' THEN 12503.76
            ELSE Cta.SaldoInicial
        END
        + ISNULL(D.TotalDepositos, 0)
        + ISNULL(I.IngresosDirectos, 0)
        - ISNULL(E.Egresos, 0)
        AS decimal(18,2)) AS SaldoLibroCalculado
FROM dbo.CuentasBancarias Cta
OUTER APPLY (
    SELECT SUM(MontoTotal) AS TotalDepositos
    FROM dbo.Depositos
    WHERE ID_Cuenta_FK = Cta.ID_Cuenta
      AND ISNULL(Anulado, 0) = 0
) D
OUTER APPLY (
    SELECT SUM(T.Monto) AS IngresosDirectos
    FROM dbo.Transacciones T
    JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
    JOIN dbo.Categorias Cat ON Cat.ID_Categoria = S.ID_Categoria_FK
    WHERE T.ID_Cuenta_FK = Cta.ID_Cuenta
      AND Cat.TipoCategoria = 'Ingreso'
      AND ISNULL(T.Anulada, 0) = 0
      AND T.MetodoPago NOT IN ('Efectivo', 'Cheque')
) I
OUTER APPLY (
    SELECT SUM(T.Monto) AS Egresos
    FROM dbo.Transacciones T
    JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
    JOIN dbo.Categorias Cat ON Cat.ID_Categoria = S.ID_Categoria_FK
    WHERE T.ID_Cuenta_FK = Cta.ID_Cuenta
      AND Cat.TipoCategoria = 'Egreso'
      AND ISNULL(T.Anulada, 0) = 0
) E
ORDER BY Cta.NombreCuenta;
"@

    $reader = $command.ExecuteReader()
    $table = [System.Data.DataTable]::new()
    $table.Load($reader)
    $table | Format-List
}
finally {
    $connection.Close()
}
