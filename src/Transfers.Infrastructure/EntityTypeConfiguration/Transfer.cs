using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transfers.Domain;

namespace Transfers.Infrastructure.Persistence;

public class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("transfers");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();   // Guid tworzymy w domenie

        builder.Property(t => t.Amount)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(t => t.Currency)
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.Category)
            .HasConversion<string>()
            .HasMaxLength(30);                               // nullable, bo Category?

        builder.HasIndex(t => t.Status).HasDatabaseName("ix_transfers_status");
        builder.HasIndex(t => t.CreatedAt).HasDatabaseName("ix_transfers_created_at");

    }
}