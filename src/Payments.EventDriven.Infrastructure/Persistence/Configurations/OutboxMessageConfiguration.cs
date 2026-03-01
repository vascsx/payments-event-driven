using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payments.EventDriven.Domain.Entities;

namespace Payments.EventDriven.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("id");

        builder.Property(o => o.Topic)
            .HasColumnName("topic")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(o => o.MessageKey)
            .HasColumnName("message_key")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(o => o.Payload)
            .HasColumnName("payload")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(o => o.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(255);

        builder.Property(o => o.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(o => o.ProcessedAt)
            .HasColumnName("processed_at");

        builder.Property(o => o.RetryCount)
            .HasColumnName("retry_count")
            .IsRequired();

        builder.Property(o => o.LastRetryAt)
            .HasColumnName("last_retry_at");

        builder.Property(o => o.LastError)
            .HasColumnName("last_error")
            .HasColumnType("text");

        builder.Property(o => o.Status)
            .HasColumnName("status")
            .IsRequired();

        // Índices para otimizar queries do processor
        builder.HasIndex(o => new { o.Status, o.CreatedAt })
            .HasDatabaseName("idx_outbox_status_created");

        builder.HasIndex(o => o.ProcessedAt)
            .HasDatabaseName("idx_outbox_processed_at");
    }
}
