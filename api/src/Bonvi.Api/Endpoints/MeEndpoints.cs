using Bonvi.Api.Common;
using Bonvi.Api.Extensions;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Mobile;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Identity.EmployeeAuth;
using Bonvi.Application.Me;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Api.Endpoints;

// ════════════════════════════════════════════════════════════════════
//  So'rov tanalari — `Guid X = default` YO'Q (OpenAPI tuzog'i).
// ════════════════════════════════════════════════════════════════════

/// <summary>Telefondan belgilash tanasi.</summary>
/// <remarks>
/// ⚠️ VAQT bu yerda YO'Q va hech qachon bo'lmaydi: o'tish lahzasini
/// SERVER qo'yadi (SPEC §B3). Telefonning soati noto'g'ri bo'lsa ham,
/// tabelga to'g'ri vaqt tushishi kerak.
/// </remarks>
/// <param name="Direction">«In» yoki «Out».</param>
/// <param name="Latitude">Kenglik, o'nlik gradus.</param>
/// <param name="Longitude">Uzunlik, o'nlik gradus.</param>
/// <param name="AccuracyMeters">Brauzer bergan aniqlik, metr.</param>
/// <remarks>
/// ⚠️ Sonlar NULLABLE: yuborilmagan maydon endi 0 bo'lib bog'lanmaydi
/// (aks holda aniqligi yo'q o'lchov «±0 m» — mukammal — bo'lib o'tib
/// ketardi). Yo'q qiymat — <c>davomat.joylashuv_notogri</c> (400).
/// </remarks>
public sealed record MobileCheckInRequest(
    PassDirection Direction,
    decimal? Latitude,
    decimal? Longitude,
    decimal? AccuracyMeters);

/// <summary>Xodim yaratadigan yo'qlik so'rovining tanasi.</summary>
/// <remarks>
/// ⚠️ <c>EmployeeId</c> ATAYLAB yo'q: u tokendan olinadi. Tanada bo'lsa,
/// bitta maydon butun korxonaning so'rovlarini yozish imkonini berardi.
/// </remarks>
/// <param name="AbsenceTypeId">Yo'qlik turi (<c>GET /api/me</c> dagi ro'yxatdan).</param>
/// <param name="FromDate">Davr boshi.</param>
/// <param name="ToDate">Davr oxiri.</param>
/// <param name="FromTime">Kun ichidagi boshlanish — faqat bir kunlik so'rovda.</param>
/// <param name="ToTime">Kun ichidagi tugash — faqat bir kunlik so'rovda.</param>
/// <param name="Note">Izoh.</param>
public sealed record MyAbsenceRequestBody(
    Guid AbsenceTypeId,
    DateOnly FromDate,
    DateOnly ToDate,
    TimeOnly? FromTime = null,
    TimeOnly? ToTime = null,
    string? Note = null);

/// <summary>
/// Xodim ilovasining endpointlari — <c>/api/me/*</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Butun guruh <see cref="AuthenticationExtensions.EmployeePolicy"/>
/// ostida, ruxsat kodisiz.</b> Buning sababi shakliy emas: xodim tokenida
/// ruxsat da'volari YO'Q va bo'lmasligi ham kerak (SPEC §D2), ya'ni bu
/// endpointlarni ruxsat bilan qo'riqlab bo'lmaydi. Himoya ikki qatlamda:
/// </para>
/// <list type="number">
///   <item>
///     <b>Auditoriya.</b> Siyosat AYNAN xodim sxemasini talab qiladi
///     (<c>bonvi-employee</c>). HR tokeni — hatto super administratorniki
///     ham — bu sxemadan o'tmaydi va 401 oladi.
///   </item>
///   <item>
///     <b>Identifikator TOKENDAN.</b> Har bir ishlovchi xodimni tokendagi
///     hisob orqali topadi; manzilda ham, tanada ham xodim identifikatori
///     umuman qatnashmaydi. Ya'ni «boshqa odamning ma'lumotini so'rash»
///     degan holat texnik jihatdan mavjud emas — xuddi
///     <c>/api/auth/me/photo</c> dagidek.
///   </item>
/// </list>
/// <para>
/// ⚠️ Parolni almashtirish endpointi bu guruhda EMAS
/// (<c>/api/employee-auth/password</c>): u bir martalik parol bilan
/// kirgan odamga ham ochiq bo'lishi kerak.
/// </para>
/// <para>
/// ⭐ <b>«Avval parolni almashtiring» — butun guruhga filtr.</b> Hisobda
/// <c>must_change_password</c> turgan ekan, har bir <c>/api/me/*</c> so'rovi
/// <c>xodim.parol_almashtirilsin</c> (403) oladi. Bayroq TOKENDAN emas,
/// HISOB QATORIDAN o'qiladi: HR parolni tiklaganda eski (bayroqsiz) token
/// 15 daqiqa amal qilishda davom etadi va da'voga ishonilsa, bir martalik
/// parol amalda cheksiz ishlab qolardi.
/// </para>
/// </remarks>
public static partial class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/me")
            .WithTags("Me")
            .RequireAuthorization(AuthenticationExtensions.EmployeePolicy)
            .AddEndpointFilter(RequirePasswordSetAsync)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        MapProfile(group);
        MapAttendance(group);
        MapCheckIn(group);
        MapRequests(group);

        return app;
    }

    /// <summary>
    /// Bir martalik parol almashtirilmaguncha guruhning hech bir endpointi ishlamaydi.
    /// </summary>
    /// <remarks>
    /// Filtr avtorizatsiyadan KEYIN ishlaydi — ya'ni bu yerga faqat xodim
    /// sxemasidan o'tgan so'rov keladi. Hisob yo'q yoki yopilgan bo'lsa filtr
    /// hech narsa demaydi: bu savolga ishlovchilarning o'zi
    /// (<c>xodim.hisob_faol_emas</c>) javob beradi.
    /// </remarks>
    private static async ValueTask<object?> RequirePasswordSetAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var gate = context.HttpContext.RequestServices.GetRequiredService<EmployeePasswordGate>();

        var natija = await gate.CheckAsync(context.HttpContext.RequestAborted).ConfigureAwait(false);

        return natija.IsFailure
            ? natija.ToHttpResult()
            : await next(context).ConfigureAwait(false);
    }

    // ════════════════════════════════════════════════════════════
    //  Profil
    // ════════════════════════════════════════════════════════════

    private static void MapProfile(IEndpointRouteBuilder group)
    {
        group.MapGet("/", async (
                GetMeQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(new GetMeQuery(), cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .WithName("GetMe")
            .WithSummary("Xodim — o'zim haqimda")
            .WithDescription(
                "Ism, rasm kaliti, bugungi bo'lim va lavozim, «telefondan belgilash» bayrog'i va " +
                "«mustChangePassword». Javobda YO'QLIK TURLARI ro'yxati ham bor: ilova so'rov " +
                "yaratishi uchun kerak, ma'lumotnoma endpointiga esa uning tokeni yetmaydi. " +
                "Xato: «xodim.hisob_faol_emas» (403).")
            .Produces<MeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/photo", async (
                HttpContext context,
                GetMyPhotoQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetMyPhotoQuery(PhotoHttp.ReadKnownVersion(context.Request)), cancellationToken)
                    .ConfigureAwait(false);

                // JSON emas — BAYT: `ToHttpResult()` faqat xato yo'lida
                // (`EmployeeEndpoints` dagi rasm endpointi bilan bir xil).
                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : PhotoHttp.Write(context, natija.Value);
            })
            .WithName("GetMyEmployeePhoto")
            .WithSummary("Xodim — o'z rasmi")
            .WithDescription(
                "Tokendagi xodimning O'Z rasmi — manzilda identifikator YO'Q, ya'ni boshqa odamning " +
                "rasmini so'rab bo'lmaydi. Baytlar (image/jpeg), «ETag» + «If-None-Match» → 304, kesh " +
                "SHAXSIY (private). Rasm yo'q bo'lsa: «xodim.rasm_yoq» (404).")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // ════════════════════════════════════════════════════════════
    //  Mening oyim
    // ════════════════════════════════════════════════════════════

    private static void MapAttendance(IEndpointRouteBuilder group)
    {
        group.MapGet("/attendance", async (
                int year,
                int month,
                GetMyAttendanceQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetMyAttendanceQuery(year, month), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("GetMyAttendance")
            .WithSummary("Xodim — mening oyim")
            .WithDescription(
                "Oyning har bir kuni: HR tabelidagi AYNAN o'sha katak («day») va o'sha kundagi " +
                "belgilar («marks»). Belgining manbai: «Terminal» yoki «Mobile»; telefon " +
                "belgisida obyekt nomi ham keladi. " +
                "Xato: «davomat.oy_notogri» (400).")
            .Produces<MyAttendanceDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    // ════════════════════════════════════════════════════════════
    //  Telefondan belgilash
    // ════════════════════════════════════════════════════════════

    private static void MapCheckIn(IEndpointRouteBuilder group)
    {
        group.MapPost("/check-in", async (
                MobileCheckInRequest request,
                MobileCheckInCommandHandler handler,
                ICurrentEmployee currentEmployee,
                ILoggerFactory loggers,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new MobileCheckInCommand(
                            request.Direction,
                            request.Latitude,
                            request.Longitude,
                            request.AccuracyMeters),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (natija.IsFailure)
                {
                    LogRejected(
                        loggers.CreateLogger("Bonvi.Api.MobileCheckIn"),
                        natija.Error,
                        currentEmployee.AccountId,
                        request);
                }

                return natija.ToHttpResult();
            })
            .WithName("MobileCheckIn")
            .WithSummary("Xodim — telefondan belgilash")
            .WithDescription(
                "Javobdagi «occurredAt» — SERVERNING vaqti, telefonniki emas. " +
                "Tekshiruv tartibi va kodlari: «davomat.mobil_ruxsat_yoq» (403 — imkoniyat " +
                "yoqilmagan, hisob yopilgan yoki bugun ishlamaydi), «davomat.yonalish_notogri» " +
                "(400), «davomat.joylashuv_notogri» (400 — koordinata yoki aniqlik yo'q, nol " +
                "yoki chegaradan tashqari), «davomat.joylashuv_aniq_emas» (400 — aniqlik 100 m " +
                "dan yomon; javobda «accuracyMeters»), «davomat.obyektdan_uzoq» (400 — hech bir " +
                "faol obyekt hududida emas; javobda «siteName» va «distanceMeters», faol obyekt " +
                "umuman bo'lmasa ular yo'q), «davomat.takroriy_belgi» (409 — oxirgi belgidan " +
                "60 soniya o'tmagan; javobda «lastAt»). Bir martalik parol almashtirilmagan " +
                "bo'lsa — «xodim.parol_almashtirilsin» (403).")
            .Produces<MobileCheckInResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    // ════════════════════════════════════════════════════════════
    //  Mening so'rovlarim
    // ════════════════════════════════════════════════════════════

    private static void MapRequests(IEndpointRouteBuilder group)
    {
        group.MapGet("/requests", async (
                AbsenceRequestStatus? status,
                int? page,
                int? pageSize,
                GetMyRequestsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetMyRequestsQuery(status, page, pageSize), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("GetMyRequests")
            .WithSummary("Xodim — mening yo'qlik so'rovlarim")
            .WithDescription(
                "Faqat O'Z so'rovlari — xodim tokendan aniqlanadi. «status» bilan filtrlash " +
                "mumkin (Pending, Approved, Rejected, Cancelled).")
            .Produces<PagedResult<AbsenceRequestDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/requests", async (
                MyAbsenceRequestBody request,
                CreateMyRequestCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new CreateMyRequestCommand(
                            request.AbsenceTypeId,
                            request.FromDate,
                            request.ToDate,
                            request.FromTime,
                            request.ToTime,
                            request.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("CreateMyRequest")
            .WithSummary("Xodim — yo'qlik so'rovi yuborish")
            .WithDescription(
                "So'rov «Kutilmoqda» holatida yaratiladi va davomat hisobiga HECH QANDAY ta'sir " +
                "qilmaydi — hisob faqat HR qaroridan keyin o'zgaradi. " +
                "Xatolar: «davomat.sorov_davri_notogri», «davomat.sorov_uzun», " +
                "«davomat.sorov_vaqti_notogri» (400); «davomat.yoqlik_turi_topilmadi» (404); " +
                "«davomat.sorov_takroriy» (409).")
            .Produces<AbsenceRequestDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/requests/{id:guid}/cancel", async (
                Guid id,
                CancelMyRequestCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new CancelMyRequestCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("CancelMyRequest")
            .WithSummary("Xodim — so'rovni bekor qilish")
            .WithDescription(
                "Faqat O'Z so'rovi. Boshqa odamning so'rovi «davomat.sorov_topilmadi» (404) " +
                "beradi — 403 emas: aks holda javobning o'zi shunday so'rov borligini aytib " +
                "qo'yardi. Faqat QAROR KUTAYOTGAN so'rov bekor qilinadi: HR tasdiqlagan yoki " +
                "rad etgan so'rov «davomat.sorov_qaror_qabul_qilingan» (409), allaqachon " +
                "bekor qilingani «davomat.sorov_bekor_qilingan» (409) beradi.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    // ════════════════════════════════════════════════════════════
    //  Log
    // ════════════════════════════════════════════════════════════

    /// <summary>Rad etilgan belgini sababi bilan logga yozadi.</summary>
    /// <remarks>
    /// <para>
    /// Rad etilgan urinish bazaga YOZILMAYDI (har bosish qator yasaydigan
    /// jadval suiiste'mol kanali bo'lardi — ma'lumot modeli, «Nothing records
    /// what the fence was»), ya'ni «u o'tgan seshanba nega rad etildi?» degan
    /// savolning yagona javobi — shu yozuv. Takroriy bosish — odatiy hol
    /// (ma'lumot darajasi); qolganlari ogohlantirish, ular fayl jurnaliga
    /// ham tushadi.
    /// </para>
    /// <para>
    /// ⚠️ KOORDINATA yozilmaydi — faqat kod, aniqlik, obyekt nomi va
    /// chegaragacha masofa. Odamning qayerda turgani (uyi bo'lishi mumkin)
    /// shaxsiy ma'lumot va logda yashamasligi kerak; qaror uchun kerakli
    /// sonlar esa xatoning o'zida (<see cref="Error.Details"/>).
    /// </para>
    /// </remarks>
    private static void LogRejected(ILogger logger, Error error, Guid? accountId, MobileCheckInRequest request)
    {
        if (error.Code == "davomat.takroriy_belgi")
        {
            CheckInRepeated(logger, accountId, request.Direction);
            return;
        }

        var accuracy = error.Details.TryGetValue("accuracyMeters", out var measured) ? measured : request.AccuracyMeters;
        var site = error.Details.TryGetValue("siteName", out var name) ? name : null;
        var distance = error.Details.TryGetValue("distanceMeters", out var meters) ? meters : null;

        CheckInRejected(logger, error.Code, accountId, request.Direction, accuracy, site, distance);
    }

    [LoggerMessage(
        EventId = 5410,
        Level = LogLevel.Warning,
        Message = "Telefondan belgilash rad etildi: {Code} — hisob {AccountId}, yo'nalish {Direction}, " +
                  "aniqlik {AccuracyMeters} m, eng yaqin obyekt {SiteName}, chegaragacha {DistanceMeters} m")]
    private static partial void CheckInRejected(
        ILogger logger,
        string code,
        Guid? accountId,
        PassDirection direction,
        object? accuracyMeters,
        object? siteName,
        object? distanceMeters);

    [LoggerMessage(
        EventId = 5411,
        Level = LogLevel.Information,
        Message = "Telefondan belgilash: takroriy bosish rad etildi — hisob {AccountId}, yo'nalish {Direction}")]
    private static partial void CheckInRepeated(ILogger logger, Guid? accountId, PassDirection direction);
}
