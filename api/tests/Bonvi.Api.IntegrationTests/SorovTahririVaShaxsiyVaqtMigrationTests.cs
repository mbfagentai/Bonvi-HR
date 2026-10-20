using System.Globalization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <c>SorovTahririVaShaxsiyVaqt</c> migratsiyasi — SPEC-verifikatsiya-sorov-tahriri §2.1, §8.5.
/// </summary>
/// <remarks>
/// <para>
/// Ikki so'rov jadvaliga <c>version</c> + <c>edited_at</c> + <c>edited_by_account_id</c>, kun yozuviga
/// <c>personal_minutes</c>. UPDATE yo'q — mavjud qatorlar avvalgidek (versiya 0, tahrirlanmagan, 0).
/// </para>
/// <para>
/// Eski image (rollback) yangi ustunlarni bilmaydi: uning INSERT lari bazadagi sukut bilan o'tishi
/// SHART. Yo'l — <see cref="ErkinJadvalMigrationTests"/> dagi bilan bir xil.
/// </para>
/// </remarks>
public sealed class SorovTahririVaShaxsiyVaqtMigrationTests
{
    private const string OldingiMigratsiya = "20261020051430_ErkinJadval";

    private const string Migratsiya = "20261020114958_SorovTahririVaShaxsiyVaqt";

    private static readonly string[] SorovJadvallari = ["absence_requests", "schedule_change_requests"];

    private static readonly string[] Jadvallar =
    [
        "absence_requests",
        "schedule_change_requests",
        "schedule_change_request_days",
        "attendance_days",
    ];

    [Fact]
    public async Task Up_mavjud_qatorlarni_tegmaydi_eski_image_yoza_oladi_Down_ustunlarni_olib_tashlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = await UlanAsync(baza.UlanishSatri, bekorQilish);

        // ── Migratsiyadan OLDINGI qatorlar ───────────────────────
        var yoqlik = await YoqlikSorovQoshAsync(ulanish, bekorQilish);
        var jadvalSorov = Guid.NewGuid();
        var sorovKuni = await JadvalSorovQoshAsync(ulanish, jadvalSorov, bekorQilish);
        var kun = await DavomatKuniQoshAsync(ulanish, bekorQilish);

        // ── Up ───────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, Migratsiya, bekorQilish);

        foreach (var (jadval, id) in new[] { ("absence_requests", yoqlik), ("schedule_change_requests", jadvalSorov) })
        {
            (await QiymatAsync(ulanish, $"SELECT version FROM {jadval} WHERE id = $1", bekorQilish, id)).ShouldBe(0, jadval);
            (await QiymatAsync(ulanish, $"SELECT edited_at FROM {jadval} WHERE id = $1", bekorQilish, id)).ShouldBeNull(jadval);
            (await QiymatAsync(ulanish, $"SELECT edited_by_account_id FROM {jadval} WHERE id = $1", bekorQilish, id)).ShouldBeNull(jadval);
        }

        (await QiymatAsync(ulanish, "SELECT personal_minutes FROM attendance_days WHERE id = $1", bekorQilish, kun)).ShouldBe(0);

        // Eski ustunlarga tegilmagan.
        (await QiymatAsync(ulanish, "SELECT note FROM absence_requests WHERE id = $1", bekorQilish, yoqlik)).ShouldBe("Shifokorga");
        (await QiymatAsync(ulanish, "SELECT from_time::text FROM absence_requests WHERE id = $1", bekorQilish, yoqlik)).ShouldBe("14:00:00");
        (await QiymatAsync(ulanish, "SELECT status FROM absence_requests WHERE id = $1", bekorQilish, yoqlik)).ShouldBe(1);
        (await QiymatAsync(ulanish, "SELECT note FROM schedule_change_requests WHERE id = $1", bekorQilish, jadvalSorov)).ShouldBe("Juma");
        (await QiymatAsync(ulanish, "SELECT break_minutes FROM schedule_change_request_days WHERE id = $1", bekorQilish, sorovKuni)).ShouldBe(120);
        (await QiymatAsync(ulanish, "SELECT worked_minutes FROM attendance_days WHERE id = $1", bekorQilish, kun)).ShouldBe(480);
        (await QiymatAsync(ulanish, "SELECT shortfall_minutes FROM attendance_days WHERE id = $1", bekorQilish, kun)).ShouldBe(0);
        (await QiymatAsync(ulanish, "SELECT late_minutes FROM attendance_days WHERE id = $1", bekorQilish, kun)).ShouldBe(15);

        // ── Eski image: yangi ustunlarni bilmay yozadi — sukut bazada ─
        var eskiYoqlik = await YoqlikSorovQoshAsync(ulanish, bekorQilish);
        var eskiJadvalSorov = Guid.NewGuid();
        await JadvalSorovQoshAsync(ulanish, eskiJadvalSorov, bekorQilish);
        var eskiKun = await DavomatKuniQoshAsync(ulanish, bekorQilish);

        (await QiymatAsync(ulanish, "SELECT version FROM absence_requests WHERE id = $1", bekorQilish, eskiYoqlik)).ShouldBe(0);
        (await QiymatAsync(ulanish, "SELECT version FROM schedule_change_requests WHERE id = $1", bekorQilish, eskiJadvalSorov)).ShouldBe(0);
        (await QiymatAsync(ulanish, "SELECT personal_minutes FROM attendance_days WHERE id = $1", bekorQilish, eskiKun)).ShouldBe(0);

        // Eski image qatorni versiyasiz yangilaydi — o'tadi (yangi kod uni keyingi yozuvda oshiradi).
        await BajarAsync(ulanish, "UPDATE absence_requests SET status = 2 WHERE id = $1;", bekorQilish, eskiYoqlik);

        var sonlar = await QatorlarSoniAsync(ulanish, bekorQilish);

        // ── Down ─────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        foreach (var jadval in SorovJadvallari)
        {
            (await UstunBormiAsync(ulanish, jadval, "version", bekorQilish)).ShouldBeFalse(jadval);
            (await UstunBormiAsync(ulanish, jadval, "edited_at", bekorQilish)).ShouldBeFalse(jadval);
            (await UstunBormiAsync(ulanish, jadval, "edited_by_account_id", bekorQilish)).ShouldBeFalse(jadval);
        }

        (await UstunBormiAsync(ulanish, "attendance_days", "personal_minutes", bekorQilish)).ShouldBeFalse();

        (await QatorlarSoniAsync(ulanish, bekorQilish)).ShouldBe(sonlar, "Down qatorlarni o'chirmaydi");
        (await QiymatAsync(ulanish, "SELECT worked_minutes FROM attendance_days WHERE id = $1", bekorQilish, kun)).ShouldBe(480);
        (await QiymatAsync(ulanish, "SELECT note FROM absence_requests WHERE id = $1", bekorQilish, yoqlik)).ShouldBe("Shifokorga");

        // ── Yana Up — ikkinchi marta ham toza qo'llanadi ─────────
        await MigratsiyaAsync(baza.UlanishSatri, Migratsiya, bekorQilish);

        (await QiymatAsync(ulanish, "SELECT version FROM absence_requests WHERE id = $1", bekorQilish, yoqlik)).ShouldBe(0);
        (await QiymatAsync(ulanish, "SELECT personal_minutes FROM attendance_days WHERE id = $1", bekorQilish, kun)).ShouldBe(0);
    }

    /// <summary>Migratsiyadan OLDINGI ustunlar bilan kutayotgan vaqtli yo'qlik so'rovi.</summary>
    private static async Task<Guid> YoqlikSorovQoshAsync(NpgsqlConnection ulanish, CancellationToken bekorQilish)
    {
        var id = Guid.NewGuid();

        await BajarAsync(
            ulanish,
            """
            INSERT INTO absence_requests (id, employee_id, absence_type_id, from_date, to_date, from_time, to_time, note, status, created_at)
            VALUES ($1, $2, $3, $4, $4, '14:00', '16:00', 'Shifokorga', 1, now());
            """,
            bekorQilish,
            id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 10, 21));

        return id;
    }

    /// <summary>Migratsiyadan OLDINGI ustunlar bilan jadval so'rovi va uning kuni.</summary>
    private static async Task<Guid> JadvalSorovQoshAsync(NpgsqlConnection ulanish, Guid sorov, CancellationToken bekorQilish)
    {
        await BajarAsync(
            ulanish,
            "INSERT INTO schedule_change_requests (id, employee_id, kind, status, note, created_at) VALUES ($1, $2, 1, 1, 'Juma', now());",
            bekorQilish,
            sorov,
            Guid.NewGuid());

        var id = Guid.NewGuid();

        await BajarAsync(
            ulanish,
            """
            INSERT INTO schedule_change_request_days (id, request_id, date, is_working, start_time, end_time, break_start, break_end, break_minutes)
            VALUES ($1, $2, $3, true, '08:30', '17:30', '12:23', '14:23', 120);
            """,
            bekorQilish,
            id,
            sorov,
            new DateOnly(2026, 10, 23));

        return id;
    }

    /// <summary>Migratsiyadan OLDINGI ustunlar bilan kun yozuvi (480 ishlangan, 15 kechikish).</summary>
    private static async Task<Guid> DavomatKuniQoshAsync(NpgsqlConnection ulanish, CancellationToken bekorQilish)
    {
        var id = Guid.NewGuid();

        await BajarAsync(
            ulanish,
            """
            INSERT INTO attendance_days (
                id, employee_id, local_date, planned_minutes, worked_minutes, late_minutes,
                early_leave_minutes, overtime_minutes, break_taken_minutes, break_deduction_minutes,
                pass_count, ignored_pass_count, status, computed_at, credited_minutes)
            VALUES ($1, $2, $3, 480, 480, 15, 0, 0, 0, 60, 2, 0, 1, now(), 0);
            """,
            bekorQilish,
            id,
            Guid.NewGuid(),
            new DateOnly(2026, 10, 21));

        return id;
    }

    /// <summary>To'rtala jadvaldagi qatorlar soni — Down hech narsani o'chirmasligi uchun.</summary>
    private static async Task<long[]> QatorlarSoniAsync(NpgsqlConnection ulanish, CancellationToken bekorQilish)
    {
        var sonlar = new List<long>();

        foreach (var jadval in Jadvallar)
        {
            sonlar.Add((long)(await QiymatAsync(ulanish, $"SELECT count(*) FROM {jadval}", bekorQilish))!);
        }

        return [.. sonlar];
    }

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

        // Ota qatorlarisiz yozish uchun — tashqi kalit triggerlari shu sessiyada ishlamaydi (CHECK esa ishlaydi).
        await BajarAsync(ulanish, "SET session_replication_role = replica;", bekorQilish);

        return ulanish;
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

    private static async Task<bool> UstunBormiAsync(
        NpgsqlConnection ulanish,
        string jadval,
        string ustun,
        CancellationToken bekorQilish)
        => (long)(await QiymatAsync(
            ulanish,
            """
            SELECT count(*) FROM information_schema.columns
             WHERE table_schema = 'public' AND table_name = $1 AND column_name = $2;
            """,
            bekorQilish,
            jadval,
            ustun))! > 0;

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
