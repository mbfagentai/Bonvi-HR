using Bonvi.Api.Extensions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Mobile;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Me;
using Bonvi.Domain.Attendance;

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
public sealed record MobileCheckInRequest(
    PassDirection Direction,
    decimal Latitude,
    decimal Longitude,
    decimal AccuracyMeters);

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
/// </remarks>
public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/me")
            .WithTags("Me")
            .RequireAuthorization(AuthenticationExtensions.EmployeePolicy);

        MapProfile(group);
        MapAttendance(group);
        MapCheckIn(group);
        MapRequests(group);

        return app;
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

                return natija.ToHttpResult();
            })
            .WithName("MobileCheckIn")
            .WithSummary("Xodim — telefondan belgilash")
            .WithDescription(
                "Javobdagi «occurredAt» — SERVERNING vaqti, telefonniki emas. " +
                "Tekshiruv tartibi va kodlari: «davomat.mobil_ruxsat_yoq» (403 — imkoniyat " +
                "yoqilmagan), «davomat.joylashuv_aniq_emas» (400 — aniqlik 100 m dan yomon), " +
                "«davomat.obyektdan_uzoq» (400 — hech bir faol obyekt hududida emas; xabarda " +
                "eng yaqin obyekt va masofa), «davomat.takroriy_belgi» (409 — oxirgi belgidan " +
                "60 soniya o'tmagan).")
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
                "qo'yardi. Tasdiqlangan so'rov ham bekor qilinadi va uning kunlari qayta " +
                "hisoblanadi.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
