using System.ComponentModel.DataAnnotations;

namespace CFS.Core.Models;

public sealed record LookupOption(int Id, string Name);

public sealed record IncomeLookups(
    IReadOnlyList<LookupOption> Accounts,
    IReadOnlyList<LookupOption> Subcategories,
    IReadOnlyList<LookupOption> Members,
    IReadOnlyList<string> PaymentMethods);

public sealed record IncomeTransaction(
    int Id,
    DateTime Date,
    string Description,
    decimal Amount,
    int AccountId,
    string AccountName,
    int SubcategoryId,
    string SubcategoryName,
    int? MemberId,
    string? MemberName,
    string PaymentMethod,
    string? CheckNumber,
    bool IsDeposited,
    bool IsReconciled,
    bool IsVoided)
{
    public bool CanModify => !IsDeposited && !IsReconciled && !IsVoided;
}

public sealed class IncomeEntry : IValidatableObject
{
    public int Id { get; set; } = -1;

    [Required(ErrorMessage = "La fecha es requerida.")]
    public DateTime Date { get; set; } = DateTime.Today;

    [StringLength(300, ErrorMessage = "La descripción no puede exceder 300 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 999999999, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una cuenta.")]
    public int AccountId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una subcategoría.")]
    public int SubcategoryId { get; set; }

    public int? MemberId { get; set; }

    [Required(ErrorMessage = "Selecciona un método de pago.")]
    [StringLength(20, ErrorMessage = "El método no puede exceder 20 caracteres.")]
    public string PaymentMethod { get; set; } = "Efectivo";

    [StringLength(50, ErrorMessage = "El número de cheque no puede exceder 50 caracteres.")]
    public string? CheckNumber { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PaymentMethod.Equals("Cheque", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(CheckNumber))
        {
            yield return new ValidationResult(
                "El número de cheque es requerido.",
                [nameof(CheckNumber)]);
        }
    }
}

public sealed record IncomeSaveResult(
    bool Saved,
    int? TransactionId,
    bool PossibleDuplicate,
    string? ErrorMessage);

public sealed class MemberQuickEntry
{
    [Required(ErrorMessage = "El nombre es requerido.")]
    [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres.")]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "El apellido no puede exceder 100 caracteres.")]
    public string LastName { get; set; } = string.Empty;
}

public sealed record MemberSaveResult(
    bool Saved,
    int? MemberId,
    string? DisplayName,
    string? ErrorMessage);
