using System.Globalization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <c>WorkSiteShapes</c> migratsiyasining MA'LUMOT ustidagi testi (SPEC-joylashuvlar §A, §F):
/// mavjud doira obyektlari va belgilar buzilmaydimi, <c>Down</c> qachon ishlaydi va qachon to'xtaydi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <see cref="BonviApiFactory"/> yetarli emas</b> — sababi
/// <see cref="CandidateFileMigrationTests"/> da: u BO'SH bazaga barcha
/// migratsiyalarni qo'llaydi, bo'sh jadvalda esa «mavjud qatorlar
/// <c>'circle'</c> bo'lib qoladi» degan da'vo hech narsani tekshirmaydi.
/// Shu sababli baza avval oldingi migratsiyagacha ko'tariladi, xom SQL bilan
/// «eski» obyektlar va belgi yoziladi, keyin yuqoriga ko'tariladi.
/// </para>
/// <para>
/// Nishonlar NOMI bilan ko'rsatilgan (<c>null</c> = «eng oxirgisi» emas) —
/// keyinroq qo'shiladigan migratsiya «bir qadam pastga» ma'nosini
/// o'zgartirib, testni jimgina buzmasin.
/// </para>
/// </remarks>
public sealed class WorkSiteShapesMigrationTests
{
    /// <summary>Shakllardan OLDINGI holat.</summary>
    private const string OldingiMigratsiya = "20260922124712_SeedMobileAppDevice";

    /// <summary>Tekshirilayotgan migratsiya.</summary>
    private const string ShakllarMigratsiyasi = "20260923050201_WorkSiteShapes";

    /// <summary><c>AttendanceDevice.MobileAppId</c> — <c>SeedMobileAppDevice</c> yaratgan qator.</summary>
    private static readonly Guid MobilIlova = Guid.Parse("01996f00-0000-7000-8000-00000000b071");

    private static readonly DateTimeOffset Yaratilgan = new(2026, 9, 22, 7, 0, 0, TimeSpan.Zero);

    // ────────────────────────────────────────────────────────────
    //  1. Up — mavjud doiralar va belgilar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Mavjud doiralar <c>shape = 'circle'</c>, <c>edge_buffer_meters = 50</c> oladi, qolgan har bir
    /// ustuni o'zgarmaydi; mavjud belgi <c>site_shape = 'circle'</c> oladi. Sukutlar qolmaydi.
    /// </summary>
    [Fact]
    public async Task Up_mavjud_doira_obyektlari_va_belgilarini_buzmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        // Test o'z sozlamasini isbotlaydi: yangi ustun SHU nuqtada yo'q.
        (await UstunBormiAsync(ulanish, "work_sites", "shape", bekorQilish))
            .ShouldBeFalse("test migratsiyadan OLDINGI holatda turishi kerak");

        var faol = Guid.NewGuid();
        var faolsiz = Guid.NewGuid();
        await ObyektQoshAsync(ulanish, faol, "Zavod", 41.311081m, 69.279737m, 200, true, null, bekorQilish);
        await ObyektQoshAsync(ulanish, faolsiz, "Mavsumiy ombor", 40.1m, 65.3m, 5000, false, "Qishda yopiq", bekorQilish);

        var belgi = await BelgiQoshAsync(ulanish, faol, masofa: 87.4m, radius: 200, bekorQilish);

        var oldin = await ObyektlarAsync(ulanish, bekorQilish);

        // ── Migratsiya ───────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, ShakllarMigratsiyasi, bekorQilish);

        // Eski ustunlar AYNAN o'sha — hech narsa qayta yozilmagan.
        (await ObyektlarAsync(ulanish, bekorQilish)).ShouldBe(oldin);

        foreach (var id in new[] { faol, faolsiz })
        {
            (await MatnAsync(ulanish, "SELECT shape FROM work_sites WHERE id = $1;", bekorQilish, id))
                .ShouldBe("circle");
            (await SonAsync(ulanish, "SELECT edge_buffer_meters FROM work_sites WHERE id = $1;", bekorQilish, id))
                .ShouldBe(50);
            (await SanoqAsync(
                    ulanish,
                    "SELECT count(*) FROM work_sites WHERE id = $1 AND polygon IS NULL AND address IS NULL;",
                    bekorQilish,
                    id))
                .ShouldBe(1);
        }

        (await MatnAsync(ulanish, "SELECT site_shape FROM mobile_check_ins WHERE id = $1;", bekorQilish, belgi))
            .ShouldBe("circle");
        (await MatnAsync(
                ulanish,
                "SELECT distance_meters::text || '/' || site_radius_meters FROM mobile_check_ins WHERE id = $1;",
                bekorQilish,
                belgi))
            .ShouldBe("87.4/200", "dalil qatorining sonlari tegilmaydi");

        // Sukutlar qatorlarni to'ldirgach olib tashlangan (migratsiya izohi).
        foreach (var (jadval, ustun) in new[]
                 {
                     ("work_sites", "shape"),
                     ("work_sites", "edge_buffer_meters"),
                     ("mobile_check_ins", "site_shape"),
                 })
        {
            (await SanoqAsync(
                    ulanish,
                    """
                    SELECT count(*) FROM information_schema.columns
                     WHERE table_schema = 'public' AND table_name = $1 AND column_name = $2
                       AND is_nullable = 'NO' AND column_default IS NULL;
                    """,
                    bekorQilish,
                    jadval,
                    ustun))
                .ShouldBe(1, $"{jadval}.{ustun} — NOT NULL va sukutsiz bo'lishi kerak");
        }

        await using var kontekst = Kontekst(baza.UlanishSatri);
        kontekst.Database.HasPendingModelChanges().ShouldBeFalse("model surati kod bilan bir xil bo'lishi kerak");
    }

    /// <summary>Yangi cheklovlar bazada ishlaydi — domenni chetlab yozilgan SQL'ni ham ushlaydi.</summary>
    [Fact]
    public async Task Yangi_cheklovlar_notogri_shaklni_bazada_ushlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        var id = Guid.NewGuid();
        await ObyektQoshAsync(ulanish, id, "Sinov", 41.3m, 69.28m, 200, true, null, bekorQilish);
        var belgi = await BelgiQoshAsync(ulanish, id, masofa: 0m, radius: 200, bekorQilish);

        await MigratsiyaAsync(baza.UlanishSatri, ShakllarMigratsiyasi, bekorQilish);

        // Uchsiz «ko'pburchak», uchli «doira», noma'lum shakl, zaxira oralig'i.
        await CheklovBuzadiAsync(ulanish, "UPDATE work_sites SET shape = 'polygon' WHERE id = $1;", "ck_work_sites_polygon", id);
        await CheklovBuzadiAsync(ulanish, """UPDATE work_sites SET polygon = '[{"lat":1,"lng":1}]' WHERE id = $1;""", "ck_work_sites_polygon", id);
        await CheklovBuzadiAsync(ulanish, "UPDATE work_sites SET shape = 'square' WHERE id = $1;", "ck_work_sites_shape", id);
        await CheklovBuzadiAsync(ulanish, "UPDATE work_sites SET edge_buffer_meters = 501 WHERE id = $1;", "ck_work_sites_edge_buffer", id);

        // Ko'pburchak belgisida ruxsat — zaxira (0 mumkin), doirada — radius (50 dan kam emas).
        await BajarAsync(
            ulanish,
            "UPDATE mobile_check_ins SET site_shape = 'polygon', site_radius_meters = 0 WHERE id = $1;",
            bekorQilish,
            belgi);
        await CheklovBuzadiAsync(
            ulanish,
            "UPDATE mobile_check_ins SET site_shape = 'circle' WHERE id = $1;",
            "ck_mobile_check_ins_radius",
            belgi);
        await CheklovBuzadiAsync(
            ulanish,
            "UPDATE mobile_check_ins SET site_radius_meters = 501 WHERE id = $1;",
            "ck_mobile_check_ins_radius",
            belgi);
    }

    // ────────────────────────────────────────────────────────────
    //  2. Down
    // ────────────────────────────────────────────────────────────

    /// <summary>Faqat doiralar bor bazada <c>Down</c> ishlaydi, keyin <c>Up</c> yana ishlaydi.</summary>
    [Fact]
    public async Task Doiralargina_bor_bazada_Down_va_Up_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        var id = Guid.NewGuid();
        await ObyektQoshAsync(ulanish, id, "Zavod", 41.311081m, 69.279737m, 300, true, null, bekorQilish);
        await BelgiQoshAsync(ulanish, id, masofa: 12.5m, radius: 300, bekorQilish);
        var oldin = await ObyektlarAsync(ulanish, bekorQilish);

        await MigratsiyaAsync(baza.UlanishSatri, ShakllarMigratsiyasi, bekorQilish);

        // ── Down ─────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        (await UstunBormiAsync(ulanish, "work_sites", "shape", bekorQilish)).ShouldBeFalse();
        (await UstunBormiAsync(ulanish, "mobile_check_ins", "site_shape", bekorQilish)).ShouldBeFalse();
        (await ObyektlarAsync(ulanish, bekorQilish)).ShouldBe(oldin);
        (await SanoqAsync(ulanish, "SELECT count(*) FROM mobile_check_ins;", bekorQilish)).ShouldBe(1);

        // ── Up ───────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, ShakllarMigratsiyasi, bekorQilish);

        (await ObyektlarAsync(ulanish, bekorQilish)).ShouldBe(oldin);
        (await MatnAsync(ulanish, "SELECT shape FROM work_sites WHERE id = $1;", bekorQilish, id)).ShouldBe("circle");
    }

    /// <summary>
    /// Ko'pburchak bor bazada <c>Down</c> hech narsani o'zgartirmasdan to'xtaydi — aks holda ko'pburchak
    /// jimgina «markazdan 200 m» doiraga aylanardi.
    /// </summary>
    [Fact]
    public async Task Kopburchak_bor_bazada_Down_toxtaydi_va_hech_narsa_ozgarmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        var id = Guid.NewGuid();
        await ObyektQoshAsync(ulanish, id, "Ko'pburchak", 41.3m, 69.28m, 200, true, null, bekorQilish);

        await MigratsiyaAsync(baza.UlanishSatri, ShakllarMigratsiyasi, bekorQilish);

        await BajarAsync(
            ulanish,
            """
            UPDATE work_sites
               SET shape = 'polygon',
                   polygon = '[{"lat":41.299,"lng":69.2787},{"lat":41.299,"lng":69.2813},{"lat":41.301,"lng":69.28}]'
             WHERE id = $1;
            """,
            bekorQilish,
            id);

        var xato = await Should.ThrowAsync<PostgresException>(
            () => MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish));

        xato.MessageText.ShouldContain("WorkSiteShapes");

        (await UstunBormiAsync(ulanish, "work_sites", "shape", bekorQilish))
            .ShouldBeTrue("qaytarish tranzaksiyasi to'liq bekor bo'lishi kerak");
        (await MatnAsync(ulanish, "SELECT shape FROM work_sites WHERE id = $1;", bekorQilish, id)).ShouldBe("polygon");
        (await SanoqAsync(
                ulanish,
                """SELECT count(*) FROM "__EFMigrationsHistory" WHERE migration_id = $1;""",
                bekorQilish,
                ShakllarMigratsiyasi))
            .ShouldBe(1);
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    /// <summary>Bazani ko'rsatilgan migratsiyagacha ko'taradi yoki tushiradi (<see cref="IMigrator"/>).</summary>
    private static async Task MigratsiyaAsync(string ulanishSatri, string nishon, CancellationToken bekorQilish)
    {
        await using var kontekst = Kontekst(ulanishSatri);
        await kontekst.GetService<IMigrator>().MigrateAsync(nishon, bekorQilish);
    }

    private static AppDbContext Kontekst(string ulanishSatri)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ulanishSatri)
            .UseSnakeCaseNamingConvention()
            .Options);

    /// <summary>
    /// Obyekt qatori — migratsiyadan OLDINGI ustunlar bilan. Shu sababli har bir test qatorlarni
    /// <see cref="OldingiMigratsiya"/> da yozadi: yangi ustunlar sukutsiz <c>NOT NULL</c>.
    /// </summary>
    private static Task ObyektQoshAsync(
        NpgsqlConnection ulanish,
        Guid id,
        string nom,
        decimal kenglik,
        decimal uzunlik,
        int radius,
        bool faol,
        string? izoh,
        CancellationToken bekorQilish)
    {
        var buyruq = Buyruq(
            ulanish,
            """
            INSERT INTO work_sites (id, name, latitude, longitude, radius_meters, is_active, note, created_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8);
            """,
            id,
            nom,
            kenglik,
            uzunlik,
            radius,
            faol,
            (object?)izoh ?? DBNull.Value,
            Yaratilgan);

        return BajarVaYopAsync(buyruq, bekorQilish);
    }

    /// <summary>Qabul qilingan telefon belgisi: xodim → «Mobil ilova» o'tishi → koordinata qatori.</summary>
    private static async Task<Guid> BelgiQoshAsync(
        NpgsqlConnection ulanish,
        Guid obyekt,
        decimal masofa,
        int radius,
        CancellationToken bekorQilish)
    {
        var xodim = Guid.NewGuid();
        var otish = Guid.NewGuid();
        var belgi = Guid.NewGuid();

        await BajarAsync(
            ulanish,
            """
            INSERT INTO employees (id, first_name, last_name, direction_ids, needs_review, mobile_check_in_allowed, created_at)
            VALUES ($1, 'Ilova', 'Qodirov', '{}', false, true, $2);
            """,
            bekorQilish,
            xodim,
            Yaratilgan);

        await BajarAsync(
            ulanish,
            """
            INSERT INTO attendance_passes (
                id, device_id, identity_provider, raw_person_id, occurred_at, device_local_time,
                device_offset_minutes, offset_assumed, local_date, received_at, clock_skew_seconds,
                direction, direction_source, method, dedupe_key, resolved_employee_id)
            VALUES ($1, $2, 5, $3, $4, $5, 300, false, $6, $4, 0, 1, 4, 7, $7, $8);
            """,
            bekorQilish,
            otish,
            MobilIlova,
            xodim.ToString(),
            Yaratilgan,
            Yaratilgan.ToOffset(TimeSpan.FromHours(5)).DateTime,
            DateOnly.FromDateTime(Yaratilgan.ToOffset(TimeSpan.FromHours(5)).DateTime),
            "t:" + xodim,
            xodim);

        await BajarAsync(
            ulanish,
            """
            INSERT INTO mobile_check_ins (
                id, pass_id, employee_id, work_site_id, latitude, longitude,
                accuracy_meters, distance_meters, site_radius_meters, created_at)
            VALUES ($1, $2, $3, $4, 41.311, 69.279, 12.0, $5, $6, $7);
            """,
            bekorQilish,
            belgi,
            otish,
            xodim,
            obyekt,
            masofa,
            radius,
            Yaratilgan);

        return belgi;
    }

    /// <summary>Migratsiyadan oldin ham, keyin ham mavjud bo'lgan ustunlar — satr ko'rinishida, tartiblangan.</summary>
    private static async Task<IReadOnlyList<string>> ObyektlarAsync(
        NpgsqlConnection ulanish,
        CancellationToken bekorQilish)
    {
        await using var buyruq = Buyruq(
            ulanish,
            """
            SELECT concat_ws('|', id, name, latitude, longitude, radius_meters, is_active, note, created_at)
              FROM work_sites
             ORDER BY id;
            """);

        var natija = new List<string>();

        await using var oquvchi = await buyruq.ExecuteReaderAsync(bekorQilish);
        while (await oquvchi.ReadAsync(bekorQilish))
        {
            natija.Add(oquvchi.GetString(0));
        }

        return natija;
    }

    /// <summary>SQL bazadagi aynan shu cheklovni buzgani uchun rad etilishini tekshiradi.</summary>
    private static async Task CheklovBuzadiAsync(NpgsqlConnection ulanish, string sql, string cheklov, Guid id)
    {
        var xato = await Should.ThrowAsync<PostgresException>(
            () => BajarAsync(ulanish, sql, TestContext.Current.CancellationToken, id));

        xato.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation, sql);
        xato.ConstraintName.ShouldBe(cheklov, sql);
    }

    private static async Task<bool> UstunBormiAsync(
        NpgsqlConnection ulanish,
        string jadval,
        string ustun,
        CancellationToken bekorQilish)
        => await SanoqAsync(
            ulanish,
            """
            SELECT count(*) FROM information_schema.columns
             WHERE table_schema = 'public' AND table_name = $1 AND column_name = $2;
            """,
            bekorQilish,
            jadval,
            ustun) > 0;

    private static async Task<long> SanoqAsync(
        NpgsqlConnection ulanish,
        string sql,
        CancellationToken bekorQilish,
        params object[] qiymatlar)
        => (long)(await QiymatAsync(ulanish, sql, bekorQilish, qiymatlar))!;

    private static async Task<int> SonAsync(
        NpgsqlConnection ulanish,
        string sql,
        CancellationToken bekorQilish,
        params object[] qiymatlar)
        => (int)(await QiymatAsync(ulanish, sql, bekorQilish, qiymatlar))!;

    private static async Task<string?> MatnAsync(
        NpgsqlConnection ulanish,
        string sql,
        CancellationToken bekorQilish,
        params object[] qiymatlar)
        => (string?)await QiymatAsync(ulanish, sql, bekorQilish, qiymatlar);

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

    private static Task BajarAsync(
        NpgsqlConnection ulanish,
        string sql,
        CancellationToken bekorQilish,
        params object[] qiymatlar)
        => BajarVaYopAsync(Buyruq(ulanish, sql, qiymatlar), bekorQilish);

    private static async Task BajarVaYopAsync(NpgsqlCommand buyruq, CancellationToken bekorQilish)
    {
        await using (buyruq)
        {
            _ = await buyruq.ExecuteNonQueryAsync(bekorQilish);
        }
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
    /// <see cref="CandidateFileMigrationTests"/> dagi yordamchining nusxasi — u yerda
    /// <c>private</c> (<see cref="AttendanceMigrationTests"/> dagi bilan bir xil qaror).
    /// Prefiks va xavfsizlik to'ri aynan bir xil.
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
