$ErrorActionPreference = "Stop"

$secretsPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\cfs-web-local-dev\secrets.json"
$connectionString = (Get-Content $secretsPath | ConvertFrom-Json)."ConnectionStrings:CfsDatabase"

$connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
$connection.Open()

try {
    $command = $connection.CreateCommand()
    $command.CommandText = @"
SELECT TOP 20
       ISNULL(T.MetodoPago, '(NULL)') AS MetodoPago,
       COUNT(*) AS Total,
       SUM(CASE WHEN T.ID_Deposito_FK IS NULL THEN 1 ELSE 0 END) AS SinDeposito,
       SUM(CASE WHEN ISNULL(T.Anulada, 0) = 1 THEN 1 ELSE 0 END) AS Anuladas,
       SUM(CASE WHEN ISNULL(T.Conciliada, 0) = 1 THEN 1 ELSE 0 END) AS Conciliadas
FROM dbo.Transacciones T
JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
WHERE C.TipoCategoria = 'Ingreso'
GROUP BY ISNULL(T.MetodoPago, '(NULL)')
ORDER BY Total DESC;
"@

    $reader = $command.ExecuteReader()
    $table = [System.Data.DataTable]::new()
    $table.Load($reader)
    $table | Format-Table -AutoSize

    $command = $connection.CreateCommand()
    $command.CommandText = @"
SELECT TOP 10
       T.ID_Transaccion,
       T.Fecha,
       T.Monto,
       ISNULL(T.MetodoPago, '') AS MetodoPago,
       T.ID_Deposito_FK,
       ISNULL(T.Anulada, 0) AS Anulada,
       ISNULL(T.Conciliada, 0) AS Conciliada
FROM dbo.Transacciones T
JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
WHERE C.TipoCategoria = 'Ingreso'
ORDER BY T.Fecha DESC, T.ID_Transaccion DESC;
"@

    $reader = $command.ExecuteReader()
    $sample = [System.Data.DataTable]::new()
    $sample.Load($reader)
    "RECENT_INCOME_SAMPLE"
    $sample | Format-Table -AutoSize
}
finally {
    $connection.Close()
}
