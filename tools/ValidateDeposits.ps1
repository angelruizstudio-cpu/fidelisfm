$ErrorActionPreference = "Stop"

$secretsPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\cfs-web-local-dev\secrets.json"
$connectionString = (Get-Content $secretsPath | ConvertFrom-Json)."ConnectionStrings:CfsDatabase"

$connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
$connection.Open()

try {
    $command = $connection.CreateCommand()
    $command.CommandText = @"
SELECT 'DepositosColumns' AS Section, COUNT(*) AS Value
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = 'Depositos'
UNION ALL
SELECT 'PendingDepositCandidates', COUNT(*)
FROM dbo.Transacciones T
JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
WHERE C.TipoCategoria = 'Ingreso'
  AND ISNULL(T.Anulada, 0) = 0
  AND ISNULL(T.Conciliada, 0) = 0
  AND T.ID_Deposito_FK IS NULL
  AND T.MetodoPago IN ('Efectivo', 'Cheque')
UNION ALL
SELECT 'RecentDeposits', COUNT(*)
FROM dbo.Depositos;
"@

    $reader = $command.ExecuteReader()
    $table = [System.Data.DataTable]::new()
    $table.Load($reader)
    $table | Format-Table -AutoSize

    $command = $connection.CreateCommand()
    $command.CommandText = @"
SELECT TOP 5
       T.ID_Transaccion,
       T.Fecha,
       T.Monto,
       T.MetodoPago,
       ISNULL(T.NumeroCheque, '') AS NumeroCheque,
       ISNULL(T.Descripcion, '') AS Descripcion
FROM dbo.Transacciones T
JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
WHERE C.TipoCategoria = 'Ingreso'
  AND ISNULL(T.Anulada, 0) = 0
  AND ISNULL(T.Conciliada, 0) = 0
  AND T.ID_Deposito_FK IS NULL
  AND T.MetodoPago IN ('Efectivo', 'Cheque')
ORDER BY T.Fecha, T.ID_Transaccion;
"@

    $reader = $command.ExecuteReader()
    $pending = [System.Data.DataTable]::new()
    $pending.Load($reader)
    "PENDING_SAMPLE"
    $pending | Format-Table -AutoSize
}
finally {
    $connection.Close()
}
