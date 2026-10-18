using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                    table.CheckConstraint("ck_outbox_error", "last_error IS NULL OR last_error ~ '^[a-z0-9_]{1,80}$'");
                    table.CheckConstraint("ck_outbox_payload", "payload IS NULL OR COALESCE((jsonb_typeof(payload) = 'object' AND payload ?& ARRAY['keyId', 'nonce', 'ciphertext', 'tag'] AND payload - ARRAY['keyId', 'nonce', 'ciphertext', 'tag'] = '{}'::jsonb AND jsonb_typeof(payload->'keyId') = 'string' AND jsonb_typeof(payload->'nonce') = 'string' AND jsonb_typeof(payload->'ciphertext') = 'string' AND jsonb_typeof(payload->'tag') = 'string' AND (payload->>'keyId') ~ '^[A-Za-z0-9_.-]{1,100}$' AND (payload->>'nonce') ~ '^[A-Za-z0-9+/]{16}$' AND (payload->>'tag') ~ '^[A-Za-z0-9+/]{22}==$' AND length(payload->>'ciphertext') BETWEEN 4 AND 87384 AND (payload->>'ciphertext') ~ '^[A-Za-z0-9+/]+={0,2}$'), false)");
                    table.CheckConstraint("ck_outbox_state", "attempt_count >= 0 AND next_attempt_at >= created_at AND (processed_at IS NULL) = (payload IS NOT NULL) AND (processed_at IS NULL OR processed_at >= created_at)");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    email_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_users_deactivation", "(status = 3) = (deactivated_at IS NOT NULL)");
                    table.CheckConstraint("ck_users_email", "length(btrim(email)) > 0 AND email !~ '[[:cntrl:]]' AND length(btrim(normalized_email)) > 0 AND normalized_email !~ '[[:cntrl:]]'");
                    table.CheckConstraint("ck_users_login_failures", "failed_login_count >= 0 AND (locked_until IS NULL OR locked_until > created_at)");
                    table.CheckConstraint("ck_users_status", "status IN (0, 1, 2, 3)");
                    table.CheckConstraint("ck_users_times", "updated_at >= created_at AND (email_verified_at IS NULL OR email_verified_at BETWEEN created_at AND updated_at) AND (deactivated_at IS NULL OR deactivated_at BETWEEN created_at AND updated_at)");
                    table.CheckConstraint("ck_users_verification", "(status = 0 AND email_verified_at IS NULL) OR (status IN (1, 2) AND email_verified_at IS NOT NULL) OR status = 3");
                });

            migrationBuilder.CreateTable(
                name: "credentials",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    password_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credentials", x => x.user_id);
                    table.CheckConstraint("ck_credentials_hash", "length(password_hash) BETWEEN 1 AND 4096");
                    table.CheckConstraint("ck_credentials_times", "password_changed_at >= created_at");
                    table.ForeignKey(
                        name: "FK_credentials_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "one_time_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<short>(type: "smallint", nullable: false),
                    token_hash = table.Column<string>(type: "character(64)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_one_time_tokens", x => x.id);
                    table.CheckConstraint("ck_one_time_hash", "token_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_one_time_purpose", "purpose IN (0, 1)");
                    table.CheckConstraint("ck_one_time_times", "expires_at > created_at AND expires_at <= created_at + CASE purpose WHEN 0 THEN interval '24 hours' ELSE interval '30 minutes' END AND (consumed_at IS NULL OR (consumed_at >= created_at AND consumed_at < expires_at))");
                    table.ForeignKey(
                        name: "FK_one_time_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "security_audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_audit_events", x => x.id);
                    table.CheckConstraint("ck_audit_event_type", "event_type IN ('register', 'verify_email', 'login_success', 'login_failed', 'refresh_rotated', 'refresh_replay_detected', 'logout', 'logout_all', 'password_changed', 'password_reset', 'account_deactivated')");
                    table.CheckConstraint("ck_audit_metadata", "jsonb_typeof(metadata) = 'object' AND metadata - 'attemptCount' = '{}'::jsonb AND CASE WHEN NOT (metadata ? 'attemptCount') THEN true WHEN jsonb_typeof(metadata->'attemptCount') = 'number' THEN (metadata->>'attemptCount')::numeric BETWEEN 0 AND 2147483647 AND mod((metadata->>'attemptCount')::numeric, 1) = 0 ELSE false END");
                    table.ForeignKey(
                        name: "FK_security_audit_events_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    absolute_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoke_reason = table.Column<short>(type: "smallint", nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                    table.CheckConstraint("ck_sessions_client", "length(btrim(client_id)) > 0");
                    table.CheckConstraint("ck_sessions_revocation", "(revoked_at IS NULL) = (revoke_reason IS NULL) AND (revoke_reason IS NULL OR revoke_reason BETWEEN 0 AND 6)");
                    table.CheckConstraint("ck_sessions_times", "absolute_expires_at > created_at AND absolute_expires_at <= created_at + interval '720 hours' AND last_used_at >= created_at AND last_used_at < absolute_expires_at AND (revoked_at IS NULL OR revoked_at >= last_used_at)");
                    table.ForeignKey(
                        name: "FK_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character(64)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.UniqueConstraint("ak_refresh_tokens_id_session", x => new { x.id, x.session_id });
                    table.CheckConstraint("ck_refresh_hash", "token_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_refresh_replacement", "(consumed_at IS NULL) = (replaced_by_token_id IS NULL) AND (replaced_by_token_id IS NULL OR replaced_by_token_id <> id)");
                    table.CheckConstraint("ck_refresh_times", "expires_at > created_at AND expires_at <= created_at + interval '168 hours' AND (consumed_at IS NULL OR (consumed_at >= created_at AND consumed_at < expires_at)) AND (revoked_at IS NULL OR revoked_at >= COALESCE(consumed_at, created_at))");
                    table.ForeignKey(
                        name: "FK_refresh_tokens_refresh_tokens_replaced_by_token_id_session_~",
                        columns: x => new { x.replaced_by_token_id, x.session_id },
                        principalTable: "refresh_tokens",
                        principalColumns: new[] { "id", "session_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_one_time_tokens_expiry",
                table: "one_time_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_one_time_tokens_user_id",
                table: "one_time_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_one_time_tokens_hash",
                table: "one_time_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending",
                table: "outbox_messages",
                column: "next_attempt_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_expiry",
                table: "refresh_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_replaced_by_token_id_session_id",
                table: "refresh_tokens",
                columns: new[] { "replaced_by_token_id", "session_id" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_session",
                table: "refresh_tokens",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ux_refresh_tokens_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_occurred_at",
                table: "security_audit_events",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_events_user_id",
                table: "security_audit_events",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_user_revocation",
                table: "sessions",
                columns: new[] { "user_id", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "ux_users_normalized_email",
                table: "users",
                column: "normalized_email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credentials");

            migrationBuilder.DropTable(
                name: "one_time_tokens");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "security_audit_events");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
