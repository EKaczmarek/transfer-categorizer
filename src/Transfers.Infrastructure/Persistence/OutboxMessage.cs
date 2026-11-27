public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = default!;
    public string Payload { get; set; } = default!;      
    public DateTimeOffset OccurredAt { get; set; }       
    public DateTimeOffset? ProcessedAt { get; set; }
}