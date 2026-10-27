using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Nomzod anketasi HR-F02 (SPEC-planshet-anketa §6.1): yangi <c>candidate_forms</c> jadvali va
    /// «Anketa №» ketma-ketligi <c>candidate_form_number_seq</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Faqat QO'SHISH: bitta jadval, bitta ketma-ketlik, uchta indeks. Mavjud jadvallarga tegilmaydi —
    /// <c>candidates</c> ga faqat FK (CASCADE) qo'shiladi, u ham yangi jadval tomonida. UPDATE yo'q.
    /// Yangi enum qiymatlari (manba «OnlineForm», «Oliy (magistr)», «Beva», «4–5 mln», «7 mln+»,
    /// «E'lon», «Kollej / OTM») SON bo'lib saqlanadi va migratsiya talab qilmaydi.
    /// </para>
    /// <para>
    /// 2026-10-26 da qayta yasalgan (deploy qilinmagan <c>20261024043920_PlanshetAnketasi</c> o'rniga):
    /// forma endi ochiq havola, planshet kaliti yo'q — <c>api_keys</c> ga tegilmaydi, jadvalda qurilma
    /// ustunlari (<c>device_key_id</c>, <c>device_name</c>) yo'q.
    /// </para>
    /// <para>
    /// ⚠️ Down onlayn anketalarni (javoblar, imzo kaliti) O'CHIRADI; nomzodga biriktirilgan PDF
    /// fayllari va yaratilgan nomzodlar esa qoladi (ular boshqa jadvallarda).
    /// </para>
    /// </remarks>
    public partial class PlanshetAnketasi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET lock_timeout = '3s';");

            migrationBuilder.CreateSequence<int>(
                name: "candidate_form_number_seq");

            migrationBuilder.CreateTable(
                name: "candidate_forms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    form_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    form_version = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    answers = table.Column<string>(type: "jsonb", nullable: false),
                    language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    photo_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    signature_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    client_submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_forms", x => x.id);
                    table.ForeignKey(
                        name: "fk_candidate_forms_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_forms_candidate_id_submitted_at",
                table: "candidate_forms",
                columns: new[] { "candidate_id", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_forms_client_submission_id",
                table: "candidate_forms",
                column: "client_submission_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_candidate_forms_number",
                table: "candidate_forms",
                column: "number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_forms");

            migrationBuilder.DropSequence(
                name: "candidate_form_number_seq");
        }
    }
}
