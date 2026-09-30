using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAbsenceRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "absence_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false, collation: "uz-Cyrl-x-icu"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_absence_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "absence_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    absence_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_date = table.Column<DateOnly>(type: "date", nullable: false),
                    from_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    to_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    decision_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_absence_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_absence_requests_absence_types_absence_type_id",
                        column: x => x.absence_type_id,
                        principalTable: "absence_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_absence_requests_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_absence_requests_absence_type_id",
                table: "absence_requests",
                column: "absence_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_absence_requests_employee_id_from_date_to_date",
                table: "absence_requests",
                columns: new[] { "employee_id", "from_date", "to_date" });

            migrationBuilder.CreateIndex(
                name: "ix_absence_requests_status",
                table: "absence_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_absence_types_is_active_name",
                table: "absence_types",
                columns: new[] { "is_active", "name" });

            // Nom bo'yicha unikal — boshqa ma'lumotnomalardagi bilan AYNAN bir
            // xil (`CatalogTables` migratsiyasi): `lower(name)`, yumshoq
            // o'chirilganlar chetda. EF buni o'zi yasay olmaydi, shu sababli
            // xom SQL.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_absence_types_name_lower
                    ON absence_types (lower(name))
                    WHERE deleted_at IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_absence_types_name_lower;");

            migrationBuilder.DropTable(
                name: "absence_requests");

            migrationBuilder.DropTable(
                name: "absence_types");
        }
    }
}
