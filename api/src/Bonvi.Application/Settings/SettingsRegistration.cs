using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Settings;

/// <summary>Tizim sozlamalari use-case'larini ro'yxatga oladi.</summary>
/// <remarks>
/// <para>
/// <see cref="SystemSettingsReader"/> — <c>Scoped</c>. U qiymatlarni
/// so'rov davomida eslab qoladi, ya'ni umri SO'ROV bilan bir xil
/// bo'lishi SHART. <c>Singleton</c> qilinsa, o'zgartirilgan sozlama
/// jarayon qayta ishga tushmaguncha eskiligicha qolardi; <c>Transient</c>
/// bo'lsa esa kesh ma'nosini yo'qotib, bitta so'rovda bir necha
/// bir xil <c>SELECT</c> ketardi.
/// </para>
/// <para>
/// Reader shu yerda ro'yxatga olinadi, garchi undan
/// <c>NotificationCollector</c> ham foydalansa: sozlamalar BU bo'limga
/// tegishli va bildirishnomalar undan shunchaki O'QIYDI (xuddi
/// <c>CatalogNameResolver</c> ni bir necha bo'lim ishlatgani kabi —
/// u ham bitta joyda ro'yxatga olingan).
/// </para>
/// </remarks>
internal static class SettingsRegistration
{
    public static IServiceCollection AddSettings(this IServiceCollection services)
    {
        services.AddScoped<SystemSettingsReader>();

        services.AddScoped<GetSettingsQueryHandler>();
        services.AddScoped<GetBotSettingsQueryHandler>();
        services.AddScoped<GetBotTokenQueryHandler>();
        services.AddScoped<UpdateSettingsCommandHandler>();

        return services;
    }
}
