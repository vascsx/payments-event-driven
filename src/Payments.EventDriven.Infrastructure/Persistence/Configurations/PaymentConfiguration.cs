using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payments.EventDriven.Domain.Entities;
using Payments.EventDriven.Domain.Enums;

namespace Payments.EventDriven.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id");

        builder.Property(p => p.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(p => p.Currency)
            .HasColumnName("currency")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.Type)
            .HasColumnName("type")
            .IsRequired()
            .HasDefaultValue(PaymentType.Default); 

        builder.Property(p => p.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(255);

        // Índice único para prevenir pagamentos duplicados
        builder.HasIndex(p => p.IdempotencyKey)
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL")
            .HasDatabaseName("idx_payment_idempotency_key");

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .IsRequired();
        
        builder.Property(p => p.FailureReason)
            .HasColumnName("failure_reason")
            .HasColumnType("text");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}