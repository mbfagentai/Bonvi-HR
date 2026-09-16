using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bosqich yozuviga «tuzatilgan vaqt» ustunini qo'shadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nima uchun kerak.</b> Endi tarixdagi qatorni ataylab tuzatish
    /// mumkin (<c>PUT /candidates/{id}/stages/{entryId}</c>), lekin
    /// <c>recorded_at</c> bunda O'ZGARMAYDI — lenta aynan shu bo'yicha
    /// saralanadi va tuzatilgan qator tarixning o'rtasidan tepasiga
    /// sakrab chiqmasligi kerak. Tuzatish faktining o'zi esa yo'qolmasin:
    /// u shu ustunda qoladi va interfeysda «tahrirlandi» belgisi bo'lib
    /// ko'rinadi.
    /// </para>
    /// <para>
    /// <b>Mavjud qatorlar NULL bo'lib qoladi va bu to'g'ri.</b> Ular
    /// tahrirlanmagan — hech bo'lmaganda bu mexanizm orqali. Ustunga
    /// <c>recorded_at</c> ni ko'chirish YARAMASDI: o'shanda tizimdagi
    /// har bir eski yozuv «tahrirlangan» bo'lib ko'rinardi.
    /// </para>
    /// </remarks>
    public partial class BosqichYozuviTahriri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "candidate_stage_entries",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "candidate_stage_entries");
        }
    }
}
