using System.ComponentModel.DataAnnotations;

namespace CFS.Core.Models;

public sealed record DepositLookups(IReadOnlyList<LookupOption> Accounts);

public sealed record DepositCandidate(
    int TransactionId,
    DateTime Date,
    string Description,
    decimal Amount,
    int AccountId,
    string AccountName,
    string SubcategoryName,
    string? MemberName,
    string PaymentMethod,
    string? CheckNumber);

public sealed record DepositSummary(
    int Id,
    DateTime DepositDate,
    int AccountId,
    string AccountName,
    decimal ExpectedTotal,
    decimal ActualTotal,
    int ItemCount,
    string Status,
    string CreatedBy,
    DateTime CreatedAt);

public sealed class DepositEntry
{
    [Required(ErrorMessage = "La fecha es requerida.")]
    public DateTime DepositDate { get; set; } = DateTime.Today;

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una cuenta bancaria.")]
    public int AccountId { get; set; }

    [Range(0.01, 999999999, ErrorMessage = "El total real debe ser mayor que cero.")]
    public decimal ActualTotal { get; set; }

    [StringLength(300, ErrorMessage = "La nota no puede exceder 300 caracteres.")]
    public string? Notes { get; set; }

    public List<int> TransactionIds { get; set; } = [];
}

public sealed record DepositSaveResult(
    bool Saved,
    int? DepositId,
    string? ErrorMessage);

public sealed record DepositBatchSaveResult(
    bool Saved,
    IReadOnlyList<int> DepositIds,
    string? ErrorMessage);
