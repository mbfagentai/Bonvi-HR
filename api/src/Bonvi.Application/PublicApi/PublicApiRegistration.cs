using Bonvi.Application.PublicApi.Keys;
using Bonvi.Application.PublicApi.Queries;
using Bonvi.Application.PublicApi.Team;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.PublicApi;

/// <summary>
/// Tashqi API ning use-case'larini ro'yxatga oladi.
/// </summary>
/// <remarks>
/// Bo'lim o'z bog'liqliklarini o'zi olib boradi — qolgan modullardagi
/// bilan bir xil naqsh (<c>EmployeeRegistration</c> izohi).
/// </remarks>
internal static class PublicApiRegistration
{
    public static IServiceCollection AddPublicApi(this IServiceCollection services)
    {
        // Kalitlarni boshqarish — ICHKI endpoint'lar, `settings.manage` ostida.
        services.AddScoped<ListApiKeysQueryHandler>();
        services.AddScoped<CreateApiKeyCommandHandler>();
        services.AddScoped<RevokeApiKeyCommandHandler>();

        // Kalitni tekshirish — autentifikatsiya sxemasi shu sinfni chaqiradi.
        services.AddScoped<ApiKeyAuthenticator>();

        // Ommaviy o'qish — HAMMASI faqat `GET`.
        services.AddScoped<ListPublicEmployeesQueryHandler>();
        services.AddScoped<GetPublicEmployeeQueryHandler>();
        services.AddScoped<GetPublicEmployeePhotoQueryHandler>();
        services.AddScoped<ListPublicDeletionsQueryHandler>();
        services.AddScoped<ListPublicReferenceQueryHandler>();

        // Bo'lim davomati kaliti — bo'lim rahbarining boti (2026-10-30), «Jamoa» bilan bir xil ma'lumot
        services.AddScoped<PublicTeamAccess>();
        services.AddScoped<GetPublicTeamQueryHandler>();
        services.AddScoped<GetPublicTeamBoardQueryHandler>();
        services.AddScoped<GetPublicTeamMemberAttendanceQueryHandler>();
        services.AddScoped<GetPublicTeamMemberPhotoQueryHandler>();
        services.AddScoped<GetPublicTeamPassesQueryHandler>();

        return services;
    }
}
