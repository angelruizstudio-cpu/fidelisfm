using System.Data;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlCheckRepository(SqlConnectionFactory connectionFactory, ITenantContext tenantContext) : ICheckRepository
{
    private readonly int _tenantId = tenantContext.TenantId;

    public async Task<CheckLookups> GetLookupsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var accounts = new List<LookupOption>();
        await using (var command = new SqlCommand(
            "SELECT ID_Cuenta, NombreCuenta FROM dbo.CuentasBancarias WHERE ID_Tenant_FK = @tenantId ORDER BY NombreCuenta;",
            connection))
        {
            command.Parameters.AddWithValue("@tenantId", _tenantId);
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    accounts.Add(new LookupOption(reader.GetInt32(0), reader.GetString(1)));
                }
            }
        }

        var pendingExpenses = new List<CheckExpenseOption>();
        const string expensesSql = """
            SELECT TOP 100
                   T.ID_Transaccion,
                   T.Fecha,
                   ISNULL(T.Descripcion, '') AS Descripcion,
                   T.Monto,
                   T.ID_Cuenta_FK,
                   B.NombreCuenta,
                   T.NumeroCheque,
                   S.NombreSubcategoria
            FROM dbo.Transacciones T
            INNER JOIN dbo.CuentasBancarias B ON B.ID_Cuenta = T.ID_Cuenta_FK
            INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
            INNER JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
            WHERE C.TipoCategoria = 'Egreso'
              AND T.MetodoPago = 'Cheque'
              AND T.ID_Tenant_FK = @tenantId
              AND ISNULL(T.Anulada, 0) = 0
              AND NOT EXISTS (
                  SELECT 1
                  FROM dbo.CFS_Cheques Ch
                  WHERE Ch.EgresoId = T.ID_Transaccion
                    AND Ch.Estado <> 'Anulado'
                    AND Ch.ID_Tenant_FK = @tenantId
              )
            ORDER BY T.Fecha DESC, T.ID_Transaccion DESC;
            """;

        await using (var command = new SqlCommand(expensesSql, connection))
        {
            command.Parameters.AddWithValue("@tenantId", _tenantId);
            await using (var expenseReader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await expenseReader.ReadAsync(cancellationToken))
                {
                    pendingExpenses.Add(new CheckExpenseOption(
                        expenseReader.GetInt32(expenseReader.GetOrdinal("ID_Transaccion")),
                        expenseReader.GetDateTime(expenseReader.GetOrdinal("Fecha")),
                        expenseReader.GetString(expenseReader.GetOrdinal("Descripcion")),
                        expenseReader.GetDecimal(expenseReader.GetOrdinal("Monto")),
                        expenseReader.GetInt32(expenseReader.GetOrdinal("ID_Cuenta_FK")),
                        expenseReader.GetString(expenseReader.GetOrdinal("NombreCuenta")),
                        expenseReader["NumeroCheque"] is DBNull ? null : expenseReader.GetString(expenseReader.GetOrdinal("NumeroCheque")),
                        expenseReader.GetString(expenseReader.GetOrdinal("NombreSubcategoria"))));
                }
            }
        }

        return new CheckLookups(accounts, pendingExpenses);
    }

    public async Task<IReadOnlyList<CheckVoucher>> GetRecentAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP 100
                   C.Id,
                   C.EgresoId,
                   C.CuentaBancariaId,
                   B.NombreCuenta,
                   C.NumeroCheque,
                   C.FechaCheque,
                   C.Beneficiario,
                   C.DireccionBeneficiario,
                   C.Monto,
                   C.Memo,
                   C.Estado,
                   C.CreatedAt,
                   C.CreatedBy,
                   C.PrintedAt,
                   C.PrintedBy,
                   C.VoidedAt,
                   C.VoidedBy,
                   C.VoidReason
            FROM dbo.CFS_Cheques C
            INNER JOIN dbo.CuentasBancarias B ON B.ID_Cuenta = C.CuentaBancariaId
            WHERE C.ID_Tenant_FK = @tenantId
            ORDER BY C.FechaCheque DESC, C.Id DESC;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        return await ReadChecksAsync(command, cancellationToken);
    }

    public async Task<CheckVoucher?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP 1
                   C.Id,
                   C.EgresoId,
                   C.CuentaBancariaId,
                   B.NombreCuenta,
                   C.NumeroCheque,
                   C.FechaCheque,
                   C.Beneficiario,
                   C.DireccionBeneficiario,
                   C.Monto,
                   C.Memo,
                   C.Estado,
                   C.CreatedAt,
                   C.CreatedBy,
                   C.PrintedAt,
                   C.PrintedBy,
                   C.VoidedAt,
                   C.VoidedBy,
                   C.VoidReason
            FROM dbo.CFS_Cheques C
            INNER JOIN dbo.CuentasBancarias B ON B.ID_Cuenta = C.CuentaBancariaId
            WHERE C.Id = @id
              AND C.ID_Tenant_FK = @tenantId;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        var rows = await ReadChecksAsync(command, cancellationToken);
        return rows.FirstOrDefault();
    }

    public async Task<CheckSaveResult> SaveDraftAsync(
        CheckEntry entry,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(entry);
        if (validation is not null)
        {
            return new CheckSaveResult(false, null, validation);
        }

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        if (await HasDuplicateNumberAsync(connection, entry, _tenantId, cancellationToken))
        {
            return new CheckSaveResult(false, null, "Ya existe un cheque activo con ese numero en la cuenta seleccionada.");
        }

        if (entry.Id <= 0)
        {
            const string insertSql = """
                INSERT INTO dbo.CFS_Cheques
                    (EgresoId, CuentaBancariaId, NumeroCheque, FechaCheque, Beneficiario,
                     DireccionBeneficiario, Monto, Memo, Estado, CreatedAt, CreatedBy, ID_Tenant_FK)
                VALUES
                    (@expenseId, @accountId, @checkNumber, @checkDate, @payee,
                     @payeeAddress, @amount, @memo, 'Borrador', SYSUTCDATETIME(), @user, @tenantId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """;

            await using var command = new SqlCommand(insertSql, connection);
            AddEntryParameters(command, entry);
            command.Parameters.Add("@user", SqlDbType.NVarChar, 100).Value = userName;
            command.Parameters.AddWithValue("@tenantId", _tenantId);
            var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            await AuditLogger.TryLogAsync(connectionFactory, _tenantId, userName, "CREAR", "Cheque", id.ToString(), $"Cheque #{entry.CheckNumber} creado. Monto: {entry.Amount}", cancellationToken);
            return new CheckSaveResult(true, id, null);
        }

        const string updateSql = """
            UPDATE dbo.CFS_Cheques
            SET EgresoId = @expenseId,
                CuentaBancariaId = @accountId,
                NumeroCheque = @checkNumber,
                FechaCheque = @checkDate,
                Beneficiario = @payee,
                DireccionBeneficiario = @payeeAddress,
                Monto = @amount,
                Memo = @memo
            WHERE Id = @id
              AND ID_Tenant_FK = @tenantId
              AND Estado = 'Borrador';
            """;

        await using (var command = new SqlCommand(updateSql, connection))
        {
            AddEntryParameters(command, entry);
            command.Parameters.Add("@id", SqlDbType.Int).Value = entry.Id;
            command.Parameters.AddWithValue("@tenantId", _tenantId);
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
            {
                return new CheckSaveResult(false, null, "Solo se pueden editar cheques en borrador.");
            }
        }

        return new CheckSaveResult(true, entry.Id, null);
    }

    public async Task<CheckSaveResult> MarkPrintedAsync(
        int id,
        string userName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.CFS_Cheques
            SET Estado = 'Impreso',
                PrintedAt = SYSUTCDATETIME(),
                PrintedBy = @user
            WHERE Id = @id
              AND ID_Tenant_FK = @tenantId
              AND Estado <> 'Anulado';
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        command.Parameters.Add("@user", SqlDbType.NVarChar, 100).Value = userName;
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            return new CheckSaveResult(false, null, "No se pudo marcar el cheque como impreso.");
        }

        await AuditLogger.TryLogAsync(connectionFactory, _tenantId, userName, "IMPRIMIR", "Cheque", id.ToString(), "Cheque marcado como impreso.", cancellationToken);
        return new CheckSaveResult(true, id, null);
    }

    public async Task<CheckSaveResult> VoidAsync(
        int id,
        string reason,
        string userName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new CheckSaveResult(false, null, "Debes especificar un motivo de anulacion.");
        }

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.CFS_Cheques
            SET Estado = 'Anulado',
                VoidedAt = SYSUTCDATETIME(),
                VoidedBy = @user,
                VoidReason = @reason
            WHERE Id = @id
              AND ID_Tenant_FK = @tenantId
              AND Estado <> 'Anulado';
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        command.Parameters.Add("@user", SqlDbType.NVarChar, 100).Value = userName;
        command.Parameters.Add("@reason", SqlDbType.NVarChar, 300).Value = reason.Trim();
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            return new CheckSaveResult(false, null, "No se pudo anular el cheque.");
        }

        await AuditLogger.TryLogAsync(connectionFactory, _tenantId, userName, "ANULAR", "Cheque", id.ToString(), $"Cheque anulado. Motivo: {reason}", cancellationToken);
        return new CheckSaveResult(true, id, null);
    }

    private static string? Validate(CheckEntry entry)
    {
        if (entry.AccountId <= 0) return "Selecciona una cuenta bancaria.";
        if (string.IsNullOrWhiteSpace(entry.CheckNumber)) return "El numero de cheque es requerido.";
        if (string.IsNullOrWhiteSpace(entry.Payee)) return "El beneficiario es requerido.";
        if (entry.Amount <= 0) return "El monto debe ser mayor que cero.";
        return null;
    }

    private static async Task<bool> HasDuplicateNumberAsync(
        SqlConnection connection,
        CheckEntry entry,
        int tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM dbo.CFS_Cheques
            WHERE CuentaBancariaId = @accountId
              AND NumeroCheque = @checkNumber
              AND ID_Tenant_FK = @tenantId
              AND Estado <> 'Anulado'
              AND (@id <= 0 OR Id <> @id);
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = entry.AccountId;
        command.Parameters.Add("@checkNumber", SqlDbType.NVarChar, 50).Value = entry.CheckNumber.Trim();
        command.Parameters.Add("@id", SqlDbType.Int).Value = entry.Id;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static void AddEntryParameters(SqlCommand command, CheckEntry entry)
    {
        command.Parameters.Add("@expenseId", SqlDbType.Int).Value = entry.ExpenseId.HasValue ? entry.ExpenseId.Value : DBNull.Value;
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = entry.AccountId;
        command.Parameters.Add("@checkNumber", SqlDbType.NVarChar, 50).Value = entry.CheckNumber.Trim();
        command.Parameters.Add("@checkDate", SqlDbType.Date).Value = entry.CheckDate.Date;
        command.Parameters.Add("@payee", SqlDbType.NVarChar, 200).Value = entry.Payee.Trim();
        command.Parameters.Add("@payeeAddress", SqlDbType.NVarChar, 300).Value = string.IsNullOrWhiteSpace(entry.PayeeAddress)
            ? DBNull.Value
            : entry.PayeeAddress.Trim();
        command.Parameters.Add("@amount", SqlDbType.Money).Value = entry.Amount;
        command.Parameters.Add("@memo", SqlDbType.NVarChar, 300).Value = string.IsNullOrWhiteSpace(entry.Memo)
            ? DBNull.Value
            : entry.Memo.Trim();
    }

    private static async Task<IReadOnlyList<CheckVoucher>> ReadChecksAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        var rows = new List<CheckVoucher>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var amount = reader.GetDecimal(reader.GetOrdinal("Monto"));
            rows.Add(new CheckVoucher(
                reader.GetInt32(reader.GetOrdinal("Id")),
                reader["EgresoId"] is DBNull ? null : reader.GetInt32(reader.GetOrdinal("EgresoId")),
                reader.GetInt32(reader.GetOrdinal("CuentaBancariaId")),
                reader.GetString(reader.GetOrdinal("NombreCuenta")),
                reader.GetString(reader.GetOrdinal("NumeroCheque")),
                reader.GetDateTime(reader.GetOrdinal("FechaCheque")),
                reader.GetString(reader.GetOrdinal("Beneficiario")),
                reader["DireccionBeneficiario"] is DBNull ? null : reader.GetString(reader.GetOrdinal("DireccionBeneficiario")),
                amount,
                SpanishMoneyWriter.ToDollars(amount),
                reader["Memo"] is DBNull ? null : reader.GetString(reader.GetOrdinal("Memo")),
                reader.GetString(reader.GetOrdinal("Estado")),
                reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                reader.GetString(reader.GetOrdinal("CreatedBy")),
                reader["PrintedAt"] is DBNull ? null : reader.GetDateTime(reader.GetOrdinal("PrintedAt")),
                reader["PrintedBy"] is DBNull ? null : reader.GetString(reader.GetOrdinal("PrintedBy")),
                reader["VoidedAt"] is DBNull ? null : reader.GetDateTime(reader.GetOrdinal("VoidedAt")),
                reader["VoidedBy"] is DBNull ? null : reader.GetString(reader.GetOrdinal("VoidedBy")),
                reader["VoidReason"] is DBNull ? null : reader.GetString(reader.GetOrdinal("VoidReason"))));
        }

        return rows;
    }
}
