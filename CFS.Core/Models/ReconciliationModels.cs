using System.ComponentModel.DataAnnotations;

namespace CFS.Core.Models;

public sealed record ReconciliationLookups(IReadOnlyList<LookupOption> Accounts);

public sealed record ReconciliationCandidate(
    int Id,
    string Source,
    DateTime Date,
    string Description,
    decimal Amount,
    string Reference);

public sealed record ReconciliationSummary(
    int Id,
    int AccountId,
    string AccountName,
    DateTime ReconciliationDate,
    decimal StatementBalance);

public sealed class ReconciliationEntry
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una cuenta bancaria.")]
    public int AccountId { get; set; }

    [Required(ErrorMessage = "La fecha del estado de cuenta es requerida.")]
    public DateTime StatementDate { get; set; } = DateTime.Today;

    [Range(typeof(decimal), "-999999999", "999999999", ErrorMessage = "El saldo del estado de cuenta no es valido.")]
    public decimal StatementBalance { get; set; }

    public List<int> DepositIds { get; set; } = [];

    public List<int> TransactionIds { get; set; } = [];
}

public sealed record ReconciliationWorkspace(
    decimal BeginningBalance,
    DateTime? LastStatementDate,
    IReadOnlyList<ReconciliationCandidate> Deposits,
    IReadOnlyList<ReconciliationCandidate> Transactions,
    IReadOnlyList<ReconciliationSummary> Recent);

public sealed record ReconciliationSaveResult(
    bool Saved,
    int? ReconciliationId,
    string? ErrorMessage);
