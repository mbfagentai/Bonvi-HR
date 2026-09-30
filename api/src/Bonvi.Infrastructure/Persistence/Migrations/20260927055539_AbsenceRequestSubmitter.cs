using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Yo'qlik so'rovini XODIMNING O'ZI yuborgan/bekor qilganini yozish
    /// (SPEC-verifikatsiya-sorovlar §B2 «CreatedBy: … or the employee themselves»).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Faqat QO'SHADI</b> — ikkita nullable <c>uuid</c> ustun
    /// (<c>submitted_by_account_id</c>, <c>cancelled_by_account_id</c>), sukutsiz:
    /// mavjud qatorlar <c>NULL</c> bo'lib qoladi (= HR kiritgan/bekor qilgan) va
    /// qayta yozilmaydi — faqat katalog o'zgarishi, jadval qulflanmaydi.
    /// </para>
    /// <para>
    /// Tashqi kalit YO'Q — <c>created_by</c> / <c>decided_by</c> bilan bir xil
    /// qaror: bu audit ma'lumoti, bog'lanish emas, va ilova hisoblari hech qachon
    /// o'chirilmaydi (<c>EmployeeAccount</c> izohi).
    /// </para>
    /// <para>
    /// <c>Down</c> ikki ustunni tashlaydi: yo'qotiladigani faqat «kim yubordi»
    /// belgisi, so'rovning o'zi va HR qarori joyida qoladi.
    /// </para>
    /// </remarks>
    public partial class AbsenceRequestSubmitter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by_account_id",
                table: "absence_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "submitted_by_account_id",
                table: "absence_requests",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancelled_by_account_id",
                table: "absence_requests");

            migrationBuilder.DropColumn(
                name: "submitted_by_account_id",
                table: "absence_requests");
        }
    }
}
