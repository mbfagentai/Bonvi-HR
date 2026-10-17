using System.Globalization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <c>IshVaqtiSorovi</c> migratsiyasi: <c>absence_types.counts_as_work_time</c> va
/// <c>attendance_days.credited_minutes</c> (SPEC-so'rovlar §B3 AMENDED 2026-10-17).
/// </summary>
/// <remarks>
/// Mavjud qatorlar O'ZGARMASLIGI va sukut BAZADA qolishi tekshiriladi: eski image
/// rollback'dan keyin ustunni bilmay qator yozadi (<c>EmployeeMobileFlagSukuti</c> saboq).
/// Yo'l — <see cref="JarimaFoizMigrationTests"/> dagi bilan bir xil.
/// </remarks>
public sealed class IshVaqtiSorovMigrationTests
{
    private const string OldingiMigratsiya = "20261016092521_RagbatJarimaBuyruqsiz";

    private const string Migratsiya = "20261017072546_IshVaqtiSorovi";

    [Fact]
    public async Task Up_mavjud_qatorlarga_sukut_beradi_Down_ustunlarni_olib_tashlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = await UlanAsync(baza.UlanishSatri, bekorQilish);

        var tur = await TurQoshAsync(ulanish, "Ishxona ishi bilan", bekorQilish);
        var kun = await KunQoshAsync(ulanish, bekorQilish);

        // ── Up ───────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, Migratsiya, bekorQilish);

        (await QiymatAsync(ulanish, "SELECT counts_as_work_time FROM absence_types WHERE id = $1", bekorQilish, tur))
            .ShouldBe(false, "mavjud tur avvalgidek — faqat «Sababli»");
        (await QiymatAsync(ulanish, "SELECT credited_minutes FROM attendance_days WHERE id = $1", bekorQilish, kun))
            .ShouldBe(0, "mavjud kun so'rovsiz hisoblangan");
        (await QiymatAsync(ulanish, "SELECT worked_minutes FROM attendance_days WHERE id = $1", bekorQilish, kun))
            .ShouldBe(480, "boshqa ustunlarga tegilmaydi");

        // Eski image ustunlarni bilmaydi — sukut bazada bo'lishi shart.
        var eskiTur = await TurQoshAsync(ulanish, "Kasallik", bekorQilish);
        var eskiKun = await KunQoshAsync(ulanish, bekorQilish);
        (await QiymatAsync(ulanish, "SELECT counts_as_work_time FROM absence_types WHERE id = $1", bekorQilish, eskiTur))
            .ShouldBe(false);
        (await QiymatAsync(ulanish, "SELECT credited_minutes FROM attendance_days WHERE id = $1", bekorQilish, eskiKun))
            .ShouldBe(0);

        // ── Down ─────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        (await UstunBormiAsync(ulanish, "absence_types", "counts_as_work_time", bekorQilish)).ShouldBeFalse();
        (await UstunBormiAsync(ulanish, "attendance_days", "credited_minutes", bekorQilish)).ShouldBeFalse();
        (await QiymatAsync(ulanish, "SELECT count(*) FROM attendance_days", bekorQilish)).ShouldBe(2L, "qatorlar joyida");
    }

    /// <summary>
    /// <c>JadvalSorovi</c>: mavjud yo'qlik turi xodimlarga ochiq qoladi (sukut TRUE), jadval so'rovlari
    /// jadvallari yaratiladi; Down ularni olib tashlaydi, turlar joyida.
    /// </summary>
    [Fact]
    public async Task JadvalSorovi_mavjud_turni_ochiq_qoldiradi_Down_jadvallarni_olib_tashlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, Migratsiya, bekorQilish);

        await using var ulanish = await UlanAsync(baza.UlanishSatri, bekorQilish);
        var tur = await TurQoshAsync(ulanish, "Shaxsiy sabab", bekorQilish);

        await MigratsiyaAsync(baza.UlanishSatri, JadvalMigratsiyasi, bekorQilish);

        (await QiymatAsync(ulanish, "SELECT employee_can_request FROM absence_types WHERE id = $1", bekorQilish, tur))
            .ShouldBe(true, "avval xodimlar so'ray olgan tur — hozir ham so'ray oladi");
        (await QiymatAsync(ulanish, "SELECT count(*) FROM schedule_change_requests", bekorQilish)).ShouldBe(0L);
        (await QiymatAsync(ulanish, "SELECT count(*) FROM schedule_change_request_days", bekorQilish)).ShouldBe(0L);

        await MigratsiyaAsync(baza.UlanishSatri, Migratsiya, bekorQilish);

        (await UstunBormiAsync(ulanish, "absence_types", "employee_can_request", bekorQilish)).ShouldBeFalse();
        (await QiymatAsync(
                ulanish,
                "SELECT count(*) FROM information_schema.tables WHERE table_name = 'schedule_change_requests'",
                bekorQilish))
            .ShouldBe(0L);
        (await QiymatAsync(ulanish, "SELECT count(*) FROM absence_types", bekorQilish)).ShouldBe(1L);
    }

    private const string JadvalMigratsiyasi = "20261017091318_JadvalSorovi";

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
        await using var buyruq = Buyruq(ulanish, "SET session_replication_role = replica;");
        _ = await buyruq.ExecuteNonQueryAsync(bekorQilish);

        return ulanish;
    }

    /// <summary>Migratsiyadan OLDINGI ustunlar bilan yo'qlik turi.</summary>
    private static async Task<Guid> TurQoshAsync(NpgsqlConnection ulanish, string nom, CancellationToken bekorQilish)
    {
        var id = Guid.NewGuid();

        await using var buyruq = Buyruq(
            ulanish,
            "INSERT INTO absence_types (id, name, is_active, created_at) VALUES ($1, $2, true, now());",
            id,
            nom);
        _ = await buyruq.ExecuteNonQueryAsync(bekorQilish);

        return id;
    }

    /// <summary>Migratsiyadan OLDINGI ustunlar bilan kun yozuvi (480 daqiqa ishlangan).</summary>
    private static async Task<Guid> KunQoshAsync(NpgsqlConnection ulanish, CancellationToken bekorQilish)
    {
        var id = Guid.NewGuid();

        await using var buyruq = Buyruq(
            ulanish,
            """
            INSERT INTO attendance_days (
                id, employee_id, local_date, planned_minutes, worked_minutes, late_minutes,
                early_leave_minutes, overtime_minutes, break_taken_minutes, break_deduction_minutes,
                pass_count, ignored_pass_count, status, computed_at)
            VALUES ($1, $2, $3, 480, 480, 0, 0, 0, 0, 0, 2, 0, 1, now());
            """,
            id,
            Guid.NewGuid(),
            new DateOnly(2026, 10, 1));
        _ = await buyruq.ExecuteNonQueryAsync(bekorQilish);

        return id;
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
