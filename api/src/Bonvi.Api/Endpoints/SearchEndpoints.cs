using Bonvi.Api.Extensions;
using Bonvi.Application.Search.Dtos;
using Bonvi.Application.Search.Queries;

namespace Bonvi.Api.Endpoints;

/// <summary>
/// Global qidiruv endpoint'i — yuqori paneldagi qidiruv maydoni.
/// </summary>
public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/search").WithTags("Search");

        group.MapGet("/", async (
                string? q,
                GlobalSearchQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GlobalSearchQuery(q), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            // `employees.view` YOKI `recruiting.view` — sababi
            // `AuthenticationExtensions.GlobalSearchPolicy` izohida. Ruxsati
            // yo'q guruh javobda `null` bo'ladi.
            .RequireAuthorization(AuthenticationExtensions.GlobalSearchPolicy)
            .WithName("GlobalSearch")
            .WithSummary("Global qidiruv — barcha bo'limlar bo'yicha")
            .WithDescription(
                "Bitta matn bilan xodimlar, nomzodlar va vakansiyalardan qidiradi. Qidiruv " +
                "qoidasi bo'limlardagi bilan AYNAN bir xil (xodim: F.I.Sh. lotin/kirill " +
                "farqsiz, JSHSHIR, pasport, telefon; nomzod: F.I.Sh. va telefon; vakansiya: " +
                "nom), lekin HECH KIM yashirilmaydi: ishdan ketgan xodimlar, ishga olingan, " +
                "rad etilgan, o'zi voz kechgan, rezerv va qora ro'yxatdagi nomzodlar hamda " +
                "Excel arxivi ham qidiriladi. Har guruhda birinchi bir nechta yozuv (xodim 6, " +
                "nomzod 6, vakansiya 3) va «total» — topilganlarning hammasi. Xodimlar: avval " +
                "ishlayotganlar, keyin ishdan ketganlar, familiya bo'yicha; nomzodlar va " +
                "vakansiyalar: eng yangisi birinchi. «employees» guruhi faqat «employees.view», " +
                "«candidates» va «vacancies» faqat «recruiting.view» ruxsati borlarga " +
                "qaytadi, boshqalarda «null». 2 belgidan qisqa matn qidirilmaydi (bo'sh " +
                "guruhlar), 100 belgidan uzuni kesiladi.")
            .Produces<GlobalSearchDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
