using System.Globalization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NpgsqlTypes;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <c>20260929061633_VakansiyaNomiVakansiyaningOzida</c> migratsiyasining
/// MA'LUMOT ustidagi testi: «Vakansiya nomlari» ma'lumotnomasidagi HAR BIR
/// nom vakansiyalar bo'limiga o'tadimi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <see cref="BonviApiFactory"/> yetarli emas</b> — sabab
/// <see cref="CandidateFileMigrationTests"/> izohida: bo'sh bazada
/// ko'chirish nol qator ko'chiradi va hech qachon yiqilmaydi. Aynan shunday
/// bo'lgan ham: birinchi variant vakansiyasiz nomdayoq yiqilardi, dev
/// bazada esa bunday nom yo'q edi va xato faqat nusxadagi qo'lda sinovda
/// ko'rindi. Shu sababli baza migratsiyadan OLDINGI holatga keltiriladi,
/// «eski» nomlar va vakansiyalar xom SQL bilan yoziladi va shundan
/// keyingina yuqoriga ko'tariladi.
/// </para>
/// <para>
/// Egasining talabi (2026-09-29): ma'lumotnomadagi nomlar — hali vakansiya
/// ochilmaganlari ham — vakansiyalar qismiga o'tishi kerak.
/// </para>
/// </remarks>
public sealed class VacancyTitleMigrationTests
{
    /// <summary>Ko'chirishdan OLDINGI holat.</summary>
    private const string OldingiMigratsiya = "20260927105152_QoshimchaVazifaTugashi";

    /// <summary>Tekshirilayotgan migratsiya.</summary>
    private const string KochirishMigratsiyasi = "20260929061633_VakansiyaNomiVakansiyaningOzida";

    /// <summary>
    /// 20:30 UTC — Toshkentda ertasi kun (+5). Vakansiyasiz nomning sanasi
    /// aynan Toshkent kuni bo'yicha olinishini shu vaqt tekshiradi.
    /// </summary>
    private static readonly DateTimeOffset Yaratilgan = new(2026, 1, 15, 20, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Ochirilgan = new(2026, 2, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Har_bir_nom_vakansiyaga_otadi_vakansiyasizlari_YOPIQ_bolib()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        // Vakansiyasi bor nom: ochiq va yopiq vakansiya, izohlar birlashadi
        var sotuvchi = await NomQoshAsync(ulanish, "Сотувчи", faol: true, "Номнинг изоҳи", ochirilgan: false, bekorQilish);
        var ochiq = await VakansiyaQoshAsync(ulanish, sotuvchi, ochiq: true, "Вакансия изоҳи", ochirilgan: false, bekorQilish);
        var yopiq = await VakansiyaQoshAsync(ulanish, sotuvchi, ochiq: false, null, ochirilgan: false, bekorQilish);

        // Vakansiyasiz nomlar — faol va faolsizlantirilgan
        _ = await NomQoshAsync(ulanish, "Омборчи", faol: true, "Омбор учун", ochirilgan: false, bekorQilish);
        _ = await NomQoshAsync(ulanish, "Ҳайдовчи", faol: false, "   ", ochirilgan: false, bekorQilish);

        // O'chirilgan nom — ma'lumotnomada ko'rinmasdi, ko'chmaydi
        _ = await NomQoshAsync(ulanish, "Ўчирилган ном", faol: true, null, ochirilgan: true, bekorQilish);

        // Yagona vakansiyasi o'chirilgan nom — tirik vakansiya yo'q, ya'ni u ham ko'chadi
        var qorovul = await NomQoshAsync(ulanish, "Қоровул", faol: true, null, ochirilgan: false, bekorQilish);
        var ochirilganVakansiya = await VakansiyaQoshAsync(ulanish, qorovul, ochiq: false, null, ochirilgan: true, bekorQilish);

        await MigratsiyaAsync(baza.UlanishSatri, KochirishMigratsiyasi, bekorQilish);

        // ── Mavjud vakansiyalar: identifikator o'sha, nom va izoh ko'chgan ──
        var ochiqQator = await VakansiyaAsync(ulanish, ochiq, bekorQilish);
        ochiqQator.Nom.ShouldBe("Сотувчи");
        ochiqQator.Izoh.ShouldBe("Вакансия изоҳи\n\nНомнинг изоҳи", "ikkala izoh ham saqlanadi");
        ochiqQator.Holat.ShouldBe(1);

        (await VakansiyaAsync(ulanish, yopiq, bekorQilish)).Izoh
            .ShouldBe("Номнинг изоҳи", "vakansiyada izoh bo'lmasa — ma'lumotnomaniki olinadi");

        (await VakansiyaAsync(ulanish, ochirilganVakansiya, bekorQilish)).Nom
            .ShouldBe("Қоровул", "o'chirilgan vakansiyaga ham nom yoziladi — ustun majburiy");

        // ── Vakansiyasiz nomlar — YOPIQ vakansiya bo'lib ──
        var omborchi = await TirikVakansiyalarAsync(ulanish, "Омборчи", bekorQilish);
        var yangi = omborchi.ShouldHaveSingleItem("vakansiyasiz nom vakansiyalar bo'limiga o'tishi kerak");
        yangi.Holat.ShouldBe(3, "ochiq bo'lsa, bot uni darhol nomzodga ko'rsatardi");
        yangi.Ochilgan.ShouldBe(new DateOnly(2026, 1, 16), "Toshkent kuni — UTC bo'yicha hali 15-yanvar");
        yangi.Yopilgan.ShouldBe(new DateOnly(2026, 1, 16));
        yangi.Orinlar.ShouldBe(1);
        yangi.Izoh.ShouldBe("Омбор учун");

        var haydovchi = (await TirikVakansiyalarAsync(ulanish, "Ҳайдовчи", bekorQilish))
            .ShouldHaveSingleItem("faolsizlantirilgan nom ham ro'yxatda turardi — u ham o'tadi");
        haydovchi.Izoh.ShouldBeNull("faqat bo'shliqdan iborat izoh — izoh emas");

        (await TirikVakansiyalarAsync(ulanish, "Қоровул", bekorQilish))
            .ShouldHaveSingleItem("tirik vakansiyasi yo'q nom — yangi yopiq vakansiya");

        (await TirikVakansiyalarAsync(ulanish, "Ўчирилган ном", bekorQilish))
            .ShouldBeEmpty("o'chirilgan nom ma'lumotnomada ko'rinmasdi — ko'chmaydi");

        (await TirikVakansiyalarAsync(ulanish, "Сотувчи", bekorQilish))
            .Count.ShouldBe(2, "vakansiyasi bor nomga YANGI vakansiya qo'shilmaydi");

        // ── Eski tuzilma ketgan ──
        (await SanoqAsync(
            ulanish,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'vacancy_titles';",
            bekorQilish)).ShouldBe(0);

        (await SanoqAsync(
            ulanish,
            """
            SELECT count(*) FROM information_schema.columns
             WHERE table_name = 'vacancies' AND column_name IN ('vacancy_title_id', 'responsible_employee_id');
            """,
            bekorQilish)).ShouldBe(0);
    }

    /// <summary>
    /// Qo'shilgan izoh 2000 belgiga sig'masa — migratsiya KESMAYDI, TO'XTAYDI
    /// va hech narsa o'zgarmaydi.
    /// </summary>
    [Fact]
    public async Task Sigmaydigan_izoh_migratsiyani_toxtatadi_va_hech_narsa_ozgarmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        var nom = await NomQoshAsync(ulanish, "Узун", faol: true, new string('б', 1500), ochirilgan: false, bekorQilish);
        _ = await VakansiyaQoshAsync(ulanish, nom, ochiq: true, new string('в', 1000), ochirilgan: false, bekorQilish);

        // O'chirilgan va vakansiyasiz nomning uzun izohi hech qayerga
        // ko'chmaydi — u migratsiyani to'xtatmasligi kerak (shu sababli
        // yuqoridagi nom bilan birga faqat BITTA sabab qoladi).
        _ = await NomQoshAsync(ulanish, "Ўчирилган узун", faol: true, new string('г', 2500), ochirilgan: true, bekorQilish);

        var xato = await Should.ThrowAsync<PostgresException>(
            () => MigratsiyaAsync(baza.UlanishSatri, KochirishMigratsiyasi, bekorQilish));

        xato.SqlState.ShouldBe("P0001", "migratsiyaning o'z RAISE EXCEPTION i");
        xato.MessageText.ShouldContain("(1 ta yozuv");

        (await SanoqAsync(
            ulanish,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'vacancy_titles';",
            bekorQilish)).ShouldBe(1, "tranzaksiya qaytadi — ma'lumotnoma joyida");

        (await SanoqAsync(
            ulanish,
            "SELECT count(*) FROM information_schema.columns WHERE table_name = 'vacancies' AND column_name = 'title';",
            bekorQilish)).ShouldBe(0, "yarim yo'lda qolgan sxema yo'q");
    }

    /// <summary>
    /// <c>Down</c> ma'lumotnomani nomlardan qayta yig'adi; keyingi <c>Up</c>
    /// yangi vakansiya QO'SHMAYDI.
    /// </summary>
    [Fact]
    public async Task Down_va_qayta_Up_vakansiyalar_sonini_ozgartirmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        var sotuvchi = await NomQoshAsync(ulanish, "Сотувчи", faol: true, null, ochirilgan: false, bekorQilish);
        _ = await VakansiyaQoshAsync(ulanish, sotuvchi, ochiq: true, null, ochirilgan: false, bekorQilish);
        _ = await NomQoshAsync(ulanish, "Омборчи", faol: true, null, ochirilgan: false, bekorQilish);

        await MigratsiyaAsync(baza.UlanishSatri, KochirishMigratsiyasi, bekorQilish);
        var keyin = await SanoqAsync(ulanish, "SELECT count(*) FROM vacancies;", bekorQilish);
        keyin.ShouldBe(2);

        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        (await SanoqAsync(ulanish, "SELECT count(*) FROM vacancy_titles;", bekorQilish))
            .ShouldBe(2, "har bir noyob nom — bitta yozuv");
        (await SanoqAsync(ulanish, "SELECT count(*) FROM vacancies WHERE vacancy_title_id IS NULL;", bekorQilish))
            .ShouldBe(0);

        await MigratsiyaAsync(baza.UlanishSatri, KochirishMigratsiyasi, bekorQilish);

        (await SanoqAsync(ulanish, "SELECT count(*) FROM vacancies;", bekorQilish))
            .ShouldBe(keyin, "nomlarning hammasida tirik vakansiya bor — qo'shimcha qator yo'q");
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    private sealed record VakansiyaQatori(
        string Nom,
        int Holat,
        DateOnly Ochilgan,
        DateOnly? Yopilgan,
        int Orinlar,
        string? Izoh);

    /// <summary>Bazani ko'rsatilgan migratsiyagacha ko'taradi yoki tushiradi.</summary>
    private static async Task MigratsiyaAsync(string ulanishSatri, string nishon, CancellationToken bekorQilish)
    {
        var sozlamalar = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ulanishSatri)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var kontekst = new AppDbContext(sozlamalar);
        await kontekst.GetService<IMigrator>().MigrateAsync(nishon, bekorQilish);
    }

    /// <summary>Migratsiyadan OLDINGI sxemadagi vakansiya nomi — xom SQL bilan.</summary>
    private static async Task<Guid> NomQoshAsync(
        NpgsqlConnection ulanish,
        string nom,
        bool faol,
        string? izoh,
        bool ochirilgan,
        CancellationToken bekorQilish)
    {
        var id = Guid.NewGuid();

        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = """
            INSERT INTO vacancy_titles (id, name, is_active, note, created_at, deleted_at)
            VALUES ($1, $2, $3, $4, $5, $6);
            """;
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = id });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = nom });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = faol });
        _ = buyruq.Parameters.Add(Ixtiyoriy(NpgsqlDbType.Text, izoh));
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = Yaratilgan });
        _ = buyruq.Parameters.Add(Ixtiyoriy(NpgsqlDbType.TimestampTz, ochirilgan ? Ochirilgan : null));

        _ = await buyruq.ExecuteNonQueryAsync(bekorQilish);
        return id;
    }

    /// <summary>Migratsiyadan OLDINGI sxemadagi vakansiya — xom SQL bilan.</summary>
    private static async Task<Guid> VakansiyaQoshAsync(
        NpgsqlConnection ulanish,
        Guid nomId,
        bool ochiq,
        string? izoh,
        bool ochirilgan,
        CancellationToken bekorQilish)
    {
        var id = Guid.NewGuid();

        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = """
            INSERT INTO vacancies
                (id, vacancy_title_id, openings, opened_at, closed_at, status, note, created_at, deleted_at)
            VALUES ($1, $2, 2, DATE '2026-03-01', $3, $4, $5, $6, $7);
            """;
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = id });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = nomId });
        _ = buyruq.Parameters.Add(Ixtiyoriy(NpgsqlDbType.Date, ochiq ? null : new DateOnly(2026, 4, 1)));
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = ochiq ? 1 : 3 });
        _ = buyruq.Parameters.Add(Ixtiyoriy(NpgsqlDbType.Varchar, izoh));
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = Yaratilgan });
        _ = buyruq.Parameters.Add(Ixtiyoriy(NpgsqlDbType.TimestampTz, ochirilgan ? Ochirilgan : null));

        _ = await buyruq.ExecuteNonQueryAsync(bekorQilish);
        return id;
    }

    private static async Task<VakansiyaQatori> VakansiyaAsync(
        NpgsqlConnection ulanish,
        Guid id,
        CancellationToken bekorQilish)
    {
        var qatorlar = await QatorlarAsync(ulanish, "WHERE id = $1", id, bekorQilish);
        return qatorlar.ShouldHaveSingleItem("vakansiya o'z identifikatori bilan qolishi kerak");
    }

    private static Task<List<VakansiyaQatori>> TirikVakansiyalarAsync(
        NpgsqlConnection ulanish,
        string nom,
        CancellationToken bekorQilish)
        => QatorlarAsync(ulanish, "WHERE title = $1 AND deleted_at IS NULL", nom, bekorQilish);

    private static async Task<List<VakansiyaQatori>> QatorlarAsync(
        NpgsqlConnection ulanish,
        string shart,
        object qiymat,
        CancellationToken bekorQilish)
    {
        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = $"SELECT title, status, opened_at, closed_at, openings, note FROM vacancies {shart};";
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = qiymat });

        var qatorlar = new List<VakansiyaQatori>();

        await using var oquvchi = await buyruq.ExecuteReaderAsync(bekorQilish);
        while (await oquvchi.ReadAsync(bekorQilish))
        {
            qatorlar.Add(new VakansiyaQatori(
                oquvchi.GetString(0),
                oquvchi.GetInt32(1),
                oquvchi.GetFieldValue<DateOnly>(2),
                await oquvchi.IsDBNullAsync(3, bekorQilish) ? null : oquvchi.GetFieldValue<DateOnly>(3),
                oquvchi.GetInt32(4),
                await oquvchi.IsDBNullAsync(5, bekorQilish) ? null : oquvchi.GetString(5)));
        }

        return qatorlar;
    }

    private static async Task<long> SanoqAsync(NpgsqlConnection ulanish, string sql, CancellationToken bekorQilish)
    {
        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = sql;
        return (long)(await buyruq.ExecuteScalarAsync(bekorQilish))!;
    }

    /// <summary>NULL bo'lishi mumkin bo'lgan parametr — turi OSHKORA ko'rsatiladi.</summary>
    private static NpgsqlParameter Ixtiyoriy(NpgsqlDbType tur, object? qiymat)
        => new() { NpgsqlDbType = tur, Value = qiymat ?? DBNull.Value };

    /// <summary>
    /// Faqat shu test uchun yaratilgan va tugagach o'chiriladigan baza —
    /// <see cref="CandidateFileMigrationTests"/> dagi bilan aynan bir xil tartib.
    /// </summary>
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
            // ⚠️ Xavfsizlik to'ri: DROP faqat SHU testlar yaratgan nomga tushishi mumkin
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
