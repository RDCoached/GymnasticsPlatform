using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Training.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillsCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    effectiveness_rating = table.Column<int>(type: "integer", nullable: false),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    usage_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    embedding_vector = table.Column<Vector>(type: "vector(384)", nullable: true),
                    created_by_tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "skill_sections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_sections", x => x.id);
                    table.ForeignKey(
                        name: "FK_skill_sections_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_skill_sections_section",
                table: "skill_sections",
                column: "section");

            migrationBuilder.CreateIndex(
                name: "ix_skill_sections_skill_id",
                table: "skill_sections",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_skill_sections_skill_section_unique",
                table: "skill_sections",
                columns: new[] { "skill_id", "section" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skills_created_by_tenant_id",
                table: "skills",
                column: "created_by_tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_skills_effectiveness_rating",
                table: "skills",
                column: "effectiveness_rating");

            migrationBuilder.CreateIndex(
                name: "ix_skills_title",
                table: "skills",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "ix_skills_usage_count",
                table: "skills",
                column: "usage_count");

            // Create HNSW index for vector similarity search
            migrationBuilder.Sql(@"
                CREATE INDEX ix_skills_embedding_vector_hnsw
                ON skills
                USING hnsw (embedding_vector vector_cosine_ops)
                WITH (m = 16, ef_construction = 64);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_skills_embedding_vector_hnsw;");

            migrationBuilder.DropTable(
                name: "skill_sections");

            migrationBuilder.DropTable(
                name: "skills");
        }
    }
}
