using Bonvi.Api.Extensions;
using Bonvi.Application.Common.Documents;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Recruiting.Candidates;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Recruiting;
using Microsoft.AspNetCore.Mvc;

namespace Bonvi.Api.Endpoints;

/// <summary>Nomzodni voronkaga kiritish so'rovi tanasi.</summary>
/// <param name="VacancyId">Qaysi vakansiyaga. Majburiy.</param>
/// <param name="LastName">Familiya. Majburiy.</param>
/// <param name="FirstName">Ism. Majburiy.</param>
/// <param name="MiddleName">Otasining ismi.</param>
/// <param name="BirthDate">Tug'ilgan sana.</param>
/// <param name="Phone">Telefon (+998… yoki 9 xonali).</param>
/// <param name="Address">Manzil.</param>
/// <param name="Speciality">Mutaxassisligi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="StageId">Boshlang'ich bosqich. Bo'sh — voronkaning birinchi faol bosqichi.</param>
/// <param name="Source">Manual (sukut) yoki TelegramBot.</param>
/// <param name="TelegramChatId">Telegram suhbatining raqami — faqat bot to'ldiradi.</param>
/// <param name="TelegramLanguage">Nomzod botda tanlagan til.</param>
public sealed record RegisterCandidateRequest(
    Guid VacancyId,
    string? LastName,
    string? FirstName,
    string? MiddleName = null,
    DateOnly? BirthDate = null,
    string? Phone = null,
    string? Address = null,
    string? Speciality = null,
    string? Note = null,
    Guid? StageId = null,
    CandidateSource Source = CandidateSource.Manual,

    /*
      Telegram suhbatining raqami va nomzod tanlagan til — FAQAT bot
      to'ldiradi. Ular rad etish xabarini yuborish uchun kerak
      (`Candidate.TelegramChatId` izohi): busiz bot nomzodni topa
      olmaydi, telefon raqami esa Telegram'da manzil emas.
    */
    long? TelegramChatId = null,
    string? TelegramLanguage = null);

/// <summary>Nomzod ma'lumotini tuzatish so'rovi tanasi — vakansiya va manba o'zgarmaydi.</summary>
public sealed record UpdateCandidateRequest(
    string? LastName,
    string? FirstName,
    string? MiddleName = null,
    DateOnly? BirthDate = null,
    string? Phone = null,
    string? Address = null,
    string? Speciality = null,
    string? Note = null);

/// <summary>Nomzodni boshqa vakansiyaga o'tkazish so'rovi tanasi.</summary>
public sealed record ReassignCandidateRequest(Guid VacancyId);

/// <summary>Bosqich natijasini yozish so'rovi tanasi.</summary>
/// <param name="StageId">Bosqich. Majburiy.</param>
/// <param name="Outcome">Pending (sukut) | Passed | Failed.</param>
/// <param name="Date">Bosqich sanasi. Bo'sh — bugun.</param>
/// <param name="Conclusion">Xulosa matni — kengash xulosasi ham shu maydon.</param>
public sealed record RecordCandidateStageRequest(
    Guid StageId,
    StageOutcome Outcome = StageOutcome.Pending,
    DateOnly? Date = null,
    string? Conclusion = null);

/// <summary>Nomzodni voronkadan chiqarish so'rovi tanasi.</summary>
public sealed record CloseCandidateRequest(DateOnly? At = null, string? Reason = null);

/// <summary>
/// Nomzodlar va voronka — rekrutingning asosiy ekrani (2-bosqich).
/// </summary>
/// <remarks>
/// <para>
/// <b>Voronka taxtasi shu endpoint bilan chiziladi</b>, alohida
/// «/pipeline» endpointi YO'Q: har bir ustun
/// <c>?status=InProgress&amp;stageId=…</c> so'rovi bilan olinadi. Alohida
/// jamlovchi endpoint qo'shilsa, u ro'yxat bilan bir xil filtrlarni
/// ikkinchi marta amalga oshirishi kerak bo'lardi va ikkalasi vaqt o'tib
/// bir-biridan uzoqlashardi.
/// </para>
/// <para>
/// «Ishga olindi» deb belgilash amali ATAYLAB yo'q: nomzod faqat taklif
/// qabul qilinganda va HAQIQIY xodim kartasi yaratilganda yopiladi
/// (<c>POST /api/recruiting/offers/{id}/accept</c>).
/// </para>
/// </remarks>
public static class CandidateEndpoints
{
    /// <summary>
    /// So'rov chegarasi fayl chegarasidan sal katta: multipart konvertida
    /// forma maydonlari ham joy egallaydi (xodim hujjatlaridagi bilan bir xil).
    /// </summary>
    private const long MaxResumeRequestBytes = DocumentLimits.MaxBytes + (64 * 1024);

    public static IEndpointRouteBuilder MapCandidateEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/recruiting/candidates").WithTags("Recruiting");

        MapReads(group);
        MapWrites(group);
        MapFunnel(group);
        MapResume(group);

        return app;
    }

    private static void MapReads(IEndpointRouteBuilder group)
    {
        group.MapGet("/", async (
                string? search,
                Guid? vacancyId,
                Guid? stageId,
                string? status,
                string? source,
                int? page,
                int? pageSize,
                string? sort,
                SearchCandidatesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new SearchCandidatesQuery(
                            search, vacancyId, stageId, status, source, page, pageSize, sort),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("SearchCandidates")
            .WithSummary("Nomzodlar — ro'yxat va voronka")
            .WithDescription(
                "SERVER tomonda sahifalanadi, filtrlanadi va saralanadi. " +
                "«search» — F.I.Sh. yoki TELEFON bo'yicha (nomzodda JSHSHIR va pasport " +
                "bo'lmaydi); qidiruv xodimlar ro'yxatidagi kabi kirill imlo variantlarini " +
                "(қ/ғ/ҳ/ў/ё) va telefon ajratgichlarini hisobga oladi. " +
                "«status» — InProgress | Rejected | Hired | Withdrawn, «source» — " +
                "Manual | TelegramBot (noma'lum qiymat 400: «nomzod.holat_notogri» / " +
                "«nomzod.manba_notogri»). «sort» — createdAt (sukut, yangidan eskiga), " +
                "name, stage. Voronka ustuni: «?status=InProgress&stageId=…».")
            .Produces<PagedResult<CandidateListItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                GetCandidateQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetCandidateQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("GetCandidate")
            .WithSummary("Nomzod — karta")
            .WithDescription(
                "Bosqichlar TARIXI («stages», yozilgan tartibda) va barcha ish " +
                "takliflari («offers», yangidan eskiga) bilan. Ishga olingan bo'lsa — " +
                "«hiredEmployeeId» va «hiredEmploymentId» to'ldiriladi.")
            .Produces<CandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapWrites(IEndpointRouteBuilder group)
    {
        group.MapPost("/", async (
                RegisterCandidateRequest request,
                RegisterCandidateCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new RegisterCandidateCommand(
                            request.VacancyId,
                            request.LastName,
                            request.FirstName,
                            request.MiddleName,
                            request.BirthDate,
                            request.Phone,
                            request.Address,
                            request.Speciality,
                            request.Note,
                            request.StageId,
                            request.Source,
                            request.TelegramChatId,
                            request.TelegramLanguage),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("RegisterCandidate")
            .WithSummary("Nomzod — voronkaga kiritish")
            .WithDescription(
                "Nomzod DARHOL birinchi bosqichga («Анкета») «kutilmoqda» natijasi " +
                "bilan qo'yiladi — «stageId» berilsa, o'sha bosqichga. Vakansiya OCHIQ " +
                "bo'lishi shart («vakansiya.qabul_qilmaydi», 409). Dublikat tekshiruvi " +
                "YO'Q: nomzodda JSHSHIR kabi ishonchli kalit bo'lmaydi. " +
                "Xatolar: «bosqich.faol_emas» (409), «bosqich.faol_yoq» (409), " +
                "«ism.familiya_bosh» / «ism.ism_bosh» / «telefon.*» (400).")
            .Produces<CandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateCandidateRequest request,
                UpdateCandidateCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new UpdateCandidateCommand(
                            id,
                            request.LastName,
                            request.FirstName,
                            request.MiddleName,
                            request.BirthDate,
                            request.Phone,
                            request.Address,
                            request.Speciality,
                            request.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("UpdateCandidate")
            .WithSummary("Nomzod — ma'lumotini tuzatish")
            .WithDescription(
                "Vakansiya va MANBA bu yerdan o'zgarmaydi: vakansiya uchun " +
                "«/reassign» bor, manba esa tarixiy fakt (bot samaradorligi " +
                "hisoboti unga tayanadi).")
            .Produces<CandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/reassign", async (
                Guid id,
                ReassignCandidateRequest request,
                ReassignCandidateCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ReassignCandidateCommand(id, request.VacancyId), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("ReassignCandidate")
            .WithSummary("Nomzod — boshqa vakansiyaga o'tkazish")
            .WithDescription(
                "Bosqichlar tarixi SAQLANADI va joriy bosqich o'zgarmaydi. " +
                "Yangi vakansiya ochiq bo'lishi shart, nomzod esa jarayonda " +
                "(«nomzod.jarayonda_emas», 409).")
            .Produces<CandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                DeleteCandidateCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeleteCandidateCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("DeleteCandidate")
            .WithSummary("Nomzod — yozuvni o'chirish")
            .WithDescription(
                "Yumshoq o'chirish: yozuv bazada qoladi. Rad etilgan nomzod " +
                "O'CHIRILMAYDI — u kelasi izlanishning bazasi; o'chirish faqat " +
                "xato kiritilgan yozuv uchun.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapFunnel(IEndpointRouteBuilder group)
    {
        group.MapPost("/{id:guid}/stages", async (
                Guid id,
                RecordCandidateStageRequest request,
                RecordCandidateStageCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new RecordCandidateStageCommand(
                            id, request.StageId, request.Outcome, request.Date, request.Conclusion),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("RecordCandidateStage")
            .WithSummary("Nomzod — bosqich natijasini yozish")
            .WithDescription(
                "Voronkaning YAGONA harakati: telefon suhbat natijasi ham, KENGASH " +
                "XULOSASI ham, rahbar suhbati ham shu endpoint orqali yoziladi " +
                "(«conclusion» — xulosa matni). Ikki ish uslubi qo'llab-quvvatlanadi: " +
                "«outcome=Pending» bilan bosqichni ochib, keyin AYNI bosqichga natija " +
                "yozilsa — o'sha yozuv TO'LDIRILADI, yangisi qo'shilmaydi. " +
                "Voronka oldinga yurishga majburlamaydi: bosqichni o'tkazib yuborish " +
                "va orqaga qaytish mumkin. Xatolar: «bosqich.topilmadi» (404), " +
                "«bosqich.faol_emas» (409), «nomzod.jarayonda_emas» (409). " +
                "Javob — YANGILANGAN nomzod kartasi (butun tarixi bilan).")
            .Produces<CandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        /*
          ═══════════════════════════════════════════════════════════
           RAD ETISH XABARINING NAVBATI — FAQAT BOT XIZMAT HISOBIGA.

           ⭐ Nega bu ikkita endpoint umuman bor. Bot alohida jarayon va
           u API ga O'ZI ulanadi — teskarisi emas. Ya'ni HR nomzodni
           rad etganda API botga xabar yubora olmaydi: uning botga
           yo'li yo'q. Shu sababli bot «xabar kutayotganlar» ro'yxatini
           davriy so'rab turadi.

           ⭐ Nega ROL, ruxsat emas. Javobda nomzodning Telegram
           MANZILI bor — u bilan istalgan xabarni yuborish mumkin.
           Bu odam uchun emas, JARAYON uchun ochilgan yo'l va u
           token endpoint'i bilan bir xil qoidaga bo'ysunadi
           (`SettingsEndpoints` dagi batafsil izoh).
          ═══════════════════════════════════════════════════════════
        */
        group.MapGet("/rejection-notices", async (
                int? limit,
                ListPendingRejectionNoticesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListPendingRejectionNoticesQuery(limit ?? 20), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(AuthenticationExtensions.BotServicePolicy)
            .WithName("ListPendingRejectionNotices")
            .WithSummary("Rad etish xabari kutayotgan nomzodlar — faqat botga")
            .WithDescription(
                "Rad etilgan, Telegram suhbati ma'lum va xabar hali yuborilmagan nomzodlar. " +
                "Odatda BO'SH ro'yxat qaytadi. Har bir yozuvda faqat xabar yozish uchun " +
                "kerakli maydonlar bor: nomzod, suhbat raqami, til va vakansiya nomi — " +
                "telefon, manzil va ichki izohlar bu javobga TUSHMAYDI. Tartib rad etilgan " +
                "sana bo'yicha: eng uzoq kutgan birinchi javob oladi. «limit» — ko'pi bilan 50.")
            .Produces<IReadOnlyList<RejectionNoticeDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/{id:guid}/rejection-notice-sent", async (
                Guid id,
                MarkRejectionNoticeSentCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new MarkRejectionNoticeSentCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(AuthenticationExtensions.BotServicePolicy)
            .WithName("MarkRejectionNoticeSent")
            .WithSummary("Rad etish xabari yuborildi — faqat botga")
            .WithDescription(
                "Bot xabarni HAQIQATAN yuborgandan KEYIN chaqiradi. IDEMPOTENT: ikkinchi " +
                "marta chaqirilsa hech narsa o'zgarmaydi va 204 qaytadi — javob yo'qolgan " +
                "bo'lsa bot so'rovni takrorlashi mumkin. Belgi oldindan qo'yilmaydi: " +
                "tarmoq uzilganda nomzod javobsiz qolardi va buni hech kim bilmasdi. " +
                "Xato: «nomzod.topilmadi» (404).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/reject", async (
                Guid id,
                CloseCandidateRequest? request,
                CloseCandidateCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new RejectCandidateCommand(id, request?.At, request?.Reason),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("RejectCandidate")
            .WithSummary("Nomzod — rad etish (korxona qarori)")
            .WithDescription(
                "Nomzod voronkadan chiqadi va faol ro'yxatlarda ko'rinmaydi. " +
                "«at» bo'sh — bugun. Ikkinchi marta yopib bo'lmaydi: " +
                "«nomzod.jarayonda_emas» (409).")
            .Produces<CandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/withdraw", async (
                Guid id,
                CloseCandidateRequest? request,
                CloseCandidateCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new WithdrawCandidateCommand(id, request?.At, request?.Reason),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("WithdrawCandidate")
            .WithSummary("Nomzod — o'zi voz kechdi")
            .WithDescription(
                "«Rad etish» dan ATAYLAB alohida: voronka hisobotida «biz rad etdik» " +
                "va «u ketib qoldi» butunlay boshqa ma'no beradi (birinchisi tanlov " +
                "sifati, ikkinchisi taklif shartlari haqida gapiradi).")
            .Produces<CandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapResume(IEndpointRouteBuilder group)
    {
        group.MapPost("/{id:guid}/resume", async (
                Guid id,
                IFormFile file,
                UploadCandidateResumeCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                await using var content = file.OpenReadStream();

                var natija = await handler
                    .HandleAsync(
                        new UploadCandidateResumeCommand(id, content, file.FileName, file.Length),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)

            // CSRF himoyasi bu yerda ORTIQCHA — JWT sarlavhada, sessiya
            // cookie'da emas (xodim hujjatlaridagi bilan bir xil sabab).
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxResumeRequestBytes))
            .WithName("UploadCandidateResume")
            .WithSummary("Nomzod — rezyume yuklash")
            .WithDescription(
                "«multipart/form-data», maydon nomi «file». Fayl turi MAZMUNIGA qarab " +
                "aniqlanadi: PDF, JPEG, PNG yoki WebP, eng ko'pi 20 MB. Nomzodda BITTA " +
                "rezyume bo'ladi — yangisi eskisini almashtiradi va eski fayl diskdan " +
                "o'chiriladi. Xatolar: «nomzod.rezyume_katta», «nomzod.rezyume_yaroqsiz» " +
                "(400), «nomzod.topilmadi» (404).")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<CandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/resume", async (
                Guid id,
                GetCandidateResumeQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetCandidateResumeQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                // JSON emas, BAYT qaytaradi — xodim hujjatidagi bilan bir xil.
                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.File(natija.Value.Content, natija.Value.ContentType, natija.Value.FileName);
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("DownloadCandidateResume")
            .WithSummary("Nomzod — rezyumeni yuklab olish")
            .WithDescription(
                "Fayl bayti. «Content-Disposition» da ASL fayl nomi qaytadi. " +
                "Rezyume yo'q bo'lsa: «nomzod.rezyume_topilmadi» (404).")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/resume", async (
                Guid id,
                DeleteCandidateResumeCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeleteCandidateResumeCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("DeleteCandidateResume")
            .WithSummary("Nomzod — rezyumeni olib tashlash")
            .WithDescription("Fayl HAQIQATAN diskdan o'chiriladi: unga boshqa hech kim ishora qilmaydi.")
            .Produces<CandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
