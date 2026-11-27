public interface IMessagePublisher
{
    Task PublishAsync(string routingKey, Guid messageId, string payload, CancellationToken ct);
}