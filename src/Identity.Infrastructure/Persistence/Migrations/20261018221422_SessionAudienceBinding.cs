using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SessionAudienceBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_refresh_tokens_refresh_tokens_replaced_by_token_id_session_~",
                table: "refresh_tokens");

            migrationBuilder.AddColumn<string>(
                name: "audience",
                table: "sessions",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_sessions_expiry",
                table: "sessions",
                column: "absolute_expires_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sessions_audience",
                table: "sessions",
                sql: "audience IS NULL OR (length(btrim(audience)) > 0 AND audience !~ '[[:cntrl:]]')");

            migrationBuilder.AddForeignKey(
                name: "FK_refresh_tokens_refresh_tokens_replaced_by_token_id_session_~",
                table: "refresh_tokens",
                columns: new[] { "replaced_by_token_id", "session_id" },
                principalTable: "refresh_tokens",
                principalColumns: new[] { "id", "session_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_refresh_tokens_refresh_tokens_replaced_by_token_id_session_~",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_sessions_expiry",
                table: "sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sessions_audience",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "audience",
                table: "sessions");

            migrationBuilder.AddForeignKey(
                name: "FK_refresh_tokens_refresh_tokens_replaced_by_token_id_session_~",
                table: "refresh_tokens",
                columns: new[] { "replaced_by_token_id", "session_id" },
                principalTable: "refresh_tokens",
                principalColumns: new[] { "id", "session_id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
