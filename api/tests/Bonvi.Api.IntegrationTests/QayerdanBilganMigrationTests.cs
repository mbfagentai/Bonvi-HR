using System.Globalization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <c>QayerdanBilgan</c> migratsiyasi — SPEC-qayerdan-bilgan §4, §8.
/// </summary>
/// <remarks>
/// <para>
/// Ma'lumot KO'CHADI: bot anketasidagi <c>referral_source</c> (1..8) → nomzodning
/// <c>referral_channel_id</c> (seed kanallar), keyin eski ustun o'chadi. Down teskarisini
/// qiladi: seed kanal → o'z qiymati, HR qo'shgan boshqa kanal → 6 («Boshqa»).
/// </para>
/// <para>
/// Sakkizala qiymat ikkala yo'nalishda ham alohida qatorda tekshiriladi — xaritadagi bitta
/// adashgan raqam prod'dagi 275 anketaning bir qismini jimgina boshqa kanalga o'tkazardi.
/// </para>
/// </remarks>
public sealed class QayerdanBilganMigrationTests
{
    private const string OldingiMigratsiya = "20261026072156_PlanshetAnketasi";

    private const string Migratsiya = "20261026101017_QayerdanBilgan";

    [Fact]
    public async Task Up_sakkizala_qiymatni_kochiradi_Down_ularni_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = await UlanAsync(baza.UlanishSatri, bekorQilish);

        // ── Migratsiyadan OLDIN: har qiymatga bitta bot nomzodi + anketasiz qo'lda nomzod ──
        var botNomzodlari = new Dictionary<int, Guid>();

        foreach (var kanal in ReferralSeeds.All)
        {
            var nomzod = await NomzodQoshAsync(ulanish, bekorQilish);
            await AnketaQoshAsync(ulanish, nomzod, kanal.OldValue, bekorQilish);
            botNomzodlari[kanal.OldValue] = nomzod;
        }

        var qolda = await NomzodQoshAsync(ulanish, bekorQilish);

        // ── Up ───────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, Migratsiya, bekorQilish);

        foreach (var kanal in ReferralSeeds.All)
        {
            (await QiymatAsync(ulanish, "SELECT referral_channel_id FROM candidates WHERE id = $1", bekorQilish, botNomzodlari[kanal.OldValue]))
                .ShouldBe(kanal.Id, $"eski {kanal.OldValue} → {kanal.Name}");
            (await QiymatAsync(ulanish, "SELECT referral_detail FROM candidates WHERE id = $1", bekorQilish, botNomzodlari[kanal.OldValue]))
                .ShouldBeNull("bot aniqlik so'ramagan");

            (await QiymatAsync(ulanish, "SELECT name FROM referral_channels WHERE id = $1", bekorQilish, kanal.Id)).ShouldBe(kanal.Name);
            (await QiymatAsync(ulanish, "SELECT asks_detail FROM referral_channels WHERE id = $1", bekorQilish, kanal.Id)).ShouldBe(kanal.AsksDetail, kanal.Name);
            (await QiymatAsync(ulanish, "SELECT is_active FROM referral_channels WHERE id = $1", bekorQilish, kanal.Id)).ShouldBe(true, kanal.Name);
        }

        (await QiymatAsync(ulanish, "SELECT count(*) FROM referral_channels", bekorQilish)).ShouldBe(8L);
        (await QiymatAsync(ulanish, "SELECT referral_channel_id FROM candidates WHERE id = $1", bekorQilish, qolda))
            .ShouldBeNull("anketasiz nomzodda manba yo'q edi — o'ylab topilmaydi");
        (await UstunBormiAsync(ulanish, "candidate_questionnaires", "referral_source", bekorQilish)).ShouldBeFalse("ko'chirilgach olib tashlanadi");
        (await QiymatAsync(ulanish, "SELECT character_maximum_length FROM information_schema.columns WHERE table_name = 'candidates' AND column_name = 'referral_detail'", bekorQilish))
            .ShouldBe(200);

        // Nom bo'yicha unikal — `PostgresFault` taniydigan nom bilan, registrsiz
        (await QiymatAsync(ulanish, "SELECT indexdef FROM pg_indexes WHERE indexname = 'ux_referral_channels_name'", bekorQilish))
            .ShouldNotBeNull().ToString()!.ShouldContain("lower(name)");

        var takror = await Should.ThrowAsync<PostgresException>(() => BajarAsync(
            ulanish,
            "INSERT INTO referral_channels (id, name, is_active, created_at) VALUES ($1, 'TELEGRAM', true, now());",
            bekorQilish,
            Guid.NewGuid()));
        takror.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        takror.ConstraintName.ShouldBe("ux_referral_channels_name");

        // `asks_detail` sukuti bazada — ustunni bilmagan INSERT ham o'tadi
        var hrKanali = Guid.NewGuid();
        await BajarAsync(
            ulanish,
            "INSERT INTO referral_channels (id, name, is_active, created_at) VALUES ($1, 'Radio', true, now());",
            bekorQilish,
            hrKanali);
        (await QiymatAsync(ulanish, "SELECT asks_detail FROM referral_channels WHERE id = $1", bekorQilish, hrKanali)).ShouldBe(false);

        // HR qo'shgan kanal bilan bot nomzodi — Down uni «Boshqa» ga o'giradi
        var radioNomzod = await NomzodQoshAsync(ulanish, bekorQilish);
        await AnketaQoshAsync(ulanish, radioNomzod, eskiQiymat: null, bekorQilish);
        await BajarAsync(ulanish, "UPDATE candidates SET referral_channel_id = $2, referral_detail = 'FM 101' WHERE id = $1;", bekorQilish, radioNomzod, hrKanali);

        // ── Down ─────────────────────────────────────────────────
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        foreach (var kanal in ReferralSeeds.All)
        {
            (await QiymatAsync(ulanish, "SELECT referral_source FROM candidate_questionnaires WHERE candidate_id = $1", bekorQilish, botNomzodlari[kanal.OldValue]))
                .ShouldBe(kanal.OldValue, $"{kanal.Name} → eski {kanal.OldValue}");
        }

        (await QiymatAsync(ulanish, "SELECT referral_source FROM candidate_questionnaires WHERE candidate_id = $1", bekorQilish, radioNomzod))
            .ShouldBe(6, "boshqa kanal → 6 «Boshqa»");
        (await QiymatAsync(ulanish, "SELECT column_default FROM information_schema.columns WHERE table_name = 'candidate_questionnaires' AND column_name = 'referral_source'", bekorQilish))
            .ShouldBeNull("ustun asl holida — sukutsiz");
        (await QiymatAsync(ulanish, "SELECT is_nullable FROM information_schema.columns WHERE table_name = 'candidate_questionnaires' AND column_name = 'referral_source'", bekorQilish))
            .ShouldBe("NO");

        (await UstunBormiAsync(ulanish, "candidates", "referral_channel_id", bekorQilish)).ShouldBeFalse();
        (await UstunBormiAsync(ulanish, "candidates", "referral_detail", bekorQilish)).ShouldBeFalse();
        (await QiymatAsync(ulanish, "SELECT to_regclass('public.referral_channels')::text", bekorQilish)).ShouldBeNull();
        (await QiymatAsync(ulanish, "SELECT count(*) FROM candidates", bekorQilish)).ShouldBe(10L, "Down nomzodlarni o'chirmaydi");

        // ── Yana Up — ikkinchi marta ham toza qo'llanadi va o'sha natijani beradi ──
        await MigratsiyaAsync(baza.UlanishSatri, Migratsiya, bekorQilish);

        foreach (var kanal in ReferralSeeds.All)
        {
            (await QiymatAsync(ulanish, "SELECT referral_channel_id FROM candidates WHERE id = $1", bekorQilish, botNomzodlari[kanal.OldValue]))
                .ShouldBe(kanal.Id, $"qayta Up: eski {kanal.OldValue}");
        }

        (await QiymatAsync(ulanish, "SELECT referral_channel_id FROM candidates WHERE id = $1", bekorQilish, radioNomzod))
            .ShouldBe(ReferralSeeds.Other, "Down 6 ga o'girgan edi — qayta Up «Boshqa» ga ko'chiradi");
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

    /// <summary>
    /// Bot anketasi. <paramref name="eskiQiymat"/> — migratsiyadan OLDINGI <c>referral_source</c>;
    /// <c>null</c> — migratsiyadan KEYIN (ustun yo'q).
    /// </summary>
    private static Task AnketaQoshAsync(NpgsqlConnection ulanish, Guid nomzod, int? eskiQiymat, CancellationToken bekorQilish)
        => eskiQiymat is { } qiymat
            ? BajarAsync(
                ulanish,
                """
                INSERT INTO candidate_questionnaires
                    (id, candidate_id, education, marital_status, russian_level, has_driver_license, expected_salary, referral_source)
                VALUES ($1, $2, 3, 1, 2, false, 2, $3);
                """,
                bekorQilish,
                Guid.NewGuid(),
                nomzod,
                qiymat)
            : BajarAsync(
                ulanish,
                """
                INSERT INTO candidate_questionnaires
                    (id, candidate_id, education, marital_status, russian_level, has_driver_license, expected_salary)
                VALUES ($1, $2, 3, 1, 2, false, 2);
                """,
                bekorQilish,
                Guid.NewGuid(),
                nomzod);

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

        // Ota qatorlarisiz yozish uchun — tashqi kalit triggerlari shu sessiyada ishlamaydi (CHECK va UNIQUE esa ishlaydi).
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
