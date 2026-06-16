using System.ComponentModel.DataAnnotations;

namespace CFS.Core.Models;

public sealed record CheckLookups(
    IReadOnlyList<LookupOption> Accounts,
    IReadOnlyList<CheckExpenseOption> PendingExpenses);

public sealed record CheckExpenseOption(
    int Id,
    DateTime Date,
    string Description,
    decimal Amount,
    int AccountId,
    string AccountName,
    string? CheckNumber,
    string SubcategoryName);

public sealed record CheckVoucher(
    int Id,
    int? ExpenseId,
    int AccountId,
    string AccountName,
    string CheckNumber,
    DateTime CheckDate,
    string Payee,
    string? PayeeAddress,
    decimal Amount,
    string AmountInWords,
    string? Memo,
    string Status,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? PrintedAt,
    string? PrintedBy,
    DateTime? VoidedAt,
    string? VoidedBy,
    string? VoidReason);

public sealed class CheckEntry
{
    public int Id { get; set; }

    public int? ExpenseId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una cuenta bancaria.")]
    public int AccountId { get; set; }

    [Required(ErrorMessage = "El numero de cheque es requerido.")]
    [StringLength(50, ErrorMessage = "El numero de cheque no puede exceder 50 caracteres.")]
    public string CheckNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha es requerida.")]
    public DateTime CheckDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "El beneficiario es requerido.")]
    [StringLength(200, ErrorMessage = "El beneficiario no puede exceder 200 caracteres.")]
    public string Payee { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "La direccion no puede exceder 300 caracteres.")]
    public string? PayeeAddress { get; set; }

    [Range(0.01, 999999999, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    [StringLength(300, ErrorMessage = "El memo no puede exceder 300 caracteres.")]
    public string? Memo { get; set; }
}

public sealed record CheckSaveResult(
    bool Saved,
    int? CheckId,
    string? ErrorMessage);
