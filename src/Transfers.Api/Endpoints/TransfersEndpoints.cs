using Microsoft.EntityFrameworkCore;

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

            db.Transfers.Add(transfer);
            await db.SaveChangesAsync(ct);
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