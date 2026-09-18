using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Tashqi API kalitlari (<c>api_keys</c>) — holdingning boshqa loyihalari
    /// uchun FAQAT O'QISH huquqi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nima uchun kerak.</b> Boshqa loyihalarimizga xodimlar ma'lumoti
    /// kerak va u bir tomonli o'qiladi (<c>docs/SPEC-tashqi-api.md</c>,
    /// egasining 2026-09-18 dagi qarori: kalit bilan, IP ro'yxatisiz).
    /// Kalit sozlamalar ekranidan yaratiladi va bekor qilinadi.
    /// </para>
    /// <para>
    /// ⚠️ <b>Kalitning O'ZI bu jadvalda YO'Q.</b> Saqlanadigan narsa —
    /// <c>key_prefix</c> (ochiq, 8 belgi, qidiruv tutqichi) va
    /// <c>key_hash</c> (to'liq kalitning SHA-256 xeshi, 32 bayt). Bazaga
    /// kirgan odam ham kalitni tiklay olmaydi; yo'qolgan kalitning yagona
    /// chorasi — bekor qilib, yangisini yaratish.
    /// </para>
    /// <para>
    /// <b>Yozuvlar hech qachon o'chirilmaydi</b> va jadval
    /// <c>ISoftDeletable</c> ham emas: bekor qilingan kalit tarix bo'lib
    /// qoladi («bu kalit kimga berilgan edi va qachon o'chirilgan?» —
    /// xavfsizlik tekshiruvining birinchi savoli). Shu sababli bu yerda
    /// <c>deleted_at</c> ustuni YO'Q.
    /// </para>
    /// <para>
    /// <b>Avtomatik yaratilgan variant o'qildi</b> va u toza edi: bitta
    /// <c>CREATE TABLE</c>, bitta unikal indeks, birorta ham kutilmagan
    /// <c>DROP</c> yo'q. Qo'lda faqat IKKI narsa qo'shildi —
    /// <c>ArgumentNullException</c> tekshiruvi (loyihaning uslubi) va
    /// nom bo'yicha QISMAN unikal indeks (pastdagi izoh).
    /// </para>
    /// </remarks>
    public partial class TashqiApiKalitlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    key_prefix = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    key_hash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_used_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_api_keys", x => x.id);
                });

            // Kalitni tekshirishning YAGONA so'rovi shu indeks bo'yicha
            // ketadi (`ApiKeyRepository.FindByPrefixAsync`) va u har bir
            // ommaviy so'rovda bajariladi. Unikal: ikkita kalit bir xil
            // prefiks bilan yashasa, tekshiruv qaysi biriga tegishli
            // ekanini bilmasdi.
            migrationBuilder.CreateIndex(
                name: "ix_api_keys_key_prefix",
                table: "api_keys",
                column: "key_prefix",
                unique: true);

            // ════════════════════════════════════════════════════════════
            //  NOM BO'YICHA UNIKALLIK — FAQAT KUCHDAGI KALITLAR ORASIDA
            //
            //  `WHERE revoked_at IS NULL` — ataylab: sizib ketgan
            //  «BonviCall» kaliti bekor qilingandan keyin o'sha loyihaga
            //  AYNAN o'sha nom bilan yangi kalit berish mumkin bo'lishi
            //  kerak. To'liq unikal indeks buni to'sib qo'yardi va nomga
            //  «BonviCall-2» kabi qo'shimcha yozishga majbur qilardi.
            //
            //  `lower(name)` ustida — ma'lumotnomalardagi bilan aynan bir
            //  xil naqsh va bir xil sabab: «BonviCall» va «bonvicall» bir
            //  xil loyiha. Kod tomonidagi tekshiruv ham AYNAN shu ifodada
            //  (`ApiKeyRepository.NameEquals`), aks holda tekshiruvdan
            //  o'tgan nom bazada 23505 bilan yiqilib, mijoz 409 o'rniga
            //  500 olardi.
            //
            //  Nega EF `HasIndex(...)` emas: fluent API ifoda bo'yicha
            //  indeksni ifodalay olmaydi (`CatalogTables` migratsiyasida
            //  batafsil).
            //
            //  DIQQAT: bu indeks EF model suratida (snapshot) YO'Q — u
            //  qo'shimcha cheklov va keyingi migratsiyalar uni ko'rmaydi,
            //  ya'ni o'chirmaydi ham. Jadval yoki ustun nomi o'zgarsa,
            //  bu SQL qo'lda yangilanishi kerak.
            // ════════════════════════════════════════════════════════════
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_api_keys_name_lower_active
                    ON api_keys (lower(name))
                    WHERE revoked_at IS NULL;
                """);
        }

        /// <summary>
        /// Jadvalni butunlay olib tashlaydi.
        /// </summary>
        /// <remarks>
        /// ⚠️ <b>Qabul qilingan YO'QOTISH:</b> barcha kalitlar yo'qoladi
        /// va tashqi loyihalar darhol 401 oladi. Ularni tiklashning yo'li
        /// yo'q (kalitlar xeshlangan), ya'ni har bir iste'molchiga YANGI
        /// kalit berish kerak bo'ladi. Bu — ishlab chiqish uchun zaxira
        /// yo'l, production amali emas.
        /// <para>
        /// Xom SQL bilan yaratilgan indeks alohida o'chirilmaydi:
        /// <c>DROP TABLE</c> uni jadval bilan birga olib ketadi.
        /// </para>
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "api_keys");
        }
    }
}
