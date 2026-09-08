using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Voronkaga ikkita bosqich qo'shadi: «Гаплашилди» va
    /// «Кенгашга чақирилди».
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nima uchun kerak.</b> Foydalanuvchi talabi (2026-09-08): HR
    /// nomzodga telefon qilgandan keyin uni «gaplashildi» bosqichiga, kengash
    /// kuniga chaqirgandan keyin esa «kengashga chaqirildi» bosqichiga
    /// o'tkazishi kerak. Urug' fayliga (<c>pipeline-stages.json</c>) ikkala
    /// bosqich qo'shildi, lekin urug' faqat BO'SH ma'lumotnomani to'ldiradi
    /// (<c>CatalogSeeder.SeedCatalogAsync</c> izohi) — ishlab turgan bazada u
    /// hech narsa qilmaydi. Shu sababli mavjud bazaga ular migratsiya bilan
    /// qo'shiladi.
    /// </para>
    /// <para>
    /// <b>Nega jadval BO'SH bo'lsa hech narsa qilinmaydi.</b> Bo'sh jadval —
    /// bu yangi o'rnatish: migratsiyalardan keyin urug'lantiruvchi ishlaydi
    /// va butun voronkani (beshta bosqichni) o'zi quradi. Agar bu yerda
    /// ikkita qator kiritilsa, ma'lumotnoma «bo'sh emas» bo'lib qolardi va
    /// urug'lantiruvchi qolgan uchtasini — «Анкета», «Кенгаш», «Раҳбар
    /// суҳбати» ni — umuman qo'shmasdi.
    /// </para>
    /// <para>
    /// <b>Tartib — mazmun bo'yicha, oxiriga tirkash emas.</b> Voronkada
    /// <c>sort_order</c> bezak emas, ma'lumotning o'zi
    /// (<c>PipelineStage.SortOrder</c> izohi): «Гаплашилди» «Анкета» dan
    /// keyin, «Кенгашга чақирилди» esa «Кенгаш» dan OLDIN turishi kerak.
    /// Shu sababli qo'shishdan oldin keyingi bosqichlarning tartibi bittaga
    /// suriladi. Agar mo'ljal bosqichi topilmasa (HR uni qayta nomlagan yoki
    /// o'chirgan bo'lsa), yangi bosqich oxiriga qo'shiladi — migratsiya
    /// HECH QACHON yiqilmaydi, HR esa tartibni ekrandan tuzata oladi.
    /// </para>
    /// <para>
    /// <b>Takroriy ishga tushirishga chidamli.</b> Har bir qo'shish nom
    /// bo'yicha tekshiriladi (<c>lower(name)</c> — unikal indeks bilan bir xil
    /// qoida), ya'ni HR bosqichni allaqachon qo'lda qo'shgan bo'lsa, ikkinchi
    /// nusxa paydo bo'lmaydi.
    /// </para>
    /// </remarks>
    public partial class VoronkaBosqichlariToldirildi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    urin int;
                BEGIN
                    -- Bo'sh ma'lumotnoma — yangi o'rnatish: voronkani
                    -- urug'lantiruvchi to'liq quradi, bu yerda tegilmaydi.
                    IF NOT EXISTS (SELECT 1 FROM pipeline_stages) THEN
                        RETURN;
                    END IF;

                    -- ── «Гаплашилди» — «Анкета» dan KEYIN ──────────────
                    IF NOT EXISTS (
                        SELECT 1 FROM pipeline_stages
                         WHERE deleted_at IS NULL AND lower(name) = lower('Гаплашилди'))
                    THEN
                        SELECT sort_order INTO urin
                          FROM pipeline_stages
                         WHERE deleted_at IS NULL AND lower(name) = lower('Анкета')
                         ORDER BY sort_order
                         LIMIT 1;

                        -- Mo'ljal topilmadi — oxiriga.
                        IF urin IS NULL THEN
                            SELECT coalesce(max(sort_order), -1) INTO urin FROM pipeline_stages;
                        END IF;

                        UPDATE pipeline_stages
                           SET sort_order = sort_order + 1
                         WHERE sort_order > urin;

                        INSERT INTO pipeline_stages (id, sort_order, name, is_active, note, created_at)
                        VALUES (
                            gen_random_uuid(),
                            urin + 1,
                            'Гаплашилди',
                            true,
                            'HR телефон қилиб гаплашди. Номзод кўрсаткичи эмас — АЛОҚА фақти: шундан кейин у анкета тўлдиргани келади.',
                            now());
                    END IF;

                    -- ── «Кенгашга чақирилди» — «Кенгаш» dan OLDIN ──────
                    IF NOT EXISTS (
                        SELECT 1 FROM pipeline_stages
                         WHERE deleted_at IS NULL AND lower(name) = lower('Кенгашга чақирилди'))
                    THEN
                        SELECT sort_order INTO urin
                          FROM pipeline_stages
                         WHERE deleted_at IS NULL AND lower(name) = lower('Кенгаш')
                         ORDER BY sort_order
                         LIMIT 1;

                        IF urin IS NULL THEN
                            SELECT coalesce(max(sort_order), -1) + 1 INTO urin FROM pipeline_stages;
                        END IF;

                        UPDATE pipeline_stages
                           SET sort_order = sort_order + 1
                         WHERE sort_order >= urin;

                        INSERT INTO pipeline_stages (id, sort_order, name, is_active, note, created_at)
                        VALUES (
                            gen_random_uuid(),
                            urin,
                            'Кенгашга чақирилди',
                            true,
                            'Кенгаш кунига таклиф қилинди. «Кенгаш» дан фарқи: бу ТАКЛИФ, хулоса эмас — хулоса кенгаш бўлиб ўтгач ёзилади.',
                            now());
                    END IF;
                END $$;
                """);
        }

        /// <summary>
        /// Ikkala bosqichni olib tashlaydi — lekin FAQAT ularga hech kim
        /// bog'lanmagan bo'lsa.
        /// </summary>
        /// <remarks>
        /// Nomzod allaqachon shu bosqichga o'tkazilgan bo'lsa, yozuv QOLADI:
        /// uni o'chirish nomzodning bosqich tarixini yo'q qilardi (va tashqi
        /// kalit baribir yiqitardi). Bu — <c>ISoftDeletable</c> falsafasi
        /// bilan bir xil qoida: HR ma'lumotidan hech narsa jimgina
        /// yo'qolmaydi. Qolgan bosqichlar tartibi 0 dan boshlab qayta
        /// zichlanadi, aks holda voronkada bo'sh raqamlar qolardi (nisbiy
        /// tartib to'g'ri bo'lsa ham, keyingi qo'shish o'sha bo'shliqqa
        /// tushib qolishi mumkin edi).
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql("""
                DELETE FROM pipeline_stages s
                 WHERE lower(s.name) IN (lower('Гаплашилди'), lower('Кенгашга чақирилди'))
                   AND NOT EXISTS (
                       SELECT 1 FROM candidate_stage_entries e WHERE e.stage_id = s.id)
                   AND NOT EXISTS (
                       SELECT 1 FROM candidates c WHERE c.current_stage_id = s.id);

                WITH tartib AS (
                    SELECT id, row_number() OVER (ORDER BY sort_order, name) - 1 AS urin
                      FROM pipeline_stages
                )
                UPDATE pipeline_stages p
                   SET sort_order = tartib.urin
                  FROM tartib
                 WHERE p.id = tartib.id
                   AND p.sort_order <> tartib.urin;
                """);
        }
    }
}
