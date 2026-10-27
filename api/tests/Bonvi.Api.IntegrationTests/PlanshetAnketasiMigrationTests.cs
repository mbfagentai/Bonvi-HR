using System.Globalization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <c>PlanshetAnketasi</c> migratsiyasi: faqat QO'SHADI (jadval + ketma-ketlik), mavjud
/// nomzodlarga tegmaydi; Down jadvalni va ketma-ketlikni olib tashlaydi, nomzodlar qoladi;
/// qayta Up toza qo'llanadi. <c>api_keys</c> ga TEGMAYDI (2026-10-26 dan planshet kaliti yo'q) va
/// jadvalda qurilma ustunlari yo'q.
/// </summary>
public sealed class PlanshetAnketasiMigrationTests
{
    private const string OldingiMigratsiya = "20261020114958_SorovTahririVaShaxsiyVaqt";

    private const string Migratsiya = "20261026072156_PlanshetAnketasi";

    [Fact]
    public async Task Up_jadval_va_raqam_qoshadi_Down_olib_tashlaydi_nomzodlar_qoladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = await UlanAsync(baza.UlanishSatri, bekorQilish);

        var nomzod = await NomzodQoshAsync(ulanish, bekorQilish);
        var kalitIndekslari = await KalitIndekslariAsync(ulanish, bekorQilish);

        // ── Up ───────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, Migratsiya, bekorQilish);

        (await JadvalBormiAsync(ulanish, bekorQilish)).ShouldBeTrue();
        (await KalitIndekslariAsync(ulanish, bekorQilish)).ShouldBe(kalitIndekslari, "api_keys indekslari o'zgarmaydi");
        (await QiymatAsync(
                ulanish,
                "SELECT count(*) FROM information_schema.columns WHERE table_name = 'candidate_forms' AND column_name LIKE 'device%'",
                bekorQilish))
            .ShouldBe(0L, "qurilma ustunlari yo'q — forma ochiq havola");
        (await QiymatAsync(ulanish, "SELECT count(*) FROM candidates WHERE id = $1", bekorQilish, nomzod)).ShouldBe(1L);

        var birinchi = (int)(await QiymatAsync(ulanish, "SELECT nextval('candidate_form_number_seq')::int", bekorQilish))!;
        var ikkinchi = (int)(await QiymatAsync(ulanish, "SELECT nextval('candidate_form_number_seq')::int", bekorQilish))!;
        ikkinchi.ShouldBe(birinchi + 1);

        var yuborish = Guid.NewGuid();
        await AnketaQoshAsync(ulanish, nomzod, birinchi, yuborish, bekorQilish);

        (await QiymatAsync(ulanish, "SELECT answers->>'lastName' FROM candidate_forms WHERE number = $1", bekorQilish, birinchi))
            .ShouldBe("G‘ulomov", "javoblar jsonb — bazada o'qiladi");

        // Unikal: bitta yuborish identifikatori ikki marta yozilmaydi
        await Should.ThrowAsync<PostgresException>(() => AnketaQoshAsync(ulanish, nomzod, ikkinchi, yuborish, bekorQilish));

        // Unikal: «Anketa №» takrorlanmaydi
        await Should.ThrowAsync<PostgresException>(() => AnketaQoshAsync(ulanish, nomzod, birinchi, Guid.NewGuid(), bekorQilish));

        // ── Down ─────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        (await JadvalBormiAsync(ulanish, bekorQilish)).ShouldBeFalse();
        (await QiymatAsync(ulanish, "SELECT count(*) FROM pg_class WHERE relname = 'candidate_form_number_seq'", bekorQilish)).ShouldBe(0L);
        (await QiymatAsync(ulanish, "SELECT count(*) FROM candidates WHERE id = $1", bekorQilish, nomzod)).ShouldBe(1L, "Down nomzodlarga tegmaydi");
        (await KalitIndekslariAsync(ulanish, bekorQilish)).ShouldBe(kalitIndekslari, "Down ham api_keys ga tegmaydi");

        // ── Yana Up ──────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, Migratsiya, bekorQilish);

        (await JadvalBormiAsync(ulanish, bekorQilish)).ShouldBeTrue();
        (await QiymatAsync(ulanish, "SELECT nextval('candidate_form_number_seq')::int", bekorQilish)).ShouldBe(1, "ketma-ketlik qaytadan 1 dan");
    }

    private static async Task<Guid> NomzodQoshAsync(NpgsqlConnection ulanish, CancellationToken bekorQilish)
    {
        var id = Guid.NewGuid();

        await BajarAsync(
            ulanish,
            """
            INSERT INTO candidates (id, last_name, first_name, vacancy_id, status, source, created_at)
            VALUES ($1, 'Каримов', 'Азиз', $2, 1, 2, now());
            """,
            bekorQilish,
            id,
            Guid.NewGuid());

        return id;
    }

    /// <summary><c>api_keys</c> indekslarining ta'rifi — nomi bo'yicha, bitta matnda.</summary>
    private static async Task<string> KalitIndekslariAsync(NpgsqlConnection ulanish, CancellationToken bekorQilish)
        => (string)(await QiymatAsync(
            ulanish,
            "SELECT string_agg(indexdef, E'\\n' ORDER BY indexname) FROM pg_indexes WHERE tablename = 'api_keys';",
            bekorQilish))!;

    private static Task AnketaQoshAsync(NpgsqlConnection ulanish, Guid nomzod, int raqam, Guid yuborish, CancellationToken bekorQilish)
        => BajarAsync(
            ulanish,
            """
            INSERT INTO candidate_forms (id, candidate_id, number, form_code, form_version, answers, language,
                                         photo_key, signature_key, client_submission_id, submitted_at)
            VALUES ($1, $2, $3, 'HR-F02', '3.0', '{"lastName":"G‘ulomov"}'::jsonb, 'uz-Latn', 'r.jpg', 'i.png', $4, now());
            """,
            bekorQilish,
            Guid.NewGuid(),
            nomzod,
            raqam,
            yuborish);

    private static async Task<bool> JadvalBormiAsync(NpgsqlConnection ulanish, CancellationToken bekorQilish)
        => (long)(await QiymatAsync(
            ulanish,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'candidate_forms';",
            bekorQilish))! > 0;

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

        // Ota qatorlarisiz yozish uchun — tashqi kalit triggerlari shu sessiyada ishlamaydi.
        await BajarAsync(ulanish, "SET session_replication_role = replica;", bekorQilish);

        return ulanish;
    }

    private static async Task BajarAsync(NpgsqlConnection ulanish, string sql, CancellationToken bekorQilish, params object[] qiymatlar)
    {
        await using var buyruq = Buyruq(ulanish, sql, qiymatlar);
        _ = await buyruq.ExecuteNonQueryAsync(bekorQilish);
    }

    private static async Task<object?> QiymatAsync(NpgsqlConnection ulanish, string sql, CancellationToken bekorQilish, params object[] qiymatlar)
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
