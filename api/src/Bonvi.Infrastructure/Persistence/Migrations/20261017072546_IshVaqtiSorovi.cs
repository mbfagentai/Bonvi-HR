using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// «Ish vaqti hisoblanadigan» yo'qlik turi (SPEC-so'rovlar §B3 AMENDED 2026-10-17):
    /// <c>absence_types.counts_as_work_time</c> va <c>attendance_days.credited_minutes</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ikkala ustun ham DOIMIY sukut bilan — PostgreSQL 11+ da bu faqat katalog
    /// yozuvi, jadval qayta yozilmaydi (<c>attendance_days</c> katta). Sukut bazada
    /// QOLADI: eski image rollback'dan keyin ustunni bilmay qator qo'sha oladi
    /// (<c>EmployeeMobileFlagSukuti</c> dagi saboq). Modelda sukut yo'q — loyiha qoidasi.
    /// </para>
    /// <para>
    /// Mavjud qatorlar: barcha turlar <c>false</c> (avvalgi xatti-harakat), barcha
    /// kunlar <c>0</c> (so'rovsiz hisoblangan) — ma'lumot o'zgarmaydi.
    /// </para>
    /// </remarks>
    public partial class IshVaqtiSorovi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET lock_timeout = '3s';");

            migrationBuilder.AddColumn<int>(
                name: "credited_minutes",
                table: "attendance_days",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "counts_as_work_time",
                table: "absence_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "credited_minutes",
                table: "attendance_days");

            migrationBuilder.DropColumn(
                name: "counts_as_work_time",
                table: "absence_types");
        }
    }
}
