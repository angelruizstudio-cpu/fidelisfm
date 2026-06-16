namespace CFS.Core.Models;

public sealed record BankAccountBalance(
    int Id,
    string Name,
    decimal CurrentBalance);

