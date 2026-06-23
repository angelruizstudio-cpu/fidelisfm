using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlDashboardRepository(SqlConnectionFactory connectionFactory, ITenantContext tenantContext) : IDashboardRepository
{
    private readonly int _tenantId = tenantContext.TenantId;

    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var summary = await GetFinancialSummaryAsync(connection, _tenantId, cancellationToken);
        var hasStartDate = await HasAccountStartDateColumnAsync(connection, cancellationToken);
        var accounts = await GetBankAccountsAsync(connection, hasStartDate, _tenantId, cancellationToken);
        var kpis = await GetKpiTrendsAsync(connection, _tenantId, cancellationToken);

        return new DashboardSnapshot(summary, accounts, kpis, DateTime.Now);
    }

    private static async Task<IReadOnlyList<KpiTrend>> GetKpiTrendsAsync(
        SqlConnection connection,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var currentStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var currentEnd = DateTime.Today.AddDays(1);
        var previousStart = currentStart.AddMonths(-1);
        var previousEnd = currentStart;

        async Task<decimal> SumAsync(string categoryType, string? subcategoryLike, DateTime start, DateTime end)
        {
            var filter = subcategoryLike is null
                ? ""
                : "AND LOWER(ISNULL(S.NombreSubcategoria, '')) LIKE @subcategoryLike";

            var sql = $"""
                SELECT ISNULL(SUM(T.Monto), 0)
                  FROM Transacciones T
                  JOIN Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
                  JOIN Categorias K ON K.ID_Categoria = S.ID_Categoria_FK
                 WHERE T.Fecha >= @start AND T.Fecha < @end
                   AND T.ID_Tenant_FK = @tenantId
                   AND ISNULL(T.Anulada, 0) = 0
                   AND K.TipoCategoria = @categoryType
                   {filter};
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@start", start);
            command.Parameters.AddWithValue("@end", end);
            command.Parameters.AddWithValue("@tenantId", tenantId);
            command.Parameters.AddWithValue("@categoryType", categoryType);
            if (subcategoryLike is not null)
            {
                command.Parameters.AddWithValue("@subcategoryLike", subcategoryLike);
            }

            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is null or DBNull ? 0 : Convert.ToDecimal(value);
        }

        var incomeCurrent = await SumAsync("Ingreso", null, currentStart, currentEnd);
        var incomePrevious = await SumAsync("Ingreso", null, previousStart, previousEnd);
        var expensesCurrent = await SumAsync("Egreso", null, currentStart, currentEnd);
        var expensesPrevious = await SumAsync("Egreso", null, previousStart, previousEnd);
        var tithesCurrent = await SumAsync("Ingreso", "%tithe%", currentStart, currentEnd);
        var tithesPrevious = await SumAsync("Ingreso", "%tithe%", previousStart, previousEnd);
        var devotionalCurrent = await SumAsync("Ingreso", "%devotional%", currentStart, currentEnd);
        var devotionalPrevious = await SumAsync("Ingreso", "%devotional%", previousStart, previousEnd);

        return
        [
            new KpiTrend("Ingresos", incomeCurrent, incomePrevious),
            new KpiTrend("Gastos", expensesCurrent, expensesPrevious),
            new KpiTrend("Diezmos", tithesCurrent, tithesPrevious),
            new KpiTrend("Ofrendas Devocionales", devotionalCurrent, devotionalPrevious)
        ];
    }

    private static async Task<FinancialSummary> GetFinancialSummaryAsync(
        SqlConnection connection,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var start = new DateTime(DateTime.Today.Year, 1, 1);
        var end = DateTime.Today.AddDays(1);
        const string checkingAccount = "Checking-6163";

        const string incomeSql = """
            SELECT ISNULL(SUM(T.Monto), 0)
              FROM Transacciones T
              JOIN Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
              JOIN Categorias K ON K.ID_Categoria = S.ID_Categoria_FK
              JOIN CuentasBancarias C ON C.ID_Cuenta = T.ID_Cuenta_FK
             WHERE T.Fecha >= @start AND T.Fecha < @end
               AND T.ID_Tenant_FK = @tenantId
               AND ISNULL(T.Anulada, 0) = 0
               AND K.TipoCategoria = 'Ingreso'
               AND C.NombreCuenta = @account
               AND LOWER(ISNULL(S.NombreSubcategoria, '')) NOT LIKE '%missionary%'
               AND LOWER(ISNULL(S.NombreSubcategoria, '')) NOT LIKE '%misionera%'
               AND LOWER(ISNULL(S.NombreSubcategoria, '')) NOT LIKE '%bldg%'
               AND LOWER(ISNULL(S.NombreSubcategoria, '')) NOT LIKE '%ahorros pro-templo%'
               AND LOWER(ISNULL(S.NombreSubcategoria, '')) NOT LIKE '%pro-templo%'
               AND LOWER(ISNULL(S.NombreSubcategoria, '')) NOT LIKE '%bldg funds%'
               AND LOWER(ISNULL(S.NombreSubcategoria, '')) NOT LIKE '%saldo inicial%';
            """;

        const string expensesSql = """
            SELECT ISNULL(SUM(T.Monto), 0)
              FROM Transacciones T
              JOIN Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
              JOIN Categorias K ON K.ID_Categoria = S.ID_Categoria_FK
              JOIN CuentasBancarias C ON C.ID_Cuenta = T.ID_Cuenta_FK
             WHERE T.Fecha >= @start AND T.Fecha < @end
               AND T.ID_Tenant_FK = @tenantId
               AND ISNULL(T.Anulada, 0) = 0
               AND K.TipoCategoria = 'Egreso'
               AND C.NombreCuenta = @account;
            """;

        var income = await ExecuteMoneyScalarAsync(connection, incomeSql, start, end, checkingAccount, tenantId, cancellationToken);
        var expenses = await ExecuteMoneyScalarAsync(connection, expensesSql, start, end, checkingAccount, tenantId, cancellationToken);

        return new FinancialSummary(income, expenses, income - expenses);
    }

    private static async Task<IReadOnlyList<BankAccountBalance>> GetBankAccountsAsync(
        SqlConnection connection,
        bool hasStartDate,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var startDateSelect = hasStartDate
            ? "CAST(ISNULL(Cta.FechaInicioSaldo, '19000101') AS date)"
            : "CAST('19000101' AS date)";

        var sql = $"""
            SELECT Cta.ID_Cuenta,
                   Cta.NombreCuenta,
                   CAST(ISNULL(Cta.SaldoInicial, 0)
                        + ISNULL(D.TotalDepositos, 0)
                        + ISNULL(I.IngresosDirectos, 0)
                        - ISNULL(E.Egresos, 0) AS decimal(18, 2)) AS SaldoActual
              FROM CuentasBancarias Cta
              CROSS APPLY (SELECT {startDateSelect} AS FechaInicioSaldo) Inicio
              OUTER APPLY (
                  SELECT SUM(MontoTotal) AS TotalDepositos
                    FROM dbo.Depositos
                   WHERE ID_Cuenta_FK = Cta.ID_Cuenta
                     AND ID_Tenant_FK = @tenantId
                     AND FechaDeposito >= Inicio.FechaInicioSaldo
                     AND ISNULL(Anulado, 0) = 0
              ) D
              OUTER APPLY (
                  SELECT SUM(T.Monto) AS IngresosDirectos
                    FROM dbo.Transacciones T
                    JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
                    JOIN dbo.Categorias K ON K.ID_Categoria = S.ID_Categoria_FK
                   WHERE T.ID_Cuenta_FK = Cta.ID_Cuenta
                     AND T.ID_Tenant_FK = @tenantId
                     AND T.Fecha >= Inicio.FechaInicioSaldo
                     AND K.TipoCategoria = 'Ingreso'
                     AND ISNULL(T.Anulada, 0) = 0
                     AND T.MetodoPago NOT IN ('Efectivo', 'Cheque')
                     AND LOWER(ISNULL(S.NombreSubcategoria, '')) NOT LIKE '%saldo inicial%'
              ) I
              OUTER APPLY (
                  SELECT SUM(T.Monto) AS Egresos
                    FROM dbo.Transacciones T
                    JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
                    JOIN dbo.Categorias K ON K.ID_Categoria = S.ID_Categoria_FK
                   WHERE T.ID_Cuenta_FK = Cta.ID_Cuenta
                     AND T.ID_Tenant_FK = @tenantId
                     AND T.Fecha >= Inicio.FechaInicioSaldo
                     AND K.TipoCategoria = 'Egreso'
                     AND ISNULL(T.Anulada, 0) = 0
              ) E
             WHERE Cta.NombreCuenta NOT LIKE '%(OLD-ID-%'
               AND Cta.ID_Tenant_FK = @tenantId
             ORDER BY Cta.NombreCuenta;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tenantId", tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var accounts = new List<BankAccountBalance>();

        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(new BankAccountBalance(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDecimal(2)));
        }

        return accounts;
    }

    private static async Task<bool> HasAccountStartDateColumnAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT CASE WHEN COL_LENGTH('dbo.CuentasBancarias', 'FechaInicioSaldo') IS NULL THEN 0 ELSE 1 END;",
            connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value) == 1;
    }

    private static async Task<decimal> ExecuteMoneyScalarAsync(
        SqlConnection connection,
        string sql,
        DateTime start,
        DateTime end,
        string account,
        int tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@start", start);
        command.Parameters.AddWithValue("@end", end);
        command.Parameters.AddWithValue("@account", account);
        command.Parameters.AddWithValue("@tenantId", tenantId);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? 0 : Convert.ToDecimal(value);
    }
}
