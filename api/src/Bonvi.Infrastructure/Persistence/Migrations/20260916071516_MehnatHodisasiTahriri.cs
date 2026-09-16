using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Mehnat yo'li hodisasiga «tuzatilgan vaqt» ustunini qo'shadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nima uchun kerak.</b> Hodisaning terish xatosini tuzatish
    /// yo'li ochildi (<c>PUT /employments/{id}/events/{eventId}</c>):
    /// sana, buyruq raqami, izoh va oklad summasi. Hodisaning O'ZI
    /// avvalgidek o'zgarmas fakt bo'lib qoladi — turi, lavozimi va
    /// bo'limi tegilmaydi. Tuzatilgani esa lentada ko'rinib turishi
    /// kerak, aks holda HR yozuv boshqa qiymat bilan kiritilganini
    /// bilmasdi.
    /// </para>
    /// <para>
    /// <b>Mavjud qatorlar NULL bo'lib qoladi va bu to'g'ri</b> — ular
    /// tuzatilmagan. <c>recorded_at</c> ni ko'chirish yaramasdi: o'shanda
    /// bazadagi har bir eski yozuv «tahrirlangan» bo'lib ko'rinardi
    /// (<c>BosqichYozuviTahriri</c> dagi bilan bir xil qaror).
    /// </para>
    /// </remarks>
    public partial class MehnatHodisasiTahriri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "employment_events",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "employment_events");
        }
    }
}
