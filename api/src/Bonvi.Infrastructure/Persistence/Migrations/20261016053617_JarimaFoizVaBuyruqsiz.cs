using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Jarima: summa o'rniga foiz bo'lishi mumkin, buyruq chiqarilmaydi (egasi, 2026-10-16).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Jarimadagi buyruq ma'lumoti tozalanadi</b> — <c>ck_incentives_penalty_no_order</c>
    /// cheklovi qo'yilishidan OLDIN, aks holda u mavjud qator bilan yiqiladi. Prod'da
    /// 2026-10-16 holatida jarima yozuvi umuman yo'q edi (faqat 24 rag'bat, ularda ham
    /// buyruq bo'sh), ya'ni u yerda bu qadam hech narsaga tegmaydi.
    /// </para>
    /// <para>
    /// ⚠️ <c>Down</c> tozalangan buyruq ma'lumotini QAYTARMAYDI va <c>percent</c>
    /// ustunini tashlaydi — foiz bilan berilgan jarimalarning miqdori yo'qoladi.
    /// Qaytarish kerak bo'lsa — zaxiradan.
    /// </para>
    /// </remarks>
    public partial class JarimaFoizVaBuyruqsiz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "percent",
                table: "incentives",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE incentives SET order_no = NULL, order_date = NULL " +
                "WHERE kind = 2 AND (order_no IS NOT NULL OR order_date IS NOT NULL);");

            migrationBuilder.AddCheckConstraint(
                name: "ck_incentives_amount_or_percent",
                table: "incentives",
                sql: "amount IS NULL OR percent IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_incentives_penalty_no_order",
                table: "incentives",
                sql: "kind <> 2 OR (order_no IS NULL AND order_date IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_incentives_percent_penalty_only",
                table: "incentives",
                sql: "percent IS NULL OR kind = 2");

            migrationBuilder.AddCheckConstraint(
                name: "ck_incentives_percent_range",
                table: "incentives",
                sql: "percent IS NULL OR (percent > 0 AND percent <= 100)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_incentives_amount_or_percent",
                table: "incentives");

            migrationBuilder.DropCheckConstraint(
                name: "ck_incentives_penalty_no_order",
                table: "incentives");

            migrationBuilder.DropCheckConstraint(
                name: "ck_incentives_percent_penalty_only",
                table: "incentives");

            migrationBuilder.DropCheckConstraint(
                name: "ck_incentives_percent_range",
                table: "incentives");

            migrationBuilder.DropColumn(
                name: "percent",
                table: "incentives");
        }
    }
}
