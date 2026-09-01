using Bonvi.Api.Common;
using Bonvi.Api.Extensions;
using Bonvi.Application.Common.Documents;
using Bonvi.Application.Common.Photos;
using Bonvi.Application.Employees.Commands;
using Bonvi.Application.Employees.Competencies;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Employees.Flags;
using Bonvi.Application.Employees.MehnatUz;
using Bonvi.Application.Employees.Photos;
using Bonvi.Application.Employees.Queries;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bonvi.Api.Endpoints;

/// <summary>Xodimga belgi qo'yish so'rovi tanasi.</summary>
/// <param name="Type">Belgi turi: rezerv / qora ro'yxat / nazorat / qobiliyatli.</param>
/// <param name="StartDate">Boshlanish sanasi. Bo'sh bo'lsa — bugun.</param>
/// <param name="EndDate">Tugash sanasi (muddatli belgilar uchun).</param>
/// <param name="Reason">Sabab. Qora ro'yxat uchun MAJBURIY.</param>
/// <param name="Confirm">
/// Kuchdagi belgini almashtirishga rozilik (so'rov qatoridagi «confirm»
/// bilan bir xil — bo'shatishdagi naqsh).
/// </param>
public sealed record RaiseFlagRequest(
    EmployeeFlagType Type,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    string? Reason = null,
    bool? Confirm = null);

/// <summary>Belgini yopish so'rovi tanasi.</summary>
/// <param name="ClosedAt">Yopilish sanasi. Bo'sh bo'lsa — bugun.</param>
/// <param name="Note">Izoh — sababga qo'shiladi.</param>
public sealed record CloseFlagRequest(DateOnly? ClosedAt = null, string? Note = null);

/// <summary>Qobiliyatlar profilini saqlash so'rovi tanasi — butun profil bir yo'la yuboriladi.</summary>
/// <param name="Languages">
/// ESKI erkin matn — faqat Excel'dan ko'chgan yozuvni saqlab turish uchun;
/// yangi kiritishlar <paramref name="LanguageSkills"/> ga boradi.
/// </param>
/// <param name="LanguageSkills">
/// Tillar ro'yxati: har biri <c>{"language":"English","level":"Advanced"}</c>.
/// Bitta til faqat bir marta kelishi mumkin.
/// </param>
/// <param name="ComputerLiteracyPercent">Kompyuter savodxonligi, 0–100%.</param>
/// <param name="Specialization">Mutaxassisligi.</param>
/// <param name="OtherAbilities">Boshqa qobiliyatlar.</param>
/// <param name="Interest">Qiziqishi nimada.</param>
/// <param name="EducationPlace">Ta'lim olgan joyi.</param>
public sealed record SaveCompetencyRequest(
    string? Languages = null,
    IReadOnlyList<LanguageSkill>? LanguageSkills = null,
    int? ComputerLiteracyPercent = null,
    string? Specialization = null,
    string? OtherAbilities = null,
    string? Interest = null,
    string? EducationPlace = null);

/// <summary>
/// Xodimlar endpoint'lari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Oklad haqida.</b> Bu yerdagi hech bir endpoint okladni alohida
/// tekshirmaydi — u DTO yig'ilishida <c>salary.view</c> ruxsatiga qarab
/// qo'shiladi yoki qo'shilmaydi. Sabab: tekshiruv HTTP qatlamida bo'lsa,
/// yangi endpoint qo'shgan dasturchi uni unutishi mumkin; DTO darajasida
/// esa unutish imkoni yo'q.
/// </para>
/// <para>
/// <b>Ruxsatlar taqsimoti.</b> Ko'rish (<c>employees.view</c>), yaratish
/// (<c>employees.create</c>) va tahrirlash (<c>employees.edit</c>) ataylab
/// ajratilgan: HR yordamchisi ma'lumot kiritishi mumkin, lekin mavjud
/// kartani o'zgartira olmasligi kerak bo'lgan holat bor.
/// </para>
/// </remarks>
public static class EmployeeEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/employees").WithTags("Employees");

        MapSearch(group);
        MapCard(group);
        MapWrites(group);
        MapPhoto(group);
        MapMehnatUz(group);
        MapStages(group);
        MapFlags(group);
        MapCompetency(group);

        return app;
    }

    /// <summary>
    /// Rasm so'rovi tanasining eng katta hajmi.
    /// </summary>
    /// <remarks>
    /// Faylning o'zi 10 MB bilan cheklangan (<see cref="PhotoLimits.MaxBytes"/>),
    /// lekin <c>multipart/form-data</c> tanasiga chegara satrlari va maydon
    /// sarlavhalari ham qo'shiladi. 64 KB zaxira aynan shu «konvert» uchun:
    /// usiz chegaraga tegib turgan haqiqiy 10 MB fayl 413 bilan rad etilardi.
    /// </remarks>
    private const long MaxPhotoRequestBytes = PhotoLimits.MaxBytes + (64 * 1024);

    // ════════════════════════════════════════════════════════════
    //  Ro'yxat — asosiy ekran
    // ════════════════════════════════════════════════════════════

    private static void MapSearch(IEndpointRouteBuilder group)
    {
        group.MapGet("/", async (
                string? search,
                Guid? departmentId,
                Guid? positionId,
                Guid? directionId,
                Guid? legalEntityId,
                // Sanoq emas, SATR: sabab `SearchEmployeesQuery.ParseStatus`
                // izohida — minimal API sanoqni registrga sezgir bog'laydi
                // va bo'sh qiymatda 400 qaytarardi.
                string? status,
                bool? needsReview,
                int? page,
                int? pageSize,
                string? sort,
                SearchEmployeesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new SearchEmployeesQuery(
                            search,
                            departmentId,
                            positionId,
                            directionId,
                            legalEntityId,
                            status,
                            needsReview,
                            page,
                            pageSize,
                            sort),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmployeesView)
            .WithName("SearchEmployees")
            .WithSummary("Xodimlar — ro'yxat va qidiruv")
            .WithDescription(
                "Filtr, saralash va sahifalash SERVERDA bajariladi: arxiv bilan birga " +
                "2000+ yozuv bo'lishi mumkin. «search» — FIO, JSHSHIR, telefon yoki pasport. " +
                "«sort» qiymatlari: name, hireDate, department, position, status, birthDate; " +
                "teskari tartib uchun oldiga «-» qo'yiladi. «pageSize» eng ko'pi 200. " +
                "Oklad ustuni faqat «salary.view» ruxsati bilan to'ldiriladi.")
            .Produces<PagedResult<EmployeeListItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    // ════════════════════════════════════════════════════════════
    //  Karta va mehnat yo'li
    // ════════════════════════════════════════════════════════════

    private static void MapCard(IEndpointRouteBuilder group)
    {
        group.MapGet("/{id:guid}", async (
                Guid id,
                GetEmployeeQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetEmployeeQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmployeesView)
            .WithName("GetEmployee")
            .WithSummary("Xodim — to'liq karta")
            .WithDescription(
                "Shaxs, uning BARCHA mehnat munosabatlari (arxiv ham) va belgilari bitta javobda. " +
                "«totalActiveRate» — faol stavkalar yig'indisi (1.5 dan oshsa e'tibor talab qiladi).")
            .Produces<EmployeeCardDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/timeline", async (
                Guid id,
                bool? oldestFirst,
                GetEmployeeTimelineQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetEmployeeTimelineQuery(id, !(oldestFirst ?? false)), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmploymentView)
            .WithName("GetEmployeeTimeline")
            .WithSummary("Xodim — butun mehnat yo'li bitta lentada")
            .WithDescription(
                "Barcha mehnat munosabatlarining hodisalari QO'SHIB, sana bo'yicha tartiblanadi. " +
                "Har bir element qaysi firmaga tegishli ekanini ko'rsatadi — holdingda bitta odam " +
                "ikki firmada ishlashi mumkin. Sukut bo'yicha eng yangisi tepada; " +
                "«oldestFirst=true» xronologik tartib beradi.")
            .Produces<EmployeeTimelineDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // ════════════════════════════════════════════════════════════
    //  Yaratish va tahrirlash
    // ════════════════════════════════════════════════════════════

    private static void MapWrites(IEndpointRouteBuilder group)
    {
        group.MapPost("/", async (
                SaveEmployeeCommand command,
                CreateEmployeeCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmployeesCreate)
            .WithName("CreateEmployee")
            .WithSummary("Xodim — yangi karta")
            .WithDescription(
                "Mehnat munosabatisiz karta (nomzod yoki arxivdan kiritilayotgan shaxs). " +
                "Odatdagi yo'l esa «POST /api/employments/hire» — u shaxsni ham, ishni ham " +
                "bitta forma bilan yaratadi. JSHSHIR takrorlansa: «xodim.jshshir_band» (409).")
            .Produces<EmployeeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                SaveEmployeeCommand command,
                UpdateEmployeeCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(id, command, cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmployeesEdit)
            .WithName("UpdateEmployee")
            .WithSummary("Xodim — kartani tahrirlash")
            .WithDescription(
                "Tug'ilgan sana ham tahrirlanadi: uni JSHSHIR bilan BIRGA yuborish mumkin va " +
                "moslik yangi juftlik bo'yicha tekshiriladi — aks holda ko'chirishda belgilangan " +
                "nomuvofiqlikni tuzatib bo'lmasdi. «birthDate» yuborilmasa sana o'zgarmaydi. " +
                "«photoKey» ham shunday: rasm bu forma orqali BO'SHATILMAYDI, uni olib tashlash " +
                "uchun «DELETE /api/employees/{id}/photo» bor. Muvaffaqiyatli tahrirdan keyin " +
                "«tuzatish kerak» belgisi olib tashlanadi — yozuvni odam ko'rib chiqdi.")
            .Produces<EmployeeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    // ════════════════════════════════════════════════════════════
    //  Rasm
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Xodim surati: berish, yuklash, o'chirish.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega rasm alohida endpoint.</b> Uni kartaning JSON javobiga base64
    /// bo'lib qo'shish javobni bir necha barobar shishirardi va brauzer
    /// keshidan foydalanish imkonini yo'qotardi. Alohida manzil esa oddiy
    /// <c>&lt;img src&gt;</c> bo'lib ishlaydi va <c>ETag</c> bilan keshlanadi.
    /// </para>
    /// <para>
    /// <b>Ruxsatlar.</b> Ko'rish — <c>employees.view</c> (kartani ko'ra
    /// olgan odam suratni ham ko'radi), yuklash va o'chirish esa
    /// <c>employees.edit</c>: rasm ham kartaning bir qismi.
    /// </para>
    /// </remarks>
    private static void MapPhoto(IEndpointRouteBuilder group)
    {
        group.MapGet("/{id:guid}/photo", async (
                Guid id,
                HttpContext context,
                GetEmployeePhotoQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetEmployeePhotoQuery(id, PhotoHttp.ReadKnownVersion(context.Request)),
                        cancellationToken)
                    .ConfigureAwait(false);

                // Yagona endpoint bo'lib, JSON emas — BAYT qaytaradi.
                // Shu sababli `ToHttpResult()` faqat xato yo'lida ishlatiladi:
                // aks holda u oqimni obyekt deb serializatsiya qilib yuborardi.
                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : PhotoHttp.Write(context, natija.Value);
            })
            .RequireAuthorization(Permissions.EmployeesView)
            .WithName("GetEmployeePhoto")
            .WithSummary("Xodim — rasmi")
            .WithDescription(
                "Rasm baytlari (image/jpeg). «ETag» saqlash kalitidan olingan barqaror xesh; " +
                "«If-None-Match» mos kelsa 304 qaytadi. Kesh SHAXSIY (private, 1 kun) — " +
                "surat shaxsiy ma'lumot va umumiy proksida saqlanmasligi kerak. " +
                "Rasm yo'q bo'lsa: «xodim.rasm_yoq» (404).")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/photo", async (
                Guid id,
                IFormFile file,
                UploadEmployeePhotoCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                await using var content = file.OpenReadStream();

                var natija = await handler
                    .HandleAsync(
                        new UploadEmployeePhotoCommand(id, content, file.ContentType, file.Length),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmployeesEdit)

            // CSRF himoyasi bu yerda ORTIQCHA: sessiya cookie'da emas, JWT
            // sarlavhasida yuriladi va boshqa saytdagi forma uni qo'sha olmaydi.
            // Belgi qoldirilsa, minimal API forma so'rovini token yo'qligi
            // uchun rad etardi.
            .DisableAntiforgery()

            // Cheklov marshrut darajasida: chegaradan katta tana Kestrel
            // tomonidan umuman o'qilmaydi va serverga yuk tushmaydi.
            .WithMetadata(new RequestSizeLimitAttribute(MaxPhotoRequestBytes))
            .WithName("UploadEmployeePhoto")
            .WithSummary("Xodim — rasm yuklash")
            .WithDescription(
                "«multipart/form-data», maydon nomi «file». JPEG, PNG yoki WebP, eng ko'pi 10 MB. " +
                "Server rasmni markazdan kvadrat qirqib 512×512 JPEG qiladi va eskisini o'chiradi — " +
                "manba fayl saqlanmaydi. Xatolar: «xodim.rasm_katta», «xodim.rasm_turi», " +
                "«xodim.rasm_yaroqsiz» (hammasi 400).")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<EmployeePhotoDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/photo", async (
                Guid id,
                DeleteEmployeePhotoCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmployeesEdit)
            .WithName("DeleteEmployeePhoto")
            .WithSummary("Xodim — rasmni o'chirish")
            .WithDescription(
                "Fayl ham o'chadi. Amal IDEMPOTENT: rasm allaqachon yo'q bo'lsa ham 204 qaytadi — " +
                "so'rovning maqsadi rasm qolmasligi, u esa bajarilgan.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // ════════════════════════════════════════════════════════════
    //  mehnat.uz skrinshoti
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// So'rov chegarasi: hujjat chegarasi + multipart «konverti».
    /// </summary>
    /// <remarks>
    /// Rasm emas, HUJJAT chegarasi (20 MB): skrinshot qirqilmaydi va
    /// kichraytirilmaydi, sababi <c>Employee.MehnatUzScreenshotKey</c>
    /// izohida. Katta monitorning to'liq ekran surati bemalol bir necha
    /// megabayt bo'ladi.
    /// </remarks>
    private const long MaxScreenshotRequestBytes = DocumentLimits.MaxBytes + (64 * 1024);

    /// <summary>
    /// mehnat.uz profilining skrinshoti: berish, yuklash, o'chirish.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uchlik xodim rasmidagi bilan bir xil shaklda, lekin ATAYLAB alohida
    /// marshrutda: bu boshqa fakt (xodimning yuzi emas, tekshiruv dalili)
    /// va u boshqa quvurdan o'tadi. Ruxsatlar rasm bilan bir xil:
    /// ko'rish — <c>employees.view</c>, o'zgartirish — <c>employees.edit</c>.
    /// </para>
    /// <para>
    /// <c>ETag</c> bu yerda YO'Q (rasmdan farqli): skrinshot xodimlar
    /// ro'yxatida 25 marta takrorlanmaydi, u faqat bitta kartada va faqat
    /// ochilganda so'raladi — shartli so'rov mexanizmini qurishning foydasi
    /// bo'lmasdi. Kalit o'zgarganda mijozdagi kesh baribir eskiradi:
    /// so'rov kaliti <c>screenshotKey</c> ni o'z ichiga oladi.
    /// </para>
    /// </remarks>
    private static void MapMehnatUz(IEndpointRouteBuilder group)
    {
        group.MapGet("/{id:guid}/mehnat-uz-screenshot", async (
                Guid id,
                GetMehnatUzScreenshotQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetMehnatUzScreenshotQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                // Bayt qaytaradigan endpoint: `ToHttpResult()` faqat xato
                // yo'lida (rasm endpoint'idagi bilan bir xil sabab).
                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.Stream(natija.Value.Content, natija.Value.ContentType);
            })
            .RequireAuthorization(Permissions.EmployeesView)
            .WithName("GetMehnatUzScreenshot")
            .WithSummary("Xodim — mehnat.uz skrinshoti")
            .WithDescription(
                "Fayl baytlari SAQLANGAN turida qaytadi (PDF, JPEG, PNG yoki WebP) — " +
                "skrinshot qayta kodlanmaydi, chunki uning qiymati matnda. " +
                "Skrinshot yo'q bo'lsa: «xodim.mehnat_rasm_yoq» (404).")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/mehnat-uz-screenshot", async (
                Guid id,
                IFormFile file,
                UploadMehnatUzScreenshotCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                await using var content = file.OpenReadStream();

                var natija = await handler
                    .HandleAsync(
                        new UploadMehnatUzScreenshotCommand(id, content, file.Length),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmployeesEdit)

            // CSRF himoyasi ortiqcha — sabab `MapPhoto` dagi bilan bir xil:
            // sessiya JWT sarlavhasida, cookie'da emas.
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxScreenshotRequestBytes))
            .WithName("UploadMehnatUzScreenshot")
            .WithSummary("Xodim — mehnat.uz skrinshotini yuklash")
            .WithDescription(
                "«multipart/form-data», maydon nomi «file». Fayl turi MAZMUNIGA qarab " +
                "aniqlanadi: PDF, JPEG, PNG yoki WebP, eng ko'pi 20 MB. Rasm QIRQILMAYDI va " +
                "KICHRAYTIRILMAYDI — skrinshotdagi matn o'qilishi kerak. Eskisi almashtiriladi " +
                "va diskdan o'chadi. Xatolar: «hujjat.katta», «hujjat.yaroqsiz» (400).")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<MehnatUzScreenshotDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/mehnat-uz-screenshot", async (
                Guid id,
                DeleteMehnatUzScreenshotCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeleteMehnatUzScreenshotCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmployeesEdit)
            .WithName("DeleteMehnatUzScreenshot")
            .WithSummary("Xodim — mehnat.uz skrinshotini o'chirish")
            .WithDescription(
                "Fayl ham o'chadi. Amal IDEMPOTENT: skrinshot allaqachon yo'q bo'lsa ham 204.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // ════════════════════════════════════════════════════════════
    //  Nomzodlik bosqichlari — «Kengash xulosasi»
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Xodimning nomzodlik davridagi bosqich xulosalari.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Faqat <c>GET</c>.</b> Yozish endpoint'i ataylab yo'q: bo'lim
    /// rekrutingdan kelgan TARIXNI ko'rsatadi va unga xodim kartasidan
    /// yozuv qo'shib bo'lmaydi (to'liq asos <c>GetEmployeeStagesQuery</c>
    /// izohida).
    /// </para>
    /// <para>
    /// Ruxsat — <see cref="Permissions.RecruitingView"/>,
    /// <c>employees.view</c> EMAS. Sabab rekruting ruxsati izohida
    /// yozilgan: suhbat xulosalari va rad etish sabablari alohida toifadagi
    /// ma'lumot, va xodimlar ro'yxatini ochgan har bir kishi «kimni nega
    /// olmadik» degan yozuvlarni ko'rishi shart emas. Bo'lim ruxsatsiz
    /// foydalanuvchida umuman CHIZILMAYDI — mijoz uni so'ramaydi ham.
    /// </para>
    /// </remarks>
    private static void MapStages(IEndpointRouteBuilder group)
    {
        group.MapGet("/{id:guid}/stages", async (
                Guid id,
                GetEmployeeStagesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetEmployeeStagesQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("GetEmployeeStages")
            .WithSummary("Xodim — nomzodlik bosqichlari va kengash xulosasi")
            .WithDescription(
                "Xodim rekruting orqali kelgan bo'lsa, uning voronkadagi bosqich yozuvlari " +
                "XRONOLOGIK tartibda qaytadi: bosqich, sana, natija, xulosa matni va kim " +
                "yozgani. Bog'lanish ishga qabul hodisasidagi «candidateId» orqali. " +
                "Excel'dan ko'chirilgan xodimlarda bunday yozuv YO'Q — bo'sh ro'yxat qaytadi, " +
                "bu xato emas.")
            .Produces<IReadOnlyList<EmployeeStageEntryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // ════════════════════════════════════════════════════════════
    //  Belgilar
    // ════════════════════════════════════════════════════════════

    private static void MapFlags(IEndpointRouteBuilder group)
    {
        group.MapGet("/flagged", async (
                EmployeeFlagType type,
                bool? onlyActive,
                string? search,
                ListFlaggedQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListFlaggedQuery(type, onlyActive ?? true, search), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.ListsView)
            .WithName("ListFlaggedEmployees")
            .WithSummary("Belgilar — tur bo'yicha ro'yxat")
            .WithDescription(
                "To'rt ro'yxat bitta endpoint bilan: «type» = Reserve | Blacklist | Monitoring | Capable. " +
                "Sukut bo'yicha faqat bugun kuchda bo'lgan belgilar; «onlyActive=false» tarixni ham beradi. " +
                "«search» — XODIM bo'yicha (F.I.Sh., JSHSHIR, pasport, telefon), xodimlar ro'yxatidagi " +
                "bilan aynan bir xil qoida.")
            .Produces<IReadOnlyList<FlaggedEmployeeDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}/flags", async (
                Guid id,
                GetEmployeeFlagsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetEmployeeFlagsQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.ListsView)
            .WithName("GetEmployeeFlags")
            .WithSummary("Xodim — belgilari")
            .WithDescription(
                "HAR DOIM tarix bilan: kartadagi «Belgilar» tabi yopilgan belgilarni ham sanalari " +
                "bilan ko'rsatadi, nishonlar esa mijozda kesiladi. «onlyActive» parametri OLIB " +
                "TASHLANDI — uni hech kim ishlatmasdi va u kuchdagilikni qolgan hamma joydan " +
                "boshqacha hisoblardi.")
            .Produces<IReadOnlyList<EmployeeFlagDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/flags", async (
                Guid id,
                RaiseFlagRequest request,
                bool? confirm,
                RaiseFlagCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new RaiseFlagCommand(
                            id,
                            request.Type,
                            request.StartDate,
                            request.EndDate,
                            request.Reason,
                            confirm ?? request.Confirm ?? false),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.ListsManage)
            .WithName("RaiseEmployeeFlag")
            .WithSummary("Xodim — belgi qo'yish")
            .WithDescription(
                "BOSQICH qoidasi: ishlab turgan xodimga faqat Monitoring | Capable qo'yiladi, " +
                "ishdan ketganga faqat Reserve | Blacklist. Mos kelmasa — «belgi.xodim_ishlayapti» " +
                "yoki «belgi.xodim_ketgan» (409) va bu TAQIQ: «confirm» uni ochmaydi. " +
                "Qora ro'yxat uchun sabab MAJBURIY: «belgi.sabab_bosh» (400). " +
                "Xodimda bir vaqtda ATIGI BITTA kuchdagi belgi bo'ladi: eskisi bo'lsa " +
                "«belgi.boshqa_belgi_bor» (409) qaytadi, «?confirm=true» bilan qayta " +
                "yuborilganda esa eskisi YOPILADI (o'chirilmaydi — tarix qoladi) va " +
                "yangisi ochiladi, ikkalasi bitta tranzaksiyada. " +
                "Belgilar bosqich almashganda O'ZI ham yopiladi: bo'shatish «nazoratda»/«qobiliyatli» " +
                "ni, ishga qabul esa «rezerv» ni yopadi. " +
                "Qora ro'yxat va nazorat keyinchalik ishga qabul va bo'shatishni to'sadi.")
            .Produces<EmployeeFlagDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/flags/{flagId:guid}/close", async (
                Guid flagId,
                CloseFlagRequest? request,
                CloseFlagCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new CloseFlagCommand(flagId, request?.ClosedAt, request?.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.ListsManage)
            .WithName("CloseEmployeeFlag")
            .WithSummary("Xodim — belgini yopish")
            .WithDescription(
                "Belgi O'CHIRILMAYDI, unga tugash sanasi qo'yiladi: «qachon va nima uchun " +
                "yopildi» savoli javobsiz qolmasligi kerak. Yopilgan belgini qayta yopib " +
                "bo'lmaydi: «belgi.yopilgan» (409).")
            .Produces<EmployeeFlagDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    // ════════════════════════════════════════════════════════════
    //  Qobiliyatlar profili (91.15)
    // ════════════════════════════════════════════════════════════

    private static void MapCompetency(IEndpointRouteBuilder group)
    {
        group.MapGet("/{id:guid}/competency", async (
                Guid id,
                GetEmployeeCompetencyQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetEmployeeCompetencyQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmployeesView)
            .WithName("GetEmployeeCompetency")
            .WithSummary("Xodim — qobiliyatlar profili")
            .WithDescription(
                "Tillar («languageSkills»: til + daraja, sanoqlar NOM bilan), kompyuter savodxonligi " +
                "(0-100%), mutaxassislik, boshqa qobiliyatlar, qiziqish, ta'lim olgan joyi. " +
                "«languages» — Excel'dan ko'chgan ESKI erkin matn, faqat ko'rsatish uchun. " +
                "HR hali to'ldirmagan bo'lsa — bo'sh javob (200, null tana), xato emas.")
            .Produces<EmployeeCompetencyDto?>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/competency", async (
                Guid id,
                SaveCompetencyRequest request,
                SaveEmployeeCompetencyCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new SaveEmployeeCompetencyCommand(
                            id,
                            request.Languages,
                            request.LanguageSkills,
                            request.ComputerLiteracyPercent,
                            request.Specialization,
                            request.OtherAbilities,
                            request.Interest,
                            request.EducationPlace),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmployeesEdit)
            .WithName("SaveEmployeeCompetency")
            .WithSummary("Xodim — qobiliyatlar profilini saqlash")
            .WithDescription(
                "Profil BUTUNLIGICHA almashtiriladi (yaratadi yoki bor bo'lsa yangilaydi) — 91.15 " +
                "varag'idagi bitta qator kabi. «computerLiteracyPercent» 0 dan 100 gacha: " +
                "«qobiliyat.kompyuter_foiz» (400). «languageSkills»da bitta til bir marta: " +
                "«qobiliyat.til_takror» (400); ro'yxatdan tashqari sanoq raqami: " +
                "«qobiliyat.til_notogri» (400).")
            .Produces<EmployeeCompetencyDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
