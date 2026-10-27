using Bonvi.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Bonvi.Api.Startup;

/// <summary>
/// Ishga tushishda bot tekshiruvi (Turnstile) holatini jurnalga yozadi (SPEC-planshet-anketa §2.3.2).
/// </summary>
/// <remarks>
/// Faqat bitta kalit berilgan bo'lsa tekshiruv O'CHIQ qoladi (<see cref="TurnstileOptions"/>) — bu
/// jimgina o'tib ketmasin: egasi kalit qo'ydim deb o'ylaydi, forma esa himoyasiz. Shu holat
/// ogohlantirish bilan, yoqilgani esa oddiy xabar bilan yoziladi. Kalitlarning o'zi yozilmaydi.
/// </remarks>
public sealed partial class TurnstileStartupCheck : IHostedService
{
    private readonly IOptions<TurnstileOptions> _options;
    private readonly ILogger<TurnstileStartupCheck> _logger;

    public TurnstileStartupCheck(IOptions<TurnstileOptions> options, ILogger<TurnstileStartupCheck> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;

        if (options.Problem is { } problem)
        {
            LogHalfConfigured(problem);
        }
        else if (options.Enabled)
        {
            LogEnabled();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = 4702,
        Level = LogLevel.Warning,
        Message = "Onlayn anketaning bot tekshiruvi (Turnstile) O'CHIQ: {Problem} — ikkala kalit birga berilishi shart")]
    private partial void LogHalfConfigured(string problem);

    [LoggerMessage(
        EventId = 4703,
        Level = LogLevel.Information,
        Message = "Onlayn anketaning bot tekshiruvi (Turnstile) yoqiq")]
    private partial void LogEnabled();
}
