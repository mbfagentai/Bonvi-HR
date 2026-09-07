using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Ish taklifi jadvalini butunlay olib tashlaydi (<c>job_offers</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nima o'zgaradi.</b> Yagona amal — <c>DROP TABLE job_offers</c>
    /// (u bilan birga jadvalning O'Z indekslari va tashqi kalitlari
    /// ketadi). <c>candidates</c> va <c>vacancies</c> jadvallariga
    /// TEGILMAYDI: bog'lanish taklif tomonida turgan va uni o'chirish
    /// nomzod yoki vakansiya yozuvidan hech narsa olib tashlamaydi.
    /// </para>
    /// <para>
    /// <b>Nega ma'lumot yo'qolmaydi.</b> Prod bazasida jadval BO'SH (0
    /// qator) — funksiya hech qachon ishlatilmagan. Nomzoddan xodimga
    /// o'tish esa allaqachon taklifsiz ishlaydi
    /// (<c>POST /api/recruiting/candidates/{id}/hire</c>), ya'ni
    /// o'chirish hech qanday yo'lni uzmaydi
    /// (<c>docs/SPEC-nomzod-yakuni.md</c>, 3-bo'lim).
    /// </para>
    /// <para>
    /// <b><c>Down</c> jadvalni AYNAN eski shaklida qaytaradi</b> —
    /// ustunlar, ikkita tashqi kalit (nomzodga <c>CASCADE</c>,
    /// vakansiyaga <c>RESTRICT</c>) va uchala indeks bilan. Qatorlar
    /// qaytmaydi, lekin qaytariladigan qator ham yo'q edi.
    /// </para>
    /// </remarks>
    public partial class TaklifOlibTashlandi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_offers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_offers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_at = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    responded_at = table.Column<DateOnly>(type: "date", nullable: true),
                    response_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    sent_at = table.Column<DateOnly>(type: "date", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    vacancy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    salary_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    salary_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_offers", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_offers_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_job_offers_vacancies_vacancy_id",
                        column: x => x.vacancy_id,
                        principalTable: "vacancies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_offers_candidate_id_status",
                table: "job_offers",
                columns: new[] { "candidate_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_job_offers_sent_at",
                table: "job_offers",
                column: "sent_at");

            migrationBuilder.CreateIndex(
                name: "ix_job_offers_vacancy_id",
                table: "job_offers",
                column: "vacancy_id");
        }
    }
}
