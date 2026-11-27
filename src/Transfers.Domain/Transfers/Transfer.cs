namespace Transfers.Domain;

public class Transfer
{
    public Guid Id { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }    
    public string Description { get; private  set; }
    public TransferStatus Status { get; private set; }
    public string? Category { get; private set;}
    public DateTime CreatedAt { get; private set; }
    public const int MaxDescriptionLength = 200;  
    private Transfer()
    {
        Currency = null!;
        Description = null!;
    }
    private Transfer(decimal amount, string currency, string description, DateTime date, TransferStatus? status = null, TransferCategory? category = null)
    {
        Id = Guid.NewGuid();
        Amount = amount;
        Currency = currency;
        Description = description;
        CreatedAt = date;
    }


    public static Transfer Create(decimal amount, string currency, string description, DateTime createdAt)
    {
        if (amount == 0)
            throw new ArgumentException("Amount must not be zero.", nameof(amount));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter code.", nameof(currency));

        description = description.Trim(); 

        if (description.Length > MaxDescriptionLength)
            throw new ArgumentException(
                $"Description cannot exceed {MaxDescriptionLength} characters.",
                nameof(description));

        return new Transfer(amount, currency.ToUpperInvariant(), description.Trim(), createdAt);
    }

    public void MarkCategorized(string category)
    {
        if (Status == TransferStatus.Categorized)
            throw new InvalidOperationException("Transfer is already categorized.");

        Category = category;
        Status = TransferStatus.Categorized;
    }
}
