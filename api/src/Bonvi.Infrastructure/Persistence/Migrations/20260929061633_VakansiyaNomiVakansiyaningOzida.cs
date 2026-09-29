using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Vakansiya nomi vakansiyaning O'ZIGA ko'chadi; «Vakansiya nomlari»
    /// ma'lumotnomasi va mas'ul xodim olib tashlanadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nima o'zgaradi.</b> Egasining 2026-09-29 dagi qarori: vakansiya
    /// BITTA joyda boshqariladi. Ilgari HR e'lon nomini va uning
    /// faolligini «Ma'lumotnomalar → Vakansiya nomlari» da, ochiq/yopiqligini
    /// esa «Vakansiyalar» da boshqarardi. Endi nom <c>vacancies.title</c>
    /// ustunida yashaydi, faollik — vakansiyaning holati. Mas'ul xodim
    /// (<c>responsible_employee_id</c>) ham kerak emas deb topildi.
    /// </para>
    /// <para>
    /// <b>⭐ Hech bir nom YO'QOLMAYDI.</b> Ikki yo'l bilan ko'chadi:
    /// </para>
    /// <list type="number">
    /// <item>
    /// Vakansiyasi bor nom — o'sha vakansiyalarning <c>title</c> iga
    /// yoziladi (yumshoq o'chirilganlariga ham: ustun majburiy).
    /// </item>
    /// <item>
    /// Birorta tirik vakansiyasi YO'Q nom (HR ma'lumotnomaga qo'shib,
    /// hali vakansiya ochmagan) — YOPIQ vakansiya bo'lib o'tadi. Ochiq
    /// emas, chunki ochiq vakansiya darhol botda nomzodga tanlov bo'lib
    /// chiqardi — bu esa ko'chirish emas, e'lon qilish bo'lardi. HR
    /// keraklisini bir bosishda qayta ochadi. Faolsizlantirilgan nomlar
    /// ham ko'chadi: ular ma'lumotnoma ro'yxatida ko'rinib turardi.
    /// </item>
    /// </list>
    /// <para>
    /// Ma'lumotnoma yozuvidagi IZOH ham yo'qolmaydi — vakansiya izohiga
    /// qo'shiladi. Qo'shilgan izoh 2000 belgiga sig'masa, migratsiya
    /// kesib yozmaydi, balki TO'XTAYDI (2-qadam).
    /// </para>
    /// <para>
    /// <b>Tartib muhim.</b> EF avtomatik yaratgan variant avval jadvalni
    /// o'chirib, keyin bo'sh ustun qo'shardi — ya'ni barcha nomlar
    /// yo'qolardi. Shu sababli: ustun → ko'chirish → majburiy qilish →
    /// eski havolalarni o'chirish.
    /// </para>
    /// <para>
    /// ⚠️ Birinchi variantda 4-qadam <c>vacancy_title_id</c> ni yozmasdi va
    /// vakansiyasiz BIRINCHI nomdayoq yiqilardi; dev bazada bunday nom yo'q
    /// edi, xatoni nusxadagi sinov topdi. Prod'dan oldin tekshiruv so'rovi —
    /// <c>docs/DEPLOY-CHECKLIST.md</c> da.
    /// </para>
    /// </remarks>
    public partial class VakansiyaNomiVakansiyaningOzida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Yangi ustun — hozircha bo'sh bo'lishi mumkin ──
            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "vacancies",
                type: "text",
                nullable: true,
                collation: "uz-Cyrl-x-icu");

            // ════════════════════════════════════════════════════════════
            //  2. Izoh sig'maydimi — JIMGINA KESILMAYDI
            //
            //  `vacancies.note` 2000 belgi bilan cheklangan, ma'lumotnoma
            //  izohi esa `text`. Qo'shilgan izoh sig'masa, migratsiya
            //  TO'XTAYDI (butun tranzaksiya qaytadi, API ko'tarilmaydi) —
            //  kesib yozish esa HR matnini hech kim sezmasdan yo'qotardi.
            //  Xavfsiz deploy tartibi (zaxira nusxasida sinov) buni prod'dan
            //  oldin ko'rsatadi; u holda izohni qo'lda qisqartirish kerak.
            //
            //  Bo'sh izoh — faqat bo'shliq, tabulyatsiya va qator
            //  uzilishidan iborat bo'lgani ham. Vakansiyasiz O'CHIRILGAN nom
            //  hisobga olinmaydi: u 4-qadamda ko'chmaydi, ya'ni uning izohi
            //  hech qayerga yozilmaydi va migratsiyani to'xtatmasligi kerak.
            // ════════════════════════════════════════════════════════════
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    sigmaydi int;
                BEGIN
                    SELECT count(*) INTO sigmaydi
                    FROM vacancy_titles t
                    LEFT JOIN vacancies v ON v.vacancy_title_id = t.id
                    WHERE btrim(coalesce(t.note, ''), E' \t\r\n') <> ''
                      AND (v.id IS NOT NULL OR t.deleted_at IS NULL)
                      AND length(
                            CASE
                                WHEN v.id IS NULL OR btrim(coalesce(v.note, ''), E' \t\r\n') = '' THEN t.note
                                WHEN v.note = t.note THEN v.note
                                ELSE v.note || E'\n\n' || t.note
                            END) > 2000;

                    IF sigmaydi > 0 THEN
                        RAISE EXCEPTION
                            'Vakansiya nomi izohi vakansiya izohiga sig''maydi (% ta yozuv, chegara 2000 belgi). Izohni qisqartirib, qayta urining.',
                            sigmaydi;
                    END IF;
                END
                $$;
                """);

            // ════════════════════════════════════════════════════════════
            //  3. Vakansiyasi bor nomlar — o'z vakansiyalariga
            //
            //  Izoh: vakansiyada izoh bo'lmasa — ma'lumotnomaniki olinadi;
            //  ikkalasi bo'lsa — ketma-ket yoziladi.
            // ════════════════════════════════════════════════════════════
            migrationBuilder.Sql(
                """
                UPDATE vacancies v
                SET title = t.name,
                    note = CASE
                        WHEN btrim(coalesce(t.note, ''), E' \t\r\n') = '' THEN v.note
                        WHEN btrim(coalesce(v.note, ''), E' \t\r\n') = '' THEN t.note
                        WHEN v.note = t.note THEN v.note
                        ELSE v.note || E'\n\n' || t.note
                    END
                FROM vacancy_titles t
                WHERE t.id = v.vacancy_title_id;
                """);

            // ════════════════════════════════════════════════════════════
            //  4. Vakansiyasiz nomlar — YOPIQ vakansiya bo'lib
            //
            //  «Tirik vakansiyasi yo'q» NOM bo'yicha tekshiriladi
            //  (registrsiz), havola bo'yicha emas: yumshoq o'chirilgan
            //  nomning vakansiyasi xuddi shu nomda bo'lishi mumkin va
            //  ikkinchi nusxa ochilmasligi kerak.
            //
            //  Sana — nom yaratilgan kun, Toshkent vaqtida (saqlash UTC,
            //  kun esa +5 bo'yicha — loyihaning umumiy qoidasi). Ochilgan
            //  va yopilgan sana bir xil: vakansiya aslida hech qachon
            //  ochiq turmagan.
            //
            //  `status = 3` — `VacancyStatus.Closed`.
            //
            //  `vacancy_title_id` ham yoziladi: ustun hali NOT NULL va
            //  tashqi kalitli — u 6-qadamda o'chiriladi. Usiz birinchi
            //  vakansiyasiz nomdayoq migratsiya yiqilardi.
            // ════════════════════════════════════════════════════════════
            migrationBuilder.Sql(
                """
                INSERT INTO vacancies (
                    id, vacancy_title_id, title, openings, opened_at, closed_at, status, note,
                    created_at, created_by, updated_at, updated_by)
                SELECT
                    uuidv7(),
                    t.id,
                    t.name,
                    1,
                    ((t.created_at AT TIME ZONE 'UTC') + interval '5 hours')::date,
                    ((t.created_at AT TIME ZONE 'UTC') + interval '5 hours')::date,
                    3,
                    CASE WHEN btrim(coalesce(t.note, ''), E' \t\r\n') = '' THEN NULL ELSE t.note END,
                    t.created_at,
                    t.created_by,
                    t.updated_at,
                    t.updated_by
                FROM vacancy_titles t
                WHERE t.deleted_at IS NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM vacancies v
                      WHERE v.deleted_at IS NULL
                        AND lower(v.title) = lower(t.name));
                """);

            // ── 5. Endi nom majburiy ──
            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "vacancies",
                type: "text",
                nullable: false,
                collation: "uz-Cyrl-x-icu",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldCollation: "uz-Cyrl-x-icu");

            // ── 6. Eski havolalar ketadi ──
            migrationBuilder.DropForeignKey(
                name: "fk_vacancies_employees_responsible_employee_id",
                table: "vacancies");

            migrationBuilder.DropForeignKey(
                name: "fk_vacancies_vacancy_titles_vacancy_title_id",
                table: "vacancies");

            migrationBuilder.DropIndex(
                name: "ix_vacancies_responsible_employee_id",
                table: "vacancies");

            migrationBuilder.DropIndex(
                name: "ix_vacancies_vacancy_title_id",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "responsible_employee_id",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "vacancy_title_id",
                table: "vacancies");

            // Jadval bilan birga uning `ix_vacancy_titles_name_lower` unikal
            // indeksi ham ketadi (u xom SQL bilan yaratilgan edi).
            migrationBuilder.DropTable(
                name: "vacancy_titles");
        }

        /// <summary>
        /// Sxemani ortga qaytaradi.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ⚠️ <b>Mas'ul xodim qaytmaydi</b> — ustun bo'sh tiklanadi: qiymat
        /// bazaning boshqa hech qayerida saqlanmagan.
        /// </para>
        /// <para>
        /// Ma'lumotnoma esa vakansiyalar NOMIDAN qayta yig'iladi: har bir
        /// noyob nom (registrsiz) — bitta yozuv. Identifikatorlar yangi,
        /// chunki eskilari saqlanmagan. <c>Up</c> da vakansiyasiz nomdan
        /// yaratilgan yopiq vakansiyalar esa vakansiya bo'lib QOLADI —
        /// ularni boshqalaridan ajratib bo'lmaydi va o'chirish HR
        /// ko'rgan yozuvni yo'qotardi.
        /// </para>
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.Sql(
                """
                INSERT INTO vacancy_titles (id, name, is_active, created_at)
                SELECT uuidv7(), d.title, TRUE, d.created_at
                FROM (
                    SELECT DISTINCT ON (lower(title)) title, created_at
                    FROM vacancies
                    ORDER BY lower(title), created_at
                ) d;

                CREATE UNIQUE INDEX ix_vacancy_titles_name_lower
                    ON vacancy_titles (lower(name))
                    WHERE deleted_at IS NULL;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "vacancy_title_id",
                table: "vacancies",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE vacancies v
                SET vacancy_title_id = t.id
                FROM vacancy_titles t
                WHERE lower(t.name) = lower(v.title);
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "vacancy_title_id",
                table: "vacancies",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "responsible_employee_id",
                table: "vacancies",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_vacancies_responsible_employee_id",
                table: "vacancies",
                column: "responsible_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacancies_vacancy_title_id",
                table: "vacancies",
                column: "vacancy_title_id");

            migrationBuilder.AddForeignKey(
                name: "fk_vacancies_employees_responsible_employee_id",
                table: "vacancies",
                column: "responsible_employee_id",
                principalTable: "employees",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vacancies_vacancy_titles_vacancy_title_id",
                table: "vacancies",
                column: "vacancy_title_id",
                principalTable: "vacancy_titles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "title",
                table: "vacancies");
        }
    }
}
