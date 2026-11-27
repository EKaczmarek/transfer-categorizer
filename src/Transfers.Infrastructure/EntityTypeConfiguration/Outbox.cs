using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Transfers.Infrastructure.Persistence.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasMaxLength(100).IsRequired();
        b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.OccurredAt).IsRequired();

        b.HasIndex(x => x.OccurredAt)
         .HasFilter("\"ProcessedAt\" IS NULL")
         .HasDatabaseName("ix_outbox_pending");
    }
}