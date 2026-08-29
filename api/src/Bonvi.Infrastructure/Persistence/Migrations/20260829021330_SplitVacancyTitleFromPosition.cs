using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Vakansiya nomi kompaniya lavozimidan AJRATILADI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nima o'zgaradi.</b> Ilgari vakansiya <c>positions</c>
    /// ma'lumotnomasidan tanlanardi va yonida bo'lim hamda firma ham
    /// turardi. Endi vakansiya — faqat E'LON: yangi <c>vacancy_titles</c>
    /// ma'lumotnomasiga bitta havolasi qoladi, <c>department_id</c> va
    /// <c>legal_entity_id</c> esa butunlay o'chadi. Nega ular ataylab
    /// ikki xil tushuncha ekani <c>VacancyTitle</c> va <c>Vacancy</c>
    /// domen izohlarida.
    /// </para>
    /// <para>
    /// <b>⭐ Mavjud vakansiyalar YO'QOLMAYDI.</b> <c>position_id</c>
    /// ustuni <c>vacancy_title_id</c> ga QAYTA NOMLANADI (o'chirilib qayta
    /// yaratilmaydi), va yangi ma'lumotnomaga o'sha lavozimlarning nusxasi
    /// AYNAN O'SHA IDENTIFIKATORLAR bilan quyiladi. Natijada eski qatorlar
    /// yangi tashqi kalitga o'zgarishsiz tushadi va HR ro'yxatda hech
    /// narsa yo'qolganini ko'rmaydi — faqat nom endi o'zining
    /// ma'lumotnomasidan keladi.
    /// </para>
    /// <para>
    /// <b>Nega identifikator ko'chiriladi, yangi berilmaydi.</b> Yangi
    /// identifikator berilsa, har bir vakansiya uchun eski qiymatni
    /// yangisiga almashtiruvchi <c>UPDATE</c> kerak bo'lardi — ya'ni
    /// o'sha ishning uzunroq va xatoga ochiqroq varianti. Identifikator
    /// takrorlanishidan xavf yo'q: u ikki BOSHQA jadvalda yashaydi va
    /// hech qayerda birga ishlatilmaydi.
    /// </para>
    /// <para>
    /// <b>Nomlar takrorlanmaydi.</b> Nusxa faqat vakansiyada UCHRAGAN
    /// lavozimlardan olinadi; ular esa har xil yozuvlar, ya'ni
    /// <c>positions</c> dagi <c>lower(name)</c> unikal indeksi tufayli
    /// nomlari ham har xil. Shu sababli quyidagi <c>INSERT</c>
    /// <c>vacancy_titles</c> ning o'z unikal indeksini buzmaydi.
    /// </para>
    /// </remarks>
    public partial class SplitVacancyTitleFromPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Kompaniya tuzilmasiga bog'lovchi hamma narsa ketadi ──
            migrationBuilder.DropForeignKey(
                name: "fk_vacancies_departments_department_id",
                table: "vacancies");

            migrationBuilder.DropForeignKey(
                name: "fk_vacancies_legal_entities_legal_entity_id",
                table: "vacancies");

            migrationBuilder.DropForeignKey(
                name: "fk_vacancies_positions_position_id",
                table: "vacancies");

            migrationBuilder.DropIndex(
                name: "ix_vacancies_department_id",
                table: "vacancies");

            migrationBuilder.DropIndex(
                name: "ix_vacancies_legal_entity_id",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "legal_entity_id",
                table: "vacancies");

            // ── 2. Ustun QAYTA NOMLANADI, o'chirilmaydi ──
            //  Aynan shu qaror mavjud vakansiyalarni saqlab qoladi:
            //  qiymatlar joyida turadi va faqat ma'nosi ko'chadi.
            migrationBuilder.RenameColumn(
                name: "position_id",
                table: "vacancies",
                newName: "vacancy_title_id");

            migrationBuilder.RenameIndex(
                name: "ix_vacancies_position_id",
                table: "vacancies",
                newName: "ix_vacancies_vacancy_title_id");

            // ── 3. Yangi ma'lumotnoma ──
            migrationBuilder.CreateTable(
                name: "vacancy_titles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false, collation: "uz-Cyrl-x-icu"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vacancy_titles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vacancy_titles_is_active_name",
                table: "vacancy_titles",
                columns: new[] { "is_active", "name" });

            // ════════════════════════════════════════════════════════════
            //  4. MA'LUMOTNI KO'CHIRISH — tashqi kalitdan OLDIN
            //
            //  Bu qadam bo'lmasa, quyidagi tashqi kalit darhol yiqilardi:
            //  `vacancies.vacancy_title_id` da lavozim identifikatorlari
            //  turibdi, `vacancy_titles` esa bo'sh.
            //
            //  Yumshoq o'chirilgan vakansiyalar ham hisobga OLINADI: tashqi
            //  kalit `deleted_at` ni ko'rmaydi va bunday qator ham havolasi
            //  bilan birga bazada turibdi.
            //
            //  `is_active = TRUE`: nusxa olinayotgan lavozim
            //  faolsizlantirilgan bo'lishi mumkin, lekin uning e'lon nomi
            //  sifatidagi taqdiri boshqa — HR uni o'zi yopadi.
            // ════════════════════════════════════════════════════════════
            migrationBuilder.Sql(
                """
                INSERT INTO vacancy_titles (id, name, is_active, created_at)
                SELECT p.id, p.name, TRUE, now()
                FROM positions p
                WHERE EXISTS (SELECT 1 FROM vacancies v WHERE v.vacancy_title_id = p.id);
                """);

            // ── 5. Endi bog'lash xavfsiz ──
            migrationBuilder.AddForeignKey(
                name: "fk_vacancies_vacancy_titles_vacancy_title_id",
                table: "vacancies",
                column: "vacancy_title_id",
                principalTable: "vacancy_titles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // ════════════════════════════════════════════════════════════
            //  6. NOM bo'yicha unikal indeks — XOM SQL bilan
            //
            //  Boshqa sakkizta ma'lumotnomadagi bilan AYNAN bir xil qoida
            //  (`AddRecruiting` migratsiyasidagi `pipeline_stages` izohiga
            //  qarang): EF fluent API `lower(name)` kabi IFODA ustidagi
            //  indeksni ifodalay olmaydi, usiz esa «Сотувчи» va «сотувчи»
            //  ikki xil e'lon nomi bo'lib o'tib ketardi.
            //
            //  `WHERE deleted_at IS NULL` — yumshoq o'chirilgan yozuvning
            //  nomini qayta ishlatish mumkin bo'lishi kerak.
            //
            //  DIQQAT: bu indeks EF model suratida (snapshot) YO'Q.
            //  Jadval nomi o'zgarsa, bu yerdagi SQL ham qo'lda
            //  yangilanishi kerak.
            // ════════════════════════════════════════════════════════════
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_vacancy_titles_name_lower
                    ON vacancy_titles (lower(name))
                    WHERE deleted_at IS NULL;
                """);
        }

        /// <summary>
        /// Sxemani ortga qaytaradi.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ⚠️ Bu <b>faqat SXEMA</b> ni tiklaydi. O'chirilgan uchta
        /// ustunning MAZMUNI qaytmaydi: qaysi vakansiya qaysi firmaga va
        /// bo'limga tegishli bo'lgani bazada boshqa hech qayerda
        /// saqlanmagan. Uni «taxminan» to'ldirish yolg'on ma'lumot
        /// yaratardi, shu sababli firma ustuni bo'sh (nol) qiymat bilan
        /// tiklanadi va uning tashqi kaliti QAYTA QO'YILMAYDI.
        /// </para>
        /// <para>
        /// Lavozim havolasi esa aniq qaytadi — identifikatorlar
        /// ko'chirilgani sababli (<c>Up</c> izohiga qarang). Lekin bu
        /// faqat <c>Up</c> dan KEYIN darhol qaytarilganda to'g'ri:
        /// migratsiyadan so'ng HR qo'shgan yangi e'lon nomlarining
        /// identifikatori <c>positions</c> da yo'q va o'sha qatorlar
        /// uchun tashqi kalit tiklanmaydi — shu sababli ular oldindan
        /// o'chiriladi.
        /// </para>
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_vacancy_titles_name_lower;");

            migrationBuilder.DropForeignKey(
                name: "fk_vacancies_vacancy_titles_vacancy_title_id",
                table: "vacancies");

            // Lavozimlar ma'lumotnomasida aksi bo'lmagan e'lon nomlari —
            // migratsiyadan keyin qo'shilganlar. Ular bilan birga
            // vakansiyalar ham ketadi: eski sxemada bunday yozuvni
            // ifodalash mumkin emas.
            migrationBuilder.Sql(
                """
                DELETE FROM candidate_stage_entries WHERE candidate_id IN (
                    SELECT c.id FROM candidates c
                    JOIN vacancies v ON v.id = c.vacancy_id
                    WHERE NOT EXISTS (SELECT 1 FROM positions p WHERE p.id = v.vacancy_title_id));

                DELETE FROM job_offers WHERE vacancy_id IN (
                    SELECT v.id FROM vacancies v
                    WHERE NOT EXISTS (SELECT 1 FROM positions p WHERE p.id = v.vacancy_title_id));

                DELETE FROM candidates WHERE vacancy_id IN (
                    SELECT v.id FROM vacancies v
                    WHERE NOT EXISTS (SELECT 1 FROM positions p WHERE p.id = v.vacancy_title_id));

                DELETE FROM vacancies v
                WHERE NOT EXISTS (SELECT 1 FROM positions p WHERE p.id = v.vacancy_title_id);
                """);

            migrationBuilder.DropTable(
                name: "vacancy_titles");

            migrationBuilder.RenameColumn(
                name: "vacancy_title_id",
                table: "vacancies",
                newName: "position_id");

            migrationBuilder.RenameIndex(
                name: "ix_vacancies_vacancy_title_id",
                table: "vacancies",
                newName: "ix_vacancies_position_id");

            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                table: "vacancies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "legal_entity_id",
                table: "vacancies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_vacancies_department_id",
                table: "vacancies",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacancies_legal_entity_id",
                table: "vacancies",
                column: "legal_entity_id");

            migrationBuilder.AddForeignKey(
                name: "fk_vacancies_departments_department_id",
                table: "vacancies",
                column: "department_id",
                principalTable: "departments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // ⚠️ `fk_vacancies_legal_entities_legal_entity_id` ATAYLAB
            // tiklanmaydi: ustun nol qiymat bilan to'lgan va bunday
            // identifikator `legal_entities` da yo'q — cheklov darhol
            // yiqilardi. Sxemani to'liq tiklash uchun ma'lumotni qo'lda
            // to'g'rilab, keyin cheklovni qo'shish kerak.

            migrationBuilder.AddForeignKey(
                name: "fk_vacancies_positions_position_id",
                table: "vacancies",
                column: "position_id",
                principalTable: "positions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
