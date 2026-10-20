using System.Globalization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <c>ErkinJadval</c> migratsiyasi — SPEC-verifikatsiya-erkin-jadval §3, §10.4.
/// </summary>
/// <remarks>
/// <para>
/// Uchta kun jadvaliga <c>is_flexible</c> + <c>required_minutes</c>, kun yozuviga <c>is_flexible</c> +
/// <c>shortfall_minutes</c>; uchta CHECK. UPDATE yo'q — mavjud qatorlar avvalgidek (erkin emas, 0).
/// </para>
/// <para>
/// Eski image (rollback) yangi ustunlarni bilmaydi: uning INSERT lari bazadagi sukut bilan o'tishi
/// SHART. CHECK da <c>is_working</c> yo'q — eski image erkin kunni dam olishga aylantirsa ham saqlay
/// oladi. Yo'l — <see cref="IshVaqtiSorovMigrationTests"/> dagi bilan bir xil.
/// </para>
/// </remarks>
public sealed class ErkinJadvalMigrationTests
{
    private const string OldingiMigratsiya = "20261017163149_JadvalSorovTanaffusi";

    private const string Migratsiya = "20261020051430_ErkinJadval";

    private static readonly string[] KunJadvallari =
    [
        "work_schedule_days",
        "work_schedule_dates",
        "schedule_change_request_days",
    ];

    private static readonly string[] Cheklovlar =
    [
        "ck_work_schedule_days_flexible",
        "ck_work_schedule_dates_flexible",
        "ck_schedule_change_request_days_flexible",
    ];

    [Fact]
    public async Task Up_mavjud_qatorlarni_erkin_emas_qiladi_cheklov_ishlaydi_Down_ustunlarni_olib_tashlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = await UlanAsync(baza.UlanishSatri, bekorQilish);

        // ── Migratsiyadan OLDINGI qatorlar ───────────────────────
        var jadval = await JadvalQoshAsync(ulanish, bekorQilish);
        var katak = await KatakQoshAsync(ulanish, jadval, bekorQilish);
        var sorovKuni = await SorovKuniQoshAsync(ulanish, Guid.NewGuid(), bekorQilish);
        var kun = await DavomatKuniQoshAsync(ulanish, bekorQilish);

        // ── Up ───────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, Migratsiya, bekorQilish);

        (await QiymatAsync(ulanish, "SELECT count(*) FROM work_schedule_days WHERE work_schedule_id = $1 AND NOT is_flexible AND required_minutes IS NULL", bekorQilish, jadval))
            .ShouldBe(7L, "mavjud yettala kun erkin emas");
        (await QiymatAsync(ulanish, "SELECT is_flexible FROM work_schedule_dates WHERE id = $1", bekorQilish, katak)).ShouldBe(false);
        (await QiymatAsync(ulanish, "SELECT required_minutes FROM work_schedule_dates WHERE id = $1", bekorQilish, katak)).ShouldBeNull();
        (await QiymatAsync(ulanish, "SELECT is_flexible FROM schedule_change_request_days WHERE id = $1", bekorQilish, sorovKuni)).ShouldBe(false);
        (await QiymatAsync(ulanish, "SELECT required_minutes FROM schedule_change_request_days WHERE id = $1", bekorQilish, sorovKuni)).ShouldBeNull();
        (await QiymatAsync(ulanish, "SELECT is_flexible FROM attendance_days WHERE id = $1", bekorQilish, kun)).ShouldBe(false);
        (await QiymatAsync(ulanish, "SELECT shortfall_minutes FROM attendance_days WHERE id = $1", bekorQilish, kun)).ShouldBe(0);

        // Eski ustunlarga tegilmagan.
        (await QiymatAsync(ulanish, "SELECT sum(break_minutes) FROM work_schedule_days WHERE work_schedule_id = $1", bekorQilish, jadval))
            .ShouldBe(7L * 120);
        (await QiymatAsync(ulanish, "SELECT start_time::text FROM work_schedule_dates WHERE id = $1", bekorQilish, katak))
            .ShouldBe("08:30:00");
        (await QiymatAsync(ulanish, "SELECT break_minutes FROM schedule_change_request_days WHERE id = $1", bekorQilish, sorovKuni))
            .ShouldBe(120);
        (await QiymatAsync(ulanish, "SELECT worked_minutes FROM attendance_days WHERE id = $1", bekorQilish, kun)).ShouldBe(480);
        (await QiymatAsync(ulanish, "SELECT late_minutes FROM attendance_days WHERE id = $1", bekorQilish, kun)).ShouldBe(15);

        // ── Eski image: yangi ustunlarni bilmay yozadi — sukut bazada ─
        var eskiJadval = await JadvalQoshAsync(ulanish, bekorQilish);
        var eskiKatak = await KatakQoshAsync(ulanish, eskiJadval, bekorQilish);
        var eskiSorovKuni = await SorovKuniQoshAsync(ulanish, Guid.NewGuid(), bekorQilish);
        var eskiKun = await DavomatKuniQoshAsync(ulanish, bekorQilish);

        (await QiymatAsync(ulanish, "SELECT bool_or(is_flexible) FROM work_schedule_days WHERE work_schedule_id = $1", bekorQilish, eskiJadval))
            .ShouldBe(false);
        (await QiymatAsync(ulanish, "SELECT is_flexible FROM work_schedule_dates WHERE id = $1", bekorQilish, eskiKatak)).ShouldBe(false);
        (await QiymatAsync(ulanish, "SELECT is_flexible FROM schedule_change_request_days WHERE id = $1", bekorQilish, eskiSorovKuni)).ShouldBe(false);
        (await QiymatAsync(ulanish, "SELECT shortfall_minutes FROM attendance_days WHERE id = $1", bekorQilish, eskiKun)).ShouldBe(0);

        // ── CHECK: erkin ⇔ norma 1…1440; is_working shartda YO'Q ──
        foreach (var jadvalNomi in KunJadvallari)
        {
            await RadEtiladiAsync(ulanish, jadvalNomi, ishKuni: true, erkin: true, norma: null, bekorQilish);
            await RadEtiladiAsync(ulanish, jadvalNomi, ishKuni: true, erkin: false, norma: 450, bekorQilish);
            await RadEtiladiAsync(ulanish, jadvalNomi, ishKuni: true, erkin: true, norma: 0, bekorQilish);

            await ErkinQatorAsync(ulanish, jadvalNomi, ishKuni: true, erkin: true, norma: 450, bekorQilish);
            await ErkinQatorAsync(ulanish, jadvalNomi, ishKuni: false, erkin: true, norma: 450, bekorQilish);
        }

        var sonlar = await QatorlarSoniAsync(ulanish, bekorQilish);

        // ── Down ─────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        foreach (var jadvalNomi in KunJadvallari)
        {
            (await UstunBormiAsync(ulanish, jadvalNomi, "is_flexible", bekorQilish)).ShouldBeFalse(jadvalNomi);
            (await UstunBormiAsync(ulanish, jadvalNomi, "required_minutes", bekorQilish)).ShouldBeFalse(jadvalNomi);
        }

        (await UstunBormiAsync(ulanish, "attendance_days", "is_flexible", bekorQilish)).ShouldBeFalse();
        (await UstunBormiAsync(ulanish, "attendance_days", "shortfall_minutes", bekorQilish)).ShouldBeFalse();

        foreach (var cheklov in Cheklovlar)
        {
            (await QiymatAsync(ulanish, "SELECT count(*) FROM pg_constraint WHERE conname = $1", bekorQilish, cheklov))
                .ShouldBe(0L, cheklov);
        }

        (await QatorlarSoniAsync(ulanish, bekorQilish)).ShouldBe(sonlar, "Down qatorlarni o'chirmaydi");
        (await QiymatAsync(ulanish, "SELECT worked_minutes FROM attendance_days WHERE id = $1", bekorQilish, kun)).ShouldBe(480);
    }

    /// <summary>Migratsiyadan OLDINGI ustunlar bilan jadval va yettita kuni (08:30–18:00, tanaffus 120).</summary>
    private static async Task<Guid> JadvalQoshAsync(NpgsqlConnection ulanish, CancellationToken bekorQilish)
    {
        var id = Guid.NewGuid();

        await BajarAsync(
            ulanish,
            """
            INSERT INTO work_schedules (id, name, kind, grace_in_minutes, grace_out_minutes, is_active, created_at)
            VALUES ($1, $2, 1, 10, 10, true, now());
            """,
            bekorQilish,
            id,
            "Erkin sinov " + id.ToString("N", CultureInfo.InvariantCulture)[..8]);

        for (var kun = 1; kun <= 7; kun++)
        {
            await BajarAsync(
                ulanish,
                """
                INSERT INTO work_schedule_days (id, work_schedule_id, day_index, is_working, start_time, end_time, break_minutes, break_start, break_end)
                VALUES ($1, $2, $3, true, '08:30', '18:00', 120, '12:00', '14:00');
                """,
                bekorQilish,
                Guid.NewGuid(),
                id,
                kun);
        }

        return id;
    }

    /// <summary>Migratsiyadan OLDINGI ustunlar bilan yillik to'r katagi.</summary>
    private static async Task<Guid> KatakQoshAsync(NpgsqlConnection ulanish, Guid jadval, CancellationToken bekorQilish)
    {
        var id = Guid.NewGuid();

        await BajarAsync(
            ulanish,
            """
            INSERT INTO work_schedule_dates (id, work_schedule_id, date, is_working, start_time, end_time, break_minutes, created_at)
            VALUES ($1, $2, $3, true, '08:30', '18:00', 60, now());
            """,
            bekorQilish,
            id,
            jadval,
            new DateOnly(2026, 10, 21));

        return id;
    }

    /// <summary>Migratsiyadan OLDINGI ustunlar bilan jadval so'rovi kuni (so'rovning o'zi ham).</summary>
    private static async Task<Guid> SorovKuniQoshAsync(NpgsqlConnection ulanish, Guid sorov, CancellationToken bekorQilish)
    {
        await BajarAsync(
            ulanish,
            "INSERT INTO schedule_change_requests (id, employee_id, kind, status, created_at) VALUES ($1, $2, 1, 1, now());",
            bekorQilish,
            sorov,
            Guid.NewGuid());

        var id = Guid.NewGuid();

        await BajarAsync(
            ulanish,
            """
            INSERT INTO schedule_change_request_days (id, request_id, date, is_working, start_time, end_time, break_start, break_end, break_minutes)
            VALUES ($1, $2, $3, true, '08:30', '18:00', '12:00', '14:00', 120);
            """,
            bekorQilish,
            id,
            sorov,
            new DateOnly(2026, 10, 21));

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
            VALUES ($1, $2, $3, 450, 480, 15, 0, 30, 0, 120, 2, 0, 1, now(), 0);
            """,
            bekorQilish,
            id,
            Guid.NewGuid(),
            new DateOnly(2026, 10, 21));

        return id;
    }

    /// <summary>Kun jadvaliga yangi ustunlar bilan qator — har safar o'z ota yozuvi bilan (unikal kalitlar).</summary>
    private static Task ErkinQatorAsync(
        NpgsqlConnection ulanish,
        string jadval,
        bool ishKuni,
        bool erkin,
        int? norma,
        CancellationToken bekorQilish)
    {
        var holat = ishKuni ? "true" : "false";
        var vaqtlar = ishKuni ? "'08:30', '18:00'" : "NULL, NULL";

        var sql = jadval switch
        {
            "work_schedule_days" =>
                $"""
                INSERT INTO work_schedule_days (id, work_schedule_id, day_index, is_working, start_time, end_time, break_minutes, is_flexible, required_minutes)
                VALUES ($1, $2, 1, {holat}, {vaqtlar}, 0, $3::boolean, $4::int);
                """,
            "work_schedule_dates" =>
                $"""
                INSERT INTO work_schedule_dates (id, work_schedule_id, date, is_working, start_time, end_time, break_minutes, created_at, is_flexible, required_minutes)
                VALUES ($1, $2, '2026-10-22', {holat}, {vaqtlar}, 0, now(), $3::boolean, $4::int);
                """,
            "schedule_change_request_days" =>
                $"""
                INSERT INTO schedule_change_request_days (id, request_id, date, is_working, start_time, end_time, break_minutes, is_flexible, required_minutes)
                VALUES ($1, $2, '2026-10-22', {holat}, {vaqtlar}, 0, $3::boolean, $4::int);
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(jadval), jadval, "Noma'lum jadval."),
        };

        return BajarAsync(ulanish, sql, bekorQilish, Guid.NewGuid(), Guid.NewGuid(), erkin, (object?)norma ?? DBNull.Value);
    }

    private static async Task RadEtiladiAsync(
        NpgsqlConnection ulanish,
        string jadval,
        bool ishKuni,
        bool erkin,
        int? norma,
        CancellationToken bekorQilish)
    {
        var xato = await Should.ThrowAsync<PostgresException>(
            () => ErkinQatorAsync(ulanish, jadval, ishKuni, erkin, norma, bekorQilish));

        xato.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation, $"{jadval}: ({erkin}, {norma?.ToString(CultureInfo.InvariantCulture) ?? "NULL"})");
        xato.ConstraintName.ShouldBe($"ck_{jadval}_flexible");
    }

    /// <summary>To'rtala jadvaldagi qatorlar soni — Down hech narsani o'chirmasligi uchun.</summary>
    private static async Task<long[]> QatorlarSoniAsync(NpgsqlConnection ulanish, CancellationToken bekorQilish)
    {
        var sonlar = new List<long>();

        foreach (var jadval in KunJadvallari.Append("attendance_days"))
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
