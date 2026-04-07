using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClearExistingUserData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete all existing data in the correct order to respect foreign key constraints
            migrationBuilder.Sql("DELETE FROM user_roles;");
            migrationBuilder.Sql("DELETE FROM club_invites;");
            migrationBuilder.Sql("DELETE FROM clubs;");
            migrationBuilder.Sql("DELETE FROM user_profiles;");
            migrationBuilder.Sql("DELETE FROM \"AuditLogs\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cannot restore deleted data - this migration is irreversible
        }
    }
}
