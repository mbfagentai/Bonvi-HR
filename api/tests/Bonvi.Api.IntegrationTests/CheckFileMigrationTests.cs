using System.Globalization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NpgsqlTypes;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <c>20260927054635_TekshiruvMateriallari</c> migratsiyasining MA'LUMOT
/// ustidagi testi: eski yagona mehnat.uz skrinshoti yangi
/// <c>employee_check_files</c> jadvaliga HAQIQATAN ko'chadimi, eski
/// ustunlar joyida qoladimi va <c>Down</c> → <c>Up</c> ishlaydimi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <see cref="BonviApiFactory"/> yetarli emas</b> — sabab
/// <see cref="CandidateFileMigrationTests"/> izohida: bo'sh bazada
/// ko'chirish nol qator ko'chiradi va hech qachon yiqilmaydi. Shu sababli
/// baza migratsiyadan OLDINGI holatga keltiriladi, «eski» xodimlar xom SQL
/// bilan yoziladi va shundan keyingina yuqoriga ko'tariladi.
/// </para>
/// <para>
/// <b>Eski ustunlar O'CHIRILMASLIGI shart</b> — bu migratsiyaning asosiy
/// va'dasi: deploy'dan keyin eski image'ga qaytilsa, u aynan shu
/// ustunlarni o'qiydi (kengaytirish/qisqartirish usuli).
/// </para>
/// </remarks>
public sealed class CheckFileMigrationTests
{
    /// <summary>Ko'chirishdan OLDINGI holat.</summary>
    private const string OldingiMigratsiya = "20260924045102_NomzodlikIzohlari";

    /// <summary>Tekshirilayotgan migratsiya.</summary>
    private const string KochirishMigratsiyasi = "20260927054635_TekshiruvMateriallari";

    private static readonly DateTimeOffset Yaratilgan = new(2026, 1, 15, 8, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Yangilangan = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Har bir skrinshot bitta material bo'lib ko'chadi: nom turdan yasaladi,
    /// hajm va muallif noma'lum, vaqt — xodimning oxirgi tahriri. Yumshoq
    /// o'chirilgan xodim ham ko'chadi, skrinshotsiz xodimga qator yo'q.
    /// </summary>
    [Fact]
    public async Task Eski_skrinshot_material_bolib_KOCHADI_eski_ustunlar_QOLADI()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        var png = await XodimQoshAsync(ulanish, "2026/01/png-kalit", "image/png", Yangilangan, ochirilgan: false, bekorQilish);
        var pdf = await XodimQoshAsync(ulanish, "2026/01/pdf-kalit", "application/pdf", null, ochirilgan: false, bekorQilish);
        var turisiz = await XodimQoshAsync(ulanish, "2026/01/turisiz", null, null, ochirilgan: false, bekorQilish);
        var ochirilgan = await XodimQoshAsync(ulanish, "2026/01/jpeg-kalit", "image/jpeg", null, ochirilgan: true, bekorQilish);
        var skrinshotsiz = await XodimQoshAsync(ulanish, null, null, null, ochirilgan: false, bekorQilish);

        await MigratsiyaAsync(baza.UlanishSatri, KochirishMigratsiyasi, bekorQilish);

        var pngQator = await YagonaMaterialAsync(ulanish, png, bekorQilish);
        pngQator.Kalit.ShouldBe("2026/01/png-kalit", "fayl diskda o'z joyida — faqat kalit ko'chadi");
        pngQator.Nom.ShouldBe("mehnat-uz-skrinshot.png");
        pngQator.Tur.ShouldBe("image/png");
        pngQator.Hajm.ShouldBeNull("hajm saqlanmagan edi — «noma'lum»");
        pngQator.Kim.ShouldBeNull("kim yuklagani noma'lum — oxirgi tahrirlovchi muallif emas");
        pngQator.Yaratilgan.ShouldBe(Yangilangan, "vaqt — xodimning oxirgi tahriri");
        pngQator.Ochirilgan.ShouldBeFalse();

        var pdfQator = await YagonaMaterialAsync(ulanish, pdf, bekorQilish);
        pdfQator.Nom.ShouldBe("mehnat-uz-skrinshot.pdf");
        pdfQator.Yaratilgan.ShouldBe(Yaratilgan, "tahrir bo'lmasa — yaratilgan vaqt");

        var turisizQator = await YagonaMaterialAsync(ulanish, turisiz, bekorQilish);
        turisizQator.Nom.ShouldBe("mehnat-uz-skrinshot");
        turisizQator.Tur.ShouldBe("application/octet-stream");

        (await YagonaMaterialAsync(ulanish, ochirilgan, bekorQilish)).Nom
            .ShouldBe("mehnat-uz-skrinshot.jpg", "yumshoq o'chirilgan xodimning dalili ham yo'qolmaydi");

        (await SanoqAsync(ulanish, "SELECT count(*) FROM employee_check_files WHERE employee_id = $1;", bekorQilish, skrinshotsiz))
            .ShouldBe(0, "skrinshotsiz xodimga material yaratilmaydi");

        // ⭐ Eski ustunlar joyida va qiymatlari tegilmagan — eski image shu bilan ishlaydi
        (await SanoqAsync(ulanish, "SELECT count(*) FROM employees WHERE mehnat_uz_screenshot_key IS NOT NULL;", bekorQilish))
            .ShouldBe(4);
    }

    /// <summary>
    /// <c>Down</c> faqat yangi jadvalni oladi — eski ustunlardagi skrinshot
    /// o'z holicha qoladi; keyingi <c>Up</c> uni qayta, TAKRORSIZ ko'chiradi.
    /// </summary>
    [Fact]
    public async Task Down_eski_ustunlarga_tegmaydi_qayta_Up_takrorlamaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        var xodim = await XodimQoshAsync(ulanish, "2026/01/png-kalit", "image/png", null, ochirilgan: false, bekorQilish);

        await MigratsiyaAsync(baza.UlanishSatri, KochirishMigratsiyasi, bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        (await SanoqAsync(
            ulanish,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'employee_check_files';",
            bekorQilish)).ShouldBe(0, "Down yangi jadvalni olib tashlashi kerak");

        (await QiymatAsync(ulanish, "SELECT mehnat_uz_screenshot_key FROM employees WHERE id = $1;", bekorQilish, xodim))
            .ShouldBe("2026/01/png-kalit", "eski skrinshot joyida — qaytishda tiklash kerak emas");

        await MigratsiyaAsync(baza.UlanishSatri, KochirishMigratsiyasi, bekorQilish);

        (await YagonaMaterialAsync(ulanish, xodim, bekorQilish)).Kalit.ShouldBe("2026/01/png-kalit");
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    private sealed record MaterialQatori(
        string Kalit,
        string Nom,
        string Tur,
        long? Hajm,
        DateTimeOffset Yaratilgan,
        Guid? Kim,
        bool Ochirilgan);

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

    /// <summary>Migratsiyadan OLDINGI sxemadagi xodim — xom SQL bilan.</summary>
    private static async Task<Guid> XodimQoshAsync(
        NpgsqlConnection ulanish,
        string? kalit,
        string? tur,
        DateTimeOffset? yangilangan,
        bool ochirilgan,
        CancellationToken bekorQilish)
    {
        var id = Guid.NewGuid();

        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = """
            INSERT INTO employees
                (id, last_name, first_name, direction_ids, needs_review, created_at, updated_at, updated_by,
                 deleted_at, mehnat_uz_screenshot_key, mehnat_uz_screenshot_content_type)
            VALUES ($1, 'Синов', 'Ходим', '{}', false, $2, $3, $4, $5, $6, $7);
            """;
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = id });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = Yaratilgan });
        _ = buyruq.Parameters.Add(Ixtiyoriy(NpgsqlDbType.TimestampTz, yangilangan));

        // Oxirgi tahrirlovchi bor — lekin u material muallifi bo'lmasligi kerak
        _ = buyruq.Parameters.Add(Ixtiyoriy(NpgsqlDbType.Uuid, yangilangan is null ? null : Guid.NewGuid()));
        _ = buyruq.Parameters.Add(Ixtiyoriy(NpgsqlDbType.TimestampTz, ochirilgan ? Yangilangan : null));
        _ = buyruq.Parameters.Add(Ixtiyoriy(NpgsqlDbType.Text, kalit));
        _ = buyruq.Parameters.Add(Ixtiyoriy(NpgsqlDbType.Text, tur));

        _ = await buyruq.ExecuteNonQueryAsync(bekorQilish);
        return id;
    }

    private static async Task<MaterialQatori> YagonaMaterialAsync(
        NpgsqlConnection ulanish,
        Guid xodim,
        CancellationToken bekorQilish)
    {
        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = """
            SELECT file_key, file_name, content_type, size_bytes, created_at, created_by, deleted_at IS NOT NULL
              FROM employee_check_files
             WHERE employee_id = $1;
            """;
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = xodim });

        await using var oquvchi = await buyruq.ExecuteReaderAsync(bekorQilish);
        (await oquvchi.ReadAsync(bekorQilish)).ShouldBeTrue("xodimning materiali ko'chmagan");

        var qator = new MaterialQatori(
            oquvchi.GetString(0),
            oquvchi.GetString(1),
            oquvchi.GetString(2),
            await oquvchi.IsDBNullAsync(3, bekorQilish) ? null : oquvchi.GetInt64(3),
            oquvchi.GetFieldValue<DateTimeOffset>(4),
            await oquvchi.IsDBNullAsync(5, bekorQilish) ? null : oquvchi.GetGuid(5),
            oquvchi.GetBoolean(6));

        (await oquvchi.ReadAsync(bekorQilish)).ShouldBeFalse("bitta skrinshotdan ANIQ bitta material chiqishi kerak");
        return qator;
    }

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
        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = sql;

        foreach (var qiymat in qiymatlar)
        {
            _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = qiymat });
        }

        var natija = await buyruq.ExecuteScalarAsync(bekorQilish);
        return natija is DBNull ? null : natija;
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
