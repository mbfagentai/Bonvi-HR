using System.Security.Claims;
using Bonvi.Api.Common;
using Bonvi.Api.Extensions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.PublicApi.Abstractions;
using Bonvi.Application.PublicApi.Dtos;
using Bonvi.Application.PublicApi.Queries;
using Bonvi.Domain.Abstractions;

namespace Bonvi.Api.Endpoints;

/// <summary>
/// Tashqi API — holdingning boshqa loyihalari uchun <b>FAQAT O'QISH</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Bu guruhda faqat <c>MapGet</c> bor.</b> <c>POST</c>, <c>PUT</c>,
/// <c>PATCH</c> va <c>DELETE</c> o'chirilgan emas, to'silgan emas — ular
/// hech qachon MAP QILINMAYDI (SPEC §1). Trafik bir tomonli: iste'molchi
/// yuborgan hech narsa bu yerda hech narsani o'zgartira olmaydi.
/// Yangi yozish endpoint'i qo'shishga ehtiyoj tug'ilsa, u bu faylda
/// EMAS, ichki guruhda paydo bo'lishi kerak.
/// </para>
/// <para>
/// <b>Himoya:</b> <see cref="ApiKeyAuthenticationExtensions.PublicApiPolicy"/>
/// — ya'ni FAQAT <c>X-API-Key</c>. Foydalanuvchi JWT'si bu yerda umuman
/// o'qilmaydi (sababi <c>ApiKeyAuthenticationHandler</c> izohida), kalit esa
/// ichki endpoint'larga yetib bormaydi. Ikkala yo'nalish ham test bilan
/// qo'riqlanadi.
/// </para>
/// <para>
/// <b>Maydonlar chegarasi</b> — <c>PublicEmployeeDto</c> izohida. Unga
/// maydon qo'shish SPEC §1 ni o'zgartirish demak va buni «maydon sizib
/// chiqishi» testi to'sadi.
/// </para>
/// <para>
/// <b>OpenAPI</b> — o'z hujjatida (<see cref="DocumentName"/>): iste'molchi
/// faqat o'zi chaqira oladigan narsani ko'rishi kerak, ichki 150 ta
/// endpoint'ni emas.
/// </para>
/// </remarks>
public static class PublicApiEndpoints
{
    /// <summary>Ommaviy yuzaning yo'l prefiksi.</summary>
    /// <remarks>
    /// Versiya yo'lda (<c>/v1</c>) va bu ataylab: iste'molchilar tashqi
    /// loyihalar, ya'ni ularni bir kunda birga yangilab bo'lmaydi. Ikkinchi
    /// versiya kerak bo'lganda u YONMA-YON paydo bo'ladi va <c>v1</c>
    /// ishlashda davom etadi.
    /// </remarks>
    public const string Prefix = "/api/public/v1";

    /// <summary>Ommaviy OpenAPI hujjatining nomi — <c>/openapi/public.json</c>.</summary>
    public const string DocumentName = "public";

    public static IEndpointRouteBuilder MapPublicApiEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Prefix)
            .WithTags("Public")

            // Guruh nomi HUJJATNI tanlaydi: `ShouldInclude` shu qiymatga
            // qarab endpoint'ni ommaviy hujjatga qo'yadi va ichki
            // hujjatdan chiqarib tashlaydi (`Program.cs`).
            .WithGroupName(DocumentName)
            .RequireAuthorization(ApiKeyAuthenticationExtensions.PublicApiPolicy)
            .RequireRateLimiting(RateLimitPolicies.PublicApi);

        MapPing(group);
        MapEmployees(group);
        MapReference(group);

        return app;
    }

    // ════════════════════════════════════════════════════════════
    //  Integratsiyani tekshirish
    // ════════════════════════════════════════════════════════════

    private static void MapPing(IEndpointRouteBuilder group)
    {
        group.MapGet("/ping", (ClaimsPrincipal caller, IClock clock) =>
                Results.Ok(new PublicPingDto(
                    Ok: true,
                    Key: caller.FindFirstValue(ApiKeyClaims.KeyName) ?? string.Empty,
                    ServerTime: clock.UtcNow)))
            .WithName("PublicPing")
            .WithSummary("Integratsiyani tekshirish")
            .WithDescription(
                "«{ ok: true, key, serverTime }». «key» — kalitning NOMI (prefiksi emas): " +
                "iste'molchi to'g'ri kalit bilan kelganini shundan bilib oladi. " +
                "«serverTime» — UTC. Kalit yaroqsiz bo'lsa: «tashqi.kalit_notogri» (401).")
            .Produces<PublicPingDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    // ════════════════════════════════════════════════════════════
    //  Xodimlar
    // ════════════════════════════════════════════════════════════

    private static void MapEmployees(IEndpointRouteBuilder group)
    {
        /*
          ⚠️ SO'ROV PARAMETRLARI HAMMASI NULLABLE va sukut qiymatsiz.

          Bu shakliy talab emas: 2026-08-29 da aniqlangan tuzoq (SPEC §8)
          — parametr sifatida turgan rekordda `Guid = default` ko'rinishidagi
          sukut qiymat BUTUN OpenAPI hujjatini 500 bilan yiqitadi va
          `./bonvi gen` ishlamay qoladi. Shu sababli bu yerda rekord emas,
          alohida nullable parametrlar ishlatiladi va ularning sukut
          qiymatlari `null`.
        */
        group.MapGet("/employees", async (
                string? search,
                Guid? legalEntityId,
                Guid? departmentId,
                Guid? positionId,
                Guid? directionId,
                string? status,
                DateTimeOffset? updatedSince,
                int? page,
                int? pageSize,
                ListPublicEmployeesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ListPublicEmployeesQuery(
                            search,
                            legalEntityId,
                            departmentId,
                            positionId,
                            directionId,
                            status,
                            updatedSince,
                            page,
                            pageSize),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("PublicListEmployees")
            .WithSummary("Xodimlar — sahifalangan ro'yxat")
            .WithDescription(
                "Filtr, saralash va sahifalash SERVERDA. «page» 1 dan; «pageSize» sukut 50, " +
                "eng ko'pi 200 (kattasi jimgina 200 ga KESILADI, xato qaytmaydi). " +
                "«status»: Probation | Active | Terminated; noto'g'ri qiymat " +
                "«tashqi.holat_notogri» (400). «departmentId» ICHKI bo'limlari bilan birga " +
                "qidiradi. «search» — F.I.Sh., JSHSHIR, telefon yoki pasport bo'yicha " +
                "(qidirish mumkin, lekin JSHSHIR va pasport JAVOBDA YO'Q). " +
                "«updatedSince» — UTC lahza: shundan keyin o'zgargan yozuvlar qaytadi va " +
                "tartib «updatedAt» bo'yicha O'SISH tartibiga o'tadi, ya'ni sinxronlashni " +
                "uzilgan joydan davom ettirish mumkin. «updatedAt» xodim kartasi va uning " +
                "mehnat munosabatlaridan ENG KEYINGISI — lavozim o'zgarishi ham sezib olinadi. " +
                "O'CHIRILGAN xodimlar bu ro'yxatda HECH QACHON bo'lmaydi; ular uchun " +
                "«/employees/deletions» bor. Oxiridan keyingi sahifa — bo'sh ro'yxat, xato emas.")
            .Produces<PagedResult<PublicEmployeeDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        /*
          «deletions» marshruti «{id}» dan OLDIN e'lon qilinadi. Bu yerda
          to'qnashuv YO'Q (`{id:guid}` cheklovi «deletions» ni qabul
          qilmaydi), lekin tartib ataylab: o'quvchi uchun «bu qat'iy
          segment, o'zgaruvchi emas» degani birinchi ko'rinib turadi.
        */
        group.MapGet("/employees/deletions", async (
                DateTimeOffset? since,
                ListPublicDeletionsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListPublicDeletionsQuery(since), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("PublicListDeletions")
            .WithSummary("O'chirilgan xodimlar")
            .WithDescription(
                "«[{ id, deletedAt }]» — iste'molchi o'z nusxasidan olib tashlashi uchun. " +
                "O'chirilgan xodim haqida boshqa HECH NARSA qaytmaydi. «since» (UTC) " +
                "berilsa, faqat shundan keyin o'chirilganlar; tartib «deletedAt» bo'yicha " +
                "o'sish tartibida, ya'ni oxirgi ko'rilgan qiymatni keyingi so'rovga berish " +
                "mumkin. Javob SAHIFALANMAYDI, lekin bitta so'rovda eng ko'pi 5000 yozuv " +
                "keladi — ro'yxat kichik bo'lib turishi uchun «since» ishlatiladi.")
            .Produces<IReadOnlyList<PublicDeletionDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/employees/{id:guid}", async (
                Guid id,
                GetPublicEmployeeQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetPublicEmployeeQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("PublicGetEmployee")
            .WithSummary("Xodim — bitta yozuv")
            .WithDescription(
                "Ro'yxatdagi bilan AYNAN bir xil shakl. O'chirilgan yoki mavjud bo'lmagan " +
                "xodim: «tashqi.xodim_topilmadi» (404) — ikkalasi ham bir xil javob oladi. " +
                "«employments» — YAKUNLANMAGAN munosabatlar, yangisidan eskisiga; ishdan " +
                "ketgan xodimda u BO'SH bo'ladi va yuqori darajadagi «status», «hireDate», " +
                "«terminationDate» javob beradi (ular ASOSIY munosabatdan olinadi).")
            .Produces<PublicEmployeeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/employees/{id:guid}/photo", async (
                Guid id,
                HttpContext context,
                GetPublicEmployeePhotoQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetPublicEmployeePhotoQuery(id, PhotoHttp.ReadKnownVersion(context.Request)),
                        cancellationToken)
                    .ConfigureAwait(false);

                // JSON emas, BAYT qaytaradi — `ToHttpResult()` faqat xato
                // yo'lida (ichki rasm endpoint'i bilan bir xil sabab).
                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : PhotoHttp.Write(context, natija.Value);
            })
            .WithName("PublicGetEmployeePhoto")
            .WithSummary("Xodim — rasmi")
            .WithDescription(
                "Rasm baytlari (image/jpeg), ichki marshrut bilan AYNAN bir xil quvurdan. " +
                "«ETag» — saqlash kalitidan olingan barqaror xesh; «If-None-Match» mos " +
                "kelsa 304. Kesh SHAXSIY: «Cache-Control: private, max-age=86400» — surat " +
                "shaxsiy ma'lumot va umumiy proksida saqlanmasligi kerak. Rasm yo'q bo'lsa " +
                "«tashqi.surat_yoq» (404); xodim umuman mavjud bo'lmasa ham AYNI shu javob " +
                "keladi (identifikatorlarni tanlab ko'rishga arzon tekshirgich bo'lmasligi uchun).")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    // ════════════════════════════════════════════════════════════
    //  Ma'lumotnomalar
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// To'rtta ma'lumotnoma — bir xil shaklda.
    /// </summary>
    /// <remarks>
    /// Marshrutlar ALOHIDA (<c>?kind=</c> parametri bilan bitta marshrut
    /// emas): shundagina OpenAPI hujjatida iste'molchi qaysi to'rtta
    /// ro'yxat borligini KO'RADI. Parametrli variantda u «kind» ga qanday
    /// qiymat berish mumkinligini hujjat matnidan o'qib olishi kerak
    /// bo'lardi.
    /// </remarks>
    private static void MapReference(IEndpointRouteBuilder group)
    {
        MapReference(group, "legal-entities", PublicReferenceKind.LegalEntities, "Yuridik shaxslar");
        MapReference(group, "departments", PublicReferenceKind.Departments, "Bo'limlar");
        MapReference(group, "positions", PublicReferenceKind.Positions, "Lavozimlar");
        MapReference(group, "directions", PublicReferenceKind.Directions, "Yo'nalishlar");
    }

    private static void MapReference(
        IEndpointRouteBuilder group,
        string route,
        PublicReferenceKind kind,
        string title)
    {
        group.MapGet($"/reference/{route}", async (
                ListPublicReferenceQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListPublicReferenceQuery(kind), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName($"PublicListReference{kind}")
            .WithSummary($"Ma'lumotnoma — {title}")
            .WithDescription(
                "«[{ id, name, parentId }]», alifbo tartibida. «parentId» faqat BO'LIMLARDA " +
                "to'ladi (ular daraxt), qolganlarida har doim null. Faolsizlantirilgan " +
                "yozuvlar ham qaytadi: xodimning munosabatida tarixan o'sha bo'lim turgan " +
                "bo'lishi mumkin va usiz javobdagi identifikator nomsiz qolardi.")
            .Produces<IReadOnlyList<PublicReferenceDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
