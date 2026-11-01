using System.Globalization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <c>AddFormDefinitions</c> migratsiyasi (SPEC-anketa-konstruktori §5.1): faqat QO'SHADI —
/// <c>form_definitions</c> jadvali va <c>candidate_forms.definition_version</c> (NULL = o'rnatilgan
/// anketa). Mavjud anketalar o'zgarmaydi; versiya (kod, raqam) bo'yicha yagona; anketa bog'langan
/// versiyani o'chirib bo'lmaydi; Down ikkalasini olib tashlaydi, anketalar qoladi; qayta Up toza.
/// </summary>
public sealed class AnketaKonstruktoriMigrationTests
{
    private const string OldingiMigratsiya = "20261031165439_AddHrMentorBlanksAndTexts";

    private const string Migratsiya = "20261031233130_AddFormDefinitions";

    [Fact]
    public async Task Up_versiyalar_jadvalini_qoshadi_Down_olib_tashlaydi_anketalar_qoladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = await UlanAsync(baza.UlanishSatri, bekorQilish);

        var nomzod = await NomzodQoshAsync(ulanish, bekorQilish);
        var eskiAnketa = await AnketaQoshAsync(ulanish, nomzod, 1, "3.1", null, bekorQilish);

        // ── Up ───────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, Migratsiya, bekorQilish);

        (await JadvalBormiAsync(ulanish, bekorQilish)).ShouldBeTrue();
        (await UstunBormiAsync(ulanish, bekorQilish)).ShouldBeTrue();
        (await QiymatAsync(ulanish, "SELECT definition_version FROM candidate_forms WHERE id = $1", bekorQilish, eskiAnketa))
            .ShouldBeNull("mavjud anketa — o'rnatilgan anketa bo'lib qoladi");

        await VersiyaQoshAsync(ulanish, 1, "4.0", bekorQilish);
        (await QiymatAsync(ulanish, "SELECT definition->>'label' FROM form_definitions WHERE version = 1", bekorQilish))
            .ShouldBe("4.0", "ta'rif jsonb — bazada o'qiladi");

        // Yagona: bitta versiya ikki marta yozilmaydi
        await Should.ThrowAsync<PostgresException>(() => VersiyaQoshAsync(ulanish, 1, "4.1", bekorQilish));

        // Tashqi kalitlar shu yerdan ishlaydi
        await BajarAsync(ulanish, "SET session_replication_role = origin;", bekorQilish);

        var yangiAnketa = await AnketaQoshAsync(ulanish, nomzod, 2, "4.0", 1, bekorQilish);
        (await QiymatAsync(ulanish, "SELECT definition_version FROM candidate_forms WHERE id = $1", bekorQilish, yangiAnketa))
            .ShouldBe(1);

        // Saqlanmagan versiyaga bog'lab bo'lmaydi
        var yoqVersiya = await Should.ThrowAsync<PostgresException>(
            () => AnketaQoshAsync(ulanish, nomzod, 3, "9.0", 9, bekorQilish));
        yoqVersiya.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);

        // Anketa bog'langan versiya o'chirilmaydi — imzolangan anketaning savollari qolishi shart
        var ochirish = await Should.ThrowAsync<PostgresException>(
            () => BajarAsync(ulanish, "DELETE FROM form_definitions WHERE version = 1;", bekorQilish));
        ochirish.SqlState.ShouldBe(PostgresErrorCodes.RestrictViolation);

        await BajarAsync(ulanish, "SET session_replication_role = replica;", bekorQilish);

        // ── Down ─────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        (await JadvalBormiAsync(ulanish, bekorQilish)).ShouldBeFalse();
        (await UstunBormiAsync(ulanish, bekorQilish)).ShouldBeFalse();
        (await QiymatAsync(ulanish, "SELECT count(*) FROM candidate_forms", bekorQilish)).ShouldBe(2L, "Down anketalarga tegmaydi");

        // ── Yana Up ──────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, Migratsiya, bekorQilish);

        (await JadvalBormiAsync(ulanish, bekorQilish)).ShouldBeTrue();
        (await QiymatAsync(ulanish, "SELECT count(*) FROM candidate_forms WHERE definition_version IS NULL", bekorQilish))
            .ShouldBe(2L);
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

    private static async Task<Guid> AnketaQoshAsync(
        NpgsqlConnection ulanish,
        Guid nomzod,
        int raqam,
        string versiya,
        int? taRifVersiyasi,
        CancellationToken bekorQilish)
    {
        var id = Guid.NewGuid();
        var ustun = taRifVersiyasi is null ? string.Empty : ", definition_version";
        var qiymat = taRifVersiyasi is null ? string.Empty : ", $6";

        object[] qiymatlar = taRifVersiyasi is { } v
            ? [id, nomzod, raqam, versiya, Guid.NewGuid(), v]
            : [id, nomzod, raqam, versiya, Guid.NewGuid()];

        await BajarAsync(
            ulanish,
            $$"""
            INSERT INTO candidate_forms (id, candidate_id, number, form_code, form_version, answers, language,
                                         photo_key, signature_key, client_submission_id, submitted_at{{ustun}})
            VALUES ($1, $2, $3, 'HR-F02', $4, '{"lastName":"G‘ulomov"}'::jsonb, 'uz-Latn', 'r.jpg', 'i.png', $5, now(){{qiymat}});
            """,
            bekorQilish,
            qiymatlar);

        return id;
    }

    private static Task VersiyaQoshAsync(NpgsqlConnection ulanish, int versiya, string yorliq, CancellationToken bekorQilish)
        => BajarAsync(
            ulanish,
            """
            INSERT INTO form_definitions (id, form_code, version, label, definition, sha256, received_at)
            VALUES ($1, 'HR-F02', $2, $3, jsonb_build_object('label', $3::text), repeat('a', 64), now());
            """,
            bekorQilish,
            Guid.NewGuid(),
            versiya,
            yorliq);

    private static async Task<bool> JadvalBormiAsync(NpgsqlConnection ulanish, CancellationToken bekorQilish)
        => (long)(await QiymatAsync(
            ulanish,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'form_definitions';",
            bekorQilish))! > 0;

    private static async Task<bool> UstunBormiAsync(NpgsqlConnection ulanish, CancellationToken bekorQilish)
        => (long)(await QiymatAsync(
            ulanish,
            "SELECT count(*) FROM information_schema.columns WHERE table_name = 'candidate_forms' AND column_name = 'definition_version';",
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
