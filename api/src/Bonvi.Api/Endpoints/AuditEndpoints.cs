using Bonvi.Api.Extensions;
using Bonvi.Application.Audit;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Reports.Queries;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>
/// Audit jurnalini O'QISH — «kim, qachon, nimani o'zgartirdi» (SPEC-rollar-va-audit §8.3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Faqat <c>GET</c>.</b> Jurnal o'zgarmas: bu yerda yozish, tahrirlash va o'chirish
/// marshruti yo'q va bo'lmaydi — ilovada ham (<c>AuditTrailInterceptor</c>), bazada ham
/// (<c>audit_logs_append_only</c> triggeri) qo'riqlangan.
/// </para>
/// <para>
/// <b>Ikki qavat himoya:</b> endpoint siyosati — <c>audit.view</c>; QATOR esa so'rovchining
/// o'sha bo'limga kirish huquqi bo'lsagina ko'rinadi, oklad va shaxsiy maydonlar alohida
/// ruxsat bilan (<see cref="AuditVisibility"/>). Ya'ni <c>audit.view</c> — «jurnalni ochish»,
/// «hamma narsani ko'rish» emas.
/// </para>
/// <para>
/// Xodim kartasidagi tarix (<c>/api/employees/{id}/history</c>) ham shu faylda, o'z
/// guruhida: qoidasi jurnalniki bilan bir, faqat manzili xodim ostida.
/// </para>
/// </remarks>
public static class AuditEndpoints
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/audit").WithTags("Audit");

        group.MapGet("/", async (
                DateOnly? from,
                DateOnly? to,
                string? actor,
                string? module,
                string? action,
                Guid? employeeId,
                string? entity,
                string? entityId,
                string? kind,
                int? page,
                int? pageSize,
                ListAuditEntriesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ListAuditEntriesQuery(
                            from,
                            to,
                            actor,
                            module,
                            action,
                            employeeId,
                            entity,
                            entityId,
                            kind,
                            page,
                            pageSize),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AuditView)
            .WithName("ListAuditEntries")
            .WithSummary("Audit jurnali — kim, qachon, nimani o'zgartirdi")
            .WithDescription(
                "Eng yangisi birinchi (vaqt, keyin identifikator bo'yicha kamayish). «from»/«to» — " +
                "Toshkent sanalari, ikkalasi ham kiradi; «to» berilmasa — bugun, «from» berilmasa — " +
                "«to» dan 30 kun oldin (davr DOIM chegaralangan); «from» > «to» — «audit.davr_notogri» (400). " +
                "«actor» — foydalanuvchi identifikatori yoki «system». «module» — bo'lim kaliti " +
                "(employees, employment, lists, incentives, feedback, safety, recruiting, attendance, " +
                "catalog, reports, users, roles, audit, settings). «action» — Create | Update | Delete | " +
                "Login | LoginFailed | Logout | Export | PermissionDenied | TokenReuse. «entity» — entity " +
                "turi (Employee, EmploymentEvent…), «entityId» — bitta yozuv, «employeeId» — xodimga " +
                "tegishli qatorlar. «kind»: changes (sukut: o'zgarishlar va Excel eksporti) | security " +
                "(kirish va xavfsizlik hodisalari) | all; «users.manage» siz security/all — changes " +
                "bo'lib ishlaydi. Notanish bo'lim, amal, yozuv turi, ko'rinish yoki bajaruvchi — " +
                "«audit.filtr_notogri» (400). KO'RINISH filtrdan oldin qo'llanadi: qator faqat " +
                "so'rovchiga o'sha bo'limning kirish ruxsati (eng quyi darajasining kodlari) bo'lsa " +
                "chiqadi — ko'rinmaydigan bo'lim so'ralsa xato emas, sahifa bo'sh. Hujjat, qobiliyat va " +
                "tekshiruv materiali qatorlari — «employees.personal.view» (yoki «employees.edit») bilan. " +
                "Ko'rinadigan qator ichida oklad — «salary.view», qo'shimcha vazifa haqi — " +
                "«salary.extra.view» ham, shaxsiy maydonlar — «employees.personal.view» bilan; aks " +
                "holda o'zgarish «masked: true» va qiymatsiz keladi (maydon NOMI ko'rinadi). Sirlar " +
                "(parol xeshi, kalitlar) — hammaga «masked». Pul — bitta o'zgarish " +
                "{ amount, currency }; havolalar — «oldLabel»/«newLabel» bilan (o'chirilgan nomlar ham). " +
                "«page» 1 dan, «pageSize» sukut 50, eng ko'pi 200.")
            .Produces<PagedResult<AuditEntryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // «/export» ham «/{id:guid}» bilan to'qnashmaydi. Ko'rinish va yashirish — ro'yxatning
        // o'zidan (`ListAuditEntriesQueryHandler.BuildFilter`), ya'ni fayl ekrandan ko'p bermaydi.
        group.MapGet("/export", async (
                DateOnly? from,
                DateOnly? to,
                string? actor,
                string? module,
                string? action,
                Guid? employeeId,
                string? entity,
                string? entityId,
                string? kind,
                ExportAuditEntriesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ListAuditEntriesQuery(
                            from,
                            to,
                            actor,
                            module,
                            action,
                            employeeId,
                            entity,
                            entityId,
                            kind,
                            Page: null,
                            PageSize: null),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.File(
                        natija.Value.Content,
                        XlsxContentType,
                        natija.Value.FileName);
            })
            // Ikkala ruxsat ham kerak: jurnalni ochish (`audit.view`) va tizimdan fayl olib
            // chiqish (`reports.export`) — boshqa eksportlar bilan bir xil.
            .RequireAuthorization(Permissions.AuditView, Permissions.ReportsExport)
            .RequireRateLimiting(RateLimitPolicies.Export)
            .WithName("ExportAuditEntriesToExcel")
            .WithSummary("Audit jurnali — Excel eksport")
            .WithDescription(
                "Filtr parametrlari «GET /api/audit» bilan AYNAN bir xil (sahifasiz) — ekrandagi yozuvlar, " +
                "o'sha tartibda (eng yangisi birinchi), bitta faylda; davr berilmasa — o'sha 30 kun. " +
                "KO'RINISH ham ro'yxatniki: ko'rinmaydigan bo'lim qatorlari tushmaydi, «kind=security|all» " +
                "faqat «users.manage» bilan (usiz — «changes»), oklad va shaxsiy maydonlar ruxsatsiz «•••». " +
                "Ustunlar: vaqt (Toshkent), kim, roli (o'sha lahzadagi), amal, nima, yozuv turi, " +
                "o'zgarishlar (ekrandagi bir qatorlik xulosa). IP manzil yo'q — u ro'yxat qatorida emas. " +
                "Eng ko'pi 5000 qator — «eksport.juda_kop» (400); filtr xatolari ro'yxatdagidek " +
                "(«audit.filtr_notogri», «audit.davr_notogri»). Ruxsat: «audit.view» VA «reports.export». " +
                "Har bir eksport audit jurnaliga yoziladi.")
            .Produces(StatusCodes.Status200OK, contentType: XlsxContentType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // «/actors» «/{id:guid}» bilan to'qnashmaydi (marshrut cheklovi GUID talab qiladi).
        group.MapGet("/actors", async (
                ListAuditActorsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AuditView)
            .WithName("ListAuditActors")
            .WithSummary("Audit jurnali — «Kim» filtrining tanlovlari")
            .WithDescription(
                "Jurnalda kamida bir marta uchragan tizim foydalanuvchilari (o'chirilgan va faolsiz " +
                "hisoblar ham — «isActive»), F.I.Sh. bo'yicha. Tizimning o'zi bu ro'yxatda yo'q — " +
                "uning filtri «actor=system». Xodim ilovasi va qurilmalar ham yo'q: ularning " +
                "qatorlarida foydalanuvchi bo'lmaydi.")
            .Produces<IReadOnlyList<AuditActorOptionDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                GetAuditEntryQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetAuditEntryQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AuditView)
            .WithName("GetAuditEntry")
            .WithSummary("Audit jurnali — bitta yozuv")
            .WithDescription(
                "Qator — IP manzil va brauzer satri bilan; «related» — SHU harakatda (bitta so'rov " +
                "yoki tizim amali, bir xil «correlationId») yozilgan boshqa qatorlar, so'rovchi ko'ra " +
                "oladiganlari, yozilish tartibida, eng ko'pi 50 ta. Yashirish qoidalari ro'yxatdagi " +
                "bilan bir xil. Yozuv yo'q YOKI so'rovchiga ko'rinmaydi — «audit.topilmadi» (404): " +
                "ikkalasi ataylab bir xil, aks holda yozuv borligi ma'lum bo'lib qolardi.")
            .Produces<AuditEntryDetailsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var employees = app.MapGroup("/api/employees").WithTags("Audit");

        employees.MapGet("/{id:guid}/history", async (
                Guid id,
                string? module,
                int? page,
                int? pageSize,
                ListEmployeeHistoryQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListEmployeeHistoryQuery(id, module, page, pageSize), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AuditView, Permissions.EmployeesView)
            .WithName("ListEmployeeHistory")
            .WithSummary("Xodim kartasi — o'zgarishlar tarixi")
            .WithDescription(
                "Shu xodimga tegishli barcha o'zgarishlar (karta, mehnat munosabati va buyruqlar, sinov " +
                "muddati, belgilar, rag'bat, hodisalar, davomat…) — eng yangisi birinchi, davr " +
                "chegarasiz. Kirish hodisalari va eksport bu yerda yo'q. «audit.view» VA " +
                "«employees.view» ikkalasi ham kerak. Ko'rinish va yashirish jurnaldagi bilan AYNAN " +
                "bir xil: ko'rinmaydigan bo'limning qatorlari tushmaydi, oklad va shaxsiy maydonlar " +
                "ruxsatsiz «masked». «module» — kartadagi bo'lim chipi (notanish kalit — " +
                "«audit.filtr_notogri», 400). Notanish yoki o'chirilgan xodim — xato emas, bo'sh yoki " +
                "o'sha xodimning saqlangan tarixi. «page» 1 dan, «pageSize» sukut 50, eng ko'pi 200.")
            .Produces<PagedResult<AuditEntryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
