using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Obyektlar ma'lumotnomasi — telefondan belgilashning geo-to'sig'i
    /// (SPEC-xodim-ilovasi §D1).
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>ORQAGA QAYTARISH — MA'LUMOT YO'QOLADI, ZAXIRA SHART.</b>
    /// <c>Down</c> HR kiritgan barcha obyektlarni o'chiradi. Ustiga, u
    /// <c>mobile_check_ins</c> dagi tashqi kalit sababli o'sha migratsiya
    /// qaytarilmaguncha umuman bajarilmaydi.
    /// </remarks>
    public partial class AddWorkSites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_sites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    radius_meters = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_work_sites", x => x.id);
                    table.CheckConstraint("ck_work_sites_coordinates", "latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180");
                    table.CheckConstraint("ck_work_sites_radius", "radius_meters BETWEEN 50 AND 5000");
                });

            migrationBuilder.CreateIndex(
                name: "ix_work_sites_is_active_name",
                table: "work_sites",
                columns: new[] { "is_active", "name" });

            // Nom bo'yicha unikal — qolgan to'qqizta ma'lumotnomadagi bilan
            // AYNAN bir xil (`CatalogTables`, `AddAbsenceRequests`):
            // `lower(name)` ustida va yumshoq o'chirilganlarni hisobga
            // olmagan holda. EF fluent API ifoda ustidagi indeksni
            // ifodalay olmaydi — shu sababli xom SQL.
            //
            // Registrga sezgir emas, chunki «Qo'qon ombori» va «qo'qon
            // ombori» — bitta joy. Qisman filtr esa o'chirilgan obyektning
            // nomini qayta ishlatishga imkon beradi.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_work_sites_name
                    ON work_sites (lower(name))
                    WHERE deleted_at IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_work_sites_name;");

            migrationBuilder.DropTable(
                name: "work_sites");
        }
    }
}
