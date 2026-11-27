namespace Transfers.Contracts;

public sealed record TransferCreated(
    Guid MessageId, Guid TransferId, decimal Amount,
    string Currency, string Description, DateTimeOffset CreatedAt);

public sealed record TransferCategorized(
    Guid MessageId, Guid TransferId, string Category, DateTimeOffset CategorizedAt);