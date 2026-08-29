using Bonvi.Application.Notifications.Commands;
using Bonvi.Application.Notifications.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Notifications;

/// <summary>Bildirishnomalar use-case'larini ro'yxatga oladi.</summary>
internal static class NotificationRegistration
{
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        // Yig'uvchi — to'rtala use-case ham shundan foydalanadi. `Scoped`:
        // u `ICurrentUser` ga bog'liq, ya'ni umri so'rov bilan bir xil
        // bo'lishi SHART. `Singleton` bo'lsa, birinchi so'rovdagi
        // foydalanuvchining ruxsatlari keyingi hammasiga tarqalardi.
        services.AddScoped<NotificationCollector>();

        services.AddScoped<ListNotificationsQueryHandler>();
        services.AddScoped<GetUnreadNotificationCountQueryHandler>();

        services.AddScoped<MarkNotificationsReadCommandHandler>();
        services.AddScoped<DeleteNotificationsCommandHandler>();

        return services;
    }
}
