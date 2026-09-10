using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Nomzod kartasining uchta yangiligi: KO'P fayl, amal izohlari va
    /// keyingi aloqa sanasi. Eski BITTA rezyume ustunlari shu bilan birga
    /// yangi jadvalga ko'chiriladi va o'chiriladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nima uchun kerak.</b> Foydalanuvchi talabi (2026-09-10): pasport
    /// va diplom telefonda uch-to'rt rasm bo'lib keladi, nomzodda esa
    /// bitta fayl o'rni bor edi; HR har amaldan keyin izoh yozadigan joy
    /// so'radi; «qo'ng'iroq qildim, 5-6 kunda keladi dedi» degani uchun
    /// esa aloqa sanasi kerak bo'ldi. Uchalasi bitta migratsiyada, chunki
    /// EF butun modelni bitta <c>AppDbContextModelSnapshot</c> ga
    /// seriyalaydi — parallel migratsiya yozib bo'lmaydi.
    /// </para>
    /// <para>
    /// ⚠️ <b><see cref="Up"/> DAGI TARTIB — ENG MUHIM QISM.</b>
    /// Avtomatik yaratilgan variant to'rtta <c>DROP COLUMN</c> ni ENG
    /// BOSHIDA chiqargan edi, ya'ni u ishlab turgan bazadagi barcha
    /// rezyumelarni yo'q qilardi. Shu sababli fayl qo'lda qayta yozildi:
    /// avval jadvallar yaratiladi, keyin ma'lumot KO'CHIRILADI, eng
    /// oxirida ustunlar o'chiriladi. Bu tartibni buzish — nomzodlarning
    /// rezyumelarini yo'qotish.
    /// </para>
    /// <para>
    /// <b>Nega ustunlar HOZIR o'chiriladi, bitta relizga qoldirilmaydi.</b>
    /// Deploy nishoni bitta (bitta VPS, <c>/opt/bonvi-hr</c>) va API bilan
    /// web birga chiqadi — ya'ni eski ustunga yozishda davom etadigan
    /// eskirgan API yo'q, «ikki relizda o'chirish» qoidasining sababi bu
    /// yerda ishlamaydi. Yozilmaydigan, lekin turgan ustun esa tuzoq:
    /// keyingi <c>--autogenerate</c> uni baribir o'chirishni taklif
    /// qiladi, oradagi vaqtda esa kimdir uni o'qib eskirgan qiymat oladi.
    /// Xavfsizlik to'ri — <see cref="Down"/> va migratsiya testi.
    /// </para>
    /// <para>
    /// <b>Ustun turlari — snapshotdan, SPEC jadvalidan emas.</b>
    /// <c>resume_file_key</c> amalda <c>text</c> bo'lgan (unga hech qachon
    /// <c>HasMaxLength</c> qo'yilmagan), shu sababli <see cref="Down"/> ni
    /// ham aynan <c>text</c> tiklaydi. Yangi <c>candidate_files.file_key</c>
    /// esa <c>varchar(500)</c> — u <c>employee_documents.file_key</c> dan
    /// ko'chirilgan chegara.
    /// </para>
    /// </remarks>
    public partial class NomzodIzohVaFayllar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // ── 1. Fayllar jadvali ───────────────────────────────────
            migrationBuilder.CreateTable(
                name: "candidate_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_files", x => x.id);
                    table.ForeignKey(
                        name: "fk_candidate_files_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Fayllar DOIM nomzod bo'yicha va yuklash tartibida o'qiladi;
            // o'nta chegarasining sanog'i ham shu indeksga tushadi.
            migrationBuilder.CreateIndex(
                name: "ix_candidate_files_candidate_id_created_at",
                table: "candidate_files",
                columns: new[] { "candidate_id", "created_at" });

            // ── 2. Izohlar jadvali ───────────────────────────────────
            migrationBuilder.CreateTable(
                name: "candidate_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_candidate_notes_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_notes_candidate_id_created_at",
                table: "candidate_notes",
                columns: new[] { "candidate_id", "created_at" });

            // ── 3. Keyingi aloqa sanasi ──────────────────────────────
            migrationBuilder.AddColumn<DateOnly>(
                name: "next_contact_date",
                table: "candidates",
                type: "date",
                nullable: true);

            // Qisman indeks — telefon va xabar navbati indekslari bilan bir
            // xil sabab: nomzodlarning ko'pida aloqa sanasi yo'q va to'liq
            // indeks minglab NULL yozuvni bekorga saqlardi.
            migrationBuilder.CreateIndex(
                name: "ix_candidates_next_contact_date",
                table: "candidates",
                column: "next_contact_date",
                filter: "next_contact_date IS NOT NULL");

            /*
              ── 4. MAVJUD REZYUMELARNI KO'CHIRISH ─────────────────────

              ⚠️ Bu blok o'chirishdan OLDIN turishi SHART (izohi sinf
              tepasida): u ishlab turgan bazadagi haqiqiy nomzod
              fayllarini saqlab qoladi.

              `WHERE resume_file_key IS NOT NULL` — fayl borligini
              isbotlaydigan yagona ustun. `AttachResume` hech qachon
              kalitni tur bilan birga yozmasdan qo'ymagan, lekin qo'lda
              yoki ko'chirish bilan kelgan qatorda tur bo'sh bo'lishi
              mumkin — shu sababli `NOT NULL` ni buzib migratsiyani
              yiqitish o'rniga `coalesce` zaxira qiymat qo'yadi.

              `created_at` NOMZODNING yaratilgan vaqtidan olinadi,
              `updated_at` dan EMAS: yuklash lahzasi hech qachon
              yozilmagan (eski karta yuklash sanasini ataylab
              ko'rsatmagan), nomzodning `created_at` esa haqiqiy yuklash
              vaqtidan kechikmaydigan yagona kafolatlangan qiymat.
              `updated_at` har bir aloqasiz tahrirda siljib ketardi va
              fayllar tartibi ma'nosini yo'qotardi.

              `created_by` — NULL: kim yuklagani rostdan ham noma'lum,
              ustun aynan shu holat uchun ixtiyoriy.

              `gen_random_uuid()` (v4) — bu yerdagi barcha migratsiyalar
              bilan bir xil; kod yo'lidagi yozuvlar UUID v7 oladi
              (`Entity.NewId()`). Ustun uchun ikkisining farqi yo'q.
            */
            migrationBuilder.Sql("""
                INSERT INTO candidate_files
                    (id, candidate_id, file_key, file_name, content_type, size_bytes, created_at, created_by)
                SELECT gen_random_uuid(),
                       id,
                       resume_file_key,
                       coalesce(nullif(btrim(resume_file_name), ''), 'rezyume'),
                       coalesce(nullif(btrim(resume_content_type), ''), 'application/octet-stream'),
                       coalesce(resume_size_bytes, 0),
                       created_at,
                       NULL
                  FROM candidates
                 WHERE resume_file_key IS NOT NULL;
                """);

            // ── 5. Eski ustunlar ─────────────────────────────────────
            // Faqat shu to'rttasi o'chiriladi — boshqa hech qanday
            // `DROP` bu migratsiyada yo'q va bo'lmasligi ham kerak.
            migrationBuilder.DropColumn(
                name: "resume_file_key",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "resume_file_name",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "resume_content_type",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "resume_size_bytes",
                table: "candidates");
        }

        /// <summary>
        /// To'rtta ustunni qaytaradi va har bir nomzodning ENG ESKI faylini
        /// ularga tiklaydi, so'ng ikkala yangi jadvalni va aloqa sanasini
        /// olib tashlaydi.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Eng eski fayl ATAYLAB: ko'chirilgan ma'lumot uchun u aynan
        /// asl rezyume. Tanlov aniq (<c>DISTINCT ON ... ORDER BY
        /// candidate_id, created_at, id</c>) — bir lahzada yuklangan ikki
        /// fayl bo'lsa ham natija har safar bir xil bo'ladi.
        /// </para>
        /// <para>
        /// ⚠️ <b>Qabul qilingan YO'QOTISH.</b> Ikkinchidan o'ninchisiga
        /// qadar fayllar diskda YETIM qoladi (migratsiyaning
        /// <c>IFileStorage</c> ga yo'li yo'q), barcha aloqa sanalari va
        /// barcha izohlar esa butunlay yo'qoladi. <see cref="Down"/> — bu
        /// ishlab chiqish uchun zaxira yo'l, production amali emas;
        /// keyingi o'quvchi buni kutmasdan qolmasligi uchun shu yerda
        /// ochiq yozilgan.
        /// </para>
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // ── 1. Ustunlarni asl turlari bilan qaytarish ────────────
            migrationBuilder.AddColumn<string>(
                name: "resume_file_key",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resume_file_name",
                table: "candidates",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resume_content_type",
                table: "candidates",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "resume_size_bytes",
                table: "candidates",
                type: "bigint",
                nullable: true);

            // ── 2. Eng eski faylni ustunlarga qaytarish ──────────────
            // `nullif(size_bytes, 0)`: 0 — «hajmi noma'lum» degani
            // (`CandidateFile.SizeBytes` izohi), eski ustunda esa bu
            // holat NULL bilan ifodalangan edi.
            migrationBuilder.Sql("""
                UPDATE candidates AS c
                   SET resume_file_key     = f.file_key,
                       resume_file_name    = f.file_name,
                       resume_content_type = f.content_type,
                       resume_size_bytes   = nullif(f.size_bytes, 0)
                  FROM (SELECT DISTINCT ON (candidate_id)
                               candidate_id, file_key, file_name, content_type, size_bytes
                          FROM candidate_files
                         ORDER BY candidate_id, created_at, id) AS f
                 WHERE c.id = f.candidate_id;
                """);

            // ── 3. Yangi jadvallar ──────────────────────────────────
            migrationBuilder.DropTable(
                name: "candidate_files");

            migrationBuilder.DropTable(
                name: "candidate_notes");

            // ── 4. Aloqa sanasi ─────────────────────────────────────
            migrationBuilder.DropIndex(
                name: "ix_candidates_next_contact_date",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "next_contact_date",
                table: "candidates");
        }
    }
}
