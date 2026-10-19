using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminWriteLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sessions_revocation",
                table: "sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_type",
                table: "security_audit_events");

            migrationBuilder.AddColumn<short>(
                name: "status_before_disable",
                table: "users",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_previous_status",
                table: "users",
                sql: "status_before_disable IS NULL OR (status = 2 AND status_before_disable IN (0, 1, 4) AND ((status_before_disable = 1) = (email_verified_at IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sessions_revocation",
                table: "sessions",
                sql: "(revoked_at IS NULL) = (revoke_reason IS NULL) AND (revoke_reason IS NULL OR revoke_reason BETWEEN 0 AND 8)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_type",
                table: "security_audit_events",
                sql: "event_type IN ('register', 'verify_email', 'login_success', 'login_failed', 'refresh_rotated', 'refresh_replay_detected', 'logout', 'logout_all', 'password_changed', 'password_reset', 'account_deactivated', 'admin_login', 'admin_login_failed', 'admin_logout', 'admin_role_granted', 'admin_role_revoked', 'admin_invited_user', 'admin_resent_invitation', 'admin_requested_password_reset', 'admin_disabled_user', 'admin_enabled_user', 'admin_revoked_session', 'admin_revoked_all_sessions', 'invitation_accepted')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_previous_status",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sessions_revocation",
                table: "sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_event_type",
                table: "security_audit_events");

            migrationBuilder.DropColumn(
                name: "status_before_disable",
                table: "users");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sessions_revocation",
                table: "sessions",
                sql: "(revoked_at IS NULL) = (revoke_reason IS NULL) AND (revoke_reason IS NULL OR revoke_reason BETWEEN 0 AND 6)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_event_type",
                table: "security_audit_events",
                sql: "event_type IN ('register', 'verify_email', 'login_success', 'login_failed', 'refresh_rotated', 'refresh_replay_detected', 'logout', 'logout_all', 'password_changed', 'password_reset', 'account_deactivated', 'admin_login', 'admin_login_failed', 'admin_logout', 'admin_role_granted', 'admin_role_revoked', 'admin_invited_user', 'admin_resent_invitation', 'admin_requested_password_reset', 'admin_disabled_user', 'admin_enabled_user', 'admin_revoked_session')");
        }
    }
}
