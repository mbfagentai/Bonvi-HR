using System.Reflection;
using Bonvi.Api.Mcp;
using Bonvi.Domain.Organization;
using Bonvi.Infrastructure.Mcp;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// AI ulanish — «faqat o'qish» kafolatining UCH mustaqil qulfi
/// (<c>docs/SPEC-mcp.md</c> §3, §4, §10).
/// </summary>
/// <remarks>
/// <list type="number">
///   <item>Yozadigan vosita yo'q — vosita konstruktori faqat <see cref="McpToolContext"/> oladi (refleksiya).</item>
///   <item>Baza roli yoza olmaydi va pasport/fayl/sir ustunlarini ko'rmaydi — testning O'ZI
///     shu rol bilan ulanib, SQL yuboradi (PostgreSQL <c>42501</c>).</item>
///   <item>O'qish kontekstida <c>SaveChanges</c> va SELECT bo'lmagan buyruq yuborilmasdan rad etiladi.</item>
/// </list>
/// </remarks>
public sealed class McpReadOnlyTests : IClassFixture<McpHost>
{
    /// <summary>Yozish rad etilganda kutiladigan kodlar: faqat-o'qish tranzaksiyasi (25006) yoki ruxsat yo'q (42501).</summary>
    private static readonly string[] YozishRadKodlari = ["25006", PostgresErrorCodes.InsufficientPrivilege];

    /// <summary>
    /// §4 — «butun jadval»: nomi nozik bo'lmagan HAR BIR ustun ochiq, lekin ruxsat
    /// USTUN darajasida (kelajakdagi nozik ustun o'z-o'zidan ochilmasin).
    /// </summary>
    private static readonly string[] OchiqJadvallar =
    [
        "absence_requests", "absence_types", "attendance_days", "award_types", "candidate_notes",
        "candidate_questionnaires", "candidate_stage_entries", "departments", "directions", "document_types",
        "employee_competencies", "employee_competency_languages", "employee_flags", "employment_events",
        "employments", "exit_interviews", "incentives", "incidents", "legal_entities", "mobile_check_ins",
        "penalty_types", "pipeline_stages", "position_duties", "positions", "probations", "referral_channels", "roles",
        "schedule_assignments", "termination_reasons", "vacancies", "work_schedule_dates",
        "work_schedule_days", "work_schedules", "work_sites",

        // SPEC-blankalar §11: fayl kaliti/nomi va imzo kaliti naqsh bilan yopiq.
        "blank_attachments", "blank_role_assignments", "blank_submission_steps", "blank_submissions",
        "blank_template_versions", "blank_templates",

        // SPEC-mening-ishlarim §12: the process_* tables for the MCP read tools.
        "process_case_steps", "process_cases", "process_gate_overrides", "process_holidays",
        "process_stage_bindings", "process_template_versions", "process_templates",
    ];

    /// <summary>§4 — faqat ustun darajasida ochiq jadvallar va ULARDA YOPIQ bo'lishi shart ustunlar.</summary>
    private static readonly Dictionary<string, string[]> YopiqUstunlar = new(StringComparer.Ordinal)
    {
        ["employees"] = ["pinfl", "passport", "photo_key"],
        ["candidates"] = ["photo_key", "telegram_chat_id"],
        ["employee_documents"] = ["file_key", "file_name", "content_type", "size_bytes", "note"],
        ["attendance_passes"] = ["photo_key", "raw_event", "raw_person_id", "dedupe_key"],
        ["attendance_devices"] = [],
        ["users"] = ["login", "password_hash"],
    };

    /// <summary>Rol yozishga urinadigan buyruqlar — hammasi rad etilishi shart.</summary>
    private static readonly string[] YozishBuyruqlari =
    [
        "INSERT INTO departments (id, name, created_at) VALUES (gen_random_uuid(), 'ai yozdi', now())",
        "UPDATE employees SET first_name = 'buzildi'",
        "DELETE FROM employees",
        "UPDATE users SET is_active = false",
        "DELETE FROM mcp_audit",
        "INSERT INTO mcp_audit (at, user_name, client_id, client_name, tool, arguments, outcome, duration_ms) VALUES (now(), 'x', 'x', 'x', 'x', '', 'ok', 0)",
        "TRUNCATE employees",
        "CREATE TABLE ai_yozdi (id int)",
    ];

    /// <summary>Rol ko'ra olmasligi kerak bo'lgan ma'lumotlar.</summary>
    private static readonly string[] YopiqSorovlar =
    [
        "SELECT pinfl FROM employees LIMIT 1",
        "SELECT passport FROM employees LIMIT 1",
        "SELECT photo_key FROM employees LIMIT 1",
        "SELECT * FROM employees LIMIT 1",
        "SELECT file_key FROM employee_documents LIMIT 1",
        "SELECT file_name FROM employee_documents LIMIT 1",
        "SELECT note FROM employee_documents LIMIT 1",
        "SELECT photo_key FROM candidates LIMIT 1",
        "SELECT telegram_chat_id FROM candidates LIMIT 1",
        "SELECT photo_key FROM attendance_passes LIMIT 1",
        "SELECT raw_event FROM attendance_passes LIMIT 1",
        "SELECT login FROM users LIMIT 1",
        "SELECT password_hash FROM users LIMIT 1",
        "SELECT * FROM users LIMIT 1",
        "SELECT * FROM system_settings LIMIT 1",
        "SELECT * FROM audit_logs LIMIT 1",
        "SELECT * FROM api_keys LIMIT 1",
        "SELECT * FROM refresh_tokens LIMIT 1",
        "SELECT * FROM employee_accounts LIMIT 1",
        "SELECT * FROM employee_refresh_tokens LIMIT 1",
        "SELECT * FROM employee_check_files LIMIT 1",
        "SELECT * FROM candidate_files LIMIT 1",
        "SELECT * FROM attendance_identities LIMIT 1",
        "SELECT * FROM device_enrollments LIMIT 1",
        "SELECT * FROM notification_states LIMIT 1",
        "SELECT * FROM mcp_tokens LIMIT 1",
        "SELECT * FROM mcp_grants LIMIT 1",
        "SELECT * FROM mcp_auth_codes LIMIT 1",
        "SELECT * FROM mcp_clients LIMIT 1",
        "SELECT * FROM mcp_audit LIMIT 1",
        "SELECT * FROM \"__EFMigrationsHistory\" LIMIT 1",
        "SELECT storage_key FROM blank_attachments LIMIT 1",
        "SELECT file_name FROM blank_attachments LIMIT 1",
        "SELECT signature_storage_key FROM blank_submission_steps LIMIT 1",
        "SELECT final_pdf_storage_key FROM blank_submissions LIMIT 1",
        "SELECT * FROM blank_number_counters LIMIT 1",
    ];

    /// <summary>Rol ko'ra olishi kerak bo'lgan ma'lumotlar (vositalar shular bilan ishlaydi).</summary>
    private static readonly string[] OchiqSorovlar =
    [
        "SELECT first_name, last_name, phone, birth_date FROM employees LIMIT 1",
        "SELECT id, employee_id, document_type_id, created_at FROM employee_documents LIMIT 1",
        "SELECT id, first_name, last_name, status FROM candidates LIMIT 1",
        "SELECT id, referral_channel_id, referral_detail FROM candidates LIMIT 1",
        "SELECT * FROM referral_channels LIMIT 1",
        "SELECT id, first_name, last_name, is_active FROM users LIMIT 1",
        "SELECT * FROM departments LIMIT 1",
        "SELECT * FROM roles LIMIT 1",
        "SELECT * FROM employments LIMIT 1",
        "SELECT * FROM vacancies LIMIT 1",
        "SELECT id, template_code, status, answers, computed, missing, return_comment FROM blank_submissions LIMIT 1",
        "SELECT id, kind, content_type, size_bytes FROM blank_attachments LIMIT 1",
        "SELECT id, step_code, actor_role, decision, signer_name, answers_patch FROM blank_submission_steps LIMIT 1",
        "SELECT * FROM blank_template_versions LIMIT 1",
    ];

    private readonly McpHost _host;

    public McpReadOnlyTests(McpHost host) => _host = host;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ────────────────────────────────────────────────────────────
    //  2-qulf: baza roli
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Oqish_roli_yoza_olmaydi()
    {
        await using var ro = new NpgsqlConnection(_host.ReadOnlyConnectionString);
        await ro.OpenAsync(Ct);

        (await SkalyarAsync(ro, "SELECT current_user")).ShouldBe(McpHost.DbRole);
        (await SkalyarAsync(ro, "SHOW transaction_read_only")).ShouldBe("on");

        foreach (var sql in YozishBuyruqlari)
        {
            var xato = await Should.ThrowAsync<PostgresException>(() => BajarAsync(ro, sql), sql);
            xato.SqlState.ShouldBeOneOf(YozishRadKodlari, sql);
        }

        // Sessiyani «yozish» rejimiga o'tkazsa ham — RUXSAT yo'q: ikkinchi qavat mustaqil.
        await BajarAsync(ro, "SET SESSION CHARACTERISTICS AS TRANSACTION READ WRITE");
        (await SkalyarAsync(ro, "SHOW transaction_read_only")).ShouldBe("off");

        foreach (var sql in YozishBuyruqlari)
        {
            var xato = await Should.ThrowAsync<PostgresException>(() => BajarAsync(ro, sql), sql);
            xato.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege, sql);
        }

        // Boshqa rolga o'tib bo'lmaydi (u hech qaysi rolning a'zosi emas).
        var egasi = new NpgsqlConnectionStringBuilder(_host.ConnectionString).Username!;
        var rol = await Should.ThrowAsync<PostgresException>(() => BajarAsync(ro, $"SET ROLE \"{egasi}\""));
        rol.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task Oqish_roli_pasport_fayl_va_sir_ustunlarini_oqiy_olmaydi()
    {
        await using var ro = new NpgsqlConnection(_host.ReadOnlyConnectionString);
        await ro.OpenAsync(Ct);

        foreach (var sql in YopiqSorovlar)
        {
            var xato = await Should.ThrowAsync<PostgresException>(() => BajarAsync(ro, sql), sql);
            xato.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege, sql);
        }

        foreach (var sql in OchiqSorovlar)
        {
            await BajarAsync(ro, sql);
        }
    }

    /// <summary>
    /// Ruxsat ro'yxati SHARTNOMA sifatida: bazadagi HAR BIR jadval uchun rolning
    /// huquqlari §4 dagi bilan bir xil — ro'yxatda yo'q jadval (jumladan kelajakda
    /// qo'shiladigani) butunlay yopiq, SELECT dan boshqa huquq hech qayerda yo'q.
    /// </summary>
    [Fact]
    public async Task Ruxsat_royxati_SPEC_dagi_bilan_bir_xil()
    {
        await using var egasi = new NpgsqlConnection(_host.ConnectionString);
        await egasi.OpenAsync(Ct);

        var jadvallar = new List<string>();

        await using (var buyruq = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY 1", egasi))
        await using (var oquvchi = await buyruq.ExecuteReaderAsync(Ct))
        {
            while (await oquvchi.ReadAsync(Ct))
            {
                jadvallar.Add(oquvchi.GetString(0));
            }
        }

        jadvallar.ShouldContain("employees");
        jadvallar.ShouldContain("mcp_tokens");

        foreach (var jadval in jadvallar)
        {
            var toliq = "public.\"" + jadval + "\"";

            // SELECT dan boshqa huquq — na jadval, na ustun darajasida (ro'yxatdagi birortasi bo'lsa ham `true`).
            (await HuquqAsync(egasi, "has_table_privilege(@rol, @jadval, @huquq)", toliq, "INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER"))
                .ShouldBeFalse($"{jadval}: yozish huquqi");
            (await HuquqAsync(egasi, "has_any_column_privilege(@rol, @jadval, @huquq)", toliq, "INSERT, UPDATE, REFERENCES"))
                .ShouldBeFalse($"{jadval}: ustunga yozish huquqi");

            var butunJadval = await HuquqAsync(egasi, "has_table_privilege(@rol, @jadval, @huquq)", toliq, "SELECT");
            var birorUstun = await HuquqAsync(egasi, "has_any_column_privilege(@rol, @jadval, @huquq)", toliq, "SELECT");

            if (OchiqJadvallar.Contains(jadval, StringComparer.Ordinal))
            {
                butunJadval.ShouldBeFalse($"{jadval}: ruxsat ustun darajasida bo'lishi kerak (jadval darajasida emas)");

                foreach (var ustun in await UstunlarAsync(egasi, jadval))
                {
                    var nozik = System.Text.RegularExpressions.Regex.IsMatch(
                        ustun,
                        McpDatabase.SensitiveColumnPattern,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    (await UstunHuquqiAsync(egasi, toliq, ustun)).ShouldBe(!nozik, $"{jadval}.{ustun}");
                }
            }
            else if (YopiqUstunlar.TryGetValue(jadval, out var yopiqlar))
            {
                butunJadval.ShouldBeFalse($"{jadval} faqat ustun darajasida ochilishi kerak");
                birorUstun.ShouldBeTrue($"{jadval}: hech bir ustun ochilmagan");

                foreach (var ustun in yopiqlar)
                {
                    (await UstunHuquqiAsync(egasi, toliq, ustun)).ShouldBeFalse($"{jadval}.{ustun} YOPIQ bo'lishi kerak");
                }
            }
            else
            {
                birorUstun.ShouldBeFalse($"{jadval} ruxsat ro'yxatida yo'q — butunlay yopiq bo'lishi kerak");
            }
        }

        // Hech bir jadvalda nomi nozik ustun berilmagan (o'z-o'zini tekshirish bilan bir xil qoida).
        await using (var nozik = new NpgsqlCommand(
                         "SELECT count(*) FROM information_schema.column_privileges WHERE grantee = @rol AND column_name ~* @naqsh",
                         egasi))
        {
            nozik.Parameters.AddWithValue("rol", McpHost.DbRole);
            nozik.Parameters.AddWithValue("naqsh", McpDatabase.SensitiveColumnPattern);
            ((long)(await nozik.ExecuteScalarAsync(Ct))!).ShouldBe(0L, "nomi nozik ustun o'qish roliga berilgan");
        }

        // Rol hech narsaga egalik qilmaydi va superuser emas.
        (await SkalyarAsync(egasi, $"SELECT count(*) FROM pg_class WHERE pg_get_userbyid(relowner) = '{McpHost.DbRole}'")).ShouldBe(0L);
        (await SkalyarAsync(egasi, $"SELECT rolsuper FROM pg_roles WHERE rolname = '{McpHost.DbRole}'")).ShouldBe(false);
        (await SkalyarAsync(egasi, $"SELECT has_schema_privilege('{McpHost.DbRole}', 'public', 'CREATE')")).ShouldBe(false);
    }

    [Fact]
    public async Task Ozini_tekshirish_otadi_va_holat_On()
    {
        var baza = _host.Factory.Services.GetRequiredService<McpDatabase>();

        baza.RoleName.ShouldBe(McpHost.DbRole);
        (await baza.SelfTestAsync(Ct)).ShouldBeNull();
        McpDatabase.ClosedProbes.ShouldContain("employees.pinfl");
        McpDatabase.ClosedProbes.ShouldContain("employees.passport");
        McpDatabase.ClosedProbes.ShouldContain("employee_documents.file_key");

        var holat = await _host.Factory.Services.GetRequiredService<McpRuntime>().GetStatusAsync(Ct);
        holat.State.ShouldBe(McpState.On);
    }

    // ────────────────────────────────────────────────────────────
    //  3-qulf: EF qo'riqchilari
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Oqish_konteksti_rol_bilan_ulanadi_va_yozishni_rad_etadi()
    {
        var baza = _host.Factory.Services.GetRequiredService<McpDatabase>();

        await using var kontekst = baza.CreateReadOnlyContext();

        (await kontekst.Database.SqlQueryRaw<string>("SELECT current_user AS \"Value\"").SingleAsync(Ct)).ShouldBe(McpHost.DbRole);
        kontekst.ChangeTracker.QueryTrackingBehavior.ShouldBe(QueryTrackingBehavior.NoTracking);
        (await kontekst.Departments.CountAsync(Ct)).ShouldBeGreaterThanOrEqualTo(0);

        kontekst.Add(LegalEntity.Create("Hech qachon " + McpHost.Belgi()).Value);

        var async = await Should.ThrowAsync<InvalidOperationException>(() => kontekst.SaveChangesAsync(Ct));
        async.Message.ShouldBe(McpDatabase.RefuseWrites.Message);

        var sinxron = Should.Throw<InvalidOperationException>(() => kontekst.SaveChanges());
        sinxron.Message.ShouldBe(McpDatabase.RefuseWrites.Message);

        // Ommaviy yozish va xom SQL — bazaga yetib bormasdan rad etiladi.
        var ommaviy = await Should.ThrowAsync<InvalidOperationException>(
            () => kontekst.Departments.Where(bolim => bolim.Id == Guid.Empty).ExecuteDeleteAsync(Ct));
        ommaviy.Message.ShouldBe(McpDatabase.SelectOnly.Message);

        var yangilash = await Should.ThrowAsync<InvalidOperationException>(
            () => kontekst.Departments.Where(bolim => bolim.Id == Guid.Empty)
                .ExecuteUpdateAsync(setters => setters.SetProperty(bolim => bolim.Name, "buzildi"), Ct));
        yangilash.Message.ShouldBe(McpDatabase.SelectOnly.Message);

        var xom = await Should.ThrowAsync<InvalidOperationException>(
            () => kontekst.Database.ExecuteSqlRawAsync("DELETE FROM departments WHERE false", Ct));
        xom.Message.ShouldBe(McpDatabase.SelectOnly.Message);
    }

    [Fact]
    public async Task Vositalar_oladigan_kontekst_aynan_oqish_roliniki()
    {
        await using var qamrov = _host.Factory.Services.CreateAsyncScope();
        var vositaKonteksti = qamrov.ServiceProvider.GetRequiredService<McpToolContext>();

        (await vositaKonteksti.Db.Database.SqlQueryRaw<string>("SELECT current_user AS \"Value\"").SingleAsync(Ct))
            .ShouldBe(McpHost.DbRole);

        // So'rovning o'z (o'qish-yozish) konteksti — BOSHQA ulanish.
        var asosiy = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        ReferenceEquals(asosiy, vositaKonteksti.Db).ShouldBeFalse();
    }

    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("  select * from employees")]
    [InlineData("-- teg\nSELECT e.id FROM employees AS e")]
    [InlineData("/* izoh */ SELECT 1")]
    [InlineData("WITH a AS (SELECT 1 AS x) SELECT x FROM a")]
    [InlineData("(SELECT 1) UNION ALL (SELECT 2)")]
    [InlineData("SELECT e.updated_at, e.deleted_by FROM employees AS e WHERE e.deleted_at IS NULL")]
    public void SelectOnly_SELECT_ni_otkazadi(string sql)
        => McpDatabase.SelectOnly.IsSelect(sql).ShouldBeTrue(sql);

    [Theory]
    [InlineData("INSERT INTO departments (id, name) VALUES (gen_random_uuid(), 'x')")]
    [InlineData("UPDATE employees SET first_name = 'x'")]
    [InlineData("DELETE FROM employees")]
    [InlineData("WITH a AS (DELETE FROM employees RETURNING id) SELECT * FROM a")]
    [InlineData("WITH a AS (UPDATE employees SET first_name = 'x' RETURNING id) SELECT * FROM a")]
    [InlineData("WITH a AS (INSERT INTO departments (id) VALUES (gen_random_uuid()) RETURNING id) SELECT * FROM a")]
    [InlineData("SELECT * INTO nusxa FROM employees")]
    [InlineData("SELECT 1; DELETE FROM employees")]
    [InlineData("MERGE INTO employees USING departments ON false WHEN NOT MATCHED THEN DO NOTHING")]
    [InlineData("TRUNCATE employees")]
    [InlineData("SET ROLE bonvi")]
    [InlineData("COPY employees TO STDOUT")]
    [InlineData("CALL nimadir()")]
    [InlineData("")]
    [InlineData("-- faqat izoh")]
    [InlineData("WITH a AS (DELETE\nFROM employees RETURNING id) SELECT * FROM a")]
    [InlineData("WITH a AS (INSERT\tINTO departments (id) VALUES (gen_random_uuid()) RETURNING id) SELECT * FROM a")]
    [InlineData("SELECT * INTO\nnusxa FROM employees")]
    [InlineData("WITH a AS (update\r\nemployees SET first_name = 'x' RETURNING id) SELECT * FROM a")]
    public void SelectOnly_yozish_va_boshqa_buyruqlarni_rad_etadi(string sql)
        => McpDatabase.SelectOnly.IsSelect(sql).ShouldBeFalse(sql);

    // ────────────────────────────────────────────────────────────
    //  1-qulf: yozadigan vosita yo'q
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Vosita_konstruktorlari_faqat_McpToolContext_oladi()
    {
        McpSetup.ToolTypes.ShouldNotBeEmpty();

        foreach (var tur in McpSetup.ToolTypes)
        {
            tur.GetCustomAttribute<McpServerToolTypeAttribute>().ShouldNotBeNull(tur.Name);

            var konstruktor = tur.GetConstructors(BindingFlags.Instance | BindingFlags.Public).ShouldHaveSingleItem(tur.Name);
            konstruktor.GetParameters().ShouldHaveSingleItem(tur.Name).ParameterType.ShouldBe(typeof(McpToolContext), tur.Name);

            // Maydonlarda ham boshqa xizmat yo'q (birlamchi konstruktor parametri maydon bo'lib qoladi).
            foreach (var maydon in tur.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                maydon.FieldType.ShouldBe(typeof(McpToolContext), $"{tur.Name}.{maydon.Name}");
            }

            var usullar = tur.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(usul => usul.GetCustomAttribute<McpServerToolAttribute>() is not null)
                .ToList();

            usullar.ShouldNotBeEmpty(tur.Name);

            foreach (var usul in usullar)
            {
                var atribut = usul.GetCustomAttribute<McpServerToolAttribute>()!;
                atribut.ReadOnly.ShouldBeTrue($"{atribut.Name}: ReadOnly");
                atribut.Destructive.ShouldBeFalse($"{atribut.Name}: Destructive");

                foreach (var parametr in usul.GetParameters())
                {
                    var t = parametr.ParameterType;
                    (typeof(DbContext).IsAssignableFrom(t) || t == typeof(IServiceProvider) || t == typeof(McpDatabase))
                        .ShouldBeFalse($"{atribut.Name}: {parametr.Name} — {t.Name}");
                }
            }
        }

        // Vosita kontekstining o'zi ham faqat HTTP konteksti va O'QISH bazasini oladi.
        var kontekstTurlari = typeof(McpToolContext).GetConstructors().ShouldHaveSingleItem()
            .GetParameters().Select(parametr => parametr.ParameterType.FullName)
            .ToList();

        string.Join(", ", kontekstTurlari).ShouldBe(
            typeof(Microsoft.AspNetCore.Http.IHttpContextAccessor).FullName + ", " + typeof(McpDatabase).FullName);
    }

    [Fact]
    public void Taqiqlangan_xossa_nomlari_aniqlanadi()
    {
        foreach (var nom in new[]
                 {
                     "pinfl", "Pinfl", "passport", "passportSeries", "pasport", "jshshir", "fileKey", "file_key",
                     "fileName", "photoKey", "screenshotKey", "storageKey", "passwordHash", "login", "token",
                     "accessToken", "botToken", "apiKey", "secret", "clientSecret", "salt", "otp", "pin",
                 })
        {
            McpToolContext.IsForbiddenName(nom).ShouldBeTrue(nom);
        }

        foreach (var nom in new[] { "firstName", "fullName", "phone", "address", "amount", "salary", "documentType", "department", "note" })
        {
            McpToolContext.IsForbiddenName(nom).ShouldBeFalse(nom);
        }
    }

    // ────────────────────────────────────────────────────────────

    private static async Task BajarAsync(NpgsqlConnection ulanish, string sql)
    {
        await using var buyruq = new NpgsqlCommand(sql, ulanish);
        await buyruq.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<object?> SkalyarAsync(NpgsqlConnection ulanish, string sql)
    {
        await using var buyruq = new NpgsqlCommand(sql, ulanish);
        return await buyruq.ExecuteScalarAsync(Ct);
    }

    private static async Task<bool> HuquqAsync(NpgsqlConnection egasi, string ifoda, string jadval, string huquq)
    {
        await using var buyruq = new NpgsqlCommand("SELECT " + ifoda, egasi);
        buyruq.Parameters.AddWithValue("rol", McpHost.DbRole);
        buyruq.Parameters.AddWithValue("jadval", jadval);
        buyruq.Parameters.AddWithValue("huquq", huquq);
        return (bool)(await buyruq.ExecuteScalarAsync(Ct))!;
    }

    private static async Task<List<string>> UstunlarAsync(NpgsqlConnection egasi, string jadval)
    {
        var ustunlar = new List<string>();

        await using var buyruq = new NpgsqlCommand(
            "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @jadval",
            egasi);
        buyruq.Parameters.AddWithValue("jadval", jadval);

        await using var oquvchi = await buyruq.ExecuteReaderAsync(Ct);

        while (await oquvchi.ReadAsync(Ct))
        {
            ustunlar.Add(oquvchi.GetString(0));
        }

        return ustunlar;
    }

    private static async Task<bool> UstunHuquqiAsync(NpgsqlConnection egasi, string jadval, string ustun)
    {
        await using var buyruq = new NpgsqlCommand("SELECT has_column_privilege(@rol, @jadval, @ustun, 'SELECT')", egasi);
        buyruq.Parameters.AddWithValue("rol", McpHost.DbRole);
        buyruq.Parameters.AddWithValue("jadval", jadval);
        buyruq.Parameters.AddWithValue("ustun", ustun);
        return (bool)(await buyruq.ExecuteScalarAsync(Ct))!;
    }
}
