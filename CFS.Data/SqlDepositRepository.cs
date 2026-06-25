using System.Data;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlDepositRepository(SqlConnectionFactory connectionFactory, ITenantContext tenantContext) : IDepositRepository
{
    private readonly int _tenantId = tenantContext.TenantId;

    public async Task<DepositLookups> GetLookupsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var accounts = new List<LookupOption>();
        await using var command = new SqlCommand(
            "SELECT ID_Cuenta, NombreCuenta FROM dbo.CuentasBancarias WHERE ID_Tenant_FK = @tenantId ORDER BY NombreCuenta;",
            connection);
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(new LookupOption(reader.GetInt32(0), reader.GetString(1)));
        }

        return new DepositLookups(accounts);
    }

    public async Task<IReadOnlyList<DepositCandidate>> GetPendingCandidatesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP 500
                   T.ID_Transaccion,
                   T.Fecha,
                   ISNULL(T.Descripcion, '') AS Descripcion,
                   T.Monto,
                   T.ID_Cuenta_FK,
                   Cta.NombreCuenta,
                   S.NombreSubcategoria,
                   CASE WHEN M.ID_Miembro IS NULL THEN NULL
                        ELSE LTRIM(RTRIM(M.Nombre)) + CASE WHEN LTRIM(RTRIM(M.Apellido)) <> '' THEN ' ' + LTRIM(RTRIM(M.Apellido)) ELSE '' END
                   END AS Miembro,
                   T.MetodoPago,
                   T.NumeroCheque
            FROM dbo.Transacciones T
            INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
            INNER JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
            INNER JOIN dbo.CuentasBancarias Cta ON Cta.ID_Cuenta = T.ID_Cuenta_FK
            LEFT JOIN dbo.Miembros M ON M.ID_Miembro = T.ID_Miembro_FK
            WHERE C.TipoCategoria = 'Ingreso'
              AND T.ID_Tenant_FK = @tenantId
              AND ISNULL(T.Anulada, 0) = 0
              AND ISNULL(T.Conciliada, 0) = 0
              AND T.ID_Deposito_FK IS NULL
              AND T.MetodoPago IN ('Efectivo', 'Cheque')
            ORDER BY T.Fecha, T.ID_Transaccion;
            """;

        var rows = new List<DepositCandidate>();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new DepositCandidate(
                reader.GetInt32(reader.GetOrdinal("ID_Transaccion")),
                reader.GetDateTime(reader.GetOrdinal("Fecha")),
                reader.GetString(reader.GetOrdinal("Descripcion")),
                reader.GetDecimal(reader.GetOrdinal("Monto")),
                reader.GetInt32(reader.GetOrdinal("ID_Cuenta_FK")),
                reader.GetString(reader.GetOrdinal("NombreCuenta")),
                reader.GetString(reader.GetOrdinal("NombreSubcategoria")),
                reader["Miembro"] is DBNull ? null : reader.GetString(reader.GetOrdinal("Miembro")),
                reader.GetString(reader.GetOrdinal("MetodoPago")),
                reader["NumeroCheque"] is DBNull ? null : reader.GetString(reader.GetOrdinal("NumeroCheque"))));
        }

        return rows;
    }

    public async Task<IReadOnlyList<DepositSummary>> GetRecentAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP 100
                   D.ID_Deposito,
                   D.FechaDeposito,
                   D.ID_Cuenta_FK,
                   C.NombreCuenta,
                   D.MontoTotal,
                   COUNT(T.ID_Transaccion) AS CantidadItems,
                   CASE WHEN ISNULL(D.Anulado, 0) = 1 THEN 'Anulado'
                        WHEN ISNULL(D.Conciliado, 0) = 1 THEN 'Conciliado'
                        ELSE 'Registrado'
                   END AS Estado,
                   ISNULL(D.UsuarioAnulacion, '') AS Usuario
            FROM dbo.Depositos D
            INNER JOIN dbo.CuentasBancarias C ON C.ID_Cuenta = D.ID_Cuenta_FK
            LEFT JOIN dbo.Transacciones T ON T.ID_Deposito_FK = D.ID_Deposito
            WHERE D.ID_Tenant_FK = @tenantId
            GROUP BY D.ID_Deposito, D.FechaDeposito, D.ID_Cuenta_FK, C.NombreCuenta,
                     D.MontoTotal, D.Anulado, D.Conciliado, D.UsuarioAnulacion
            ORDER BY D.FechaDeposito DESC, D.ID_Deposito DESC;
            """;

        var rows = new List<DepositSummary>();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new DepositSummary(
                reader.GetInt32(reader.GetOrdinal("ID_Deposito")),
                reader.GetDateTime(reader.GetOrdinal("FechaDeposito")),
                reader.GetInt32(reader.GetOrdinal("ID_Cuenta_FK")),
                reader.GetString(reader.GetOrdinal("NombreCuenta")),
                reader.GetDecimal(reader.GetOrdinal("MontoTotal")),
                reader.GetDecimal(reader.GetOrdinal("MontoTotal")),
                reader.GetInt32(reader.GetOrdinal("CantidadItems")),
                reader.GetString(reader.GetOrdinal("Estado")),
                reader.GetString(reader.GetOrdinal("Usuario")),
                reader.GetDateTime(reader.GetOrdinal("FechaDeposito"))));
        }

        return rows;
    }

    public async Task<DepositSaveResult> CreateAsync(
        DepositEntry entry,
        string userName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await CreateWithinTransactionAsync(connection, transaction, entry, _tenantId, cancellationToken);
            if (!result.Saved)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new DepositSaveResult(false, null, result.ErrorMessage);
            }

            await transaction.CommitAsync(cancellationToken);
            await AuditLogger.TryLogAsync(connectionFactory, _tenantId, userName, "CREAR", "Deposito", result.DepositId?.ToString(), $"Nuevo deposito creado. Monto: {entry.ActualTotal}", cancellationToken);
            return new DepositSaveResult(true, result.DepositId, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new DepositSaveResult(false, null, ex.Message);
        }
    }

    public async Task<DepositBatchSaveResult> CreateBatchAsync(
        IReadOnlyList<DepositEntry> entries,
        string userName,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return new DepositBatchSaveResult(false, [], "No hay depósitos para crear.");
        }

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var depositIds = new List<int>();
            foreach (var entry in entries)
            {
                var result = await CreateWithinTransactionAsync(connection, transaction, entry, _tenantId, cancellationToken);
                if (!result.Saved || !result.DepositId.HasValue)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new DepositBatchSaveResult(false, [], result.ErrorMessage ?? "No se pudo crear uno de los depósitos.");
                }

                depositIds.Add(result.DepositId.Value);
            }

            await transaction.CommitAsync(cancellationToken);
            return new DepositBatchSaveResult(true, depositIds, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new DepositBatchSaveResult(false, [], ex.Message);
        }
    }

    public async Task<DepositSaveResult> VoidAsync(
        int id,
        string reason,
        string userName,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return new DepositSaveResult(false, null, "ID de deposito invalido.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return new DepositSaveResult(false, null, "Debes especificar un motivo de anulacion.");
        }

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string getSql = """
                SELECT ID_Cuenta_FK,
                       MontoTotal,
                       ISNULL(Conciliado, 0) AS Conciliado,
                       ISNULL(Anulado, 0) AS Anulado
                FROM dbo.Depositos
                WHERE ID_Deposito = @id
                  AND ID_Tenant_FK = @tenantId;
                """;

            int accountId;
            decimal amount;
            bool reconciled;
            bool voided;

            await using (var command = new SqlCommand(getSql, connection, transaction))
            {
                command.Parameters.Add("@id", SqlDbType.Int).Value = id;
                command.Parameters.AddWithValue("@tenantId", _tenantId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new DepositSaveResult(false, null, "No se encontro el deposito.");
                }

                accountId = reader.GetInt32(reader.GetOrdinal("ID_Cuenta_FK"));
                amount = reader.GetDecimal(reader.GetOrdinal("MontoTotal"));
                reconciled = reader.GetBoolean(reader.GetOrdinal("Conciliado"));
                voided = reader.GetBoolean(reader.GetOrdinal("Anulado"));
            }

            if (voided)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new DepositSaveResult(false, null, "El deposito ya esta anulado.");
            }

            if (reconciled)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new DepositSaveResult(false, null, "No se puede anular un deposito conciliado.");
            }

            const string voidSql = """
                UPDATE dbo.Depositos
                SET Anulado = 1,
                    FechaAnulacion = GETDATE(),
                    UsuarioAnulacion = @user,
                    MotivoAnulacion = @reason
                WHERE ID_Deposito = @id
                  AND ID_Tenant_FK = @tenantId
                  AND ISNULL(Anulado, 0) = 0
                  AND ISNULL(Conciliado, 0) = 0;
                """;

            await using (var command = new SqlCommand(voidSql, connection, transaction))
            {
                command.Parameters.Add("@id", SqlDbType.Int).Value = id;
                command.Parameters.Add("@user", SqlDbType.NVarChar, 100).Value = userName;
                command.Parameters.Add("@reason", SqlDbType.NVarChar, 255).Value = reason.Trim();
                command.Parameters.AddWithValue("@tenantId", _tenantId);
                var affected = await command.ExecuteNonQueryAsync(cancellationToken);
                if (affected == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new DepositSaveResult(false, null, "No se pudo anular el deposito.");
                }
            }

            await using (var command = new SqlCommand(
                "UPDATE dbo.Transacciones SET ID_Deposito_FK = NULL WHERE ID_Deposito_FK = @id AND ID_Tenant_FK = @tenantId;",
                connection,
                transaction))
            {
                command.Parameters.Add("@id", SqlDbType.Int).Value = id;
                command.Parameters.AddWithValue("@tenantId", _tenantId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var command = new SqlCommand(
                "UPDATE dbo.CuentasBancarias SET SaldoActual = SaldoActual - @amount WHERE ID_Cuenta = @accountId;",
                connection,
                transaction))
            {
                command.Parameters.Add("@amount", SqlDbType.Money).Value = amount;
                command.Parameters.Add("@accountId", SqlDbType.Int).Value = accountId;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            await AuditLogger.TryLogAsync(connectionFactory, _tenantId, userName, "ANULAR", "Deposito", id.ToString(), $"Deposito anulado. Motivo: {reason}", cancellationToken);
            return new DepositSaveResult(true, id, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new DepositSaveResult(false, null, ex.Message);
        }
    }

    private static async Task<IReadOnlyList<DepositCandidate>> LoadSelectedAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IReadOnlyCollection<int> ids,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var rows = new List<DepositCandidate>();
        var parameterNames = ids.Select((_, index) => $"@id{index}").ToList();
        var sql = $"""
            SELECT T.ID_Transaccion,
                   T.Fecha,
                   ISNULL(T.Descripcion, '') AS Descripcion,
                   T.Monto,
                   T.ID_Cuenta_FK,
                   Cta.NombreCuenta,
                   S.NombreSubcategoria,
                   CASE WHEN M.ID_Miembro IS NULL THEN NULL
                        ELSE LTRIM(RTRIM(M.Nombre)) + CASE WHEN LTRIM(RTRIM(M.Apellido)) <> '' THEN ' ' + LTRIM(RTRIM(M.Apellido)) ELSE '' END
                   END AS Miembro,
                   T.MetodoPago,
                   T.NumeroCheque
            FROM dbo.Transacciones T
            INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
            INNER JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
            INNER JOIN dbo.CuentasBancarias Cta ON Cta.ID_Cuenta = T.ID_Cuenta_FK
            LEFT JOIN dbo.Miembros M ON M.ID_Miembro = T.ID_Miembro_FK
            WHERE T.ID_Transaccion IN ({string.Join(", ", parameterNames)})
              AND T.ID_Tenant_FK = @tenantId
              AND C.TipoCategoria = 'Ingreso'
              AND ISNULL(T.Anulada, 0) = 0
              AND ISNULL(T.Conciliada, 0) = 0
              AND T.ID_Deposito_FK IS NULL
              AND T.MetodoPago IN ('Efectivo', 'Cheque')
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@tenantId", tenantId);
        var index = 0;
        foreach (var id in ids)
        {
            command.Parameters.Add(parameterNames[index++], SqlDbType.Int).Value = id;
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new DepositCandidate(
                reader.GetInt32(reader.GetOrdinal("ID_Transaccion")),
                reader.GetDateTime(reader.GetOrdinal("Fecha")),
                reader.GetString(reader.GetOrdinal("Descripcion")),
                reader.GetDecimal(reader.GetOrdinal("Monto")),
                reader.GetInt32(reader.GetOrdinal("ID_Cuenta_FK")),
                reader.GetString(reader.GetOrdinal("NombreCuenta")),
                reader.GetString(reader.GetOrdinal("NombreSubcategoria")),
                reader["Miembro"] is DBNull ? null : reader.GetString(reader.GetOrdinal("Miembro")),
                reader.GetString(reader.GetOrdinal("MetodoPago")),
                reader["NumeroCheque"] is DBNull ? null : reader.GetString(reader.GetOrdinal("NumeroCheque"))));
        }

        return rows;
    }

    private static async Task<DepositSaveResult> CreateWithinTransactionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        DepositEntry entry,
        int tenantId,
        CancellationToken cancellationToken)
    {
        if (entry.AccountId <= 0) return new DepositSaveResult(false, null, "Selecciona una cuenta bancaria.");
        if (entry.TransactionIds.Count == 0) return new DepositSaveResult(false, null, "Selecciona al menos un ingreso pendiente.");

        var selected = await LoadSelectedAsync(connection, transaction, entry.TransactionIds, tenantId, cancellationToken);
        if (selected.Count != entry.TransactionIds.Distinct().Count())
        {
            return new DepositSaveResult(false, null, "Uno o mas ingresos seleccionados ya no estan disponibles.");
        }

        if (selected.Any(i => i.AccountId != entry.AccountId))
        {
            return new DepositSaveResult(false, null, "Uno o mas ingresos no pertenecen a la cuenta bancaria seleccionada.");
        }

        var expected = selected.Sum(i => i.Amount);
        if (expected != entry.ActualTotal)
        {
            return new DepositSaveResult(false, null, "El total real debe cuadrar con los ingresos seleccionados.");
        }

        const string insertDepositSql = """
            INSERT INTO dbo.Depositos
                (ID_Cuenta_FK, FechaDeposito, MontoTotal, Conciliado, Anulado, ID_Tenant_FK)
            VALUES
                (@accountId, @date, @actual, 0, 0, @tenantId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;

        int depositId;
        await using (var command = new SqlCommand(insertDepositSql, connection, transaction))
        {
            command.Parameters.Add("@date", SqlDbType.Date).Value = entry.DepositDate.Date;
            command.Parameters.Add("@accountId", SqlDbType.Int).Value = entry.AccountId;
            command.Parameters.Add("@actual", SqlDbType.Money).Value = entry.ActualTotal;
            command.Parameters.AddWithValue("@tenantId", tenantId);
            depositId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }

        foreach (var item in selected)
        {
            await using var command = new SqlCommand(
                "UPDATE dbo.Transacciones SET ID_Deposito_FK = @depositId WHERE ID_Transaccion = @transactionId AND ID_Deposito_FK IS NULL AND ID_Tenant_FK = @tenantId;",
                connection,
                transaction);
            command.Parameters.Add("@depositId", SqlDbType.Int).Value = depositId;
            command.Parameters.Add("@transactionId", SqlDbType.Int).Value = item.TransactionId;
            command.Parameters.AddWithValue("@tenantId", tenantId);
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
            {
                return new DepositSaveResult(false, null, "Uno o mas ingresos ya fueron depositados por otro proceso.");
            }
        }

        await using (var command = new SqlCommand(
            "UPDATE dbo.CuentasBancarias SET SaldoActual = SaldoActual + @amount WHERE ID_Cuenta = @accountId;",
            connection,
            transaction))
        {
            command.Parameters.Add("@amount", SqlDbType.Money).Value = entry.ActualTotal;
            command.Parameters.Add("@accountId", SqlDbType.Int).Value = entry.AccountId;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return new DepositSaveResult(true, depositId, null);
    }
}
