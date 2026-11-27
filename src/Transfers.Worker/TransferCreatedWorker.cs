using System.Text.Json;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using Transfers.Application;
using Transfers.Contracts;
using Transfers.Infrastructure.Messaging;
using Transfers.Infrastructure.Persistence;

public sealed class TransferCreatedConsumer(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger<TransferCreatedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var conn = await connection.GetAsync(ct);          // przy braku brokera: patrz uwaga niżej
        var channel = await conn.CreateChannelAsync(cancellationToken: ct);
        await RabbitMqTopology.DeclareAsync(channel, ct);
        await channel.BasicQosAsync(0, 10, false, ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                await HandleAsync(ea.Body.ToArray(), ct);
                await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process message {MessageId}", ea.BasicProperties.MessageId);
                await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, ct);
            }
        };

        await channel.BasicConsumeAsync(RabbitMqTopology.CategorizerQueue, autoAck: false, consumer, ct);
        await Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => { });   // czekaj do zamknięcia
        await channel.DisposeAsync();
    }

    private async Task HandleAsync(byte[] body, CancellationToken ct)
    {
        var evt = JsonSerializer.Deserialize<TransferCreated>(body)!;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransfersDbContext>();
        var categorizer = scope.ServiceProvider.GetRequiredService<ITransferCategorizer>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();

        var transfer = await db.Transfers.FindAsync([evt.TransferId], ct)
            ?? throw new InvalidOperationException($"Transfer {evt.TransferId} not found");

        if (transfer.Status == TransferStatus.Categorized) return;   // duplikat: po prostu ack

        transfer.MarkCategorized(categorizer.Categorize(transfer.Description));

        var messageId = Guid.NewGuid();
        outbox.Add(OutboxMessageTypes.TransferCategorized,
            new TransferCategorized(messageId, transfer.Id, transfer.Category!, DateTimeOffset.UtcNow),
            messageId);

        await db.SaveChangesAsync(ct);   // jedna transakcja: status + outbox
    }
}