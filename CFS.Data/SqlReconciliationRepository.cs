using System.Data;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlReconciliationRepository(SqlConnectionFactory connectionFactory, ITenantContext tenantContext) : IReconciliationRepository
{
    private readonly int _tenantId = tenantContext.TenantId;

    public async Task<ReconciliationLookups> GetLookupsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var accounts = new List<LookupOption>();
        await using var command = new SqlCommand(
            "SELECT ID_Cuenta, NombreCuenta FROM dbo.CuentasBancarias WHERE NombreCuenta NOT LIKE '%(OLD-ID-%' AND ID_Tenant_FK = @tenantId ORDER BY NombreCuenta;",
            connection);
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(new LookupOption(reader.GetInt32(0), reader.GetString(1)));
        }

        return new ReconciliationLookups(accounts);
    }

    public async Task<ReconciliationWorkspace> GetWorkspaceAsync(
        int accountId,
        DateTime statementDate,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var startingPoint = await GetBeginningBalanceAsync(connection, accountId, statementDate, _tenantId, cancellationToken);
        var deposits = await GetDepositCandidatesAsync(connection, accountId, statementDate, _tenantId, cancellationToken);
        var transactions = await GetTransactionCandidatesAsync(connection, accountId, statementDate, _tenantId, cancellationToken);
        var recent = await GetRecentAsync(connection, accountId, _tenantId, cancellationToken);

        return new ReconciliationWorkspace(startingPoint.Balance, startingPoint.LastStatementDate, deposits, transactions, recent);
    }

    public async Task<ReconciliationSaveResult> CloseAsync(
        ReconciliationEntry entry,
        string userName,
        CancellationToken cancellationToken = default)
    {
        if (entry.AccountId <= 0)
        {
            return new ReconciliationSaveResult(false, null, "Selecciona una cuenta bancaria.");
        }

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var beginningBalance = await GetBeginningBalanceAsync(connection, entry.AccountId, entry.StatementDate, _tenantId, transaction, cancellationToken);
            var selectedTotal = await GetSelectedTotalAsync(connection, transaction, entry, _tenantId, cancellationToken);
            var clearedBalance = beginningBalance.Balance + selectedTotal;

            if (clearedBalance != entry.StatementBalance)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ReconciliationSaveResult(false, null, "La conciliacion no cuadra. Revisa las partidas seleccionadas.");
            }

            const string insertSql = """
                INSERT INTO dbo.Conciliaciones (ID_Cuenta_FK, FechaConciliacion, SaldoEstadoCuenta, ID_Tenant_FK)
                VALUES (@accountId, @date, @balance, @tenantId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """;

            int reconciliationId;
            await using (var command = new SqlCommand(insertSql, connection, transaction))
            {
                command.Parameters.Add("@accountId", SqlDbType.Int).Value = entry.AccountId;
                command.Parameters.Add("@date", SqlDbType.Date).Value = entry.StatementDate.Date;
                command.Parameters.Add("@balance", SqlDbType.Decimal).Value = entry.StatementBalance;
                command.Parameters.AddWithValue("@tenantId", _tenantId);
                reconciliationId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            }

            await MarkDepositsAsync(connection, transaction, entry.DepositIds, entry.AccountId, entry.StatementDate, _tenantId, reconciliationId, cancellationToken);
            await MarkTransactionsAsync(connection, transaction, entry.TransactionIds, entry.AccountId, entry.StatementDate, _tenantId, reconciliationId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new ReconciliationSaveResult(true, reconciliationId, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ReconciliationSaveResult(false, null, ex.Message);
        }
    }

    public async Task<ReconciliationSaveResult> VoidAsync(
        int reconciliationId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        if (reconciliationId <= 0)
        {
            return new ReconciliationSaveResult(false, null, "ID de conciliacion invalido.");
        }

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            int accountId;
            DateTime statementDate;

            const string getSql = """
                SELECT ID_Cuenta_FK, FechaConciliacion
                FROM dbo.Conciliaciones
                WHERE ID_Conciliacion = @id AND ID_Tenant_FK = @tenantId;
                """;
            await using (var command = new SqlCommand(getSql, connection, transaction))
            {
                command.Parameters.Add("@id", SqlDbType.Int).Value = reconciliationId;
                command.Parameters.AddWithValue("@tenantId", _tenantId);
                await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new ReconciliationSaveResult(false, null, "No se encontro la conciliacion.");
                }

                accountId = reader.GetInt32(reader.GetOrdinal("ID_Cuenta_FK"));
                statementDate = reader.GetDateTime(reader.GetOrdinal("FechaConciliacion"));
            }

            // 1) Un-clear items recorded as members of this reconciliation (precise path).
            var fkAffected = 0;
            await using (var command = new SqlCommand(
                "UPDATE dbo.Depositos SET Conciliado = 0, ID_Conciliacion_FK = NULL WHERE ID_Conciliacion_FK = @id AND ID_Tenant_FK = @tenantId;",
                connection, transaction))
            {
                command.Parameters.Add("@id", SqlDbType.Int).Value = reconciliationId;
                command.Parameters.AddWithValue("@tenantId", _tenantId);
                fkAffected += await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var command = new SqlCommand(
                "UPDATE dbo.Transacciones SET Conciliada = 0, ID_Conciliacion_FK = NULL WHERE ID_Conciliacion_FK = @id AND ID_Tenant_FK = @tenantId;",
                connection, transaction))
            {
                command.Parameters.Add("@id", SqlDbType.Int).Value = reconciliationId;
                command.Parameters.AddWithValue("@tenantId", _tenantId);
                fkAffected += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            // 2) Legacy fallback: reconciliations closed before membership tracking have no FK on
            //    their items. Only when this reconciliation has no recorded members, un-clear items
            //    in its statement-date window (after the previous reconciliation, up to this date).
            if (fkAffected == 0)
            {
                DateTime? previousDate = null;
                const string prevSql = """
                    SELECT MAX(FechaConciliacion)
                    FROM dbo.Conciliaciones
                    WHERE ID_Cuenta_FK = @accountId AND ID_Tenant_FK = @tenantId
                      AND FechaConciliacion < @date AND ID_Conciliacion <> @id;
                    """;
                await using (var command = new SqlCommand(prevSql, connection, transaction))
                {
                    command.Parameters.Add("@accountId", SqlDbType.Int).Value = accountId;
                    command.Parameters.Add("@date", SqlDbType.Date).Value = statementDate.Date;
                    command.Parameters.Add("@id", SqlDbType.Int).Value = reconciliationId;
                    command.Parameters.AddWithValue("@tenantId", _tenantId);
                    if (await command.ExecuteScalarAsync(cancellationToken) is DateTime dt)
                    {
                        previousDate = dt;
                    }
                }

                const string legacyDeposits = """
                    UPDATE dbo.Depositos SET Conciliado = 0
                    WHERE ID_Cuenta_FK = @accountId AND ID_Tenant_FK = @tenantId
                      AND ID_Conciliacion_FK IS NULL AND ISNULL(Conciliado, 0) = 1
                      AND FechaDeposito <= @date AND (@prevDate IS NULL OR FechaDeposito > @prevDate);
                    """;
                const string legacyTransactions = """
                    UPDATE dbo.Transacciones SET Conciliada = 0
                    WHERE ID_Cuenta_FK = @accountId AND ID_Tenant_FK = @tenantId
                      AND ID_Conciliacion_FK IS NULL AND ISNULL(Conciliada, 0) = 1
                      AND Fecha <= @date AND (@prevDate IS NULL OR Fecha > @prevDate);
                    """;
                foreach (var legacySql in new[] { legacyDeposits, legacyTransactions })
                {
                    await using var command = new SqlCommand(legacySql, connection, transaction);
                    command.Parameters.Add("@accountId", SqlDbType.Int).Value = accountId;
                    command.Parameters.Add("@date", SqlDbType.Date).Value = statementDate.Date;
                    command.Parameters.Add("@prevDate", SqlDbType.Date).Value = (object?)previousDate ?? DBNull.Value;
                    command.Parameters.AddWithValue("@tenantId", _tenantId);
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            // 3) Delete the reconciliation record.
            await using (var command = new SqlCommand(
                "DELETE FROM dbo.Conciliaciones WHERE ID_Conciliacion = @id AND ID_Tenant_FK = @tenantId;",
                connection, transaction))
            {
                command.Parameters.Add("@id", SqlDbType.Int).Value = reconciliationId;
                command.Parameters.AddWithValue("@tenantId", _tenantId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            await AuditLogger.TryLogAsync(connectionFactory, _tenantId, userName, "ANULAR", "Conciliacion", reconciliationId.ToString(), "Conciliacion anulada.", cancellationToken);
            return new ReconciliationSaveResult(true, reconciliationId, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ReconciliationSaveResult(false, null, ex.Message);
        }
    }

    private static async Task<ReconciliationStartingPoint> GetBeginningBalanceAsync(
        SqlConnection connection,
        int accountId,
        DateTime statementDate,
        int tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            GetBeginningBalanceSql(),
            connection);
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@statementDate", SqlDbType.Date).Value = statementDate.Date;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new ReconciliationStartingPoint(0, null);
        }

        return ReadStartingPoint(reader);
    }

    private static async Task<ReconciliationStartingPoint> GetBeginningBalanceAsync(
        SqlConnection connection,
        int accountId,
        DateTime statementDate,
        int tenantId,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            GetBeginningBalanceSql(),
            connection,
            transaction);
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@statementDate", SqlDbType.Date).Value = statementDate.Date;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new ReconciliationStartingPoint(0, null);
        }

        return ReadStartingPoint(reader);
    }

    private static string GetBeginningBalanceSql() =>
        """
        SELECT CAST(COALESCE(Prev.SaldoEstadoCuenta, Cta.SaldoInicial, 0) AS decimal(18, 2)) AS BeginningBalance,
               Prev.FechaConciliacion AS LastStatementDate
        FROM dbo.CuentasBancarias Cta
        OUTER APPLY (
            SELECT TOP 1 R.SaldoEstadoCuenta,
                   R.FechaConciliacion
            FROM dbo.Conciliaciones R
            WHERE R.ID_Cuenta_FK = Cta.ID_Cuenta
              AND R.ID_Tenant_FK = @tenantId
              AND R.FechaConciliacion < @statementDate
            ORDER BY R.FechaConciliacion DESC, R.ID_Conciliacion DESC
        ) Prev
        WHERE Cta.ID_Cuenta = @accountId
          AND Cta.ID_Tenant_FK = @tenantId;
        """;

    private static ReconciliationStartingPoint ReadStartingPoint(SqlDataReader reader)
    {
        var lastDateOrdinal = reader.GetOrdinal("LastStatementDate");
        var lastStatementDate = reader.IsDBNull(lastDateOrdinal)
            ? (DateTime?)null
            : reader.GetDateTime(lastDateOrdinal);

        return new ReconciliationStartingPoint(
            reader.GetDecimal(reader.GetOrdinal("BeginningBalance")),
            lastStatementDate);
    }

    private static async Task<IReadOnlyList<ReconciliationCandidate>> GetDepositCandidatesAsync(
        SqlConnection connection,
        int accountId,
        DateTime statementDate,
        int tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ID_Deposito,
                   FechaDeposito,
                   MontoTotal
            FROM dbo.Depositos
            WHERE ID_Cuenta_FK = @accountId
              AND ID_Tenant_FK = @tenantId
              AND FechaDeposito <= @statementDate
              AND ISNULL(Anulado, 0) = 0
              AND ISNULL(Conciliado, 0) = 0
            ORDER BY FechaDeposito, ID_Deposito;
            """;

        var rows = new List<ReconciliationCandidate>();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@statementDate", SqlDbType.Date).Value = statementDate.Date;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(reader.GetOrdinal("ID_Deposito"));
            rows.Add(new ReconciliationCandidate(
                id,
                "Deposito",
                reader.GetDateTime(reader.GetOrdinal("FechaDeposito")),
                $"Deposito #{id}",
                reader.GetDecimal(reader.GetOrdinal("MontoTotal")),
                id.ToString()));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ReconciliationCandidate>> GetTransactionCandidatesAsync(
        SqlConnection connection,
        int accountId,
        DateTime statementDate,
        int tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT T.ID_Transaccion,
                   T.Fecha,
                   ISNULL(T.Descripcion, '') AS Descripcion,
                   T.Monto,
                   ISNULL(T.NumeroCheque, '') AS NumeroCheque,
                   C.TipoCategoria,
                   T.MetodoPago
            FROM dbo.Transacciones T
            INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
            INNER JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
            WHERE T.ID_Cuenta_FK = @accountId
              AND T.ID_Tenant_FK = @tenantId
              AND T.Fecha <= @statementDate
              AND ISNULL(T.Anulada, 0) = 0
              AND ISNULL(T.Conciliada, 0) = 0
              AND (
                    C.TipoCategoria = 'Egreso'
                    OR (
                        C.TipoCategoria = 'Ingreso'
                        AND T.ID_Deposito_FK IS NULL
                        AND T.MetodoPago NOT IN ('Efectivo', 'Cheque')
                    )
              )
            ORDER BY T.Fecha, T.ID_Transaccion;
            """;

        var rows = new List<ReconciliationCandidate>();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@statementDate", SqlDbType.Date).Value = statementDate.Date;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var type = reader.GetString(reader.GetOrdinal("TipoCategoria"));
            var amount = reader.GetDecimal(reader.GetOrdinal("Monto"));
            if (type.Equals("Egreso", StringComparison.OrdinalIgnoreCase))
            {
                amount = -amount;
            }

            var checkNumber = reader.GetString(reader.GetOrdinal("NumeroCheque"));
            var id = reader.GetInt32(reader.GetOrdinal("ID_Transaccion"));
            var source = type.Equals("Egreso", StringComparison.OrdinalIgnoreCase)
                ? "Pago"
                : "Ingreso directo";

            rows.Add(new ReconciliationCandidate(
                id,
                source,
                reader.GetDateTime(reader.GetOrdinal("Fecha")),
                reader.GetString(reader.GetOrdinal("Descripcion")),
                amount,
                string.IsNullOrWhiteSpace(checkNumber) ? id.ToString() : checkNumber));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ReconciliationSummary>> GetRecentAsync(
        SqlConnection connection,
        int accountId,
        int tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP 20
                   R.ID_Conciliacion,
                   R.ID_Cuenta_FK,
                   C.NombreCuenta,
                   R.FechaConciliacion,
                   R.SaldoEstadoCuenta
            FROM dbo.Conciliaciones R
            INNER JOIN dbo.CuentasBancarias C ON C.ID_Cuenta = R.ID_Cuenta_FK
            WHERE R.ID_Cuenta_FK = @accountId
              AND R.ID_Tenant_FK = @tenantId
            ORDER BY R.FechaConciliacion DESC, R.ID_Conciliacion DESC;
            """;

        var rows = new List<ReconciliationSummary>();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = accountId;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ReconciliationSummary(
                reader.GetInt32(reader.GetOrdinal("ID_Conciliacion")),
                reader.GetInt32(reader.GetOrdinal("ID_Cuenta_FK")),
                reader.GetString(reader.GetOrdinal("NombreCuenta")),
                reader.GetDateTime(reader.GetOrdinal("FechaConciliacion")),
                reader.GetDecimal(reader.GetOrdinal("SaldoEstadoCuenta"))));
        }

        return rows;
    }

    private static async Task<decimal> GetSelectedTotalAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ReconciliationEntry entry,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var total = 0m;
        total += await SumDepositsAsync(connection, transaction, entry.DepositIds, entry.AccountId, entry.StatementDate, tenantId, cancellationToken);
        total += await SumTransactionsAsync(connection, transaction, entry.TransactionIds, entry.AccountId, entry.StatementDate, tenantId, cancellationToken);
        return total;
    }

    private static async Task<decimal> SumDepositsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IReadOnlyCollection<int> ids,
        int accountId,
        DateTime statementDate,
        int tenantId,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return 0;
        }

        var parameterNames = ids.Select((_, index) => $"@deposit{index}").ToList();
        var sql = $"""
            SELECT ISNULL(SUM(MontoTotal), 0)
            FROM dbo.Depositos
            WHERE ID_Deposito IN ({string.Join(", ", parameterNames)})
              AND ID_Cuenta_FK = @accountId
              AND ID_Tenant_FK = @tenantId
              AND FechaDeposito <= @statementDate
              AND ISNULL(Anulado, 0) = 0
              AND ISNULL(Conciliado, 0) = 0;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@statementDate", SqlDbType.Date).Value = statementDate.Date;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        AddIdParameters(command, parameterNames, ids);
        return Convert.ToDecimal(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<decimal> SumTransactionsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IReadOnlyCollection<int> ids,
        int accountId,
        DateTime statementDate,
        int tenantId,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return 0;
        }

        var parameterNames = ids.Select((_, index) => $"@transaction{index}").ToList();
        var sql = $"""
            SELECT ISNULL(SUM(CASE WHEN C.TipoCategoria = 'Egreso' THEN -T.Monto ELSE T.Monto END), 0)
            FROM dbo.Transacciones T
            INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
            INNER JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
            WHERE T.ID_Transaccion IN ({string.Join(", ", parameterNames)})
              AND T.ID_Cuenta_FK = @accountId
              AND T.ID_Tenant_FK = @tenantId
              AND T.Fecha <= @statementDate
              AND ISNULL(T.Anulada, 0) = 0
              AND ISNULL(T.Conciliada, 0) = 0
              AND (
                    C.TipoCategoria = 'Egreso'
                    OR (
                        C.TipoCategoria = 'Ingreso'
                        AND T.ID_Deposito_FK IS NULL
                        AND T.MetodoPago NOT IN ('Efectivo', 'Cheque')
                    )
              );
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@statementDate", SqlDbType.Date).Value = statementDate.Date;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        AddIdParameters(command, parameterNames, ids);
        return Convert.ToDecimal(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task MarkDepositsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IReadOnlyCollection<int> ids,
        int accountId,
        DateTime statementDate,
        int tenantId,
        int reconciliationId,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var parameterNames = ids.Select((_, index) => $"@deposit{index}").ToList();
        var sql = $"""
            UPDATE dbo.Depositos
            SET Conciliado = 1,
                ID_Conciliacion_FK = @reconciliationId
            WHERE ID_Deposito IN ({string.Join(", ", parameterNames)})
              AND ID_Cuenta_FK = @accountId
              AND ID_Tenant_FK = @tenantId
              AND FechaDeposito <= @statementDate
              AND ISNULL(Anulado, 0) = 0
              AND ISNULL(Conciliado, 0) = 0;
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@statementDate", SqlDbType.Date).Value = statementDate.Date;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.Add("@reconciliationId", SqlDbType.Int).Value = reconciliationId;
        AddIdParameters(command, parameterNames, ids);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MarkTransactionsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IReadOnlyCollection<int> ids,
        int accountId,
        DateTime statementDate,
        int tenantId,
        int reconciliationId,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var parameterNames = ids.Select((_, index) => $"@transaction{index}").ToList();
        var sql = $"""
            UPDATE T
            SET Conciliada = 1,
                ID_Conciliacion_FK = @reconciliationId
            FROM dbo.Transacciones T
            INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
            INNER JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
            WHERE T.ID_Transaccion IN ({string.Join(", ", parameterNames)})
              AND T.ID_Cuenta_FK = @accountId
              AND T.ID_Tenant_FK = @tenantId
              AND T.Fecha <= @statementDate
              AND ISNULL(T.Anulada, 0) = 0
              AND ISNULL(T.Conciliada, 0) = 0
              AND (
                    C.TipoCategoria = 'Egreso'
                    OR (
                        C.TipoCategoria = 'Ingreso'
                        AND T.ID_Deposito_FK IS NULL
                        AND T.MetodoPago NOT IN ('Efectivo', 'Cheque')
                    )
              );
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@statementDate", SqlDbType.Date).Value = statementDate.Date;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.Add("@reconciliationId", SqlDbType.Int).Value = reconciliationId;
        AddIdParameters(command, parameterNames, ids);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddIdParameters(SqlCommand command, IReadOnlyList<string> names, IEnumerable<int> ids)
    {
        var index = 0;
        foreach (var id in ids)
        {
            command.Parameters.Add(names[index++], SqlDbType.Int).Value = id;
        }
    }

    private sealed record ReconciliationStartingPoint(decimal Balance, DateTime? LastStatementDate);
}
