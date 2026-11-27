using System.Text;
using RabbitMQ.Client;
using Transfers.Application;

namespace Transfers.Infrastructure.Messaging;

internal sealed class RabbitMqPublisher(RabbitMqConnection connection)
    : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IChannel? _channel;

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;

        await _lock.WaitAsync(ct);
        try
        {
            if (_channel is { IsOpen: true }) return _channel;

            var conn = await connection.GetAsync(ct);

            // Publisher confirms: BasicPublishAsync kończy się dopiero po potwierdzeniu przez broker,
            // a przy odmowie rzuca wyjątek.
            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);

            _channel = await conn.CreateChannelAsync(channelOptions, ct);
            await RabbitMqTopology.DeclareAsync(_channel, ct);
            return _channel;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task PublishAsync(string routingKey, Guid messageId, string payload, CancellationToken ct)
    {
        var channel = await GetChannelAsync(ct);

        var props = new BasicProperties
        {
            Persistent = true,
            MessageId = messageId.ToString(),
            ContentType = "application/json"
        };

        await channel.BasicPublishAsync(
            exchange: RabbitMqTopology.Exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: Encoding.UTF8.GetBytes(payload),
            cancellationToken: ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        _lock.Dispose();
    }
}