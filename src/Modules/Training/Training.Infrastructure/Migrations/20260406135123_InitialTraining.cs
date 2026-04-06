using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Training.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialTraining : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "coach_gymnast_relationships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coach_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gymnast_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coach_gymnast_relationships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "programme_builder_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coach_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gymnast_id = table.Column<Guid>(type: "uuid", nullable: false),
                    initial_goals = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    conversation_history_json = table.Column<string>(type: "jsonb", nullable: true),
                    resulting_programme_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    rag_scope_config = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programme_builder_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "programme_metadata",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gymnast_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coach_id = table.Column<Guid>(type: "uuid", nullable: false),
                    couchdb_doc_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    couchdb_rev = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    embedding_vector = table.Column<Vector>(type: "vector", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programme_metadata", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_coach_gymnast_relationships_coach_id",
                table: "coach_gymnast_relationships",
                column: "coach_id");

            migrationBuilder.CreateIndex(
                name: "ix_coach_gymnast_relationships_gymnast_id",
                table: "coach_gymnast_relationships",
                column: "gymnast_id");

            migrationBuilder.CreateIndex(
                name: "ix_coach_gymnast_relationships_tenant_id",
                table: "coach_gymnast_relationships",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_coach_gymnast_relationships_unique",
                table: "coach_gymnast_relationships",
                columns: new[] { "tenant_id", "coach_id", "gymnast_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_programme_builder_sessions_coach_id",
                table: "programme_builder_sessions",
                column: "coach_id");

            migrationBuilder.CreateIndex(
                name: "ix_programme_builder_sessions_gymnast_id",
                table: "programme_builder_sessions",
                column: "gymnast_id");

            migrationBuilder.CreateIndex(
                name: "ix_programme_builder_sessions_resulting_programme_id",
                table: "programme_builder_sessions",
                column: "resulting_programme_id");

            migrationBuilder.CreateIndex(
                name: "ix_programme_builder_sessions_status",
                table: "programme_builder_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_programme_builder_sessions_tenant_id",
                table: "programme_builder_sessions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_programme_metadata_active_per_gymnast",
                table: "programme_metadata",
                columns: new[] { "tenant_id", "gymnast_id" },
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ix_programme_metadata_coach_id",
                table: "programme_metadata",
                column: "coach_id");

            migrationBuilder.CreateIndex(
                name: "ix_programme_metadata_couchdb_doc_id",
                table: "programme_metadata",
                column: "couchdb_doc_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_programme_metadata_gymnast_id",
                table: "programme_metadata",
                column: "gymnast_id");

            migrationBuilder.CreateIndex(
                name: "ix_programme_metadata_tenant_gymnast_status",
                table: "programme_metadata",
                columns: new[] { "tenant_id", "gymnast_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_programme_metadata_tenant_id",
                table: "programme_metadata",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coach_gymnast_relationships");

            migrationBuilder.DropTable(
                name: "programme_builder_sessions");

            migrationBuilder.DropTable(
                name: "programme_metadata");
        }
    }
}
