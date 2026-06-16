$ErrorActionPreference = "Stop"

$secretsPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\cfs-web-local-dev\secrets.json"
$connectionString = (Get-Content $secretsPath | ConvertFrom-Json)."ConnectionStrings:CfsDatabase"

$connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
$connection.Open()

try {
    foreach ($tableName in @("Conciliaciones", "ConciliacionesEnProgreso")) {
        $command = $connection.CreateCommand()
        $command.CommandText = @"
SELECT
    COLUMN_NAME AS ColumnName,
    DATA_TYPE AS DataType,
    IS_NULLABLE AS IsNullable,
    CHARACTER_MAXIMUM_LENGTH AS MaxLength
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = @tableName
ORDER BY ORDINAL_POSITION;
"@
        $null = $command.Parameters.Add("@tableName", [System.Data.SqlDbType]::NVarChar, 128)
        $command.Parameters["@tableName"].Value = $tableName

        $reader = $command.ExecuteReader()
        $schema = [System.Data.DataTable]::new()
        $schema.Load($reader)
        "TABLE: $tableName"
        $schema | Format-Table -AutoSize
    }
}
finally {
    $connection.Close()
}
