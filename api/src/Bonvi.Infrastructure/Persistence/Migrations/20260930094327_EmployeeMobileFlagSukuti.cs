using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// <c>employees.mobile_check_in_allowed</c> ga BAZA darajasida <c>DEFAULT false</c> —
    /// faqat image rollback xavfsizligi uchun (verification → main birlashtirilishi, 2026-09-30).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega.</b> <c>AddEmployeeMobileFlag</c> ustunni NOT NULL qilib, sukutini ataylab
    /// olib tashlagan. Verifikatsiyadan OLDINGI image esa bu ustunni bilmaydi: rollback'dan
    /// keyin u xodim qo'shganda (karta, nomzodni ishga olish) ustunni yubormaydi va INSERT
    /// 23502 bilan yiqiladi — HR ekranda 500 ko'radi. 2026-09-30 da lokal repetitsiyada
    /// aynan shunday bo'ldi. Sukut bilan eski image qo'shimcha qadamsiz ishlaydi.
    /// </para>
    /// <para>
    /// Model va suratga TEGILMAYDI: joriy kod ustunni har doim domen qiymati bilan yozadi
    /// (<c>AttendanceConfigurations</c> dagi qoida o'z kuchida), sukut faqat ustunni umuman
    /// bilmaydigan eski kod uchun.
    /// </para>
    /// </remarks>
    public partial class EmployeeMobileFlagSukuti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Faqat katalogdagi metama'lumot — qatorlar qayta yozilmaydi.
            migrationBuilder.Sql("SET lock_timeout = '3s';");
            migrationBuilder.Sql("ALTER TABLE employees ALTER COLUMN mobile_check_in_allowed SET DEFAULT false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE employees ALTER COLUMN mobile_check_in_allowed DROP DEFAULT;");
        }
    }
}
