using Bonvi.Api.Extensions;
using Bonvi.Application.PublicApi.Dtos;
using Bonvi.Application.PublicApi.Keys;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>Yangi kalit so'rovi tanasi.</summary>
/// <param name="Name">
/// Kalit qaysi loyihaga beriladi («BonviCall»). Bo'sh bo'lmasligi va 120
/// belgidan oshmasligi kerak.
/// </param>
public sealed record CreateApiKeyRequest(string? Name);

/// <summary>
/// Tashqi API kalitlarini boshqarish — <b>ICHKI</b> endpoint'lar.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ Bu guruh ommaviy yuzaning bir qismi EMAS. U oddiy JWT bilan va
/// <see cref="Permissions.SettingsManage"/> ostida ishlaydi — ya'ni
/// faqat super administrator. Kalit bilan bu endpoint'larga kirib
/// bo'lmaydi: ular sxemani ko'rsatmaydi va shu sababli standart
/// (<c>Bearer</c>) sxemasidan o'tadi.
/// </para>
/// <para>
/// <b>Nega <c>settings.manage</c>.</b> Kalit berish — tizim sozlamasi,
/// HR ishi emas: u holdingning boshqa loyihasiga xodimlar ma'lumotini
/// ochadi. Bu ruxsat esa ataylab faqat bitta odamda
/// (sababi <see cref="Permissions.SettingsManage"/> izohida) va bunday
/// qarorga tegadigan odam bitta bo'lgani ma'qul.
/// </para>
/// <para>
/// <b>Marshrutlar <c>/api/api-keys</c> ostida, <c>/api/settings</c>
/// ichida emas.</b> Sozlamalar — kalit-qiymat jadvali va uning o'z
/// shakli bor (<c>SystemSettingDto</c>); kalitlar esa YOZUVLAR ro'yxati
/// va ular hech qanday sozlama kaliti bilan ifodalanmaydi. Ekranda
/// ikkalasi bir bo'limda ko'rinadi, API'da esa alohida.
/// </para>
/// </remarks>
public static class ApiKeyEndpoints
{
    public static IEndpointRouteBuilder MapApiKeyEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/api-keys").WithTags("ApiKeys");

        group.MapGet("/", async (
                ListApiKeysQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListApiKeysQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("ListApiKeys")
            .WithSummary("Tashqi API kalitlari — ro'yxat")
            .WithDescription(
                "Har bir kalit uchun: nomi, OCHIQ prefiksi («prefix»), yaratilgan vaqti, oxirgi " +
                "ishlatilgan vaqti va IP'si, bekor qilingan vaqti. Kalitning O'ZI bu javobda " +
                "YO'Q va boshqa hech qaysi javobda ham yo'q — u faqat yaratilgan paytda bir " +
                "marta ko'rinadi. «lastUsedAt» aniqligi DAQIQA: yozuv har so'rovda emas, " +
                "daqiqada bir marta yangilanadi (sabab kodda). Tartib: avval kuchdagilar, " +
                "keyin bekor qilinganlar; ichida yangisidan eskisiga.")
            .Produces<IReadOnlyList<ApiKeyDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/", async (
                CreateApiKeyRequest request,
                CreateApiKeyCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new CreateApiKeyCommand(request.Name), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("CreateApiKey")
            .WithSummary("Tashqi API kaliti — yangisini yaratish")
            .WithDescription(
                "«{ name }» → «{ id, name, prefix, key }». ⚠️ «key» — TO'LIQ kalit va u SHU " +
                "JAVOBDA bir marta qaytadi: bazada faqat uning SHA-256 xeshi saqlanadi va " +
                "kalitni qaytarib olishning yo'li yo'q. Nusxa olinmasa, yagona chora — bekor " +
                "qilib, yangisini yaratish. Format: «bonvi_<prefiks8>_<sir32>». Xatolar: " +
                "nom bo'sh «tashqi.kalit_nomi_bosh» (400), nom uzun «tashqi.kalit_nomi_uzun» " +
                "(400), shu nomdagi kuchdagi kalit bor «tashqi.kalit_nomi_band» (409). " +
                "Bekor qilingan kalitning nomini QAYTA ishlatish mumkin.")
            .Produces<ApiKeyCreatedDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/revoke", async (
                Guid id,
                RevokeApiKeyCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new RevokeApiKeyCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("RevokeApiKey")
            .WithSummary("Tashqi API kaliti — bekor qilish")
            .WithDescription(
                "Kalit DARHOL ishlamay qoladi (kesh yo'q — tekshiruv har safar bazadan " +
                "o'qiydi). Yozuv O'CHIRILMAYDI: u ro'yxatda kulrang bo'lib qoladi, chunki " +
                "«bu kalit kimga berilgan edi va qachon o'chirilgan» degan savol xavfsizlik " +
                "tekshiruvida birinchi so'raladi. Amal IDEMPOTENT: allaqachon bekor " +
                "qilingan kalitda ham 200 qaytadi. Javobda YANGILANGAN qator keladi. " +
                "Noma'lum kalit: «tashqi.kalit_topilmadi» (404).")
            .Produces<ApiKeyDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
