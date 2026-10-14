using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Ism bo'yicha qidiruv yozuvga (lotin / kirill) bog'liq bo'lmaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Muammo.</b> Ismlar bazada aralash: №91 va Jobster'dan kelganlar
    /// kirillda, bot orqali ariza berganlar esa o'z ismini lotinda yozgan.
    /// HR «Karimov» deb qidirsa «Каримов» chiqmasdi, «Каримов» deb qidirsa
    /// «Karimov» chiqmasdi — ya'ni har ikki holatda ham odamning bir qismi
    /// «yo'q» bo'lib ko'rinardi (egasining 2026-10-14 dagi talabi; 2026-09-01
    /// dagi «qanday yozilgan bo'lsa shunday» qarori shu bilan bekor bo'ldi).
    /// </para>
    /// <para>
    /// <b>Yechim — umumiy «qidiruv kaliti».</b> <c>search_key()</c> ikkala
    /// yozuvni ham bitta lotin shaklga keltiradi va qidiruv matni ham, ism ham
    /// shu kalit orqali solishtiriladi: «Ёқубов», «Yoqubov», «Yokubov»,
    /// «Екубов» → <c>ekubuv</c>. Qoida — funksiya izohida. Ism bazada QANDAY
    /// yozilgan bo'lsa shunday QOLADI: hech bir qator o'zgarmaydi, ekranda ham
    /// hech narsa o'zgarmaydi.
    /// </para>
    /// <para>
    /// <b>Nega kalit tayyor ustunda (<c>GENERATED … STORED</c>).</b> O'lchov
    /// (2026-10-14, 6 000 qator): har qidiruvda hisoblansa qisqa so'rov
    /// («ов», «a») 10 ms → 29 ms ga sekinlashardi — indeks topgan har bir
    /// qatorda uzun funksiya qayta ishlaydi. Saqlangan ustunda esa 0,5 ms,
    /// ya'ni bugungidan ham tez. Kalit qator yozilganda bir marta hisoblanadi.
    /// ⚠️ PostgreSQL 18 da <c>STORED</c> so'zisiz ustun VIRTUAL bo'ladi va
    /// uning ustiga indeks qurib bo'lmaydi.
    /// </para>
    /// <para>
    /// <b>Ustun EF modelida YO'Q — ataylab.</b> Uni faqat qidiruvning xom SQL
    /// so'rovi o'qiydi (<c>SearchQuery</c>). EF bilmagani uchun uni hech qachon
    /// yozmaydi (ustunga yozib ham bo'lmaydi — uni baza o'zi to'ldiradi),
    /// audit jurnalining eski → yangi qiymatlariga ham tushmaydi. Eski image'ga
    /// qaytilsa ham ishlaydi: eski kod ustunni ko'rmaydi, faqat qidiruvi
    /// indekssiz (≈10 ms) bo'ladi.
    /// </para>
    /// <para>
    /// ⚠️ Kelajakdagi to'g'ridan-to'g'ri SQL importlar: <c>INSERT INTO
    /// employees VALUES (…)</c> ustun ro'yxatisiz yozilsa, endi yiqiladi —
    /// ustunlarni nomi bilan sanang (<c>INSERT INTO employees (id, …)</c>).
    /// </para>
    /// <para>
    /// <b>Foydalanuvchilar</b> jadvaliga ustun qo'shilmaydi — unda bir necha
    /// o'nlab hisob bor; so'rov o'sha funksiyani to'g'ridan-to'g'ri chaqiradi.
    /// </para>
    /// </remarks>
    public partial class IsmQidiruvKaliti : Migration
    {
        /// <summary>To'liq ism ifodasi — ustun va eski indeks uchun bir xil.</summary>
        private const string FullName =
            "last_name || ' ' || first_name || ' ' || coalesce(middle_name, '')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Qoida (har qadam ikkala yozuvga ham qo'llanadi — tartib muhim):
            //  1. Kichik harf — ICU ildiz qoidasi bilan. Chaqiruvchining
            //     collation'iga bog'lanmaslik uchun ATAYLAB: "C" collation'da
            //     lower() faqat ASCII harflarni kichraytiradi va «Каримов»
            //     katta «К» bilan qolib, hech narsa topilmasdi.
            //  2. Kirill → lotin, bitta harflilari translate() bilan
            //     (ў → u, ғ → g, қ → q, ҳ → h), lotin diakritikasi esa
            //     asosiy harfga (Uluģbek → ulugbek).
            //  3. Ko'p harfli kirill: ш sh, ч ch, ц ts, ю yu, я ya, ё yo.
            //  4. Lotin «o'» (har qanday apostrof bilan) → u — xuddi ў kabi.
            //     ⚠️ ў va o' 7-qadamdan OLDIN «u» bo'lishi SHART: aks holda
            //     «йў» / «yo'» «yo» bo'lib qolib, 7-qadamda «e» ga aylanardi va
            //     «Йўлдошев» rus klaviaturasidagi «Йулдошев» ni (bugun
            //     topiladi) ham, «Юлдошев» ni ham topmay qolardi. Prod
            //     nusxasidagi sinovda aynan shu ushlangan (2026-10-14).
            //  5. Qolgan apostroflar va ъ/ь tashlanadi:
            //     G'ofurov = Gʻofurov = G‘ofurov = Gofurov.
            //  6. Ruscha lotin yozuvi: kh → x, dj/zh → j (Khasanov, Mamadjanov).
            //  7. q = k, h = x — HR rus klaviaturasida қ/ҳ o'rniga к/х yozadi,
            //     lotinda ham Xasan/Hasan aralash.
            //  8. ye → e, yo → e: Abdullayev = Абдуллаев, Yoqubov = Ёқубов, va
            //     eski «Екубов → Ёқубов» (ё = е) qoidasi saqlanadi.
            //  9. o = u: ў lotinda o', o yoki u bo'lib yoziladi — Жўраев,
            //     Jo'rayev, Jorayev, Jurayev bitta odam.
            //  Bilib turib QILINMAGAN: «Yoldoshev» (apostrofsiz) «Йўлдошев» ni
            //  topmaydi — buni 8-qadam bilan birga qilib bo'lmaydi; a/o
            //  almashuvi (Тошматов / Tashmatov) ham yo'q — u juda ko'p begona
            //  natija berardi.
            //
            // RETURN shakli (SQL standart tanasi) ataylab: tana yaratilishda
            // tahlil qilinadi, ya'ni pg_dump/pg_restore paytidagi bo'sh
            // search_path ham, keyin qo'shilgan bir xil nomli funksiya ham uni
            // buzolmaydi.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION search_key(value text) RETURNS text
                    LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE
                RETURN translate(
                    replace(replace(
                        translate(
                            replace(replace(replace(
                                regexp_replace(
                                    regexp_replace(
                                        replace(replace(replace(replace(replace(replace(replace(replace(replace(
                                            translate(lower(value COLLATE "und-x-icu"),
                                                'абвгғдежзийкқлмнопрстуўфхҳыэіģğǵöóòüúıé',
                                                'abvggdejziykqlmnoprstuufxhieigggooouuie'),
                                            'ш', 'sh'), 'щ', 'sh'), 'ч', 'ch'), 'ц', 'ts'), 'ю', 'yu'),
                                            'я', 'ya'), 'ё', 'yo'), 'ş', 'sh'), 'ç', 'ch'),
                                        'o[''ʻʼ‘’`´ʹ]', 'u', 'g'),
                                    '[''ʻʼ‘’`´ʹъь]', '', 'g'),
                                'kh', 'x'), 'dj', 'j'), 'zh', 'j'),
                            'qh', 'kx'),
                        'ye', 'e'), 'yo', 'e'),
                    'o', 'u');
                """);

            migrationBuilder.Sql(
                $"""
                ALTER TABLE employees
                    ADD COLUMN search_key text GENERATED ALWAYS AS (search_key({FullName})) STORED;
                """);

            migrationBuilder.Sql(
                $"""
                ALTER TABLE candidates
                    ADD COLUMN search_key text GENERATED ALWAYS AS (search_key({FullName})) STORED;
                """);

            // Eski indeks endi hech bir so'rovda ishlatilmaydi — u faqat har
            // yozuvda qo'shimcha ish bo'lib qolardi.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_employees_full_name_fold_trgm;");

            migrationBuilder.Sql(
                "CREATE INDEX ix_employees_search_key_trgm ON employees USING gin (search_key gin_trgm_ops);");

            // Nomzodlarda ilgari indeks yo'q edi («yiliga bir necha yuz
            // qator» deb hisoblangan), Jobster ko'chirilgach esa ular minglab.
            migrationBuilder.Sql(
                "CREATE INDEX ix_candidates_search_key_trgm ON candidates USING gin (search_key gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_candidates_search_key_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_employees_search_key_trgm;");
            migrationBuilder.Sql("ALTER TABLE candidates DROP COLUMN IF EXISTS search_key;");
            migrationBuilder.Sql("ALTER TABLE employees DROP COLUMN IF EXISTS search_key;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS search_key(text);");

            // Eski indeks aynan avvalgi ko'rinishida — eski so'rov kodi uni
            // faqat ifoda belgima-belgi mos tushganda ishlatadi.
            migrationBuilder.Sql(
                $"""
                CREATE INDEX ix_employees_full_name_fold_trgm
                    ON employees
                    USING gin (
                        translate(lower({FullName}), 'қғҳўё', 'кгхуе')
                        gin_trgm_ops);
                """);
        }
    }
}
