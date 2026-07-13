using System.Data;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlBudgetRepository(SqlConnectionFactory connectionFactory, ITenantContext tenantContext) : IBudgetRepository
{
    private readonly int _tenantId = tenantContext.TenantId;

    public async Task<BudgetOverview> GetOverviewAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        if (month is < 1 or > 12) month = 12;

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        // 1. Every income/expense category for this tenant.
        var categories = new List<(int Id, string Name, string Type)>();
        const string catSql = """
            SELECT ID_Categoria,
                   ISNULL(NombreCategoria, TipoCategoria) AS NombreCategoria,
                   TipoCategoria
              FROM dbo.Categorias
             WHERE ID_Tenant_FK = @tenantId
               AND TipoCategoria IN ('Ingreso', 'Egreso')
             ORDER BY TipoCategoria, NombreCategoria;
            """;
        await using (var command = new SqlCommand(catSql, connection))
        {
            command.Parameters.AddWithValue("@tenantId", _tenantId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                categories.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        // 2. Annual budget amounts stored for the year.
        var budgets = new Dictionary<int, decimal>();
        const string budgetSql = """
            SELECT ID_Categoria_FK, MontoAnual
              FROM dbo.CFS_Presupuestos
             WHERE ID_Tenant_FK = @tenantId AND Anio = @year;
            """;
        try
        {
            await using var command = new SqlCommand(budgetSql, connection);
            command.Parameters.AddWithValue("@tenantId", _tenantId);
            command.Parameters.Add("@year", SqlDbType.Int).Value = year;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                budgets[reader.GetInt32(0)] = reader.GetDecimal(1);
            }
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            // dbo.CFS_Presupuestos migration not applied yet — treat as no budgets set.
        }

        // 3. Actuals from Jan 1 through the end of the selected month (exclusive upper bound).
        var actuals = new Dictionary<int, decimal>();
        var start = new DateTime(year, 1, 1);
        var end = new DateTime(year, month, 1).AddMonths(1);
        const string actualSql = """
            SELECT Cat.ID_Categoria, SUM(T.Monto) AS Total
              FROM dbo.Transacciones T
              INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
              INNER JOIN dbo.Categorias Cat ON Cat.ID_Categoria = S.ID_Categoria_FK
             WHERE T.Fecha >= @start
               AND T.Fecha < @end
               AND T.ID_Tenant_FK = @tenantId
               AND ISNULL(T.Anulada, 0) = 0
               AND Cat.TipoCategoria IN ('Ingreso', 'Egreso')
             GROUP BY Cat.ID_Categoria;
            """;
        await using (var command = new SqlCommand(actualSql, connection))
        {
            command.Parameters.AddWithValue("@tenantId", _tenantId);
            command.Parameters.Add("@start", SqlDbType.Date).Value = start;
            command.Parameters.Add("@end", SqlDbType.Date).Value = end;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                actuals[reader.GetInt32(0)] = reader.GetDecimal(1);
            }
        }

        // 4. Combine. Expected-to-date = annual budget prorated by months elapsed.
        var income = new List<BudgetCategoryLine>();
        var expense = new List<BudgetCategoryLine>();
        foreach (var (id, name, type) in categories)
        {
            var annual = budgets.GetValueOrDefault(id, 0m);
            var actual = actuals.GetValueOrDefault(id, 0m);
            var expected = Math.Round(annual / 12m * month, 2);
            var line = new BudgetCategoryLine(id, name, type, annual, expected, actual);
            if (line.IsIncome) income.Add(line);
            else expense.Add(line);
        }

        return new BudgetOverview(year, month, income, expense);
    }

    public async Task SaveCategoryBudgetAsync(int categoryId, int year, decimal annualAmount, string userName, CancellationToken cancellationToken = default)
    {
        if (annualAmount < 0) annualAmount = 0;

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        // Upsert: update the existing row, or insert if none exists for this (tenant, category, year).
        const string upsertSql = """
            UPDATE dbo.CFS_Presupuestos
               SET MontoAnual = @amount, UpdatedAt = SYSUTCDATETIME()
             WHERE ID_Tenant_FK = @tenantId AND ID_Categoria_FK = @categoryId AND Anio = @year;

            IF @@ROWCOUNT = 0
                INSERT INTO dbo.CFS_Presupuestos (ID_Tenant_FK, ID_Categoria_FK, Anio, MontoAnual)
                VALUES (@tenantId, @categoryId, @year, @amount);
            """;

        await using var command = new SqlCommand(upsertSql, connection);
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        command.Parameters.Add("@categoryId", SqlDbType.Int).Value = categoryId;
        command.Parameters.Add("@year", SqlDbType.Int).Value = year;
        command.Parameters.Add("@amount", SqlDbType.Decimal).Value = annualAmount;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await AuditLogger.TryLogAsync(connectionFactory, _tenantId, userName, "EDITAR", "Presupuesto",
            categoryId.ToString(), $"Presupuesto {year} de categoría {categoryId} = {annualAmount:C2}", cancellationToken);
    }
}
