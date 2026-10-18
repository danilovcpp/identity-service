using System.Text.Json;
using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", table =>
        {
            table.HasCheckConstraint("ck_outbox_state", "attempt_count >= 0 AND next_attempt_at >= created_at AND (processed_at IS NULL) = (payload IS NOT NULL) AND (processed_at IS NULL OR processed_at >= created_at)");
            table.HasCheckConstraint("ck_outbox_error", "last_error IS NULL OR last_error ~ '^[a-z0-9_]{1,80}$'");
            table.HasCheckConstraint("ck_outbox_payload", "payload IS NULL OR COALESCE((jsonb_typeof(payload) = 'object' AND payload ?& ARRAY['keyId', 'nonce', 'ciphertext', 'tag'] AND payload - ARRAY['keyId', 'nonce', 'ciphertext', 'tag'] = '{}'::jsonb AND jsonb_typeof(payload->'keyId') = 'string' AND jsonb_typeof(payload->'nonce') = 'string' AND jsonb_typeof(payload->'ciphertext') = 'string' AND jsonb_typeof(payload->'tag') = 'string' AND (payload->>'keyId') ~ '^[A-Za-z0-9_.-]{1,100}$' AND (payload->>'nonce') ~ '^[A-Za-z0-9+/]{16}$' AND (payload->>'tag') ~ '^[A-Za-z0-9+/]{22}==$' AND length(payload->>'ciphertext') BETWEEN 4 AND 87384 AND (payload->>'ciphertext') ~ '^[A-Za-z0-9+/]+={0,2}$'), false)");
        });
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.Type).HasMaxLength(80).IsRequired();
        builder.Property(message => message.Payload).HasColumnType("jsonb")
            .HasConversion(payload => JsonSerializer.Serialize(payload, JsonConfiguration.Options),
                json => JsonSerializer.Deserialize<EncryptedEmailPayload>(json, JsonConfiguration.Options));
        builder.Property(message => message.LastError).HasMaxLength(80);
        builder.Property<uint>("Version").IsRowVersion();
        builder.HasIndex(message => message.NextAttemptAt).HasFilter("processed_at IS NULL").HasDatabaseName("ix_outbox_pending");
    }
}
