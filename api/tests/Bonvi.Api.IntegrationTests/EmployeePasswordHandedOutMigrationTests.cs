using System.Globalization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <c>XodimParoliBerilgani</c> migratsiyasi: eski hisoblarning «HR parolni bilib oldi» belgisi
/// audit izidan to'g'ri to'ldiriladimi.
/// </summary>
/// <remarks>
/// Bu xato bo'lsa, ilovadan allaqachon foydalanayotgan xodim deploy'dan keyin kira
/// olmay qoladi (belgi bo'sh) — yoki aksincha, hech kim bilmaydigan parol ochiq
/// qoladi (belgi noto'g'ri to'lgan). Bo'sh bazada eski qatorlar yo'q, shu sababli
/// alohida vaqtinchalik baza (<see cref="AuditMigrationTests"/> bilan bir xil usul).
/// </remarks>
public sealed class EmployeePasswordHandedOutMigrationTests
{
    /// <summary>Belgidan OLDINGI holat.</summary>
    private const string OldingiMigratsiya = "20261012115700_AuditJurnaliKengaytirildi";

    /// <summary>Tekshirilayotgan migratsiya.</summary>
    private const string Migratsiya = "20261012152522_XodimParoliBerilgani";

    /// <summary>Joriy parol berilgan lahza.</summary>
    private static readonly DateTimeOffset ParolVaqti = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Belgi_joriy_paroldan_keyingi_birinchi_korish_yoki_qoyishdan_toldiriladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        // Xodim qatorlari kerak emas: tashqi kalitlar shu sessiyada o'chiq (vaqtinchalik baza).
        await BajarAsync(ulanish, "SET session_replication_role = replica;", bekorQilish);

        var korgan = await HisobAsync(ulanish, bekorQilish);
        var eskiKorish = await HisobAsync(ulanish, bekorQilish);
        var ozi = await HisobAsync(ulanish, bekorQilish);
        var hechKim = await HisobAsync(ulanish, bekorQilish);
        var ikkiMarta = await HisobAsync(ulanish, bekorQilish);
        var nusxasiz = await HisobAsync(ulanish, bekorQilish, shifrlangan: null);
        var kirgan = await HisobAsync(ulanish, bekorQilish);
        var eskiParoldaKirgan = await HisobAsync(ulanish, bekorQilish);

        // Joriy parol bilan kirgan — parolni kimdir biladi; eski parol davrida kirgan esa yo'q.
        await BajarAsync(ulanish, $"UPDATE employee_accounts SET last_login_at = password_set_at + interval '1 day' WHERE id = '{kirgan}';", bekorQilish);
        await BajarAsync(ulanish, $"UPDATE employee_accounts SET last_login_at = password_set_at - interval '1 day' WHERE id = '{eskiParoldaKirgan}';", bekorQilish);

        await HodisaAsync(ulanish, korgan, "password_viewed", ParolVaqti.AddHours(1), bekorQilish);

        // Ko'rish joriy paroldan OLDIN — keyin «Yangi parol yasash» bosilgan: yangisini hech kim bilmaydi.
        await HodisaAsync(ulanish, eskiKorish, "password_viewed", ParolVaqti.AddDays(-1), bekorQilish);

        // HR o'zi yozgan — hodisa parol vaqtidan millisekundlar keyin.
        await HodisaAsync(ulanish, ozi, "password_set", ParolVaqti.AddMilliseconds(3), bekorQilish);

        // Avtomatik yasalgan, keyin ikki marta ko'rilgan — BIRINCHI ko'rish.
        await HodisaAsync(ulanish, ikkiMarta, "password_generated", ParolVaqti, bekorQilish);
        await HodisaAsync(ulanish, ikkiMarta, "password_viewed", ParolVaqti.AddHours(2), bekorQilish);
        await HodisaAsync(ulanish, ikkiMarta, "password_viewed", ParolVaqti.AddHours(3), bekorQilish);

        await MigratsiyaAsync(baza.UlanishSatri, Migratsiya, bekorQilish);

        (await BelgiAsync(ulanish, korgan, bekorQilish)).ShouldBe(ParolVaqti.AddHours(1));
        (await BelgiAsync(ulanish, eskiKorish, bekorQilish)).ShouldBeNull();
        (await BelgiAsync(ulanish, ozi, bekorQilish)).ShouldBe(ParolVaqti.AddMilliseconds(3));
        (await BelgiAsync(ulanish, hechKim, bekorQilish)).ShouldBeNull();
        (await BelgiAsync(ulanish, ikkiMarta, bekorQilish)).ShouldBe(ParolVaqti.AddHours(2));

        // Shifrlangan nusxasiz eski parol — HR o'sha paytda o'zi bergan.
        (await BelgiAsync(ulanish, nusxasiz, bekorQilish)).ShouldBe(ParolVaqti);

        (await BelgiAsync(ulanish, kirgan, bekorQilish)).ShouldBe(ParolVaqti);
        (await BelgiAsync(ulanish, eskiParoldaKirgan, bekorQilish)).ShouldBeNull();

        // Audit jurnaliga tegilmagan (u faqat o'qildi).
        (await QiymatAsync(ulanish, "SELECT count(*) FROM audit_logs;", bekorQilish)).ShouldBe(6L);
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    private static async Task<Guid> HisobAsync(
        NpgsqlConnection ulanish,
        CancellationToken bekorQilish,
        string? shifrlangan = "v1.shifr")
    {
        var id = Guid.CreateVersion7();

        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = """
            INSERT INTO employee_accounts (id, employee_id, password_hash, password_protected, password_set_at, created_at)
            VALUES ($1, $2, 'xesh', $4, $3, $3);
            """;
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = id });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = Guid.NewGuid() });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = ParolVaqti });
        _ = buyruq.Parameters.Add(new NpgsqlParameter<string?> { TypedValue = shifrlangan, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text });

        _ = await buyruq.ExecuteNonQueryAsync(bekorQilish);
        return id;
    }

    /// <summary>Oldingi relizning `AuditLogger` i yozgan shakldagi hodisa.</summary>
    private static async Task HodisaAsync(
        NpgsqlConnection ulanish,
        Guid hisob,
        string amal,
        DateTimeOffset vaqt,
        CancellationToken bekorQilish)
    {
        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = """
            INSERT INTO audit_logs (id, occurred_at, action, entity_name, entity_id, new_values)
            VALUES ($1, $2, 2, 'EmployeeAccount', $3, $4);
            """;
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = Guid.CreateVersion7() });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = vaqt });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = hisob.ToString() });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = "{\"action\":\"" + amal + "\"}" });

        _ = await buyruq.ExecuteNonQueryAsync(bekorQilish);
    }

    private static async Task<DateTimeOffset?> BelgiAsync(NpgsqlConnection ulanish, Guid hisob, CancellationToken bekorQilish)
        => await QiymatAsync(ulanish, "SELECT password_handed_out_at FROM employee_accounts WHERE id = $1;", bekorQilish, hisob)
            is DateTime vaqt
            ? new DateTimeOffset(DateTime.SpecifyKind(vaqt, DateTimeKind.Utc))
            : null;

    private static async Task MigratsiyaAsync(string ulanishSatri, string nishon, CancellationToken bekorQilish)
    {
        var sozlamalar = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ulanishSatri)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var kontekst = new AppDbContext(sozlamalar);
        await kontekst.GetService<IMigrator>().MigrateAsync(nishon, bekorQilish);
    }

    private static async Task BajarAsync(NpgsqlConnection ulanish, string sql, CancellationToken bekorQilish)
    {
        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = sql;
        _ = await buyruq.ExecuteNonQueryAsync(bekorQilish);
    }

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

    /// <summary>Vaqtinchalik baza — har bir test uchun alohida.</summary>
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
            // ⚠️ Xavfsizlik to'ri: DROP faqat SHU testlar yaratgan nomga tushishi mumkin.
            if (!_nom.StartsWith(Prefiks, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"«{_nom}» — bu testlar yaratgan baza emas, o'chirilmaydi.");
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
