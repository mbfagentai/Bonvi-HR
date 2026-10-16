using System.Globalization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <c>JarimaFoizVaBuyruqsiz</c> migratsiyasi: jarimada foiz, jarimaga buyruq yo'q.
/// </summary>
/// <remarks>
/// <para>
/// Migratsiya mavjud qatorlarga tegadi — jarimadagi buyruq ma'lumotini
/// cheklovdan OLDIN tozalaydi. Shu sababli baza avval oldingi migratsiyagacha
/// ko'tariladi, xom SQL bilan «eski» qatorlar yoziladi, keyin yuqoriga
/// ko'tariladi (<see cref="WorkSiteShapesMigrationTests"/> dagi yo'l).
/// </para>
/// <para>
/// Xodim qatori yozilmaydi: <c>employees</c> jadvalida o'nlab majburiy ustun
/// bor va ular bu migratsiyaga aloqasiz. Tashqi kalit shu sessiyada
/// <c>session_replication_role = replica</c> bilan o'chiriladi.
/// </para>
/// </remarks>
public sealed class JarimaFoizMigrationTests
{
    /// <summary>Foizdan OLDINGI holat.</summary>
    private const string OldingiMigratsiya = "20261014063834_IsmQidiruvKaliti";

    /// <summary>Tekshirilayotgan migratsiya.</summary>
    private const string FoizMigratsiyasi = "20261016053617_JarimaFoizVaBuyruqsiz";

    /// <summary>Rag'batdan ham buyruq olib tashlangan holat — ustunlar yo'q.</summary>
    private const string BuyruqsizMigratsiya = "20261016092521_RagbatJarimaBuyruqsiz";

    private const int Ragbat = 1;
    private const int Jarima = 2;

    private static readonly DateOnly Sana = new(2026, 9, 1);

    [Fact]
    public async Task Up_jarimadagi_buyruqni_tozalaydi_ragbatnikiga_tegmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = await UlanAsync(baza.UlanishSatri, bekorQilish);

        (await UstunBormiAsync(ulanish, "percent", bekorQilish))
            .ShouldBeFalse("test migratsiyadan OLDINGI holatda turishi kerak");

        var ragbat = await YozuvQoshAsync(ulanish, Ragbat, 500_000m, "12-b", Sana, null, bekorQilish);
        var buyruqliJarima = await YozuvQoshAsync(ulanish, Jarima, 300_000m, "45-b", Sana, "Kechikish", bekorQilish);
        var sanaliJarima = await YozuvQoshAsync(ulanish, Jarima, null, null, Sana, "Kechikish", bekorQilish);
        var oddiyJarima = await YozuvQoshAsync(ulanish, Jarima, null, null, null, "Hayfsan", bekorQilish);

        // ── Migratsiya ───────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, FoizMigratsiyasi, bekorQilish);

        (await UstunBormiAsync(ulanish, "percent", bekorQilish)).ShouldBeTrue();

        (await QatorAsync(ulanish, ragbat, bekorQilish))
            .ShouldBe("1|500000|12-b|2026-09-01||", "rag'batning buyrug'i saqlanadi");
        (await QatorAsync(ulanish, buyruqliJarima, bekorQilish))
            .ShouldBe("2|300000|||Kechikish|", "jarimaning buyrug'i tozalanadi, summa va sabab qoladi");
        (await QatorAsync(ulanish, sanaliJarima, bekorQilish))
            .ShouldBe("2||||Kechikish|", "faqat sanasi bor buyruq ham tozalanadi");
        (await QatorAsync(ulanish, oddiyJarima, bekorQilish))
            .ShouldBe("2||||Hayfsan|");
    }

    [Fact]
    public async Task Up_dan_keyin_baza_cheklovlari_qoidani_ushlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, FoizMigratsiyasi, bekorQilish);

        await using var ulanish = await UlanAsync(baza.UlanishSatri, bekorQilish);

        var ragbat = await YozuvQoshAsync(ulanish, Ragbat, 500_000m, "12-b", Sana, null, bekorQilish);
        var jarima = await YozuvQoshAsync(ulanish, Jarima, 300_000m, null, null, "Kechikish", bekorQilish);

        // To'g'ri o'tish: summadan foizga.
        await BajarAsync(ulanish, "UPDATE incentives SET amount = NULL, percent = 4.5 WHERE id = $1;", bekorQilish, jarima);
        (await QatorAsync(ulanish, jarima, bekorQilish)).ShouldBe("2||||Kechikish|4.50");

        await CheklovBuzadiAsync(
            ulanish, "UPDATE incentives SET amount = 300000 WHERE id = $1;", "ck_incentives_amount_or_percent", jarima);
        await CheklovBuzadiAsync(
            ulanish, "UPDATE incentives SET order_no = '45-b' WHERE id = $1;", "ck_incentives_penalty_no_order", jarima);
        await CheklovBuzadiAsync(
            ulanish, "UPDATE incentives SET order_date = '2026-09-01' WHERE id = $1;", "ck_incentives_penalty_no_order", jarima);
        await CheklovBuzadiAsync(
            ulanish, "UPDATE incentives SET percent = 0 WHERE id = $1;", "ck_incentives_percent_range", jarima);
        await CheklovBuzadiAsync(
            ulanish, "UPDATE incentives SET percent = 100.01 WHERE id = $1;", "ck_incentives_percent_range", jarima);
        await CheklovBuzadiAsync(
            ulanish,
            "UPDATE incentives SET amount = NULL, percent = 10 WHERE id = $1;",
            "ck_incentives_percent_penalty_only",
            ragbat);

        // Chegaradagi qiymat — 100 — o'tadi.
        await BajarAsync(ulanish, "UPDATE incentives SET percent = 100 WHERE id = $1;", bekorQilish, jarima);
    }

    [Fact]
    public async Task Down_foiz_ustunini_va_cheklovlarni_olib_tashlaydi_qatorlar_qoladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, FoizMigratsiyasi, bekorQilish);

        await using var ulanish = await UlanAsync(baza.UlanishSatri, bekorQilish);

        var ragbat = await YozuvQoshAsync(ulanish, Ragbat, 500_000m, "12-b", Sana, null, bekorQilish);
        var jarima = await YozuvQoshAsync(ulanish, Jarima, null, null, null, "Kechikish", bekorQilish);
        await BajarAsync(ulanish, "UPDATE incentives SET percent = 4 WHERE id = $1;", bekorQilish, jarima);

        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        (await UstunBormiAsync(ulanish, "percent", bekorQilish)).ShouldBeFalse();
        (await SanoqAsync(
                ulanish,
                """
                SELECT count(*) FROM pg_constraint
                 WHERE conrelid = 'incentives'::regclass AND conname LIKE 'ck_incentives_%';
                """,
                bekorQilish))
            .ShouldBe(0);
        (await SanoqAsync(ulanish, "SELECT count(*) FROM incentives WHERE id IN ($1, $2);", bekorQilish, ragbat, jarima))
            .ShouldBe(2);
    }

    /// <summary>
    /// <c>RagbatJarimaBuyruqsiz</c>: buyruq ustunlari va ularning cheklovi yo'qoladi, yozuvlarning
    /// o'zi — summa, foiz, sabab — joyida qoladi. <c>Down</c> ustunlarni bo'sh qaytaradi.
    /// </summary>
    [Fact]
    public async Task Buyruqsiz_Up_ustunlarni_olib_tashlaydi_yozuvlar_qoladi_Down_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, FoizMigratsiyasi, bekorQilish);

        await using var ulanish = await UlanAsync(baza.UlanishSatri, bekorQilish);

        var ragbat = await YozuvQoshAsync(ulanish, Ragbat, 500_000m, "12-b", Sana, null, bekorQilish);
        var jarima = await YozuvQoshAsync(ulanish, Jarima, null, null, null, "Kechikish", bekorQilish);
        await BajarAsync(ulanish, "UPDATE incentives SET percent = 4 WHERE id = $1;", bekorQilish, jarima);

        // ── Up ───────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, BuyruqsizMigratsiya, bekorQilish);

        (await UstunBormiAsync(ulanish, "order_no", bekorQilish)).ShouldBeFalse();
        (await UstunBormiAsync(ulanish, "order_date", bekorQilish)).ShouldBeFalse();
        (await CheklovlarAsync(ulanish, bekorQilish)).ShouldBe(
            "ck_incentives_amount_or_percent,ck_incentives_percent_penalty_only,ck_incentives_percent_range");

        (await QiymatAsync(
                ulanish,
                "SELECT kind || '|' || coalesce(trunc(amount)::text, '') || '|' || coalesce(percent::text, '') || '|' || coalesce(reason, '') FROM incentives WHERE id = $1;",
                bekorQilish,
                ragbat))
            .ShouldBe("1|500000||");
        (await QiymatAsync(
                ulanish,
                "SELECT kind || '|' || coalesce(trunc(amount)::text, '') || '|' || coalesce(percent::text, '') || '|' || coalesce(reason, '') FROM incentives WHERE id = $1;",
                bekorQilish,
                jarima))
            .ShouldBe("2||4.00|Kechikish");

        // ── Down ─────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, FoizMigratsiyasi, bekorQilish);

        (await UstunBormiAsync(ulanish, "order_no", bekorQilish)).ShouldBeTrue();
        (await SanoqAsync(
                ulanish,
                "SELECT count(*) FROM incentives WHERE id IN ($1, $2) AND order_no IS NULL AND order_date IS NULL;",
                bekorQilish,
                ragbat,
                jarima))
            .ShouldBe(2, "ustunlar bo'sh qaytadi — o'chirilgan qiymat tiklanmaydi");
        (await CheklovlarAsync(ulanish, bekorQilish)).ShouldContain("ck_incentives_penalty_no_order");
    }

    private static async Task<string> CheklovlarAsync(NpgsqlConnection ulanish, CancellationToken bekorQilish)
        => (string?)await QiymatAsync(
               ulanish,
               """
               SELECT string_agg(conname, ',' ORDER BY conname) FROM pg_constraint
                WHERE conrelid = 'incentives'::regclass AND contype = 'c';
               """,
               bekorQilish) ?? string.Empty;

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    private static async Task MigratsiyaAsync(string ulanishSatri, string nishon, CancellationToken bekorQilish)
    {
        await using var kontekst = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ulanishSatri)
            .UseSnakeCaseNamingConvention()
            .Options);

        await kontekst.GetService<IMigrator>().MigrateAsync(nishon, bekorQilish);
    }

    private static async Task<NpgsqlConnection> UlanAsync(string ulanishSatri, CancellationToken bekorQilish)
    {
        var ulanish = new NpgsqlConnection(ulanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        // Xodim qatorisiz yozish uchun — tashqi kalit triggerlari shu sessiyada ishlamaydi.
        await BajarAsync(ulanish, "SET session_replication_role = replica;", bekorQilish);

        return ulanish;
    }

    /// <summary>Migratsiyadan OLDINGI ustunlar bilan qator — <c>percent</c> yozilmaydi.</summary>
    private static async Task<Guid> YozuvQoshAsync(
        NpgsqlConnection ulanish,
        int tur,
        decimal? summa,
        string? buyruqRaqami,
        DateOnly? buyruqSanasi,
        string? sabab,
        CancellationToken bekorQilish)
    {
        var id = Guid.NewGuid();

        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText =
            """
            INSERT INTO incentives (id, employee_id, kind, type_id, date, amount, amount_currency,
                                    reason, order_no, order_date, created_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, now());
            """;
        buyruq.Parameters.Add(new NpgsqlParameter { Value = id });
        buyruq.Parameters.Add(new NpgsqlParameter { Value = Guid.NewGuid() });
        buyruq.Parameters.Add(new NpgsqlParameter { Value = tur });
        buyruq.Parameters.Add(new NpgsqlParameter { Value = Guid.NewGuid() });
        buyruq.Parameters.Add(new NpgsqlParameter { Value = Sana });
        buyruq.Parameters.Add(new NpgsqlParameter { Value = summa is { } pul ? pul : DBNull.Value });
        buyruq.Parameters.Add(new NpgsqlParameter { Value = summa is null ? DBNull.Value : "UZS" });
        buyruq.Parameters.Add(new NpgsqlParameter { Value = (object?)sabab ?? DBNull.Value });
        buyruq.Parameters.Add(new NpgsqlParameter { Value = (object?)buyruqRaqami ?? DBNull.Value });
        buyruq.Parameters.Add(new NpgsqlParameter { Value = buyruqSanasi is { } kun ? kun : DBNull.Value });

        _ = await buyruq.ExecuteNonQueryAsync(bekorQilish);
        return id;
    }

    /// <summary>
    /// Qatorning shu migratsiyaga aloqador ustunlari bitta satrda:
    /// <c>kind|amount|order_no|order_date|reason|percent</c> (ustun hali bo'lmasa — bo'sh).
    /// </summary>
    private static async Task<string?> QatorAsync(NpgsqlConnection ulanish, Guid id, CancellationToken bekorQilish)
    {
        var foiz = await UstunBormiAsync(ulanish, "percent", bekorQilish) ? "coalesce(percent::text, '')" : "''";

        return (string?)await QiymatAsync(
            ulanish,
            $"""
            SELECT kind || '|' || coalesce(trunc(amount)::text, '') || '|' || coalesce(order_no, '') || '|'
                   || coalesce(to_char(order_date, 'YYYY-MM-DD'), '') || '|' || coalesce(reason, '') || '|' || {foiz}
              FROM incentives WHERE id = $1;
            """,
            bekorQilish,
            id);
    }

    /// <summary>SQL bazadagi aynan shu cheklovni buzgani uchun rad etilishini tekshiradi.</summary>
    private static async Task CheklovBuzadiAsync(NpgsqlConnection ulanish, string sql, string cheklov, Guid id)
    {
        var xato = await Should.ThrowAsync<PostgresException>(
            () => BajarAsync(ulanish, sql, TestContext.Current.CancellationToken, id));

        xato.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation, sql);
        xato.ConstraintName.ShouldBe(cheklov, sql);
    }

    private static async Task<bool> UstunBormiAsync(NpgsqlConnection ulanish, string ustun, CancellationToken bekorQilish)
        => await SanoqAsync(
            ulanish,
            """
            SELECT count(*) FROM information_schema.columns
             WHERE table_schema = 'public' AND table_name = 'incentives' AND column_name = $1;
            """,
            bekorQilish,
            ustun) > 0;

    private static async Task<long> SanoqAsync(
        NpgsqlConnection ulanish,
        string sql,
        CancellationToken bekorQilish,
        params object[] qiymatlar)
        => (long)(await QiymatAsync(ulanish, sql, bekorQilish, qiymatlar))!;

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
    /// Faqat shu test uchun yaratilgan va tugagach o'chiriladigan baza.
    /// </summary>
    /// <remarks>
    /// <see cref="WorkSiteShapesMigrationTests"/> dagi yordamchining nusxasi — u yerda
    /// <c>private</c>. Prefiks va xavfsizlik to'ri aynan bir xil.
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
            // tushishi mumkin. `bonvi_hr` — ishlab chiqish bazasi.
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
