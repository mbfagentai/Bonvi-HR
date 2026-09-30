using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// «Telefondan belgilash» ruxsati — <c>employees</c> jadvalidagi bitta
    /// ustun (SPEC-xodim-ilovasi §C, ma'lumot modeli §0.1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Bu to'lqindagi YAGONA migratsiya mavjud jadvalga tegadi</b> —
    /// shu sababli u ataylab bir qatorlik va alohida turadi: bir qarashda
    /// tekshirib bo'ladi va mustaqil qaytariladi.
    /// </para>
    /// <para>
    /// ⚠️ <b>ORQAGA QAYTARISH — MA'LUMOT YO'QOLADI, ZAXIRA SHART.</b>
    /// <c>Down</c> HR ning har bir odam uchun qo'lda qo'ygan qaroridan
    /// iz qoldirmaydi va qayta ko'tarilganda hamma JIMGINA <c>false</c>
    /// ga qaytadi.
    /// </para>
    /// </remarks>
    public partial class AddEmployeeMobileFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            /*
              QULF NAVBATI — bu yerdagi YAGONA haqiqiy xavf.

              Ustun qo'shish PostgreSQL 11+ da metama'lumot amali: 950 ta
              qator QAYTA YOZILMAYDI va amal millisekundlarda tugaydi.
              Lekin u `ACCESS EXCLUSIVE` qulfini so'raydi, ya'ni uzoq
              ishlayotgan hisobot `employees` ni ushlab tursa, migratsiya
              kutib qoladi — va uning ORQASIDA butun navbat to'planadi.

              `lock_timeout` bilan bu holat 3 soniyada TUSHUNARLI xato
              bo'lib tugaydi: joriy qilishni qaytadan boshlash mumkin.
              Usiz esa terminallar noma'lum muddatga jim qolardi.
            */
            migrationBuilder.Sql("SET lock_timeout = '3s';");

            migrationBuilder.AddColumn<bool>(
                name: "mobile_check_in_allowed",
                table: "employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            /*
              ⚠️ EF qo'ygan USTUN SUKUTINI darhol olib tashlaymiz.

              `defaultValue: false` yuqorida ATAYLAB qoldirilgan: aynan u
              tufayli mavjud 950 ta qator qayta yozilmaydi (PostgreSQL
              sukutni katalogda saqlaydi). Lekin bazada QOLIB KETGAN sukut
              loyihaning qoidasini buzardi — `AttendanceConfigurations`
              boshida yozilgan: «ustun sukut qiymatlari qo'yilmaydi, qator
              har doim domen bergan qiymat bilan yoziladi». Model
              suratida ham u yo'q, ya'ni keyingi `migrations add` uni
              o'chirish uchun migratsiya yasab, chalkashlik tug'dirardi.
            */
            migrationBuilder.Sql(
                "ALTER TABLE employees ALTER COLUMN mobile_check_in_allowed DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET lock_timeout = '3s';");

            migrationBuilder.DropColumn(
                name: "mobile_check_in_allowed",
                table: "employees");
        }
    }
}
