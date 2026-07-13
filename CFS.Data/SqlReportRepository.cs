using System.Data;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlReportRepository(SqlConnectionFactory connectionFactory, ITenantContext tenantContext) : IReportRepository
{
    private readonly int _tenantId = tenantContext.TenantId;

    private static readonly IReadOnlyList<ReportDefinition> Catalog =
    [
        new("profit-loss", "Profit and Loss", "Resumen de ingresos, gastos y net income por categoría.", true),
        new("profit-loss-detail", "Profit and Loss Detail", "Detalle transaccional del Profit and Loss.", true),
        new("tithes-members", "Diezmos (Miembros que diezman)", "Miembros con diezmos registrados en el periodo.", true)
    ];

    public Task<IReadOnlyList<ReportDefinition>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Catalog);

    public async Task<IReadOnlyList<ReportBankAccount>> GetBankAccountsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT ID_Cuenta, NombreCuenta
            FROM dbo.CuentasBancarias
            WHERE NombreCuenta NOT LIKE '%(OLD-ID-%'
              AND ID_Tenant_FK = @tenantId
            ORDER BY NombreCuenta;
            """;

        var accounts = new List<ReportBankAccount>();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tenantId", _tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(new ReportBankAccount(
                reader.GetInt32(reader.GetOrdinal("ID_Cuenta")),
                reader.GetString(reader.GetOrdinal("NombreCuenta"))));
        }

        return accounts;
    }

    public async Task<FinancialReport> GetReportAsync(
        ReportRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        return request.Key switch
        {
            "profit-loss-detail" => await GetProfitAndLossDetailAsync(connection, request, _tenantId, cancellationToken),
            "tithes-members" => await GetTithesByMemberAsync(connection, request, _tenantId, cancellationToken),
            _ => await GetProfitAndLossAsync(connection, request, _tenantId, cancellationToken)
        };
    }

    private static async Task<FinancialReport> GetProfitAndLossAsync(
        SqlConnection connection,
        ReportRequest request,
        int tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Cat.TipoCategoria,
                   ISNULL(Cat.NombreCategoria, Cat.TipoCategoria) AS NombreCategoria,
                   S.NombreSubcategoria,
                   SUM(T.Monto) AS Total
            FROM dbo.Transacciones T
            INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
            INNER JOIN dbo.Categorias Cat ON Cat.ID_Categoria = S.ID_Categoria_FK
            WHERE T.Fecha >= @start
              AND T.Fecha < @end
              AND T.ID_Tenant_FK = @tenantId
              AND ISNULL(T.Anulada, 0) = 0
              AND Cat.TipoCategoria IN ('Ingreso', 'Egreso')
              AND (@accountId IS NULL OR T.ID_Cuenta_FK = @accountId)
            GROUP BY Cat.TipoCategoria, Cat.NombreCategoria, S.NombreSubcategoria
            ORDER BY Cat.TipoCategoria, Cat.NombreCategoria, S.NombreSubcategoria;
            """;

        var groupedRows = new List<GroupedReportRow>();
        await using var command = CreateDateCommand(sql, connection, request, tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var type = reader.GetString(reader.GetOrdinal("TipoCategoria"));
            var category = reader.GetString(reader.GetOrdinal("NombreCategoria"));
            var subcategory = reader.GetString(reader.GetOrdinal("NombreSubcategoria"));
            var amount = reader.GetDecimal(reader.GetOrdinal("Total"));
            groupedRows.Add(new GroupedReportRow(type, category, subcategory, amount));
        }

        var income = BuildIncomeLines(groupedRows);
        var expenses = BuildExpenseLines(groupedRows);
        var totalIncome = income.Sum(l => l.Amount);
        var totalExpenses = groupedRows
            .Where(row => row.Type.Equals("Egreso", StringComparison.OrdinalIgnoreCase))
            .Sum(row => row.Amount);
        var sections = new List<ReportSection>
        {
            new("Income", AddTotalLine(income, "Total for Income", totalIncome), totalIncome),
            new("Expenses", [.. expenses, new ReportLine("Total:Total for Expenses", "Total for Expenses", totalExpenses, 0, true, false, [])], totalExpenses)
        };

        var insights = BuildInsights(sections, totalIncome, totalExpenses);
        sections = MarkInsightLines(sections, insights).ToList();

        return new FinancialReport(
            request.Key,
            "Profit and Loss",
            PeriodLabel(request),
            sections,
            totalIncome,
            totalExpenses,
            totalIncome - totalExpenses,
            insights,
            DateTime.Now);
    }

    private static async Task<FinancialReport> GetProfitAndLossDetailAsync(
        SqlConnection connection,
        ReportRequest request,
        int tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT T.ID_Transaccion,
                   T.Fecha,
                   ISNULL(T.Descripcion, '') AS Descripcion,
                   Cat.TipoCategoria,
                   ISNULL(Cat.NombreCategoria, Cat.TipoCategoria) AS NombreCategoria,
                   S.NombreSubcategoria,
                   Cta.NombreCuenta,
                   T.Monto,
                   ISNULL(T.NumeroCheque, '') AS NumeroCheque,
                   ISNULL(T.MetodoPago, '') AS MetodoPago,
                   CASE WHEN M.ID_Miembro IS NULL THEN ''
                        ELSE LTRIM(RTRIM(M.Nombre)) + CASE WHEN LTRIM(RTRIM(M.Apellido)) <> '' THEN ' ' + LTRIM(RTRIM(M.Apellido)) ELSE '' END
                   END AS Nombre
            FROM dbo.Transacciones T
            INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
            INNER JOIN dbo.Categorias Cat ON Cat.ID_Categoria = S.ID_Categoria_FK
            INNER JOIN dbo.CuentasBancarias Cta ON Cta.ID_Cuenta = T.ID_Cuenta_FK
            LEFT JOIN dbo.Miembros M ON M.ID_Miembro = T.ID_Miembro_FK
            WHERE T.Fecha >= @start
              AND T.Fecha < @end
              AND T.ID_Tenant_FK = @tenantId
              AND ISNULL(T.Anulada, 0) = 0
              AND Cat.TipoCategoria IN ('Ingreso', 'Egreso')
              AND (@accountId IS NULL OR T.ID_Cuenta_FK = @accountId)
            ORDER BY Cat.TipoCategoria, Cat.NombreCategoria, S.NombreSubcategoria, T.Fecha, T.ID_Transaccion;
            """;

        var rows = new List<DetailTransactionRow>();
        await using var command = CreateDateCommand(sql, connection, request, tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new DetailTransactionRow(
                reader.GetString(reader.GetOrdinal("TipoCategoria")),
                reader.GetString(reader.GetOrdinal("NombreCategoria")),
                reader.GetString(reader.GetOrdinal("NombreSubcategoria")),
                reader.GetDateTime(reader.GetOrdinal("Fecha")),
                reader.GetString(reader.GetOrdinal("Descripcion")),
                reader.GetString(reader.GetOrdinal("Nombre")),
                reader.GetString(reader.GetOrdinal("NombreCuenta")),
                reader.GetDecimal(reader.GetOrdinal("Monto")),
                reader.GetString(reader.GetOrdinal("NumeroCheque")),
                reader.GetString(reader.GetOrdinal("MetodoPago"))));
        }

        var income = BuildDetailLines(rows, "Ingreso", nestUnderCategory: false);
        var expenses = BuildDetailLines(rows, "Egreso", nestUnderCategory: true);

        var totalIncome = rows.Where(r => r.Type.Equals("Ingreso", StringComparison.OrdinalIgnoreCase)).Sum(r => r.Amount);
        var totalExpenses = rows.Where(r => r.Type.Equals("Egreso", StringComparison.OrdinalIgnoreCase)).Sum(r => r.Amount);
        var sections = new List<ReportSection>
        {
            new("Income", AddTotalLine(income, "Total for Income", totalIncome), totalIncome),
            new("Expenses", [.. expenses, new ReportLine("Total:Total for Expenses", "Total for Expenses", totalExpenses, 0, true, false, [])], totalExpenses)
        };

        var insights = BuildInsights(sections, totalIncome, totalExpenses);
        sections = MarkInsightLines(sections, insights).ToList();

        return new FinancialReport(
            request.Key,
            "Profit and Loss Detail",
            PeriodLabel(request),
            sections,
            totalIncome,
            totalExpenses,
            totalIncome - totalExpenses,
            insights,
            DateTime.Now);
    }

    /// <summary>
    /// Groups raw transactions by category/subcategory (matching the plain P&amp;L's grouping),
    /// attaching each subcategory line's individual transactions — with a running balance that
    /// resets to zero at the start of every subcategory — so the UI/print view can drill down
    /// the same way QuickBooks' "Profit and Loss Detail" does.
    /// </summary>
    private static IReadOnlyList<ReportLine> BuildDetailLines(
        IReadOnlyList<DetailTransactionRow> rows,
        string type,
        bool nestUnderCategory)
    {
        var typeRows = rows.Where(r => r.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
        var lines = new List<ReportLine>();

        IReadOnlyList<ReportLine> BuildSubcategoryLines(string category, IEnumerable<DetailTransactionRow> subRows, int level)
        {
            var result = new List<ReportLine>();
            foreach (var subGroup in subRows.GroupBy(r => r.Subcategory).OrderBy(g => g.Key))
            {
                var ordered = subGroup.OrderBy(r => r.Date).ToList();
                var runningBalance = 0m;
                var details = new List<ReportDetailLine>(ordered.Count);
                foreach (var row in ordered)
                {
                    runningBalance += row.Amount;
                    var transactionType = !string.IsNullOrWhiteSpace(row.CheckNumber) ? "Cheque" : row.PaymentMethod;
                    details.Add(new ReportDetailLine(
                        row.Date, row.Description, row.Name, row.AccountName, row.Subcategory,
                        row.Amount, row.CheckNumber, transactionType, runningBalance));
                }

                var subtotal = ordered.Sum(r => r.Amount);
                result.Add(new ReportLine($"{type}:{category}:{subGroup.Key}", subGroup.Key, subtotal, level, false, false, details));
            }

            return result;
        }

        if (!nestUnderCategory)
        {
            return BuildSubcategoryLines(string.Empty, typeRows, 1);
        }

        foreach (var categoryGroup in typeRows.GroupBy(r => r.Category).OrderBy(g => g.Key))
        {
            var categoryTotal = categoryGroup.Sum(r => r.Amount);
            lines.Add(new ReportLine($"{type}:{categoryGroup.Key}", categoryGroup.Key, categoryTotal, 1, false, false, []));
            lines.AddRange(BuildSubcategoryLines(categoryGroup.Key, categoryGroup, 2));
            lines.Add(new ReportLine($"Total:{categoryGroup.Key}", $"Total for {categoryGroup.Key}", categoryTotal, 1, true, false, []));
        }

        return lines;
    }

    private sealed record DetailTransactionRow(
        string Type,
        string Category,
        string Subcategory,
        DateTime Date,
        string Description,
        string Name,
        string AccountName,
        decimal Amount,
        string CheckNumber,
        string PaymentMethod);

    private static async Task<FinancialReport> GetTithesByMemberAsync(
        SqlConnection connection,
        ReportRequest request,
        int tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT T.ID_Miembro_FK,
                   CASE WHEN M.ID_Miembro IS NULL THEN 'Sin miembro'
                        ELSE LTRIM(RTRIM(M.Nombre)) + CASE WHEN LTRIM(RTRIM(M.Apellido)) <> '' THEN ' ' + LTRIM(RTRIM(M.Apellido)) ELSE '' END
                   END AS Miembro,
                   COUNT(*) AS Cantidad,
                   SUM(T.Monto) AS Total
            FROM dbo.Transacciones T
            INNER JOIN dbo.Subcategorias S ON S.ID_Subcategoria = T.ID_Subcategoria_FK
            INNER JOIN dbo.Categorias Cat ON Cat.ID_Categoria = S.ID_Categoria_FK
            LEFT JOIN dbo.Miembros M ON M.ID_Miembro = T.ID_Miembro_FK
            WHERE T.Fecha >= @start
              AND T.Fecha < @end
              AND T.ID_Tenant_FK = @tenantId
              AND ISNULL(T.Anulada, 0) = 0
              AND Cat.TipoCategoria = 'Ingreso'
              AND (S.NombreSubcategoria LIKE '%Diezm%' OR S.NombreSubcategoria LIKE '%Tithe%')
              AND (@accountId IS NULL OR T.ID_Cuenta_FK = @accountId)
            GROUP BY T.ID_Miembro_FK, M.ID_Miembro, M.Nombre, M.Apellido
            ORDER BY Total DESC, Miembro;
            """;

        var lines = new List<ReportLine>();
        await using var command = CreateDateCommand(sql, connection, request, tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var member = reader.GetString(reader.GetOrdinal("Miembro"));
            var total = reader.GetDecimal(reader.GetOrdinal("Total"));
            var count = reader.GetInt32(reader.GetOrdinal("Cantidad"));
            lines.Add(new ReportLine($"Tithes:{member}", $"{member} ({count})", total, 1, false, false, []));
        }

        var totalTithes = lines.Sum(l => l.Amount);
        var sections = new List<ReportSection>
        {
            new("Diezmos", AddTotalLine(lines, "Total Diezmos", totalTithes), totalTithes)
        };

        var insights = BuildInsights(sections, totalTithes, 0);
        sections = MarkInsightLines(sections, insights).ToList();

        return new FinancialReport(
            request.Key,
            "Diezmos (Miembros que diezman)",
            PeriodLabel(request),
            sections,
            totalTithes,
            0,
            totalTithes,
            insights,
            DateTime.Now);
    }

    private static SqlCommand CreateDateCommand(string sql, SqlConnection connection, ReportRequest request, int tenantId)
    {
        var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@start", SqlDbType.Date).Value = request.StartDate.Date;
        command.Parameters.Add("@end", SqlDbType.Date).Value = request.EndDate.Date.AddDays(1);
        command.Parameters.Add("@accountId", SqlDbType.Int).Value = request.AccountId.HasValue ? request.AccountId.Value : DBNull.Value;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        return command;
    }

    private static IReadOnlyList<ReportLine> AddTotalLine(IReadOnlyList<ReportLine> lines, string label, decimal total)
    {
        var result = lines.ToList();
        result.Add(new ReportLine($"Total:{label}", label, total, 0, true, false, []));
        return result;
    }

    private static IReadOnlyList<ReportLine> BuildIncomeLines(IReadOnlyList<GroupedReportRow> rows) =>
        rows
            .Where(row => row.Type.Equals("Ingreso", StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => row.Subcategory)
            .Select(row => new ReportLine($"Ingreso:{row.Category}:{row.Subcategory}", row.Subcategory, row.Amount, 1, false, false, []))
            .ToList();

    private static IReadOnlyList<ReportLine> BuildExpenseLines(IReadOnlyList<GroupedReportRow> rows)
    {
        var lines = new List<ReportLine>();
        var groups = rows
            .Where(row => row.Type.Equals("Egreso", StringComparison.OrdinalIgnoreCase))
            .GroupBy(row => row.Category)
            .OrderBy(group => group.Key);

        foreach (var group in groups)
        {
            var total = group.Sum(row => row.Amount);
            lines.Add(new ReportLine($"Egreso:{group.Key}", group.Key, total, 1, false, false, []));

            foreach (var row in group.OrderBy(item => item.Subcategory))
            {
                lines.Add(new ReportLine($"Egreso:{group.Key}:{row.Subcategory}", row.Subcategory, row.Amount, 2, false, false, []));
            }

            lines.Add(new ReportLine($"Total:{group.Key}", $"Total for {group.Key}", total, 1, true, false, []));
        }

        return lines;
    }

    private static IEnumerable<ReportSection> MarkInsightLines(
        IReadOnlyList<ReportSection> sections,
        IReadOnlyList<ReportInsight> insights) =>
        sections.Select(section => section with
        {
            Lines = section.Lines
                .Select(line => line with { HasInsight = insights.Any(i => i.LineKey == line.Key) })
                .ToList()
        });

    private static IReadOnlyList<ReportInsight> BuildInsights(
        IReadOnlyList<ReportSection> sections,
        decimal totalIncome,
        decimal totalExpenses)
    {
        var insights = new List<ReportInsight>();
        var net = totalIncome - totalExpenses;

        if (totalIncome > 0)
        {
            insights.Add(new ReportInsight(
                "Total:Total for Income",
                "Ingresos del periodo",
                $"Los ingresos suman {totalIncome:C2}. Usa este total como punto de control contra depósitos y recibos del periodo.",
                "Info",
                totalIncome,
                1m,
                "Total de todas las líneas de ingreso activas dentro del periodo seleccionado."));

            insights.Add(new ReportInsight(
                "GrossProfit",
                "Gross Profit",
                $"Gross Profit para este periodo es {totalIncome:C2}. Este total refleja los ingresos antes de gastos.",
                "Info",
                totalIncome,
                1m,
                "En Fidelis los ingresos de iglesia se presentan como gross profit antes de restar gastos."));
        }

        if (totalExpenses > 0)
        {
            insights.Add(new ReportInsight(
                "Total:Total for Expenses",
                "Total de gastos",
                $"Los gastos suman {totalExpenses:C2}. Esto representa {Percent(totalExpenses, totalIncome)} de los ingresos del periodo.",
                totalIncome > 0 && totalExpenses >= totalIncome * 0.85m ? "Warning" : "Info",
                totalExpenses,
                Ratio(totalExpenses, totalIncome),
                "Suma de todas las líneas de egreso activas dentro del periodo seleccionado."));
        }

        AddLineContributionInsights(insights, sections, "Income", totalIncome, "Ingreso principal", "Este ingreso representa una porción relevante del total recibido.");
        AddLineContributionInsights(insights, sections, "Expenses", totalExpenses, "Gasto relevante", "Esta partida representa una porción relevante del total de gastos.");
        AddExpenseCategoryInsights(insights, sections, totalExpenses, totalIncome);

        if (totalExpenses > totalIncome && totalIncome > 0)
        {
            insights.Add(new ReportInsight(
                "Total:Total for Expenses",
                "Gastos sobre ingresos",
                $"Los gastos exceden los ingresos por {Math.Abs(net):C2} en este periodo.",
                "Warning",
                Math.Abs(net),
                Ratio(totalExpenses, totalIncome),
                "El total de gastos es mayor que el total de ingresos del periodo."));
        }

        var netSeverity = net < 0 ? "Warning" : "Info";
        if (net < 0)
        {
            insights.Add(new ReportInsight(
                "NetIncome",
                "Net income negativo",
                $"El resultado neto del periodo es {net:C2}. Conviene revisar las categorías de gasto principales.",
                netSeverity,
                net,
                Ratio(net, totalIncome),
                "La diferencia entre ingresos y gastos terminó por debajo de cero."));
        }
        else if (totalIncome > 0)
        {
            insights.Add(new ReportInsight(
                "NetIncome",
                "Net income positivo",
                $"El resultado neto del periodo es {net:C2}, equivalente a {Percent(net, totalIncome)} de los ingresos.",
                netSeverity,
                net,
                Ratio(net, totalIncome),
                "La diferencia entre ingresos y gastos quedó positiva para el periodo."));
        }

        return insights
            .GroupBy(i => i.LineKey)
            .SelectMany(group => group
                .OrderByDescending(i => i.Severity == "Warning")
                .Take(1))
            .Take(12)
            .ToList();
    }

    private static void AddLineContributionInsights(
        List<ReportInsight> insights,
        IReadOnlyList<ReportSection> sections,
        string sectionName,
        decimal sectionTotal,
        string title,
        string explanation)
    {
        if (sectionTotal <= 0)
        {
            return;
        }

        var section = sections.FirstOrDefault(s => s.Name.Equals(sectionName, StringComparison.OrdinalIgnoreCase));
        if (section is null)
        {
            return;
        }

        foreach (var line in section.Lines
            .Where(line => !line.IsTotal && line.Amount > 0)
            .OrderByDescending(line => line.Amount)
            .Take(5))
        {
            var share = line.Amount / sectionTotal;
            if (share < 0.08m)
            {
                continue;
            }

            insights.Add(new ReportInsight(
                line.Key,
                title,
                $"{line.Label} suma {line.Amount:C2}, equivalente a {share:P0} del total de {sectionName}. {explanation}",
                share >= 0.35m ? "Warning" : "Info",
                line.Amount,
                share,
                $"{line.Label} está entre las partidas de mayor peso dentro de {sectionName}."));
        }
    }

    private static void AddExpenseCategoryInsights(
        List<ReportInsight> insights,
        IReadOnlyList<ReportSection> sections,
        decimal totalExpenses,
        decimal totalIncome)
    {
        if (totalExpenses <= 0)
        {
            return;
        }

        var expenses = sections.FirstOrDefault(s => s.Name.Equals("Expenses", StringComparison.OrdinalIgnoreCase));
        if (expenses is null)
        {
            return;
        }

        foreach (var line in expenses.Lines
            .Where(line => line.IsTotal && line.Key.StartsWith("Total:", StringComparison.OrdinalIgnoreCase) && line.Label != "Total for Expenses")
            .OrderByDescending(line => line.Amount)
            .Take(6))
        {
            var expenseShare = line.Amount / totalExpenses;
            var incomeShare = totalIncome > 0 ? line.Amount / totalIncome : 0;
            var severity = incomeShare >= 0.25m || expenseShare >= 0.35m ? "Warning" : "Info";

            insights.Add(new ReportInsight(
                line.Key,
                "Categoría de gasto destacada",
                $"{line.Label} suma {line.Amount:C2}. Representa {expenseShare:P0} de los gastos y {Percent(line.Amount, totalIncome)} de los ingresos.",
                severity,
                line.Amount,
                expenseShare,
                $"{line.Label} concentra una parte relevante del gasto total."));
        }
    }

    private static string Percent(decimal amount, decimal total) =>
        total <= 0 ? "0%" : (amount / total).ToString("P0");

    private static decimal? Ratio(decimal amount, decimal total) =>
        total <= 0 ? null : amount / total;

    private static string PeriodLabel(ReportRequest request) =>
        $"{request.StartDate:MMMM d, yyyy} - {request.EndDate:MMMM d, yyyy}";

    private sealed record GroupedReportRow(string Type, string Category, string Subcategory, decimal Amount);
}
