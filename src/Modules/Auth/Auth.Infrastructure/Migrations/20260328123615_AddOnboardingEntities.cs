using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "onboarding_choice",
                table: "user_profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "onboarding_completed",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "clubs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clubs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "club_invites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    max_uses = table.Column<int>(type: "integer", nullable: false),
                    times_used = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_invites", x => x.id);
                    table.ForeignKey(
                        name: "FK_club_invites_clubs_club_id",
                        column: x => x.club_id,
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_club_invites_club_id",
                table: "club_invites",
                column: "club_id");

            migrationBuilder.CreateIndex(
                name: "ix_club_invites_code",
                table: "club_invites",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_club_invites_expires_at",
                table: "club_invites",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_clubs_owner_user_id",
                table: "clubs",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_clubs_tenant_id",
                table: "clubs",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "club_invites");

            migrationBuilder.DropTable(
                name: "clubs");

            migrationBuilder.DropColumn(
                name: "onboarding_choice",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "onboarding_completed",
                table: "user_profiles");
        }
    }
}
