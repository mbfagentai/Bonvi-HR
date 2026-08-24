using Bonvi.Api.Extensions;
using Bonvi.Application.Dashboard.Dtos;
using Bonvi.Application.Dashboard.Queries;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>
/// Bosh sahifa endpoint'lari.
/// </summary>
/// <remarks>
/// Endpoint'lar mavzu bo'yicha guruhlanadi va guruh <c>Map...</c>
/// kengaytma metodi orqali <c>Program.cs</c> ga ulanadi — kompozitsiya
/// ildizi qisqa qoladi.
/// </remarks>
public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/dashboard").WithTags("Dashboard");

        // «period» ATAYLAB satr bo'lib qabul qilinadi, sanoq bo'lib emas:
        // minimal API sanoqni registrga sezgir bog'laydi va `?period=day`
        // 400 bo'lib qaytardi. Sababi va yechimi `GetDashboardStatsQuery`
        // izohida — tekshiruv Application qatlamida, xato esa boshqa
        // validatsiya xatolari bilan bir xil ko'rinishda qaytadi.
        group.MapGet("/stats", async (
                string? period,
                GetDashboardStatsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetDashboardStatsQuery(period), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            // ─────────────────────────────────────────────────────────
            //  Nega aynan `employees.view`.
            //
            //  Bu yerdagi raqamlarning hammasi xodimlar ro'yxatidan
            //  hosil bo'ladi: kim ishlayapti, kim sinovda, kim qachon
            //  kelgan. Ya'ni ro'yxatni ko'ra oladigan odam bu sonlarni
            //  o'zi ham sanab chiqa oladi — alohida sir yo'q.
            //
            //  `reports.view` ataylab TANLANMADI: bu sahifa hisobot emas,
            //  tizimga kirgandan keyingi BIRINCHI ekran. Uni «hisobotlar»
            //  ruxsatiga bog'lash — xodimlar bilan ishlash huquqi bor,
            //  lekin hisobot huquqi yo'q foydalanuvchiga bosh sahifada
            //  xato ko'rsatish demak.
            //
            //  Ma'lumotnomalar SONI ham shu ruxsat ostida: son yozuvning
            //  o'zi emas, ro'yxat mazmuni ochilmaydi. Ikkita ruxsatni
            //  birdan talab qilish esa sahifani sababsiz sindirardi.
            // ─────────────────────────────────────────────────────────
            .RequireAuthorization(Permissions.EmployeesView)
            .WithName("GetDashboardStats")
            .WithSummary("Bosh sahifa ko'rsatkichlari")
            .WithDescription(
                "Tizimga kirgan HR ko'radigan birinchi ekranning barcha raqamlari: " +
                "ishlayotgan xodimlar, sinovdagilar, arxiv, tanlangan davrdagi qabul va " +
                "bo'shatishlar, bugungi tug'ilgan kunlar hamda ma'lumotnomalardagi faol " +
                "yozuvlar soni. «period» qiymatlari: day | week | days15 | month " +
                "(sukut — month); noma'lum qiymat 400 beradi. Davr — kalendar oralig'i " +
                "emas, bugundan orqaga siljuvchi oyna; javobda uning aniq chegaralari " +
                "(«from», «to») va TAQQOSLASH uchun oldingi shunday davrning chegaralari " +
                "va sonlari ham qaytadi. Xodimlar ODAM bo'yicha sanaladi: ikki firmada " +
                "ishlaydigan o'rindosh bir marta hisoblanadi.")
            .Produces<DashboardStatsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
