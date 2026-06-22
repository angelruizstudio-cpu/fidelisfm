using CFS.Core.Models;
using CFS.Core.Services;

namespace CFS.Data;

public sealed class DemoUserAuthenticationRepository : IUserAuthenticationRepository
{
    public Task<AuthenticatedUser?> ValidateCredentialsAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (userName.Equals("demo", StringComparison.OrdinalIgnoreCase) && password == "demo")
        {
            return Task.FromResult<AuthenticatedUser?>(DemoData.User);
        }

        return Task.FromResult<AuthenticatedUser?>(null);
    }
}

public sealed class DemoTenantAccessRepository : ITenantAccessRepository
{
    public Task<IReadOnlyList<TenantAccessOption>> GetAccessibleTenantsAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TenantAccessOption>>([
            new TenantAccessOption(DemoData.User.TenantId, DemoData.User.TenantName, DemoData.User.PlanKey, DemoData.User.Roles, true)
        ]);
}

public sealed class DemoDashboardRepository : IDashboardRepository
{
    public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new DashboardSnapshot(
            new FinancialSummary(186_420.75m, 121_884.30m, 64_536.45m),
            DemoData.Accounts.Select(a => new BankAccountBalance(a.Id, a.Name, DemoData.AccountBalances[a.Id])).ToList(),
            DateTime.Now));
}

public sealed class DemoIncomeRepository : IIncomeRepository
{
    public Task<IncomeLookups> GetLookupsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new IncomeLookups(
            DemoData.Accounts,
            DemoData.IncomeSubcategories,
            DemoData.Members.OrderBy(m => m.Name).ToList(),
            ["Efectivo", "Cheque", "Zelle", "ACH", "Tarjeta", "Transferencia"]));

    public Task<IReadOnlyList<IncomeTransaction>> GetRecentAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var start = startDate?.Date ?? DateTime.Today.AddDays(-30);
        var end = (endDate?.Date ?? DateTime.Today).AddDays(1);
        var rows = DemoData.Incomes
            .Where(i => i.Date >= start && i.Date < end)
            .OrderByDescending(i => i.Date)
            .ThenByDescending(i => i.Id)
            .ToList();

        return Task.FromResult<IReadOnlyList<IncomeTransaction>>(rows);
    }

    public Task<IncomeTransaction?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(DemoData.Incomes.FirstOrDefault(i => i.Id == id));

    public Task<IncomeSaveResult> SaveAsync(
        IncomeEntry entry,
        string userName,
        bool ignorePossibleDuplicate = false,
        CancellationToken cancellationToken = default)
    {
        if (entry.Amount <= 0)
        {
            return Task.FromResult(new IncomeSaveResult(false, null, false, "El monto debe ser mayor que cero."));
        }

        if (entry.PaymentMethod.Equals("Cheque", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(entry.CheckNumber))
        {
            return Task.FromResult(new IncomeSaveResult(false, null, false, "El número de cheque es requerido."));
        }

        var id = entry.Id > 0 ? entry.Id : DemoData.NextIncomeId++;
        var account = DemoData.Accounts.FirstOrDefault(a => a.Id == entry.AccountId) ?? DemoData.Accounts[0];
        var subcategory = DemoData.IncomeSubcategories.FirstOrDefault(s => s.Id == entry.SubcategoryId) ?? DemoData.IncomeSubcategories[0];
        var member = entry.MemberId.HasValue ? DemoData.Members.FirstOrDefault(m => m.Id == entry.MemberId.Value) : null;

        var row = new IncomeTransaction(
            id,
            entry.Date.Date,
            entry.Description,
            entry.Amount,
            account.Id,
            account.Name,
            subcategory.Id,
            subcategory.Name,
            member?.Id,
            member?.Name,
            entry.PaymentMethod,
            entry.CheckNumber,
            IsDeposited: false,
            IsReconciled: false,
            IsVoided: false);

        DemoData.Incomes.RemoveAll(i => i.Id == id);
        DemoData.Incomes.Insert(0, row);
        return Task.FromResult(new IncomeSaveResult(true, id, false, null));
    }

    public Task<MemberSaveResult> CreateMemberAsync(
        MemberQuickEntry entry,
        CancellationToken cancellationToken = default)
    {
        var firstName = entry.FirstName.Trim();
        var lastName = entry.LastName.Trim();
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return Task.FromResult(new MemberSaveResult(false, null, null, "El nombre es requerido."));
        }

        var displayName = string.IsNullOrWhiteSpace(lastName) ? firstName : $"{firstName} {lastName}";
        var existing = DemoData.Members.FirstOrDefault(m => m.Name.Equals(displayName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return Task.FromResult(new MemberSaveResult(true, existing.Id, existing.Name, null));
        }

        var id = DemoData.NextMemberId++;
        DemoData.Members.Add(new LookupOption(id, displayName));
        return Task.FromResult(new MemberSaveResult(true, id, displayName, null));
    }

    public Task<IncomeSaveResult> VoidAsync(
        int id,
        string reason,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var existing = DemoData.Incomes.FirstOrDefault(i => i.Id == id);
        if (existing is null)
        {
            return Task.FromResult(new IncomeSaveResult(false, null, false, "No se encontro el ingreso demo."));
        }

        DemoData.Incomes.Remove(existing);
        DemoData.Incomes.Insert(0, existing with { IsVoided = true });
        return Task.FromResult(new IncomeSaveResult(true, id, false, null));
    }
}

public sealed class DemoCheckRepository : ICheckRepository
{
    public Task<CheckLookups> GetLookupsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new CheckLookups(
            DemoData.Accounts,
            DemoData.Expenses
                .Where(e => !e.IsVoided &&
                            e.PaymentMethod.Equals("Cheque", StringComparison.OrdinalIgnoreCase) &&
                            DemoData.Checks.All(c => c.ExpenseId != e.Id || c.Status == "Anulado"))
                .OrderByDescending(e => e.Date)
                .Select(e => new CheckExpenseOption(
                    e.Id,
                    e.Date,
                    e.Description,
                    e.Amount,
                    e.AccountId,
                    e.AccountName,
                    e.CheckNumber,
                    e.SubcategoryName))
                .ToList()));

    public Task<IReadOnlyList<CheckVoucher>> GetRecentAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CheckVoucher>>(DemoData.Checks.OrderByDescending(c => c.CheckDate).ThenByDescending(c => c.Id).ToList());

    public Task<CheckVoucher?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(DemoData.Checks.FirstOrDefault(c => c.Id == id));

    public Task<CheckSaveResult> SaveDraftAsync(
        CheckEntry entry,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var id = entry.Id > 0 ? entry.Id : DemoData.NextCheckId++;
        var account = DemoData.Accounts.FirstOrDefault(a => a.Id == entry.AccountId) ?? DemoData.Accounts[0];
        var existing = DemoData.Checks.FirstOrDefault(c => c.Id == id);

        var row = new CheckVoucher(
            id,
            entry.ExpenseId,
            account.Id,
            account.Name,
            entry.CheckNumber,
            entry.CheckDate.Date,
            entry.Payee,
            entry.PayeeAddress,
            entry.Amount,
            SpanishMoneyWriter.ToDollars(entry.Amount),
            entry.Memo,
            existing?.Status == "Impreso" ? "Impreso" : "Borrador",
            existing?.CreatedAt ?? DateTime.Now,
            existing?.CreatedBy ?? userName,
            existing?.PrintedAt,
            existing?.PrintedBy,
            existing?.VoidedAt,
            existing?.VoidedBy,
            existing?.VoidReason);

        DemoData.Checks.RemoveAll(c => c.Id == id);
        DemoData.Checks.Insert(0, row);
        return Task.FromResult(new CheckSaveResult(true, id, null));
    }

    public Task<CheckSaveResult> MarkPrintedAsync(int id, string userName, CancellationToken cancellationToken = default)
    {
        var existing = DemoData.Checks.FirstOrDefault(c => c.Id == id);
        if (existing is null)
        {
            return Task.FromResult(new CheckSaveResult(false, null, "No se encontro el cheque demo."));
        }

        DemoData.Checks.Remove(existing);
        DemoData.Checks.Insert(0, existing with { Status = "Impreso", PrintedAt = DateTime.Now, PrintedBy = userName });
        return Task.FromResult(new CheckSaveResult(true, id, null));
    }

    public Task<CheckSaveResult> VoidAsync(int id, string reason, string userName, CancellationToken cancellationToken = default)
    {
        var existing = DemoData.Checks.FirstOrDefault(c => c.Id == id);
        if (existing is null)
        {
            return Task.FromResult(new CheckSaveResult(false, null, "No se encontro el cheque demo."));
        }

        DemoData.Checks.Remove(existing);
        DemoData.Checks.Insert(0, existing with
        {
            Status = "Anulado",
            VoidedAt = DateTime.Now,
            VoidedBy = userName,
            VoidReason = reason
        });

        return Task.FromResult(new CheckSaveResult(true, id, null));
    }
}

public sealed class DemoCheckPrintSettingsRepository : ICheckPrintSettingsRepository
{
    public Task<CheckPrintSettings> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(DemoData.CheckSettings);

    public Task SaveAsync(
        CheckPrintSettingsEntry entry,
        string userName,
        CancellationToken cancellationToken = default)
    {
        DemoData.CheckSettings = new CheckPrintSettings
        {
            SheetOffsetX = entry.SheetOffsetX,
            SheetOffsetY = entry.SheetOffsetY,
            DateLeft = entry.DateLeft,
            DateTop = entry.DateTop,
            PayeeLeft = entry.PayeeLeft,
            PayeeTop = entry.PayeeTop,
            AmountLeft = entry.AmountLeft,
            AmountTop = entry.AmountTop,
            WordsLeft = entry.WordsLeft,
            WordsTop = entry.WordsTop,
            AddressLeft = entry.AddressLeft,
            AddressTop = entry.AddressTop,
            MemoLeft = entry.MemoLeft,
            MemoTop = entry.MemoTop,
            StubTitleLeft = entry.StubTitleLeft,
            StubTitleTop = entry.StubTitleTop,
            StubPayeeLeft = entry.StubPayeeLeft,
            StubPayeeTop = entry.StubPayeeTop,
            StubDateLeft = entry.StubDateLeft,
            StubDateTop = entry.StubDateTop,
            StubAccountLeft = entry.StubAccountLeft,
            StubAccountTop = entry.StubAccountTop,
            StubMemoLeft = entry.StubMemoLeft,
            StubMemoTop = entry.StubMemoTop,
            StubAmountLeft = entry.StubAmountLeft,
            StubAmountTop = entry.StubAmountTop,
            UpdatedAt = DateTime.Now,
            UpdatedBy = userName
        };

        return Task.CompletedTask;
    }
}

public sealed class DemoDepositRepository : IDepositRepository
{
    public Task<DepositLookups> GetLookupsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new DepositLookups(DemoData.Accounts));

    public Task<IReadOnlyList<DepositCandidate>> GetPendingCandidatesAsync(CancellationToken cancellationToken = default)
    {
        var rows = DemoData.Incomes
            .Where(i => !i.IsVoided && !i.IsDeposited && !i.IsReconciled && IsDepositMethod(i.PaymentMethod))
            .OrderBy(i => i.Date)
            .Select(i => new DepositCandidate(
                i.Id,
                i.Date,
                i.Description,
                i.Amount,
                i.AccountId,
                i.AccountName,
                i.SubcategoryName,
                i.MemberName,
                i.PaymentMethod,
                i.CheckNumber))
            .ToList();

        return Task.FromResult<IReadOnlyList<DepositCandidate>>(rows);
    }

    public Task<IReadOnlyList<DepositSummary>> GetRecentAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DepositSummary>>(DemoData.Deposits.OrderByDescending(d => d.DepositDate).ThenByDescending(d => d.Id).ToList());

    public Task<DepositSaveResult> CreateAsync(
        DepositEntry entry,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var selected = DemoData.Incomes
            .Where(i => entry.TransactionIds.Contains(i.Id) && !i.IsVoided && !i.IsDeposited && IsDepositMethod(i.PaymentMethod))
            .ToList();

        if (selected.Count == 0)
        {
            return Task.FromResult(new DepositSaveResult(false, null, "Selecciona al menos un ingreso pendiente."));
        }

        if (selected.Any(i => i.AccountId != entry.AccountId))
        {
            return Task.FromResult(new DepositSaveResult(false, null, "Uno o mas ingresos no pertenecen a la cuenta bancaria seleccionada."));
        }

        var expected = selected.Sum(i => i.Amount);
        if (expected != entry.ActualTotal)
        {
            return Task.FromResult(new DepositSaveResult(false, null, "El total real debe cuadrar con los ingresos seleccionados."));
        }

        var account = DemoData.Accounts.FirstOrDefault(a => a.Id == entry.AccountId) ?? DemoData.Accounts[0];
        var id = DemoData.NextDepositId++;
        DemoData.Deposits.Insert(0, new DepositSummary(
            id,
            entry.DepositDate.Date,
            account.Id,
            account.Name,
            expected,
            entry.ActualTotal,
            selected.Count,
            "Registrado",
            userName,
            DateTime.Now));

        foreach (var income in selected)
        {
            DemoData.Incomes.Remove(income);
            DemoData.Incomes.Add(income with { IsDeposited = true });
        }

        DemoData.AccountBalances[account.Id] += entry.ActualTotal;
        return Task.FromResult(new DepositSaveResult(true, id, null));
    }

    public Task<DepositBatchSaveResult> CreateBatchAsync(
        IReadOnlyList<DepositEntry> entries,
        string userName,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return Task.FromResult(new DepositBatchSaveResult(false, [], "No hay depósitos para crear."));
        }

        foreach (var entry in entries)
        {
            var selected = DemoData.Incomes
                .Where(i => entry.TransactionIds.Contains(i.Id) && !i.IsVoided && !i.IsDeposited && IsDepositMethod(i.PaymentMethod))
                .ToList();

            if (selected.Count != entry.TransactionIds.Distinct().Count())
            {
                return Task.FromResult(new DepositBatchSaveResult(false, [], "Uno o mas ingresos seleccionados ya no estan disponibles."));
            }

            if (selected.Any(i => i.AccountId != entry.AccountId))
            {
                return Task.FromResult(new DepositBatchSaveResult(false, [], "Uno o mas ingresos no pertenecen a la cuenta bancaria seleccionada."));
            }

            if (selected.Sum(i => i.Amount) != entry.ActualTotal)
            {
                return Task.FromResult(new DepositBatchSaveResult(false, [], "El total real debe cuadrar con los ingresos seleccionados."));
            }
        }

        var ids = new List<int>();
        foreach (var entry in entries)
        {
            var result = CreateAsync(entry, userName, cancellationToken).Result;
            if (!result.Saved || !result.DepositId.HasValue)
            {
                return Task.FromResult(new DepositBatchSaveResult(false, [], result.ErrorMessage ?? "No se pudo crear uno de los depósitos."));
            }

            ids.Add(result.DepositId.Value);
        }

        return Task.FromResult(new DepositBatchSaveResult(true, ids, null));
    }

    public Task<DepositSaveResult> VoidAsync(
        int id,
        string reason,
        string userName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Task.FromResult(new DepositSaveResult(false, null, "Debes especificar un motivo de anulacion."));
        }

        var existing = DemoData.Deposits.FirstOrDefault(d => d.Id == id);
        if (existing is null)
        {
            return Task.FromResult(new DepositSaveResult(false, null, "No se encontro el deposito demo."));
        }

        if (existing.Status == "Conciliado")
        {
            return Task.FromResult(new DepositSaveResult(false, null, "No se puede anular un deposito conciliado."));
        }

        DemoData.Deposits.Remove(existing);
        DemoData.Deposits.Insert(0, existing with { Status = "Anulado" });
        DemoData.AccountBalances[existing.AccountId] -= existing.ActualTotal;
        return Task.FromResult(new DepositSaveResult(true, id, null));
    }

    private static bool IsDepositMethod(string method) =>
        method.Equals("Efectivo", StringComparison.OrdinalIgnoreCase) ||
        method.Equals("Cheque", StringComparison.OrdinalIgnoreCase);
}

public sealed class DemoReconciliationRepository : IReconciliationRepository
{
    public Task<ReconciliationLookups> GetLookupsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ReconciliationLookups(DemoData.Accounts));

    public Task<ReconciliationWorkspace> GetWorkspaceAsync(
        int accountId,
        DateTime statementDate,
        CancellationToken cancellationToken = default)
    {
        var deposits = DemoData.Deposits
            .Where(d => d.AccountId == accountId && d.Status == "Registrado")
            .Select(d => new ReconciliationCandidate(d.Id, "Deposito", d.DepositDate, $"Deposito #{d.Id}", d.ActualTotal, d.Id.ToString()))
            .ToList();

        var transactions = DemoData.Incomes
            .Where(i => i.AccountId == accountId && !i.IsReconciled && !i.IsVoided && i.IsDeposited)
            .Select(i => new ReconciliationCandidate(i.Id, "Ingreso", i.Date, i.Description, i.Amount, i.CheckNumber ?? i.Id.ToString()))
            .Concat(DemoData.Expenses
                .Where(e => e.AccountId == accountId && !e.IsReconciled && !e.IsVoided)
                .Select(e => new ReconciliationCandidate(e.Id, "Pago", e.Date, e.Description, -e.Amount, e.CheckNumber ?? e.Id.ToString())))
            .OrderBy(t => t.Date)
            .ToList();

        var latest = DemoData.Reconciliations
            .Where(r => r.AccountId == accountId && r.ReconciliationDate < statementDate.Date)
            .OrderByDescending(r => r.ReconciliationDate)
            .FirstOrDefault();

        return Task.FromResult(new ReconciliationWorkspace(
            latest?.StatementBalance ?? DemoData.AccountBalances.GetValueOrDefault(accountId),
            latest?.ReconciliationDate,
            deposits,
            transactions,
            DemoData.Reconciliations.Where(r => r.AccountId == accountId).ToList()));
    }

    public Task<ReconciliationSaveResult> CloseAsync(
        ReconciliationEntry entry,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var account = DemoData.Accounts.FirstOrDefault(a => a.Id == entry.AccountId) ?? DemoData.Accounts[0];
        var id = DemoData.NextReconciliationId++;
        DemoData.Reconciliations.Insert(0, new ReconciliationSummary(
            id,
            account.Id,
            account.Name,
            entry.StatementDate.Date,
            entry.StatementBalance));

        return Task.FromResult(new ReconciliationSaveResult(true, id, null));
    }
}

public sealed class DemoReportRepository : IReportRepository
{
    private static readonly IReadOnlyList<ReportDefinition> Catalog =
    [
        new("profit-loss", "Profit and Loss", "Resumen de ingresos, gastos y net income por categoría.", true),
        new("profit-loss-detail", "Profit and Loss Detail", "Detalle transaccional del Profit and Loss.", true),
        new("balance-sheet", "Balance Sheet", "Balance por cuentas bancarias activas.", true),
        new("tithes-members", "Diezmos (Miembros que diezman)", "Miembros con diezmos registrados en el periodo.", true)
    ];

    public Task<IReadOnlyList<ReportDefinition>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Catalog);

    public Task<IReadOnlyList<ReportBankAccount>> GetBankAccountsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ReportBankAccount>>(DemoData.Accounts
            .Select(account => new ReportBankAccount(account.Id, account.Name))
            .ToList());

    public Task<FinancialReport> GetReportAsync(
        ReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var income = DemoData.Incomes
            .Where(i => !i.IsVoided && i.Date.Date >= request.StartDate.Date && i.Date.Date <= request.EndDate.Date)
            .Where(i => request.AccountId is null || i.AccountId == request.AccountId.Value)
            .GroupBy(i => i.SubcategoryName)
            .Select(group => new ReportLine($"Ingreso:{group.Key}", group.Key, group.Sum(i => i.Amount), 1, false, true, []))
            .ToList();

        var expenseTotal = DemoData.Checks
            .Where(c => c.Status != "Anulado")
            .Where(c => request.AccountId is null || c.AccountId == request.AccountId.Value)
            .Sum(c => c.Amount);
        var expenses = new List<ReportLine>
        {
            new("Egreso:Cheques", "Cheques", expenseTotal, 1, false, true, [])
        };

        var totalIncome = income.Sum(i => i.Amount);
        var sections = new List<ReportSection>
        {
            new("Income", AddTotal(income, "Total for Income", totalIncome), totalIncome),
            new("Expenses", AddTotal(expenses, "Total for Expenses", expenseTotal), expenseTotal)
        };

        var report = new FinancialReport(
            request.Key,
            Catalog.FirstOrDefault(r => r.Key == request.Key)?.Name ?? "Profit and Loss",
            $"{request.StartDate:MMMM d, yyyy} - {request.EndDate:MMMM d, yyyy}",
            sections,
            totalIncome,
            expenseTotal,
            totalIncome - expenseTotal,
            [new ReportInsight("Total:Total for Income", "Demo insight", "Este insight confirma que el panel de IA está conectado al reporte.", "Info")],
            DateTime.Now);

        return Task.FromResult(report);
    }

    private static IReadOnlyList<ReportLine> AddTotal(IReadOnlyList<ReportLine> lines, string label, decimal total)
    {
        var result = lines.ToList();
        result.Add(new ReportLine($"Total:{label}", label, total, 0, true, true, []));
        return result;
    }
}

internal static class DemoData
{
    public static readonly AuthenticatedUser User = new(1, "demo", "Pastor Demo", ["Administrador", "Finanzas"]);

    public static readonly List<LookupOption> Accounts =
    [
        new(1, "Checking-6163"),
        new(2, "Ahorros-8698"),
        new(3, "Misiones-9012")
    ];

    public static readonly Dictionary<int, decimal> AccountBalances = new()
    {
        [1] = 42_815.22m,
        [2] = 78_640.10m,
        [3] = 11_225.50m
    };

    public static readonly List<LookupOption> IncomeSubcategories =
    [
        new(101, "Diezmos"),
        new(102, "Ofrendas"),
        new(103, "Misiones"),
        new(104, "Fondo de Construccion")
    ];

    public static readonly List<LookupOption> ExpenseSubcategories =
    [
        new(201, "Office Expenses (Oficina)"),
        new(202, "Utilities (Utilidades)"),
        new(203, "Payroll Taxes"),
        new(204, "Activity Expenses (Actividades)")
    ];

    public static readonly List<LookupOption> Members =
    [
        new(1, "Angel Ruiz"),
        new(2, "Marisol Rivera"),
        new(3, "Luis Santiago"),
        new(4, "Familia Torres"),
        new(5, "Visitante Anonimo")
    ];

    public static int NextIncomeId = 5004;
    public static int NextMemberId = 6;
    public static int NextExpenseId = 6004;
    public static int NextCheckId = 3003;
    public static int NextDepositId = 7002;
    public static int NextReconciliationId = 9002;
    public static CheckPrintSettings CheckSettings = CheckPrintSettings.Defaults();

    public static readonly List<IncomeTransaction> Incomes =
    [
        new(5003, DateTime.Today.AddDays(-1), "Servicio domingo", 3240.00m, 1, "Checking-6163", 101, "Diezmos", 1, "Angel Ruiz", "Cheque", "1842", false, false, false),
        new(5002, DateTime.Today.AddDays(-2), "Ofrenda general", 1875.50m, 1, "Checking-6163", 102, "Ofrendas", null, null, "Efectivo", null, false, false, false),
        new(5001, DateTime.Today.AddDays(-5), "Promesa misionera", 650.00m, 3, "Misiones-9012", 103, "Misiones", 3, "Luis Santiago", "Zelle", null, true, false, false)
    ];

    public static readonly List<ExpenseTransaction> Expenses =
    [
        new(6003, DateTime.Today.AddDays(-1), "Northwest Utilities", 428.73m, 1, "Checking-6163", 202, "Utilities (Utilidades)", "Cheque", "2108", false, false),
        new(6002, DateTime.Today.AddDays(-4), "Sams Club", 101.46m, 1, "Checking-6163", 204, "Activity Expenses (Actividades)", "Tarjeta", null, false, false),
        new(6001, DateTime.Today.AddDays(-8), "Office supplies", 62.09m, 1, "Checking-6163", 201, "Office Expenses (Oficina)", "ACH", null, false, false)
    ];

    public static readonly List<CheckVoucher> Checks =
    [
        new(3002, null, 1, "Checking-6163", "2108", DateTime.Today, "Northwest Utilities", "1200 Market St.\nMerrillville, IN 46410", 428.73m, SpanishMoneyWriter.ToDollars(428.73m), "Electricidad santuario", "Borrador", DateTime.Now.AddHours(-3), "demo", null, null, null, null, null),
        new(3001, null, 1, "Checking-6163", "2107", DateTime.Today.AddDays(-3), "Ministerio de Misiones", null, 1200.00m, SpanishMoneyWriter.ToDollars(1200.00m), "Apoyo mensual", "Impreso", DateTime.Now.AddDays(-3), "demo", DateTime.Now.AddDays(-3), "demo", null, null, null)
    ];

    public static readonly List<DepositSummary> Deposits =
    [
        new(7001, DateTime.Today.AddDays(-7), 1, "Checking-6163", 5125.75m, 5125.75m, 8, "Registrado", "demo", DateTime.Now.AddDays(-7))
    ];

    public static readonly List<ReconciliationSummary> Reconciliations =
    [
        new(9001, 1, "Checking-6163", DateTime.Today.AddMonths(-1), 37_820.25m)
    ];
}
