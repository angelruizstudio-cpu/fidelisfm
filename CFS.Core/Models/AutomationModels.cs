using System.ComponentModel.DataAnnotations;

namespace CFS.Core.Models;

public static class AutomationTransactionType
{
    public const string Income = "Ingreso";
    public const string Expense = "Egreso";
}

public static class AutomationFrequency
{
    public const string Weekly = "Semanal";
    public const string BiWeekly = "Quincenal";
    public const string Monthly = "Mensual";
}

public sealed record AutomationLookups(
    IReadOnlyList<LookupOption> Accounts,
    IReadOnlyList<LookupOption> IncomeSubcategories,
    IReadOnlyList<LookupOption> ExpenseSubcategories);

public sealed class AutomationRuleEntry : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre de la regla es requerido.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string TransactionType { get; set; } = AutomationTransactionType.Expense;

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una cuenta bancaria.")]
    public int AccountId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una subcategoria.")]
    public int SubcategoryId { get; set; }

    [Range(0.01, 999999999, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    [Required]
    public string Frequency { get; set; } = AutomationFrequency.Monthly;

    [Required(ErrorMessage = "La proxima fecha de ejecucion es requerida.")]
    public DateTime NextRunDate { get; set; } = DateTime.Today;

    [StringLength(300)]
    public string? Description { get; set; }

    public bool Active { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TransactionType != AutomationTransactionType.Income && TransactionType != AutomationTransactionType.Expense)
        {
            yield return new ValidationResult("Tipo de transaccion invalido.", [nameof(TransactionType)]);
        }

        if (Frequency != AutomationFrequency.Weekly && Frequency != AutomationFrequency.BiWeekly && Frequency != AutomationFrequency.Monthly)
        {
            yield return new ValidationResult("Frecuencia invalida.", [nameof(Frequency)]);
        }
    }
}

public sealed record AutomationRule(
    int Id,
    string Name,
    string TransactionType,
    int AccountId,
    string AccountName,
    int SubcategoryId,
    string SubcategoryName,
    decimal Amount,
    string Frequency,
    DateTime NextRunDate,
    string? Description,
    bool Active);

public sealed record AutomationSaveResult(bool Saved, int? Id, string? ErrorMessage);

public sealed record AutomationRunResult(int RulesExecuted, IReadOnlyList<string> Messages);
