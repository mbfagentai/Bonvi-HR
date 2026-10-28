using Bonvi.Api.Common;
using Bonvi.Api.Extensions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Agent;
using Bonvi.Application.Attendance.Devices;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Identities;
using Bonvi.Application.Attendance.Queries;
using Bonvi.Application.Attendance.Requests;
using Bonvi.Application.Attendance.Recompute;
using Bonvi.Application.Attendance.Schedules;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Reports.Queries;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bonvi.Api.Endpoints;

// ════════════════════════════════════════════════════════════════════
//  So'rov tanalari
//
//  ⚠️ `Guid X = default` BU YERDA YOZILMAYDI. .NET 10 ning OpenAPI
//  generatori bunday yozuvda BUTUN `/openapi/v1.json` ni 500 bilan
//  yiqitadi va `./bonvi gen` hamma endpoint uchun jimgina o'ladi
//  (2026-08-29 da bir marta tuzatilgan). Majburiy — sukutsiz,
//  ixtiyoriy — `Guid?`.
// ════════════════════════════════════════════════════════════════════

/// <summary>Qurilmani ro'yxatga olish va tahrirlash tanasi — ikkalasi bir xil.</summary>
/// <remarks>
/// Enum maydonlari ixtiyoriy va domen sukutiga tushadi (Hikvision, Verifix,
/// qurilma yo'nalishi, faqat token): tanada tushib qolgan maydon <c>0</c> — sanoqda
/// yo'q qiymat — bo'lib bazaga yozilmasligi kerak.
/// </remarks>
/// <param name="Name">Nom. Majburiy va takrorlanmas.</param>
/// <param name="SerialNumber">Seriya raqami.</param>
/// <param name="Model">Model.</param>
/// <param name="Provider">Hikvision | Generic.</param>
/// <param name="IdentityProvider">Verifix | Hikvision | ZkTeco | Manual.</param>
/// <param name="DirectionRole">ReportsDirection | EntryOnly | ExitOnly.</param>
/// <param name="LegalEntityId">Firma.</param>
/// <param name="DepartmentId">Bo'lim.</param>
/// <param name="LocationNote">Joylashuvi.</param>
/// <param name="AuthMode">Token | TokenAndHmac.</param>
/// <param name="AllowedIps">Vergul bilan ajratilgan IP/CIDR; bo'sh — cheklovsiz.</param>
/// <param name="HmacSecret">Imzo siri, faqat yoziladi: tahrirda <c>null</c> — o'zgarmaydi, <c>""</c> — o'chiriladi.</param>
public sealed record AttendanceDeviceRequest(
    string? Name,
    string? SerialNumber = null,
    string? Model = null,
    AttendanceProvider? Provider = null,
    AttendanceIdentityProvider? IdentityProvider = null,
    DeviceDirectionRole? DirectionRole = null,
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    string? LocationNote = null,
    DeviceAuthMode? AuthMode = null,
    string? AllowedIps = null,
    string? HmacSecret = null)
{
    /// <summary>Use-case maydonlariga — sukutlar domen bilan bir xil.</summary>
    internal AttendanceDeviceSettings ToSettings() => new(
        Name,
        SerialNumber,
        Model,
        Provider ?? AttendanceProvider.Hikvision,
        IdentityProvider ?? AttendanceIdentityProvider.Verifix,
        DirectionRole ?? DeviceDirectionRole.ReportsDirection,
        LegalEntityId,
        DepartmentId,
        LocationNote,
        AuthMode ?? DeviceAuthMode.Token,
        AllowedIps,
        HmacSecret);
}

/// <summary>Moslik yaratish tanasi.</summary>
/// <param name="Provider">Raqamlar fazosi; berilmasa — Verifix (bugun yagona ro'yxat).</param>
/// <param name="ExternalPersonId">Qurilmadagi shaxs raqami.</param>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="DisplayNameInDevice">Qurilma ro'yxatidagi F.I.Sh.</param>
/// <param name="Note">Izoh.</param>
public sealed record CreateAttendanceIdentityRequest(
    AttendanceIdentityProvider? Provider,
    string? ExternalPersonId,
    Guid EmployeeId,
    string? DisplayNameInDevice = null,
    string? Note = null);

/// <summary>Yillik to'r kataklarini yozish tanasi.</summary>
/// <remarks>
/// ⚠️ Maydonlarda <c>Guid = default</c> YO'Q (OpenAPI tuzog'i, boshqa
/// tanalardagi bilan bir xil sabab). <c>Dates</c> — ixtiyoriy massiv:
/// bo'sh kelsa buyruq «kamida bitta sana» xatosini beradi va bu 400,
/// 500 emas.
/// </remarks>
/// <param name="Dates">Qaysi sanalar.</param>
/// <param name="Reset">Kataklar o'chirilsinmi (kun naqshga qaytadi).</param>
/// <param name="IsWorking">Ish kunimi.</param>
/// <param name="StartTime">Boshlanish — ish kunida majburiy.</param>
/// <param name="EndTime">Tugash — ish kunida majburiy.</param>
/// <param name="BreakMinutes">Tanaffus (daqiqa).</param>
/// <param name="BreakStart">Tanaffus oynasining boshi.</param>
/// <param name="BreakEnd">Tanaffus oynasining oxiri.</param>
/// <param name="IsFlexible">«Erkin ish jadvali»; dam olish kunida va <c>reset</c> da e'tiborsiz.</param>
/// <param name="RequiredMinutes">«Ish vaqti» (daqiqa); bo'sh — sukut (smena − tanaffus).</param>
public sealed record ScheduleDatesBody(
    IReadOnlyList<DateOnly>? Dates,
    bool Reset = false,
    bool IsWorking = true,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    int BreakMinutes = 0,
    TimeOnly? BreakStart = null,
    TimeOnly? BreakEnd = null,
    bool IsFlexible = false,
    int? RequiredMinutes = null);

/// <summary>Yilni haftalik naqshdan to'ldirish tanasi.</summary>
/// <param name="Year">Qaysi yil (2000–2100).</param>
public sealed record ScheduleYearFillBody(int Year);

/// <summary>Yo'qlik so'rovining tanasi — yaratish va tahrirlash uchun bitta shakl.</summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Maydonlar <c>Guid = default</c> bilan berilmaydi</b> — bu loyihada
/// OpenAPI hujjatini butunlay yiqitgan tuzoq (`AttendanceDeviceRequest`
/// izohi): bo'sh <c>Guid</c> ni standart qiymat qilib yozish hujjat
/// yaratishda 500 beradi.
/// </para>
/// <para>
/// Tahrirlashda <c>EmployeeId</c> O'QILMAYDI: boshqa odamning so'rovi — bu
/// boshqa so'rov va uni yangisini yaratib olish kerak.
/// </para>
/// </remarks>
/// <param name="EmployeeId">Kim yo'q bo'ladi (tahrirlashda e'tiborga olinmaydi).</param>
/// <param name="AbsenceTypeId">Yo'qlik turi.</param>
/// <param name="FromDate">Davr boshi.</param>
/// <param name="ToDate">Davr oxiri.</param>
/// <param name="FromTime">Kun ichidagi boshlanish — faqat bir kunlik so'rovda.</param>
/// <param name="ToTime">Kun ichidagi tugash — faqat bir kunlik so'rovda.</param>
/// <param name="Note">Izoh.</param>
/// <param name="Version">
/// Tahrirda — HR ko'rgan <c>version</c> (SPEC-sorov-tahriri §3.3); farq qilsa 409
/// «davomat.sorov_ozgargan». Berilmasa tekshirilmaydi (eski mijoz); yaratishda e'tiborsiz.
/// </param>
public sealed record AbsenceRequestBody(
    Guid EmployeeId,
    Guid AbsenceTypeId,
    DateOnly FromDate,
    DateOnly ToDate,
    TimeOnly? FromTime = null,
    TimeOnly? ToTime = null,
    string? Note = null,
    int? Version = null);

/// <summary>Jadval so'rovining bitta kuni (SPEC-verifikatsiya-jadval-sorovi §C3).</summary>
/// <param name="Date">Sana.</param>
/// <param name="IsWorking">Ish kunimi.</param>
/// <param name="StartTime">Boshlanish — ish kunida shart.</param>
/// <param name="EndTime">Tugash — boshlanishdan oldin bo'lsa tungi smena.</param>
/// <param name="BreakStart">Tanaffus boshi (oxiri bilan birga).</param>
/// <param name="BreakEnd">Tanaffus oxiri.</param>
/// <param name="BreakMinutes">
/// Ayiriladigan tanaffus (daqiqa). Bo'sh — oynaning uzunligi. Forma jadvaldan to'ldirilgan va
/// tanaffusi o'zgartirilmagan kunda jadvalning qiymatini qaytaradi.
/// </param>
/// <param name="IsFlexible">«Erkin ish jadvali» (Verifix formasidagi belgi); berilmasa — yo'q.</param>
/// <param name="RequiredMinutes">«Ish vaqti» (daqiqa); bo'sh — sukut (smena − tanaffus).</param>
public sealed record ScheduleChangeDayBody(
    DateOnly Date,
    bool IsWorking,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    TimeOnly? BreakStart = null,
    TimeOnly? BreakEnd = null,
    int? BreakMinutes = null,
    bool IsFlexible = false,
    int? RequiredMinutes = null)
{
    /// <summary>Domen tavsifiga — HR va xodim ilovasi uchun bitta o'girish.</summary>
    public ScheduleChangeDayInput ToInput()
        => new(
            Date,
            IsWorking,
            StartTime,
            EndTime,
            BreakStart,
            BreakEnd,
            BreakMinutes: BreakMinutes,
            IsFlexible: IsFlexible,
            RequiredMinutes: RequiredMinutes);
}

/// <summary>Kun almashtirish jufti.</summary>
/// <param name="Date">Birinchi sana.</param>
/// <param name="WithDate">U bilan o'rin almashadigan sana.</param>
public sealed record ScheduleSwapBody(DateOnly Date, DateOnly WithDate);

/// <summary>Jadval so'rovi tanasi (HR).</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="Kind">Change — kunni o'zgartirish, Swap — kun almashtirish.</param>
/// <param name="Days">Change uchun kunlar.</param>
/// <param name="Swaps">Swap uchun juftlar.</param>
/// <param name="Note">Izoh.</param>
/// <param name="Version">Tahrirda — HR ko'rgan <c>version</c>; yaratishda e'tiborsiz.</param>
public sealed record ScheduleChangeRequestBody(
    Guid EmployeeId,
    ScheduleChangeKind Kind,
    IReadOnlyList<ScheduleChangeDayBody>? Days = null,
    IReadOnlyList<ScheduleSwapBody>? Swaps = null,
    string? Note = null,
    int? Version = null)
{
    /// <summary>Kunlar — domen tavsifiga.</summary>
    public IReadOnlyList<ScheduleChangeDayInput> DayInputs()
        => [.. (Days ?? []).Select(day => day.ToInput())];

    /// <summary>Juftlar — buyruq tavsifiga.</summary>
    public IReadOnlyList<ScheduleSwapInput> SwapInputs()
        => [.. (Swaps ?? []).Select(pair => new ScheduleSwapInput(pair.Date, pair.WithDate))];
}

/// <summary>Qaror tanasi.</summary>
/// <param name="Approve">Tasdiqlansinmi (<c>false</c> — rad etish).</param>
/// <param name="DecisionNote">HR izohi.</param>
/// <param name="Version">
/// HR ko'rgan <c>version</c> (SPEC-sorov-tahriri §3.3): xodim shu orada so'rovni o'zgartirgan bo'lsa —
/// 409 «davomat.sorov_ozgargan». Berilmasa tekshirilmaydi.
/// </param>
public sealed record AbsenceDecisionBody(bool Approve, string? DecisionNote = null, int? Version = null);

/// <summary>Import tasdig'idagi bitta qaror.</summary>
/// <param name="RawPersonId">Shaxs raqami.</param>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="DisplayNameInDevice">Fayldagi F.I.Sh.</param>
public sealed record ConfirmIdentityImportRowRequest(
    string? RawPersonId,
    Guid EmployeeId,
    string? DisplayNameInDevice = null);

/// <summary>Import tasdig'i tanasi.</summary>
/// <param name="Provider">Oldindan ko'rishdagi fazo; berilmasa — Verifix.</param>
/// <param name="Rows">Operator tasdiqlagan qatorlar.</param>
public sealed record ConfirmIdentityImportRequest(
    AttendanceIdentityProvider? Provider,
    IReadOnlyList<ConfirmIdentityImportRowRequest>? Rows);

/// <summary>Jadval kuni.</summary>
/// <param name="DayIndex">ISO hafta kuni (1 — dushanba … 7 — yakshanba).</param>
/// <param name="IsWorking">Ish kunimi.</param>
/// <param name="StartTime">Smena boshlanishi («09:00»).</param>
/// <param name="EndTime">Smena tugashi.</param>
/// <param name="BreakMinutes">Tanaffus uzunligi (0…480).</param>
/// <param name="BreakStart">Tanaffus oynasining boshlanishi — <paramref name="BreakEnd"/> bilan birga.</param>
/// <param name="BreakEnd">Tanaffus oynasining tugashi.</param>
/// <param name="IsFlexible">«Erkin ish jadvali» — kechikish/erta ketish yo'q, kun normaga qarab baholanadi.</param>
/// <param name="RequiredMinutes">«Ish vaqti» (1 … smena − tanaffus); bo'sh — sukut (eng kattasi).</param>
public sealed record WorkScheduleDayRequest(
    int DayIndex,
    bool IsWorking,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    int BreakMinutes = 0,
    TimeOnly? BreakStart = null,
    TimeOnly? BreakEnd = null,
    bool IsFlexible = false,
    int? RequiredMinutes = null);

/// <summary>Jadval yaratish va tahrirlash tanasi.</summary>
/// <param name="Name">Nom.</param>
/// <param name="GraceInMinutes">Kelishdagi yon berish.</param>
/// <param name="GraceOutMinutes">Ketishdagi yon berish.</param>
/// <param name="Note">Izoh.</param>
/// <param name="Days">Yettala kun.</param>
/// <param name="EarlyArrivalThresholdMinutes">Erta kelish chegarasi (0…240), berilmasa 0.</param>
/// <param name="LateDepartureThresholdMinutes">Kech ketish chegarasi (0…240), berilmasa 0.</param>
public sealed record WorkScheduleRequest(
    string? Name,
    int GraceInMinutes,
    int GraceOutMinutes,
    string? Note,
    IReadOnlyList<WorkScheduleDayRequest>? Days,
    int EarlyArrivalThresholdMinutes = 0,
    int LateDepartureThresholdMinutes = 0)
{
    /// <summary>Use-case maydonlariga.</summary>
    internal WorkScheduleSettings ToSettings() => new(
        Name,
        GraceInMinutes,
        GraceOutMinutes,
        Note,
        Days is null
            ? null
            : [
                .. Days.Select(day => new WorkScheduleDayInput(
                    day.DayIndex,
                    day.IsWorking,
                    day.StartTime,
                    day.EndTime,
                    day.BreakMinutes,
                    day.BreakStart,
                    day.BreakEnd,
                    day.IsFlexible,
                    day.RequiredMinutes)),
            ],
        EarlyArrivalThresholdMinutes,
        LateDepartureThresholdMinutes);
}

/// <summary>Jadval biriktirish tanasi.</summary>
/// <param name="WorkScheduleId">Jadval.</param>
/// <param name="Scope">Employee | Position | Department | Default.</param>
/// <param name="EmployeeId">Faqat Employee pog'onasida.</param>
/// <param name="PositionId">Faqat Position pog'onasida.</param>
/// <param name="DepartmentId">Faqat Department pog'onasida.</param>
/// <param name="EffectiveFrom">Kuchga kirish sanasi.</param>
/// <param name="EffectiveTo">Tugash sanasi (shu kun ham kiradi).</param>
public sealed record ScheduleAssignmentRequest(
    Guid WorkScheduleId,
    ScheduleScope Scope,
    Guid? EmployeeId,
    Guid? PositionId,
    Guid? DepartmentId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo = null);

/// <summary>Qayta hisoblash tanasi.</summary>
/// <param name="From">Davr boshi.</param>
/// <param name="To">Davr oxiri (92 kundan oshmasin).</param>
/// <param name="EmployeeId">Faqat shu xodim; bo'sh — davrga tegishli hamma.</param>
public sealed record RecomputeAttendanceRequest(DateOnly From, DateOnly To, Guid? EmployeeId = null);

/// <summary>
/// Davomat moduli endpointlari — qabul yo'lidan (<see cref="AttendanceIngestEndpoints"/>) TASHQARI hammasi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ruxsatlar — SPEC §7.3 jadvali, aynan.</b> Ikki guruh:
/// <see cref="Permissions.AttendanceView"/> (jurnal, taxta, tabel, hisobotlar,
/// jadvallarni O'QISH) va <see cref="Permissions.AttendanceManage"/> (qurilmalar,
/// mosliklar, import, jadval va biriktirmalarni o'zgartirish, qayta hisoblash).
/// Qurilmalar va mosliklar O'QISH uchun ham <c>manage</c>: qurilma ro'yxati token
/// prefikslari, IP ro'yxatlari va kirish joylarini, moslik ro'yxati esa terminal
/// raqamidan tirik odamga xaritani ko'rsatadi — hujumchi birinchi shularni qidiradi.
/// </para>
/// <para>
/// <b>Eksport</b> — <see cref="Permissions.AttendanceView"/> VA
/// <see cref="Permissions.ReportsExport"/>, <c>RateLimitPolicies.Export</c> bilan
/// (<c>ReportsEndpoints</c> bilan bir xil sabab: fayl bo'lib binodan chiqadigan
/// og'ir amal). Ikki siyosat birga — ikkalasi ham bajarilishi shart.
/// </para>
/// </remarks>
public static class AttendanceEndpoints
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Import so'rovining chegarasi: fayl + multipart sarlavhalari uchun zaxira.</summary>
    /// <remarks>
    /// Chegaradan katta tana Kestrel tomonidan umuman o'qilmaydi (413). Fayl
    /// chegarasining o'zi use-case'da tekshiriladi va tushunarli kod beradi
    /// (<c>davomat.import_fayl_katta</c>) — <c>EmployeeEndpoints</c> dagi rasm bilan bir xil.
    /// </remarks>
    private const long MaxImportRequestBytes = IdentityImportLimits.MaxFileBytes + (64 * 1024);

    /// <summary>Jadval so'rovi tanasining chegarasi — eng katta to'g'ri so'rov 16 KB dan kichik.</summary>
    internal const long MaxScheduleRequestBytes = 32 * 1024;

    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/attendance").WithTags("Attendance");

        MapDevices(group);
        MapEnrollments(group);
        MapIdentities(group);
        MapSchedules(group);
        MapAssignments(group);
        MapPasses(group);
        MapBoard(group);
        MapAbsenceRequests(group);
        MapScheduleRequests(group);
        MapReports(group);
        MapExports(group);
        MapRecompute(group);

        return app;
    }

    // ════════════════════════════════════════════════════════════
    //  Qurilmalar (SPEC §6.2) — attendance.manage
    // ════════════════════════════════════════════════════════════

    private static void MapDevices(IEndpointRouteBuilder group)
    {
        group.MapGet("/devices", async (
                bool? includeInactive,
                ListDevicesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListDevicesQuery(includeInactive ?? false), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("ListAttendanceDevices")
            .WithSummary("Davomat — qurilmalar reyestri")
            .WithDescription(
                "Nom bo'yicha. «includeInactive» (sukut false) — faolsizlantirilganlari ham. " +
                "Token HECH QACHON qaytmaydi, faqat «tokenPrefix»; imzo siri — faqat «hasHmacSecret». " +
                "«passCount24h» — so'nggi 24 soatda qabul qilingan o'tishlar, «skewWarning» — oxirgi " +
                "o'tishdagi soat og'ishi 300 soniyadan katta (serverda hisoblanadi).")
            .Produces<IReadOnlyList<AttendanceDeviceDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/devices/{id:guid}", async (
                Guid id,
                GetDeviceQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(new GetDeviceQuery(id), cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("GetAttendanceDevice")
            .WithSummary("Davomat — bitta qurilma")
            .WithDescription("Topilmasa — «davomat.qurilma_topilmadi» (404).")
            .Produces<AttendanceDeviceDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/devices", async (
                AttendanceDeviceRequest request,
                RegisterDeviceCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new RegisterDeviceCommand(request.ToSettings()), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("RegisterAttendanceDevice")
            .WithSummary("Davomat — qurilmani ro'yxatga olish")
            .WithDescription(
                "Javob { device, token } — token FAQAT SHU javobda bir marta ko'rsatiladi va boshqa " +
                "hech qachon qaytmaydi (yo'qolsa — «rotate-token»). Xatolar: «davomat.qurilma_nom_bosh», " +
                "«davomat.imzo_siri_yoq», «davomat.ip_royxati_notogri» (400); «yuridik_shaxs.topilmadi», " +
                "«bolim.topilmadi» (404); «davomat.qurilma_nom_takror», «davomat.qurilma_seriya_takror» (409).")
            .Produces<AttendanceDeviceCreatedDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/devices/{id:guid}", async (
                Guid id,
                AttendanceDeviceRequest request,
                UpdateDeviceCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new UpdateDeviceCommand(id, request.ToSettings()), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("UpdateAttendanceDevice")
            .WithSummary("Davomat — qurilmani tahrirlash")
            .WithDescription(
                "Token bu yerdan O'ZGARMAYDI. «hmacSecret»: null — eskisi qoladi, \"\" — o'chiriladi, " +
                "boshqa qiymat — almashtiriladi. Xatolar yaratishdagi bilan bir xil, qo'shimcha " +
                "«davomat.qurilma_topilmadi» (404).")
            .Produces<AttendanceDeviceDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/devices/{id:guid}/rotate-token", async (
                Guid id,
                RotateDeviceTokenCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new RotateDeviceTokenCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("RotateAttendanceDeviceToken")
            .WithSummary("Davomat — qurilma tokenini almashtirish")
            .WithDescription(
                "Yangi token { device, token } javobida bir marta qaytadi. ESKI TOKEN SHU ZAHOTI O'LADI: " +
                "terminal yangisini olmaguncha yuborgan hamma narsasi 401.")
            .Produces<AttendanceDeviceCreatedDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/devices/{id:guid}/activate", async (
                Guid id,
                SetDeviceActiveCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new SetDeviceActiveCommand(id, Active: true), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("ActivateAttendanceDevice")
            .WithSummary("Davomat — qurilmani yoqish")
            .WithDescription("Token o'zgarmaydi — terminalni qayta sozlash kerak emas.")
            .Produces<AttendanceDeviceDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/devices/{id:guid}/deactivate", async (
                Guid id,
                SetDeviceActiveCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new SetDeviceActiveCommand(id, Active: false), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("DeactivateAttendanceDevice")
            .WithSummary("Davomat — qurilmani o'chirish (faolsizlantirish)")
            .WithDescription(
                "Faolsiz qurilma hodisalari 403 bilan rad etiladi va hech narsa yozilmaydi. " +
                "O'tishlari bor qurilma uchun o'chirish (DELETE) o'rniga shu yo'l.")
            .Produces<AttendanceDeviceDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/devices/{id:guid}", async (
                Guid id,
                DeleteDeviceCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(new DeleteDeviceCommand(id), cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("DeleteAttendanceDevice")
            .WithSummary("Davomat — qurilmani o'chirish")
            .WithDescription(
                "Yumshoq o'chirish; tokeni ham o'ladi. O'tish yozgan qurilma o'chirilmaydi — " +
                "«davomat.qurilma_otishlari_bor» (409): jurnal o'tishning manbasini yo'qotmasin, faolsizlantiring.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    // ════════════════════════════════════════════════════════════
    //  Qurilmaga yozish holati (SPEC-agent §D5) — attendance.manage
    // ════════════════════════════════════════════════════════════

    private static void MapEnrollments(IEndpointRouteBuilder group)
    {
        group.MapGet("/devices/{id:guid}/enrollments", async (
                Guid id,
                GetDeviceEnrollmentsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetDeviceEnrollmentsQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("GetDeviceEnrollments")
            .WithSummary("Davomat — qurilmaga kim yozilgan")
            .WithDescription(
                "«summary» — sanoqlar (ro'yxatda, yozilgan, navbatda, xato, rasmi yo'q) va agentning " +
                "oxirgi aloqasi. «rows» — faqat E'TIBOR TALAB QILADIGANLARI: joyida turgan yuzlab " +
                "xodim ro'yxatlanmaydi. Qurilma yo'q — «davomat.qurilma_topilmadi» (404).")
            .Produces<DeviceEnrollmentPageDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/devices/{id:guid}/enrollments/resync", async (
                Guid id,
                ResyncDeviceEnrollmentsCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ResyncDeviceEnrollmentsCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("ResyncDeviceEnrollments")
            .WithSummary("Davomat — hammasini sinxronlash")
            .WithDescription(
                "XATO qatorlarni qayta urinishga qo'yadi. Qurilmadagi odam O'CHIRILIB qayta " +
                "yozilmaydi va joyida turgan xodimga umuman tegilmaydi (SPEC-agent §E) — agent " +
                "yetishmaganini o'zi tugmasiz ham yozadi. «affected» — qayta urinishga qo'yilgan qator soni.")
            .Produces<EnrollmentResyncDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/devices/{id:guid}/enrollments/approve-removals", async (
                Guid id,
                ApproveDeviceRemovalsCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ApproveDeviceRemovalsCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("ApproveDeviceRemovals")
            .WithSummary("Davomat — ushlab qolingan o'chirishlarni tasdiqlash")
            .WithDescription(
                "Agent ro'yxat bo'sh bo'lsa yoki terminaldagi Bonvi odamlarining beshdan biridan ko'pi " +
                "o'chirilishi kerak bo'lsa, o'chirmaydi va sonini kartaga yozadi («heldRemovals»). Bu tugma " +
                "to'siqni 10 daqiqaga ochadi — agent keyingi tekshiruvida o'chiradi. Doimiy o'chirib qo'yish " +
                "yo'q. Qurilma yo'q — «davomat.qurilma_topilmadi» (404).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/employees/{id:guid}/enrollments", async (
                Guid id,
                GetEmployeeEnrollmentsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetEmployeeEnrollmentsQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            // `manage`, `view` EMAS (SPEC-agent §D5): javobda qurilmadagi shaxs
            // raqami bor — bu terminal boshqaruvining ma'lumoti.
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("GetEmployeeEnrollments")
            .WithSummary("Davomat — xodim qaysi qurilmada bor")
            .WithDescription(
                "Har bir FAOL qurilma bo'yicha bitta qator. Holat serverda hisoblanadi: «Synced» — " +
                "qurilmada bor va joriy, «Pending» — navbatda, «Failed» — qurilma rad etgan, " +
                "«PhotoMissing» — rasmi yo'q, «Removed» — olib tashlangan. " +
                "Xodim yo'q — «davomat.xodim_topilmadi» (404).")
            .Produces<IReadOnlyList<DeviceEnrollmentRowDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/employees/{id:guid}/enrollments/resync", async (
                Guid id,
                ResyncEmployeeEnrollmentsCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ResyncEmployeeEnrollmentsCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("ResyncEmployeeEnrollments")
            .WithSummary("Davomat — xodimni qurilmaga qayta yozish")
            .WithDescription(
                "Xodimning har bir faol qurilmadagi belgisini eskirgan qiladi; agent keyingi " +
                "tekshiruvida (60 soniyagacha) O'SHA RAQAM ostida qayta yozadi — o'chirib emas. " +
                "Xodim yo'q — «davomat.xodim_topilmadi» (404).")
            .Produces<EnrollmentResyncDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // ════════════════════════════════════════════════════════════
    //  Mosliklar va Verifix importi (SPEC §4, §6.3) — attendance.manage
    // ════════════════════════════════════════════════════════════

    private static void MapIdentities(IEndpointRouteBuilder group)
    {
        group.MapGet("/identities", async (
                string? search,
                Guid? employeeId,
                AttendanceIdentityProvider? provider,
                int? page,
                int? pageSize,
                SearchIdentitiesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new SearchIdentitiesQuery(search, employeeId, provider, page, pageSize), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("ListAttendanceIdentities")
            .WithSummary("Davomat — qurilma raqami ↔ xodim mosliklari")
            .WithDescription(
                "Sahifalangan, xodim familiyasi bo'yicha. «search» — qurilmadagi shaxs raqami YOKI xodim " +
                "(F.I.Sh., JSHSHIR, pasport, telefon). «photoKey» — xodim kartasidagi rasm.")
            .Produces<PagedResult<AttendanceIdentityDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/identities", async (
                CreateAttendanceIdentityRequest request,
                CreateIdentityCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new CreateIdentityCommand(
                            request.Provider ?? AttendanceIdentityProvider.Verifix,
                            request.ExternalPersonId,
                            request.EmployeeId,
                            request.DisplayNameInDevice,
                            request.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("CreateAttendanceIdentity")
            .WithSummary("Davomat — qurilma raqamini xodimga bog'lash")
            .WithDescription(
                "Saqlangach shu raqamning ESKI moslanmagan o'tishlari xodimga bog'lanadi va o'sha kunlar " +
                "qayta hisoblanadi (SPEC §4.3) — alohida «qayta hisoblash» kerak emas. Xatolar: " +
                "«davomat.shaxs_raqami_bosh», «davomat.shaxs_raqami_uzun», «davomat.xodim_yoq» (400); " +
                "«davomat.xodim_topilmadi» (404); «davomat.moslik_takror» (409).")
            .Produces<AttendanceIdentityDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/identities/{id:guid}", async (
                Guid id,
                DeleteIdentityCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(new DeleteIdentityCommand(id), cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("DeleteAttendanceIdentity")
            .WithSummary("Davomat — moslikni uzish")
            .WithDescription(
                "Yumshoq o'chirish. O'TGAN o'tishlar xodimga bog'langanicha QOLADI (SPEC §4.3) — moslik " +
                "xato bo'lsa, to'g'risini yarating va davrni «/recompute» bilan qayta hisoblang. " +
                "Topilmasa — «davomat.moslik_topilmadi» (404).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/identities/unmatched", async (
                int? page,
                int? pageSize,
                ListUnmatchedQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListUnmatchedQuery(page, pageSize), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("ListUnmatchedAttendancePersons")
            .WithSummary("Davomat — moslanmagan shaxs raqamlari")
            .WithDescription(
                "Moslanmagan o'tishlar (fazo, shaxs raqami) bo'yicha guruhlangan, eng ko'p o'tgani birinchi. " +
                "«samplePassId» — surati bor eng yangi o'tish: yuzni «/passes/{id}/photo» dan oling.")
            .Produces<PagedResult<UnmatchedGroupDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/identities/import", async (
                IFormFile file,
                [FromForm] AttendanceIdentityProvider? provider,
                PreviewIdentityImportQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                await using var content = file.OpenReadStream();

                var natija = await handler
                    .HandleAsync(
                        new PreviewIdentityImportQuery(
                            content,
                            file.FileName,
                            file.Length,
                            provider ?? AttendanceIdentityProvider.Verifix),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)

            // CSRF himoyasi ortiqcha — JWT sarlavhada yuriladi (`EmployeeEndpoints` dagi sabab).
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxImportRequestBytes))
            .WithName("PreviewAttendanceIdentityImport")
            .WithSummary("Davomat — Verifix ro'yxatini oldindan ko'rish")
            .WithDescription(
                "«multipart/form-data»: «file» (.xlsx yoki .csv, 10 MB gacha, 5 000 qatorgacha) va ixtiyoriy " +
                "«provider» (sukut Verifix). HECH NARSA YOZILMAYDI — har bir qator uchun moslash natijasi " +
                "qaytadi: AlreadyMapped → ExactPinfl → ExactName → Ambiguous (2…5 nomzod) → None. Sarlavha " +
                "birinchi 10 qatordan qidiriladi. Xatolar: «davomat.import_fayl_turi», «davomat.import_fayl_katta», " +
                "«davomat.import_qator_kop», «davomat.import_ustun_topilmadi» (400).")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<IdentityImportPreviewDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/identities/import/confirm", async (
                ConfirmIdentityImportRequest request,
                ConfirmIdentityImportCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ConfirmIdentityImportCommand(
                            request.Provider ?? AttendanceIdentityProvider.Verifix,
                            request.Rows?
                                .Select(row => new IdentityImportDecision(row.RawPersonId, row.EmployeeId, row.DisplayNameInDevice))
                                .ToList()),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("ConfirmAttendanceIdentityImport")
            .WithSummary("Davomat — Verifix importini tasdiqlash")
            .WithDescription(
                "Tasdiqlangan qatorlardan mosliklar yaratiladi (manba — Import) va har biri uchun eski " +
                "o'tishlar bog'lanadi. Allaqachon bog'langan raqam — xato emas, «skipped». Qator xatosi " +
                "qolganlarini to'xtatmaydi: «failed» da raqam va kod. 5 000 dan ko'p qator — " +
                "«davomat.import_qator_kop» (400).")
            .Produces<IdentityImportResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    // ════════════════════════════════════════════════════════════
    //  Jadvallar (SPEC §6.4)
    // ════════════════════════════════════════════════════════════

    private static void MapSchedules(IEndpointRouteBuilder group)
    {
        group.MapGet("/schedules", async (
                ListWorkSchedulesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(new ListWorkSchedulesQuery(), cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("ListWorkSchedules")
            .WithSummary("Davomat — ish jadvallari")
            .WithDescription("Nom bo'yicha, faolsizlari ham; har birida yettala kun.")
            .Produces<IReadOnlyList<WorkScheduleDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // `/resolve` `{id:guid}` dan OLDIN e'lon qilinadi — tartib ahamiyatsiz
        // (cheklov «resolve» ni guid deb olmaydi), lekin o'qiyotgan uchun aniq.
        group.MapGet("/schedules/resolve", async (
                Guid employeeId,
                DateOnly date,
                ResolveScheduleQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ResolveScheduleQuery(employeeId, date), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("ResolveWorkSchedule")
            .WithSummary("Davomat — «nega bu odamda shu jadval?»")
            .WithDescription(
                "Tabel hisoblashi ishlatadigan AYNAN o'sha qoida bilan. Natija ikki null bilan o'qiladi: " +
                "«employmentId» null — shu sanada ishlamaydi; aks holda «scope» null — jadval topilmadi; " +
                "aks holda hal qilingan. Bo'lim pog'onasida «matchedDepartment*» — biriktirmasi yutgan " +
                "bo'lim (ota bo'lim bo'lishi mumkin). Xodim yo'q — «davomat.xodim_topilmadi» (404).")
            .Produces<ScheduleResolutionDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/schedules/{id:guid}", async (
                Guid id,
                GetWorkScheduleQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(new GetWorkScheduleQuery(id), cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("GetWorkSchedule")
            .WithSummary("Davomat — bitta ish jadvali")
            .WithDescription("Topilmasa — «davomat.jadval_topilmadi» (404).")
            .Produces<WorkScheduleDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/schedules", async (
                WorkScheduleRequest request,
                CreateWorkScheduleCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new CreateWorkScheduleCommand(request.ToSettings()), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("CreateWorkSchedule")
            .WithSummary("Davomat — ish jadvali yaratish")
            .WithDescription(
                "Haftalik jadval: 1…7 kunlarning har biri AYNAN bir marta. Tanaffus oynasi («breakStart»/" +
                "«breakEnd») ikkalasi birga yoki ikkalasi ham null; oyna bo'lmasa tanaffus ishlangan vaqtdan " +
                "AYIRILMAYDI. Dam olish kunidagi oyna qabul qilinib tashlab yuboriladi. Xatolar: " +
                "«davomat.jadval_nom_bosh», «davomat.jadval_kun_takror», «davomat.jadval_kun_yetishmaydi», " +
                "«davomat.jadval_vaqt_bosh», «davomat.jadval_vaqt_teng», «davomat.jadval_yon_berish», " +
                "«davomat.jadval_tanaffus_toliq_emas», «davomat.jadval_tanaffus_oynasi», " +
                "«davomat.jadval_tanaffus_uzunligi», «davomat.jadval_erkin_vaqti», «davomat.jadval_chegara» (400); " +
                "«davomat.jadval_nom_takror» (409). " +
                "«isFlexible» — erkin ish jadvali: kechikish va erta ketish yo'q, kun «requiredMinutes» (ish vaqti) " +
                "bilan baholanadi; «requiredMinutes» bo'sh bo'lsa — smena − tanaffus. Dam olish kunida bayroq tozalanadi. " +
                "«earlyArrivalThresholdMinutes» / «lateDepartureThresholdMinutes» (0…240, sukut 0) — reja boshlanishidan " +
                "oldingi va tugashidan keyingi shuncha vaqt ishlangan vaqtga qo'shilmaydi, undan tashqarisi qo'shiladi; " +
                "faqat qat'iy ish kunida.")
            .Produces<WorkScheduleDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/schedules/{id:guid}", async (
                Guid id,
                WorkScheduleRequest request,
                UpdateWorkScheduleCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new UpdateWorkScheduleCommand(id, request.ToSettings()), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("UpdateWorkSchedule")
            .WithSummary("Davomat — ish jadvalini tahrirlash")
            .WithDescription(
                "Tana yaratishdagi bilan bir xil (xatolar ham, «davomat.jadval_erkin_vaqti» bilan); oynani o'chirish — " +
                "ikkala maydonni null yuborish. " +
                "⚠️ O'tgan kunlar AVTOMATIK qayta hisoblanmaydi — kerakli davr uchun «/recompute» chaqiring.")
            .Produces<WorkScheduleDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Yillik to'r (SPEC-jadval-to'ri §C). O'qish — `attendance.view`:
        // tabelni o'qiyotgan HR «bu kun nega dam olish?» degan savolga
        // javobni shu yerdan topadi.
        group.MapGet("/schedules/{id:guid}/year", async (
                Guid id,
                int? year,
                GetScheduleYearQueryHandler handler,
                IClock clock,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetScheduleYearQuery(id, year ?? clock.Today.Year), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("GetWorkScheduleYear")
            .WithSummary("Davomat — jadvalning yillik to'ri")
            .WithDescription(
                "Yilning HAR BIR kuni, o'sish tartibida va TIRQISHSIZ. «plannedMinutes» — kun normasi " +
                "(tanaffus ayirilgan), ekranda soat bo'lib chiqadi. «source»: Pattern — qiymat haftalik " +
                "naqshdan keladi, Date — shu sanaga QO'LDA yozilgan katak. «year» berilmasa — joriy yil " +
                "(Toshkent). Yil 2000–2100 oralig'idan tashqarida bo'lsa — «davomat.jadval_yili_notogri» (400).")
            .Produces<ScheduleYearDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/schedules/{id:guid}/dates", async (
                Guid id,
                ScheduleDatesBody body,
                SetScheduleDatesCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new SetScheduleDatesCommand(
                            id,
                            body.Dates ?? [],
                            body.Reset,
                            body.IsWorking,
                            body.StartTime,
                            body.EndTime,
                            body.BreakMinutes,
                            body.BreakStart,
                            body.BreakEnd,
                            body.IsFlexible,
                            body.RequiredMinutes),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("SetWorkScheduleDates")
            .WithSummary("Davomat — yillik to'r kataklarini yozish yoki tozalash")
            .WithDescription(
                "Bitta chaqiruv bilan bir nechta sana: bitta katak ham, oyning qatori ham, butun yil ham " +
                "shu yerdan. «reset: true» — kataklar O'CHIRILADI va kunlar haftalik naqshga qaytadi " +
                "(qolgan maydonlar e'tiborga olinmaydi). Aks holda ish kuni uchun «startTime» va «endTime» " +
                "SHART («davomat.jadval_vaqt_bosh»), tanaffus oynasi smena ichida bo'lishi kerak; erkin katakda " +
                "«requiredMinutes» 1 … smena − tanaffus («davomat.jadval_erkin_vaqti»). " +
                "Eng ko'pi 400 ta sana. ⭐ Yozuvdan keyin BUGUNGACHA bo'lgan kunlar qayta hisoblanadi: " +
                "jadval o'zgarishi kechagi tabelni ham o'zgartiradi. Kelajakdagi kunlar qayta hisoblanmaydi " +
                "— ularning saqlangan yozuvi yo'q.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // «Yilni naqshdan to'ldirish» (SPEC-jadval-to'ri §D). «Yilni tozalash»
        // uchun alohida endpoint YO'Q: u — yilning yozilgan sanalariga
        // `PUT /dates` bilan `reset: true`, ya'ni bitta imo — bitta chaqiruv.
        group.MapPost("/schedules/{id:guid}/year/fill", async (
                Guid id,
                ScheduleYearFillBody body,
                FillScheduleYearCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new FillScheduleYearCommand(id, body.Year), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("FillWorkScheduleYear")
            .WithSummary("Davomat — yilni haftalik naqshdan to'ldirish")
            .WithDescription(
                "Yilning katagi YOZILMAGAN har bir kuniga haftalik naqshdagi qiymat (vaqtlar, tanaffus va " +
                "uning oynasi) yoziladi; qo'lda yozilgan kataklarga TEGILMAYDI. Hisob o'zgarmaydi (katak " +
                "naqsh bilan bir xil), shu sababli qayta hisoblash navbatga qo'yilmaydi. Shundan keyin bu yil " +
                "haftalik naqshga ergashmaydi. Yil 2000–2100 dan tashqarida — «davomat.jadval_yili_notogri» " +
                "(400); haftalik bo'lmagan jadval — «davomat.jadval_turi_qollanmaydi» (400).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/schedules/{id:guid}/activate", async (
                Guid id,
                SetWorkScheduleActiveCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new SetWorkScheduleActiveCommand(id, Active: true), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("ActivateWorkSchedule")
            .WithSummary("Davomat — ish jadvalini faollashtirish")
            .Produces<WorkScheduleDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/schedules/{id:guid}/deactivate", async (
                Guid id,
                SetWorkScheduleActiveCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new SetWorkScheduleActiveCommand(id, Active: false), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("DeactivateWorkSchedule")
            .WithSummary("Davomat — ish jadvalini faolsizlantirish")
            .WithDescription(
                "Biriktirmasi bor jadval faolsizlantirilmaydi — «davomat.jadval_biriktirilgan» (409): tabel " +
                "hisoblashi faollikka qaramaydi, ya'ni ekran «faolsiz» deb, tabel esa jadvalni ishlatishda davom " +
                "etgan bo'lardi. Avval biriktirmalarni olib tashlang.")
            .Produces<WorkScheduleDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/schedules/{id:guid}", async (
                Guid id,
                DeleteWorkScheduleCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeleteWorkScheduleCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("DeleteWorkSchedule")
            .WithSummary("Davomat — ish jadvalini o'chirish")
            .WithDescription(
                "Yumshoq o'chirish. Biriktirmasi bor jadval (muddati o'tganlari ham) o'chirilmaydi — " +
                "«davomat.jadval_biriktirilgan» (409).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapAssignments(IEndpointRouteBuilder group)
    {
        group.MapGet("/schedule-assignments", async (
                ScheduleScope? scope,
                Guid? employeeId,
                Guid? departmentId,
                Guid? positionId,
                DateOnly? onDate,
                ListScheduleAssignmentsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ListScheduleAssignmentsQuery(scope, employeeId, departmentId, positionId, onDate),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("ListScheduleAssignments")
            .WithSummary("Davomat — jadval biriktirmalari")
            .WithDescription(
                "Pog'ona, so'ng boshlanish sanasi (yangisi birinchi) bo'yicha. «employeeId»/«departmentId»/" +
                "«positionId» — aynan SHU obyektga qilingan biriktirmalar (ota bo'limnikilar emas; «kimga qaysi " +
                "jadval tushadi» savoli uchun — «/schedules/resolve»). «onDate» — shu sanada kuchdagilari.")
            .Produces<IReadOnlyList<ScheduleAssignmentDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/schedule-assignments", async (
                ScheduleAssignmentRequest request,
                CreateScheduleAssignmentCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new CreateScheduleAssignmentCommand(
                            request.WorkScheduleId,
                            request.Scope,
                            request.EmployeeId,
                            request.PositionId,
                            request.DepartmentId,
                            request.EffectiveFrom,
                            request.EffectiveTo),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("CreateScheduleAssignment")
            .WithSummary("Davomat — jadval biriktirish")
            .WithDescription(
                "Pog'onaga mos BITTA obyekt maydoni to'ldiriladi (Default da hech biri). ⚠️ O'tgan kunlar " +
                "avtomatik qayta hisoblanmaydi — «/recompute». Xatolar: «davomat.jadval_yoq», " +
                "«davomat.biriktirma_obyekt_yoq», «davomat.davr_notogri» (400); «davomat.jadval_topilmadi», " +
                "«davomat.xodim_topilmadi», «lavozim.topilmadi», «bolim.topilmadi» (404); " +
                "«davomat.jadval_faolsiz», «davomat.biriktirma_takror» (409).")
            .Produces<ScheduleAssignmentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/schedule-assignments/{id:guid}", async (
                Guid id,
                DeleteScheduleAssignmentCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeleteScheduleAssignmentCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("DeleteScheduleAssignment")
            .WithSummary("Davomat — jadval biriktirmasini olib tashlash")
            .WithDescription(
                "Yumshoq o'chirish. Saqlangan kunlar qayta hisoblanmaguncha o'zgarmaydi. " +
                "Topilmasa — «davomat.biriktirma_topilmadi» (404).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // ════════════════════════════════════════════════════════════
    //  O'tishlar jurnali (SPEC §6.5) — attendance.view
    // ════════════════════════════════════════════════════════════

    private static void MapPasses(IEndpointRouteBuilder group)
    {
        group.MapGet("/passes", async (
                DateOnly? from,
                DateOnly? to,
                Guid? employeeId,
                Guid? deviceId,
                PassDirection? direction,
                PassMethod? method,
                bool? matched,
                Guid? legalEntityId,
                Guid? departmentId,
                string? search,
                int? page,
                int? pageSize,
                string? sort,
                SearchPassesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new SearchPassesQuery(
                            from, to, employeeId, deviceId, direction, method, matched,
                            legalEntityId, departmentId, search, page, pageSize, sort),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("SearchAttendancePasses")
            .WithSummary("Davomat — o'tishlar jurnali")
            .WithDescription(
                "Qurilma AYTGANI, talqinsiz. Davr «from»/«to» ikki tomondan yopiq; berilmasa — bugun, " +
                "92 kundan oshmasin («davomat.davr_uzun», «davomat.davr_notogri» — 400). «matched» — " +
                "true/false/bo'sh. «search» — xodim yoki qurilmadagi shaxs raqami. «sort» — occurredAt | " +
                "-occurredAt (sukut). «photoKey» — TURNIKET surati, «employeePhotoKey» — xodim kartasidagi rasm.")
            .Produces<PagedResult<AttendancePassDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/passes/{id:guid}", async (
                Guid id,
                GetPassQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(new GetPassQuery(id), cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("GetAttendancePass")
            .WithSummary("Davomat — bitta o'tish")
            .WithDescription(
                "Jurnal qatori bilan bir xil shakl — ishdan ketganlar ogohlantirishidagi havola uchun. " +
                "Yo'q yoki saqlash muddati tugab o'chirilgan — «davomat.otish_topilmadi» (404).")
            .Produces<AttendancePassDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/passes/{id:guid}/photo", async (
                Guid id,
                HttpContext context,
                GetPassPhotoQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetPassPhotoQuery(id, PhotoHttp.ReadKnownVersion(context.Request)),
                        cancellationToken)
                    .ConfigureAwait(false);

                // JSON emas, BAYT — `ToHttpResult()` faqat xato yo'lida
                // (xodim rasmi endpointidagi bilan bir xil sabab).
                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : PhotoHttp.Write(context, natija.Value);
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("GetAttendancePassPhoto")
            .WithSummary("Davomat — o'tishning turniket surati")
            .WithDescription(
                "Rasm baytlari (image/jpeg yoki image/png), «ETag» + «If-None-Match» → 304, kesh shaxsiy " +
                "(private, 1 kun). O'tish yo'q — «davomat.otish_topilmadi», surat yo'q yoki tozalangan — " +
                "«davomat.surat_yoq» (ikkalasi 404).")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // ════════════════════════════════════════════════════════════
    //  Jonli taxta, asosiy sahifa paneli va ishdan ketganlar (SPEC §6.5, §8.7) — attendance.view
    // ════════════════════════════════════════════════════════════

    private static void MapBoard(IEndpointRouteBuilder group)
    {
        group.MapGet("/board", async (
                Guid? legalEntityId,
                Guid? departmentId,
                string? search,
                GetBoardQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetBoardQuery(legalEntityId, departmentId, search), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("GetAttendanceBoard")
            .WithSummary("Davomat — «hozir kim ishda» taxtasi")
            .WithDescription(
                "Xom o'tishlardan, shu zahoti. «lateCount» hali kelmaganlarni ham sanaydi. Yo'nalishi noma'lum " +
                "qurilma o'tishlari «ichkarida» ro'yxatiga kirmaydi. ⚠️ FILTRLAR («legalEntityId», «departmentId», " +
                "«search») ISHDAN KETGANLAR OGOHLANTIRISHIGA («formerEmployeeAlerts») TA'SIR QILMAYDI: ishdan " +
                "ketgan odamning bugungi bo'limi yo'q, va bo'lim filtri yashira oladigan xavfsizlik " +
                "ogohlantirishi ogohlantirish emas.")
            .Produces<AttendanceBoardDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // Asosiy sahifa paneli. Parametrlar `/timesheet` dagidek alohida-alohida
        // bog'lanadi — so'rov yozuvi (va unga yopishadigan `Guid = default`
        // tuzog'i) yo'q.
        group.MapGet("/dashboard", async (
                DateOnly? date,
                string? search,
                Guid? legalEntityId,
                Guid? departmentId,
                Guid? positionId,
                AttendanceDashboardStatus? status,
                string? sort,
                int? page,
                int? pageSize,
                GetAttendanceDashboardQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetAttendanceDashboardQuery(
                            date, search, legalEntityId, departmentId, positionId, status, sort, page, pageSize),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("GetAttendanceDashboard")
            .WithSummary("Davomat — asosiy sahifa paneli (kunlik holatlar)")
            .WithDescription(
                "Xom o'tishlardan, shu zahoti. «date» berilmasa — bugun (Toshkent). To'plam — shu sanada mehnat " +
                "munosabati bor xodimlar; munosabati yo'q odam umuman chiqmaydi. Holat: OnTime | Late | Absent | " +
                "NotStarted | DayOff | NoSchedule; «lateMinutes» faqat Late da, REJA BOSHLANISHIDAN sanaladi. " +
                "«legalEntityId», «departmentId» (ichki bo'limlari bilan), «positionId» — tabeldagi bilan bir xil. " +
                "⚠️ «total» va «counts» QIDIRUV («search») VA HOLAT («status») FILTRIGA BO'YSUNMAYDI — ular faqat " +
                "«items», «totalItems» va «pageCount» ni toraytiradi; oltita sanoq yig'indisi doim «total». " +
                "«sort» — name (sukut) | -name. «pageSize» sukut 50, eng ko'pi 200.")
            .Produces<AttendanceDashboardDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // Davr grafigi. «from» va «to» MAJBURIY va shu sababli `DateOnly?`
        // emas: berilmagan oraliq «bugun» degani bo'lardi, u esa
        // `/dashboard` ning o'zi. Qidiruv, holat, saralash va sahifa YO'Q —
        // bu grafik, ro'yxat emas.
        group.MapGet("/dashboard/trend", async (
                DateOnly from,
                DateOnly to,
                Guid? legalEntityId,
                Guid? departmentId,
                Guid? positionId,
                GetAttendanceTrendQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetAttendanceTrendQuery(from, to, legalEntityId, departmentId, positionId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("GetAttendanceTrend")
            .WithSummary("Davomat — davr grafigi (kunma-kun holatlar)")
            .WithDescription(
                "Xom o'tishlardan, «/dashboard» bilan AYNAN bir xil qoida bo'yicha: bir kunlik oraliq so'ralsa " +
                "«days[0].counts» panelning «counts» i bilan teng chiqadi. «from» va «to» majburiy, ikkala " +
                "chegara ham kiradi va ular TOSHKENT sanasi. ⚠️ «days» da oraliqning HAR BIR sanasi bor, " +
                "o'sish tartibida, TIRQISHSIZ: hech kim ishlamagan kun ham «total: 0» va oltita nol bilan " +
                "turadi. Oltita sanoq yig'indisi doim «total». «legalEntityId», «departmentId» (ichki " +
                "bo'limlari bilan), «positionId» — paneldagi bilan bir xil va HAR BIR kunga alohida qo'llanadi. " +
                "«from» > «to» — «davomat.davr_notogri»; 92 kundan uzun davr — «davomat.davr_uzun» (ikkalasi 400).")
            .Produces<AttendanceTrendDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // Tug'ilgan kunlar paneli. Sana parametri YO'Q: «bugun» serverda
        // (`IClock`), Toshkent vaqti bo'yicha. Davr ham yo'q — bu «keyingi
        // navbatda kim» degan savol, tanlangan oraliqning kesimi emas.
        group.MapGet("/dashboard/birthdays", async (
                Guid? legalEntityId,
                Guid? departmentId,
                Guid? positionId,
                int? limit,
                GetUpcomingBirthdaysQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetUpcomingBirthdaysQuery(legalEntityId, departmentId, positionId, limit),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("GetUpcomingBirthdays")
            .WithSummary("Davomat — yaqinlashib kelayotgan tug'ilgan kunlar")
            .WithDescription(
                "Eng yaqinidan boshlab tartiblangan: «daysUntil» o'sish bo'yicha, teng sanalar ism bo'yicha. " +
                "Bugungi tug'ilgan kun ham kiradi («daysUntil: 0»). To'plam — BUGUN mehnat munosabati bor " +
                "xodimlar; «legalEntityId», «departmentId» (ichki bo'limlari bilan), «positionId» paneldagi " +
                "bilan bir xil ma'noda. Tug'ilgan sanasi yo'q xodim umuman chiqmaydi. «limit» sukut 10, eng " +
                "ko'pi 50 — chegaradan tashqaridagi qiymat rad etilmaydi, QISILADI. ⚠️ 29-fevralda " +
                "tug'ilganlar kabisa bo'lmagan yilda ro'yxatga tushmaydi (sun'iy sana tanlanmaydi). " +
                "«age» — «nextBirthday» da to'ladigan yosh; shubhali sanada (0 dan kichik yoki 120 dan katta) " +
                "null. ⚠️ Bu «/api/dashboard/birthdays» EMAS: u faqat BUGUNGI kunni biladi va org-filtri yo'q.")
            .Produces<IReadOnlyList<AttendanceBirthdayDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/former-employee-alerts", async (
                GetFormerEmployeeAlertsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetFormerEmployeeAlertsQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("GetFormerEmployeeAlerts")
            .WithSummary("Davomat — ishdan ketganlar ogohlantirishi (nav nishoni)")
            .WithDescription(
                "Taxtadagi «formerEmployeeAlerts» bilan AYNAN bir xil qatorlar, bitta metoddan. Parametr va " +
                "filtr YO'Q. So'nggi 24 soatda o'tgan, o'tish sanasida mehnat munosabati bo'lmagan har bir " +
                "ODAM uchun bitta yozuv (eng so'nggi o'tishi bilan); «count» — odamlar, o'tishlar emas.")
            .Produces<FormerEmployeeAlertsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    // ════════════════════════════════════════════════════════════
    //  Jadval so'rovlari (SPEC-verifikatsiya-jadval-sorovi §C3)
    //  O'qish — attendance.view, yozish va qaror — attendance.requests.manage
    // ════════════════════════════════════════════════════════════

    private static void MapScheduleRequests(IEndpointRouteBuilder group)
    {
        const string ErrorCodes =
            "Xatolar: «davomat.jadval_sorovi_kun_yoq», «davomat.jadval_sorovi_kop», «davomat.jadval_sorovi_sana_takror», " +
            "«davomat.jadval_vaqt_bosh», «davomat.jadval_vaqt_teng», «davomat.jadval_tanaffus_toliq_emas», " +
            "«davomat.jadval_tanaffus_oynasi», «davomat.jadval_erkin_vaqti», «davomat.jadval_sorovi_bir_xil_sana», " +
            "«davomat.jadval_sorovi_jadval_yoq», «davomat.jadval_sorovi_tanaffus_oraligi», " +
            "«davomat.jadval_sorovi_tanaffus_davomiyligi» (tanaffus qoidasi — «Kunni o'zgartirish» ning ish kunida), " +
            "«davomat.izoh_uzun» (400); " +
            "«davomat.xodim_topilmadi» (404); «davomat.jadval_sorovi_takroriy» (409).";

        group.MapGet("/schedule-requests/break-rule", async (
                GetRequestBreakRuleQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(new GetRequestBreakRuleQuery(), cancellationToken).ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("GetScheduleRequestBreakRule")
            .WithSummary("Davomat — jadval so'rovidagi tanaffus qoidasi")
            .WithDescription(
                "«Kunni o'zgartirish» so'rovining ish kunida tanaffus «from»–«to» oralig'ida, «minutes» daqiqa " +
                "(jumada «minutes»–«fridayMaxMinutes»); tanaffussiz kun ham mumkin. Sozlama: " +
                "«attendance.requestBreakFrom/To/Minutes/FridayMaxMinutes» (PUT /api/settings). Forma oldindan " +
                "tekshiradi, server baribir o'zi tekshiradi.")
            .Produces<RequestBreakRuleDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/schedule-requests", async (
                Guid? employeeId,
                AbsenceRequestStatus? status,
                bool? decided,
                DateOnly? from,
                DateOnly? to,
                string? search,
                int? page,
                int? pageSize,
                SearchScheduleChangeRequestsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new SearchScheduleChangeRequestsQuery(employeeId, status, decided, from, to, search, page, pageSize),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("SearchScheduleChangeRequests")
            .WithSummary("Davomat — jadval so'rovlari")
            .WithDescription(
                "Yangisi birinchi. Filtrlar yo'qlik so'rovlaridagi bilan bir xil; «from»/«to» (ikkalasi ham kiradi, " +
                "istalgani berilmasligi mumkin) so'rovning KUNLARI bo'yicha: birorta kuni (almashtirishda — " +
                "ikkala sanadan biri) oraliq ICHIGA tushsa chiqadi. «from» > «to» — «davomat.davr_notogri» (400).")
            .Produces<PagedResult<ScheduleChangeRequestDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/schedule-requests/{id:guid}", async (
                Guid id,
                GetScheduleChangeRequestQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetScheduleChangeRequestQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("GetScheduleChangeRequest")
            .WithSummary("Davomat — bitta jadval so'rovi")
            .Produces<ScheduleChangeRequestDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/schedule-requests", async (
                ScheduleChangeRequestBody body,
                CreateScheduleChangeRequestCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new CreateScheduleChangeRequestCommand(
                            body.EmployeeId, body.Kind, body.DayInputs(), body.SwapInputs(), body.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceRequestsManage)
            .WithMetadata(new RequestSizeLimitAttribute(MaxScheduleRequestBytes))
            .WithName("CreateScheduleChangeRequest")
            .WithSummary("Davomat — jadval so'rovini yaratish (xodim nomidan)")
            .WithDescription(
                "«kind: Change» — «days» (1–31 sana: ish kuni — boshlanish/tugash, ixtiyoriy tanaffus va «isFlexible» + " +
                "«requiredMinutes»; yoki dam olish kuni). «kind: Swap» — «swaps» (1–15 juft): server ikkala sananing " +
                "AMALDAGI rejasini (erkinligi bilan) " +
                "o'rin almashtirib saqlaydi. Yangi so'rov tabelga ta'sir qilmaydi — faqat tasdiqlangach. " + ErrorCodes)
            .Produces<ScheduleChangeRequestDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/schedule-requests/{id:guid}", async (
                Guid id,
                ScheduleChangeRequestBody body,
                UpdateScheduleChangeRequestCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new UpdateScheduleChangeRequestCommand(
                            id, body.Kind, body.DayInputs(), body.SwapInputs(), body.Note, ExpectedVersion: body.Version),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceRequestsManage)
            .WithMetadata(new RequestSizeLimitAttribute(MaxScheduleRequestBytes))
            .WithName("UpdateScheduleChangeRequest")
            .WithSummary("Davomat — kutayotgan jadval so'rovini tahrirlash")
            .WithDescription(
                "Faqat KUTAYOTGAN so'rov («davomat.sorov_qaror_qabul_qilingan», 409). Xodim o'zgarmaydi — " +
                "«employeeId» e'tiborsiz qoldiriladi. «version» berilsa va so'rov boshqa joyda o'zgargan bo'lsa " +
                "(yoki parallel yozuv yutgan bo'lsa) — «davomat.sorov_ozgargan» (409). " + ErrorCodes)
            .Produces<ScheduleChangeRequestDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/schedule-requests/{id:guid}/decide", async (
                Guid id,
                AbsenceDecisionBody body,
                DecideScheduleChangeRequestCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new DecideScheduleChangeRequestCommand(id, body.Approve, body.DecisionNote, body.Version),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceRequestsManage)
            .WithName("DecideScheduleChangeRequest")
            .WithSummary("Davomat — jadval so'rovini tasdiqlash yoki rad etish")
            .WithDescription(
                "⭐ Tasdiqlangan kun xodimning o'sha sanadagi rejasi O'RNIGA turadi va kunlar (±1) shu yerning o'zida " +
                "qayta hisoblanadi. Jadvali yo'q xodimga qo'llanmaydi. Tanaffus qoidasi qarorda qayta tekshirilmaydi. " +
                "«davomat.sorov_qaror_qabul_qilingan» (409); «version» eskirgan yoki parallel yozuv — " +
                "«davomat.sorov_ozgargan» (409); «davomat.izoh_uzun» (400).")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces<ScheduleChangeRequestDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/schedule-requests/{id:guid}/cancel", async (
                Guid id,
                CancelScheduleChangeRequestCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new CancelScheduleChangeRequestCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceRequestsManage)
            .WithName("CancelScheduleChangeRequest")
            .WithSummary("Davomat — jadval so'rovini bekor qilish")
            .WithDescription(
                "Tasdiqlangani ham bekor qilinadi — kunlar jadvalning o'z rejasiga qaytadi va qayta hisoblanadi. " +
                "Allaqachon bekor qilingan — «davomat.sorov_bekor_qilingan» (409); parallel yozuv — " +
                "«davomat.sorov_ozgargan» (409).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    // ════════════════════════════════════════════════════════════
    //  Yo'qlik so'rovlari (SPEC-so'rovlar §B4)
    //  O'qish — attendance.view, qaror — attendance.requests.manage
    // ════════════════════════════════════════════════════════════

    private static void MapAbsenceRequests(IEndpointRouteBuilder group)
    {
        group.MapGet("/requests", async (
                Guid? employeeId,
                AbsenceRequestStatus? status,
                bool? decided,
                DateOnly? from,
                DateOnly? to,
                string? search,
                int? page,
                int? pageSize,
                Guid? absenceTypeId,
                AbsenceRequestMode? mode,
                SearchAbsenceRequestsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new SearchAbsenceRequestsQuery(
                            employeeId, status, decided, from, to, search, page, pageSize, absenceTypeId, mode),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("SearchAbsenceRequests")
            .WithSummary("Davomat — yo'qlik so'rovlari")
            .WithDescription(
                "Yangisi birinchi («createdAt» kamayish bo'yicha). «employeeId» — xodim kartasidagi ro'yxat, " +
                "usiz — butun tashkilot bo'yicha navbat. «status»: Pending | Approved | Rejected | Cancelled. " +
                "«from»/«to» so'rovning DAVRI bo'yicha kesadi (yaratilgan sana bo'yicha emas) va oraliqqa " +
                "QISMAN tegadigan so'rov ham chiqadi; ikkala chegara ham kiradi, istalgani berilmasligi mumkin, " +
                "«from» > «to» — «davomat.davr_notogri» (400). «search» — xodim bo'yicha, boshqa ro'yxatlardagi bilan " +
                "bir xil shart. «absenceTypeId» — yo'qlik turi; «mode»: PartDay | WholeDay | MultiDay " +
                "(qism kun / butun kun / bir necha kun). «pageSize» sukut 25, eng ko'pi 200.")
            .Produces<PagedResult<AbsenceRequestDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/requests/pending-count", async (
                GetPendingAbsenceRequestCountQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetPendingAbsenceRequestCountQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("GetPendingAbsenceRequestCount")
            .WithSummary("Davomat — qaror kutayotgan so'rovlar soni")
            .WithDescription(
                "Nav nishoni uchun: faqat SON, ro'yxat emas. Parametr va filtr yo'q — nishon butun " +
                "tashkilotni ko'rsatadi.")
            .Produces<AbsenceRequestCountDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/requests", async (
                AbsenceRequestBody body,
                CreateAbsenceRequestCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new CreateAbsenceRequestCommand(
                            body.EmployeeId,
                            body.AbsenceTypeId,
                            body.FromDate,
                            body.ToDate,
                            body.FromTime,
                            body.ToTime,
                            body.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceRequestsManage)
            .WithName("CreateAbsenceRequest")
            .WithSummary("Davomat — yo'qlik so'rovini yaratish")
            .WithDescription(
                "Birinchi bosqichda so'rovni HR xodim nomidan kiritadi. Vaqt oralig'i («fromTime»/«toTime») " +
                "faqat BIR KUNLIK so'rovda beriladi va ikkalasi birga bo'lishi shart; ko'p kunlik so'rovda " +
                "vaqt — «davomat.sorov_vaqti_notogri» (400). «fromDate» > «toDate» — «davomat.sorov_davri_notogri»; " +
                "92 kundan uzun — «davomat.sorov_uzun». Shu kunlarga xodimda FAOL (kutayotgan yoki tasdiqlangan) " +
                "so'rov bo'lsa — «davomat.sorov_takroriy» (409). Izoh 2000 belgidan uzun — «davomat.izoh_uzun» (400). " +
                "⚠️ Yangi so'rov davomat hisobini O'ZGARTIRMAYDI: kun faqat TASDIQLANGANDA «Sababli» bo'ladi.")
            .Produces<AbsenceRequestDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/requests/{id:guid}", async (
                Guid id,
                AbsenceRequestBody body,
                UpdateAbsenceRequestCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new UpdateAbsenceRequestCommand(
                            id,
                            body.AbsenceTypeId,
                            body.FromDate,
                            body.ToDate,
                            body.FromTime,
                            body.ToTime,
                            body.Note,
                            ExpectedVersion: body.Version),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceRequestsManage)
            .WithName("UpdateAbsenceRequest")
            .WithSummary("Davomat — kutayotgan so'rovni tahrirlash")
            .WithDescription(
                "Faqat KUTAYOTGAN so'rov tahrirlanadi: qaror qabul qilingandan keyin — " +
                "«davomat.sorov_qaror_qabul_qilingan» (409). Xodim o'zgartirilmaydi (bunday maydon yo'q): " +
                "boshqa odamning so'rovi — bu boshqa so'rov. «version» berilsa va so'rov boshqa joyda o'zgargan " +
                "bo'lsa (yoki parallel yozuv yutgan bo'lsa) — «davomat.sorov_ozgargan» (409). Izoh 2000 belgidan " +
                "uzun — «davomat.izoh_uzun» (400).")
            .Produces<AbsenceRequestDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/requests/{id:guid}/decide", async (
                Guid id,
                AbsenceDecisionBody body,
                DecideAbsenceRequestCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new DecideAbsenceRequestCommand(id, body.Approve, body.DecisionNote, body.Version),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceRequestsManage)
            .WithName("DecideAbsenceRequest")
            .WithSummary("Davomat — so'rovni tasdiqlash yoki rad etish")
            .WithDescription(
                "«approve: true» — tasdiqlash, «false» — rad etish. ⭐ Qarordan keyin so'rov qamragan kunlar " +
                "QAYTA HISOBLANADI: tasdiqlangan so'rov «Kelmadi» ni «Sababli» ga aylantiradi. «Ish vaqti " +
                "hisoblanadigan» turning soatlari ishlangan vaqtga qo'shiladi; SHAXSIY turning (ish vaqti " +
                "hisoblanmaydigan) VAQTLI so'rovi esa o'z soatlarini ishlangan vaqtdan ayiradi va «Shaxsiy " +
                "vaqt» bo'lib ko'rinadi — u butun kunni «Sababli» qilmaydi. Qaror allaqachon qabul qilingan " +
                "bo'lsa — «davomat.sorov_qaror_qabul_qilingan» (409); «version» eskirgan yoki parallel yozuv — " +
                "«davomat.sorov_ozgargan» (409); izoh 2000 belgidan uzun — «davomat.izoh_uzun» (400).")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces<AbsenceRequestDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/requests/{id:guid}/cancel", async (
                Guid id,
                CancelAbsenceRequestCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new CancelAbsenceRequestCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceRequestsManage)
            .WithName("CancelAbsenceRequest")
            .WithSummary("Davomat — so'rovni bekor qilish")
            .WithDescription(
                "Tasdiqlangan so'rov ham bekor qilinadi — shunda kunlar qayta hisoblanadi va «Sababli» " +
                "holati qaytariladi. Allaqachon bekor qilingan so'rov — «davomat.sorov_bekor_qilingan» (409); " +
                "parallel yozuv — «davomat.sorov_ozgargan» (409).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    // ════════════════════════════════════════════════════════════
    //  Tabel va hisobotlar (SPEC §6.6) — attendance.view
    // ════════════════════════════════════════════════════════════

    private static void MapReports(IEndpointRouteBuilder group)
    {
        group.MapGet("/timesheet", async (
                int year,
                int month,
                Guid? legalEntityId,
                Guid? departmentId,
                Guid? positionId,
                Guid? employeeId,
                string? search,
                int? page,
                int? pageSize,
                GetTimesheetQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetTimesheetQuery(
                            year, month, legalEntityId, departmentId, positionId, employeeId, search, page, pageSize),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("GetAttendanceTimesheet")
            .WithSummary("Davomat — tabel")
            .WithDescription(
                "«year»/«month» majburiy («davomat.oy_notogri» — 400). Xodim bo'yicha sahifalangan; har bir " +
                "xodimga oyning HAR BIR kuni uchun katak — yozuvi yo'q kunlar serverda hosil qilinadi " +
                "(DayOff, NotEmployed, NoSchedule, Absent). «lastComputedAt» — sahifadagi saqlangan kunlarning " +
                "eng so'nggi hisoblanishi.")
            .Produces<AttendanceTimesheetDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/reports/exceptions", async (
                string? kind,
                DateOnly? from,
                DateOnly? to,
                Guid? legalEntityId,
                Guid? departmentId,
                Guid? positionId,
                Guid? employeeId,
                int? minMinutes,
                int? page,
                int? pageSize,
                string? sort,
                GetAttendanceExceptionsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetAttendanceExceptionsQuery(
                            kind, from, to, legalEntityId, departmentId, positionId, employeeId,
                            minMinutes, page, pageSize, sort),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("GetAttendanceExceptions")
            .WithSummary("Davomat — chetlanishlar hisoboti")
            .WithDescription(
                "«kind» majburiy: Late | EarlyLeave | Absent | Missing (MissingExit + MissingEntry + NoSchedule) | " +
                "Shortfall (erkin kunda kam ishlangan) — «davomat.hisobot_turi_notogri» (400). Davr berilmasa — shu oy " +
                "boshidan bugungacha, 92 kundan oshmasin. «minMinutes» faqat Late/EarlyLeave/Shortfall uchun. «sort» — " +
                "date (sukut) | employee | minutes.")
            .Produces<PagedResult<AttendanceExceptionDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/reports/summary", async (
                DateOnly? from,
                DateOnly? to,
                Guid? legalEntityId,
                Guid? departmentId,
                Guid? positionId,
                int? page,
                int? pageSize,
                string? sort,
                GetAttendanceSummaryQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetAttendanceSummaryQuery(from, to, legalEntityId, departmentId, positionId, page, pageSize, sort),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceView)
            .WithName("GetAttendanceSummary")
            .WithSummary("Davomat — xulosa hisoboti")
            .WithDescription(
                "Xodim bo'yicha sahifalangan, faqat saqlangan kunlardan. «problemDays» = MissingExit + " +
                "MissingEntry + NoSchedule. «sort» — employee (sukut) | workedMinutes | lateMinutes | " +
                "absentDays | problemDays.")
            .Produces<PagedResult<AttendanceSummaryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    // ════════════════════════════════════════════════════════════
    //  Excel eksport (SPEC §6.7) — attendance.view + reports.export
    // ════════════════════════════════════════════════════════════

    private static void MapExports(IEndpointRouteBuilder group)
    {
        group.MapGet("/export/timesheet", async (
                int year,
                int month,
                Guid? legalEntityId,
                Guid? departmentId,
                Guid? positionId,
                Guid? employeeId,
                string? search,
                ExportTimesheetQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetTimesheetQuery(year, month, legalEntityId, departmentId, positionId, employeeId, search),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.File(natija.Value.Content, XlsxContentType, natija.Value.FileName);
            })
            .RequireAuthorization(Permissions.AttendanceView, Permissions.ReportsExport)
            .RequireRateLimiting(RateLimitPolicies.Export)
            .WithName("ExportAttendanceTimesheet")
            .WithSummary("Davomat — tabel, Excel")
            .WithDescription(
                "Parametrlar «GET /timesheet» bilan AYNAN bir xil (sahifasiz). Eng ko'pi 2 000 xodim — " +
                "«davomat.eksport_juda_kop» (400). Sana — haqiqiy Excel sanasi, davomiylik — daqiqa (son) va " +
                "«soat:daqiqa» (matn) ikki ustunda.")
            .Produces(StatusCodes.Status200OK, contentType: XlsxContentType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/export/passes", async (
                DateOnly? from,
                DateOnly? to,
                Guid? employeeId,
                Guid? deviceId,
                PassDirection? direction,
                PassMethod? method,
                bool? matched,
                Guid? legalEntityId,
                Guid? departmentId,
                string? search,
                string? sort,
                ExportPassesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new SearchPassesQuery(
                            from, to, employeeId, deviceId, direction, method, matched,
                            legalEntityId, departmentId, search, Page: null, PageSize: null, sort),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.File(natija.Value.Content, XlsxContentType, natija.Value.FileName);
            })
            .RequireAuthorization(Permissions.AttendanceView, Permissions.ReportsExport)
            .RequireRateLimiting(RateLimitPolicies.Export)
            .WithName("ExportAttendancePasses")
            .WithSummary("Davomat — o'tishlar jurnali, Excel")
            .WithDescription(
                "Parametrlar «GET /passes» bilan AYNAN bir xil (sahifasiz). Eng ko'pi 20 000 qator — " +
                "«davomat.eksport_juda_kop» (400).")
            .Produces(StatusCodes.Status200OK, contentType: XlsxContentType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/export/exceptions", async (
                string? kind,
                DateOnly? from,
                DateOnly? to,
                Guid? legalEntityId,
                Guid? departmentId,
                Guid? positionId,
                Guid? employeeId,
                int? minMinutes,
                string? sort,
                ExportAttendanceExceptionsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetAttendanceExceptionsQuery(
                            kind, from, to, legalEntityId, departmentId, positionId, employeeId,
                            minMinutes, Page: null, PageSize: null, sort),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.File(natija.Value.Content, XlsxContentType, natija.Value.FileName);
            })
            .RequireAuthorization(Permissions.AttendanceView, Permissions.ReportsExport)
            .RequireRateLimiting(RateLimitPolicies.Export)
            .WithName("ExportAttendanceExceptions")
            .WithSummary("Davomat — chetlanishlar, Excel")
            .WithDescription(
                "Parametrlar «GET /reports/exceptions» bilan AYNAN bir xil (sahifasiz). Eng ko'pi 20 000 qator.")
            .Produces(StatusCodes.Status200OK, contentType: XlsxContentType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/export/summary", async (
                DateOnly? from,
                DateOnly? to,
                Guid? legalEntityId,
                Guid? departmentId,
                Guid? positionId,
                string? sort,
                ExportAttendanceSummaryQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetAttendanceSummaryQuery(
                            from, to, legalEntityId, departmentId, positionId, Page: null, PageSize: null, sort),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.File(natija.Value.Content, XlsxContentType, natija.Value.FileName);
            })
            .RequireAuthorization(Permissions.AttendanceView, Permissions.ReportsExport)
            .RequireRateLimiting(RateLimitPolicies.Export)
            .WithName("ExportAttendanceSummary")
            .WithSummary("Davomat — xulosa, Excel")
            .WithDescription(
                "Parametrlar «GET /reports/summary» bilan AYNAN bir xil (sahifasiz). Eng ko'pi 20 000 qator.")
            .Produces(StatusCodes.Status200OK, contentType: XlsxContentType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // ── Ro'yxat eksportlari (SPEC-excel-eksport.md §2 #6–#8) ─────────────
        // Manzil — «<ro'yxat>/export», «/export/…» emas: hamma ro'yxatda bitta qoida. Ular
        // «/requests/{id:guid}» va «/schedule-requests/{id:guid}» bilan to'qnashmaydi —
        // «export» GUID emas. Chegara — umumiy 5 000 («eksport.juda_kop»), audit yoziladi.

        group.MapGet("/requests/export", async (
                Guid? employeeId,
                AbsenceRequestStatus? status,
                bool? decided,
                DateOnly? from,
                DateOnly? to,
                string? search,
                Guid? absenceTypeId,
                AbsenceRequestMode? mode,
                ExportAbsenceRequestsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new SearchAbsenceRequestsQuery(
                            employeeId, status, decided, from, to, search,
                            Page: null, PageSize: null, absenceTypeId, mode),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.File(natija.Value.Content, XlsxContentType, natija.Value.FileName);
            })
            .RequireAuthorization(Permissions.AttendanceView, Permissions.ReportsExport)
            .RequireRateLimiting(RateLimitPolicies.Export)
            .WithName("ExportAbsenceRequestsToExcel")
            .WithSummary("Davomat — yo'qlik so'rovlari, Excel")
            .WithDescription(
                "Parametrlar «GET /requests» bilan AYNAN bir xil (sahifasiz) — ekrandagi filtr bilan mos " +
                "keladigan HAMMA so'rov, o'sha tartibda (yangisi birinchi). Ekrandagi ikki jadval — " +
                "«status=Pending» va «decided=true»; ikkalasisiz — butun navbat. Ustunlar: xodim, bo'lim, " +
                "yo'qlik turi, sanadan, sanagacha, vaqt (yoki «Butun kun»), izoh, holat, qaror izohi, qaror " +
                "qilgan, qaror sanasi, ilovadan (Ha/Yo'q). Eng ko'pi 5000 qator — «eksport.juda_kop» (400); " +
                "«from» > «to» — «davomat.davr_notogri» (400). Ruxsat: «attendance.view» VA «reports.export». " +
                "Har bir eksport audit jurnaliga yoziladi (qidiruv matnisiz).")
            .Produces(StatusCodes.Status200OK, contentType: XlsxContentType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/schedule-requests/export", async (
                Guid? employeeId,
                AbsenceRequestStatus? status,
                bool? decided,
                DateOnly? from,
                DateOnly? to,
                string? search,
                ExportScheduleChangeRequestsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new SearchScheduleChangeRequestsQuery(
                            employeeId, status, decided, from, to, search, Page: null, PageSize: null),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.File(natija.Value.Content, XlsxContentType, natija.Value.FileName);
            })
            .RequireAuthorization(Permissions.AttendanceView, Permissions.ReportsExport)
            .RequireRateLimiting(RateLimitPolicies.Export)
            .WithName("ExportScheduleChangeRequestsToExcel")
            .WithSummary("Davomat — jadval so'rovlari, Excel")
            .WithDescription(
                "Parametrlar «GET /schedule-requests» bilan AYNAN bir xil (sahifasiz), o'sha tartibda. Bitta " +
                "qator — bitta so'rov; uning sanalari, ish vaqti va tanaffusi katak ICHIDA, kun sayin bir " +
                "qatordan (almashtirishda — juftlar). Ustunlar: xodim, bo'lim, so'rov turi, sanalar, ish " +
                "vaqti, tanaffus, izoh, holat, qaror izohi, qaror qilgan, qaror sanasi, ilovadan. Eng ko'pi " +
                "5000 qator — «eksport.juda_kop» (400); «from» > «to» — «davomat.davr_notogri» (400). Ruxsat: " +
                "«attendance.view» VA «reports.export». Har bir eksport audit jurnaliga yoziladi (qidiruv matnisiz).")
            .Produces(StatusCodes.Status200OK, contentType: XlsxContentType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/dashboard/export", async (
                DateOnly? date,
                string? search,
                Guid? legalEntityId,
                Guid? departmentId,
                Guid? positionId,
                AttendanceDashboardStatus? status,
                string? sort,
                ExportAttendanceDashboardQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetAttendanceDashboardQuery(
                            date, search, legalEntityId, departmentId, positionId, status, sort, Page: null, PageSize: null),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.File(natija.Value.Content, XlsxContentType, natija.Value.FileName);
            })
            .RequireAuthorization(Permissions.AttendanceView, Permissions.ReportsExport)
            .RequireRateLimiting(RateLimitPolicies.Export)
            .WithName("ExportAttendanceDashboardToExcel")
            .WithSummary("Davomat — statistika jadvali, Excel")
            .WithDescription(
                "Parametrlar «GET /dashboard» bilan AYNAN bir xil (sahifasiz): bitta sana (berilmasa — bugun), " +
                "firma/bo'lim/lavozim, holat («status») va qidiruv («search») — jadvaldagidek SERVERDA " +
                "qo'llanadi, saralash — ism bo'yicha. Davr (grafik) eksport qilinmaydi: unda jadval yo'q. " +
                "Ustunlar: F.I.Sh., lavozim, bo'lim, kelgan, ketgan (jadvalsiz kunning avtomatik 22:00 si — " +
                "«(avto)» izohi bilan matn), holat, kechikish (daq.). Fayl nomidagi sana — jadval sanasi. " +
                "Eng ko'pi 5000 qator — «eksport.juda_kop» (400). Ruxsat: «attendance.view» VA " +
                "«reports.export». Har bir eksport audit jurnaliga yoziladi (qidiruv matnisiz).")
            .Produces(StatusCodes.Status200OK, contentType: XlsxContentType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    // ════════════════════════════════════════════════════════════
    //  Qayta hisoblash (SPEC §6.7, §5.8) — attendance.manage
    // ════════════════════════════════════════════════════════════

    private static void MapRecompute(IEndpointRouteBuilder group)
    {
        group.MapPost("/recompute", async (
                RecomputeAttendanceRequest request,
                RecomputeAttendanceCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new RecomputeAttendanceCommand(request.From, request.To, request.EmployeeId), cancellationToken)
                    .ConfigureAwait(false);

                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.Accepted(uri: null, natija.Value);
            })
            .RequireAuthorization(Permissions.AttendanceManage)

            // Og'ir amal — bir oylik qayta hisoblash o'n minglab qator: eksport
            // bilan bir xil cheklov (SPEC §7.3).
            .RequireRateLimiting(RateLimitPolicies.Export)
            .WithName("RecomputeAttendance")
            .WithSummary("Davomat — davrni qayta hisoblash")
            .WithDescription(
                "Jadval yoki biriktirma tuzatilgach tabelni tiklashning YAGONA yo'li — kunni qo'lda tahrirlash " +
                "yo'q. Davr 92 kundan oshmasin («davomat.davr_uzun», «davomat.davr_notogri» — 400); «employeeId» " +
                "bo'sh — davrga tegishli hamma, berilgan-u topilmasa — «davomat.xodim_topilmadi» (404). Javob " +
                "202: { jobId, employeeCount, dayCount }. Endi saqlanmasligi kerak bo'lgan eski kun yozuvi " +
                "(masalan jadval dam olish kuniga o'zgargan) o'chiriladi.")
            .Produces<AttendanceRecomputeAcceptedDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
