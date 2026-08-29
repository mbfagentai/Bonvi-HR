using Bonvi.Api.Extensions;
using Bonvi.Application.Notifications.Commands;
using Bonvi.Application.Notifications.Dtos;
using Bonvi.Application.Notifications.Queries;

namespace Bonvi.Api.Endpoints;

/// <summary>«O'qildi» so'rovi tanasi.</summary>
/// <param name="Keys">
/// Qaysi bildirishnomalar — javobdan olingan <c>key</c> qiymatlari.
/// <paramref name="All"/> berilganda e'tiborga olinmaydi.
/// </param>
/// <param name="All">
/// Hammasini belgilash. Kalitlar kerak emas: server foydalanuvchi shu
/// paytda ko'rayotgan bildirishnomalarni O'ZI hisoblaydi (sababi
/// <see cref="MarkNotificationsReadCommand"/> izohida).
/// </param>
public sealed record MarkNotificationsReadRequest(IReadOnlyList<string>? Keys = null, bool All = false);

/// <summary>«O'chirish» so'rovi tanasi.</summary>
/// <param name="Keys">Qaysi bildirishnomalar — javobdan olingan <c>key</c> qiymatlari.</param>
public sealed record DeleteNotificationsRequest(IReadOnlyList<string>? Keys);

/// <summary>
/// Bildirishnomalar endpoint'lari — yuqori paneldagi qo'ng'iroq.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Bu yerda hech narsa saqlanmaydi.</b> Bildirishnomalar so'rov
/// kelganda joriy ma'lumotdan hisoblanadi — fon vazifasi (Hangfire,
/// cron) YO'Q. Sababi <c>INotificationReadModel</c> izohida batafsil:
/// bildirishnomalarning ko'pchiligi hodisa emas, HOLAT, va tizimdan
/// atigi bir necha kishi foydalanadi.
/// </para>
/// <para>
/// <b>Ruxsat: guruh darajasida faqat autentifikatsiya.</b> Kirgan har
/// bir foydalanuvchi o'z bildirishnomalarini ko'radi. MAZMUN esa
/// ruxsatga bog'liq va u endpoint'da emas, yig'ish paytida filtrlanadi
/// (<c>NotificationAccess</c>): <c>recruiting.view</c> yo'q odamga
/// nomzod bildirishnomasi umuman kelmaydi, <c>salary.view</c> yo'qqa
/// esa taklif summasi parametrlarda bo'lmaydi. Guruhni bitta ruxsat
/// bilan yopish noto'g'ri bo'lardi — qaysi birini tanlasak ham, boshqa
/// ruxsatlarga ega foydalanuvchi qo'ng'iroqni umuman ocha olmasdi.
/// </para>
/// </remarks>
public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/notifications").WithTags("Notifications");

        group.MapGet("/", async (
                bool? onlyUnread,
                int? limit,
                ListNotificationsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ListNotificationsQuery(onlyUnread ?? false, limit),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName("ListNotifications")
            .WithSummary("Bildirishnomalar — ro'yxat")
            .WithDescription(
                "Foydalanuvchining SHAXSIY ro'yxati: o'qilganlari ham, o'qilmaganlari ham. " +
                "O'chirilganlar hech qanday parametr bilan qaytmaydi. Javobda o'qilmaganlar " +
                "soni ham bor — qo'ng'iroq ustidagi nuqta uchun alohida so'rov shart emas. " +
                "Matn YO'Q: har bir yozuvda tur («type»), parametrlar («params») va tiplashgan " +
                "havola («link») keladi, jumlani frontend yig'adi (tizim 4 tilli). " +
                "Bildirishnoma mazmuni RUXSATGA bog'liq: «recruiting.view» yo'q foydalanuvchi " +
                "nomzod va taklif yozuvlarini umuman ko'rmaydi, «salary.view» yo'qqa esa " +
                "taklif summasi parametrlarda bo'lmaydi. «limit» — 1 dan 200 gacha (sukut 50); " +
                "sahifalash ataylab yo'q. «onlyUnread=true» — faqat o'qilmaganlar.")
            .Produces<NotificationListDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/unread-count", async (
                GetUnreadNotificationCountQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetUnreadNotificationCountQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName("GetUnreadNotificationCount")
            .WithSummary("Bildirishnomalar — o'qilmaganlar soni")
            .WithDescription(
                "Qo'ng'iroq ustidagi nuqta uchun. Ro'yxat javobida ham shu son bor — bu " +
                "endpoint ro'yxat OCHILMAGANDA sonni yangilab turish uchun: butun ro'yxatni " +
                "tarmoqdan olib o'tmaydi. Sanoq ro'yxat bilan AYNAN bir xil yo'ldan " +
                "hisoblanadi, ya'ni ikkalasi hech qachon ajralib ketmaydi.")
            .Produces<NotificationCountDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/read", async (
                MarkNotificationsReadRequest request,
                MarkNotificationsReadCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new MarkNotificationsReadCommand(request.Keys, request.All),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName("MarkNotificationsRead")
            .WithSummary("Bildirishnomalar — «o'qildi» belgisi")
            .WithDescription(
                "«{ keys: [...] }» yoki «{ all: true }». «all» berilganda kalitlar kerak " +
                "emas va e'tiborga olinmaydi: server foydalanuvchi SHU PAYTDA ko'rayotgan " +
                "bildirishnomalarni o'zi hisoblaydi. Belgi FOYDALANUVCHIGA tegishli — " +
                "bittasi o'qisa, boshqalarda yangiligicha qoladi. Amal idempotent: takroriy " +
                "so'rov birinchi o'qish vaqtini o'zgartirmaydi. Noma'lum turdagi kalit " +
                "«bildirishnoma.kalit_notogri» (400), bo'sh ro'yxat «bildirishnoma.kalit_yoq» " +
                "(400), 200 tadan ortiq kalit «bildirishnoma.kalit_kop» (400). Javobda " +
                "amaldan KEYINGI o'qilmaganlar soni qaytadi.")
            .Produces<NotificationActionResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/delete", async (
                DeleteNotificationsRequest request,
                DeleteNotificationsCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeleteNotificationsCommand(request.Keys), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName("DeleteNotifications")
            .WithSummary("Bildirishnomalar — ro'yxatdan olib tashlash")
            .WithDescription(
                "«{ keys: [...] }». O'chirilgan bildirishnoma O'SHA FOYDALANUVCHI uchun " +
                "boshqa hech qachon chiqmaydi — hatto sharti hamon rost bo'lsa ham (masalan " +
                "sinov muddati yakunlanmagan bo'lsa). Boshqa foydalanuvchilarda o'z holicha " +
                "qoladi. «Hammasini o'chirish» ataylab YO'Q: bir bosishda butun ish " +
                "ro'yxatini yo'qotib bo'lmaydi (izohi «DeleteNotificationsCommand» da).")
            .Produces<NotificationActionResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
