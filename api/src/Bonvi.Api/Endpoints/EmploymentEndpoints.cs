using Bonvi.Api.Extensions;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Employees.Employments;
using Bonvi.Application.Employees.Probations;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>Lavozim/bo'lim o'zgarishi so'rovi tanasi.</summary>
/// <param name="EffectiveDate">Kuchga kirish sanasi. Bo'sh bo'lsa — bugun.</param>
/// <param name="DepartmentId">Yangi bo'lim. Bo'sh — o'zgarmaydi.</param>
/// <param name="PositionId">Yangi lavozim. Bo'sh — o'zgarmaydi.</param>
/// <param name="SalaryAmount">Yangi oklad (ko'chirish bilan birga o'zgarsa).</param>
/// <param name="SalaryCurrency">Valyuta kodi, sukut bo'yicha UZS.</param>
/// <param name="OrderNo">Buyruq raqami.</param>
/// <param name="OrderDate">Buyruq sanasi.</param>
/// <param name="Note">Izoh.</param>
public sealed record TransferRequest(
    DateOnly? EffectiveDate = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    decimal? SalaryAmount = null,
    string? SalaryCurrency = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null,
    string? Note = null);

/// <summary>Qo'shimcha vazifa so'rovi tanasi.</summary>
/// <param name="Duty">Vazifa matni — majburiy.</param>
/// <param name="EffectiveDate">Kuchga kirish sanasi.</param>
/// <param name="ExtraSalaryAmount">Qo'shimcha to'lov.</param>
/// <param name="SalaryCurrency">Valyuta kodi.</param>
/// <param name="OrderNo">Buyruq raqami.</param>
/// <param name="OrderDate">Buyruq sanasi.</param>
public sealed record AdditionalDutyRequest(
    string? Duty,
    DateOnly? EffectiveDate = null,
    decimal? ExtraSalaryAmount = null,
    string? SalaryCurrency = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null);

/// <summary>Oklad o'zgarishi so'rovi tanasi.</summary>
/// <param name="Amount">Yangi oklad. <c>null</c> — oklad olib tashlanadi.</param>
/// <param name="Currency">Valyuta kodi, sukut bo'yicha UZS.</param>
/// <param name="EffectiveDate">Kuchga kirish sanasi.</param>
/// <param name="OrderNo">Buyruq raqami.</param>
/// <param name="OrderDate">Buyruq sanasi.</param>
public sealed record ChangeSalaryRequest(
    decimal? Amount,
    string? Currency = null,
    DateOnly? EffectiveDate = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null);

/// <summary>Mehnat yo'li yozuvining terish xatosini tuzatish so'rovi tanasi.</summary>
/// <remarks>
/// ⭐ Hodisa TURI, lavozim va bo'lim bu yerda YO'Q va bo'lishi ham mumkin
/// emas: ular «nima bo'lgan» degan faktning o'zi va ularni almashtirish
/// bitta hodisani boshqasiga aylantirib yuborardi
/// (<c>EmploymentEvent</c> domen izohi). Noto'g'ri QAROR yozilgan bo'lsa,
/// uning ustiga yangi hodisa yoziladi — «/transfer», «/salary».
/// </remarks>
/// <param name="EffectiveDate">Kuchga kirish sanasi. Bo'sh — bugun.</param>
/// <param name="Amount">
/// Oklad summasi — FAQAT summasi bor hodisada (qabul, oklad o'zgarishi)
/// qo'llanadi. Qolganlarida jimgina e'tiborsiz qoladi: interfeys maydonni
/// ko'rsatmaydi ham.
/// </param>
/// <param name="Currency">Valyuta kodi, sukut bo'yicha UZS.</param>
/// <param name="OrderNo">Buyruq raqami.</param>
/// <param name="OrderDate">Buyruq sanasi.</param>
/// <param name="Note">Izoh.</param>
public sealed record EditEmploymentEventRequest(
    DateOnly? EffectiveDate = null,
    decimal? Amount = null,
    string? Currency = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null,
    string? Note = null);

/// <summary>Qo'shimcha vazifani tugatish so'rovi tanasi.</summary>
/// <param name="EndDate">Vazifa tugagan sana; <c>null</c> — vazifa davom etmoqda (qayta ochish).</param>
public sealed record EndAdditionalDutyRequest(DateOnly? EndDate = null);

/// <summary>Ishdan bo'shatish so'rovi tanasi.</summary>
/// <param name="TerminationDate">Ketish sanasi. Bo'sh bo'lsa — bugun.</param>
/// <param name="ReasonId">Ketish sababi (ma'lumotnomadan).</param>
/// <param name="OrderNo">Buyruq raqami.</param>
/// <param name="OrderDate">Buyruq sanasi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="Confirm">Belgili xodimni bo'shatishga rozilik (so'rov qatoridagi «confirm» bilan bir xil).</param>
public sealed record TerminateRequest(
    DateOnly? TerminationDate = null,
    Guid? ReasonId = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null,
    string? Note = null,
    bool? Confirm = null);

/// <summary>
/// Sinov muddati xulosasi — va u «o'tdi» bo'lsa, doimiy holatga o'tkazish
/// shartlari.
/// </summary>
/// <remarks>
/// Tayinlash maydonlari (<paramref name="PositionId"/> dan
/// <paramref name="OrderDate"/> gacha) IXTIYORIY va bo'sh qoldirilgani
/// «o'zgarmadi» degani. Ular faqat <c>Passed</c> bilan ishlaydi.
/// <c>Failed</c> / <c>LeftEarly</c> esa ishdan bo'shatishdir va
/// <paramref name="Termination"/> ni talab qiladi.
/// </remarks>
/// <param name="Status">Natija: o'tdi / o'tmadi / erta ketdi. «Bekor qilindi» qabul qilinmaydi.</param>
/// <param name="ResolvedAt">Yakunlangan sana. Bo'sh bo'lsa — bugun.</param>
/// <param name="Conclusion">Xulosa matni.</param>
/// <param name="ReviewerEmployeeId">Xulosa bergan mas'ul.</param>
/// <param name="PositionId">Yangi lavozim.</param>
/// <param name="DepartmentId">Yangi bo'lim.</param>
/// <param name="SalaryAmount">Yangi oklad.</param>
/// <param name="SalaryCurrency">Oklad valyutasi. Bo'sh bo'lsa — so'm.</param>
/// <param name="Rate">Yangi stavka: 1.0 — to'liq, 0.5 — yarim.</param>
/// <param name="OrderNo">Buyruq raqami.</param>
/// <param name="OrderDate">Buyruq sanasi.</param>
/// <param name="Termination">
/// Ishdan bo'shatish — faqat <c>Failed</c> / <c>LeftEarly</c> bilan va ular
/// uchun majburiy. Maydonlar oddiy bo'shatishdagining o'zi
/// (<see cref="TerminateRequest"/>), <c>confirm</c> ham.
/// </param>
public sealed record ResolveProbationRequest(
    ProbationStatus Status,
    DateOnly? ResolvedAt = null,
    string? Conclusion = null,
    Guid? ReviewerEmployeeId = null,
    Guid? PositionId = null,
    Guid? DepartmentId = null,
    decimal? SalaryAmount = null,
    string? SalaryCurrency = null,
    decimal? Rate = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null,
    TerminateRequest? Termination = null);

/// <summary>
/// Sinov yozuvisiz «sinovda» qolgan munosabatlarni tasdiqlash so'rovi.
/// </summary>
/// <param name="EmploymentIds">HR belgilagan qatorlar.</param>
/// <param name="At">Qaysi sanadan doimiy hisoblanadi. Bo'sh bo'lsa — bugun.</param>
/// <param name="Note">Tarixda hodisa matni bo'lib qoladigan izoh.</param>
public sealed record ConfirmMissingProbationsRequest(
    IReadOnlyList<Guid> EmploymentIds,
    DateOnly? At = null,
    string? Note = null);

/// <summary>Sinov muddatini uzaytirish so'rovi tanasi.</summary>
/// <param name="NewEndDate">Yangi tugash sanasi.</param>
public sealed record ExtendProbationRequest(DateOnly NewEndDate);

/// <summary>
/// Mehnat munosabatlari va sinov muddatlari endpoint'lari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega hodisalar «buyruq» ko'rinishida.</b> Har bir amal
/// (qabul, ko'chirish, oklad, bo'shatish) o'zining raqami va sanasi bilan
/// yoziladi. Aynan shu kadrlar ishining haqiqiy hujjati va aynan shu
/// Excel'da yo'q edi: u faqat OXIRGI holatni saqlar edi, «qachon va qaysi
/// buyruq bilan» degan savol javobsiz qolardi.
/// </para>
/// <para>
/// <b>Ogohlantirish va blok farqi.</b> Javobdagi <c>warnings</c> — amal
/// bajarildi, lekin e'tibor talab qiladi (stavka 1.5 dan oshdi, xodim
/// nazoratda). 409 esa — amal BAJARILMADI: qora ro'yxat yoki tasdiqlanmagan
/// bo'shatish.
/// </para>
/// <para>
/// <b>Har bir yozish amali — o'z ruxsati bilan</b> (<c>employment.hire</c>,
/// <c>.transfer</c>, <c>.duty</c>, <c>.salary</c>, <c>.terminate</c>,
/// <c>.probation.extend</c>, <c>.probation.complete</c>, <c>.correct</c>).
/// Ilgari hammasi bitta <c>employment.manage</c> ostida edi; bo'linish
/// sababi <see cref="Permissions.EmploymentActions"/> oldidagi izohda.
/// Amal ICHIDA yuborilgan summa endpoint'da emas, handler'da tekshiriladi:
/// <c>RequireAuthorization</c> so'rov tanasini ko'rmaydi.
/// </para>
/// </remarks>
public static class EmploymentEndpoints
{
    public static IEndpointRouteBuilder MapEmploymentEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        MapEmployments(app.MapGroup("/api/employments").WithTags("Employments"));
        MapProbations(app.MapGroup("/api/probations").WithTags("Probations"));

        return app;
    }

    // ════════════════════════════════════════════════════════════
    //  Mehnat munosabatlari
    // ════════════════════════════════════════════════════════════

    private static void MapEmployments(IEndpointRouteBuilder group)
    {
        group.MapPost("/hire", async (
                HireEmployeeCommand command,
                HireEmployeeCommandHandler handler,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                // «person» — YANGI xodim kartasi: «employment.hire» ning o'zi bunga
                // yetmaydi, «employees.create» ham shart (xavfsizlik tekshiruvi,
                // 2026-09-26). Mavjud xodimni qayta qabul qilish esa faqat hire bilan.
                if (command.Person is not null && !currentUser.HasPermission(Permissions.EmployeesCreate))
                {
                    return ((Result)EmployeeErrors.EmployeeCreatePermissionRequired).ToHttpResult();
                }

                var natija = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentHire)
            .WithName("HireEmployee")
            .WithSummary("Ishga qabul — yagona forma")
            .WithDescription(
                "Shaxs + firma + bo'lim + lavozim + oklad + buyruq BITTA so'rovda. " +
                "Xodim «employeeId» bilan ko'rsatiladi yoki «person» ichida yangi kiritiladi — " +
                "ikkinchi holatda avval JSHSHIR bo'yicha qidiriladi va topilsa DUBLIKAT " +
                "yaratilmaydi (qayta ishga olish). " +
                "Sinov muddati AVTOMATIK va ISTISNOSIZ ochiladi: lavozimning «probationDays» " +
                "qiymati, u yo'q bo'lsa 90 kun. Sinovsiz qabul YO'Q — «probationDays: 0» " +
                "yoki manfiy qiymat «sinov.majburiy» (400) beradi. " +
                "Qora ro'yxatdagi xodim: «xodim.qora_royxatda» (409). Nazoratdagi xodim va " +
                "stavkalar yig'indisi 1.5 dan oshgani — javobdagi «warnings» massivida, " +
                "amal esa bajariladi. Javobda lavozimning vazifalari ham qaytadi. " +
                "Ruxsat: «employment.hire»; «person» bilan YANGI karta ochilsa «employees.create» ham " +
                "(yo'q bo'lsa 403 «auth.ruxsat_yoq»); boshlang'ich oklad qabul buyrug'ining o'zi, " +
                "shu sababli «employment.salary» talab qilinmaydi.")
            .Produces<HireResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // «/events» «/{id:guid}» dan OLDIN turadi — o'qish uchun: ikkisi
        // to'qnashmaydi (marshrut cheklovi GUID talab qiladi), lekin
        // umumiy ro'yxat bitta munosabatning kartasidan muhimroq.
        group.MapGet("/events", async (
                string? search,
                EmploymentEventType? type,
                DateOnly? from,
                DateOnly? to,
                Guid? departmentId,
                Guid? legalEntityId,
                int? page,
                int? pageSize,
                ListEmploymentEventsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ListEmploymentEventsQuery(
                            search,
                            type,
                            from,
                            to,
                            departmentId,
                            legalEntityId,
                            page,
                            pageSize),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentView)
            .WithName("ListEmploymentEvents")
            .WithSummary("Buyruqlar — barcha xodimlar bo'yicha umumiy ro'yxat")
            .WithDescription(
                "Excel'dagi «91.2 янги ишга келганлар», «91.3 лавозими ўзгарганлар», " +
                "«91.4 қўшимча вазифалар» va «91.6 ишдан кетганлар» varaqlarining o'rniga: " +
                "bitta hodisalar oqimi va uning filtrlangan ko'rinishlari. " +
                "«type» qiymatlari: Hire | Transfer | Promotion | AdditionalDuty | " +
                "SalaryChange | ProbationResult | Termination | Rehire; noma'lum qiymat 400 beradi. " +
                "«search» — BUYRUQ RAQAMI YOKI XODIM bo'yicha (F.I.Sh., JSHSHIR, pasport, " +
                "telefon): ikkala shart «yoki» bilan bog'langan, ya'ni «185-б» ham, " +
                "«Каримов» ham, JSHSHIR ham ishlaydi. «from»/«to» — kuchga kirish " +
                "sanasining ikki tomondan yopiq oralig'i. «departmentId» — bo'lim ICHKI " +
                "bo'limlari bilan birga: hodisaning yangi yoki eski bo'limiga, hodisada bo'lim " +
                "ko'rsatilmagan bo'lsa (oklad, bo'shatish, qo'shimcha vazifa) munosabatning " +
                "joriy bo'limiga mos keladi. Ro'yxat kuchga kirish sanasi bo'yicha " +
                "kamayish tartibida, «pageSize» eng ko'pi 10 000. " +
                "Oklad maydonlari faqat «salary.view» ruxsati bilan to'ldiriladi.")
            .Produces<PagedResult<EmploymentEventListItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                GetEmploymentQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetEmploymentQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentView)
            .WithName("GetEmployment")
            .WithSummary("Mehnat munosabati — hodisalari bilan")
            .WithDescription(
                "Bitta munosabat va uning barcha buyruqlari xronologik tartibda. " +
                "Oklad maydonlari «salary.view» ruxsatisiz bo'sh qaytadi.")
            .Produces<EmploymentDetailsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/transfer", async (
                Guid id,
                TransferRequest request,
                TransferEmployeeCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new TransferEmployeeCommand(
                            id,
                            request.EffectiveDate,
                            request.DepartmentId,
                            request.PositionId,
                            request.SalaryAmount,
                            request.SalaryCurrency,
                            request.OrderNo,
                            request.OrderDate,
                            request.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentTransfer)
            .WithName("TransferEmployee")
            .WithSummary("Lavozim yoki bo'lim o'zgarishi")
            .WithDescription(
                "Bo'sh qoldirilgan maydon «o'zgarmadi» degani. Lavozim o'zgarsa hodisa " +
                "«Promotion», faqat bo'lim o'zgarsa «Transfer» bo'lib yoziladi — hisobotda " +
                "ular alohida ko'rinadi. Yakunlangan munosabat: «mehnat.tugagan» (409). " +
                "Ruxsat: «employment.transfer»; «salaryAmount» yuborilsa, qo'shimcha " +
                "«employment.salary» ham kerak — aks holda «auth.ruxsat_yoq» (403).")
            .Produces<EmploymentChangeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/additional-duty", async (
                Guid id,
                AdditionalDutyRequest request,
                AddAdditionalDutyCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new AddAdditionalDutyCommand(
                            id,
                            request.Duty,
                            request.EffectiveDate,
                            request.ExtraSalaryAmount,
                            request.SalaryCurrency,
                            request.OrderNo,
                            request.OrderDate),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentDuty)
            .WithName("AddAdditionalDuty")
            .WithSummary("Qo'shimcha vazifa berish")
            .WithDescription(
                "Mavjud ish doirasidagi qo'shimcha yuk (yangi mehnat munosabati EMAS). " +
                "Vazifa matni majburiy: «mehnat.vazifa_bosh» (400). " +
                "Ruxsat: «employment.duty»; «extraSalaryAmount» yuborilsa, qo'shimcha " +
                "«employment.salary» ham kerak — aks holda «auth.ruxsat_yoq» (403).")
            .Produces<EmploymentChangeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/salary", async (
                Guid id,
                ChangeSalaryRequest request,
                ChangeSalaryCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ChangeSalaryCommand(
                            id,
                            request.Amount,
                            request.Currency,
                            request.EffectiveDate,
                            request.OrderNo,
                            request.OrderDate),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentSalary)
            .WithName("ChangeSalary")
            .WithSummary("Oklad o'zgarishi")
            .WithDescription(
                "Eski va yangi qiymat hodisada saqlanadi — oklad tarixi shundan quriladi. " +
                "DIQQAT: amalni «employment.salary» bajaradi, lekin javobdagi oklad maydoni " +
                "«salary.view» ruxsatisiz bo'sh qaytadi.")
            .Produces<EmploymentChangeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/events/{eventId:guid}/duty-end", async (
                Guid id,
                Guid eventId,
                EndAdditionalDutyRequest request,
                EndAdditionalDutyCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new EndAdditionalDutyCommand(id, eventId, request.EndDate), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentDuty)
            .WithName("EndAdditionalDuty")
            .WithSummary("Qo'shimcha vazifani tugatish")
            .WithDescription(
                "Vazifa yozuvi O'CHIRILMAYDI — unga tugash sanasi qo'yiladi va u tarixda " +
                "«falon sanadan falon sanagacha» bo'lib qoladi. Shu kundan boshlab vazifa " +
                "(va uning qo'shimcha haqi) kartadagi jamiga kirmaydi. «endDate»: null — " +
                "vazifani qayta ochish (davom etmoqda). Ruxsat: «employment.duty»; HAQLI " +
                "vazifa uchun qo'shimcha «employment.salary» va «salary.extra.view» — aks " +
                "holda «auth.ruxsat_yoq» (403). Xatolar: «mehnat_hodisasi.topilmadi» (404), " +
                "«mehnat.vazifa_emas», «mehnat.vazifa_tugash_sanasi» (400).")
            .Produces<EmploymentEventDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/events/{eventId:guid}", async (
                Guid id,
                Guid eventId,
                EditEmploymentEventRequest request,
                EditEmploymentEventCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new EditEmploymentEventCommand(
                            id,
                            eventId,
                            request.EffectiveDate,
                            request.Amount,
                            request.Currency,
                            request.OrderNo,
                            request.OrderDate,
                            request.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentCorrect)
            .WithName("EditEmploymentEvent")
            .WithSummary("Mehnat yo'li — yozuvdagi terish xatosini tuzatish")
            .WithDescription(
                "Mehnat yo'lidagi BITTA yozuvni tuzatadi: kuchga kirish sanasi, buyruq " +
                "raqami, buyruq sanasi, izoh va oklad SUMMASI. Yangi hodisa QO'SHILMAYDI " +
                "va eskisi bekor qilinmaydi. " +
                "⚠️ Bu «qarorni o'zgartirish» EMAS: oklad boshqa summaga o'zgargan bo'lsa, " +
                "u yangi buyruq va «/salary» orqali yoziladi; bu yerda esa AYNI buyruqning " +
                "noto'g'ri yozib olingani to'g'rilanadi. Shu sababli hodisa TURI, lavozim " +
                "va bo'lim yuborilmaydi. " +
                "Oklad summasi faqat summasi BOR hodisada qo'llanadi (qabul, oklad " +
                "o'zgarishi); qolganlarida jimgina e'tiborsiz qoladi. Tuzatilgan yozuv " +
                "xodimning JORIY okladini ham yangilaydi — lekin faqat o'sha yozuv " +
                "okladning manbai bo'lsa (eng oxirgi summali hodisa). " +
                "Ishdan ketgan xodimning yozuvi ham tuzatiladi: arxivdagi xato ham " +
                "xato. " +
                "Xatolar: «mehnat.topilmadi» / «mehnat_hodisasi.topilmadi» (404). " +
                "Ruxsat: «employment.correct»; «amount» yuborilsa, qo'shimcha " +
                "«employment.salary» ham kerak — aks holda «auth.ruxsat_yoq» (403). " +
                "Javobdagi oklad «salary.view» ruxsatisiz bo'sh qaytadi.")
            .Produces<EmploymentEventDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/terminate", async (
                Guid id,
                bool? confirm,
                TerminateRequest? request,
                TerminateEmploymentCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new TerminateEmploymentCommand(
                            id,
                            request?.TerminationDate,
                            request?.ReasonId,
                            request?.OrderNo,
                            request?.OrderDate,
                            request?.Note,
                            confirm ?? request?.Confirm ?? false),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentTerminate)
            .WithName("TerminateEmployment")
            .WithSummary("Ishdan bo'shatish")
            .WithDescription(
                "BLOK QOIDASI: xodim qora ro'yxatda yoki nazoratda bo'lsa, birinchi so'rov " +
                "409 bilan qaytadi («xodim.qora_royxatda» yoki «xodim.nazoratda»). " +
                "HR ataylab «?confirm=true» yuborsa amal bajariladi va sabab «warnings» da " +
                "qayd etiladi. Ochiq sinov muddati «erta ketdi» deb yopiladi. " +
                "Ketish sanasi ishga kirish sanasidan oldin bo'lolmaydi: «mehnat.ketish_sanasi». " +
                "Ruxsat: «employment.terminate».")
            .Produces<EmploymentChangeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    // ════════════════════════════════════════════════════════════
    //  Sinov muddatlari
    // ════════════════════════════════════════════════════════════

    private static void MapProbations(IEndpointRouteBuilder group)
    {
        group.MapGet("/", async (
                ProbationStatus? status,
                int? endingWithinDays,
                string? search,
                int? page,
                int? pageSize,
                ListProbationsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ListProbationsQuery(status, endingWithinDays, search, page, pageSize),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentView)
            .WithName("ListProbations")
            .WithSummary("Sinov muddatlari — ro'yxat")
            .WithDescription(
                "«status» bo'yicha filtr (InProgress | Passed | Failed | LeftEarly | Cancelled). " +
                "«endingWithinDays=7» — muddati tugashiga oz qolganlar; ro'yxat tugash sanasi " +
                "bo'yicha tartiblanadi, ya'ni eng shoshilinchi tepada. " +
                "«search» — XODIM bo'yicha (F.I.Sh., JSHSHIR, pasport, telefon), xodimlar " +
                "ro'yxatidagi bilan aynan bir xil qoida. " +
                "SERVER tomonda sahifalanadi: «page» (1 dan) va «pageSize» " +
                "(sukut 25, eng ko'pi 10 000) — xodimlar ro'yxatidagi bilan bir xil chegaralar.")
            .Produces<PagedResult<ProbationListItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/ending-soon", async (
                int? withinDays,
                GetProbationsEndingSoonQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetProbationsEndingSoonQuery(withinDays), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentView)
            .WithName("GetProbationsEndingSoon")
            .WithSummary("Sinov muddatlari — 7 kun ichida tugaydiganlar")
            .WithDescription(
                "Bosh sahifadagi «diqqat» bloki va kunlik eslatma uchun. Sukut bo'yicha " +
                "7 kun — domendagi yagona chegara (Probation.WarningDays).")
            .Produces<IReadOnlyList<ProbationListItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/{id:guid}/resolve", async (
                Guid id,
                bool? confirm,
                ResolveProbationRequest request,
                ResolveProbationCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ResolveProbationCommand(
                            id,
                            request.Status,
                            request.ResolvedAt,
                            request.Conclusion,
                            request.ReviewerEmployeeId,
                            request.PositionId,
                            request.DepartmentId,
                            request.SalaryAmount,
                            request.SalaryCurrency,
                            request.Rate,
                            request.OrderNo,
                            request.OrderDate,
                            request.Termination is { } termination
                                ? new ProbationTermination(
                                    termination.TerminationDate,
                                    termination.ReasonId,
                                    termination.OrderNo,
                                    termination.OrderDate,
                                    termination.Note)
                                : null,
                            confirm ?? request.Termination?.Confirm ?? false),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentProbationComplete)
            .WithName("ResolveProbation")
            .WithSummary("Sinov muddati — xulosa va doimiy holatga o'tkazish")
            .WithDescription(
                "«Passed» bo'lsa mehnat munosabati «sinovda» dan «faol» ga o'tadi — " +
                "aks holda xodim ro'yxatda abadiy sinovda qolardi. " +
                "SHU BITTA SO'ROVDA xodimga yangi lavozim, bo'lim, oklad va stavka ham " +
                "tayinlanadi: bo'sh qoldirilgan maydon «o'zgarmadi» degani. Tarixda ikki " +
                "hodisa qoladi — «sinov natijasi» va «lavozim/oklad o'zgarishi». " +
                "Tayinlash maydonlari faqat «Passed» bilan ishlaydi: «sinov.tayinlash_otmagan» (400). " +
                "Yakunlangan sinovni qayta yakunlab bo'lmaydi: «sinov.yakunlangan» (409). " +
                "«Failed» / «LeftEarly» — shu so'rovning o'zida ishdan bo'shatish: «termination» " +
                "(oddiy bo'shatish maydonlari) majburiy — «sinov.bosatish_kerak» (400), qora ro'yxat / " +
                "nazoratdagi xodim «confirm=true» bilan; qo'shimcha «employment.terminate» ruxsati kerak. " +
                "«Cancelled» qabul qilinmaydi: «sinov.bekor_qilinmaydi» (400). " +
                "Ruxsat: «employment.probation.complete»; «salaryAmount» yuborilsa, " +
                "qo'shimcha «employment.salary» ham kerak — aks holda «auth.ruxsat_yoq» (403).")
            .Produces<ProbationDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/extend", async (
                Guid id,
                ExtendProbationRequest request,
                ExtendProbationCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ExtendProbationCommand(id, request.NewEndDate), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentProbationExtend)
            .WithName("ExtendProbation")
            .WithSummary("Sinov muddati — uzaytirish")
            .WithDescription(
                "Yangi sana joriy tugash sanasidan keyin bo'lishi kerak va umumiy muddat " +
                "180 kundan oshmasligi kerak (mehnat kodeksi chegarasi): «sinov.muddat» (400). " +
                "Ruxsat: «employment.probation.extend».")
            .Produces<ProbationDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/missing", async (
                ListMissingProbationsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListMissingProbationsQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentView)
            .WithName("ListMissingProbations")
            .WithSummary("Sinov yozuvisiz «sinovda» qolganlar")
            .WithDescription(
                "Holati «sinovda», lekin unga bog'langan sinov muddati yozuvi YO'Q " +
                "munosabatlar. Bunday xodim xodimlar ro'yxatida ham (u yerda doimiylar), " +
                "sinov muddatlari sahifasida ham (u yerda sinov yozuvlari) ko'rinmaydi — " +
                "ya'ni tizimda bor, ekranda yo'q. Manba: Excel'dan ko'chirish, «91.5» " +
                "varag'ida bo'lmagan odamlar. Ro'yxat sahifalanmaydi va tozalash " +
                "tugagach bo'shab qoladi.")
            .Produces<IReadOnlyList<MissingProbationDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/missing/confirm", async (
                ConfirmMissingProbationsRequest request,
                ConfirmMissingProbationsCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ConfirmMissingProbationsCommand(
                            request.EmploymentIds,
                            request.At,
                            request.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentProbationComplete)
            .WithName("ConfirmMissingProbations")
            .WithSummary("Sinov yozuvisiz qolganlarni doimiy holatga o'tkazish")
            .WithDescription(
                "HR belgilagan munosabatlarni bir amalda «faol» qiladi va har biriga " +
                "«sinov natijasi» hodisasini yozadi. Identifikatorlar server tomonda " +
                "QAYTA tekshiriladi — faqat haqiqatan shu ro'yxatda turgan yozuv " +
                "o'zgaradi. Ro'yxatda qolmaganlari xato bermaydi, «skipped» da sanaladi: " +
                "HR ekranni ochib turganda boshqa birov o'zgartirgan bo'lishi mumkin. " +
                "Hammasi bitta tranzaksiyada. Ruxsat: «employment.probation.complete» — " +
                "bu ham sinovni yakunlash, faqat yozuvsiz qolganlar uchun.")
            .Produces<ConfirmMissingProbationsResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
