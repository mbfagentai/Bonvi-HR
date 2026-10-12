using System.Globalization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <c>AuditJurnaliKengaytirildi</c> migratsiyasining MA'LUMOT ustidagi testi
/// (SPEC-rollar-va-audit §12.2.3–§12.2.4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <see cref="BonviApiFactory"/> yetarli emas</b> — sabab
/// <see cref="CandidateFileMigrationTests"/> izohida: bo'sh bazada eski qatorlar
/// yo'q va «eski qator qiymatini saqlaydimi» degan savol umuman tug'ilmaydi.
/// Ishlab chiqarishda esa jurnalda foydalanuvchi boshqaruvining eski yozuvlari bor.
/// </para>
/// <para>
/// <b>Oldingi reliz yangi sxemada ishlashi kerak</b> (kod qaytarilishi, §5.7): u
/// yangi ustunlarni bilmaydi va qatorni ularsiz qo'shadi — sukutlar shuni ta'minlaydi.
/// </para>
/// </remarks>
public sealed class AuditMigrationTests
{
    /// <summary>Kengaytirishdan OLDINGI holat.</summary>
    private const string OldingiMigratsiya = "20261011175423_ApiKeyScope";

    /// <summary>Tekshirilayotgan migratsiya.</summary>
    private const string AuditMigratsiyasi = "20261012115700_AuditJurnaliKengaytirildi";

    private const string TriggerXabari = "audit_logs: yozuvlar o'zgartirilmaydi va o'chirilmaydi";

    private static readonly DateTimeOffset Vaqt = new(2026, 9, 20, 10, 15, 0, TimeSpan.Zero);

    /// <summary>
    /// Eski qatorlar tegilmaydi va «foydalanuvchi» bo'lib qoladi; oldingi reliz yangi ustunlarsiz
    /// qo'shishda davom etadi.
    /// </summary>
    [Fact]
    public async Task Eski_qatorlar_qiymatini_saqlaydi_va_oldingi_reliz_qoshishda_davom_etadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        var eski = await EskiQatorAsync(ulanish, bekorQilish);

        await MigratsiyaAsync(baza.UlanishSatri, AuditMigratsiyasi, bekorQilish);

        await using (var oqish = ulanish.CreateCommand())
        {
            oqish.CommandText = """
                SELECT action, entity_name, entity_id, user_login, new_values, occurred_at,
                       actor_kind, actor_name, actor_role, operation, correlation_id,
                       subject_employee_id, entity_label, changed_fields
                  FROM audit_logs WHERE id = $1;
                """;
            _ = oqish.Parameters.Add(new NpgsqlParameter { Value = eski });

            await using var qator = await oqish.ExecuteReaderAsync(bekorQilish);
            (await qator.ReadAsync(bekorQilish)).ShouldBeTrue();

            qator.GetInt32(0).ShouldBe(1);
            qator.GetString(1).ShouldBe("User");
            qator.GetString(2).ShouldBe("eski-hisob");
            qator.GetString(3).ShouldBe("+998905088888");
            qator.GetString(4).ShouldBe("""{"login":"+998905011111"}""");
            qator.GetFieldValue<DateTimeOffset>(5).ShouldBe(Vaqt);

            qator.GetInt32(6).ShouldBe(1, "eski qatorlar — foydalanuvchi (User)");

            for (var ustun = 7; ustun <= 13; ustun++)
            {
                qator.IsDBNull(ustun).ShouldBeTrue($"yangi ustun eski qatorda bo'sh: {qator.GetName(ustun)}");
            }
        }

        // Oldingi reliz — yangi ustunlarni bilmaydi
        var oldingiReliz = await EskiQatorAsync(ulanish, bekorQilish);
        (await QiymatAsync(ulanish, "SELECT actor_kind FROM audit_logs WHERE id = $1;", bekorQilish, oldingiReliz))
            .ShouldBe(1);
    }

    /// <summary>Trigger <c>UPDATE</c>, <c>DELETE</c> va <c>TRUNCATE</c> ni rad etadi — superuser uchun ham.</summary>
    [Fact]
    public async Task Trigger_ozgartirish_ochirish_va_tozalashni_rad_etadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, AuditMigratsiyasi, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        _ = await EskiQatorAsync(ulanish, bekorQilish);

        foreach (var sql in new[]
                 {
                     "UPDATE audit_logs SET entity_name = 'Soxta';",
                     "DELETE FROM audit_logs;",
                     "TRUNCATE audit_logs;",
                 })
        {
            var xato = await Should.ThrowAsync<PostgresException>(() => BajarAsync(ulanish, sql, bekorQilish));
            xato.MessageText.ShouldContain(TriggerXabari, customMessage: sql);
        }

        (await QiymatAsync(ulanish, "SELECT count(*) FROM audit_logs;", bekorQilish)).ShouldBe(1L);
    }

    /// <summary>
    /// <c>Down</c> (faqat ishlab chiqish uchun) trigger, indeks va ustunlarni oladi, eski
    /// qatorni qoldiradi; qayta <c>Up</c> yana ishlaydi.
    /// </summary>
    [Fact]
    public async Task Down_va_qayta_Up_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        var eski = await EskiQatorAsync(ulanish, bekorQilish);

        await MigratsiyaAsync(baza.UlanishSatri, AuditMigratsiyasi, bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        (await QiymatAsync(
            ulanish,
            "SELECT count(*) FROM information_schema.columns WHERE table_name = 'audit_logs' AND column_name = 'actor_kind';",
            bekorQilish)).ShouldBe(0L);

        (await QiymatAsync(ulanish, "SELECT count(*) FROM pg_trigger WHERE tgname LIKE 'trg_audit_logs_%';", bekorQilish))
            .ShouldBe(0L);

        (await QiymatAsync(ulanish, "SELECT entity_id FROM audit_logs WHERE id = $1;", bekorQilish, eski))
            .ShouldBe("eski-hisob");

        await MigratsiyaAsync(baza.UlanishSatri, AuditMigratsiyasi, bekorQilish);

        (await QiymatAsync(ulanish, "SELECT count(*) FROM pg_trigger WHERE tgname LIKE 'trg_audit_logs_%';", bekorQilish))
            .ShouldBe(2L);
    }

    /// <summary>
    /// Migratsiyadan OLDINGI zaxira `pg_restore --clean` bilan tiklangan holat: jadval qayta
    /// yaratilgan, lekin trigger funksiyasi (eski zaxirada yo'q) bazada qolib ketgan. Keyingi
    /// deploy migratsiyani qayta bajaradi — u yiqilmasligi va trigger yana ishlashi kerak.
    /// </summary>
    [Fact]
    public async Task Eski_zaxiradan_tiklangach_qolgan_funksiya_bilan_qayta_Up_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var baza = await VaqtinchalikBaza.YaratAsync(bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, AuditMigratsiyasi, bekorQilish);
        await MigratsiyaAsync(baza.UlanishSatri, OldingiMigratsiya, bekorQilish);

        await using var ulanish = new NpgsqlConnection(baza.UlanishSatri);
        await ulanish.OpenAsync(bekorQilish);

        // Tiklashdan keyin qolib ketgan funksiya.
        await BajarAsync(ulanish, """
            CREATE FUNCTION audit_logs_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'eski'; END $$;
            """, bekorQilish);

        await MigratsiyaAsync(baza.UlanishSatri, AuditMigratsiyasi, bekorQilish);

        _ = await EskiQatorAsync(ulanish, bekorQilish);
        var xato = await Should.ThrowAsync<PostgresException>(() => BajarAsync(ulanish, "DELETE FROM audit_logs;", bekorQilish));
        xato.MessageText.ShouldContain(TriggerXabari, customMessage: "funksiya yangilangan bo'lishi kerak");
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    /// <summary>Kengaytirishdan oldingi sxemadagi qator — oldingi relizning `AuditLogger` i kabi.</summary>
    private static async Task<Guid> EskiQatorAsync(NpgsqlConnection ulanish, CancellationToken bekorQilish)
    {
        var id = Guid.CreateVersion7();

        await using var buyruq = ulanish.CreateCommand();
        buyruq.CommandText = """
            INSERT INTO audit_logs (id, occurred_at, action, entity_name, entity_id, user_id, user_login, new_values)
            VALUES ($1, $2, 1, 'User', 'eski-hisob', $3, '+998905088888', '{"login":"+998905011111"}');
            """;
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = id });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = Vaqt });
        _ = buyruq.Parameters.Add(new NpgsqlParameter { Value = Guid.NewGuid() });

        _ = await buyruq.ExecuteNonQueryAsync(bekorQilish);
        return id;
    }

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
            // ⚠️ Xavfsizlik to'ri: DROP faqat SHU testlar yaratgan nomga
            // tushishi mumkin. `bonvi_hr` — ishlab chiqish bazasi.
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
