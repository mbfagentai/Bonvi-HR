using System.Data.Common;
using System.Text.RegularExpressions;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Bonvi.Infrastructure.Mcp;

/// <summary>MCP baza sozlamalari (<c>Mcp</c> bo'limi, <c>docs/SPEC-mcp.md</c> §9).</summary>
public sealed class McpDatabaseOptions
{
    public const string SectionName = "Mcp";

    public const string DefaultRole = "bonvi_mcp";

    /// <summary>
    /// O'qish rolining paroli (<c>MCP_DB_PASSWORD</c>). Bo'sh — MCP serverda
    /// sozlanmagan va butunlay yopiq.
    /// </summary>
    public string? DbPassword { get; set; }

    /// <summary>
    /// Rol nomi. Klasterda YAGONA (rol bazaga emas, butun PostgreSQL'ga
    /// tegishli) — shu sababli testlar o'z nomini beradi, aks holda ular dev
    /// bazasining parolini almashtirib yuborardi.
    /// </summary>
    public string DbRole { get; set; } = DefaultRole;

    /// <summary>
    /// Rolning bir vaqtdagi ulanishlar chegarasi (<c>CONNECTION LIMIT</c>). Sukut 8 —
    /// havza bilan teng: AI butun server ulanishlarini egallab, HR'ning o'zini
    /// kirgizmay qo'ya olmaydi. Testlar kattaroq qiymat beradi: u yerda bir nechta
    /// xost BITTA rolni parallel ishlatadi.
    /// </summary>
    public int DbConnectionLimit { get; set; } = DefaultConnectionLimit;

    public const int DefaultConnectionLimit = 8;
}

/// <summary>
/// AI vositalari o'qiydigan baza — UCH mustaqil qulf bilan (<c>docs/SPEC-mcp.md</c> §3).
/// </summary>
/// <remarks>
/// <list type="number">
///   <item>Ulanish <see cref="McpDatabaseOptions.DbRole"/> nomli ALOHIDA rol bilan:
///     u faqat ruxsat ro'yxatidagi jadval/ustunlarni SELECT qila oladi, pasport
///     va fayl ustunlari unga umuman berilmagan, sessiyasi faqat o'qish.</item>
///   <item><c>SaveChanges</c> ni rad etuvchi interceptor.</item>
///   <item>Faqat <c>SELECT</c>/<c>WITH … SELECT</c> ni o'tkazuvchi buyruq interceptori.</item>
/// </list>
/// <para>
/// <b>Nega <see cref="AppDbContext"/> ning O'ZI, alohida kontekst turi emas.</b>
/// Ikkinchi <c>DbContext</c> turi paydo bo'lsa, har bir <c>dotnet ef migrations …</c>
/// buyrug'i «More than one DbContext was found» bilan yiqiladi. Bu yerda faqat
/// OPSIYALAR boshqa — model, jadval nomlari va so'rovlar o'sha-o'sha.
/// </para>
/// <para>
/// Bu sinf Infrastructure'da, chunki u Npgsql bilan ishlaydi — Api qatlami
/// drayverga to'g'ridan-to'g'ri bog'lanmaydi (arxitektura testi).
/// </para>
/// </remarks>
public sealed partial class McpDatabase
{
    private const int MaxPoolSize = 8;

    private const int MaxProvisionAttempts = 12;

    private readonly string? _mainConnectionString;
    private readonly string? _readOnlyConnectionString;

    private DbContextOptions<AppDbContext>? _readOnlyOptions;
    private DbContextOptions<AppDbContext>? _storeOptions;

    public McpDatabase(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new McpDatabaseOptions();
        configuration.GetSection(McpDatabaseOptions.SectionName).Bind(options);

        _mainConnectionString = configuration.GetConnectionString("Default");
        RoleName = string.IsNullOrWhiteSpace(options.DbRole) ? McpDatabaseOptions.DefaultRole : options.DbRole.Trim();

        if (!RoleNamePattern().IsMatch(RoleName))
        {
            ConfigurationProblem = "«Mcp:DbRole» faqat kichik lotin harflari, raqam va «_» dan iborat bo'lsin";
            return;
        }

        if (string.IsNullOrWhiteSpace(options.DbPassword))
        {
            ConfigurationProblem = "«Mcp:DbPassword» (MCP_DB_PASSWORD) berilmagan";
            return;
        }

        if (string.IsNullOrWhiteSpace(_mainConnectionString))
        {
            ConfigurationProblem = "«ConnectionStrings:Default» bo'sh";
            return;
        }

        // Rol nomi ilovaning O'Z foydalanuvchisi bo'lsa, tayyorlash uning
        // superuser huquqini olib, parolini almashtirib, butun tizimni
        // to'xtatardi — konfiguratsiya xatosi qimmatga tushmasin.
        if (string.Equals(new NpgsqlConnectionStringBuilder(_mainConnectionString).Username, RoleName, StringComparison.Ordinal))
        {
            ConfigurationProblem = "«Mcp:DbRole» ilovaning asosiy baza foydalanuvchisi bilan bir xil bo'lmasin";
            return;
        }

        Password = options.DbPassword;
        ConnectionLimit = options.DbConnectionLimit > 0 ? options.DbConnectionLimit : McpDatabaseOptions.DefaultConnectionLimit;
        _readOnlyConnectionString = BuildReadOnlyConnectionString(
            _mainConnectionString,
            RoleName,
            options.DbPassword,
            Math.Min(ConnectionLimit, MaxPoolSize));
    }

    /// <summary>O'qish rolining nomi.</summary>
    public string RoleName { get; }

    /// <summary><c>null</c> — sozlama to'liq; aks holda nima yetishmasligi (sir yo'q).</summary>
    public string? ConfigurationProblem { get; }

    public bool IsConfigured => ConfigurationProblem is null;

    private string? Password { get; }

    private int ConnectionLimit { get; } = McpDatabaseOptions.DefaultConnectionLimit;

    /// <summary>
    /// O'qish konteksti — <see cref="McpDatabaseOptions.DbRole"/> ulanishi, kuzatuvsiz,
    /// yozish rad etiladi. Chaqiruvchi yopadi.
    /// </summary>
    public AppDbContext CreateReadOnlyContext()
    {
        if (_readOnlyConnectionString is null)
        {
            throw new InvalidOperationException("MCP o'qish bazasi sozlanmagan.");
        }

        _readOnlyOptions ??= new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_readOnlyConnectionString)
            .UseSnakeCaseNamingConvention()
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .AddInterceptors(RefuseWrites.Instance, SelectOnly.Instance)
            .Options;

        return new AppDbContext(_readOnlyOptions);
    }

    /// <summary>
    /// <c>mcp_*</c> jadvallariga yozish uchun kontekst — ASOSIY ulanish, lekin
    /// interceptorlarSIZ.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega so'rovning o'z <see cref="AppDbContext"/> i emas.</b> Unda
    /// <c>DataChangeInterceptor</c> bor va u har bir saqlashda «ma'lumot
    /// o'zgardi» signalini uyg'otadi — bildirishnoma oqimlari har bir ulangan
    /// HR uchun qayta hisoblanadi. AI esa daqiqasiga o'nlab chaqiruv qiladi va
    /// har biri jurnalga yoziladi: HR'ning qo'ng'irog'i bekorga aylanib turardi.
    /// <c>mcp_*</c> jadvallari bildirishnomaga ta'sir qilmaydi, ya'ni signal
    /// bu yerda faqat zarar.
    /// </para>
    /// <para>
    /// Bu kontekst FAQAT <c>mcp_*</c> jadvallariga yoziladi — boshqa jadvalga
    /// yozuv auditsiz o'tib ketardi (<c>AuditableInterceptor</c> ham yo'q).
    /// </para>
    /// </remarks>
    public AppDbContext CreateStoreContext()
    {
        if (string.IsNullOrWhiteSpace(_mainConnectionString))
        {
            throw new InvalidOperationException("«ConnectionStrings:Default» bo'sh.");
        }

        _storeOptions ??= new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_mainConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(_storeOptions);
    }

    /// <summary>
    /// O'qish rolini ASOSIY ulanish bilan tayyorlaydi: yaratadi (bo'lmasa),
    /// parolini va cheklovlarini qo'yadi, ruxsat ro'yxatini qayta qo'llaydi.
    /// </summary>
    /// <returns><c>null</c> — tayyor; aks holda sabab (sirsiz).</returns>
    /// <remarks>
    /// Har ishga tushishda bajariladi: migratsiya ruxsat ro'yxatini o'zgartirgan
    /// bo'lsa (yangi jadval), u shu yerda kuchga kiradi. Parol SQL matniga
    /// YOZILMAYDI — u parametr bo'lib sessiya sozlamasiga tushadi va
    /// <c>format('%L')</c> bilan qo'llanadi (log va injeksiya xavfisiz).
    /// </remarks>
    public async Task<string?> ProvisionAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return ConfigurationProblem;
        }

        /*
          IKKI qisqa tranzaksiya, har biri o'z qayta urinishi bilan:

            1. ROL (butun klasterga tegishli) — yaratish, parol, cheklovlar.
               Shu qadam bir vaqtda ishga tushgan xostlar orasida to'qnashadi
               («tuple concurrently updated», XX000), shu sababli u ATAYLAB
               qisqa: ruxsat ro'yxati shu tranzaksiyada qo'llansa, rol qatori
               o'nlab GRANT davomida qulflangan turardi.
            2. BAZA — ulanish, sxema va jadval ruxsatlari. Har bir bazada
               o'zining, ya'ni boshqa xostlar bilan to'qnashmaydi.

          Tasodifiy kutish — bir vaqtda yiqilganlar yana bir vaqtda
          urinmasin. Advisory qulf bu yerda yordam bermaydi: u bazaga
          tegishli, rol esa butun klasterga.
        */
        if (await UnsafeExistingRoleAsync(cancellationToken).ConfigureAwait(false) is { } unsafeRole)
        {
            return unsafeRole;
        }

        var limit = ConnectionLimit.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var roleFailure = await RunWithRetriesAsync(
                async (connection, transaction) =>
                {
                    await ExecuteAsync(connection, transaction, ProvisionSetSql, ("role", RoleName), ("password", Password!), ("limit", limit))
                        .ConfigureAwait(false);
                    await ExecuteAsync(connection, transaction, ProvisionRoleSql).ConfigureAwait(false);
                    await ExecuteAsync(connection, transaction, ProvisionClearSql).ConfigureAwait(false);
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (roleFailure is not null)
        {
            return "o'qish rolini tayyorlab bo'lmadi (" + roleFailure + ")";
        }

        var grantFailure = await RunWithRetriesAsync(
                async (connection, transaction) =>
                {
                    await ExecuteAsync(connection, transaction, ProvisionRoleNameSql, ("role", RoleName)).ConfigureAwait(false);
                    await ExecuteAsync(connection, transaction, ProvisionGrantsSql).ConfigureAwait(false);
                },
                cancellationToken)
            .ConfigureAwait(false);

        return grantFailure switch
        {
            null => null,
            UndefinedFunctionMarker => "ruxsat ro'yxati funksiyasi topilmadi — migratsiya qo'llanmagan",
            _ => "o'qish roliga ruxsatlarni berib bo'lmadi (" + grantFailure + ")",
        };
    }

    private const string UndefinedFunctionMarker = "42883";

    /// <summary>
    /// Shu nomdagi rol ALLAQACHON bor va u superuser, a'zolari bor yoki
    /// obyektlarga egalik qiladi — demak u boshqa maqsadda yaratilgan; unga
    /// tegilmaydi (parol va huquqlari almashtirilsa, o'sha narsa buzilardi).
    /// </summary>
    private async Task<string?> UnsafeExistingRoleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_mainConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = new NpgsqlCommand(UnsafeRoleSql, connection);
            command.Parameters.AddWithValue("role", RoleName);

            var unsafeRole = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            return unsafeRole is true
                ? $"«{RoleName}» roli allaqachon bor va u superuser, boshqa rollarga a'zo qilingan yoki obyektlarga ega — tegilmadi"
                : null;
        }
        catch (NpgsqlException exception)
        {
            return "rolni tekshirib bo'lmadi (" + Describe(exception) + ")";
        }
    }

    private const string UnsafeRoleSql = """
        SELECT EXISTS (
            SELECT 1 FROM pg_roles r
             WHERE r.rolname = @role
               AND (r.rolsuper
                    OR EXISTS (SELECT 1 FROM pg_auth_members m WHERE m.roleid = r.oid)
                    OR EXISTS (SELECT 1 FROM pg_class c WHERE c.relowner = r.oid)))
        """;

    /// <returns><c>null</c> — muvaffaqiyat; aks holda SQLSTATE yoki istisno turi (sirsiz).</returns>
    private async Task<string?> RunWithRetriesAsync(
        Func<NpgsqlConnection, NpgsqlTransaction, Task> work,
        CancellationToken cancellationToken)
    {
        Exception? last = null;

        for (var attempt = 0; attempt < MaxProvisionAttempts; attempt++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_mainConnectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

                await work(connection, transaction).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedFunction)
            {
                return UndefinedFunctionMarker;
            }
            catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException)
            {
                last = exception;
                var pause = 150 * (attempt + 1) + Random.Shared.Next(0, 500);
                await Task.Delay(TimeSpan.FromMilliseconds(pause), cancellationToken).ConfigureAwait(false);
            }
        }

        // Istisno matni qaytarilmaydi: unda ulanish satri bo'lishi mumkin.
        return Describe(last);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        params (string Name, string Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// O'qish ulanishining O'ZINI tekshiradi: rol aynan da'vo qilgandek — faqat
    /// o'qiydi, hech narsaga egalik qilmaydi, pasport va fayl ustunlarini ko'ra
    /// olmaydi.
    /// </summary>
    /// <returns><c>null</c> — hammasi joyida; aks holda sabab.</returns>
    public async Task<string?> SelfTestAsync(CancellationToken cancellationToken = default)
    {
        if (_readOnlyConnectionString is null)
        {
            return ConfigurationProblem;
        }

        try
        {
            await using var connection = new NpgsqlConnection(_readOnlyConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using (var command = new NpgsqlCommand(SelfTestSql, connection) { Parameters = { new("sensitive", SensitiveColumnPattern) } })
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                await reader.ReadAsync(cancellationToken).ConfigureAwait(false);

                var user = reader.GetString(0);
                if (!string.Equals(user, RoleName, StringComparison.Ordinal))
                {
                    return $"ulanish «{user}» nomidan";
                }

                if (reader.GetBoolean(1))
                {
                    return "rol superuser";
                }

                if (reader.GetString(2) != "on")
                {
                    return "sessiya faqat o'qish rejimida emas";
                }

                if (reader.GetBoolean(3))
                {
                    return "rol public sxemada obyekt yarata oladi";
                }

                if (reader.GetInt64(4) > 0)
                {
                    return "rolda SELECT dan boshqa jadval ruxsati bor";
                }

                if (reader.GetInt64(5) > 0)
                {
                    return "rolda SELECT dan boshqa ustun ruxsati bor";
                }

                if (reader.GetInt64(6) > 0)
                {
                    return "rol obyektlarga egalik qiladi";
                }

                if (reader.GetInt64(7) > 0)
                {
                    return "rol boshqa rolning a'zosi";
                }

                if (reader.GetInt64(8) > 0)
                {
                    return "rolga nomi nozik (kalit, fayl, rasm, pasport…) ustun berilgan";
                }
            }

            // Eng muhim tekshiruv — MAZMUN bo'yicha: yopiq ustun haqiqatan
            // yopiqmi. `LIMIT 0` — qator o'qilmaydi, ruxsat esa rejalashtirishda
            // tekshiriladi.
            foreach (var probe in ClosedProbes)
            {
                if (await CanSelectAsync(connection, probe, cancellationToken).ConfigureAwait(false))
                {
                    return $"yopiq bo'lishi kerak bo'lgan ma'lumot ochiq: {probe}";
                }
            }

            return null;
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException)
        {
            return "o'qish ulanishi ishlamadi (" + Describe(exception) + ")";
        }
    }

    /// <summary>
    /// Nomi shunga mos ustun o'qish roliga HECH QACHON berilmaydi — migratsiyadagi
    /// <c>mcp_apply_ro_grants</c> ning <c>sensitive</c> o'zgaruvchisi bilan BIR XIL.
    /// O'z-o'zini tekshirish berilgan ustunlar orasida bunday nom topsa, MCP yopiladi.
    /// </summary>
    public const string SensitiveColumnPattern =
        "(key|file|photo|passport|pasport|pinfl|jshshir|token|secret|hash|screenshot|selfie|attachment|password|login)";

    /// <summary>
    /// Rol ko'ra OLMASLIGI kerak bo'lgan ma'lumotlar — o'z-o'zini tekshirish
    /// har birini sinab ko'radi (<c>docs/SPEC-mcp.md</c> §3, §4).
    /// </summary>
    public static IReadOnlyList<string> ClosedProbes { get; } =
    [
        "employees.pinfl",
        "employees.passport",
        "employees.photo_key",
        "employee_documents.file_key",
        "employee_documents.file_name",
        "employee_check_files.id",
        "candidate_files.id",
        "attendance_passes.photo_key",
        "users.password_hash",
        "users.login",
        "system_settings.value",
        "audit_logs.id",
        "api_keys.id",
        "mcp_tokens.id",
    ];

    private static async Task<bool> CanSelectAsync(NpgsqlConnection connection, string probe, CancellationToken cancellationToken)
    {
        var dot = probe.IndexOf('.', StringComparison.Ordinal);
        var sql = $"SELECT {probe[(dot + 1)..]} FROM public.{probe[..dot]} LIMIT 0";

        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.InsufficientPrivilege
                                                      or PostgresErrorCodes.UndefinedTable
                                                      or PostgresErrorCodes.UndefinedColumn)
        {
            return false;
        }
    }

    private static string BuildReadOnlyConnectionString(string main, string role, string password, int poolSize)
    {
        var builder = new NpgsqlConnectionStringBuilder(main)
        {
            Username = role,
            Password = password,

            // Xato tafsiloti qator qiymatlarini logga olib chiqishi mumkin.
            IncludeErrorDetail = false,
            Timeout = 5,
            CommandTimeout = 30,
            ApplicationName = "Bonvi.Mcp",
            MaxPoolSize = poolSize,

            // Bo'sh ulanish havzada uzoq turmasin: rol chegarasi bir nechta
            // jarayon (testlar, dev nusxalari) orasida UMUMIY.
            ConnectionIdleLifetime = 60,
            MinPoolSize = 0,
            Options = "-c default_transaction_read_only=on -c statement_timeout=30000",
        };

        return builder.ConnectionString;
    }

    private static string Describe(Exception? exception) => exception switch
    {
        PostgresException postgres => postgres.SqlState,
        null => "noma'lum",
        _ => exception.GetType().Name,
    };

    [GeneratedRegex("^[a-z_][a-z0-9_]{0,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex RoleNamePattern();

    /*
      Rol nomi va paroli PARAMETR bo'lib keladi va sessiya sozlamasiga
      qo'yiladi; `DO` bloki ularni `current_setting` dan o'qiydi va
      identifikatorni `%I`, parolni `%L` bilan qo'yadi. Shu tufayli SQL
      matnida na nom, na parol bor.

      `CONNECTION LIMIT` — sukut 8, ulanishlar havzasi bilan bir xil: AI butun
      server ulanishlarini egallab, HR'ning o'zini kirgizmay qo'ya olmaydi
      (`McpDatabaseOptions.DbConnectionLimit`). `::int` — qiymat SQL'ga son
      bo'lib tushadi, matn bo'lib emas.
    */
    private const string ProvisionSetSql =
        "SELECT set_config('bonvi.mcp_role', @role, true), set_config('bonvi.mcp_password', @password, true), "
        + "set_config('bonvi.mcp_limit', @limit, true)";

    private const string ProvisionClearSql = "SELECT set_config('bonvi.mcp_password', '', true)";

    private const string ProvisionRoleNameSql = "SELECT set_config('bonvi.mcp_role', @role, true)";

    private const string ProvisionRoleSql = """
        DO $provision$
        DECLARE
            r text := current_setting('bonvi.mcp_role');
        BEGIN
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
                    EXECUTE format('CREATE ROLE %I LOGIN', r);
                END IF;
            EXCEPTION WHEN duplicate_object OR unique_violation THEN
                NULL;
            END;

            -- Xato bo'lsa PostgreSQL bajarilgan buyruq matnini (ya'ni PAROLni)
            -- server jurnaliga yozib qo'yardi. Ushlab, matnsiz qayta ko'taramiz;
            -- SQLSTATE saqlanadi — qayta urinish mantig'i unga qaraydi.
            BEGIN
                EXECUTE format(
                    'ALTER ROLE %I WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS CONNECTION LIMIT %s PASSWORD %L',
                    r, current_setting('bonvi.mcp_limit')::int, current_setting('bonvi.mcp_password'));
            EXCEPTION WHEN OTHERS THEN
                RAISE EXCEPTION 'mcp: o''qish rolini sozlab bo''lmadi' USING ERRCODE = SQLSTATE;
            END;
            EXECUTE format('ALTER ROLE %I SET default_transaction_read_only = on', r);
            EXECUTE format('ALTER ROLE %I SET statement_timeout = %L', r, '30s');
        END
        $provision$
        """;

    private const string ProvisionGrantsSql = """
        DO $grants$
        DECLARE
            r text := current_setting('bonvi.mcp_role');
        BEGIN
            EXECUTE format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), r);
            EXECUTE format('GRANT USAGE ON SCHEMA public TO %I', r);
            EXECUTE format('REVOKE CREATE ON SCHEMA public FROM %I', r);
            PERFORM public.mcp_apply_ro_grants(r);
        END
        $grants$
        """;

    private const string SelfTestSql = """
        SELECT current_user::text,
               (SELECT rolsuper FROM pg_roles WHERE rolname = current_user),
               current_setting('transaction_read_only'),
               has_schema_privilege(current_user, 'public', 'CREATE'),
               (SELECT count(*) FROM information_schema.role_table_grants
                 WHERE grantee = current_user AND privilege_type <> 'SELECT'),
               (SELECT count(*) FROM information_schema.column_privileges
                 WHERE grantee = current_user AND privilege_type <> 'SELECT'),
               (SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                 WHERE pg_get_userbyid(c.relowner) = current_user),
               (SELECT count(*) FROM pg_auth_members m
                 WHERE m.member = (SELECT oid FROM pg_roles WHERE rolname = current_user)),
               (SELECT count(*) FROM information_schema.column_privileges
                 WHERE grantee = current_user AND column_name ~* @sensitive)
        """;

    /// <summary>Har qanday <c>SaveChanges</c> o'qish kontekstida darhol yiqiladi.</summary>
    public sealed class RefuseWrites : SaveChangesInterceptor
    {
        public static readonly RefuseWrites Instance = new();

        public const string Message = "MCP o'qish bazasi: yozishga urinish rad etildi.";

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
            throw new InvalidOperationException(Message);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(Message);
    }

    /// <summary>
    /// Ulanishga faqat <c>SELECT</c> / <c>WITH … SELECT</c> yetib boradi. Qolgan
    /// hammasi (xom SQL, <c>ExecuteUpdate</c>/<c>ExecuteDelete</c>) yuborilishidan
    /// OLDIN rad etiladi.
    /// </summary>
    public sealed partial class SelectOnly : DbCommandInterceptor
    {
        public static readonly SelectOnly Instance = new();

        public const string Message = "MCP o'qish bazasi: faqat SELECT so'rovlariga ruxsat bor.";

        public static bool IsSelect(string sql)
        {
            ArgumentNullException.ThrowIfNull(sql);

            var text = sql.AsSpan().TrimStart();

            // EF qo'yishi mumkin bo'lgan izohlarni (teglar) o'tkazib yuboramiz.
            while (true)
            {
                if (text.StartsWith("--", StringComparison.Ordinal))
                {
                    var newline = text.IndexOf('\n');
                    text = newline < 0 ? [] : text[(newline + 1)..].TrimStart();
                    continue;
                }

                if (text.StartsWith("/*", StringComparison.Ordinal))
                {
                    var end = text.IndexOf("*/", StringComparison.Ordinal);
                    text = end < 0 ? [] : text[(end + 2)..].TrimStart();
                    continue;
                }

                break;
            }

            if (text.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("(SELECT", StringComparison.OrdinalIgnoreCase))
            {
                return !ContainsWrite(text);
            }

            // Ma'lumot o'zgartiruvchi CTE (WITH x AS (DELETE …)) ham yozish.
            return text.StartsWith("WITH", StringComparison.OrdinalIgnoreCase) && !ContainsWrite(text);
        }

        /*
          Kalit so'z SO'Z CHEGARASI bilan izlanadi, bo'shliq bilan emas:
          `DELETE\nFROM` yoki `INSERT\tINTO` ham yozish. Ilgari faqat
          «DELETE » (bo'shliq bilan) qidirilardi va yangi qator undan o'tib
          ketardi. `updated_at` kabi ustunlar mos kelmaydi — `\b` ichida `_`
          so'zning davomi.
        */
        private static bool ContainsWrite(ReadOnlySpan<char> sql) => WriteKeyword().IsMatch(sql);

        [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|MERGE|TRUNCATE|INTO)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex WriteKeyword();

        private static void Check(DbCommand command)
        {
            if (!IsSelect(command.CommandText))
            {
                throw new InvalidOperationException(Message);
            }
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Check(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Check(command);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Check(command);
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Check(command);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result) =>
            throw new InvalidOperationException(Message);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(Message);
    }
}
