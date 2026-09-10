using System.Globalization;
using Bonvi.Domain.Recruiting;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NpgsqlTypes;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <c>20260910051756_NomzodIzohVaFayllar</c> migratsiyasining MA'LUMOT
/// ustidagi testi: eski rezyume ustunlari yangi <c>candidate_files</c>
/// jadvaliga HAQIQATAN ko'chadimi, <c>Down</c> ularni qaytaradimi va
/// undan keyin <c>Up</c> yana ishlaydimi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <see cref="BonviApiFactory"/> yetarli emas.</b> U har bir test
/// yig'masi uchun BO'SH baza yaratadi va unga barcha migratsiyalarni
/// qo'llaydi. Ko'chirish esa aynan bo'sh bazada HECH QACHON yiqilmaydi:
/// <c>INSERT ... SELECT ... WHERE resume_file_key IS NOT NULL</c> nol
/// qator ko'chiradi va to'rtta <c>DROP COLUMN</c> hech kimning faylini
/// yo'qotmaydi. Ya'ni yashil test «ma'lumot saqlandi» degani emas —
/// «ma'lumot yo'q edi» degani. Production VPS'da esa haqiqiy nomzodlarning
/// rezyumelari yotibdi.
/// </para>
/// <para>
/// <b>Shu sababli bu yerdagi testlar boshqacha yo'ldan boradi:</b> o'z
/// vaqtinchalik bazasini yaratadi, uni migratsiyadan OLDINGI holatga
/// (<c>20260908061342_SuhbatgaChaqirish</c>) keltiradi, xom SQL bilan
/// «eski» nomzodlarni yozadi va shundan keyingina yuqoriga ko'taradi.
/// Faqat shu tartibda ko'chirish bloki ishlaydi va faqat shu tartibda
/// uning xatosi ko'rinadi.
/// </para>
/// <para>
/// <b>Nishon migratsiya nomi bilan ATAYLAB ko'rsatilgan</b>
/// (<c>null</c> = «eng oxirgisi» emas): <c>Down</c> testi aynan SHU
/// migratsiyaning qaytarilishini tekshiradi. Keyinchalik yangi migratsiya
/// qo'shilsa, «bir qadam pastga» degani boshqa narsani anglatib qolardi va
/// test jim qolib buzilardi.
/// </para>
/// <para>
/// <b>Baza.</b> Har bir test o'ziga alohida <c>bonvi_migr_xxxxx</c>
/// yaratadi va tugagach o'chiradi — <see cref="BonviApiFactory"/> dagi
/// qoidaning aynan o'zi. Ishlab chiqish bazasi (<c>bonvi_hr</c>) va
/// boshqa testlarning bazalari umuman ochilmaydi.
/// </para>
/// </remarks>
public sealed class CandidateFileMigrationTests
{
    /// <summary>Ko'chirishdan OLDINGI holat.</summary>
    private const string OldingiMigratsiya = "20260908061342_SuhbatgaChaqirish";

    /// <summary>Tekshirilayotgan migratsiya.</summary>
    private const string KochirishMigratsiyasi = "20260910051756_NomzodIzohVaFayllar";

    /// <summary>Nomzod yaratilgan lahza — ko'chirilgan fayl AYNAN shuni oladi.</summary>
    private static readonly DateTimeOffset Yaratilgan = new(2026, 1, 15, 8, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// Oxirgi tahrir — ko'chirishda ISHLATILMASLIGI kerak.
    /// </summary>
    /// <remarks>
    /// <c>created_at</c> o'rniga <c>updated_at</c> yozilishi eng ehtimolli
    /// xato (ikkalasi ham qo'shni ustun). Sanalar ataylab bir-biridan olti
    /// oy uzoq — adashtirilsa tekshiruv darhol yiqiladi.
    /// </remarks>
    private static readonly DateTimeOffset Yangilangan = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    // ────────────────────────────────────────────────────────────
    //  1. Ko'chirish
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Eski to'rtta ustundagi rezyume yangi jadvalga ko'chadi; chala
    /// yozuv zaxira qiymat oladi; rezyumesiz nomzodga qator yaratilmaydi.
    /// </summary>
    [Fact]
    public async Task Eski_rezyume_yangi_jadvalga_KOCHIRILADI()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        // Test o'z sozlamasini isbotlaydi: eski ustun SHU nuqtada bor.
        // Bo'lmasa quyidagi INSERT'lar «ko'chirish» ni umuman tekshirmasdi.
        (await UstunAsync(ulanish, "candidates", "resume_file_key", bekorQilish))
            .ShouldNotBeNull("test migratsiyadan OLDINGI holatda turishi kerak");

        var vakansiya = await VakansiyaQoshAsync(ulanish, bekorQilish);

        // (a) — to'liq rezyume. Hajm ataylab int32 dan katta: `bigint`
        //       ustuni yo'lda `integer` ga aylanib qolmasin.
        var toliq = Guid.NewGuid();
        await NomzodQoshAsync(
            ulanish, toliq, vakansiya, "Рустамов",
            "2026/01/0192f3c4d5e611f0abcd1234567890ab.pdf",
            "Рустамов Ж. резюме.pdf", "application/pdf", 9_000_000_000L, bekorQilish);

        // (b) — kalit bor, qolgani YO'Q: nomi bo'sh joy, turi va hajmi NULL.
        var chala = Guid.NewGuid();
        await NomzodQoshAsync(
            ulanish, chala, vakansiya, "Каримова",
            "2026/01/0192f3c4d5e611f0abcd1234567890cd.bin",
            "   ", null, null, bekorQilish);

        // (b-2) — o'sha holatning ikkinchi ko'rinishi: nomi ATROFIDA bo'sh
        //         joy, turi BO'SH SATR, hajmi nol. `btrim` ham, `nullif` ham
        //         shu qatorda tekshiriladi.
        var bosh = Guid.NewGuid();
        await NomzodQoshAsync(
            ulanish, bosh, vakansiya, "Юсупов",
            "2026/01/0192f3c4d5e611f0abcd1234567890ef.jpg",
            "  Паспорт.jpg  ", "", 0L, bekorQilish);

        // (c) — rezyumesiz nomzod. Unga qator YARATILMASLIGI kerak.
        var rezyumesiz = Guid.NewGuid();
        await NomzodQoshAsync(
            ulanish, rezyumesiz, vakansiya, "Эргашев", null, null, null, null, bekorQilish);

        var nomzodlarOldin = await SanoqAsync(ulanish, "SELECT count(*) FROM candidates;", bekorQilish);
        nomzodlarOldin.ShouldBe(4);

        // ── Migratsiya ───────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, KochirishMigratsiyasi, bekorQilish);

        // Bironta nomzod yo'qolmadi.
        (await SanoqAsync(ulanish, "SELECT count(*) FROM candidates;", bekorQilish))
            .ShouldBe(nomzodlarOldin, "migratsiya nomzod qatorlarini yo'qotdi");

        // ── (a) to'liq rezyume — qiymatlar AYNAN ko'chdi ─────────
        var toliqFayllar = await FayllarAsync(ulanish, toliq, bekorQilish);
        toliqFayllar.Count.ShouldBe(1, "har bir rezyumedan ANIQ bitta fayl qatori chiqishi kerak");
        toliqFayllar[0].Kalit.ShouldBe("2026/01/0192f3c4d5e611f0abcd1234567890ab.pdf");
        toliqFayllar[0].Nom.ShouldBe("Рустамов Ж. резюме.pdf");
        toliqFayllar[0].Tur.ShouldBe("application/pdf");
        toliqFayllar[0].Hajm.ShouldBe(9_000_000_000L);
        toliqFayllar[0].Kim.ShouldBeNull("kim yuklagani noma'lum — ustun shu holat uchun ixtiyoriy");

        // ⭐ Vaqt NOMZODNING yaratilgan lahzasidan, tahrir lahzasidan EMAS.
        var nomzodVaqti = await VaqtAsync(
            ulanish, "SELECT created_at FROM candidates WHERE id = $1;", bekorQilish, toliq);

        nomzodVaqti.ShouldBe(Yaratilgan);
        toliqFayllar[0].Yaratilgan.ShouldBe(nomzodVaqti, "created_at nomzodnikidan olinishi kerak");
        toliqFayllar[0].Yaratilgan.ShouldNotBe(Yangilangan, "updated_at ko'chirilgan — bu xato");

        // ── (b) chala yozuv — zaxira qiymatlar ───────────────────
        var chalaFayllar = await FayllarAsync(ulanish, chala, bekorQilish);
        chalaFayllar.Count.ShouldBe(1, "kalit bor ekan, fayl ko'chirilishi SHART");
        chalaFayllar[0].Nom.ShouldBe("rezyume");
        chalaFayllar[0].Tur.ShouldBe("application/octet-stream");
        chalaFayllar[0].Hajm.ShouldBe(0);

        // ── (b-2) bo'sh satr va nol ──────────────────────────────
        var boshFayllar = await FayllarAsync(ulanish, bosh, bekorQilish);
        boshFayllar.Count.ShouldBe(1);
        boshFayllar[0].Nom.ShouldBe("Паспорт.jpg", "nom ko'chirishda tozalanadi");
        boshFayllar[0].Tur.ShouldBe("application/octet-stream");
        boshFayllar[0].Hajm.ShouldBe(0);

        // ── (c) rezyumesiz nomzod ────────────────────────────────
        (await FayllarAsync(ulanish, rezyumesiz, bekorQilish))
            .ShouldBeEmpty("rezyumesiz nomzodga fayl qatori yaratilmasligi kerak");

        (await SanoqAsync(ulanish, "SELECT count(*) FROM candidate_files;", bekorQilish))
            .ShouldBe(3, "ko'chirish faqat kaliti BOR qatorlarni olishi kerak");

        (await SanoqAsync(ulanish, "SELECT count(*) FROM candidate_notes;", bekorQilish))
            .ShouldBe(0, "ko'chirish izoh yaratmaydi");

        // ── Eski ustunlar yo'q ───────────────────────────────────
        foreach (var ustun in EskiUstunlar)
        {
            (await UstunAsync(ulanish, "candidates", ustun, bekorQilish))
                .ShouldBeNull($"«{ustun}» ustuni o'chirilishi kerak edi");
        }

        // ── Qisman indeks va kaskad ──────────────────────────────
        var indeks = await IndeksAsync(ulanish, "ix_candidates_next_contact_date", bekorQilish);
        indeks.ShouldNotBeNull("aloqa sanasi indeksi yaratilmadi");
        indeks.ShouldContain(
            "next_contact_date IS NOT NULL",
            Case.Sensitive,
            "indeks QISMAN bo'lishi kerak — to'lig'i minglab NULL yozuvni saqlardi");

        (await QiymatAsync(
            ulanish,
            """
            SELECT delete_rule FROM information_schema.referential_constraints
             WHERE constraint_schema = 'public' AND constraint_name = $1;
            """,
            bekorQilish,
            "fk_candidate_files_candidates_candidate_id"))
            .ShouldBe("CASCADE", "nomzod o'chirilganda fayl qatorlari YETIM qolmasligi kerak");
    }

    // ────────────────────────────────────────────────────────────
    //  2. Down
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>Down</c> to'rtta ustunni asl turlari bilan qaytaradi va ularga
    /// nomzodning ENG ESKI faylini — asl rezyumesini — tiklaydi.
    /// </summary>
    /// <remarks>
    /// Nomzodga ko'chirilgan rezyumedan keyin yana ikkita fayl qo'shiladi
    /// (pasport, diplom). Agar tanlov «oxirgisi» yoki «tasodifiy» bo'lib
    /// qolsa, ustunlarda pasport nusxasi paydo bo'lardi — kartada esa u
    /// rezyume deb ko'rsatilardi. Qo'shimcha fayllarning
    /// identifikatorlari ATAYLAB eng kichigi qilib berilgan: saralash
    /// <c>created_at</c> emas, <c>id</c> bo'yicha ketsa test yiqiladi.
    /// </remarks>
    [Fact]
    public async Task Down_eng_eski_faylni_ustunlarga_QAYTARADI()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        var vakansiya = await VakansiyaQoshAsync(ulanish, bekorQilish);

        var toliq = Guid.NewGuid();
        await NomzodQoshAsync(
            ulanish, toliq, vakansiya, "Рустамов",
            "2026/01/0192f3c4d5e611f0abcd1234567890ab.pdf",
            "Рустамов Ж. резюме.pdf", "application/pdf", 9_000_000_000L, bekorQilish);

        // Hajmi noma'lum eski yozuv: 0 → NULL bo'lib qaytishi kerak.
        var chala = Guid.NewGuid();
        await NomzodQoshAsync(
            ulanish, chala, vakansiya, "Каримова",
            "2026/01/0192f3c4d5e611f0abcd1234567890cd.bin",
            null, null, 0L, bekorQilish);

        await MigratsiyaAsync(baza.UlanishSatri, KochirishMigratsiyasi, bekorQilish);

        // ── Rezyumedan KEYIN kelgan fayllar ─────────────────────
        await FaylQoshAsync(
            ulanish, new Guid("00000000-0000-0000-0000-000000000001"), toliq,
            "2026/03/passport.jpg", "Паспорт 1-бет.jpg", "image/jpeg", 240_000L,
            Yaratilgan.AddMonths(2), bekorQilish);

        await FaylQoshAsync(
            ulanish, new Guid("00000000-0000-0000-0000-000000000002"), toliq,
            "2026/05/diplom.jpg", "Диплом.jpg", "image/jpeg", 310_000L,
            Yaratilgan.AddMonths(4), bekorQilish);

        await IzohQoshAsync(ulanish, toliq, "Қўнғироқ қилдим, 5-6 кунда келади деди", bekorQilish);
        await AloqaSanasiAsync(ulanish, toliq, new DateOnly(2026, 9, 20), bekorQilish);

        (await FayllarAsync(ulanish, toliq, bekorQilish)).Count.ShouldBe(3, "test sozlamasi noto'g'ri");

        // ── Down ────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        (await SanoqAsync(ulanish, "SELECT count(*) FROM candidates;", bekorQilish))
            .ShouldBe(2, "Down nomzod qatorlarini yo'qotdi");

        // ── Ustunlar asl TURI bilan qaytdi ──────────────────────
        var kalitUstuni = await UstunAsync(ulanish, "candidates", "resume_file_key", bekorQilish);
        kalitUstuni.ShouldNotBeNull();
        kalitUstuni.Tur.ShouldBe("text");
        kalitUstuni.Uzunlik.ShouldBeNull("kalitga hech qachon uzunlik chegarasi qo'yilmagan");
        kalitUstuni.Nullable.ShouldBeTrue();

        var nomUstuni = await UstunAsync(ulanish, "candidates", "resume_file_name", bekorQilish);
        nomUstuni.ShouldNotBeNull();
        nomUstuni.Tur.ShouldBe("character varying");
        nomUstuni.Uzunlik.ShouldBe(300);
        nomUstuni.Nullable.ShouldBeTrue();

        var turUstuni = await UstunAsync(ulanish, "candidates", "resume_content_type", bekorQilish);
        turUstuni.ShouldNotBeNull();
        turUstuni.Tur.ShouldBe("character varying");
        turUstuni.Uzunlik.ShouldBe(150);
        turUstuni.Nullable.ShouldBeTrue();

        var hajmUstuni = await UstunAsync(ulanish, "candidates", "resume_size_bytes", bekorQilish);
        hajmUstuni.ShouldNotBeNull();
        hajmUstuni.Tur.ShouldBe("bigint");
        hajmUstuni.Nullable.ShouldBeTrue();

        // ⭐ Ustunlarda ASL rezyume turishi kerak, keyin kelgan pasport emas.
        var tiklangan = await RezyumeAsync(ulanish, toliq, bekorQilish);
        tiklangan.Kalit.ShouldBe(
            "2026/01/0192f3c4d5e611f0abcd1234567890ab.pdf",
            "ENG ESKI fayl qaytishi kerak — pasport nusxasi rezyume emas");
        tiklangan.Nom.ShouldBe("Рустамов Ж. резюме.pdf");
        tiklangan.Tur.ShouldBe("application/pdf");
        tiklangan.Hajm.ShouldBe(9_000_000_000L);

        // Hajmi noma'lum yozuv: 0 emas, NULL bo'lib qaytadi.
        var chalaTiklangan = await RezyumeAsync(ulanish, chala, bekorQilish);
        chalaTiklangan.Kalit.ShouldBe("2026/01/0192f3c4d5e611f0abcd1234567890cd.bin");
        chalaTiklangan.Nom.ShouldBe("rezyume");
        chalaTiklangan.Tur.ShouldBe("application/octet-stream");
        chalaTiklangan.Hajm.ShouldBeNull("«hajmi noma'lum» eski ustunda NULL bilan ifodalanadi");

        // ── Yangi jadvallar, ustun va indeks yo'q ───────────────
        (await JadvalBormiAsync(ulanish, "candidate_files", bekorQilish)).ShouldBeFalse();
        (await JadvalBormiAsync(ulanish, "candidate_notes", bekorQilish)).ShouldBeFalse();

        (await UstunAsync(ulanish, "candidates", "next_contact_date", bekorQilish)).ShouldBeNull();
        (await IndeksAsync(ulanish, "ix_candidates_next_contact_date", bekorQilish)).ShouldBeNull();
    }

    // ────────────────────────────────────────────────────────────
    //  3. Down → Up
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>Down</c> dan keyin <c>Up</c> yana ishlaydi va ma'lumotni
    /// QAYTADAN ko'chiradi.
    /// </summary>
    /// <remarks>
    /// Bu — zaxira yo'lning haqiqiy sinovi: production'da migratsiya
    /// qaytarilsa, keyin uni yana qo'llash kerak bo'ladi. Ikkinchi
    /// <c>Up</c> yiqilsa yoki fayl qatorini takrorlab yuborsa, orqaga
    /// qaytish yo'li yopiq degani.
    /// </remarks>
    [Fact]
    public async Task Down_dan_keyin_Up_malumotni_QAYTA_kochiradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        var vakansiya = await VakansiyaQoshAsync(ulanish, bekorQilish);

        var toliq = Guid.NewGuid();
        await NomzodQoshAsync(
            ulanish, toliq, vakansiya, "Рустамов",
            "2026/01/0192f3c4d5e611f0abcd1234567890ab.pdf",
            "Рустамов Ж. резюме.pdf", "application/pdf", 9_000_000_000L, bekorQilish);

        var rezyumesiz = Guid.NewGuid();
        await NomzodQoshAsync(
            ulanish, rezyumesiz, vakansiya, "Эргашев", null, null, null, null, bekorQilish);

        await MigratsiyaAsync(baza.UlanishSatri, KochirishMigratsiyasi, bekorQilish);
        (await SanoqAsync(ulanish, "SELECT count(*) FROM candidate_files;", bekorQilish)).ShouldBe(1);

        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);
        (await UstunAsync(ulanish, "candidates", "resume_file_key", bekorQilish)).ShouldNotBeNull();

        // ── Ikkinchi Up ─────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, KochirishMigratsiyasi, bekorQilish);

        var fayllar = await FayllarAsync(ulanish, toliq, bekorQilish);
        fayllar.Count.ShouldBe(1, "qayta ko'chirish fayl qatorini takrorlamasligi kerak");
        fayllar[0].Kalit.ShouldBe("2026/01/0192f3c4d5e611f0abcd1234567890ab.pdf");
        fayllar[0].Nom.ShouldBe("Рустамов Ж. резюме.pdf");
        fayllar[0].Tur.ShouldBe("application/pdf");
        fayllar[0].Hajm.ShouldBe(9_000_000_000L);
        fayllar[0].Yaratilgan.ShouldBe(Yaratilgan);

        (await FayllarAsync(ulanish, rezyumesiz, bekorQilish)).ShouldBeEmpty();

        (await SanoqAsync(ulanish, "SELECT count(*) FROM candidate_files;", bekorQilish)).ShouldBe(1);
        (await SanoqAsync(ulanish, "SELECT count(*) FROM candidates;", bekorQilish)).ShouldBe(2);

        foreach (var ustun in EskiUstunlar)
        {
            (await UstunAsync(ulanish, "candidates", ustun, bekorQilish)).ShouldBeNull();
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    /// <summary>Migratsiya o'chiradigan to'rtta ustun.</summary>
    private static readonly string[] EskiUstunlar =
    [
        "resume_file_key",
        "resume_file_name",
        "resume_content_type",
        "resume_size_bytes",
    ];

    /// <summary>Bazani ko'rsatilgan migratsiyagacha ko'taradi yoki tushiradi.</summary>
    /// <remarks>
    /// <see cref="IMigrator"/> ishlatiladi, <c>Database.MigrateAsync()</c>
    /// emas: ikkinchisi FAQAT eng oxirgi migratsiyagacha ko'tara oladi va
    /// «bir qadam orqaga» degan holat undan umuman chiqmaydi.
    /// </remarks>
    private static async Task MigratsiyaAsync(
        string ulanishSatri,
        string nishon,
        CancellationToken bekorQilish)
    {
        var sozlamalar = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ulanishSatri)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var kontekst = new AppDbContext(sozlamalar);
        await kontekst.GetService<IMigrator>().MigrateAsync(nishon, bekorQilish);
    }

    /// <summary>Nomzod uchun kerak bo'ladigan eng qisqa zanjir: unvon → vakansiya.</summary>
    private static async Task<Guid> VakansiyaQoshAsync(
        NpgsqlConnection ulanish,
        CancellationToken bekorQilish)
    {
        var unvon = Guid.NewGuid();
        var vakansiya = Guid.NewGuid();
        var vaqt = new DateTimeOffset(2025, 12, 1, 9, 0, 0, TimeSpan.Zero);

        await BajarAsync(
            ulanish,
            """
            INSERT INTO vacancy_titles (id, name, is_active, created_at)
            VALUES ($1, $2, true, $3);
            """,
            bekorQilish,
            unvon,
            "Оператор",
            vaqt);

        await BajarAsync(
            ulanish,
            """
            INSERT INTO vacancies (id, vacancy_title_id, opened_at, openings, status, created_at)
            VALUES ($1, $2, $3, 1, $4, $5);
            """,
            bekorQilish,
            vakansiya,
            unvon,
            new DateOnly(2025, 12, 1),
            (int)VacancyStatus.Open,
            vaqt);

        return vakansiya;
    }

    /// <summary>«Eski» nomzodni to'rtta rezyume ustuni bilan yozadi.</summary>
    private static async Task NomzodQoshAsync(
        NpgsqlConnection ulanish,
        Guid id,
        Guid vakansiya,
        string familiya,
        string? kalit,
        string? nom,
        string? tur,
        long? hajm,
        CancellationToken bekorQilish)
    {
        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = """
            INSERT INTO candidates
                (id, vacancy_id, first_name, last_name, status, source,
                 created_at, updated_at,
                 resume_file_key, resume_file_name, resume_content_type, resume_size_bytes)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12);
            """;

        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = id });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = vakansiya });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = "Азиз" });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = familiya });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = (int)CandidateStatus.InProgress });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = (int)CandidateSource.Manual });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = Yaratilgan });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = Yangilangan });
        _ = buyruq.Parameters.Add(Ixtiyoriy(NpgsqlDbType.Text, kalit));
        _ = buyruq.Parameters.Add(Ixtiyoriy(NpgsqlDbType.Varchar, nom));
        _ = buyruq.Parameters.Add(Ixtiyoriy(NpgsqlDbType.Varchar, tur));
        _ = buyruq.Parameters.Add(Ixtiyoriy(NpgsqlDbType.Bigint, hajm));

        _ = await buyruq.ExecuteNonQueryAsync(bekorQilish);
    }

    /// <summary>Ko'chirishdan KEYIN qo'shilgan fayl (pasport, diplom).</summary>
    private static Task FaylQoshAsync(
        NpgsqlConnection ulanish,
        Guid id,
        Guid nomzod,
        string kalit,
        string nom,
        string tur,
        long hajm,
        DateTimeOffset yaratilgan,
        CancellationToken bekorQilish)
        => BajarAsync(
            ulanish,
            """
            INSERT INTO candidate_files
                (id, candidate_id, file_key, file_name, content_type, size_bytes, created_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7);
            """,
            bekorQilish,
            id,
            nomzod,
            kalit,
            nom,
            tur,
            hajm,
            yaratilgan);

    private static Task IzohQoshAsync(
        NpgsqlConnection ulanish,
        Guid nomzod,
        string matn,
        CancellationToken bekorQilish)
        => BajarAsync(
            ulanish,
            """
            INSERT INTO candidate_notes (id, candidate_id, kind, text, created_at)
            VALUES (gen_random_uuid(), $1, $2, $3, $4);
            """,
            bekorQilish,
            nomzod,
            (int)CandidateNoteKind.Reassign,
            matn,
            Yangilangan);

    private static Task AloqaSanasiAsync(
        NpgsqlConnection ulanish,
        Guid nomzod,
        DateOnly sana,
        CancellationToken bekorQilish)
        => BajarAsync(
            ulanish,
            "UPDATE candidates SET next_contact_date = $2 WHERE id = $1;",
            bekorQilish,
            nomzod,
            sana);

    /// <summary>Nomzodning fayllari — yuklash tartibida.</summary>
    private static async Task<IReadOnlyList<FaylQatori>> FayllarAsync(
        NpgsqlConnection ulanish,
        Guid nomzod,
        CancellationToken bekorQilish)
    {
        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = """
            SELECT file_key, file_name, content_type, size_bytes, created_at, created_by
              FROM candidate_files
             WHERE candidate_id = $1
             ORDER BY created_at, id;
            """;
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = nomzod });

        var qatorlar = new List<FaylQatori>();

        await using var oquvchi = await buyruq.ExecuteReaderAsync(bekorQilish);
        while (await oquvchi.ReadAsync(bekorQilish))
        {
            qatorlar.Add(new FaylQatori(
                oquvchi.GetString(0),
                oquvchi.GetString(1),
                oquvchi.GetString(2),
                oquvchi.GetInt64(3),
                oquvchi.GetFieldValue<DateTimeOffset>(4),
                await oquvchi.IsDBNullAsync(5, bekorQilish) ? null : oquvchi.GetGuid(5)));
        }

        return qatorlar;
    }

    /// <summary>Eski to'rtta ustundagi qiymat (<c>Down</c> dan keyin).</summary>
    private static async Task<EskiRezyume> RezyumeAsync(
        NpgsqlConnection ulanish,
        Guid nomzod,
        CancellationToken bekorQilish)
    {
        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = """
            SELECT resume_file_key, resume_file_name, resume_content_type, resume_size_bytes
              FROM candidates
             WHERE id = $1;
            """;
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = nomzod });

        await using var oquvchi = await buyruq.ExecuteReaderAsync(bekorQilish);
        (await oquvchi.ReadAsync(bekorQilish)).ShouldBeTrue("nomzod topilmadi");

        return new EskiRezyume(
            await oquvchi.IsDBNullAsync(0, bekorQilish) ? null : oquvchi.GetString(0),
            await oquvchi.IsDBNullAsync(1, bekorQilish) ? null : oquvchi.GetString(1),
            await oquvchi.IsDBNullAsync(2, bekorQilish) ? null : oquvchi.GetString(2),
            await oquvchi.IsDBNullAsync(3, bekorQilish) ? null : oquvchi.GetInt64(3));
    }

    /// <summary>Ustun tavsifi yoki ustun yo'q bo'lsa <c>null</c>.</summary>
    private static async Task<UstunTavsifi?> UstunAsync(
        NpgsqlConnection ulanish,
        string jadval,
        string ustun,
        CancellationToken bekorQilish)
    {
        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = """
            SELECT data_type, character_maximum_length, is_nullable
              FROM information_schema.columns
             WHERE table_schema = 'public' AND table_name = $1 AND column_name = $2;
            """;
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = jadval });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = ustun });

        await using var oquvchi = await buyruq.ExecuteReaderAsync(bekorQilish);
        if (!await oquvchi.ReadAsync(bekorQilish))
        {
            return null;
        }

        return new UstunTavsifi(
            oquvchi.GetString(0),
            await oquvchi.IsDBNullAsync(1, bekorQilish) ? null : oquvchi.GetInt32(1),
            oquvchi.GetString(2) == "YES");
    }

    private static async Task<bool> JadvalBormiAsync(
        NpgsqlConnection ulanish,
        string jadval,
        CancellationToken bekorQilish)
        => await SanoqAsync(
            ulanish,
            """
            SELECT count(*) FROM information_schema.tables
             WHERE table_schema = 'public' AND table_name = $1;
            """,
            bekorQilish,
            jadval) > 0;

    private static async Task<string?> IndeksAsync(
        NpgsqlConnection ulanish,
        string nom,
        CancellationToken bekorQilish)
        => (string?)await QiymatAsync(
            ulanish,
            """
            SELECT indexdef FROM pg_indexes
             WHERE schemaname = 'public' AND indexname = $1;
            """,
            bekorQilish,
            nom);

    private static async Task<long> SanoqAsync(
        NpgsqlConnection ulanish,
        string sql,
        CancellationToken bekorQilish,
        params object[] qiymatlar)
        => (long)(await QiymatAsync(ulanish, sql, bekorQilish, qiymatlar))!;

    /// <summary>
    /// <c>timestamptz</c> ustunini <see cref="DateTimeOffset"/> qilib o'qiydi.
    /// </summary>
    /// <remarks>
    /// <c>ExecuteScalar</c> bu ustunni <see cref="DateTime"/> qilib qaytaradi
    /// (Npgsql standarti) — o'qish OSHKORA turlanishi shu sababli kerak.
    /// </remarks>
    private static async Task<DateTimeOffset> VaqtAsync(
        NpgsqlConnection ulanish,
        string sql,
        CancellationToken bekorQilish,
        params object[] qiymatlar)
    {
        await using var buyruq = Buyruq(ulanish, sql, qiymatlar);

        await using var oquvchi = await buyruq.ExecuteReaderAsync(bekorQilish);
        (await oquvchi.ReadAsync(bekorQilish)).ShouldBeTrue("qator topilmadi");

        return oquvchi.GetFieldValue<DateTimeOffset>(0);
    }

    private static async Task<object?> QiymatAsync(
        NpgsqlConnection ulanish,
        string sql,
        CancellationToken bekorQilish,
        params object[] qiymatlar)
    {
        await using var buyruq = Buyruq(ulanish, sql, qiymatlar);
        var natija = await buyruq.ExecuteScalarAsync(bekorQilish);

        return natija is DBNull ? null : natija;
    }

    private static async Task BajarAsync(
        NpgsqlConnection ulanish,
        string sql,
        CancellationToken bekorQilish,
        params object[] qiymatlar)
    {
        await using var buyruq = Buyruq(ulanish, sql, qiymatlar);
        _ = await buyruq.ExecuteNonQueryAsync(bekorQilish);
    }

    private static NpgsqlCommand Buyruq(NpgsqlConnection ulanish, string sql, params object[] qiymatlar)
    {
        var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = sql;

        foreach (var qiymat in qiymatlar)
        {
            _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = qiymat });
        }

        return buyruq;
    }

    /// <summary>
    /// NULL bo'lishi mumkin bo'lgan parametr — turi OSHKORA ko'rsatiladi.
    /// </summary>
    /// <remarks>
    /// <c>DBNull</c> dan Npgsql ustun turini taxmin qila olmaydi va
    /// parametrni matn deb yuboradi — <c>bigint</c> ustunida bu xatoga
    /// aylanadi.
    /// </remarks>
    private static NpgsqlParameter Ixtiyoriy(NpgsqlDbType tur, object? qiymat)
        => new() { NpgsqlDbType = tur, Value = qiymat ?? DBNull.Value };

    private sealed record FaylQatori(
        string Kalit,
        string Nom,
        string Tur,
        long Hajm,
        DateTimeOffset Yaratilgan,
        Guid? Kim);

    private sealed record EskiRezyume(string? Kalit, string? Nom, string? Tur, long? Hajm);

    private sealed record UstunTavsifi(string Tur, int? Uzunlik, bool Nullable);

    /// <summary>
    /// Faqat shu test uchun yaratilgan va tugagach o'chiriladigan baza.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="BonviApiFactory"/> dagi tartibning aynan o'zi, lekin
    /// veb-xostsiz: bu yerda API kerak emas, migratsiya esa
    /// <see cref="IMigrator"/> orqali qo'lda boshqariladi.
    /// </para>
    /// <para>
    /// Nom <c>Guid.NewGuid()</c> (v4) dan olinadi — v7 ning boshlang'ich
    /// belgilari bir millisekundda bir xil bo'lib qolardi va parallel
    /// ishlaydigan sinflar to'qnashardi. Sabab
    /// <see cref="BonviApiFactory"/> da batafsil yozilgan.
    /// </para>
    /// </remarks>
    private sealed class VaqtinchalikBaza : IAsyncDisposable
    {
        /// <summary>Bu testlar yaratadigan bazalarning belgisi.</summary>
        private const string Prefiks = "bonvi_migr_";

        private static readonly string AdminUlanish =
            Environment.GetEnvironmentVariable("TEST_POSTGRES")
            ?? "Host=postgres;Port=5432;Database=postgres;Username=bonvi;Password=bonvi_dev_parol";

        private readonly string _nom;

        private VaqtinchalikBaza(string nom, string ulanishSatri)
        {
            _nom = nom;
            UlanishSatri = ulanishSatri;
        }

        public string UlanishSatri { get; }

        public static async Task<VaqtinchalikBaza> YaratAsync(CancellationToken bekorQilish)
        {
            var nom = Prefiks + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];

            await using (var admin = new NpgsqlConnection(AdminUlanish))
            {
                await admin.OpenAsync(bekorQilish);

                await using var yarat = admin.CreateCommand();
                yarat.CommandText = $"""CREATE DATABASE "{nom}";""";
                _ = await yarat.ExecuteNonQueryAsync(bekorQilish);
            }

            var quruvchi = new NpgsqlConnectionStringBuilder(AdminUlanish) { Database = nom };

            return new VaqtinchalikBaza(nom, quruvchi.ConnectionString);
        }

        public async ValueTask DisposeAsync()
        {
            // ⚠️ Xavfsizlik to'ri: DROP faqat SHU testlar yaratgan nomga
            // tushishi mumkin. `bonvi_hr` — ishlab chiqish bazasi va unda
            // haqiqiy ma'lumot bor.
            if (!_nom.StartsWith(Prefiks, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"«{_nom}» — bu testlar yaratgan baza emas, o'chirilmaydi.");
            }

            await using var ochiqUlanish = new NpgsqlConnection(UlanishSatri);
            NpgsqlConnection.ClearPool(ochiqUlanish);

            await using var admin = new NpgsqlConnection(AdminUlanish);
            await admin.OpenAsync();

            await using var ochir = admin.CreateCommand();
            ochir.CommandText = $"""DROP DATABASE IF EXISTS "{_nom}" WITH (FORCE);""";
            _ = await ochir.ExecuteNonQueryAsync();
        }
    }
}
