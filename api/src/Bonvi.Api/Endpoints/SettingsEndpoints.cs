using Bonvi.Api.Extensions;
using Bonvi.Application.Settings;
using Bonvi.Application.Settings.Dtos;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>Sozlamalarni yozish so'rovi tanasi.</summary>
/// <remarks>
/// <para>
/// Ro'yxat, chunki forma bitta: HR bir necha qiymatni o'zgartirib,
/// bitta «Saqlash» bosadi (sababi <see cref="UpdateSettingsCommand"/> da).
/// </para>
/// <para>
/// Element turi (<see cref="SystemSettingUpdate"/>) use-case qatlamidan
/// TO'G'RIDAN-TO'G'RI olinadi, egizak yozuv yaratilmaydi: unda atigi
/// ikkita maydon bor va ikkalasi ham xom satr. Nusxa yozilsa, u faqat
/// bitta ishni — bir turdagi yozuvni ikkinchisiga ko'chirishni —
/// qilardi va yangi maydon qo'shilganda unutilib qolardi.
/// </para>
/// </remarks>
/// <param name="Items">
/// O'zgartiriladigan sozlamalar: <c>[{ "key": "...", "value": "..." }]</c>.
/// O'zgarmaganlarini yuborish SHART EMAS — ular joyida qoladi.
/// </param>
public sealed record UpdateSettingsRequest(IReadOnlyList<SystemSettingUpdate>? Items);

/// <summary>
/// Tizim sozlamalari endpoint'lari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ruxsat: <see cref="Permissions.SettingsManage"/> — u faqat super
/// administratorda.</b> Bosh rahbarda ham yo'q va bu loyihadagi
/// «rahbariyat ruxsatlar bo'yicha TENG» qoidasidan yagona chekinish
/// (foydalanuvchi qarori, sabab <see cref="Permissions.SettingsManage"/>
/// izohida). Shu sababli O'QISH ham shu ruxsat ostida: bu yerdagi
/// qiymatlar HR ishi emas, tizimning ishlashi haqida va ularni
/// ko'rish ham boshqara olish bilan bir xil doiradagi ish.
/// </para>
/// <para>
/// Ikkita istisno bor va ikkalasi ham bot uchun: <c>/api/settings/bot</c>
/// (<c>recruiting.view</c>) va <c>/api/settings/bot/token</c> (rol
/// bo'yicha). Sabablari o'sha endpoint'lar yonida.
/// </para>
/// </remarks>
public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/settings").WithTags("Settings");

        group.MapGet("/", async (
                GetSettingsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetSettingsQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("GetSettings")
            .WithSummary("Tizim sozlamalari — barchasi")
            .WithDescription(
                "Har bir sozlama uchun kalit («key»), joriy qiymat («value»), turi («kind»: " +
                "Text | Number), sukut qiymat («defaultValue») va chegaralar («min», «max», " +
                "«maxLength») qaytadi. Qiymat HAR DOIM matn — son ham matn bo'lib keladi. " +
                "Hech qachon o'zgartirilmagan sozlama ham ro'yxatda BOR: uning qiymati sukut " +
                "qiymatga teng bo'ladi. Ya'ni javob har doim to'liq va ekran uni to'g'ridan-" +
                "to'g'ri chizishi mumkin — «qaysi sozlamalar bor» degan bilim frontendda " +
                "takrorlanmaydi. Ro'yxat tartibi barqaror va ma'no bo'yicha guruhlangan " +
                "(avval bot, keyin bildirishnomalar), alifbo bo'yicha emas.")
            .Produces<IReadOnlyList<SystemSettingDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/", async (
                UpdateSettingsRequest request,
                UpdateSettingsCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new UpdateSettingsCommand(request.Items), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("UpdateSettings")
            .WithSummary("Tizim sozlamalari — bir nechtasini birdan yozish")
            .WithDescription(
                "«{ items: [{ key, value }] }». Faqat O'ZGARGAN sozlamalarni yuborish kifoya. " +
                "Amal HAMMASI YOKI HECH NARSA: bitta yozuv xato bo'lsa, bironta ham qiymat " +
                "saqlanmaydi — tizim yarim o'zgartirilgan holatda qolmaydi. Xatolar: " +
                "noma'lum kalit «sozlama.notogri_kalit», kalit ko'rsatilmagan " +
                "«sozlama.kalit_bosh», bitta kalit ikki marta «sozlama.takror_kalit», " +
                "bo'sh ro'yxat «sozlama.royxat_bosh», son emas «sozlama.notogri_son», " +
                "chegaradan tashqari «sozlama.chegara», uzun matn «sozlama.matn_uzun» " +
                "(hammasi 400). Matnli sozlamani BO'SHATISH mumkin (bo'sh satr yuboriladi) — " +
                "masalan bot matni o'chirilsa, «Kompaniya haqida» tugmasi umuman chizilmaydi. " +
                "Javobda TO'LIQ ro'yxat qaytadi (GET bilan bir xil shaklda), ya'ni ekranni " +
                "yangilash uchun ikkinchi so'rov kerak emas.")
            .Produces<IReadOnlyList<SystemSettingDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        /*
          ═══════════════════════════════════════════════════════════
           BOT UCHUN ALOHIDA ENDPOINT

           ⭐ Nega umumiy `GET /api/settings` yaramaydi — RUXSAT.

           Bot — odam emas, xizmat hisobi va unda atigi ikkita ruxsat
           bor: `recruiting.view` va `recruiting.manage`
           (`SystemRoles.RecruitingBot`). `settings.manage` esa faqat
           super administratorda va uni botga berish mumkin emas: bot
           tokeni Telegram serverlariga chiqadigan, sizib ketishi
           mumkin bo'lgan yagona sir. Token sizsa, undan foydalangan
           odam eng yomon holatda soxta nomzod yozishi kerak — butun
           tizim sozlamalarini o'zgartira olishi emas.

           Ikkinchi sabab — MAZMUN. Botga bildirishnoma chegaralari
           umuman kerak emas. Bu yerda esa unga faqat uchta «bot.»
           sozlamasi ko'rinadi, qolganlari umuman javobga tushmaydi.

           Uchinchi sabab — SHAKL. Javob tiplashgan
           (`BotSettingsDto`): bot kalitlarni izlab, matnni songa
           o'girib o'tirmaydi. Aks holda `SystemSettingKeys` dagi
           bilim bot kodida ikkinchi marta paydo bo'lardi.
          ═══════════════════════════════════════════════════════════
        */
        group.MapGet("/bot", async (
                GetBotSettingsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetBotSettingsQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("GetBotSettings")
            .WithSummary("Tizim sozlamalari — faqat botga keraklilari")
            .WithDescription(
                "Telegram anketa boti uchun: «Kompaniya haqida» matni (kirill va lotin) " +
                "hamda yarim qolgan suhbat necha daqiqadan keyin unutilishi. Boshqa " +
                "sozlamalar bu javobda YO'Q. Ruxsat «recruiting.view» — bot hisobida " +
                "«settings.manage» ataylab yo'q (izohi kodda). Matnlar bo'sh bo'lishi " +
                "mumkin: bo'sh matnda bot «Kompaniya haqida» tugmasini umuman chizmaydi. " +
                "Lotincha matn bo'sh bo'lsa server uni kirilldagisi bilan ALMASHTIRMAYDI — " +
                "bu qoida botning o'zida, aks holda HR lotin matnini bo'shata olmasdi.")
            .Produces<BotSettingsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        /*
          ═══════════════════════════════════════════════════════════
           TELEGRAM TOKENI — TIZIMDAGI YAGONA ENDPOINT, UNDAN SIR
           OCHIQ HOLDA CHIQADI.

           ⭐ Nega yuqoridagi `/bot` ga qo'shilmadi. U `recruiting.view`
           ostida, bu ruxsat esa HR menejerlarida ham bor. Token o'sha
           javobga tushsa, har bir HR uni o'qiy olardi — holbuki tokenni
           QO'YGAN super administratorning o'zi ham uni qaytarib
           ololmaydi (javobda faqat niqoblangan ko'rinish qaytadi).

           ⭐ Nega ruxsat emas, ROL. Yangi ruxsat kodi
           («settings.botsecrets») super administratorga ham, bosh
           rahbarga ham O'ZI yetib borardi: ikkalasining ro'yxati
           `Permissions.All` dan hisoblanadi. Rol bilan esa doira
           aniq: bu endpoint odam uchun emas, JARAYON uchun. Batafsil
           sabab `AuthenticationExtensions.BotServicePolicy` da.

           Javobda `token: null` bo'lishi — XATO EMAS: sozlama
           qo'yilmagan va bot `.env` dagi zaxiraga tushadi.
          ═══════════════════════════════════════════════════════════
        */
        group.MapGet("/bot/token", async (
                GetBotTokenQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetBotTokenQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(AuthenticationExtensions.BotServicePolicy)
            .WithName("GetBotToken")
            .WithSummary("Telegram tokeni — faqat bot xizmat hisobiga")
            .WithDescription(
                "«{ token }» qaytaradi. Bu tizimdagi YAGONA joy, undan saqlangan sir ochiq " +
                "holda chiqadi va u faqat «recruiting_bot» roli ostidagi hisobga ochiq — " +
                "ruxsat bilan emas, ROL bilan himoyalangan (sababi kodda). Super " +
                "administrator ham, bosh rahbar ham bu endpoint'ni chaqira olmaydi. " +
                "«token: null» — sozlama ekranda qo'yilmagan; bunday holatda bot «.env» " +
                "dagi «TELEGRAM__BOTTOKEN» zaxira qiymatiga tushadi. Bot bu so'rovni " +
                "davriy takrorlaydi: token ekranda almashtirilsa, u konteynerni qayta " +
                "ishga tushirmasdan yangisiga o'tadi.")
            .Produces<BotTokenDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
