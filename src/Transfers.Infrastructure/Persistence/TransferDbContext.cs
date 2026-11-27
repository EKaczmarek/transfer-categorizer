using Microsoft.EntityFrameworkCore;
using Transfers.Domain;

namespace Transfers.Infrastructure.Persistence;

public class TransfersDbContext(DbContextOptions<TransfersDbContext> options) : DbContext(options)
{
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransfersDbContext).Assembly);
    }
}