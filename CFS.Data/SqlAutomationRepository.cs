using System.Data;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlAutomationRepository(SqlConnectionFactory connectionFactory, ITenantContext tenantContext) : IAutomationRepository
{
    private readonly int _tenantId = tenantContext.TenantId;

    public async Task<AutomationLookups> GetLookupsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var accounts = await LoadOptionsAsync(
            connection,
            "SELECT ID_Cuenta, NombreCuenta FROM dbo.CuentasBancarias WHERE ID_Tenant_FK = @tenantId ORDER BY NombreCuenta;",
            cancellationToken);

        var incomeSubcategories = await LoadOptionsAsync(
            connection,
            """
            SELECT S.ID_Subcategoria, S.NombreSubcategoria
              FROM dbo.Subcategorias S
              JOIN dbo.Categorias K ON K.ID_Categoria = S.ID_Categoria_FK
             WHERE K.TipoCategoria = 'Ingreso' AND S.ID_Tenant_FK = @tenantId
             ORDER BY S.NombreSubcategoria;
            """,
            cancellationToken);

        var expenseSubcategories = await LoadOptionsAsync(
            connection,
            """
            SELECT S.ID_Subcategoria, S.NombreSubcategoria
              FROM dbo.Subcategorias S
              JOIN dbo.Categorias K ON K.ID_Categoria = S.ID_Categoria_FK
             WHERE K.TipoCategoria = 'Egreso' AND S.ID_Tenant_FK = @tenantId
             ORDER BY S.NombreSubcategoria;
            """,
            cancellationToken);

        return new AutomationLookups(accounts, incomeSubcategories, expenseSubcategories);
    }

    public async Task<IReadOnlyList<AutomationRule>> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT R.Id, R.Name, R.TransactionType, R.AccountId, C.NombreCuenta, R.SubcategoryId, S.NombreSubcategoria,
                   R.Amount, R.Frequency, R.NextRunDate, R.Description, R.Active
              FROM dbo.CFS_AutomationRules R
              JOIN dbo.CuentasBancarias C ON C.ID_Cuenta = R.AccountId
              JOIN dbo.Subcategorias S ON S.ID_Subcategoria = R.SubcategoryId
             WHERE R.ID_Tenant_FK = @tenantId
             ORDER BY R.NextRunDate, R.Name;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tenantId", _tenantId);

        var rules = new List<AutomationRule>();

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rules.Add(new AutomationRule(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3),
                    reader.GetString(4),
                    reader.GetInt32(5),
                    reader.GetString(6),
                    reader.GetDecimal(7),
                    reader.GetString(8),
                    reader.GetDateTime(9),
                    reader.IsDBNull(10) ? null : reader.GetString(10),
                    reader.GetBoolean(11)));
            }
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            // dbo.CFS_AutomationRules migration not applied yet in this environment.
            return rules;
        }

        return rules;
    }

    public async Task<AutomationSaveResult> SaveAsync(AutomationRuleEntry entry, string userName, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        if (entry.Id <= 0)
        {
            const string insertSql = """
                INSERT INTO dbo.CFS_AutomationRules
                    (ID_Tenant_FK, Name, TransactionType, AccountId, SubcategoryId, Amount, Frequency, NextRunDate, Description, Active)
                VALUES
                    (@tenantId, @name, @type, @accountId, @subcategoryId, @amount, @frequency, @nextRunDate, @description, @active);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """;

            await using var command = new SqlCommand(insertSql, connection);
            AddEntryParameters(command, entry);
            var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            await AuditLogger.TryLogAsync(connectionFactory, _tenantId, userName, "CREAR", "Automatizacion", id.ToString(), $"Regla '{entry.Name}' creada.", cancellationToken);
            return new AutomationSaveResult(true, id, null);
        }

        const string updateSql = """
            UPDATE dbo.CFS_AutomationRules
            SET Name = @name,
                TransactionType = @type,
                AccountId = @accountId,
                SubcategoryId = @subcategoryId,
                Amount = @amount,
                Frequency = @frequency,
                NextRunDate = @nextRunDate,
                Description = @description,
                Active = @active
            WHERE Id = @id
              AND ID_Tenant_FK = @tenantId;
            """;

        await using (var command = new SqlCommand(updateSql, connection))
        {
            AddEntryParameters(command, entry);
            command.Parameters.Add("@id", SqlDbType.Int).Value = entry.Id;
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
            {
                return new AutomationSaveResult(false, null, "No se encontro la regla a actualizar.");
            }
        }

        await AuditLogger.TryLogAsync(connectionFactory, _tenantId, userName, "EDITAR", "Automatizacion", entry.Id.ToString(), $"Regla '{entry.Name}' actualizada.", cancellationToken);
        return new AutomationSaveResult(true, entry.Id, null);
    }

    public async Task<bool> SetActiveAsync(int id, bool active, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = "UPDATE dbo.CFS_AutomationRules SET Active = @active WHERE Id = @id AND ID_Tenant_FK = @tenantId;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@active", SqlDbType.Bit).Value = active;
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = "DELETE FROM dbo.CFS_AutomationRules WHERE Id = @id AND ID_Tenant_FK = @tenantId;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<AutomationRunResult> RunDueRulesAsync(string userName, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string dueSql = """
            SELECT Id, Name, TransactionType, AccountId, SubcategoryId, Amount, Frequency, NextRunDate, Description
              FROM dbo.CFS_AutomationRules
             WHERE ID_Tenant_FK = @tenantId
               AND Active = 1
               AND NextRunDate <= @today;
            """;

        var due = new List<(int Id, string Name, string Type, int AccountId, int SubcategoryId, decimal Amount, string Frequency, DateTime NextRunDate, string? Description)>();

        try
        {
        await using (var command = new SqlCommand(dueSql, connection))
        {
            command.Parameters.AddWithValue("@tenantId", _tenantId);
            command.Parameters.Add("@today", SqlDbType.Date).Value = DateTime.Today;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                due.Add((
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3),
                    reader.GetInt32(4),
                    reader.GetDecimal(5),
                    reader.GetString(6),
                    reader.GetDateTime(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8)));
            }
        }
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            // dbo.CFS_AutomationRules migration not applied yet in this environment.
            return new AutomationRunResult(0, ["La automatización aún no está disponible en este ambiente."]);
        }

        var messages = new List<string>();

        foreach (var rule in due)
        {
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                const string insertSql = """
                    INSERT INTO dbo.Transacciones
                        (Fecha, Descripcion, Monto, ID_Cuenta_FK, ID_Subcategoria_FK, ID_Miembro_FK, MetodoPago, NumeroCheque, ID_Tenant_FK)
                    VALUES
                        (@date, @description, @amount, @accountId, @subcategoryId, NULL, 'Automatico', NULL, @tenantId);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """;

                await using var insertCommand = new SqlCommand(insertSql, connection, transaction);
                insertCommand.Parameters.Add("@date", SqlDbType.Date).Value = rule.NextRunDate;
                insertCommand.Parameters.Add("@description", SqlDbType.NVarChar, 255).Value = rule.Description ?? rule.Name;
                insertCommand.Parameters.Add("@amount", SqlDbType.Money).Value = rule.Amount;
                insertCommand.Parameters.Add("@accountId", SqlDbType.Int).Value = rule.AccountId;
                insertCommand.Parameters.Add("@subcategoryId", SqlDbType.Int).Value = rule.SubcategoryId;
                insertCommand.Parameters.AddWithValue("@tenantId", _tenantId);
                var transactionId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync(cancellationToken));

                var nextRunDate = AdvanceSchedule(rule.NextRunDate, rule.Frequency);

                const string updateSql = "UPDATE dbo.CFS_AutomationRules SET NextRunDate = @nextRunDate WHERE Id = @id AND ID_Tenant_FK = @tenantId;";
                await using var updateCommand = new SqlCommand(updateSql, connection, transaction);
                updateCommand.Parameters.Add("@nextRunDate", SqlDbType.Date).Value = nextRunDate;
                updateCommand.Parameters.Add("@id", SqlDbType.Int).Value = rule.Id;
                updateCommand.Parameters.AddWithValue("@tenantId", _tenantId);
                await updateCommand.ExecuteNonQueryAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                var typeLabel = rule.Type == AutomationTransactionType.Income ? "Ingreso" : "Egreso";
                messages.Add($"{rule.Name}: {typeLabel} de {rule.Amount:C2} creado. Proxima ejecucion: {nextRunDate:MM/dd/yyyy}.");
                await AuditLogger.TryLogAsync(connectionFactory, _tenantId, userName, "EJECUTAR", "Automatizacion", transactionId.ToString(), $"Regla '{rule.Name}' ejecutada. Monto: {rule.Amount}", cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                messages.Add($"{rule.Name}: error al ejecutar - {ex.Message}");
            }
        }

        return new AutomationRunResult(due.Count, messages);
    }

    private static DateTime AdvanceSchedule(DateTime current, string frequency) => frequency switch
    {
        AutomationFrequency.Weekly => current.AddDays(7),
        AutomationFrequency.BiWeekly => current.AddDays(14),
        _ => current.AddMonths(1)
    };

    private async Task<List<LookupOption>> LoadOptionsAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tenantId", _tenantId);

        var result = new List<LookupOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new LookupOption(reader.GetInt32(0), reader.GetString(1)));
        }

        return result;
    }

    private void AddEntryParameters(SqlCommand command, AutomationRuleEntry entry)
    {
        command.Parameters.Add("@name", SqlDbType.NVarChar, 150).Value = entry.Name;
        command.Parameters.Add("@type", SqlDbType.NVarChar, 20).Value = entry.TransactionType;
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = entry.AccountId;
        command.Parameters.Add("@subcategoryId", SqlDbType.Int).Value = entry.SubcategoryId;
        command.Parameters.Add("@amount", SqlDbType.Money).Value = entry.Amount;
        command.Parameters.Add("@frequency", SqlDbType.NVarChar, 20).Value = entry.Frequency;
        command.Parameters.Add("@nextRunDate", SqlDbType.Date).Value = entry.NextRunDate;
        command.Parameters.Add("@description", SqlDbType.NVarChar, 300).Value = (object?)entry.Description ?? DBNull.Value;
        command.Parameters.Add("@active", SqlDbType.Bit).Value = entry.Active;
        command.Parameters.AddWithValue("@tenantId", _tenantId);
    }
}
