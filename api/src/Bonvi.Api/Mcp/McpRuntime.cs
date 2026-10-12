using Bonvi.Domain.Settings;
using Bonvi.Infrastructure.Mcp;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Api.Mcp;

/// <summary>MCP holati (<c>docs/SPEC-mcp.md</c> §2.2).</summary>
public enum McpState
{
    /// <summary>Serverda sozlanmagan: parol yoki ochiq manzil berilmagan.</summary>
    NotConfigured = 1,

    /// <summary>Ishga tushish tekshiruvi hali tugamagan.</summary>
    Pending = 2,

    /// <summary>O'qish rolini tayyorlash yoki o'z-o'zini tekshirish yiqildi.</summary>
    Faulty = 3,

    /// <summary>Hammasi tayyor, lekin egasi o'chirib qo'ygan (sukut).</summary>
    Off = 4,

    /// <summary>Ishlayapti.</summary>
    On = 5,
}

/// <summary>Holat va (bo'lsa) sabab — sirsiz, egasiga ko'rsatiladi.</summary>
public sealed record McpStatus(McpState State, string? Reason);

/// <summary>
/// MCP ochiqmi — har bir <c>/mcp</c> va <c>/oauth/*</c> so'rovida so'raladi.
/// YOPIQ-SUKUT: biror shart bajarilmasa javob 503/404, o'qish-yozish ulanishiga
/// qaytish YO'Q.
/// </summary>
/// <remarks>
/// <para>
/// Uch shart: (1) server sozlangan, (2) o'qish roli tayyorlangan va o'z-o'zini
/// tekshirishdan o'tgan, (3) egasi «Tizim sozlamalari» da yoqqan.
/// </para>
/// <para>
/// O'chirgich bazadan o'qiladi va 5 soniya keshlanadi: AI daqiqasiga o'nlab
/// chaqiruv qiladi, har biriga alohida so'rov ortiqcha. O'chirilgandan keyin
/// eng ko'pi 5 soniyada kuchga kiradi; shu jarayondagi o'zgarish esa darhol
/// (<see cref="SetEnabled"/>).
/// </para>
/// </remarks>
public sealed partial class McpRuntime : IDisposable
{
    private static readonly TimeSpan SwitchCacheLifetime = TimeSpan.FromSeconds(5);

    private readonly McpDatabase _database;
    private readonly McpOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<McpRuntime> _logger;
    private readonly SemaphoreSlim _checkLock = new(1, 1);

    private volatile McpStatus _infrastructure;
    // Havola turi — bir nechta oqim bir vaqtda o'qiganda yarim yozilgan
    // qiymat ko'rinmasin (struct maydoni bunday kafolat bermaydi).
    private volatile SwitchState? _switch;

    public McpRuntime(
        McpDatabase database,
        McpOptions options,
        IHostEnvironment environment,
        ILogger<McpRuntime> logger)
    {
        _database = database;
        _options = options;
        _environment = environment;
        _logger = logger;
        _infrastructure = new McpStatus(McpState.Pending, null);

        if (ConfigurationProblem() is { } problem)
        {
            _infrastructure = new McpStatus(McpState.NotConfigured, problem);
        }
    }

    [LoggerMessage(EventId = 4100, Level = LogLevel.Information, Message = "AI ulanish (MCP): o'qish roli tayyor — {Role}")]
    private partial void LogReady(string role);

    [LoggerMessage(EventId = 4101, Level = LogLevel.Warning, Message = "AI ulanish (MCP) YOPIQ: {Reason}")]
    private partial void LogDisabled(string reason);

    [LoggerMessage(EventId = 4102, Level = LogLevel.Error, Message = "AI ulanish (MCP) tekshiruvi yiqildi: {Reason}")]
    private partial void LogFaulty(string reason);

    /// <summary>
    /// O'qish rolini tayyorlaydi va tekshiradi. <c>DatabaseInitializer</c> migratsiyadan
    /// keyin chaqiradi; «Tizim sozlamalari» dagi «Qayta tekshirish» ham shu.
    /// </summary>
    public async Task<McpStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (ConfigurationProblem() is { } problem)
        {
            _infrastructure = new McpStatus(McpState.NotConfigured, problem);
            LogDisabled(problem);
            return _infrastructure;
        }

        await _checkLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var failure = await _database.ProvisionAsync(cancellationToken).ConfigureAwait(false)
                ?? await _database.SelfTestAsync(cancellationToken).ConfigureAwait(false);

            if (failure is null)
            {
                _infrastructure = new McpStatus(McpState.On, null);
                LogReady(_database.RoleName);
            }
            else
            {
                _infrastructure = new McpStatus(McpState.Faulty, failure);
                LogFaulty(failure);
            }

            return _infrastructure;
        }
        finally
        {
            _checkLock.Release();
        }
    }

    /// <summary>Infratuzilma + o'chirgich birga.</summary>
    public async Task<McpStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var infrastructure = _infrastructure;

        if (infrastructure.State != McpState.On)
        {
            return infrastructure;
        }

        return await IsSwitchedOnAsync(cancellationToken).ConfigureAwait(false)
            ? infrastructure
            : new McpStatus(McpState.Off, null);
    }

    public async Task<bool> IsOnAsync(CancellationToken cancellationToken = default) =>
        (await GetStatusAsync(cancellationToken).ConfigureAwait(false)).State == McpState.On;

    /// <summary>
    /// O'qish roli tayyor va o'z-o'zini tekshiruvdan o'tganmi — egasining
    /// o'chirgichidan QAT'I NAZAR.
    /// </summary>
    /// <remarks>
    /// Tashqi API'ning «to'liq o'qish» kaliti (<c>/api/public/v1/full</c>)
    /// shu vositalardan foydalanadi, lekin u AI chat ulanishi emas: egasi
    /// MCP'ni o'chirsa ham rahbarning kaliti ishlayveradi. Himoya (faqat
    /// o'qish roli) esa aynan bir xil — rol tayyor bo'lmasa ikkalasi ham yopiq.
    /// </remarks>
    public bool IsStoreReady => _infrastructure.State == McpState.On;

    public void Dispose() => _checkLock.Dispose();

    /// <summary>O'chirgich yozilgandan keyin keshni darhol yangilaydi.</summary>
    public void SetEnabled(bool enabled) => _switch = new SwitchState(enabled, DateTimeOffset.UtcNow);

    private async Task<bool> IsSwitchedOnAsync(CancellationToken cancellationToken)
    {
        if (_switch is { } cached && DateTimeOffset.UtcNow - cached.At < SwitchCacheLifetime)
        {
            return cached.Enabled;
        }

        bool enabled;

        await using (var context = _database.CreateStoreContext())
        {
            var value = await context.SystemSettings
                .AsNoTracking()
                .Where(setting => setting.Key == SystemSettingKeys.McpEnabled)
                .Select(setting => setting.Value)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            enabled = string.Equals(value, "true", StringComparison.Ordinal);
        }

        _switch = new SwitchState(enabled, DateTimeOffset.UtcNow);
        return enabled;
    }

    private string? ConfigurationProblem()
    {
        if (!_database.IsConfigured)
        {
            return _database.ConfigurationProblem;
        }

        if (_options.PublicBaseUrl is null
            && !_environment.IsDevelopment()
            && !_environment.IsEnvironment("Testing"))
        {
            return "«Mcp:PublicBaseUrl» berilmagan (production'da shart)";
        }

        // Production'da emitent faqat https: OAuth kodi va tokenlari ochiq
        // matnda yurmasin (Caddy https'ni beradi, manzil esa shuni aytsin).
        if (_environment.IsProduction()
            && _options.PublicBaseUrl is { } url
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return "«Mcp:PublicBaseUrl» https bilan boshlanishi kerak";
        }

        return null;
    }

    private sealed record SwitchState(bool Enabled, DateTimeOffset At);
}
