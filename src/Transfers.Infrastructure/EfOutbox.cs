using System.Text.Json;

using Transfers.Application;
using Transfers.Infrastructure.Persistence;

internal sealed class EfOutbox(TransfersDbContext db) : IOutbox
{
    public void Add(string type, object @event, Guid messageId) =>
        db.Outbox.Add(new OutboxMessage { Id = messageId, Type = type, Payload = JsonSerializer.Serialize(@event), OccurredAt = DateTimeOffset.UtcNow });
}