using Transfers.Domain;

public sealed record TransferResponse(
    Guid Id, decimal Amount, string Currency, string Description,
    DateTime CreatedAt, string Status, string? Category)
{
    public static TransferResponse From(Transfer t) =>
        new(t.Id, t.Amount, t.Currency, t.Description, t.CreatedAt, t.Status.ToString(), t.Category);
}