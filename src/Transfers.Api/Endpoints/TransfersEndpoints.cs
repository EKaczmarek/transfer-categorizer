using Microsoft.EntityFrameworkCore;

using Transfers.Application;
using Transfers.Contracts;
using Transfers.Domain;
using Transfers.Infrastructure.Persistence;

public static class TransfersEndpoints
{
    public static IEndpointRouteBuilder MapTransfers(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/transfers");

        group.MapPost("", async (
            CreateTransferRequest req,
            TransfersDbContext db,
            TimeProvider clock,
            IOutbox outbox,
            CancellationToken ct) =>
        {
            Transfer transfer;
            try
            {
                transfer = Transfer.Create(
                    req.Amount, req.Currency, req.Description,
                    clock.GetUtcNow().UtcDateTime);
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(
                    title: "Invalid transfer",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }

                var messageId = Guid.NewGuid();

            db.Transfers.Add(transfer);
            outbox.Add(
                OutboxMessageTypes.TransferCreated.ToString(),
                new TransferCreated(
                    messageId,
                    transfer.Id,
                    transfer.Amount,
                    transfer.Currency,
                    transfer.Description,
                    clock.GetUtcNow()),
                messageId);

            await db.SaveChangesAsync(ct);   // JEDEN zapis: transfer + outbox w jednej transakcji

            return Results.Created($"/transfers/{transfer.Id}", TransferResponse.From(transfer));

        });

        group.MapGet("/{id:guid}", async (Guid id, TransfersDbContext db, CancellationToken ct) =>
        {
            var transfer = await db.Transfers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
            return transfer is null ? Results.NotFound() : Results.Ok(TransferResponse.From(transfer));
        });

        return app;
    }
}