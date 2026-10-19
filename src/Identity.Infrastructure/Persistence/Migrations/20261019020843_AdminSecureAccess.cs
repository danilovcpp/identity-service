using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminSecureAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_status",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_verification",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_type",
                table: "security_audit_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_one_time_purpose",
                table: "one_time_tokens");

            migrationBuilder.DropCheckConstraint(
                name: "ck_one_time_times",
                table: "one_time_tokens");

            migrationBuilder.AddColumn<string>(
                name: "disable_reason",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "disabled_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "disabled_by",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "actor_user_id",
                table: "security_audit_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "target_user_id",
                table: "security_audit_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "admin_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_sessions", x => x.id);
                    table.CheckConstraint("ck_admin_sessions_times", "last_seen_at >= created_at AND last_seen_at < expires_at AND expires_at > created_at AND expires_at <= created_at + interval '8 hours' AND (revoked_at IS NULL OR revoked_at >= created_at)");
                    table.ForeignKey(
                        name: "FK_admin_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "identity_administrators",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<short>(type: "smallint", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_administrators", x => x.user_id);
                    table.CheckConstraint("ck_identity_administrators_role", "role IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_identity_administrators_users_granted_by",
                        column: x => x.granted_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_identity_administrators_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_disabled_by",
                table: "users",
                column: "disabled_by");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_disable_metadata",
                table: "users",
                sql: "(disabled_at IS NULL OR disabled_at BETWEEN created_at AND updated_at) AND (disable_reason IS NULL OR (length(btrim(disable_reason)) > 0 AND disable_reason !~ '[[:cntrl:]]'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_status",
                table: "users",
                sql: "status IN (0, 1, 2, 3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_verification",
                table: "users",
                sql: "(status IN (0, 4) AND email_verified_at IS NULL) OR (status = 1 AND email_verified_at IS NOT NULL) OR status IN (2, 3)");

            migrationBuilder.CreateIndex(
                name: "ix_audit_actor_time",
                table: "security_audit_events",
                columns: new[] { "actor_user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_target_time",
                table: "security_audit_events",
                columns: new[] { "target_user_id", "occurred_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_type",
                table: "security_audit_events",
                sql: "event_type IN ('register', 'verify_email', 'login_success', 'login_failed', 'refresh_rotated', 'refresh_replay_detected', 'logout', 'logout_all', 'password_changed', 'password_reset', 'account_deactivated', 'admin_login', 'admin_login_failed', 'admin_logout', 'admin_role_granted', 'admin_role_revoked', 'admin_invited_user', 'admin_resent_invitation', 'admin_requested_password_reset', 'admin_disabled_user', 'admin_enabled_user', 'admin_revoked_session')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_one_time_purpose",
                table: "one_time_tokens",
                sql: "purpose IN (0, 1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_one_time_times",
                table: "one_time_tokens",
                sql: "expires_at > created_at AND expires_at <= created_at + CASE purpose WHEN 0 THEN interval '24 hours' WHEN 2 THEN interval '72 hours' ELSE interval '30 minutes' END AND (consumed_at IS NULL OR (consumed_at >= created_at AND consumed_at < expires_at))");

            migrationBuilder.CreateIndex(
                name: "ix_admin_sessions_expiry",
                table: "admin_sessions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_admin_sessions_user_revocation",
                table: "admin_sessions",
                columns: new[] { "user_id", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "IX_identity_administrators_granted_by",
                table: "identity_administrators",
                column: "granted_by");

            migrationBuilder.CreateIndex(
                name: "ix_identity_administrators_role",
                table: "identity_administrators",
                column: "role");

            migrationBuilder.AddForeignKey(
                name: "FK_security_audit_events_users_actor_user_id",
                table: "security_audit_events",
                column: "actor_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_security_audit_events_users_target_user_id",
                table: "security_audit_events",
                column: "target_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_users_disabled_by",
                table: "users",
                column: "disabled_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_security_audit_events_users_actor_user_id",
                table: "security_audit_events");

            migrationBuilder.DropForeignKey(
                name: "FK_security_audit_events_users_target_user_id",
                table: "security_audit_events");

            migrationBuilder.DropForeignKey(
                name: "FK_users_users_disabled_by",
                table: "users");

            migrationBuilder.DropTable(
                name: "admin_sessions");

            migrationBuilder.DropTable(
                name: "identity_administrators");

            migrationBuilder.DropIndex(
                name: "IX_users_disabled_by",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_disable_metadata",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_status",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_verification",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_audit_actor_time",
                table: "security_audit_events");

            migrationBuilder.DropIndex(
                name: "ix_audit_target_time",
                table: "security_audit_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_type",
                table: "security_audit_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_one_time_purpose",
                table: "one_time_tokens");

            migrationBuilder.DropCheckConstraint(
                name: "ck_one_time_times",
                table: "one_time_tokens");

            migrationBuilder.DropColumn(
                name: "disable_reason",
                table: "users");

            migrationBuilder.DropColumn(
                name: "disabled_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "disabled_by",
                table: "users");

            migrationBuilder.DropColumn(
                name: "actor_user_id",
                table: "security_audit_events");

            migrationBuilder.DropColumn(
                name: "target_user_id",
                table: "security_audit_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_status",
                table: "users",
                sql: "status IN (0, 1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_verification",
                table: "users",
                sql: "(status = 0 AND email_verified_at IS NULL) OR (status IN (1, 2) AND email_verified_at IS NOT NULL) OR status = 3");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_type",
                table: "security_audit_events",
                sql: "event_type IN ('register', 'verify_email', 'login_success', 'login_failed', 'refresh_rotated', 'refresh_replay_detected', 'logout', 'logout_all', 'password_changed', 'password_reset', 'account_deactivated')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_one_time_purpose",
                table: "one_time_tokens",
                sql: "purpose IN (0, 1)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_one_time_times",
                table: "one_time_tokens",
                sql: "expires_at > created_at AND expires_at <= created_at + CASE purpose WHEN 0 THEN interval '24 hours' ELSE interval '30 minutes' END AND (consumed_at IS NULL OR (consumed_at >= created_at AND consumed_at < expires_at))");
        }
    }
}
