using Microsoft.EntityFrameworkCore;

using Transfers.Infrastructure.Persistence;

public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try { await DispatchBatchAsync(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { logger.LogError(ex, "Outbox dispatch failed, retrying on next tick"); }
            }
        }
        catch (OperationCanceledException) { /* zamykanie aplikacji */ }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransfersDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var batch = await db.Outbox
            .FromSqlRaw("""
                SELECT * FROM outbox_messages
                WHERE "ProcessedAt" IS NULL
                ORDER BY "OccurredAt"
                LIMIT 50
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        if (batch.Count == 0) return;

        foreach (var m in batch)
        {
            await publisher.PublishAsync(m.Type, m.Id, m.Payload, ct);
            m.ProcessedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}