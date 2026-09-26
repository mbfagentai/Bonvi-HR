using Bonvi.Api.Common;
using Bonvi.Api.Extensions;
using Bonvi.Application.Common.Documents;
using Bonvi.Application.Common.Photos;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Recruiting.Candidates;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Recruiting;
using Microsoft.AspNetCore.Mvc;

namespace Bonvi.Api.Endpoints;

/// <summary>Nomzodni voronkaga kiritish so'rovi tanasi.</summary>
/// <param name="LastName">Familiya. Majburiy.</param>
/// <param name="FirstName">Ism. Majburiy.</param>
/// <param name="VacancyId">
/// Qaysi vakansiyaga. Berilmasa — «Umumiy anketa» xizmat vakansiyasi.
/// </param>
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
/// <param name="Questionnaire">
/// Botda to'ldirilgan anketa — IXTIYORIY blok. Berilmasa so'rov AYNAN
/// avvalgidek ishlaydi (batafsil sabab <c>CandidateQuestionnaireInput</c>
/// izohida).
/// </param>
public sealed record RegisterCandidateRequest(
    string? LastName,
    string? FirstName,

    /*
      Vakansiya IXTIYORIY: ochiq ish o'rni bo'lmaganda ham nomzod anketa
      to'ldira olishi kerak va o'shanda server uni «Umumiy anketa»
      xizmat vakansiyasiga qo'yadi (izohi
      `RegisterCandidateCommandHandler` da).

      ⚠️ Tur `Guid?` va sukut qiymati `null`. `Guid` ga `= default`
      berilsa OpenAPI hujjati butunlay 500 bo'ladi — loyiha bundan bir
      marta jabr ko'rgan. Shu sababli maydon majburiylardan KEYIN
      turadi: C# ixtiyoriy parametrni oldinga qo'yishga ruxsat bermaydi,
      JSON esa nom bo'yicha o'qiladi va mijoz uchun tartib ahamiyatsiz.
    */
    Guid? VacancyId = null,
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
    string? TelegramLanguage = null,

    /*
      Anketa — botning 22 qadamli oqimidagi javoblar. Blok BUTUNLIGICHA
      ixtiyoriy: deploy tartibi «avval API, keyin bot» va oraliqda eski
      bot shu maydonsiz so'rov yuboradi.

      Tur Application qatlamidan olinadi va API'da qayta e'lon
      qilinmaydi — `LanguageSkill` bilan bir xil qaror: ikkita bir xil
      record bo'lganida ular vaqt o'tib bir-biridan uzoqlashardi va
      farqi faqat prod'da ko'rinardi.
    */
    CandidateQuestionnaireInput? Questionnaire = null);

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
/// <param name="VacancyId">
/// Yangi vakansiya. MAJBURIY va shu sababli sukut qiymatSIZ — <c>Guid</c> ga
/// <c>= default</c> yozilsa butun OpenAPI hujjati 500 bo'lardi (loyiha
/// bundan bir marta jabr ko'rgan, 2026-08-29).
/// </param>
/// <param name="Note">
/// Amal izohi — ICHKI qayd, nomzodga yuborilmaydi. Bo'sh bo'lmasa server
/// uni nomzod izohlari tarixiga yozadi. Nomzodning «izoh» maydoni EMAS: u
/// odamning uzoq muddatli tavsifi va tahrirlash formasidan qayta yozilib
/// ketardi.
/// </param>
/// <param name="NextContactDate">
/// Keyingi aloqa sanasi. Berilmasa saqlangan sana o'zgarmaydi — bu amalda
/// «tozalash» yo'q.
/// </param>
public sealed record ReassignCandidateRequest(
    Guid VacancyId,
    string? Note = null,
    DateOnly? NextContactDate = null);

/// <summary>
/// Voronkadan chiqqan nomzodni qaytarish so'rovi tanasi.
/// </summary>
/// <remarks>
/// Tana BUTUNLIGICHA ixtiyoriy (<c>CloseCandidateRequest?</c> bilan aynan
/// bir xil naqsh) — rezervdan qaytarish uni umuman yubormasligi mumkin.
/// Rad etilgan va voz kechgan nomzodda esa «note» SHART va busiz so'rov
/// 400 bilan qaytadi.
/// </remarks>
/// <param name="Note">
/// Qaytarish sababi — ICHKI qayd (yuqoridagi bilan bir xil qoida).
/// Rad etilgan va voz kechgan nomzodda MAJBURIY.
/// </param>
/// <param name="NextContactDate">Keyingi aloqa sanasi. Berilmasa — o'zgarmaydi.</param>
public sealed record ReturnCandidateToFunnelRequest(
    string? Note = null,
    DateOnly? NextContactDate = null);

/// <summary>Bosqich natijasini yozish so'rovi tanasi.</summary>
/// <param name="StageId">Bosqich. Majburiy.</param>
/// <param name="Date">Bosqich sanasi. Bo'sh — bugun.</param>
/// <param name="Conclusion">Xulosa matni — kengash xulosasi ham shu maydon.</param>
/// <param name="NextContactDate">
/// Keyingi aloqa sanasi (<c>YYYY-MM-DD</c>, Toshkent kalendari) — berilsa
/// QO'YILADI. O'tgan sana ham qabul qilinadi: maydonning butun maqsadi
/// «bugun yoki muddati o'tgan» ro'yxatini berish.
/// </param>
/// <param name="ClearNextContact">
/// <c>true</c> — saqlangan sana TOZALANADI
/// (<paramref name="NextContactDate"/> e'tiborsiz qoladi).
/// <para>
/// ⭐ <b>Nega ikkita maydon, «null — tozalash» emas.</b> Bu endpointni
/// ikkita mijoz chaqiradi: yangi oyna (formasi bor) va voronkadagi
/// SUDRASH (formasi yo'q — u sanani umuman yubormaydi). «null —
/// tozalash» shartnomasida kartani sudrab qo'yish HR besh daqiqa oldin
/// qo'ygan sanani JIMGINA o'chirib tashlardi. Ikkalasi ham berilmasa
/// saqlangan qiymat o'zgarmaydi.
/// </para>
/// <para>
/// ⚠️ Tur <c>bool?</c> va sukut qiymati <c>null</c>. <c>bool</c> ga
/// <c>= false</c> yozilsa OpenAPI hujjati butunlay 500 bo'ladi — qiymat
/// turlariga sukut qiymat berish shu faylda TAQIQLANGAN (yuqoridagi
/// <c>RegisterCandidateRequest</c> izohi). Ishlovchi qatlamdagi buyruqda
/// esa oddiy <c>bool</c>: generator faqat so'rov rekordlarini ko'radi.
/// </para>
/// </param>
public sealed record RecordCandidateStageRequest(
    Guid StageId,
    DateOnly? Date = null,
    string? Conclusion = null,
    DateOnly? NextContactDate = null,
    bool? ClearNextContact = null);

/// <summary>Tarixdagi bitta bosqich yozuvini tuzatish so'rovi tanasi.</summary>
/// <remarks>
/// ⭐ <b>Bosqich bu yerda YO'Q.</b> Tuzatish yozuvning sanasini va
/// xulosasini o'zgartiradi, «qaysi bosqich» degan faktni emas — sababi
/// <c>CandidateStageEntry.Edit</c> domen izohida. Yozuvning O'ZI manzilda
/// ko'rsatiladi (<c>/stages/{entryId}</c>), tanada emas: u resursning
/// identifikatori, ma'lumot emas.
/// </remarks>
/// <param name="Date">Bosqich sanasi. Bo'sh — bugun.</param>
/// <param name="Conclusion">
/// Xulosa matni. ⚠️ Bu maydon USTIDAN yozadi: mijoz mavjud matnni
/// oynaga TO'LDIRIB ochishi va o'zgarmagan bo'lsa AYNAN o'sha matnni
/// qaytarib yuborishi shart. Bo'sh yuborilsa xulosa o'chadi.
/// </param>
/// <param name="NextContactDate">
/// Keyingi aloqa sanasi — <see cref="RecordCandidateStageRequest"/> dagi
/// bilan bir xil uch holatli qoida.
/// </param>
/// <param name="ClearNextContact">Sanani tozalash.</param>
public sealed record EditCandidateStageEntryRequest(
    DateOnly? Date = null,
    string? Conclusion = null,
    DateOnly? NextContactDate = null,
    bool? ClearNextContact = null);

/// <summary>Nomzodni suhbatga chaqirish so'rovi tanasi.</summary>
/// <param name="InterviewAt">
/// Suhbat lahzasi — siljish bilan («2026-09-10T15:00:00+05:00»). Siljishsiz
/// yuborilgan qiymat UTC deb o'qiladi va nomzodga besh soat oldingi vaqtni
/// ko'rsatardi.
/// </param>
/// <param name="StageId">
/// Qaysi bosqichga o'tkaziladi. Bo'sh — bosqich o'zgarmaydi (vaqtni
/// ko'chirish).
/// </param>
/// <param name="Conclusion">
/// Izoh matni — ixtiyoriy va ICHKI (nomzodga yuborilmaydi).
/// <para>
/// ⭐ <b>Ikkita manzili bor, qaysi biriga tushishini SERVER hal qiladi.</b>
/// <c>stageId</c> berilsa — matn yangi bosqich yozuvining xulosasi bo'ladi
/// (avvalgidek). <c>stageId</c> bo'sh bo'lsa (suhbatni KO'CHIRISH, bosqich
/// o'zgarmaydi) — matn nomzod izohlari tarixiga «suhbat ko'chirildi» turi
/// bilan yoziladi. Mijoz DOIM bitta matn maydonini yuboradi: tanlov —
/// saqlash qarori, ya'ni serverning ishi, brauzerning emas. Ilgari bu
/// maydon ko'chirishda umuman ko'rsatilmasdi va yozilgan matn jimgina
/// tashlanardi.
/// </para>
/// </param>
/// <param name="NextContactDate">
/// Keyingi aloqa sanasi — <c>RecordCandidateStageRequest</c> dagi bilan
/// AYNAN bir xil uch holatli qoida.
/// </param>
/// <param name="ClearNextContact">
/// <c>true</c> — saqlangan sana tozalanadi. Qoidaning to'liq izohi
/// <see cref="RecordCandidateStageRequest.ClearNextContact"/> da.
/// </param>
public sealed record InviteCandidateToInterviewRequest(
    DateTimeOffset InterviewAt,
    Guid? StageId = null,
    string? Conclusion = null,
    DateOnly? NextContactDate = null,
    bool? ClearNextContact = null);

/// <summary>
/// Nomzodni voronkadan chiqarish so'rovi tanasi.
/// </summary>
/// <remarks>
/// Uchala yakun uchun BITTA shakl: rad etish, voz kechish va rezerv —
/// hammasi «qachon» va «nega» dan iborat. Uchta bir xil record yozilsa,
/// ular vaqt o'tib bir-biridan uzoqlashardi va farqi faqat mijozda
/// ko'rinardi.
/// </remarks>
/// <param name="At">Sana. Bo'sh — bugun.</param>
/// <param name="Reason">Sabab — nomzod kartasida ko'rinadi.</param>
/// <param name="NextContactDate">
/// Keyingi aloqa sanasi — ⚠️ FAQAT <c>/reserve</c> uni hisobga oladi.
/// <para>
/// <c>/reject</c> va <c>/withdraw</c> uni JIMGINA e'tiborsiz qoldiradi va
/// saqlangan sanani TOZALAYDI: rad etilgan nomzod «bugun qo'ng'iroq
/// qilinadiganlar» ro'yxatida abadiy turib qolsa, HR o'sha ro'yxatga
/// ishonmay qo'yardi. Rezerv esa chiqish emas, KECHIKTIRISH («o'rin
/// bo'shasa chaqiramiz») — u yerda sana o'z joyida.
/// </para>
/// <para>
/// Xato QAYTARILMAYDI: interfeys maydonni faqat rezerv uchun chizadi va
/// e'tiborsiz qoladigan maydon uchun 400 berish hech narsani himoya
/// qilmasdi, faqat mijozlarni buzardi.
/// </para>
/// </param>
public sealed record CloseCandidateRequest(
    DateOnly? At = null,
    string? Reason = null,
    DateOnly? NextContactDate = null);

/// <summary>
/// Nomzodni ishga olish so'rovi tanasi.
/// </summary>
/// <remarks>
/// <para>
/// Shaxsiy ma'lumot NOMZODDAN ko'chadi va bu yerda qayta so'ralmaydi.
/// JSHSHIR va pasport esa aynan shu yerda: nomzod hujjatlarini ishga
/// qabul kuni keltiradi va dublikat qidiruvi aynan JSHSHIRga tayanadi.
/// </para>
/// <para>
/// <b>Birinchi ikkita maydonda sukut qiymat ATAYLAB yo'q.</b> Ular
/// baribir MAJBURIY, ya'ni sukut qiymatning ma'nosi yo'q; qo'yilsa esa
/// BUTUN OpenAPI hujjati yiqilardi — generator <c>Guid.Empty</c> ni
/// sxemadagi sukut qiymatga aylantira olmaydi va
/// <c>/openapi/v1.json</c> 500 qaytarardi (loyiha bundan bir marta jabr
/// ko'rgan: o'shanda ish taklifini qabul qilish so'rovi butun hujjatni
/// yiqitgan edi, 2026-08-29).
/// <c>System.Text.Json</c> yetishmayotgan maydonga <c>default(Guid)</c> ni
/// o'zi beradi va tekshiruv <c>HireCandidateCommandHandler</c> da qoladi.
/// </para>
/// </remarks>
/// <param name="LegalEntityId">
/// Qaysi firmaga rasmiylashtiriladi — «/api/catalog/legal-entities» dan.
/// MAJBURIY.
/// </param>
/// <param name="PositionId">
/// Qaysi lavozimga — «/api/catalog/positions» dan. MAJBURIY.
/// </param>
/// <param name="DepartmentId">
/// Qaysi bo'limga — «/api/catalog/departments» dan. Ixtiyoriy.
/// </param>
/// <param name="HireDate">Ishga qabul sanasi. Bo'sh — bugun.</param>
/// <param name="OrderNo">Ishga qabul buyrug'ining raqami.</param>
/// <param name="OrderDate">Buyruq sanasi.</param>
/// <param name="Pinfl">JSHSHIR (14 xona).</param>
/// <param name="Passport">Pasport seriyasi va raqami.</param>
/// <param name="EmployeeId">Mavjud xodim kartasi — QAYTA ishga olishda.</param>
/// <param name="SalaryAmount">Kelishilgan oylik ish haqi. Bo'sh — oklad yozuvisiz.</param>
/// <param name="SalaryCurrency">Valyuta. Bo'sh — UZS.</param>
/// <param name="Rate">Stavka: 1.0 — to'liq, 0.5 — o'rindoshlik.</param>
/// <param name="IsPrimary">Asosiy ish joyimi.</param>
/// <param name="ResponsibleEmployeeId">Biriktirilgan xodim (mentor).</param>
/// <param name="ProbationDays">Sinov muddati (kun). Bo'sh — lavozimdagi qiymat yoki 90.</param>
public sealed record HireCandidateRequest(
    Guid LegalEntityId,
    Guid PositionId,
    Guid? DepartmentId = null,
    DateOnly? HireDate = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null,
    string? Pinfl = null,
    string? Passport = null,
    Guid? EmployeeId = null,
    decimal? SalaryAmount = null,
    string? SalaryCurrency = null,
    decimal? Rate = null,
    bool? IsPrimary = null,
    Guid? ResponsibleEmployeeId = null,
    int? ProbationDays = null);

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
/// <b>Nomzodning uchta yakuni:</b> «ishga olindi»
/// (<c>POST /{id}/hire</c> — xodim kartasi shu yerda yaratiladi),
/// «rezerv» (<c>POST /{id}/reserve</c>, qaytarish —
/// <c>POST /{id}/return-from-reserve</c>) va «qabul qilinmadi»
/// (<c>POST /{id}/reject</c>). Nomzodning O'ZI voz kechgani ALOHIDA
/// yakun (<c>POST /{id}/withdraw</c>) — u korxonaning qarori emas.
/// </para>
/// <para>
/// Holatni QO'LDA «ishga olingan» qilib qo'yish amali ATAYLAB yo'q:
/// nomzod faqat HAQIQIY xodim kartasi yaratilganda yopiladi. Ilgari
/// mavjud bo'lgan ikkinchi yo'l — rasmiy ish taklifi orqali qabul —
/// butunlay olib tashlandi (<c>docs/SPEC-nomzod-yakuni.md</c>,
/// 3-bo'lim).
/// </para>
/// </remarks>
public static class CandidateEndpoints
{
    /// <summary>
    /// So'rov chegarasi fayl chegarasidan sal katta: multipart konvertida
    /// forma maydonlari ham joy egallaydi (xodim hujjatlaridagi bilan bir xil).
    /// </summary>
    private const long MaxResumeRequestBytes = DocumentLimits.MaxBytes + (64 * 1024);

    /// <summary>Surat so'rovining chegarasi — rasm chegarasi + multipart «konverti».</summary>
    private const long MaxPhotoRequestBytes = PhotoLimits.MaxBytes + (64 * 1024);

    public static IEndpointRouteBuilder MapCandidateEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/recruiting/candidates").WithTags("Recruiting");

        MapReads(group);
        MapWrites(group);
        MapFunnel(group);
        MapHire(group);
        MapFiles(group);
        MapPhoto(group);

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
                bool? includeArchive,

                /*
                  Ikkala bayroq ham `bool?`: `?contactDue=` (bo'sh) va
                  butunlay berilmagan hol ham NORMAL — «filtr yo'q» degani.
                  `bool` bo'lsa Minimal API bo'sh qiymatni 400 qilardi
                  (`includeArchive` bilan bir xil qoida).
                */
                bool? contactDue,
                bool? rejected,
                SearchCandidatesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new SearchCandidatesQuery(
                            search, vacancyId, stageId, status, source, page, pageSize, sort,
                            includeArchive ?? false, contactDue ?? false, rejected ?? false),
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
                "«status» — InProgress | Rejected | Hired | Withdrawn | Reserve. " +
                "⚠️ Filtr BERILMASA, ISHGA OLINGANLAR, RAD ETILGANLAR va O'ZI VOZ " +
                "KECHGANLAR ro'yxatga tushmaydi (va sanoqqa ham kirmaydi): " +
                "birinchilari endi xodim va ularning o'rni «Xodimlar» bo'limida, " +
                "qolganlari voronkadan chiqqan va ularning o'rni «Rad etilganlar» " +
                "bo'limida. Yozuv o'chirilmaydi — «status=…» tanlansa ular to'liq " +
                "qaytadi. REZERVDAGILAR ham sukut ro'yxatga tushmaydi — ularning " +
                "o'rni «Rezerv» bo'limi («status=Reserve»); «contactDue=true» esa " +
                "ularni qaytaradi (kunlik aloqa ro'yxati). «source» — " +
                "Manual | TelegramBot | Excel (noma'lum qiymat 400: «nomzod.holat_notogri» / " +
                "«nomzod.manba_notogri»). «sort» — createdAt (sukut, yangidan eskiga), " +
                "name, stage, nextContact (sanasi yo'q qatorlar IKKALA yo'nalishda ham " +
                "oxirida). Voronka ustuni: «?status=InProgress&stageId=…». " +
                "«includeArchive=true» — Excel'dan ko'chirilgan kengash arxivi ham " +
                "qo'shiladi; sukut bo'yicha u ro'yxatda KO'RINMAYDI, chunki ekran " +
                "«hozir kim bor?» degan savolga javob beradi. «source=Excel» tanlansa " +
                "arxiv bayroqsiz ham ochiladi — arxiv ochilganda rad etilganlar va voz " +
                "kechganlar ham qaytadi, chunki arxivning deyarli hammasi shu holatda. " +
                "«rejected=true» — «Rad etilganlar» BO'LIMI: aynan Rejected + Withdrawn " +
                "(ikkisi AYRIM holat bo'lib qoladi — belgisi ham, hisoboti ham alohida; " +
                "faqat ro'yxatda ko'rinishi umumiy). Bayroq holat filtri bilan birga " +
                "ham ishlaydi: «rejected=true&status=Withdrawn» faqat voz kechganlarni " +
                "beradi. Bo'lim Excel arxivini O'ZI tortmaydi — arxivning kirish yo'li " +
                "«source=Excel». " +
                "«contactDue=true» — keyingi aloqa sanasi qo'yilgan VA u BUGUN yoki " +
                "undan oldin bo'lgan nomzodlar («bugun kim bilan gaplashish kerak»); " +
                "«bugun» ni server Toshkent kalendarida hisoblaydi. " +
                "Har qatorda «hasResume» (kamida bitta FAYL biriktirilganmi), " +
                "«hasPhoto» va «nextContactDate» bor; ANKETA ro'yxatga QO'SHILMAYDI — " +
                "u og'ir va faqat kartada kerak.")
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
                "Bosqichlar TARIXI bilan («stages», yozilgan tartibda), " +
                "biriktirilgan FAYLLAR («files», YUKLASH tartibida — eskisidan " +
                "yangisiga; xodim hujjatlaridan ataylab teskari, chunki pasport " +
                "1-2-3 varaq bo'lib yuklanadi) va amal IZOHLARI («notes», yozilgan " +
                "tartibda — bosqichi bo'lmagan amallardan qolgan ICHKI qaydlar). " +
                "«nextContactDate» — HR keyingi marta qachon gaplashishni " +
                "rejalashtirgani (kalendar sanasi; o'tgan sana ODATIY hol). " +
                "Ishga olingan bo'lsa — " +
                "«hiredEmployeeId» va «hiredEmploymentId» to'ldiriladi. " +
                "Botda to'ldirilgan anketa «questionnaire» blokida qaytadi; u " +
                "«null» BO'LISHI ODATIY hol (HR qo'lda kiritgan yoki eski bot " +
                "yozuvi) va o'shanda interfeys anketa bo'limini umuman chizmaydi. " +
                "«hasPhoto» — surat bor-yo'qligi; rasmning o'zi «/photo» orqali olinadi.")
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
                            request.LastName,
                            request.FirstName,
                            request.VacancyId,
                            request.MiddleName,
                            request.BirthDate,
                            request.Phone,
                            request.Address,
                            request.Speciality,
                            request.Note,
                            request.StageId,
                            request.Source,
                            request.TelegramChatId,
                            request.TelegramLanguage,
                            request.Questionnaire),
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
                "«vacancyId» IXTIYORIY: berilmasa nomzod «Umumiy anketa» xizmat " +
                "vakansiyasiga tushadi — uning identifikatorini server o'zi biladi va " +
                "mijozga ochmaydi. Xizmat vakansiyasi sozlanmagan yoki yopilgan bo'lsa: " +
                "«nomzod.umumiy_anketa_yoq» (409). " +
                "Ixtiyoriy «questionnaire» bloki berilsa, anketa nomzod bilan BITTA " +
                "saqlashda yoziladi: u rad etilsa nomzod ham yaratilmaydi (yarim " +
                "yozuv qolmaydi). Blok berilmasa so'rov AYNAN avvalgidek ishlaydi. " +
                "Anketadagi «offerAcceptedAt» IXTIYORIY: berilmasa nomzoddan ommaviy " +
                "oferta bilan rozilik SO'RALMAGAN degani (oferta havolasi sozlanmagan " +
                "bo'lsa bot 21-qadamni umuman chizmaydi) va maydon bo'sh saqlanadi. " +
                "Xatolar: «bosqich.faol_emas» (409), «bosqich.faol_yoq» (409), " +
                "«ism.familiya_bosh» / «ism.ism_bosh» / «telefon.*» (400), " +
                "«anketa.malumot_notogri» / «anketa.oila_notogri» / «anketa.til_notogri» / " +
                "«anketa.maosh_notogri» / «anketa.manba_notogri» (400).")
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
                    .HandleAsync(
                        new ReassignCandidateCommand(
                            id, request.VacancyId, request.Note, request.NextContactDate),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("ReassignCandidate")
            .WithSummary("Nomzod — boshqa vakansiyaga o'tkazish")
            .WithDescription(
                "Bosqichlar tarixi SAQLANADI va joriy bosqich o'zgarmaydi. " +
                "Yangi vakansiya ochiq bo'lishi shart, nomzod esa jarayonda " +
                "(«nomzod.jarayonda_emas», 409). " +
                "«note» bo'sh bo'lmasa — nomzod izohlari tarixiga «boshqa vakansiyaga " +
                "o'tkazildi» turi bilan yoziladi (bo'sh bo'lsa yozuv umuman hosil " +
                "bo'lmaydi). Izoh ICHKI: nomzodga hech qachon yuborilmaydi. " +
                "«nextContactDate» berilsa keyingi aloqa sanasi qo'yiladi; berilmasa " +
                "saqlangan sana o'zgarmaydi — bu amalda «tozalash» yo'q.")
            .Produces<CandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        /*
          ⚠️ NOMZODNI O'CHIRISH ENDPOINT'I YO'Q va u ATAYLAB olib
          tashlangan (foydalanuvchi qarori, 2026-09-07): «hamma anketa
          turishi kerak, o'chirish mumkin emas».

          Ilgari yumshoq o'chirish bor edi va u «xato kiritilgan yozuv
          uchun» deb izohlangan. Amalda esa har bir anketa — nomzodning
          o'zi to'ldirgan ma'lumot va u kelasi izlanishning bazasi:
          bugun kerak bo'lmagan odam olti oydan keyin aynan kerak
          bo'lishi mumkin. Shu sababli yozuv YAKUNLANADI (rezerv, rad
          etish yoki ishga olish), lekin hech qachon yo'q qilinmaydi.

          Xato kiritilgan yozuv uchun tahrirlash bor; adashib
          kiritilgani esa rad etish bilan yopiladi.
        */
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
                            id,
                            request.StageId,
                            request.Date,
                            request.Conclusion,
                            request.NextContactDate,

                            // `bool?` → `bool`: sukut qiymat so'rov
                            // rekordida BO'LMASLIGI kerak (OpenAPI tuzog'i),
                            // buyruq esa oddiy `bool` bilan ishlaydi.
                            request.ClearNextContact ?? false),
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
                "(«conclusion» — xulosa matni; alohida «natija» maydoni YO'Q — " +
                "bosqichning O'ZI holatni aytadi). ⭐ HAR CHAQIRUV TARIXGA YANGI QATOR " +
                "QO'SHADI va mavjud yozuvning ustidan HECH QACHON yozmaydi: bitta " +
                "bosqich necha marta takrorlansa, shuncha qator bo'ladi («besh marta " +
                "gaplashildi — beshta yozuv»), nomzodning joriy holati esa oxirgi " +
                "yozuvdan kelib chiqadi. Ochib qo'yilgan yozuvga xulosa yozish uchun " +
                "«PUT /stages/{entryId}» ishlatiladi. " +
                "Voronka oldinga yurishga majburlamaydi: bosqichni o'tkazib yuborish " +
                "va orqaga qaytish mumkin. " +
                "«conclusion» BO'SH bo'lishi mumkin va bu xato emas: majburlash " +
                "«ok» degan yolg'on xulosalarni tug'dirardi. " +
                "Keyingi aloqa sanasi UCH holatli: «nextContactDate» berilsa " +
                "QO'YILADI, «clearNextContact=true» bo'lsa TOZALANADI (o'shanda " +
                "«nextContactDate» e'tiborsiz), ikkalasi ham berilmasa saqlangan " +
                "sana O'ZGARMAYDI — oxirgisi voronkadagi SUDRASH uchun: u oynasiz " +
                "ishlaydi va HR qo'ygan sanani jimgina o'chirib yuborishi mumkin " +
                "emas. Sana bosqich YOZILGANDAN keyin qo'yiladi: bosqich rad etilsa " +
                "sana ham o'zgarmaydi. " +
                "Xatolar: «bosqich.topilmadi» (404), " +
                "«bosqich.faol_emas» (409), «nomzod.jarayonda_emas» (409). " +
                "Javob — YANGILANGAN nomzod kartasi (butun tarixi bilan).")
            .Produces<CandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/stages/{entryId:guid}", async (
                Guid id,
                Guid entryId,
                EditCandidateStageEntryRequest request,
                EditCandidateStageEntryCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new EditCandidateStageEntryCommand(
                            id,
                            entryId,
                            request.Date,
                            request.Conclusion,
                            request.NextContactDate,
                            request.ClearNextContact ?? false),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("EditCandidateStageEntry")
            .WithSummary("Nomzod — tarixdagi bosqich yozuvini tuzatish")
            .WithDescription(
                "TARIXNING bitta qatorini tuzatadi: bo'sh qolgan yozuvga xulosa " +
                "yozish ham, ilgari yozilgan xulosadagi xatoni to'g'rilash ham shu " +
                "endpoint. Yangi qator QO'SHILMAYDI va nomzodning joriy bosqichi " +
                "O'ZGARMAYDI — tuzatish odamni voronkada surmaydi. Yozuvning " +
                "bosqichi ham o'zgarmaydi: noto'g'ri bosqich yozilgan bo'lsa, " +
                "to'g'risi «POST /stages» bilan YANGI qator bo'lib tushadi. " +
                "⚠️ «conclusion» ustidan yozadi — mijoz mavjud matnni to'ldirib " +
                "ochishi va o'zgarmagan bo'lsa qaytarib yuborishi shart. " +
                "Faolsizlantirilgan bosqichdagi va voronkadan chiqqan nomzodning " +
                "yozuvi ham tuzatiladi: tarix aynan shuni saqlash uchun bor. " +
                "Xatolar: «nomzod.topilmadi» / «bosqich_yozuvi.topilmadi» (404).")
            .Produces<CandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

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
        group.MapGet("/application-check", async (
                string? phone,
                long? telegramChatId,
                CheckApplicationQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new CheckApplicationQuery(phone, telegramChatId), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(AuthenticationExtensions.BotServicePolicy)
            .WithName("CheckApplication")
            .WithSummary("Bu odam yangi anketa topshira oladimi — faqat botga")
            .WithDescription(
                "Ikkita to'siq, SHU tartibda: (1) JARAYONDAGI ariza — muddatdan " +
                "qat'i nazar; (2) qayta ariza muddati — «recruiting.reapplyMonths» " +
                "sozlamasi (sukut 6 oy), hisob oxirgi arizaning YOPILGAN " +
                "sanasidan ketadi. " +
                "Ikkita kalit bo'yicha qidiriladi: telefon VA Telegram hisobi — " +
                "bir odamda ikkita SIM bo'lishi mumkin va u o'sha hisobdan yangi " +
                "raqam bilan qaytib kelardi. " +
                "Vakansiya bo'yicha AJRATILMAYDI: to'siq odamga qo'yiladi, arizaga " +
                "emas — aks holda bir odam har kuni boshqa vakansiyaga anketa " +
                "tashlayverardi. " +
                "Javobda faqat xabar yozish uchun kerakli maydonlar bor. " +
                "⚠️ Qoida faqat BOTGA tegishli: HR qo'lda nomzod kiritganda hech " +
                "qanday to'siq yo'q.")
            .Produces<ApplicationCheckDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

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

        /*
          ═══════════════════════════════════════════════════════════
           SUHBATGA CHAQIRISH — bitta amal, uchta natija.

           HR uchun bu bitta tugma: «Кенгашга чақирилди». Server esa
           uchta ishni BIR tranzaksiyada bajaradi — bosqichni yozadi,
           suhbat vaqtini qo'yadi va Telegram xabarini navbatga
           tashlaydi. Ularni ajratish «bosqich yozildi, xabar
           ketmadi» degan yarim holatni tug'dirardi.
          ═══════════════════════════════════════════════════════════
        */
        group.MapPost("/{id:guid}/interview-invite", async (
                Guid id,
                InviteCandidateToInterviewRequest request,
                InviteCandidateToInterviewCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new InviteCandidateToInterviewCommand(
                            id,
                            request.StageId,
                            request.InterviewAt,
                            request.Conclusion,
                            request.NextContactDate,
                            request.ClearNextContact ?? false),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("InviteCandidateToInterview")
            .WithSummary("Nomzod — suhbatga chaqirish")
            .WithDescription(
                "Suhbat vaqtini yozadi va nomzodga Telegram TAKLIFINI navbatga qo'yadi. " +
                "«stageId» berilsa nomzod o'sha bosqichga o'tadi («kutilmoqda» natijasi " +
                "bilan, sana — SUHBAT kuni); berilmasa bosqich o'zgarmaydi — suhbatni " +
                "KO'CHIRISH aynan shunday ishlaydi va nomzod yangi vaqt bilan yangi " +
                "xabar oladi. Telegram suhbati yo'q nomzodda (HR qo'lda kiritgan) vaqt " +
                "baribir yoziladi, xabar esa yuborilmaydi — yuboradigan manzil yo'q. " +
                "⭐ «conclusion» ning IKKITA manzili bor va qaysi biriga tushishini " +
                "SERVER hal qiladi: «stageId» berilsa — yangi bosqich yozuvining " +
                "xulosasi, berilmasa (ko'chirish) — nomzod izohlari tarixida «suhbat " +
                "ko'chirildi» turidagi yozuv. Mijoz doim BITTA matn maydonini " +
                "yuboradi. Matn ICHKI: nomzodga ketadigan xabar qat'iy shabloncha " +
                "va unda izoh YO'Q. " +
                "«nextContactDate» / «clearNextContact» — «/stages» dagi bilan aynan " +
                "bir xil uch holatli qoida. Tartib: avval SUHBAT VAQTI tekshiriladi, " +
                "keyin bosqich, keyin izoh, eng oxirida aloqa sanasi — o'tgan vaqt " +
                "kiritilganda hech narsa yozilmasligi kerak. " +
                "Xatolar: «nomzod.topilmadi» (404), «bosqich.topilmadi» (404), " +
                "«bosqich.faol_emas» (409), «nomzod.jarayonda_emas» (409), " +
                "«nomzod.suhbat_vaqti_otgan» (400). Javob — yangilangan nomzod kartasi.")
            .Produces<CandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/interview-notices", async (
                int? limit,
                ListPendingInterviewNoticesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListPendingInterviewNoticesQuery(limit ?? 20), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(AuthenticationExtensions.BotServicePolicy)
            .WithName("ListPendingInterviewNotices")
            .WithSummary("Suhbat taklifi kutayotgan nomzodlar — faqat botga")
            .WithDescription(
                "Suhbatga chaqirilgan, Telegram suhbati ma'lum, voronkada turgan va " +
                "taklifni hali olmagan nomzodlar. Tartib SUHBAT VAQTI bo'yicha: eng " +
                "yaqini birinchi javob oladi. Vaqt UTC da qaytadi — Toshkent vaqtiga " +
                "botning o'zi keltiradi. «limit» — ko'pi bilan 50.")
            .Produces<IReadOnlyList<InterviewNoticeDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/{id:guid}/interview-notice-sent", async (
                Guid id,
                MarkInterviewNoticeSentCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new MarkInterviewNoticeSentCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(AuthenticationExtensions.BotServicePolicy)
            .WithName("MarkInterviewNoticeSent")
            .WithSummary("Suhbat taklifi yuborildi — faqat botga")
            .WithDescription(
                "«rejection-notice-sent» bilan bir xil qoida: bot xabarni HAQIQATAN " +
                "yuborgandan KEYIN chaqiradi. Farqi bittada — belgi suhbat vaqti " +
                "QAYTA qo'yilganda tozalanadi va nomzod yangi vaqt bo'yicha yangi " +
                "xabar oladi. Xato: «nomzod.topilmadi» (404).")
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
                "Nomzod voronkadan chiqadi va faol ro'yxatlarda ko'rinmaydi — u " +
                "«Rad etilganlar» bo'limiga o'tadi («?rejected=true»). " +
                "«at» bo'sh — bugun. Keyingi aloqa sanasi TOZALANADI: rad etilgan " +
                "nomzod «bugun qo'ng'iroq qilinadiganlar» ro'yxatida abadiy turib " +
                "qolsa, HR o'sha ro'yxatga ishonmay qo'yardi. Shu sababli tanadagi " +
                "«nextContactDate» bu yerda e'tiborsiz qoldiriladi (xato emas). " +
                "Ikkinchi marta yopib bo'lmaydi: «nomzod.jarayonda_emas» (409).")
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
                "sifati, ikkinchisi taklif shartlari haqida gapiradi). " +
                "⚠️ Nomzod endi kundalik ro'yxatdan ham CHIQADI (2026-09-10 qarori) va " +
                "«Rad etilganlar» bo'limida rad etilganlar bilan BIRGA ko'rinadi " +
                "(«?rejected=true»); holat esa alohida qoladi — belgisi ham, hisoboti " +
                "ham boshqa. «?status=Withdrawn» aynan shularni alohida beradi. " +
                "Keyingi aloqa sanasi TOZALANADI — «/reject» dagi bilan bir xil qoida.")
            .Produces<CandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reserve", async (
                Guid id,
                CloseCandidateRequest? request,
                CloseCandidateCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        // Aloqa sanasi FAQAT shu yakunga uzatiladi (izohi
                        // `CloseCandidateRequest.NextContactDate` da).
                        new ReserveCandidateCommand(
                            id, request?.At, request?.Reason, request?.NextContactDate),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("ReserveCandidate")
            .WithSummary("Nomzod — rezervga o'tkazish")
            .WithDescription(
                "«Hozir olinmadi, lekin kerak bo'lsa chaqiramiz». Nomzod voronkadan " +
                "chiqadi va voronka taxtasida («?status=InProgress») ko'rinmaydi, " +
                "lekin RAD ETILGAN emas: «Rezerv» bo'limida («?status=Reserve») " +
                "turadi, sukut nomzodlar ro'yxatida esa ko'rinmaydi, va bir amal bilan voronkaga " +
                "qaytariladi («/return-from-reserve»). Ajratish ataylab — " +
                "«bizga to'g'ri kelmadi» va «yaxshi odam, lekin bugun o'rin yo'q» " +
                "butunlay boshqa qaror va ikkinchisiga QAYTILADI. " +
                "«at» bo'sh — bugun; «reason» nomzod kartasida ko'rinadi va " +
                "qaytarilganda TOZALANADI (u chiqish sababi, uzoq muddatli tavsif emas). " +
                "⭐ Keyingi aloqa sanasi SAQLANADI — yakunlar orasida yagona joy: " +
                "«nextContactDate» berilsa qo'yiladi, berilmasa MAVJUDI o'z joyida " +
                "qoladi. Sabab: rezerv chiqish emas, KECHIKTIRISH («o'rin bo'shasa " +
                "chaqiramiz»), ya'ni qo'ng'iroq rejasi aynan o'sha yerda o'rinli. " +
                "Faqat JARAYONDAGI nomzod o'tkaziladi: ikkinchi marta yopib " +
                "bo'lmaydi — «nomzod.jarayonda_emas» (409).")
            .Produces<CandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/return", async (
                Guid id,

                // Tana IXTIYORIY (`?`) — rezervdan qaytarish uni umuman
                // yubormasligi mumkin (yopish amallaridagi
                // `CloseCandidateRequest?` bilan bir xil naqsh).
                ReturnCandidateToFunnelRequest? request,
                ReturnCandidateToFunnelCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ReturnCandidateToFunnelCommand(
                            id, request?.Note, request?.NextContactDate),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("ReturnCandidateToFunnel")
            .WithSummary("Nomzod — voronkaga qaytarish")
            .WithDescription(
                "Voronkadan chiqqan nomzodni QAYTARADI: rezervdan, RAD ETILGANDAN " +
                "va o'zi VOZ KECHGANDAN. Rad etilgan odam ko'pincha o'zi qayta " +
                "aloqaga chiqadi yoki sharoit o'zgaradi — noldan kiritish esa butun " +
                "tarixni, anketani va fayllarni yo'qotardi. " +
                "Nomzod QAYSI bosqichda to'xtagan bo'lsa, o'sha yerdan davom etadi — " +
                "bosqichlar tarixi va joriy bosqich saqlanadi. Chiqish sanasi, sababi " +
                "va rad etish xabarining belgisi tozalanadi (nomzod yana rad etilsa, " +
                "Telegram xabari qayta yuborilishi kerak). " +
                "«Ishga olingan» nomzod QAYTARILMAYDI — «nomzod.qaytarilmaydi» (409): " +
                "u bir vaqtda ham xodim, ham faol nomzod bo'lib qolardi. " +
                "⭐ «note» — QAYTARISH SABABI: rad etilgan va voz kechgan nomzodda " +
                "MAJBURIY («nomzod.qaytarish_sababi_yoq», 400), rezervdan qaytarishda " +
                "ixtiyoriy. U nomzod lentasiga «voronkaga qaytarildi» turi bilan " +
                "yoziladi va u yerdan hech qachon o'chmaydi; «closingNote» ga " +
                "yozilMAYDI, chunki qaytarish o'sha tranzaksiyada uni tozalaydi. " +
                "«nextContactDate» berilsa qo'yiladi, berilmasa mavjudi O'Z KUCHIDA " +
                "qoladi.")
            .Produces<CandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    }

    /// <summary>
    /// Nomzoddan xodimga o'tish — voronkaning YAKUNIY qadami.
    /// </summary>
    /// <remarks>
    /// Alohida guruh: bu yerdagi yagona endpoint 1-bosqichning ishga qabul
    /// oqimini chaqiradi va shu bilan boshqa barcha nomzod amallaridan
    /// farq qiladi — u rekruting jadvallaridan tashqariga, xodimlar
    /// bo'limiga yozadi.
    /// </remarks>
    private static void MapHire(IEndpointRouteBuilder group)
    {
        group.MapPost("/{id:guid}/hire", async (
                Guid id,
                HireCandidateRequest? request,
                HireCandidateCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                // Tana umuman yuborilmagan hol: bo'sh identifikatorlar bilan
                // davom etamiz va tekshiruvni `HireCandidateCommandHandler`
                // ga qoldiramiz — u ikkalasini ham `Guid.Empty` bo'yicha rad
                // etadi va ma'noli xato kodini qaytaradi. Bu yerda alohida
                // 400 yozish o'sha qoidani ikkinchi joyda takrorlash bo'lardi.
                var body = request ?? new HireCandidateRequest(Guid.Empty, Guid.Empty);

                var natija = await handler
                    .HandleAsync(
                        new HireCandidateCommand(
                            id,
                            body.LegalEntityId,
                            body.PositionId,
                            body.DepartmentId,
                            body.HireDate,
                            body.OrderNo,
                            body.OrderDate,
                            body.Pinfl,
                            body.Passport,
                            body.EmployeeId,
                            body.SalaryAmount,
                            body.SalaryCurrency,
                            body.Rate,
                            body.IsPrimary,
                            body.ResponsibleEmployeeId,
                            body.ProbationDays),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            // Ikkala ruxsat ham SHART (siyosatlar «va» bilan birlashadi): bu amal
            // xodim, mehnat munosabati va buyruq yaratadi, ya'ni «Ishga qabul
            // qilish» ruxsatini chetlab o'tib bo'lmasligi kerak (2026-09-26).
            .RequireAuthorization(Permissions.RecruitingManage, Permissions.EmploymentHire, Permissions.EmployeesCreate)
            .WithName("HireCandidate")
            .WithSummary("Nomzod — ishga olish (nomzod XODIMGA aylanadi)")
            .WithDescription(
                "Ruxsat: «recruiting.manage», «employment.hire» VA «employees.create». " +
                "Voronkaning yakuniy qadami. BITTA tranzaksiyada: nomzod «ishga " +
                "olingan» bo'lib voronkadan chiqadi va 1-bosqichning ishga qabul " +
                "oqimi ishga tushadi — xodim kartasi, mehnat munosabati, ishga qabul " +
                "buyrug'i va MAJBURIY sinov muddati. " +
                "MAJBURIY maydon IKKITA: «legalEntityId» va «positionId» " +
                "(«nomzod.yuridik_shaxs_yoq» / «nomzod.lavozim_yoq», 400); mavjud " +
                "bo'lmagan bo'lim uchun «nomzod.bolim_topilmadi» (404). " +
                "Qolganining zaxirasi bor: ishga qabul sanasi — bugun, sinov muddati — " +
                "lavozimdagi qiymat yoki 90 kun, valyuta — UZS. " +
                "F.I.Sh./telefon/tug'ilgan sana/manzil/mutaxassislik NOMZODDAN " +
                "ko'chadi; JSHSHIR va pasport shu formada kiritiladi va dublikat " +
                "qidiruvi aynan JSHSHIRga tayanadi (topilsa IKKINCHI karta " +
                "ochilmaydi). " +
                "«probationDays» 0 yoki manfiy bo'lsa «sinov.majburiy» (400): " +
                "sinovsiz qabul YO'Q. Nomzod allaqachon ishga olingan bo'lsa — " +
                "«nomzod.allaqachon_ishga_olingan» (409); voronkadan chiqqan " +
                "(rad etilgan, voz kechgan yoki REZERVDAGI) nomzod uchun " +
                "«nomzod.jarayonda_emas» (409) — rezervdagini avval " +
                "«/return-from-reserve» bilan qaytarish kerak; qora ro'yxatdagi " +
                "odam uchun «xodim.qora_royxatda» (409). " +
                "Javob: «candidate» + «hire» (ishga qabulning odatiy natijasi, " +
                "lavozim vazifalari va ogohlantirishlar bilan).")
            .Produces<DirectHireResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    /// <summary>
    /// Nomzod fayllari — o'ntagacha: rezyume, pasport varaqlari, diplom skani.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Marshrut bo'lagi <c>files</c> (KO'PLIK), xato kodlari esa
    /// <c>nomzod.rezyume_*</c> bo'lib qoldi.</b> Ziddiyat ataylab:
    /// to'plamni birlik nom bilan atash har bir keyingi o'quvchini
    /// adashtirardi, xato kodlari esa allaqachon TO'RT tilga tarjima
    /// qilingan va «rezyume» — HR ning o'z so'zi.
    /// </para>
    /// <para>
    /// <b>Eski uchlik (<c>POST|GET|DELETE /{id}/resume</c>) O'CHIRILDI</b> va
    /// «eskirgan taxallus» sifatida ham qoldirilmadi: ular endi mavjud
    /// bo'lmagan <c>candidates.resume_*</c> ustunlarini o'qirdi.
    /// </para>
    /// <para>
    /// <b>Ro'yxat endpointi YO'Q</b>: fayllar nomzod kartasi javobining
    /// ichida keladi (<c>CandidateDto.files</c>) — anketa va bosqichlar
    /// bilan bir xil qoida. Xodim hujjatlarida ro'yxat BOR, chunki u o'z
    /// tabi va turi bo'yicha filtri bilan yashaydi; bu yerda ikkalasi ham
    /// yo'q.
    /// </para>
    /// <para>
    /// <b>O'qish <c>recruiting.view</c>, yozish <c>recruiting.manage</c></b> —
    /// nomzod kartasidagi qolgan hamma narsa bilan bir xil (surat bo'limidagi
    /// izohga qarang: uchinchi ruxsat kiritilmaydi).
    /// </para>
    /// </remarks>
    private static void MapFiles(IEndpointRouteBuilder group)
    {
        group.MapPost("/{id:guid}/files", async (
                Guid id,
                IFormFile file,
                UploadCandidateFileCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                await using var content = file.OpenReadStream();

                var natija = await handler
                    .HandleAsync(
                        new UploadCandidateFileCommand(id, content, file.FileName, file.Length),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)

            // CSRF himoyasi bu yerda ORTIQCHA — JWT sarlavhada, sessiya
            // cookie'da emas (xodim hujjatlaridagi bilan bir xil sabab).
            .DisableAntiforgery()

            // Cheklov marshrut darajasida: chegaradan katta tana Kestrel
            // tomonidan UMUMAN o'qilmaydi. Doimiyning nomi («…Resume…»)
            // ATAYLAB o'zgartirilmadi — qiymati aynan bir xil va qayta
            // nomlash o'quvchiga hech narsa bermasdi.
            .WithMetadata(new RequestSizeLimitAttribute(MaxResumeRequestBytes))
            .WithName("UploadCandidateFile")
            .WithSummary("Nomzod — fayl yuklash")
            .WithDescription(
                "«multipart/form-data», maydon nomi «file». BITTA so'rov — BITTA fayl: " +
                "N ta fayl bir so'rovda kelsa, bitta yaroqsizi yo butun to'plamni " +
                "yiqitardi, yo «qisman muvaffaqiyat» degan holatni tug'dirardi. " +
                "Interfeys tanlangan fayllarni KETMA-KET yuboradi. " +
                "Fayl turi MAZMUNIGA qarab aniqlanadi (kengaytma va mijoz e'lon " +
                "qilgan sarlavhaga ishonilmaydi): PDF, JPEG, PNG yoki WebP, eng " +
                "ko'pi 20 MB. RASM serverda kichraytiriladi — eng uzun tomoni 2000 px, " +
                "natija doim JPEG; PDF esa BAYTMA-BAYT saqlanadi. " +
                "Nomzodda eng ko'pi 10 fayl va jami 60 MB (SAQLANGAN hajm bo'yicha) " +
                "bo'ladi. Javobdagi «sizeBytes» — diskka TUSHGAN hajm, yuklangan " +
                "fayl hajmi emas: 4 MB lik telefon surati ~300 KB bo'lib qoladi. " +
                "Javob — YARATILGAN FAYL, butun karta emas (kartani interfeys " +
                "keshni bekor qilish orqali yangilaydi). " +
                "Xatolar: «nomzod.rezyume_katta», «nomzod.rezyume_yaroqsiz», " +
                "«nomzod.rezyume_kop», «nomzod.rezyume_joy» (hammasi 400), " +
                "«nomzod.topilmadi» (404).")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<CandidateFileDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/files/{fileId:guid}", async (
                // ⚠️ IKKALA identifikator ham ISHLATILADI (xodim
                // hujjatlaridan ataylab farq: u yerda `employeeId` faqat
                // OpenAPI hujjati yaroqli bo'lishi uchun turadi). Fayl
                // nomzodning agregati ichidan olinadi, ya'ni chalkash
                // juftlik 404 beradi va boshqa odamning pasport nusxasi
                // berilmaydi.
                Guid id,
                Guid fileId,
                GetCandidateFileQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetCandidateFileQuery(id, fileId), cancellationToken)
                    .ConfigureAwait(false);

                // JSON emas, BAYT qaytaradi — xodim hujjatidagi bilan bir xil.
                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.File(natija.Value.Content, natija.Value.ContentType, natija.Value.FileName);
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("DownloadCandidateFile")
            .WithSummary("Nomzod — faylni yuklab olish")
            .WithDescription(
                "Fayl bayti. «Content-Disposition» da ASL fayl nomi qaytadi. " +
                "Fayl AYNAN shu nomzodga tegishli bo'lishi shart: noma'lum " +
                "identifikator ham, boshqa nomzodning fayli ham bir xil javob " +
                "beradi — «nomzod.rezyume_topilmadi» (404). Yozuv bor-u diskda " +
                "fayl yo'q bo'lsa ham javob shu: buzilgan holat mijozga 500 emas, " +
                "«fayl yo'q» bo'lib ko'rinishi kerak. " +
                "⚠️ KESH sarlavhalari ATAYLAB yo'q (suratdan farqi shunda): fayl " +
                "shaxsiy ma'lumot va u baribir ekran ochiq bo'lgan vaqtda mijoz " +
                "keshida turadi. Kesh qo'shilsa, u faqat «private» bo'lishi mumkin.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/files/{fileId:guid}", async (
                Guid id,
                Guid fileId,
                DeleteCandidateFileCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeleteCandidateFileCommand(id, fileId), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("DeleteCandidateFile")
            .WithSummary("Nomzod — faylni olib tashlash")
            .WithDescription(
                "Yozuv ham, diskdagi baytlar ham HAQIQATAN o'chiriladi (yumshoq " +
                "o'chirish YO'Q: ko'rinmaydigan yozuv o'nta o'rindan bittasini " +
                "jimgina band qilib turardi va yuklash sababsiz to'xtardi). Fayl " +
                "SAQLASHDAN KEYIN o'chadi: saqlash yiqilsa, yozuv joyida turgan " +
                "faylga ishora qilib qolishi kerak. " +
                "⚠️ Amal IDEMPOTENT EMAS (suratni o'chirishdan farqi shunda): " +
                "noma'lum «fileId» uchun «nomzod.rezyume_topilmadi» (404). " +
                "Chaqiruvchi ANIQ bir qatorni nomlagan; jimgina «bajarildi» deyish " +
                "allaqachon yo'q bo'lgan faylni ko'rsatib turgan eskirgan ekranni " +
                "yashirardi. Xato: «nomzod.topilmadi» (404).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Nomzod surati — botdagi anketada so'raladigan selfi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Quvur xodim rasmi bilan AYNAN bitta</b> (<c>SquarePhotoStore</c> +
    /// <c>PhotoHttp</c>): bir xil chegara, bir xil 512×512 JPEG, bir xil
    /// <c>ETag</c> va kesh siyosati. Ikkinchi quvur yozilsa, xavfsizlik
    /// tuzatishi ikki joyda kerak bo'lardi.
    /// </para>
    /// <para>
    /// <b>O'qish <c>recruiting.view</c>, yozish <c>recruiting.manage</c></b> —
    /// nomzod kartasidagi qolgan hamma narsa bilan bir xil: nomzodni ko'ra
    /// olgan odam suratini ham ko'radi, rasmni esa faqat kartani
    /// boshqaradigan odam almashtiradi. Botning o'zi ham
    /// <c>recruiting.manage</c> bilan yuklaydi — u nomzod yozuvini
    /// yaratayotgan tomonda turibdi.
    /// </para>
    /// </remarks>
    private static void MapPhoto(IEndpointRouteBuilder group)
    {
        group.MapGet("/{id:guid}/photo", async (
                Guid id,
                HttpContext context,
                GetCandidatePhotoQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetCandidatePhotoQuery(id, PhotoHttp.ReadKnownVersion(context.Request)),
                        cancellationToken)
                    .ConfigureAwait(false);

                // JSON emas, BAYT qaytaradi — shu sababli `ToHttpResult()`
                // faqat xato yo'lida (xodim rasmidagi bilan bir xil sabab:
                // aks holda u oqimni obyekt deb serializatsiya qilardi).
                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : PhotoHttp.Write(context, natija.Value);
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("GetCandidatePhoto")
            .WithSummary("Nomzod — surati")
            .WithDescription(
                "Rasm baytlari (image/jpeg). «ETag» saqlash kalitidan olingan barqaror xesh; " +
                "«If-None-Match» mos kelsa 304 qaytadi. Kesh SHAXSIY (private, 1 kun) — " +
                "surat shaxsiy ma'lumot va umumiy proksida saqlanmasligi kerak. " +
                "Xatolar: «nomzod.topilmadi» (404), «nomzod.rasm_yoq» (404) — ikkinchisi " +
                "XATO EMAS, interfeys o'rinbosar chizadi.")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/photo", async (
                Guid id,
                IFormFile file,
                UploadCandidatePhotoCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                await using var content = file.OpenReadStream();

                var natija = await handler
                    .HandleAsync(
                        new UploadCandidatePhotoCommand(id, content, file.ContentType, file.Length),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)

            // CSRF himoyasi bu yerda ORTIQCHA — JWT sarlavhada, sessiya
            // cookie'da emas (rezyume yuklashdagi bilan bir xil sabab).
            .DisableAntiforgery()

            // Cheklov marshrut darajasida: chegaradan katta tana Kestrel
            // tomonidan umuman o'qilmaydi.
            .WithMetadata(new RequestSizeLimitAttribute(MaxPhotoRequestBytes))
            .WithName("UploadCandidatePhoto")
            .WithSummary("Nomzod — surat yuklash")
            .WithDescription(
                "«multipart/form-data», maydon nomi «file». JPEG, PNG yoki WebP, eng ko'pi 10 MB. " +
                "Server rasmni markazdan kvadrat qirqib 512×512 JPEG qiladi va ESKISINI diskdan " +
                "o'chiradi — manba fayl saqlanmaydi. Javobda faqat saqlash kaliti qaytadi: " +
                "rasmning o'zi odatdagi «GET .../photo» orqali olinadi. " +
                "Xatolar: «nomzod.rasm_katta», «nomzod.rasm_turi», «nomzod.rasm_yaroqsiz» " +
                "(hammasi 400), «nomzod.topilmadi» (404).")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<CandidatePhotoDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/photo", async (
                Guid id,
                DeleteCandidatePhotoCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeleteCandidatePhotoCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("DeleteCandidatePhoto")
            .WithSummary("Nomzod — suratni o'chirish")
            .WithDescription(
                "Fayl ham o'chadi. Amal IDEMPOTENT: surat allaqachon yo'q bo'lsa ham 204 " +
                "qaytadi — so'rovning maqsadi rasm qolmasligi, u esa bajarilgan. " +
                "Xato: «nomzod.topilmadi» (404).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
