using System.Security.Claims;
using System.Text.Json;
using Bonvi.Api.Common;
using Bonvi.Api.Extensions;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Notifications.Commands;
using Bonvi.Application.Notifications.Dtos;
using Bonvi.Application.Notifications.Queries;
using Microsoft.AspNetCore.Http.Features;

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
/// nomzod bildirishnomasi umuman kelmaydi. Guruhni bitta ruxsat
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
                "nomzod yozuvlarini umuman ko'rmaydi. «limit» — 1 dan 200 gacha (sukut 50); " +
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

        /*
          ═══════════════════════════════════════════════════════════
           JONLI OQIM (Server-Sent Events)

           ⭐ Nima muammoni yechadi. Ilgari qo'ng'iroq ustidagi son
           bir daqiqada bir marta so'ralardi va ro'yxatning o'zi
           faqat oyna ochilganda. Ya'ni bot orqali ariza tushganda
           ish bilan band HR buni bilmasdi — sahifani yangilagandan
           keyingina ko'rardi. Foydalanuvchi aynan shuni nosozlik
           deb ko'rsatdi.

           ⭐ Nega SSE, WebSocket emas. Bu yerda kerak bo'lgan narsa —
           BIR TOMONLAMA oqim: server aytadi, brauzer eshitadi.
           WebSocket ikki tomonlama kanal, o'z protokoli, o'z
           holati va o'z nosozliklari bilan keladi — bu yerda
           ishlatilmaydigan yarmi uchun. SSE esa oddiy HTTP javobi:
           u orqada turgan proksidan (Caddy) o'zgarishsiz o'tadi.

           ⭐ Nega davriy so'rovni tezlashtirish emas. Tezlashtirish
           kechikishni faqat kamaytiradi, yo'qotmaydi: 10 soniyalik
           davr bilan ham HR eng yomon holatda 10 soniya kech
           biladi, evaziga esa kun bo'yi bo'sh so'rov ketadi.
           Bu yerda esa so'rov UMUMAN yo'q — javob o'zgarish sodir
           bo'lgan zahoti keladi.

           ⭐ Davriy so'rov OLIB TASHLANMADI (`useUnreadCount`,
           60 soniya). U endi ZAXIRA: oqim uzilgan, brauzer eski
           yoki proksi ulanishni yopib qo'ygan holatda son baribir
           yangilanib turadi. Ikkalasi bir xil kesh kalitiga
           yozadi, ya'ni to'qnashuv yo'q.

           ⚠️ Bu oqim BITTA JARAYON ichida ishlaydi (sabab va
           almashtirish yo'li `IDataChangeSignal` izohida).
          ═══════════════════════════════════════════════════════════
        */
        group.MapGet("/stream", StreamNotificationsAsync)
            .RequireAuthorization()
            .WithName("StreamNotifications")
            .WithSummary("Bildirishnomalar — jonli oqim (SSE)")
            .WithDescription(
                "`text/event-stream`. Ulanish ochiq turadi va server o'zgarish bo'lganda " +
                "`event: notifications` yuboradi; tanasi `GET /api/notifications` javobi " +
                "bilan AYNAN bir xil shaklda (`items`, `total`, `unreadCount`) — ya'ni " +
                "mijozda ikkinchi xaritalash yozilmaydi. Birinchi xabar ulanish bilan " +
                "darhol keladi (boshlang'ich holat). Keyingilari faqat SHU foydalanuvchi " +
                "uchun javob HAQIQATAN o'zgarganda: boshqa birov o'z bildirishnomasini " +
                "o'qigani bu yerga xabar keltirmaydi. Har 20 soniyada izoh qatori " +
                "(`: ping`) yuboriladi — u proksi ulanishni «bo'sh» deb yopib qo'yishining " +
                "oldini oladi va mijozga uzilishni sezishga yordam beradi. " +
                "⚠️ `EventSource` sarlavha qo'sha olmaydi, shu sababli mijoz oqimni " +
                "`fetch` bilan o'qiydi va `Authorization` ni odatdagidek yuboradi.")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    /// <summary>Oqimda nechta bildirishnoma yuboriladi.</summary>
    /// <remarks>
    /// Ro'yxat endpoint'ining sukut chegarasi bilan AYNAN bir xil: mijoz
    /// oqimdan kelgan javobni to'g'ridan-to'g'ri o'sha kesh kalitiga
    /// yozadi. Ikkalasi farq qilsa, oyna ochilganda ro'yxatning uzunligi
    /// «sakrab» o'zgarardi.
    /// </remarks>
    private const int StreamLimit = ListNotificationsQueryHandler.DefaultLimit;

    /// <summary>«Tirikman» belgisi qancha vaqtda bir yuboriladi.</summary>
    /// <remarks>
    /// 20 soniya — proksilarning odatiy «bo'sh ulanish» chegarasidan
    /// (60 s) sezilarli qisqa. Uzunroq qilinsa, ulanish jimgina yopilib,
    /// mijoz buni faqat keyingi o'zgarishda sezardi — ya'ni aynan
    /// tuzatilayotgan nosozlik qaytib kelardi.
    /// </remarks>
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(20);

    /// <summary>
    /// O'zgarish sezilgach, hisoblashdan OLDIN kutiladigan vaqt.
    /// </summary>
    /// <remarks>
    /// Bitta amal ko'pincha bir necha marta saqlaydi (masalan nomzod
    /// yoziladi, keyin bosqichga qo'yiladi). Ularning har biriga alohida
    /// qayta hisoblash o'rniga, birinchi signaldan keyin qisqa pauza
    /// olinadi va ular BITTA hisobga qo'shiladi. 250 ms odam sezmaydigan
    /// kechikish, lekin ketma-ket saqlashlarni yig'ib olishga yetadi.
    /// </remarks>
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Ulanishni ochiq ushlab, o'zgarishlarni yuborib turadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega har hisob uchun YANGI qamrov (scope).</b> So'rov qamrovi
    /// bu yerda soatlab yashaydi. Ro'yxat handler'i esa <c>Scoped</c> va
    /// u bilan birga <c>DbContext</c> keladi — so'rov qamrovidan
    /// olinganda bitta ulanish va bitta o'zgarish kuzatuvchisi butun
    /// oqim davomida ochiq qolardi. Bu yerda esa qamrov hisob vaqtigina
    /// yashaydi.
    /// </para>
    /// <para>
    /// <c>ICurrentUser</c> yangi qamrovda ham TO'G'RI ishlaydi: u
    /// <c>IHttpContextAccessor</c> orqali o'qiydi, u esa qamrovga emas,
    /// bajarilayotgan so'rovga bog'langan.
    /// </para>
    /// <para>
    /// <b>Nega har aylanishda huquq qayta o'qiladi.</b> Autentifikatsiya
    /// ulanish ochilganda BIR MARTA bajariladi, oqim esa soatlab yashaydi.
    /// Tekshiruvsiz faolsizlantirilgan hisob yoki huquqi qisqartirilgan rol
    /// ulanish yopilgunicha bildirishnoma olib turardi — «rol o'zgarishi
    /// keyingi so'rovdayoq kuchga kiradi» degan qoida shu yerda buzilardi.
    /// Hisob faol bo'lmasa yoki ruxsatlari o'zgargan bo'lsa, oqim YOPILADI:
    /// mijoz odatdagidek qayta ulanadi va yangi huquq bilan (yoki 401 bilan)
    /// qaytadi. Narxi yo'q — <see cref="IUserAccessReader"/> ≤ 30 s keshdan o'qiydi.
    /// </para>
    /// <para>
    /// <b>Nega javob solishtiriladi.</b> Signal butun tizim bo'yicha
    /// umumiy: boshqa foydalanuvchining har bir yozuvi ham uni
    /// uyg'otadi. Solishtiruvsiz har bir yozuv HAR BIR ulangan
    /// brauzerga xabar yuborardi — mijozda esa «yangi bildirishnoma»
    /// animatsiyasi bekorga o'ynardi.
    /// </para>
    /// </remarks>
    private static async Task StreamNotificationsAsync(
        HttpContext http,
        IServiceScopeFactory scopes,
        IDataChangeSignal signal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(signal);

        http.Response.Headers.ContentType = "text/event-stream; charset=utf-8";
        http.Response.Headers.CacheControl = "no-cache, no-store";

        // Nginx/Caddy uchun: bu javobni BUFERLAMA. Buferlangan oqim
        // to'planib, faqat ulanish yopilganda yetib borardi.
        http.Response.Headers["X-Accel-Buffering"] = "no";

        // Kestrel javobni o'zi ham buferlaydi — oqim uchun bu o'chiriladi.
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var userId = Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : Guid.Empty;
        var grantedAtConnect = http.User
            .FindAll(CurrentUser.PermissionClaimType)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);

        var known = signal.Version;
        string? lastSent = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!await StillGrantedAsync(scopes, userId, grantedAtConnect, cancellationToken).ConfigureAwait(false))
            {
                // Oqim tugaydi — mijoz qayta ulanadi va yangi huquq bilan qaytadi.
                return;
            }

            var payload = await ReadAsync(scopes, cancellationToken).ConfigureAwait(false);

            if (payload is not null && payload != lastSent)
            {
                lastSent = payload;

                await http.Response
                    .WriteAsync($"event: notifications\ndata: {payload}\n\n", cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (payload is null)
            {
                // Hisob bajarilmadi (masalan baza bir lahzaga yo'qoldi).
                // Ulanish UZILMAYDI: mijoz uni qayta ochib, xuddi shu
                // holatga tushardi. Keyingi aylanishda yana uriniladi.
                await http.Response.WriteAsync(": retry\n\n", cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // O'zgarish yo'q — faqat «tirikman».
                await http.Response.WriteAsync(": ping\n\n", cancellationToken).ConfigureAwait(false);
            }

            await http.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

            var current = await signal
                .WaitAsync(known, Heartbeat, cancellationToken)
                .ConfigureAwait(false);

            if (current != known)
            {
                known = current;

                // Ketma-ket saqlashlarni bitta hisobga yig'ish.
                await Task.Delay(Debounce, cancellationToken).ConfigureAwait(false);
                known = signal.Version;
            }
        }
    }

    /// <summary>
    /// Hisob hamon faolmi va ruxsatlari ulanish ochilgandagi bilan bir xilmi.
    /// </summary>
    /// <remarks>
    /// O'qishning o'zi yiqilsa (baza bir lahzaga yo'qoldi) — <c>true</c>:
    /// oqim bunday holatda uzilmaydi (sababi <see cref="ReadAsync"/> izohida),
    /// huquq esa keyingi aylanishda yana tekshiriladi.
    /// </remarks>
    private static async Task<bool> StillGrantedAsync(
        IServiceScopeFactory scopes,
        Guid userId,
        IReadOnlySet<string> grantedAtConnect,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();

            var access = await scope.ServiceProvider
                .GetRequiredService<IUserAccessReader>()
                .ReadAsync(userId, cancellationToken)
                .ConfigureAwait(false);

            return access is not null && access.Permissions.SetEquals(grantedAtConnect);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>
    /// Joriy foydalanuvchining ro'yxatini JSON satr ko'rinishida oladi.
    /// </summary>
    /// <returns>Xato bo'lsa <c>null</c> — oqim uzilmaydi.</returns>
    private static async Task<string?> ReadAsync(
        IServiceScopeFactory scopes,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();

            var handler = scope.ServiceProvider.GetRequiredService<ListNotificationsQueryHandler>();

            var natija = await handler
                .HandleAsync(new ListNotificationsQuery(OnlyUnread: false, Limit: StreamLimit), cancellationToken)
                .ConfigureAwait(false);

            if (natija.IsFailure)
            {
                return null;
            }

            /*
              Nomlar `camelCase` — ilovaning qolgan javoblari bilan bir
              xil (`Program.cs` dagi JSON sozlamasi). Bu yerda serializator
              QO'LDA chaqirilgani uchun sozlama ham qo'lda beriladi: aks
              holda oqimdan kelgan `Items` bilan odatiy so'rovdan kelgan
              `items` mijozda ikki xil o'qilardi.

              Yangi qator MUMKIN EMAS: SSE da `data:` maydonini aynan
              yangi qator tugatadi. `WriteIndented` yoqilmagan, ya'ni
              natija bir satr bo'lib chiqadi.
            */
            return JsonSerializer.Serialize(natija.Value, StreamJson);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Oqim ichidagi xato butun ulanishni yiqitmasligi kerak
            // (izohi chaqiruv joyida). Jurnalga u yuqoridagi umumiy
            // xato ishlovchisi orqali tushmaydi — shu sababli bu yerda
            // ataylab yutiladi va mijoz `: retry` belgisini oladi.
            return null;
        }
    }

    /// <summary>Oqim uchun JSON sozlamasi — ilovaning qolgan javoblari bilan bir xil.</summary>
    private static readonly JsonSerializerOptions StreamJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
}
