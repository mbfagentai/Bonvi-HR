using System.Collections.Concurrent;
using Bonvi.Application.PublicApi.Abstractions;
using Bonvi.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bonvi.Infrastructure.Services;

/// <summary>
/// «Kalit oxirgi marta qachon ishlatilgan» belgisini so'rov yo'lidan
/// tashqarida, daqiqada bir martadan ko'p bo'lmagan holda yozadi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega bu shunchaki <c>UPDATE</c> emas.</b> Belgi HAR BIR ommaviy
/// so'rovda yangilanadigan bo'lsa, faqat o'qish uchun qurilgan API
/// daqiqasiga 120 ta yozishga aylanardi va har bir yozish
/// <c>api_keys</c> dagi bitta qatorni qulflardi — ya'ni bir iste'molchi
/// ikkinchisini kutib turardi. Belgining qiymati esa juda past:
/// «bu kalit hali tirikmi?» degan savolga daqiqalik aniqlik ortig'i
/// bilan yetadi.
/// </para>
/// <para>
/// <b>Uch qoida, uchtasi ham majburiy:</b>
/// </para>
/// <list type="number">
///   <item><b>Dastlab chegara</b> — kalit bo'yicha daqiqada bitta yozuv.
///         Chegara xotirada (<see cref="ConcurrentDictionary{TKey, TValue}"/>):
///         u bitta jarayonga tegishli va API bitta nusxada ishlaydi.
///         Ikkinchi nusxa qo'shilsa, eng yomon holat — daqiqada ikkita
///         yozuv, ya'ni chegaraning maqsadi baribir saqlanadi;</item>
///   <item><b>So'rov yo'lidan tashqarida</b> — <see cref="Task.Run(Func{Task})"/>
///         va O'Z DI doirasi. So'rovning <c>DbContext</c> i javob
///         yuborilgach yopiladi, shu sababli mavjud doirani ishlatib
///         bo'lmaydi;</item>
///   <item><b>Xato — JIM</b>. Bu yozuvning yiqilishi o'qish so'rovini
///         yiqitmasligi kerak: aks holda ma'lumot beruvchi ustun butun
///         ommaviy API ni to'xtatardi. Xato <c>Warning</c> darajasida
///         jurnalga tushadi — u yerda faqat PREFIKS emas, kalit
///         IDENTIFIKATORI ko'rinadi (kalitning o'zi hech qachon).</item>
/// </list>
/// <para>
/// <b>Nega <c>IHostedService</c> navbati emas.</b> Navbat kerak bo'lganda
/// — ya'ni yozuvlar tartibi yoki yetkazilishi kafolatlanishi kerak
/// bo'lganda — mazmunli bo'ladi. Bu yerda ikkalasi ham kerak emas:
/// yozuv yo'qolsa, keyingi so'rov bir daqiqadan so'ng uni baribir
/// yangilaydi.
/// </para>
/// </remarks>
public sealed class ApiKeyUsageTracker : IApiKeyUsageTracker
{
    /// <summary>Bitta kalit uchun eng qisqa yozish oralig'i.</summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastWritten = new();
    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly ILogger<ApiKeyUsageTracker> _logger;

    public ApiKeyUsageTracker(
        IServiceScopeFactory scopes,
        IClock clock,
        ILogger<ApiKeyUsageTracker> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public void Schedule(Guid keyId, string? ip)
    {
        var now = _clock.UtcNow;

        // Atomik: ikkita parallel so'rov kelganda faqat bittasi yozadi.
        // `AddOrUpdate` ning qaytargan qiymati «kim yozadi» degan savolga
        // javob bermaydi, shu sababli qaror `TryGetValue` + `TryUpdate`
        // juftligi bilan qabul qilinadi.
        if (_lastWritten.TryGetValue(keyId, out var previous))
        {
            if (now - previous < MinimumInterval)
            {
                return;
            }

            if (!_lastWritten.TryUpdate(keyId, now, previous))
            {
                // Boshqa so'rov bizni ortda qoldirdi — u yozadi.
                return;
            }
        }
        else if (!_lastWritten.TryAdd(keyId, now))
        {
            return;
        }

        // `Task.Run` ataylab «o'qilmagan vazifa» (fire-and-forget): so'rov
        // uni kutmaydi. Ichida istisno tutiladi, ya'ni kuzatilmagan
        // vazifa istisnosi (`TaskScheduler.UnobservedTaskException`)
        // ham yuzaga kelmaydi.
        _ = Task.Run(() => WriteAsync(keyId, now, ip), CancellationToken.None);
    }

    private async Task WriteAsync(Guid keyId, DateTimeOffset at, string? ip)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();

            var keys = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var key = await keys.GetByIdAsync(keyId, CancellationToken.None).ConfigureAwait(false);
            if (key is null)
            {
                return;
            }

            key.MarkUsed(at, ip);
            await unitOfWork.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Bu yerda ATAYLAB hamma narsa tutiladi — izohi pastda.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            // Aynan shu joyda `catch (Exception)` to'g'ri: chaqiruvchi yo'q
            // (fire-and-forget), ya'ni tutilmagan istisno butun jarayonga
            // chiqardi. Va yozuvning har qanday nosozligi — baza
            // yiqilishi, qulflanish, tarmoq — o'qish so'roviga
            // TEGMASLIGI kerak.
            ApiKeyUsageLoglari.YozibBolmadi(_logger, keyId, exception);
        }
    }
}

/// <summary>
/// Kalit ishlatilishini yozish nosozliklari.
/// </summary>
/// <remarks>
/// <c>[LoggerMessage]</c> manba-generatori — loyihadagi barcha loglar
/// bilan bir xil naqsh (CA1848 shuni talab qiladi).
/// <para>
/// ⚠️ Shablonda kalitning O'ZI ham, prefiksi ham YO'Q — faqat
/// identifikator. Ustiga xato jurnalining formatteri oq ro'yxat bilan
/// ishlaydi va <c>KeyId</c> u ro'yxatda YO'Q, ya'ni faylga
/// <c>[yashirildi]</c> bo'lib tushadi. Konsol logi (<c>docker logs api</c>)
/// esa to'liq ko'rsatadi — u yerda sir emas.
/// </para>
/// </remarks>
internal static partial class ApiKeyUsageLoglari
{
    [LoggerMessage(
        EventId = 5200,
        Level = LogLevel.Warning,
        Message = "Tashqi API kalitining «oxirgi ishlatilgan» belgisini yozib bo'lmadi: {KeyId}")]
    public static partial void YozibBolmadi(ILogger logger, Guid keyId, Exception exception);
}
