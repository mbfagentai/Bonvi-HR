using System.Collections.Concurrent;
using Bonvi.Application.Recruiting.Council;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Test uchun to'liq API'ni ko'taradi va uni <b>haqiqiy PostgreSQL</b> ga ulaydi.
/// </summary>
/// <remarks>
/// <para>
/// Har bir test yig'masi uchun <b>yangi vaqtinchalik baza</b> yaratiladi
/// (<c>bonvi_test_xxxxx</c>) va tugagach o'chiriladi. Testlar bir-biriga
/// va development bazasiga aralashmaydi.
/// </para>
/// <para>
/// Nega mock emas: mock qilingan repozitoriy indeks, unikallik cheklovi,
/// tranzaksiya va migratsiya xatolarini <b>ko'rsatmaydi</b>. HR tizimida
/// aynan shular eng ko'p muammo keltiradi (masalan JSHSHIR takrorlanishi).
/// </para>
/// <para>
/// <b>Talab:</b> <c>./bonvi up</c> ishlab turishi kerak — testlar compose'dagi
/// PostgreSQL'dan foydalanadi. <c>./bonvi test</c> ulanishni avtomatik uzatadi.
/// </para>
/// </remarks>
/// <summary>The council's Telegram group in tests: everyone is in it unless told otherwise.</summary>
public sealed class FakeTelegramGroup : ITelegramGroupMembership
{
    private readonly ConcurrentDictionary<long, TelegramGroupStatus> _people = new();

    public void Set(long userId, TelegramGroupStatus status) => _people[userId] = status;

    public Task<TelegramGroupStatus> CheckAsync(string botToken, long chatId, long userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_people.GetValueOrDefault(userId, TelegramGroupStatus.Member));
}

public sealed class BonviApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Who is in the council's group, for this host.</summary>
    public FakeTelegramGroup TelegramGroup { get; } = new();

    /// <summary>Compose'dagi PostgreSQL. <c>./bonvi test</c> buni muhitdan beradi.</summary>
    private static readonly string AdminConnectionString =
        Environment.GetEnvironmentVariable("TEST_POSTGRES")
        ?? "Host=postgres;Port=5432;Database=postgres;Username=bonvi;Password=bonvi_dev_parol";

    /// <summary>
    /// Har bir test yig'masi uchun noyob baza nomi.
    /// </summary>
    /// <remarks>
    /// DIQQAT: bu yerda <c>Guid.NewGuid()</c> (v4) ishlatiladi, <c>CreateVersion7()</c> emas.
    /// v7 ning boshlang'ich 48 biti — vaqt tamg'asi, ya'ni birinchi 12 belgi
    /// bir millisekundda yaratilgan GUID'larda BIR XIL bo'ladi. Test sinflari
    /// parallel ishga tushgani uchun bu baza nomlari to'qnashishiga olib keladi.
    /// v4 esa to'liq tasodifiy.
    /// </remarks>
    private readonly string _databaseName =
        "bonvi_test_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];

    /// <summary>Testlarda ishlatiladigan administrator logini.</summary>
    public const string TestAdminLogin = "905088888";

    /// <summary>Testlarda ishlatiladigan administrator paroli.</summary>
    public const string TestAdminPassword = "Test_Bonvi_2026";

    /// <summary>Telegram bot XIZMAT hisobining logini (test uchun).</summary>
    /// <remarks>
    /// Bot hisobi har bir test bazasida yaratiladi. Nega hammasida:
    /// u qo'shimcha holat yaratmaydi (rol allaqachon urug'lantiriladi,
    /// «atigi bitta» qoidasi unga tegishli emas), lekin usiz
    /// <c>SettingsApiTests</c> tokenni o'qiy oladigan yagona hisobga
    /// kira olmasdi — va o'sha test aynan «bu endpoint kimga ochiq?»
    /// degan savolni tekshiradi.
    /// </remarks>
    public const string TestBotLogin = "905077777";

    /// <summary>Bot xizmat hisobining paroli (test uchun).</summary>
    public const string TestBotPassword = "Test_Bot_2026";

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await using (var admin = new NpgsqlConnection(AdminConnectionString))
        {
            await admin.OpenAsync().ConfigureAwait(false);

            await using var create = admin.CreateCommand();
            create.CommandText = $"""CREATE DATABASE "{_databaseName}";""";
            await create.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Database = _databaseName,
        };

        ConnectionString = builder.ConnectionString;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);

        // Vaqtinchalik bazani o'chirish — ochiq ulanishlar majburan uziladi
        await using (var admin = new NpgsqlConnection(AdminConnectionString))
        {
            await admin.OpenAsync().ConfigureAwait(false);

            await using var drop = admin.CreateCommand();
            drop.CommandText = $"""DROP DATABASE IF EXISTS "{_databaseName}" WITH (FORCE);""";
            await drop.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");

        // No test talks to Telegram: who is in the council's group is decided here.
        builder.ConfigureTestServices(services => services.AddSingleton<ITelegramGroupMembership>(TelegramGroup));

        // UseSetting — xost konfiguratsiyasiga yozadi va appsettings.json'dan
        // USTUN turadi. ConfigureAppConfiguration bilan qo'shilgan manba esa
        // appsettings.json'dan keyin emas, undan OLDIN o'qiladi va bosib ketiladi.
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);

        // Redis testda ishlatilmaydi — xotiradagi keshga tushib ketadi
        builder.UseSetting("ConnectionStrings:Redis", string.Empty);

        builder.UseSetting("Jwt:SigningKey", "test_uchun_kamida_32_belgili_maxfiy_kalit_1234");
        builder.UseSetting("Storage:RootPath", Path.Combine(Path.GetTempPath(), "bonvi-test-uploads"));

        // Testda baza bo'sh: migratsiya qo'llanadi, rollar urug'lantiriladi
        // va birinchi administrator yaratiladi — xuddi haqiqiy ishga tushishdagidek.
        // Muhit «Testing» bo'lgani uchun AutoMigrate ni ataylab yoqamiz.
        builder.UseSetting("Bootstrap:AutoMigrate", "true");
        builder.UseSetting("Bootstrap:AdminLogin", TestAdminLogin);
        builder.UseSetting("Bootstrap:AdminPassword", TestAdminPassword);
        builder.UseSetting("Bootstrap:AdminFullName", "Test Administrator Testovich");

        builder.UseSetting("Bootstrap:BotLogin", TestBotLogin);
        builder.UseSetting("Bootstrap:BotPassword", TestBotPassword);
        builder.UseSetting("Bootstrap:BotFullName", "Telegram bot");
    }
}
