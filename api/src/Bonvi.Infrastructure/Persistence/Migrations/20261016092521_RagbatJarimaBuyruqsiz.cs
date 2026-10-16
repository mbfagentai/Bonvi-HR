using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Rag'bat va jarima buyruqsiz (egasi, 2026-10-16): <c>order_no</c> / <c>order_date</c> ustunlari
    /// va ularga bog'liq <c>ck_incentives_penalty_no_order</c> cheklovi olib tashlanadi.
    /// </summary>
    /// <remarks>
    /// ⚠️ Ustundagi ma'lumot yo'qoladi — egasining talabi shu («bazadagi ma'lumot ham kerak emas»).
    /// 2026-10-16 holatida prod'da 24 rag'batning birortasida ham buyruq to'ldirilmagan edi; deploy
    /// oldidan yana tekshiriladi. <c>Down</c> ustunlarni BO'SH qaytaradi.
    /// </remarks>
    public partial class RagbatJarimaBuyruqsiz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_incentives_penalty_no_order",
                table: "incentives");

            migrationBuilder.DropColumn(
                name: "order_date",
                table: "incentives");

            migrationBuilder.DropColumn(
                name: "order_no",
                table: "incentives");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "order_date",
                table: "incentives",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "order_no",
                table: "incentives",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_incentives_penalty_no_order",
                table: "incentives",
                sql: "kind <> 2 OR (order_no IS NULL AND order_date IS NULL)");
        }
    }
}
