using Bonvi.Application.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Bonvi.Api.Common;

/// <summary>
/// Tutilmagan istisnolarni yagona ko'rinishdagi javobga aylantiradi.
/// </summary>
/// <remarks>
/// Foydalanuvchiga stack trace yoki ichki xato matni HECH QACHON ko'rsatilmaydi —
/// bu ma'lumot tizim tuzilishini oshkor qiladi. To'liq tafsilot faqat logga yoziladi.
/// </remarks>
public sealed partial class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IDatabaseFault _databaseFault;

    [LoggerMessage(EventId = 5000, Level = LogLevel.Error, Message = "Tutilmagan istisno: {Method} {Path}")]
    private partial void LogUnhandled(Exception exception, string method, string path);

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IDatabaseFault databaseFault)
    {
        _logger = logger;
        _databaseFault = databaseFault;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var problem = exception switch
        {
            BadHttpRequestException badRequest => ClientMistake(httpContext, badRequest),
            _ when _databaseFault.IsDuplicateName(exception) => DuplicateName(httpContext),
            _ => ServerFault(httpContext, exception),
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// So'rovning o'zi noto'g'ri — bu <b>mijoz xatosi</b>, server nosozligi emas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ASP.NET majburiy parametrni o'qiy olmaganda
    /// (<c>?type=</c> berilmagan yoki <c>?type=Boshqa</c> deb yozilgan)
    /// <see cref="BadHttpRequestException"/> tashlaydi. Uni boshqa istisnolar
    /// bilan bir xil ko'rsak, mijozning imlo xatosi <c>500 Ichki xato</c>
    /// bo'lib qaytadi va logga «tutilmagan istisno» bo'lib tushadi.
    /// </para>
    /// <para>
    /// Ikkala oqibat ham yomon: mijoz nimani noto'g'ri yozganini bilmaydi, va
    /// haqiqiy nosozliklar kuzatuvida mijoz xatolari shovqin bo'lib yotadi.
    /// Shu sababli bunday holat <c>400</c> bilan qaytariladi va bu yerda
    /// ALOHIDA logga yozilmaydi: <c>UseSerilogRequestLogging</c> har bir
    /// so'rovni yo'li va holat kodi bilan allaqachon yozadi, javobda esa
    /// <c>traceId</c> turadi — ikkinchi yozuv hech narsa qo'shmasdi.
    /// </para>
    /// <para>
    /// Istisno matni ATAYLAB ko'rsatilmaydi: unda parametr nomi va tip nomi
    /// bo'ladi, ya'ni ichki tuzilma haqida ma'lumot. Mijozga qaysi so'rov
    /// noto'g'ri ekani yetarli.
    /// </para>
    /// </remarks>
    private static ProblemDetails ClientMistake(HttpContext httpContext, BadHttpRequestException exception)
    {
        return new ProblemDetails
        {
            // Istisnoning o'z holati odatda 400, lekin 413 (juda katta tana)
            // ham shu turdan keladi — u saqlanadi.
            Status = exception.StatusCode,
            Title = "So'rov noto'g'ri",
            Detail = "So'rov parametrlari to'liq emas yoki noto'g'ri formatda.",
            Instance = httpContext.Request.Path,
        };
    }

    /// <summary>
    /// Nom bo'yicha unikallikni buzish — bu ham <b>mijoz xatosi</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Odatda takror nom use-case qatlamida tutiladi va toza <c>409</c>
    /// bo'lib qaytadi. Lekin tekshirish bilan yozish orasida qisqa oyna
    /// bor: ikki foydalanuvchi bir vaqtda bir xil nom yuborsa, ikkalasi
    /// ham «bo'sh» degan javob oladi va ikkinchisi bazadagi unikal
    /// indeksga uriladi.
    /// </para>
    /// <para>
    /// Bunday holat <c>500</c> bo'lib qaytsa, HR «tizim buzildi» deb
    /// o'ylab qo'ng'iroq qilardi — aslida esa unga shunchaki «bunday nom
    /// bor» deyish kerak edi. Shu sababli kod use-case qatlamidagi bilan
    /// AYNAN bir xil: interfeys ikkala yo'lni ham bir xil tarjima qiladi.
    /// </para>
    /// </remarks>
    private static ProblemDetails DuplicateName(HttpContext httpContext)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "malumotnoma.nom_band",
            Detail = "Bunday nom allaqachon mavjud.",
            Instance = httpContext.Request.Path,
        };
    }


    /// <summary>Haqiqiy nosozlik — tafsilot faqat logga tushadi.</summary>
    /// <remarks>
    /// Foydalanuvchiga stack trace yoki ichki xato matni HECH QACHON
    /// ko'rsatilmaydi — bu ma'lumot tizim tuzilishini oshkor qiladi.
    /// </remarks>
    private ProblemDetails ServerFault(HttpContext httpContext, Exception exception)
    {
        LogUnhandled(exception, httpContext.Request.Method, httpContext.Request.Path);

        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Ichki xato",
            Detail = "So'rovni bajarishda kutilmagan xato yuz berdi. Iltimos, keyinroq urinib ko'ring.",
            Instance = httpContext.Request.Path,
        };
    }
}
