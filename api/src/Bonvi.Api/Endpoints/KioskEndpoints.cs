using Bonvi.Api.Common;
using Bonvi.Api.Extensions;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bonvi.Api.Endpoints;

/// <summary>
/// Nomzod anketasi HR-F02 — ochiq onlayn forma (SPEC-planshet-anketa, 2026-10-26 tahriri).
/// </summary>
/// <remarks>
/// <para>
/// <b>Ikki guruh, ikki xil kirish.</b>
/// </para>
/// <list type="number">
///   <item>
///     <c>/api/kiosk/*</c> — forma O'ZI, Google Forms kabi OCHIQ (<c>AllowAnonymous</c>): login ham,
///     kalit ham yo'q. Havolani kim ochsa — ofisdagi planshet yoki nomzodning o'z telefoni —
///     to'ldirib yuboradi. Shu sababli bu yerda faqat ochiq vakansiyalar NOMI o'qiladi va anketa
///     yoziladi; javobda nomzodlar haqida hech narsa yo'q. Himoya — mijoz IP'si bo'yicha tezlik
///     cheklovi, kunlik umumiy chegara, «tuzoq» maydon (SPEC §2) va kalitlar berilgan bo'lsa
///     Cloudflare Turnstile bot tekshiruvi (§2.3.2). Yo'llar birinchi versiyadan qolgan (veb
///     shularni chaqiradi). Alohida <c>anketa.bonvi.uz</c> saytida Caddy faqat shu prefiksni API'ga
///     uzatadi (§2.3.1, <c>web/Caddyfile</c>).
///   </item>
///   <item>
///     <c>/api/recruiting/candidates/{id}/forms</c> — HR nomzod kartasida anketani
///     va uning PDF'ini ko'radi (<c>recruiting.view</c>).
///   </item>
/// </list>
/// </remarks>
public static class KioskEndpoints
{
    /// <summary>Ochiq forma yo'llarining prefiksi.</summary>
    public const string Prefix = "/api/kiosk";

    /// <summary>
    /// Yuborish so'rovining chegarasi: surat (5 MB) + imzo (300 KB) + javoblar + multipart «konverti».
    /// </summary>
    /// <remarks>
    /// SPEC 12 MB: suratning o'z chegarasi
    /// (<see cref="SubmitQuestionnaireFormCommandHandler.MaxPhotoBytes"/>) va qolgan qismlar uchun
    /// zaxira. Forma suratni yuborishdan oldin 1600 px gacha kichraytiradi, ya'ni haqiqiy
    /// so'rov odatda 1 MB dan kam.
    /// </remarks>
    private const long MaxSubmitRequestBytes = 12L * 1024 * 1024;

    /// <summary>
    /// Fayl bo'lmagan bitta maydonning chegarasi (bayt) — <c>answers</c> uchun.
    /// </summary>
    /// <remarks>
    /// Freymvorkning sukut qiymati 4 MiB: shuncha «javob» millionlab qator obyektiga aylanardi
    /// (xavfsizlik ko'rigi M3). Haqiqiy anketa 100 KB dan kichik; handler'dagi belgi chegarasi
    /// (<see cref="SubmitQuestionnaireFormCommandHandler.MaxAnswersLength"/>) ikkinchi qatlam.
    /// </remarks>
    private const int MaxFormValueBytes = 256 * 1024;

    /// <summary>Forma maydonlari soni: yettita (tuzoq maydon va bot tekshiruvi tokeni bilan) + zaxira.</summary>
    private const int MaxFormValueCount = 16;

    public static IEndpointRouteBuilder MapKioskEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        MapPublicForm(app);
        MapForms(app);

        return app;
    }

    // ════════════════════════════════════════════════════════════
    //  Ochiq forma — login va kalitsiz
    // ════════════════════════════════════════════════════════════

    private static void MapPublicForm(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Prefix)
            .WithTags("Kiosk")

            // Ochiq — umumiy «faqat kirganlar» siyosati (FallbackPolicy) bu ikki yo'lga tegmaydi
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Questionnaire)

            // Audit: bajaruvchi «Onlayn anketa» (Anonymous), IP — haqiqiy mijoz (ClientIp)
            .WithMetadata(OnlineFormEndpointMetadata.Instance);

        group.MapGet("/questionnaire/session", async (
                GetKioskSessionQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(new GetKioskSessionQuery(), cancellationToken).ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("GetKioskSession")
            .WithSummary("Onlayn anketa — seans (vakansiyalar)")
            .WithDescription(
                "OCHIQ — login va kalitsiz. Forma ochilganda va har 10 daqiqada chaqiriladi: «{ today, " +
                "vacancies: [{ id, title }], generalPoolAvailable, referralChannels: [{ id, name, asksDetail }] }». " +
                "«vacancies» — nomzod qabul " +
                "qilayotgan vakansiyalar NOMI bo'yicha, «Umumiy anketa» siz; «generalPoolAvailable» — " +
                "«Umumiy (istalgan lavozim)» tanlovi ko'rsatilsinmi; «referralChannels» — «Qayerdan bilgan» " +
                "ma'lumotnomasining FAOL kanallari, alifbo tartibida (javobdagi «referralChannelId» shulardan " +
                "biri bo'lishi shart; «asksDetail» bo'lsa «referralDetail» majburiy). «today» — Toshkent sanasi " +
                "(tug'ilgan sana va ishga chiqish sanasi chegaralari shundan). «turnstileSiteKey» — bot " +
                "tekshiruvi (Cloudflare Turnstile) vidjetining OCHIQ kaliti: bo'lsa forma oxirgi ekranda " +
                "vidjetni chizadi va yuborishda «turnstileToken» qismini yuboradi; null — tekshiruv o'chiq. " +
                "Javobda nomzodlar haqida HECH NARSA yo'q. Tezlik: bitta mijoz IP'siga daqiqada 60 — " +
                "«rate_limit.oshib_ketdi» (429).")
            .Produces<KioskSessionDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/questionnaire", async (
                [FromForm] string? answers,
                [FromForm] string? clientSubmissionId,
                [FromForm] string? language,
                [FromForm] string? website,
                [FromForm] string? turnstileToken,
                IFormFile? photo,
                IFormFile? signature,
                HttpContext http,
                SubmitQuestionnaireFormCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                await using var photoStream = photo?.OpenReadStream();
                await using var signatureStream = signature?.OpenReadStream();

                var natija = await handler
                    .HandleAsync(
                        new SubmitQuestionnaireFormCommand(
                            answers,
                            clientSubmissionId,
                            language,
                            photoStream,
                            photo?.ContentType,
                            photo?.Length ?? 0,
                            signatureStream,
                            signature?.Length ?? 0,
                            website,
                            turnstileToken,
                            ClientIp.Resolve(http)?.ToString()),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (natija.IsFailure)
                {
                    return natija.ToHttpResult();
                }

                // Yangi anketa — 201; takroriy yuborish (javob yo'qolgan edi) — 200, AYNAN o'sha raqam
                return natija.Value.Created
                    ? Results.Created((string?)null, natija.Value.Result)
                    : Results.Ok(natija.Value.Result);
            })

            // CSRF himoyasi ortiqcha — bu yo'lda cookie ham, sessiya ham yo'q (ochiq forma)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxSubmitRequestBytes))
            .WithFormOptions(
                valueCountLimit: MaxFormValueCount,
                valueLengthLimit: MaxFormValueBytes,
                multipartBodyLengthLimit: MaxSubmitRequestBytes)

            // Guruhning 60/daq o'rniga — o'z tor oynasi (RateLimitPolicies.QuestionnaireSubmit izohi)
            // va ustiga barcha IP'lar uchun kunlik chegara
            .RequireRateLimiting(RateLimitPolicies.QuestionnaireSubmit)
            .RequireQuestionnaireDailyCap()
            .WithName("SubmitKioskQuestionnaire")
            .WithSummary("Onlayn anketa — yuborish")
            .WithDescription(
                "OCHIQ — login va kalitsiz. «multipart/form-data»: «answers» (JSON matni — HR-F02 " +
                "javoblari, SPEC §3), «clientSubmissionId» (uuid — forma anketa boshlanganda yasaydi), " +
                "«language» (uz-Latn | uz-Cyrl | ru), «photo» (JPEG/PNG selfi, 5 MB gacha), «signature» " +
                "(PNG imzo, 300 KB va 1200×600 px gacha — server uni oq fonga qayta chizadi), «website» " +
                "(«tuzoq» maydon — odamga ko'rinmaydi, BO'SH bo'lishi shart: to'ldirilgan bo'lsa " +
                "«anketa.belgi» (400) va hech narsa yozilmaydi), «turnstileToken» (bot tekshiruvi tokeni — " +
                "faqat seansda «turnstileSiteKey» bo'lsa majburiy; yo'q, yaroqsiz, muddati o'tgan yoki " +
                "Cloudflare javob bermasa — «anketa.bot_tekshiruvi» (400, «field: turnstileToken») va hech " +
                "narsa yozilmaydi; allaqachon qabul qilingan «clientSubmissionId» qayta kelsa token " +
                "so'ralmaydi). «answers» 256 KB gacha. Avval vakansiya " +
                "tekshiriladi (javob telefon mosligiga bog'liq emas), keyin telefoni VA familiya-ismi bir " +
                "xil JARAYONDAGI nomzod bo'lsa anketani unga yozadi (faqat bo'sh maydonlarini to'ldiradi), " +
                "aks holda yangi nomzod ochadi (manba «OnlineForm», birinchi bosqich); bot anketasi " +
                "bo'lmasa uni ham to'ldiradi; anketani qog'oz blanka ko'rinishida PDF qilib nomzod " +
                "fayllariga qo'shadi (fayllar chegarasi to'lgan bo'lsa anketa baribir saqlanadi). Javob: " +
                "«{ formNumber, submittedAt }» — nomzod yozuvi haqida hech narsa qaytmaydi. Yangi — 201; " +
                "xuddi shu «clientSubmissionId» qayta kelsa (javob yo'qolgan edi) — 200 va o'sha javob, " +
                "hech narsa yozilmaydi. Tezlik: bitta mijoz IP'siga soatiga 20 ta, barcha IP'larga " +
                "kuniga 500 ta qabul qilingan anketa — «rate_limit.oshib_ketdi» (429). " +
                "Xatolar (400, tafsilotda «field» — maydon yo'li, masalan «familyMembers[0].fullName»): " +
                "«anketa.json», «anketa.majburiy», «anketa.notogri», «anketa.uzun», «anketa.belgi», " +
                "«anketa.bot_tekshiruvi», " +
                "«anketa.rozilik», «anketa.imzo_ism», «anketa.rasm_yoq», «anketa.rasm_yaroqsiz», " +
                "«anketa.imzo_yoq», «anketa.imzo_yaroqsiz», «telefon.*». Vakansiya yopilib qolgan bo'lsa: «vakansiya.topilmadi» " +
                "(404), «vakansiya.qabul_qilmaydi» / «nomzod.umumiy_anketa_yoq» (409) — «field: vacancyId».")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<QuestionnaireSubmittedDto>(StatusCodes.Status201Created)
            .Produces<QuestionnaireSubmittedDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    // ════════════════════════════════════════════════════════════
    //  HR — nomzod kartasidagi anketalar
    // ════════════════════════════════════════════════════════════

    private static void MapForms(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/recruiting/candidates").WithTags("Recruiting");

        group.MapGet("/{id:guid}/forms", async (
                Guid id,
                ListCandidateFormsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(new ListCandidateFormsQuery(id), cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("ListCandidateForms")
            .WithSummary("Nomzod — onlayn anketalari")
            .WithDescription(
                "Yangisidan eskisiga: «{ id, number, formCode, formVersion, submittedAt, " +
                "language, fileId }». «fileId» — nomzod fayllaridagi PDF; bo'sh bo'lsa (chegara to'lgan " +
                "yoki fayl o'chirilgan) PDF «…/forms/{formId}/pdf» dan olinadi. Javoblarning o'zi bu " +
                "yerda YO'Q (nozik ma'lumot) — ular faqat PDF'da. Xato: «nomzod.topilmadi» (404).")
            .Produces<IReadOnlyList<CandidateFormDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/forms/{formId:guid}/pdf", async (
                Guid id,
                Guid formId,
                GetCandidateFormPdfQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetCandidateFormPdfQuery(id, formId), cancellationToken)
                    .ConfigureAwait(false);

                // Bayt qaytadi — JSON faqat xato yo'lida
                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.File(natija.Value.Content, "application/pdf", natija.Value.FileName);
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("DownloadCandidateFormPdf")
            .WithSummary("Nomzod — onlayn anketa PDF")
            .WithDescription(
                "Qog'oz blanka HR-F02 ko'rinishidagi PDF — saqlangan javoblar, surat va imzodan HAR " +
                "SAFAR qayta chiziladi (biriktirilgan fayl o'chirilgan bo'lsa ham ishlaydi). Anketa " +
                "AYNAN shu nomzodniki bo'lishi shart: boshqa juftlik — «anketa.topilmadi» (404). " +
                "Kesh sarlavhalari yo'q — hujjat shaxsiy ma'lumot.")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
