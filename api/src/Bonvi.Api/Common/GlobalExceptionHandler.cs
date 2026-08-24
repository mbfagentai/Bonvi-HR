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

    [LoggerMessage(EventId = 5000, Level = LogLevel.Error, Message = "Tutilmagan istisno: {Method} {Path}")]
    private partial void LogUnhandled(Exception exception, string method, string path);

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var problem = exception is BadHttpRequestException badRequest
            ? ClientMistake(httpContext, badRequest)
            : ServerFault(httpContext, exception);

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
