using RabbitMQ.Client;
using Transfers.Application;

namespace Transfers.Infrastructure.Messaging;

public static class RabbitMqTopology
{
    public const string Exchange = "transfers";
    public const string CategorizerQueue = "transfers.categorizer";
    public const string AuditQueue = "transfers.audit";

    // Idempotentne: można wywołać wielokrotnie, nic się nie zepsuje.
    public static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(
            Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);

        await channel.QueueDeclareAsync(
            CategorizerQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(
            CategorizerQueue, Exchange, OutboxMessageTypes.TransferCreated, cancellationToken: ct);

        await channel.QueueDeclareAsync(
            AuditQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(
            AuditQueue, Exchange, OutboxMessageTypes.TransferCategorized, cancellationToken: ct);
    }
}