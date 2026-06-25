using System.Data;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlIncomeRepository(SqlConnectionFactory connectionFactory, ITenantContext tenantContext) : IIncomeRepository
{
    private readonly int _tenantId = tenantContext.TenantId;

    private static readonly string[] DefaultPaymentMethods =
    [
        "Efectivo",
        "Cheque",
        "Zelle",
        "ACH",
        "Tarjeta",
        "Transferencia"
    ];

    public async Task<IncomeLookups> GetLookupsAsync(CancellationToken cancellationToken = default)
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
            WHERE C.TipoCategoria = 'Ingreso'
              AND S.ID_Tenant_FK = @tenantId
            ORDER BY S.NombreSubcategoria;
            """,
            _tenantId,
            cancellationToken);

        var members = await LoadOptionsAsync(
            connection,
            """
            SELECT ID_Miembro,
                   LTRIM(RTRIM(Nombre)) + CASE WHEN LTRIM(RTRIM(Apellido)) <> '' THEN ' ' + LTRIM(RTRIM(Apellido)) ELSE '' END
            FROM dbo.Miembros
            WHERE ID_Tenant_FK = @tenantId
            ORDER BY LTRIM(RTRIM(Nombre)), LTRIM(RTRIM(Apellido));
            """,
            _tenantId,
            cancellationToken);

        var paymentMethods = await LoadPaymentMethodsAsync(connection, _tenantId, cancellationToken);
        return new IncomeLookups(accounts, subcategories, members, paymentMethods);
    }

    public async Task<IReadOnlyList<IncomeTransaction>> GetRecentAsync(
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
                   T.ID_Miembro_FK,
                   CASE WHEN M.ID_Miembro IS NULL THEN NULL
                        ELSE LTRIM(RTRIM(M.Nombre)) + CASE WHEN LTRIM(RTRIM(M.Apellido)) <> '' THEN ' ' + LTRIM(RTRIM(M.Apellido)) ELSE '' END
                   END AS Miembro,
                   T.MetodoPago,
                   T.NumeroCheque,
                   CASE WHEN T.ID_Deposito_FK IS NULL THEN 0 ELSE 1 END AS Depositada,
                   ISNULL(T.Conciliada, 0) AS Conciliada,
                   ISNULL(T.Anulada, 0) AS Anulada
            FROM dbo.Transacciones T
            INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
            INNER JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
            INNER JOIN dbo.CuentasBancarias Cta ON Cta.ID_Cuenta = T.ID_Cuenta_FK
            LEFT JOIN dbo.Miembros M ON M.ID_Miembro = T.ID_Miembro_FK
            WHERE C.TipoCategoria = 'Ingreso'
              AND T.ID_Tenant_FK = @tenantId
              AND T.Fecha >= @start
              AND T.Fecha < @end
            ORDER BY T.Fecha DESC, T.ID_Transaccion DESC;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        command.Parameters.Add("@start", SqlDbType.Date).Value = start;
        command.Parameters.Add("@end", SqlDbType.Date).Value = end;

        return await ReadIncomeTransactionsAsync(command, cancellationToken);
    }

    public async Task<IncomeTransaction?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
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
                   T.ID_Miembro_FK,
                   CASE WHEN M.ID_Miembro IS NULL THEN NULL
                        ELSE LTRIM(RTRIM(M.Nombre)) + CASE WHEN LTRIM(RTRIM(M.Apellido)) <> '' THEN ' ' + LTRIM(RTRIM(M.Apellido)) ELSE '' END
                   END AS Miembro,
                   T.MetodoPago,
                   T.NumeroCheque,
                   CASE WHEN T.ID_Deposito_FK IS NULL THEN 0 ELSE 1 END AS Depositada,
                   ISNULL(T.Conciliada, 0) AS Conciliada,
                   ISNULL(T.Anulada, 0) AS Anulada
            FROM dbo.Transacciones T
            INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
            INNER JOIN dbo.Categorias C ON C.ID_Categoria = S.ID_Categoria_FK
            INNER JOIN dbo.CuentasBancarias Cta ON Cta.ID_Cuenta = T.ID_Cuenta_FK
            LEFT JOIN dbo.Miembros M ON M.ID_Miembro = T.ID_Miembro_FK
            WHERE T.ID_Transaccion = @id
              AND T.ID_Tenant_FK = @tenantId
              AND C.TipoCategoria = 'Ingreso';
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        var rows = await ReadIncomeTransactionsAsync(command, cancellationToken);
        return rows.FirstOrDefault();
    }

    public async Task<IncomeSaveResult> SaveAsync(
        IncomeEntry entry,
        string userName,
        bool ignorePossibleDuplicate = false,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(entry);
        if (validation is not null)
        {
            return new IncomeSaveResult(false, null, false, validation);
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
                return new IncomeSaveResult(false, null, false, "La subcategoría seleccionada no existe o no es de ingreso.");
            }

            if (IsTithe(subcategoryName) && entry.MemberId is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new IncomeSaveResult(false, null, false, "Selecciona un miembro para registrar diezmos.");
            }

            if (entry.Id <= 0 && !ignorePossibleDuplicate &&
                await HasPossibleDuplicateAsync(connection, transaction, entry, _tenantId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new IncomeSaveResult(false, null, true, "Posible duplicado detectado.");
            }

            if (await HasDuplicateCheckAsync(connection, transaction, entry, _tenantId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new IncomeSaveResult(false, null, false, "Ya existe una transacción activa con ese número de cheque.");
            }

            var accountId = entry.Id <= 0
                ? await ResolveAccountIdAsync(connection, transaction, subcategoryName, entry.AccountId, _tenantId, cancellationToken)
                : entry.AccountId;

            var savedId = entry.Id <= 0
                ? await InsertIncomeAsync(connection, transaction, entry, accountId, subcategoryName, userName, _tenantId, cancellationToken)
                : await UpdateIncomeAsync(connection, transaction, entry, accountId, userName, _tenantId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new IncomeSaveResult(true, savedId, false, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new IncomeSaveResult(false, null, false, ex.Message);
        }
    }

    public async Task<IncomeSaveResult> VoidAsync(
        int id,
        string reason,
        string userName,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return new IncomeSaveResult(false, null, false, "ID de transacción inválido.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return new IncomeSaveResult(false, null, false, "Debes especificar un motivo de anulación.");
        }

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string getSql = """
                SELECT Monto,
                       MetodoPago,
                       ID_Cuenta_FK,
                       ISNULL(Anulada, 0) AS Anulada,
                       ISNULL(Conciliada, 0) AS Conciliada,
                       CASE WHEN ID_Deposito_FK IS NULL THEN 0 ELSE 1 END AS Depositada
                FROM dbo.Transacciones
                WHERE ID_Transaccion = @id
                  AND ID_Tenant_FK = @tenantId;
                """;

            decimal amount;
            string paymentMethod;
            int accountId;
            bool isVoided;
            bool isReconciled;
            bool isDeposited;

            await using (var command = new SqlCommand(getSql, connection, transaction))
            {
                command.Parameters.Add("@id", SqlDbType.Int).Value = id;
                command.Parameters.AddWithValue("@tenantId", _tenantId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new IncomeSaveResult(false, null, false, "No se encontró la transacción.");
                }

                amount = reader.GetDecimal(reader.GetOrdinal("Monto"));
                paymentMethod = reader.GetString(reader.GetOrdinal("MetodoPago"));
                accountId = reader.GetInt32(reader.GetOrdinal("ID_Cuenta_FK"));
                isVoided = reader.GetBoolean(reader.GetOrdinal("Anulada"));
                isReconciled = reader.GetBoolean(reader.GetOrdinal("Conciliada"));
                isDeposited = reader.GetInt32(reader.GetOrdinal("Depositada")) == 1;
            }

            if (isVoided)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new IncomeSaveResult(false, null, false, "La transacción ya está anulada.");
            }

            if (isReconciled)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new IncomeSaveResult(false, null, false, "No se puede anular una transacción ya conciliada.");
            }

            if (isDeposited)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new IncomeSaveResult(false, null, false, "No se puede anular una transacción ya depositada.");
            }

            if (ImpactsBankImmediately(paymentMethod))
            {
                await AdjustAccountBalanceAsync(connection, transaction, accountId, -amount, cancellationToken);
            }

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

            await InsertLogAsync(connection, transaction, id, "ANULAR", userName, $"Transacción anulada. Motivo: {reason}", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new IncomeSaveResult(true, id, false, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new IncomeSaveResult(false, null, false, ex.Message);
        }
    }

    public async Task<MemberSaveResult> CreateMemberAsync(
        MemberQuickEntry entry,
        CancellationToken cancellationToken = default)
    {
        var firstName = entry.FirstName.Trim();
        var lastName = entry.LastName.Trim();
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return new MemberSaveResult(false, null, null, "El nombre es requerido.");
        }

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string findSql = """
            SELECT TOP 1 ID_Miembro
            FROM dbo.Miembros
            WHERE UPPER(LTRIM(RTRIM(Nombre))) = UPPER(@firstName)
              AND UPPER(LTRIM(RTRIM(Apellido))) = UPPER(@lastName)
              AND ID_Tenant_FK = @tenantId;
            """;

        await using (var findCommand = new SqlCommand(findSql, connection))
        {
            findCommand.Parameters.Add("@firstName", SqlDbType.NVarChar, 100).Value = firstName;
            findCommand.Parameters.Add("@lastName", SqlDbType.NVarChar, 100).Value = lastName;
            findCommand.Parameters.AddWithValue("@tenantId", _tenantId);
            var existingId = await findCommand.ExecuteScalarAsync(cancellationToken);
            if (existingId is not null and not DBNull)
            {
                return new MemberSaveResult(true, Convert.ToInt32(existingId), BuildMemberDisplayName(firstName, lastName), null);
            }
        }

        const string insertSql = """
            INSERT INTO dbo.Miembros (Nombre, Apellido, ID_Tenant_FK)
            VALUES (@firstName, @lastName, @tenantId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;

        try
        {
            await using var insertCommand = new SqlCommand(insertSql, connection);
            insertCommand.Parameters.Add("@firstName", SqlDbType.NVarChar, 100).Value = firstName;
            insertCommand.Parameters.Add("@lastName", SqlDbType.NVarChar, 100).Value = lastName;
            insertCommand.Parameters.AddWithValue("@tenantId", _tenantId);
            var memberId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync(cancellationToken));
            return new MemberSaveResult(true, memberId, BuildMemberDisplayName(firstName, lastName), null);
        }
        catch (Exception ex)
        {
            return new MemberSaveResult(false, null, null, ex.Message);
        }
    }

    private static string? Validate(IncomeEntry entry)
    {
        if (entry.Amount <= 0) return "El monto debe ser mayor que cero.";
        if (entry.AccountId <= 0) return "Selecciona una cuenta bancaria.";
        if (entry.SubcategoryId <= 0) return "Selecciona una subcategoría.";
        if (string.IsNullOrWhiteSpace(entry.PaymentMethod)) return "Selecciona un método de pago.";
        if (entry.PaymentMethod.Length > 20) return "El método de pago no puede exceder 20 caracteres.";
        if (entry.PaymentMethod.Trim().Equals("Cheque", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(entry.CheckNumber)) return "El número de cheque es requerido.";
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

    private static string BuildMemberDisplayName(string firstName, string lastName) =>
        string.IsNullOrWhiteSpace(lastName) ? firstName : $"{firstName} {lastName}";

    private static async Task<IReadOnlyList<string>> LoadPaymentMethodsAsync(
        SqlConnection connection,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var methods = new SortedSet<string>(DefaultPaymentMethods, StringComparer.OrdinalIgnoreCase);
        await using var command = new SqlCommand(
            """
            SELECT DISTINCT MetodoPago
            FROM dbo.Transacciones
            WHERE MetodoPago IS NOT NULL
              AND LTRIM(RTRIM(MetodoPago)) <> ''
              AND ID_Tenant_FK = @tenantId
            ORDER BY MetodoPago;
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

    private static async Task<IReadOnlyList<IncomeTransaction>> ReadIncomeTransactionsAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        var rows = new List<IncomeTransaction>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new IncomeTransaction(
                reader.GetInt32(reader.GetOrdinal("ID_Transaccion")),
                reader.GetDateTime(reader.GetOrdinal("Fecha")),
                reader.GetString(reader.GetOrdinal("Descripcion")),
                reader.GetDecimal(reader.GetOrdinal("Monto")),
                reader.GetInt32(reader.GetOrdinal("ID_Cuenta_FK")),
                reader.GetString(reader.GetOrdinal("NombreCuenta")),
                reader.GetInt32(reader.GetOrdinal("ID_Subcategoria_FK")),
                reader.GetString(reader.GetOrdinal("NombreSubcategoria")),
                reader["ID_Miembro_FK"] is DBNull ? null : reader.GetInt32(reader.GetOrdinal("ID_Miembro_FK")),
                reader["Miembro"] is DBNull ? null : reader.GetString(reader.GetOrdinal("Miembro")),
                reader.GetString(reader.GetOrdinal("MetodoPago")),
                reader["NumeroCheque"] is DBNull ? null : reader.GetString(reader.GetOrdinal("NumeroCheque")),
                reader.GetInt32(reader.GetOrdinal("Depositada")) == 1,
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
              AND C.TipoCategoria = 'Ingreso';
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id", SqlDbType.Int).Value = subcategoryId;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private static async Task<bool> HasPossibleDuplicateAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IncomeEntry entry,
        int tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM dbo.Transacciones
            WHERE Fecha = @date
              AND Monto = @amount
              AND ID_Subcategoria_FK = @subcategoryId
              AND ISNULL(Anulada, 0) = 0
              AND ISNULL(ID_Miembro_FK, 0) = @memberId
              AND ID_Tenant_FK = @tenantId;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@date", SqlDbType.Date).Value = entry.Date.Date;
        command.Parameters.Add("@amount", SqlDbType.Money).Value = entry.Amount;
        command.Parameters.Add("@subcategoryId", SqlDbType.Int).Value = entry.SubcategoryId;
        command.Parameters.Add("@memberId", SqlDbType.Int).Value = entry.MemberId ?? 0;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<bool> HasDuplicateCheckAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IncomeEntry entry,
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

    private async Task<int> InsertIncomeAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IncomeEntry entry,
        int accountId,
        string subcategoryName,
        string userName,
        int tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.Transacciones
                (Fecha, Descripcion, Monto, ID_Cuenta_FK, ID_Subcategoria_FK, ID_Miembro_FK, MetodoPago, NumeroCheque, ID_Tenant_FK)
            VALUES
                (@date, @description, @amount, @accountId, @subcategoryId, @memberId, @paymentMethod, @checkNumber, @tenantId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        AddEntryParameters(command, entry, accountId);
        command.Parameters.AddWithValue("@tenantId", tenantId);
        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

        if (ImpactsBankImmediately(entry.PaymentMethod))
        {
            await AdjustAccountBalanceAsync(connection, transaction, accountId, entry.Amount, cancellationToken);
        }

        await InsertLogAsync(
            connection,
            transaction,
            id,
            "CREAR",
            userName,
            $"Nuevo ingreso creado. Monto: {entry.Amount}, Subcategoria: {subcategoryName}",
            cancellationToken);

        return id;
    }

    private async Task<int> UpdateIncomeAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IncomeEntry entry,
        int accountId,
        string userName,
        int tenantId,
        CancellationToken cancellationToken)
    {
        const string getSql = """
            SELECT Monto,
                   MetodoPago,
                   ID_Cuenta_FK,
                   ISNULL(Conciliada, 0) AS Conciliada,
                   CASE WHEN ID_Deposito_FK IS NULL THEN 0 ELSE 1 END AS Depositada,
                   ISNULL(Anulada, 0) AS Anulada
            FROM dbo.Transacciones
            WHERE ID_Transaccion = @id
              AND ID_Tenant_FK = @tenantId;
            """;

        decimal oldAmount;
        string oldPaymentMethod;
        int oldAccountId;
        bool isReconciled;
        bool isDeposited;
        bool isVoided;

        await using (var command = new SqlCommand(getSql, connection, transaction))
        {
            command.Parameters.Add("@id", SqlDbType.Int).Value = entry.Id;
            command.Parameters.AddWithValue("@tenantId", tenantId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("No se encontró la transacción para actualizar.");
            }

            oldAmount = reader.GetDecimal(reader.GetOrdinal("Monto"));
            oldPaymentMethod = reader.GetString(reader.GetOrdinal("MetodoPago"));
            oldAccountId = reader.GetInt32(reader.GetOrdinal("ID_Cuenta_FK"));
            isReconciled = reader.GetBoolean(reader.GetOrdinal("Conciliada"));
            isDeposited = reader.GetInt32(reader.GetOrdinal("Depositada")) == 1;
            isVoided = reader.GetBoolean(reader.GetOrdinal("Anulada"));
        }

        if (isVoided) throw new InvalidOperationException("No se puede editar una transacción anulada.");
        if (isReconciled) throw new InvalidOperationException("No se puede editar una transacción ya conciliada.");
        if (isDeposited) throw new InvalidOperationException("No se puede editar una transacción ya depositada.");

        const string updateSql = """
            UPDATE dbo.Transacciones
            SET Fecha = @date,
                Descripcion = @description,
                Monto = @amount,
                ID_Cuenta_FK = @accountId,
                ID_Subcategoria_FK = @subcategoryId,
                ID_Miembro_FK = @memberId,
                MetodoPago = @paymentMethod,
                NumeroCheque = @checkNumber
            WHERE ID_Transaccion = @id
              AND ID_Tenant_FK = @tenantId;
            """;

        await using (var command = new SqlCommand(updateSql, connection, transaction))
        {
            AddEntryParameters(command, entry, accountId);
            command.Parameters.Add("@id", SqlDbType.Int).Value = entry.Id;
            command.Parameters.AddWithValue("@tenantId", tenantId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (ImpactsBankImmediately(oldPaymentMethod))
        {
            await AdjustAccountBalanceAsync(connection, transaction, oldAccountId, -oldAmount, cancellationToken);
        }

        if (ImpactsBankImmediately(entry.PaymentMethod))
        {
            await AdjustAccountBalanceAsync(connection, transaction, accountId, entry.Amount, cancellationToken);
        }

        await InsertLogAsync(
            connection,
            transaction,
            entry.Id,
            "EDITAR",
            userName,
            $"Ingreso actualizado. Nuevo monto: {entry.Amount}",
            cancellationToken);

        return entry.Id;
    }

    private static void AddEntryParameters(SqlCommand command, IncomeEntry entry, int accountId)
    {
        command.Parameters.Add("@date", SqlDbType.Date).Value = entry.Date.Date;
        command.Parameters.Add("@description", SqlDbType.VarChar, 300).Value = entry.Description?.Trim() ?? string.Empty;
        command.Parameters.Add("@amount", SqlDbType.Money).Value = entry.Amount;
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = accountId;
        command.Parameters.Add("@subcategoryId", SqlDbType.Int).Value = entry.SubcategoryId;
        command.Parameters.Add("@memberId", SqlDbType.Int).Value = entry.MemberId.HasValue ? entry.MemberId.Value : DBNull.Value;
        command.Parameters.Add("@paymentMethod", SqlDbType.VarChar, 20).Value = entry.PaymentMethod.Trim();
        command.Parameters.Add("@checkNumber", SqlDbType.VarChar, 50).Value = string.IsNullOrWhiteSpace(entry.CheckNumber)
            ? DBNull.Value
            : entry.CheckNumber.Trim();
    }

    private static async Task<int> ResolveAccountIdAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string subcategoryName,
        int fallbackAccountId,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var accountKey = DetectAccountBySubcategory(subcategoryName);
        if (accountKey is null)
        {
            return fallbackAccountId;
        }

        await using var command = new SqlCommand(
            "SELECT TOP 1 ID_Cuenta FROM dbo.CuentasBancarias WHERE NombreCuenta LIKE @name AND ID_Tenant_FK = @tenantId ORDER BY ID_Cuenta;",
            connection,
            transaction);
        command.Parameters.Add("@name", SqlDbType.VarChar, 100).Value = $"%{accountKey}%";
        command.Parameters.AddWithValue("@tenantId", tenantId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? fallbackAccountId : Convert.ToInt32(value);
    }

    private static string? DetectAccountBySubcategory(string subcategoryName)
    {
        if (subcategoryName.Contains("ahorro", StringComparison.OrdinalIgnoreCase) ||
            subcategoryName.Contains("bldg", StringComparison.OrdinalIgnoreCase) ||
            subcategoryName.Contains("building", StringComparison.OrdinalIgnoreCase) ||
            subcategoryName.Contains("fund", StringComparison.OrdinalIgnoreCase))
        {
            return "Ahorros-8698";
        }

        if (subcategoryName.Contains("mision", StringComparison.OrdinalIgnoreCase) ||
            subcategoryName.Contains("mission", StringComparison.OrdinalIgnoreCase))
        {
            return "Misiones-9012";
        }

        return null;
    }

    private static bool IsTithe(string subcategoryName) =>
        subcategoryName.Contains("diezm", StringComparison.OrdinalIgnoreCase) ||
        subcategoryName.Contains("tithe", StringComparison.OrdinalIgnoreCase);

    private static bool ImpactsBankImmediately(string paymentMethod)
    {
        var method = paymentMethod.Trim().ToUpperInvariant();
        return method is not ("EFECTIVO" or "CHEQUE");
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

    private async Task InsertLogAsync(
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

        await AuditLogger.TryLogAsync(connectionFactory, _tenantId, userName, action, "Ingreso", transactionId.ToString(), detail, cancellationToken);
    }
}
