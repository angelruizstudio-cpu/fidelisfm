using System.ComponentModel.DataAnnotations;

namespace CFS.Core.Models;

public sealed record ExpenseLookups(
    IReadOnlyList<LookupOption> Accounts,
    IReadOnlyList<LookupOption> Subcategories,
    IReadOnlyList<string> PaymentMethods);

public sealed record ExpenseTransaction(
    int Id,
    DateTime Date,
    string Description,
    decimal Amount,
    int AccountId,
    string AccountName,
    int SubcategoryId,
    string SubcategoryName,
    string PaymentMethod,
    string? CheckNumber,
    bool IsReconciled,
    bool IsVoided)
{
    public bool CanModify => !IsReconciled && !IsVoided;
}

public sealed class ExpenseEntry
{
    public int Id { get; set; } = -1;

    [Required(ErrorMessage = "La fecha es requerida.")]
    public DateTime Date { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "La descripción es requerida.")]
    [StringLength(300, ErrorMessage = "La descripción no puede exceder 300 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 999999999, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una cuenta.")]
    public int AccountId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una subcategoría.")]
    public int SubcategoryId { get; set; }

    [Required(ErrorMessage = "Selecciona un método de pago.")]
    [StringLength(20, ErrorMessage = "El método no puede exceder 20 caracteres.")]
    public string PaymentMethod { get; set; } = "Cheque";

    [StringLength(50, ErrorMessage = "El número de cheque no puede exceder 50 caracteres.")]
    public string? CheckNumber { get; set; }
}

public sealed record ExpenseSaveResult(
    bool Saved,
    int? TransactionId,
    bool PossibleDuplicate,
    string? ErrorMessage);
