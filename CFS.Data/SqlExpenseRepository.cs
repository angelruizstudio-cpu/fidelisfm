using System.Data;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlExpenseRepository(SqlConnectionFactory connectionFactory, ITenantContext tenantContext) : IExpenseRepository
{
    private readonly int _tenantId = tenantContext.TenantId;

    private static readonly string[] DefaultPaymentMethods =
    [
        "Cheque",
        "ACH",
        "Tarjeta",
        "Transferencia",
        "Efectivo",
        "Zelle"
    ];

    public async Task<ExpenseLookups> GetLookupsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var accounts = await LoadOptionsAsync(
            connection,
            "SELECT ID_Cuenta, NombreCuenta FROM dbo.CuentasBancarias WHERE ID_Tenant_FK = @tenantId ORDER BY NombreCuenta;",
            _tenantId,
            cancellationToken);

        var subcategories = await LoadOptionsAsync(
            connection,
            """
            SELECT S.ID_Subcategoria, S.NombreSubcategoria
            FROM dbo.Subcategorias S
            INNER JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
            WHERE C.TipoCategoria = 'Egreso'
              AND S.ID_Tenant_FK = @tenantId
            ORDER BY C.NombreCategoria, S.NombreSubcategoria;
            """,
            _tenantId,
            cancellationToken);

        var paymentMethods = await LoadPaymentMethodsAsync(connection, _tenantId, cancellationToken);
        return new ExpenseLookups(accounts, subcategories, paymentMethods);
    }

    public async Task<IReadOnlyList<ExpenseTransaction>> GetRecentAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var start = startDate?.Date ?? DateTime.Today.AddDays(-30);
        var end = (endDate?.Date ?? DateTime.Today).AddDays(1);

        const string sql = """
            SELECT TOP 500
                   T.ID_Transaccion,
                   T.Fecha,
                   ISNULL(T.Descripcion, '') AS Descripcion,
                   T.Monto,
                   T.ID_Cuenta_FK,
                   Cta.NombreCuenta,
                   T.ID_Subcategoria_FK,
                   S.NombreSubcategoria,
                   T.MetodoPago,
                   T.NumeroCheque,
                   ISNULL(T.Conciliada, 0) AS Conciliada,
                   ISNULL(T.Anulada, 0) AS Anulada
            FROM dbo.Transacciones T
            INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
            INNER JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
            INNER JOIN dbo.CuentasBancarias Cta ON Cta.ID_Cuenta = T.ID_Cuenta_FK
            WHERE C.TipoCategoria = 'Egreso'
              AND T.ID_Tenant_FK = @tenantId
              AND T.Fecha >= @start
              AND T.Fecha < @end
            ORDER BY T.Fecha DESC, T.ID_Transaccion DESC;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        command.Parameters.Add("@start", SqlDbType.Date).Value = start;
        command.Parameters.Add("@end", SqlDbType.Date).Value = end;
        return await ReadExpenseTransactionsAsync(command, cancellationToken);
    }

    public async Task<ExpenseTransaction?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP 1
                   T.ID_Transaccion,
                   T.Fecha,
                   ISNULL(T.Descripcion, '') AS Descripcion,
                   T.Monto,
                   T.ID_Cuenta_FK,
                   Cta.NombreCuenta,
                   T.ID_Subcategoria_FK,
                   S.NombreSubcategoria,
                   T.MetodoPago,
                   T.NumeroCheque,
                   ISNULL(T.Conciliada, 0) AS Conciliada,
                   ISNULL(T.Anulada, 0) AS Anulada
            FROM dbo.Transacciones T
            INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
            INNER JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
            INNER JOIN dbo.CuentasBancarias Cta ON Cta.ID_Cuenta = T.ID_Cuenta_FK
            WHERE T.ID_Transaccion = @id
              AND T.ID_Tenant_FK = @tenantId
              AND C.TipoCategoria = 'Egreso';
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        var rows = await ReadExpenseTransactionsAsync(command, cancellationToken);
        return rows.FirstOrDefault();
    }

    public async Task<ExpenseSaveResult> SaveAsync(
        ExpenseEntry entry,
        string userName,
        bool ignorePossibleDuplicate = false,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(entry);
        if (validation is not null)
        {
            return new ExpenseSaveResult(false, null, false, validation);
        }

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var subcategoryName = await GetSubcategoryNameAsync(connection, transaction, entry.SubcategoryId, _tenantId, cancellationToken);
            if (subcategoryName is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ExpenseSaveResult(false, null, false, "La subcategoría seleccionada no existe o no es de egreso.");
            }

            if (entry.Id <= 0 && !ignorePossibleDuplicate &&
                await HasPossibleDuplicateAsync(connection, transaction, entry, _tenantId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ExpenseSaveResult(false, null, true, "Posible duplicado detectado.");
            }

            if (await HasDuplicateCheckAsync(connection, transaction, entry, _tenantId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ExpenseSaveResult(false, null, false, "Ya existe una transacción activa con ese número de cheque.");
            }

            var savedId = entry.Id <= 0
                ? await InsertExpenseAsync(connection, transaction, entry, subcategoryName, userName, _tenantId, cancellationToken)
                : await UpdateExpenseAsync(connection, transaction, entry, userName, _tenantId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new ExpenseSaveResult(true, savedId, false, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ExpenseSaveResult(false, null, false, ex.Message);
        }
    }

    public async Task<ExpenseSaveResult> VoidAsync(
        int id,
        string reason,
        string userName,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return new ExpenseSaveResult(false, null, false, "ID de transacción inválido.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return new ExpenseSaveResult(false, null, false, "Debes especificar un motivo de anulación.");
        }

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string getSql = """
                SELECT T.Monto,
                       T.ID_Cuenta_FK,
                       ISNULL(T.Anulada, 0) AS Anulada,
                       ISNULL(T.Conciliada, 0) AS Conciliada
                FROM dbo.Transacciones T
                INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
                INNER JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
                WHERE T.ID_Transaccion = @id
                  AND T.ID_Tenant_FK = @tenantId
                  AND C.TipoCategoria = 'Egreso';
                """;

            decimal amount;
            int accountId;
            bool isVoided;
            bool isReconciled;

            await using (var command = new SqlCommand(getSql, connection, transaction))
            {
                command.Parameters.Add("@id", SqlDbType.Int).Value = id;
                command.Parameters.AddWithValue("@tenantId", _tenantId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new ExpenseSaveResult(false, null, false, "No se encontró el egreso.");
                }

                amount = reader.GetDecimal(reader.GetOrdinal("Monto"));
                accountId = reader.GetInt32(reader.GetOrdinal("ID_Cuenta_FK"));
                isVoided = reader.GetBoolean(reader.GetOrdinal("Anulada"));
                isReconciled = reader.GetBoolean(reader.GetOrdinal("Conciliada"));
            }

            if (isVoided)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ExpenseSaveResult(false, null, false, "El egreso ya está anulado.");
            }

            if (isReconciled)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ExpenseSaveResult(false, null, false, "No se puede anular un egreso ya conciliado.");
            }

            await AdjustAccountBalanceAsync(connection, transaction, accountId, amount, cancellationToken);

            const string updateSql = """
                UPDATE dbo.Transacciones
                SET Anulada = 1,
                    FechaAnulacion = GETDATE(),
                    UsuarioAnulacion = @user,
                    MotivoAnulacion = @reason
                WHERE ID_Transaccion = @id
                  AND ID_Tenant_FK = @tenantId;
                """;

            await using (var command = new SqlCommand(updateSql, connection, transaction))
            {
                command.Parameters.Add("@user", SqlDbType.NVarChar, 100).Value = userName;
                command.Parameters.Add("@reason", SqlDbType.NVarChar, 255).Value = reason.Trim();
                command.Parameters.Add("@id", SqlDbType.Int).Value = id;
                command.Parameters.AddWithValue("@tenantId", _tenantId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await InsertLogAsync(connection, transaction, id, "ANULAR", userName, $"Egreso anulado. Motivo: {reason}", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ExpenseSaveResult(true, id, false, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ExpenseSaveResult(false, null, false, ex.Message);
        }
    }

    private static string? Validate(ExpenseEntry entry)
    {
        if (entry.Amount <= 0) return "El monto debe ser mayor que cero.";
        if (entry.AccountId <= 0) return "Selecciona una cuenta bancaria.";
        if (entry.SubcategoryId <= 0) return "Selecciona una subcategoría.";
        if (string.IsNullOrWhiteSpace(entry.Description)) return "La descripción es requerida.";
        if (string.IsNullOrWhiteSpace(entry.PaymentMethod)) return "Selecciona un método de pago.";
        if (entry.PaymentMethod.Length > 20) return "El método de pago no puede exceder 20 caracteres.";
        if (entry.CheckNumber?.Length > 50) return "El número de cheque no puede exceder 50 caracteres.";
        return null;
    }

    private static async Task<IReadOnlyList<LookupOption>> LoadOptionsAsync(
        SqlConnection connection,
        string sql,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var options = new List<LookupOption>();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tenantId", tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            options.Add(new LookupOption(reader.GetInt32(0), reader.GetString(1)));
        }

        return options;
    }

    private static async Task<IReadOnlyList<string>> LoadPaymentMethodsAsync(
        SqlConnection connection,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var methods = new SortedSet<string>(DefaultPaymentMethods, StringComparer.OrdinalIgnoreCase);
        await using var command = new SqlCommand(
            """
            SELECT DISTINCT T.MetodoPago
            FROM dbo.Transacciones T
            INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
            INNER JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
            WHERE C.TipoCategoria = 'Egreso'
              AND T.MetodoPago IS NOT NULL
              AND LTRIM(RTRIM(T.MetodoPago)) <> ''
              AND T.ID_Tenant_FK = @tenantId
            ORDER BY T.MetodoPago;
            """,
            connection);

        command.Parameters.AddWithValue("@tenantId", tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            methods.Add(reader.GetString(0));
        }

        return methods.ToList();
    }

    private static async Task<IReadOnlyList<ExpenseTransaction>> ReadExpenseTransactionsAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        var rows = new List<ExpenseTransaction>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ExpenseTransaction(
                reader.GetInt32(reader.GetOrdinal("ID_Transaccion")),
                reader.GetDateTime(reader.GetOrdinal("Fecha")),
                reader.GetString(reader.GetOrdinal("Descripcion")),
                reader.GetDecimal(reader.GetOrdinal("Monto")),
                reader.GetInt32(reader.GetOrdinal("ID_Cuenta_FK")),
                reader.GetString(reader.GetOrdinal("NombreCuenta")),
                reader.GetInt32(reader.GetOrdinal("ID_Subcategoria_FK")),
                reader.GetString(reader.GetOrdinal("NombreSubcategoria")),
                reader.GetString(reader.GetOrdinal("MetodoPago")),
                reader["NumeroCheque"] is DBNull ? null : reader.GetString(reader.GetOrdinal("NumeroCheque")),
                reader.GetBoolean(reader.GetOrdinal("Conciliada")),
                reader.GetBoolean(reader.GetOrdinal("Anulada"))));
        }

        return rows;
    }

    private static async Task<string?> GetSubcategoryNameAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int subcategoryId,
        int tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT S.NombreSubcategoria
            FROM dbo.Subcategorias S
            INNER JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
            WHERE S.ID_Subcategoria = @id
              AND S.ID_Tenant_FK = @tenantId
              AND C.TipoCategoria = 'Egreso';
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id", SqlDbType.Int).Value = subcategoryId;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private static async Task<bool> HasPossibleDuplicateAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ExpenseEntry entry,
        int tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM dbo.Transacciones T
            INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
            INNER JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
            WHERE C.TipoCategoria = 'Egreso'
              AND T.Fecha = @date
              AND T.Monto = @amount
              AND T.ID_Subcategoria_FK = @subcategoryId
              AND ISNULL(T.Anulada, 0) = 0
              AND T.ID_Tenant_FK = @tenantId
              AND LTRIM(RTRIM(ISNULL(T.Descripcion, ''))) = @description;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@date", SqlDbType.Date).Value = entry.Date.Date;
        command.Parameters.Add("@amount", SqlDbType.Money).Value = entry.Amount;
        command.Parameters.Add("@subcategoryId", SqlDbType.Int).Value = entry.SubcategoryId;
        command.Parameters.Add("@description", SqlDbType.VarChar, 300).Value = entry.Description.Trim();
        command.Parameters.AddWithValue("@tenantId", tenantId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<bool> HasDuplicateCheckAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ExpenseEntry entry,
        int tenantId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entry.CheckNumber) ||
            !entry.PaymentMethod.Trim().Equals("Cheque", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        const string sql = """
            SELECT COUNT(*)
            FROM dbo.Transacciones
            WHERE NumeroCheque = @checkNumber
              AND ISNULL(Anulada, 0) = 0
              AND ID_Tenant_FK = @tenantId
              AND (@currentId <= 0 OR ID_Transaccion <> @currentId);
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@checkNumber", SqlDbType.VarChar, 50).Value = entry.CheckNumber.Trim();
        command.Parameters.Add("@currentId", SqlDbType.Int).Value = entry.Id;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<int> InsertExpenseAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ExpenseEntry entry,
        string subcategoryName,
        string userName,
        int tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.Transacciones
                (Fecha, Descripcion, Monto, ID_Cuenta_FK, ID_Subcategoria_FK, ID_Miembro_FK, MetodoPago, NumeroCheque, ID_Tenant_FK)
            VALUES
                (@date, @description, @amount, @accountId, @subcategoryId, NULL, @paymentMethod, @checkNumber, @tenantId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        AddEntryParameters(command, entry);
        command.Parameters.AddWithValue("@tenantId", tenantId);
        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

        await AdjustAccountBalanceAsync(connection, transaction, entry.AccountId, -entry.Amount, cancellationToken);
        await InsertLogAsync(
            connection,
            transaction,
            id,
            "CREAR",
            userName,
            $"Nuevo egreso creado. Monto: {entry.Amount}, Subcategoria: {subcategoryName}",
            cancellationToken);

        return id;
    }

    private static async Task<int> UpdateExpenseAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ExpenseEntry entry,
        string userName,
        int tenantId,
        CancellationToken cancellationToken)
    {
        const string getSql = """
            SELECT T.Monto,
                   T.ID_Cuenta_FK,
                   ISNULL(T.Conciliada, 0) AS Conciliada,
                   ISNULL(T.Anulada, 0) AS Anulada
            FROM dbo.Transacciones T
            INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
            INNER JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
            WHERE T.ID_Transaccion = @id
              AND T.ID_Tenant_FK = @tenantId
              AND C.TipoCategoria = 'Egreso';
            """;

        decimal oldAmount;
        int oldAccountId;
        bool isReconciled;
        bool isVoided;

        await using (var command = new SqlCommand(getSql, connection, transaction))
        {
            command.Parameters.Add("@id", SqlDbType.Int).Value = entry.Id;
            command.Parameters.AddWithValue("@tenantId", tenantId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("No se encontró el egreso para actualizar.");
            }

            oldAmount = reader.GetDecimal(reader.GetOrdinal("Monto"));
            oldAccountId = reader.GetInt32(reader.GetOrdinal("ID_Cuenta_FK"));
            isReconciled = reader.GetBoolean(reader.GetOrdinal("Conciliada"));
            isVoided = reader.GetBoolean(reader.GetOrdinal("Anulada"));
        }

        if (isVoided) throw new InvalidOperationException("No se puede editar un egreso anulado.");
        if (isReconciled) throw new InvalidOperationException("No se puede editar un egreso ya conciliado.");

        const string updateSql = """
            UPDATE dbo.Transacciones
            SET Fecha = @date,
                Descripcion = @description,
                Monto = @amount,
                ID_Cuenta_FK = @accountId,
                ID_Subcategoria_FK = @subcategoryId,
                ID_Miembro_FK = NULL,
                MetodoPago = @paymentMethod,
                NumeroCheque = @checkNumber
            WHERE ID_Transaccion = @id
              AND ID_Tenant_FK = @tenantId;
            """;

        await using (var command = new SqlCommand(updateSql, connection, transaction))
        {
            AddEntryParameters(command, entry);
            command.Parameters.Add("@id", SqlDbType.Int).Value = entry.Id;
            command.Parameters.AddWithValue("@tenantId", tenantId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await AdjustAccountBalanceAsync(connection, transaction, oldAccountId, oldAmount, cancellationToken);
        await AdjustAccountBalanceAsync(connection, transaction, entry.AccountId, -entry.Amount, cancellationToken);
        await InsertLogAsync(connection, transaction, entry.Id, "EDITAR", userName, $"Egreso actualizado. Nuevo monto: {entry.Amount}", cancellationToken);

        return entry.Id;
    }

    private static void AddEntryParameters(SqlCommand command, ExpenseEntry entry)
    {
        command.Parameters.Add("@date", SqlDbType.Date).Value = entry.Date.Date;
        command.Parameters.Add("@description", SqlDbType.VarChar, 300).Value = entry.Description.Trim();
        command.Parameters.Add("@amount", SqlDbType.Money).Value = entry.Amount;
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = entry.AccountId;
        command.Parameters.Add("@subcategoryId", SqlDbType.Int).Value = entry.SubcategoryId;
        command.Parameters.Add("@paymentMethod", SqlDbType.VarChar, 20).Value = entry.PaymentMethod.Trim();
        command.Parameters.Add("@checkNumber", SqlDbType.VarChar, 50).Value = string.IsNullOrWhiteSpace(entry.CheckNumber)
            ? DBNull.Value
            : entry.CheckNumber.Trim();
    }

    private static async Task AdjustAccountBalanceAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int accountId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "UPDATE dbo.CuentasBancarias SET SaldoActual = SaldoActual + @amount WHERE ID_Cuenta = @accountId;",
            connection,
            transaction);
        command.Parameters.Add("@amount", SqlDbType.Money).Value = amount;
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = accountId;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertLogAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int transactionId,
        string action,
        string userName,
        string detail,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.Transacciones_Log (ID_Transaccion, Accion, Usuario, Detalle)
            VALUES (@id, @action, @user, @detail);
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id", SqlDbType.Int).Value = transactionId;
        command.Parameters.Add("@action", SqlDbType.VarChar, 50).Value = action;
        command.Parameters.Add("@user", SqlDbType.VarChar, 100).Value = userName;
        command.Parameters.Add("@detail", SqlDbType.NVarChar).Value = detail;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
