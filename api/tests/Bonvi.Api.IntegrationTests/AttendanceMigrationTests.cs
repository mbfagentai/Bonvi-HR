using System.Globalization;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <c>hangfire</c> sxemasi EF migratsiyalarini buzmasligining testi (SPEC §11.2 #9):
/// <c>upgrade head</c>, <c>20260917063405_DavomatModuli</c> ning <c>Down</c> /
/// <c>Up</c> i va model suratining tekshiruvi Hangfire jadvallari YONIDA ham ishlaydi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida test kerak.</b> <see cref="BonviApiFactory"/> <c>Testing</c>
/// muhitida ishlaydi, u yerda esa Hangfire UMUMAN ulanmaydi
/// (<c>BackgroundJobs.AddBonviBackgroundJobs</c>). Ya'ni boshqa birorta
/// integratsiya testining bazasida <c>hangfire</c> sxemasi paydo bo'lmaydi va
/// ikki «egasi» bir bazada uchrashadigan yagona holat — dev va production —
/// hech qayerda avtomatik tekshirilmasdi.
/// </para>
/// <para>
/// <b>Tartib ATAYLAB ikki xil, har biri alohida test.</b> Yangi bazada birinchi
/// ishga tushishda sxemani AVVAL Hangfire yaratadi: <c>Program.cs</c> dagi
/// <c>UseBonviRecurringJobs</c> omborga <c>RunAsync</c> dan OLDIN murojaat
/// qiladi, migratsiyani esa <c>DatabaseInitializer</c> xost xizmati sifatida —
/// undan KEYIN qo'llaydi. Keyingi har bir yangilanishda aksincha: Hangfire
/// sxemasi allaqachon turibdi va migratsiya uning yonida yuradi.
/// </para>
/// <para>
/// <b>Hangfire ishlab chiqarishdagi yo'l bilan ulanadi</b>
/// (<see cref="HangfireJobStorage.UseBonviPostgreSqlStorage"/>) — sxema nomi va
/// <c>PrepareSchemaIfNecessary</c> bu yerda qayta yozilmaydi, ya'ni kimdir
/// sxemani <c>public</c> ga ko'chirsa, test shu yerda yiqiladi. Ulanish satri
/// EF bilan AYNAN bir xil: ikkalasi bitta Npgsql hovuzidan ulanish oladi.
/// Hangfire o'rnatish skripti <c>SET search_path = 'hangfire'</c> qiladi —
/// u hovuz orqali EF ga o'tib qolsa, modul jadvallari <c>public</c> da emas,
/// <c>hangfire</c> da paydo bo'lardi va quyidagi tekshiruvlar aynan shuni ushlaydi.
/// </para>
/// <para>
/// ⚠️ <b>Global holat.</b> <c>UseStorage</c> statik <see cref="JobStorage.Current"/>
/// ni o'rnatadi. Test uni <c>finally</c> da oldingi qiymatiga qaytaradi: boshqa
/// testlar Hangfire'ning statik API'siga tayanmaydi, lekin o'chirilgan bazaga
/// ishora qoldirish keyingi muallifga tushunarsiz tuzoq bo'lardi.
/// </para>
/// <para>
/// <b>Baza.</b> <see cref="CandidateFileMigrationTests"/> dagi qoidaning aynan
/// o'zi: har bir test o'z <c>bonvi_migr_xxxxx</c> bazasini yaratadi va tugagach
/// o'chiradi. Ishlab chiqish bazasi (<c>bonvi_hr</c>) umuman ochilmaydi.
/// </para>
/// </remarks>
public sealed class AttendanceMigrationTests
{
    /// <summary>Modul migratsiyasidan OLDINGI holat.</summary>
    private const string OldingiMigratsiya = "20260916082625_NatijaUstuniOlibTashlandi";

    /// <summary>Tekshirilayotgan migratsiya — modul uchun yagona.</summary>
    private const string ModulMigratsiyasi = "20260917063405_DavomatModuli";

    /// <summary>Hangfire omboriga yoziladigan va migratsiyadan keyin qayta o'qiladigan belgi.</summary>
    private const string HangfireKaliti = "bonvi:migratsiya-sinovi";

    /// <summary>Modulning yettita jadvali (SPEC §2).</summary>
    private static readonly string[] ModulJadvallari =
    [
        "attendance_devices",
        "attendance_identities",
        "attendance_passes",
        "work_schedules",
        "work_schedule_days",
        "schedule_assignments",
        "attendance_days",
    ];

    // ────────────────────────────────────────────────────────────
    //  1. Birinchi ishga tushish: AVVAL Hangfire, keyin upgrade head
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Bo'sh bazada Hangfire sxemasi migratsiyadan OLDIN yaratilsa ham
    /// <c>upgrade head</c> to'liq o'tadi, jadvallar <c>public</c> ga tushadi va
    /// Hangfire ombori ishlashda davom etadi.
    /// </summary>
    [Fact]
    public async Task Hangfire_sxemasi_OLDIN_yaratilgan_bazada_upgrade_head_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        var oldingiOmbor = JoriyOmbor();

        try
        {
            // ── Program.cs tartibi: UseBonviRecurringJobs → RunAsync ──
            HangfireniUla(baza.UlanishSatri);
            HangfireGaYoz();

            await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
            await ulanish.OpenAsync(bekorQilish);

            // Test o'z sozlamasini isbotlaydi: sxema SHU nuqtada bor, EF
            // esa hali hech narsa yaratmagan. Bo'lmasa quyidagi migratsiya
            // «Hangfire yonida» emas, oddiy bo'sh bazada yurgan bo'lardi.
            var versiya = await HangfireVersiyasiAsync(ulanish, bekorQilish);
            versiya.ShouldNotBeNull("Hangfire sxemasi migratsiyadan OLDIN turishi kerak");

            (await JadvalBormiAsync(ulanish, "public", "__EFMigrationsHistory", bekorQilish))
                .ShouldBeFalse("baza EF uchun hali bo'sh bo'lishi kerak");

            // ── upgrade head — DatabaseInitializer chaqiradigan metodning o'zi ──
            await using (var kontekst = Kontekst(baza.UlanishSatri))
            {
                await kontekst.Database.MigrateAsync(bekorQilish);
                await MigratsiyaHolatiTozaAsync(kontekst, bekorQilish);
            }

            foreach (var jadval in ModulJadvallari)
            {
                (await JadvalBormiAsync(ulanish, "public", jadval, bekorQilish))
                    .ShouldBeTrue($"«{jadval}» public sxemada yaratilishi kerak edi");
            }

            await SxemalarAralashmaganAsync(ulanish, bekorQilish);
            await HangfireButunAsync(ulanish, versiya.Value, bekorQilish);
        }
        finally
        {
            JobStorage.Current = oldingiOmbor!;
        }
    }

    // ────────────────────────────────────────────────────────────
    //  2. Odatdagi yangilanish: Hangfire bor, modul migratsiyasi Down → Up
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Hangfire sxemasi turgan bazada modul migratsiyasi orqaga qaytadi va qayta
    /// qo'llanadi; ikkalasi ham <c>hangfire</c> sxemasiga tegmaydi, Hangfire
    /// qayta ishga tushgandan keyin <c>upgrade head</c> hech narsa qilmaydi.
    /// </summary>
    /// <remarks>
    /// Nishonlar nomi bilan ATAYLAB ko'rsatilgan (<c>null</c> = «eng oxirgisi»
    /// emas) — sababi <see cref="CandidateFileMigrationTests"/> izohida: keyin
    /// yangi migratsiya qo'shilsa, «bir qadam pastga» boshqa narsani anglatib
    /// qolardi va test jim buzilardi.
    /// </remarks>
    [Fact]
    public async Task Hangfire_sxemasi_yonida_modul_migratsiyasi_Down_va_Up_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);

        await using (var kontekst = Kontekst(baza.UlanishSatri))
        {
            await kontekst.Database.MigrateAsync(bekorQilish);
        }

        var oldingiOmbor = JoriyOmbor();

        try
        {
            HangfireniUla(baza.UlanishSatri);
            HangfireGaYoz();

            await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
            await ulanish.OpenAsync(bekorQilish);

            var versiya = await HangfireVersiyasiAsync(ulanish, bekorQilish);
            versiya.ShouldNotBeNull("Hangfire sxemasi test boshida turishi kerak");

            // ── Down ────────────────────────────────────────────
            await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

            foreach (var jadval in ModulJadvallari)
            {
                (await JadvalBormiAsync(ulanish, "public", jadval, bekorQilish))
                    .ShouldBeFalse($"Down «{jadval}» ni o'chirishi kerak edi");
            }

            await HangfireButunAsync(ulanish, versiya.Value, bekorQilish);

            // ── Up ──────────────────────────────────────────────
            await MigratsiyaAsync(baza.UlanishSatri, ModulMigratsiyasi, bekorQilish);

            foreach (var jadval in ModulJadvallari)
            {
                (await JadvalBormiAsync(ulanish, "public", jadval, bekorQilish))
                    .ShouldBeTrue($"Up «{jadval}» ni qayta yaratishi kerak edi");
            }

            // ── Hangfire qayta ishga tushadi, keyin yana upgrade head ──
            // Sxema allaqachon bor: `PrepareSchemaIfNecessary` ikkinchi marta
            // yurib, hech narsani buzmasligi kerak.
            HangfireniUla(baza.UlanishSatri);

            await using (var kontekst = Kontekst(baza.UlanishSatri))
            {
                await kontekst.Database.MigrateAsync(bekorQilish);
                await MigratsiyaHolatiTozaAsync(kontekst, bekorQilish);
            }

            await SxemalarAralashmaganAsync(ulanish, bekorQilish);
            await HangfireButunAsync(ulanish, versiya.Value, bekorQilish);
        }
        finally
        {
            JobStorage.Current = oldingiOmbor!;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Hangfire omborini ishlab chiqarishdagi kengaytma bilan quradi — sxema
    /// shu chaqiruvning o'zida tayyorlanadi.
    /// </summary>
    private static void HangfireniUla(string ulanishSatri)
    {
        var sozlama = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ulanishSatri,
            })
            .Build();

        _ = GlobalConfiguration.Configuration.UseBonviPostgreSqlStorage(sozlama);
    }

    /// <summary>Hangfire omboriga uning o'z API'si orqali belgi yozadi.</summary>
    private static void HangfireGaYoz()
    {
        using var ulanish = JobStorage.Current.GetConnection();
        ulanish.SetRangeInHash(HangfireKaliti, [new KeyValuePair<string, string>("holat", "tirik")]);
    }

    /// <summary>Hozirgi global ombor yoki u hali o'rnatilmagan bo'lsa <c>null</c>.</summary>
    private static JobStorage? JoriyOmbor()
    {
        try
        {
            return JobStorage.Current;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Hangfire sxemasi tegilmagan va ombor hamon ishlaydi: versiya o'sha, yozilgan
    /// belgi Hangfire'ning o'z API'si orqali o'qiladi.
    /// </summary>
    private static async Task HangfireButunAsync(
        NpgsqlConnection ulanish,
        int versiya,
        CancellationToken bekorQilish)
    {
        (await HangfireVersiyasiAsync(ulanish, bekorQilish))
            .ShouldBe(versiya, "EF migratsiyasi Hangfire sxemasining versiyasiga tegmasligi kerak");

        using var hangfire = JobStorage.Current.GetConnection();
        var yozuv = hangfire.GetAllEntriesFromHash(HangfireKaliti);

        yozuv.ShouldNotBeNull("Hangfire'ga yozilgan belgi yo'qoldi");
        yozuv["holat"].ShouldBe("tirik");
    }

    /// <summary>
    /// Ikki «ega» bir-birining sxemasiga tushmagan: EF tarix jadvali faqat
    /// <c>public</c> da, Hangfire jadvallari esa <c>public</c> da yo'q.
    /// </summary>
    private static async Task SxemalarAralashmaganAsync(
        NpgsqlConnection ulanish,
        CancellationToken bekorQilish)
    {
        (await JadvalBormiAsync(ulanish, HangfireJobStorage.Schema, "__EFMigrationsHistory", bekorQilish))
            .ShouldBeFalse("EF tarix jadvali Hangfire sxemasiga tushib qolgan — search_path oqib o'tgan");

        (await JadvalBormiAsync(ulanish, "public", "__EFMigrationsHistory", bekorQilish))
            .ShouldBeTrue();

        foreach (var jadval in ModulJadvallari)
        {
            (await JadvalBormiAsync(ulanish, HangfireJobStorage.Schema, jadval, bekorQilish))
                .ShouldBeFalse($"«{jadval}» Hangfire sxemasida yaratilgan — search_path oqib o'tgan");
        }

        (await SanoqAsync(
            ulanish,
            """
            SELECT count(*) FROM information_schema.tables
             WHERE table_schema = 'public' AND table_name IN ('job', 'jobqueue', 'lock', 'schema', 'server', 'state');
            """,
            bekorQilish))
            .ShouldBe(0, "Hangfire jadvallari public sxemaga tushmasligi kerak");
    }

    /// <summary>
    /// Kutayotgan migratsiya yo'q, tarix jadvalida faqat EF migratsiyalari va
    /// model surati bilan kod o'rtasida farq yo'q (<c>has-pending-model-changes</c>).
    /// </summary>
    private static async Task MigratsiyaHolatiTozaAsync(
        AppDbContext kontekst,
        CancellationToken bekorQilish)
    {
        (await kontekst.Database.GetPendingMigrationsAsync(bekorQilish))
            .ShouldBeEmpty("upgrade head dan keyin kutayotgan migratsiya qolmasligi kerak");

        var qollangan = (await kontekst.Database.GetAppliedMigrationsAsync(bekorQilish)).ToList();

        qollangan.ShouldBe(
            kontekst.Database.GetMigrations().ToList(),
            "tarix jadvalida AYNAN yig'madagi migratsiyalar bo'lishi kerak — begona yozuvsiz");

        qollangan.ShouldContain(ModulMigratsiyasi);

        kontekst.Database.HasPendingModelChanges()
            .ShouldBeFalse("model surati Hangfire sxemasi yonida ham kod bilan bir xil bo'lishi kerak");
    }

    /// <summary>Bazani ko'rsatilgan migratsiyagacha ko'taradi yoki tushiradi.</summary>
    /// <remarks>
    /// <see cref="IMigrator"/> — sababi <see cref="CandidateFileMigrationTests"/> da:
    /// <c>Database.MigrateAsync()</c> faqat eng oxirgisigacha ko'tara oladi.
    /// </remarks>
    private static async Task MigratsiyaAsync(
        string ulanishSatri,
        string nishon,
        CancellationToken bekorQilish)
    {
        await using var kontekst = Kontekst(ulanishSatri);
        await kontekst.GetService<IMigrator>().MigrateAsync(nishon, bekorQilish);
    }

    private static AppDbContext Kontekst(string ulanishSatri)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ulanishSatri)
            .UseSnakeCaseNamingConvention()
            .Options);

    /// <summary>Hangfire sxemasining versiyasi yoki sxema yo'q bo'lsa <c>null</c>.</summary>
    private static async Task<int?> HangfireVersiyasiAsync(
        NpgsqlConnection ulanish,
        CancellationToken bekorQilish)
    {
        if (!await JadvalBormiAsync(ulanish, HangfireJobStorage.Schema, "schema", bekorQilish))
        {
            return null;
        }

        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = $"""SELECT max(version) FROM "{HangfireJobStorage.Schema}"."schema";""";

        var natija = await buyruq.ExecuteScalarAsync(bekorQilish);

        return natija is int son ? son : null;
    }

    private static async Task<bool> JadvalBormiAsync(
        NpgsqlConnection ulanish,
        string sxema,
        string jadval,
        CancellationToken bekorQilish)
        => await SanoqAsync(
            ulanish,
            """
            SELECT count(*) FROM information_schema.tables
             WHERE table_schema = $1 AND table_name = $2;
            """,
            bekorQilish,
            sxema,
            jadval) > 0;

    private static async Task<long> SanoqAsync(
        NpgsqlConnection ulanish,
        string sql,
        CancellationToken bekorQilish,
        params object[] qiymatlar)
    {
        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = sql;

        foreach (var qiymat in qiymatlar)
        {
            _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = qiymat });
        }

        return (long)(await buyruq.ExecuteScalarAsync(bekorQilish))!;
    }

    /// <summary>
    /// Faqat shu test uchun yaratilgan va tugagach o'chiriladigan baza.
    /// </summary>
    /// <remarks>
    /// <see cref="CandidateFileMigrationTests"/> dagi yordamchining nusxasi — u
    /// yerda <c>private</c> va o'sha faylni qayta tuzish bu testning ishi emas.
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
