using Bonvi.Api.Extensions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Employees.Employments;
using Bonvi.Application.Employees.Probations;
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

/// <summary>Sinov muddati xulosasi so'rovi tanasi.</summary>
/// <param name="Status">Natija: Passed | Failed | LeftEarly | Cancelled.</param>
/// <param name="ResolvedAt">Xulosa sanasi.</param>
/// <param name="Conclusion">Xulosa matni.</param>
/// <param name="ReviewerEmployeeId">Xulosa bergan mas'ul.</param>
public sealed record ResolveProbationRequest(
    ProbationStatus Status,
    DateOnly? ResolvedAt = null,
    string? Conclusion = null,
    Guid? ReviewerEmployeeId = null);

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
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentManage)
            .WithName("HireEmployee")
            .WithSummary("Ishga qabul — yagona forma")
            .WithDescription(
                "Shaxs + firma + bo'lim + lavozim + oklad + buyruq BITTA so'rovda. " +
                "Xodim «employeeId» bilan ko'rsatiladi yoki «person» ichida yangi kiritiladi — " +
                "ikkinchi holatda avval JSHSHIR bo'yicha qidiriladi va topilsa DUBLIKAT " +
                "yaratilmaydi (qayta ishga olish). " +
                "Sinov muddati AVTOMATIK ochiladi: lavozimning «probationDays» qiymati, " +
                "u yo'q bo'lsa 90 kun; «probationDays: 0» — sinovsiz qabul. " +
                "Qora ro'yxatdagi xodim: «xodim.qora_royxatda» (409). Nazoratdagi xodim va " +
                "stavkalar yig'indisi 1.5 dan oshgani — javobdagi «warnings» massivida, " +
                "amal esa bajariladi. Javobda lavozimning vazifalari ham qaytadi.")
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
                "«search» — BUYRUQ RAQAMI bo'yicha (FIO emas). «from»/«to» — kuchga kirish " +
                "sanasining ikki tomondan yopiq oralig'i. «departmentId» — bo'lim ICHKI " +
                "bo'limlari bilan birga: hodisaning yangi yoki eski bo'limiga, hodisada bo'lim " +
                "ko'rsatilmagan bo'lsa (oklad, bo'shatish, qo'shimcha vazifa) munosabatning " +
                "joriy bo'limiga mos keladi. Ro'yxat kuchga kirish sanasi bo'yicha " +
                "kamayish tartibida, «pageSize» eng ko'pi 200. " +
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
            .RequireAuthorization(Permissions.EmploymentManage)
            .WithName("TransferEmployee")
            .WithSummary("Lavozim yoki bo'lim o'zgarishi")
            .WithDescription(
                "Bo'sh qoldirilgan maydon «o'zgarmadi» degani. Lavozim o'zgarsa hodisa " +
                "«Promotion», faqat bo'lim o'zgarsa «Transfer» bo'lib yoziladi — hisobotda " +
                "ular alohida ko'rinadi. Yakunlangan munosabat: «mehnat.tugagan» (409).")
            .Produces<EmploymentChangeDto>(StatusCodes.Status200OK)
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
            .RequireAuthorization(Permissions.EmploymentManage)
            .WithName("AddAdditionalDuty")
            .WithSummary("Qo'shimcha vazifa berish")
            .WithDescription(
                "Mavjud ish doirasidagi qo'shimcha yuk (yangi mehnat munosabati EMAS). " +
                "Vazifa matni majburiy: «mehnat.vazifa_bosh» (400).")
            .Produces<EmploymentChangeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
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
            .RequireAuthorization(Permissions.EmploymentManage)
            .WithName("ChangeSalary")
            .WithSummary("Oklad o'zgarishi")
            .WithDescription(
                "Eski va yangi qiymat hodisada saqlanadi — oklad tarixi shundan quriladi. " +
                "DIQQAT: amalni «employment.manage» bajaradi, lekin javobdagi oklad maydoni " +
                "«salary.view» ruxsatisiz bo'sh qaytadi.")
            .Produces<EmploymentChangeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

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
            .RequireAuthorization(Permissions.EmploymentManage)
            .WithName("TerminateEmployment")
            .WithSummary("Ishdan bo'shatish")
            .WithDescription(
                "BLOK QOIDASI: xodim qora ro'yxatda yoki nazoratda bo'lsa, birinchi so'rov " +
                "409 bilan qaytadi («xodim.qora_royxatda» yoki «xodim.nazoratda»). " +
                "HR ataylab «?confirm=true» yuborsa amal bajariladi va sabab «warnings» da " +
                "qayd etiladi. Ochiq sinov muddati «erta ketdi» deb yopiladi. " +
                "Ketish sanasi ishga kirish sanasidan oldin bo'lolmaydi: «mehnat.ketish_sanasi».")
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
                ListProbationsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListProbationsQuery(status, endingWithinDays), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentView)
            .WithName("ListProbations")
            .WithSummary("Sinov muddatlari — ro'yxat")
            .WithDescription(
                "«status» bo'yicha filtr (InProgress | Passed | Failed | LeftEarly | Cancelled). " +
                "«endingWithinDays=7» — muddati tugashiga oz qolganlar; ro'yxat tugash sanasi " +
                "bo'yicha tartiblanadi, ya'ni eng shoshilinchi tepada.")
            .Produces<IReadOnlyList<ProbationListItemDto>>(StatusCodes.Status200OK)
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
                            request.ReviewerEmployeeId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentManage)
            .WithName("ResolveProbation")
            .WithSummary("Sinov muddati — xulosa")
            .WithDescription(
                "«Passed» bo'lsa mehnat munosabati «sinovda» dan «faol» ga o'tadi — " +
                "aks holda xodim ro'yxatda abadiy sinovda qolardi. " +
                "Yakunlangan sinovni qayta yakunlab bo'lmaydi: «sinov.yakunlangan» (409).")
            .Produces<ProbationDto>(StatusCodes.Status200OK)
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
            .RequireAuthorization(Permissions.EmploymentManage)
            .WithName("ExtendProbation")
            .WithSummary("Sinov muddati — uzaytirish")
            .WithDescription(
                "Yangi sana joriy tugash sanasidan keyin bo'lishi kerak va umumiy muddat " +
                "180 kundan oshmasligi kerak (mehnat kodeksi chegarasi): «sinov.muddat» (400).")
            .Produces<ProbationDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
