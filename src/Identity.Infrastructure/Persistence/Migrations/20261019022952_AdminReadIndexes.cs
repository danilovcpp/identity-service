using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminReadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_occurred_at",
                table: "security_audit_events");

            migrationBuilder.DropIndex(
                name: "IX_security_audit_events_user_id",
                table: "security_audit_events");

            migrationBuilder.CreateIndex(
                name: "ix_users_created_id",
                table: "users",
                columns: new[] { "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_users_email_id",
                table: "users",
                columns: new[] { "email", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_users_status_id",
                table: "users",
                columns: new[] { "status", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_sessions_user_created_id",
                table: "sessions",
                columns: new[] { "user_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_time_id",
                table: "security_audit_events",
                columns: new[] { "event_type", "occurred_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_time_id",
                table: "security_audit_events",
                columns: new[] { "occurred_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_user_time_id",
                table: "security_audit_events",
                columns: new[] { "user_id", "occurred_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_created_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_email_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_status_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_sessions_user_created_id",
                table: "sessions");

            migrationBuilder.DropIndex(
                name: "ix_audit_event_time_id",
                table: "security_audit_events");

            migrationBuilder.DropIndex(
                name: "ix_audit_time_id",
                table: "security_audit_events");

            migrationBuilder.DropIndex(
                name: "ix_audit_user_time_id",
                table: "security_audit_events");

            migrationBuilder.CreateIndex(
                name: "ix_audit_occurred_at",
                table: "security_audit_events",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_events_user_id",
                table: "security_audit_events",
                column: "user_id");
        }
    }
}
