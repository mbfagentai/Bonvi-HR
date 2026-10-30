using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Nomzodning qayta murojaati (<c>Candidate.ReappliedAt</c>, egasi 2026-10-30): mavjud kartaga
    /// onlayn anketa qo'shilgan oxirgi lahza. Faqat qo'shadi (nullable ustun).
    /// </summary>
    /// <remarks>
    /// Eski yozuvlar bir marta to'ldiriladi: kartaning anketalaridan ochilgan lahzadan KEYIN
    /// kelgan eng oxirgisi. Anketa o'zi ochgan kartada birinchi anketa ochilish bilan bir
    /// tranzaksiyada (bir necha millisekund farq) — ikki daqiqalik chegara uni qayta murojaat deb
    /// hisoblamaydi. Down ustunni o'chiradi; boshqa jadvalga tegilmaydi.
    /// </remarks>
    public partial class NomzodQaytaMurojaat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reapplied_at",
                table: "candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE candidates AS c
                SET reapplied_at = f.last_submitted
                FROM (
                    SELECT cf.candidate_id, max(cf.submitted_at) AS last_submitted
                    FROM candidate_forms AS cf
                    JOIN candidates AS owner ON owner.id = cf.candidate_id
                    WHERE cf.submitted_at > owner.created_at + interval '2 minutes'
                    GROUP BY cf.candidate_id
                ) AS f
                WHERE c.id = f.candidate_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reapplied_at",
                table: "candidates");
        }
    }
}
