using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameKeycloakUserIdToProviderUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "keycloak_user_id",
                table: "user_profiles",
                newName: "provider_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_user_profiles_tenant_keycloak_user",
                table: "user_profiles",
                newName: "ix_user_profiles_tenant_provider_user");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "provider_user_id",
                table: "user_profiles",
                newName: "keycloak_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_user_profiles_tenant_provider_user",
                table: "user_profiles",
                newName: "ix_user_profiles_tenant_keycloak_user");
        }
    }
}
