using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeProviderUserIdGloballyUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_profiles_tenant_keycloak_user",
                table: "user_profiles");

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_provider_user_id",
                table: "user_profiles",
                column: "keycloak_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_tenant_keycloak_user",
                table: "user_profiles",
                columns: new[] { "tenant_id", "keycloak_user_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_profiles_provider_user_id",
                table: "user_profiles");

            migrationBuilder.DropIndex(
                name: "ix_user_profiles_tenant_keycloak_user",
                table: "user_profiles");

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_tenant_keycloak_user",
                table: "user_profiles",
                columns: new[] { "tenant_id", "keycloak_user_id" },
                unique: true);
        }
    }
}
