using Bonvi.Api.Extensions;
using Bonvi.Application.Employees.Queries;
using Bonvi.Application.Reports.Dtos;
using Bonvi.Application.Reports.Queries;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>
/// Hisobotlar endpoint'lari: kadrlar oqimi va Excel eksport.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <c>/api/dashboard</c> emas, alohida <c>/api/reports</c>.</b> Bosh
/// sahifadagi sonlar (<c>DashboardEndpoints</c>) — «tizimga kirgan HR ko'radigan
/// BIRINCHI ekran» va shu sababli ataylab <c>employees.view</c> bilan ochilgan
/// (izohi o'sha faylda). Bu yerdagi ikkalasi ham boshqa tabiat: kadrlar oqimi —
/// tahliliy hisobot (aynan shu maqsad uchun <c>reports.view</c> allaqachon bor),
/// eksport esa alohida <c>reports.export</c> va tezlik cheklovi (<c>RateLimitPolicies.Export</c>)
/// talab qiladigan OG'IR amal. Ularni bosh sahifa ruxsatiga qo'shish — ikkalasini
/// ham xodimlar bilan ishlay oladigan, lekin hisobot huquqi yo'q foydalanuvchiga
/// ochib qo'yish bo'lardi.
/// </para>
/// </remarks>
public static class ReportsEndpoints
{
    public static IEndpointRouteBuilder MapReportsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/reports").WithTags("Reports");

        MapTurnover(group);
        MapEmployeeExport(group);

        return app;
    }

    // ════════════════════════════════════════════════════════════
    //  Kadrlar oqimi (turnover)
    // ════════════════════════════════════════════════════════════

    private static void MapTurnover(IEndpointRouteBuilder group)
    {
        group.MapGet("/turnover", async (
                int? months,
                GetTurnoverQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetTurnoverQuery(months), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.ReportsView)
            .WithName("GetTurnoverReport")
            .WithSummary("Hisobotlar — kadrlar oqimi (turnover)")
            .WithDescription(
                "So'nggi «months» oy (sukut — 12, eng ko'pi 24) bo'yicha oylik kelgan/ketgan " +
                "hodisalar soni, har oy chegarasidagi shtat soni va shu davrdagi ketish " +
                "sabablarining taqsimoti (mavjud «ishdan ketish sababi» ma'lumotnomasi bo'yicha). " +
                "Foiz (turnover rate) ATAYLAB YO'Q: frontend uni «Left / o'rtacha shtat» formulasi " +
                "bilan o'zi hisoblaydi — kerakli ikkala son (HeadcountStart, HeadcountEnd) javobda bor.")
            .Produces<TurnoverDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    // ════════════════════════════════════════════════════════════
    //  Excel eksport
    // ════════════════════════════════════════════════════════════

    private static void MapEmployeeExport(IEndpointRouteBuilder group)
    {
        group.MapGet("/employees/export", async (
                string? search,
                Guid? departmentId,
                Guid? positionId,
                Guid? directionId,
                Guid? legalEntityId,
                string? status,
                bool? needsReview,
                string? sort,
                DateOnly? from,
                DateOnly? to,
                ExportEmployeesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                // Parametrlar «GET /api/employees» bilan AYNAN bir xil (sahifalashdan
                // tashqari — eksport sahifalanmaydi, butun mos ro'yxatni bitta faylda
                // beradi). Aynan shu bir xillik «ekrandagi bilan bir xil natija»
                // talabining HTTP darajasidagi ko'rinishi.
                var natija = await handler
                    .HandleAsync(
                        new SearchEmployeesQuery(
                            search,
                            departmentId,
                            positionId,
                            directionId,
                            legalEntityId,
                            status,
                            needsReview,
                            Page: null,
                            PageSize: null,
                            sort,
                            from,
                            to),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.File(
                        natija.Value.Content,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        natija.Value.FileName);
            })
            .RequireAuthorization(Permissions.ReportsExport)
            .RequireRateLimiting(RateLimitPolicies.Export)
            .WithName("ExportEmployeesToExcel")
            .WithSummary("Xodimlar ro'yxati — Excel eksport")
            .WithDescription(
                "Filtr va saralash parametrlari «GET /api/employees» bilan AYNAN bir xil — " +
                "eksport ekranda ko'ringan qatorlarni, o'sha tartibda, sahifasiz (hammasini " +
                "bitta faylda) qaytaradi. Eng ko'pi 5000 qator: kattaroq natija «eksport.juda_kop» " +
                "(400) bilan rad etiladi — filtrni toraytiring. «from»/«to» ham ro'yxatdagi kabi " +
                "(«status=Terminated» da ketish sanasi, aks holda ishga kirish sanasi; teskari oraliq — " +
                "«royxat.sana_oraligi_notogri», 400). Sana ustunlari haqiqiy Excel " +
                "sana (dd.mm.yyyy), oklad son va valyuta alohida ustun — HR ularni Excel'ning " +
                "o'zida yig'indilashi yoki saralashi mumkin.")
            .Produces(StatusCodes.Status200OK, contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
