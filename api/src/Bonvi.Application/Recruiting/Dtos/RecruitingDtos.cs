using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Dtos;

/// <summary>
/// Voronka bosqichi.
/// </summary>
/// <remarks>
/// Boshqa ma'lumotnoma DTO'laridan yagona farqi — <see cref="SortOrder"/>.
/// Sabab <see cref="PipelineStage.SortOrder"/> izohida: voronkada tartib
/// ma'lumotning o'zi, alifbo emas.
/// </remarks>
/// <param name="Id">Identifikator.</param>
/// <param name="Name">Nom.</param>
/// <param name="IsActive">Faolmi — faol emasiga yangi nomzod o'tkazilmaydi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="SortOrder">Voronkadagi o'rin: kichik qiymat — oldinroq.</param>
public sealed record PipelineStageDto(
    Guid Id,
    string Name,
    bool IsActive,
    string? Note,
    int SortOrder);

/// <summary>
/// Vakansiya — ro'yxat va karta uchun umumiy ko'rinish.
/// </summary>
/// <remarks>
/// <para>
/// Identifikator YONIDA nom ham qaytadi — butun loyihadagi bir xil
/// naqsh: mijozda identifikatorni nomga aylantiradigan hech narsa yo'q.
/// </para>
/// <para>
/// <b>Lavozim, bo'lim va firma maydonlari YO'Q va bu ataylab.</b>
/// Vakansiya — e'lon; kompaniya tuzilmasidan unda hech narsa
/// saqlanmaydi (batafsil sabab <see cref="Vacancy"/> izohida). Shu
/// uchtasi taklif QABUL QILINGANDA tanlanadi va o'sha yerdan
/// <c>HireEmployeeCommand</c> ga tushadi.
/// </para>
/// </remarks>
/// <param name="Id">Identifikator.</param>
/// <param name="VacancyTitleId">Vakansiya nomi — ma'lumotnoma yozuvi.</param>
/// <param name="Title">Vakansiya nomining matni.</param>
/// <param name="Openings">Nechta odam kerak.</param>
/// <param name="Status">Open | Closed.</param>
/// <param name="OpenedAt">Ochilgan sana.</param>
/// <param name="ClosedAt">Yopilgan sana.</param>
/// <param name="ResponsibleEmployeeId">Mas'ul xodim.</param>
/// <param name="ResponsibleName">Mas'ul xodimning ismi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="CandidateCount">Vakansiyaga kiritilgan nomzodlar soni (jami).</param>
/// <param name="HiredCount">
/// Shu vakansiyadan ishga olinganlar soni. <see cref="Openings"/> bilan
/// birga «2/3 to'ldirildi» ko'rinishini beradi — vakansiya O'ZI yopilmaydi
/// (sababi <see cref="Vacancy"/> izohida), qaror HR'niki.
/// </param>
public sealed record VacancyDto(
    Guid Id,
    Guid VacancyTitleId,
    string? Title,
    int Openings,
    VacancyStatus Status,
    DateOnly OpenedAt,
    DateOnly? ClosedAt,
    Guid? ResponsibleEmployeeId,
    string? ResponsibleName,
    string? Note,
    int CandidateCount,
    int HiredCount);

/// <summary>Nomzodning bitta bosqichdagi yozuvi.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="StageId">Bosqich.</param>
/// <param name="Stage">Bosqich nomi — faolsizlantirilgan bosqich uchun ham qaytadi.</param>
/// <param name="Date">Bosqich sanasi.</param>
/// <param name="Outcome">Pending | Passed | Failed.</param>
/// <param name="Conclusion">Xulosa matni (kengash xulosasi, suhbat natijasi).</param>
/// <param name="RecordedBy">Kim yozdi — tizim foydalanuvchisi.</param>
/// <param name="RecordedAt">Qachon yozildi.</param>
public sealed record CandidateStageEntryDto(
    Guid Id,
    Guid StageId,
    string? Stage,
    DateOnly Date,
    StageOutcome Outcome,
    string? Conclusion,
    Guid? RecordedBy,
    DateTimeOffset RecordedAt);

/// <summary>
/// Rad etish xabari kutayotgan nomzod — FAQAT bot uchun.
/// </summary>
/// <remarks>
/// <para>
/// Maydonlar ataylab OZ: bot xabar yozish uchun kimga, qaysi tilda va
/// qaysi vakansiya haqida yozishini bilishi kifoya. Nomzodning
/// telefoni, manzili va ichki izohlari bu javobga TUSHMAYDI — bot
/// ularni hech qayerda ishlatmaydi va ular botning jurnaliga
/// tushib qolishi mumkin bo'lgan ortiqcha shaxsiy ma'lumot bo'lardi.
/// </para>
/// <para>
/// Ismi esa KERAK: xabar «Hurmatli Каримов Азиз» deb boshlanadi va
/// nomzod uni o'ziga tegishli deb tanidi.
/// </para>
/// </remarks>
/// <param name="CandidateId">Xabar yuborilgach shu bilan belgilanadi.</param>
/// <param name="ChatId">Telegram suhbatining raqami.</param>
/// <param name="Language">Nomzod tanlagan til: <c>uz-Cyrl</c>, <c>uz-Latn</c>, <c>ru</c>.</param>
/// <param name="FullName">Nomzodning to'liq ismi.</param>
/// <param name="VacancyTitle">Qaysi vakansiyaga ariza bergani.</param>
public sealed record RejectionNoticeDto(
    Guid CandidateId,
    long ChatId,
    string? Language,
    string FullName,
    string VacancyTitle);

/// <summary>
/// Nomzodlar ro'yxatining bitta qatori.
/// </summary>
/// <remarks>
/// Bosqichlar tarixi va takliflar bu yerda YO'Q: ro'yxat yuzlab qatorni
/// tortadi va har biriga tarix yig'ish keraksiz yuk bo'lardi
/// (<c>EmployeeListItemDto</c> bilan bir xil qoida). Ular kartada.
/// </remarks>
/// <param name="Id">Identifikator.</param>
/// <param name="FullName">To'liq F.I.Sh.</param>
/// <param name="ShortName">Qisqa ko'rinish: «Каримов А.».</param>
/// <param name="Phone">Telefon.</param>
/// <param name="BirthDate">Tug'ilgan sana.</param>
/// <param name="Speciality">Mutaxassisligi.</param>
/// <param name="VacancyId">Qaysi vakansiyaga.</param>
/// <param name="Vacancy">Vakansiyaning nomi (e'lon nomi).</param>
/// <param name="CurrentStageId">Joriy bosqich.</param>
/// <param name="CurrentStage">Joriy bosqich nomi.</param>
/// <param name="Status">InProgress | Rejected | Hired | Withdrawn.</param>
/// <param name="Source">Manual | TelegramBot.</param>
/// <param name="HasResume">Rezyume biriktirilganmi.</param>
/// <param name="CreatedAt">Nomzod kiritilgan vaqt.</param>
/// <param name="HiredEmployeeId">
/// Ishga olingan bo'lsa — yaratilgan XODIM kartasi. Bog'lanish ishga qabul
/// hodisasidan olinadi (<c>EmploymentEvent.CandidateId</c>).
/// </param>
public sealed record CandidateListItemDto(
    Guid Id,
    string FullName,
    string ShortName,
    string? Phone,
    DateOnly? BirthDate,
    string? Speciality,
    Guid VacancyId,
    string? Vacancy,
    Guid? CurrentStageId,
    string? CurrentStage,
    CandidateStatus Status,
    CandidateSource Source,
    bool HasResume,
    DateTimeOffset CreatedAt,
    Guid? HiredEmployeeId);

/// <summary>Nomzod kartasi — bosqichlar tarixi va takliflari bilan.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="LastName">Familiya.</param>
/// <param name="FirstName">Ism.</param>
/// <param name="MiddleName">Otasining ismi.</param>
/// <param name="FullName">To'liq F.I.Sh.</param>
/// <param name="BirthDate">Tug'ilgan sana.</param>
/// <param name="Phone">Telefon.</param>
/// <param name="Address">Manzil.</param>
/// <param name="Speciality">Mutaxassisligi.</param>
/// <param name="VacancyId">Qaysi vakansiyaga.</param>
/// <param name="Vacancy">Vakansiyaning nomi (e'lon nomi).</param>
/// <param name="CurrentStageId">Joriy bosqich.</param>
/// <param name="CurrentStage">Joriy bosqich nomi.</param>
/// <param name="Status">Voronkadagi holati.</param>
/// <param name="Source">Qayerdan kelgan.</param>
/// <param name="Note">Izoh.</param>
/// <param name="ClosedAt">Voronkadan chiqqan sana.</param>
/// <param name="ClosingNote">Chiqish sababi.</param>
/// <param name="ResumeFileName">Rezyume fayl nomi. <c>null</c> — rezyume yo'q.</param>
/// <param name="ResumeContentType">Rezyume MIME turi.</param>
/// <param name="ResumeSizeBytes">Rezyume hajmi.</param>
/// <param name="HiredEmployeeId">Ishga olingan bo'lsa — xodim kartasi.</param>
/// <param name="HiredEmploymentId">Ishga olingan bo'lsa — mehnat munosabati.</param>
/// <param name="Stages">Bosqichlar tarixi — yozilgan tartibda.</param>
/// <param name="Offers">Ish takliflari — yangidan eskiga.</param>
public sealed record CandidateDto(
    Guid Id,
    string LastName,
    string FirstName,
    string? MiddleName,
    string FullName,
    DateOnly? BirthDate,
    string? Phone,
    string? Address,
    string? Speciality,
    Guid VacancyId,
    string? Vacancy,
    Guid? CurrentStageId,
    string? CurrentStage,
    CandidateStatus Status,
    CandidateSource Source,
    string? Note,
    DateOnly? ClosedAt,
    string? ClosingNote,
    string? ResumeFileName,
    string? ResumeContentType,
    long? ResumeSizeBytes,
    Guid? HiredEmployeeId,
    Guid? HiredEmploymentId,
    IReadOnlyList<CandidateStageEntryDto> Stages,
    IReadOnlyList<JobOfferDto> Offers);

/// <summary>Ish taklifi.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="CandidateId">Nomzod.</param>
/// <param name="CandidateName">Nomzodning F.I.Sh.</param>
/// <param name="VacancyId">Vakansiya.</param>
/// <param name="Vacancy">Vakansiyaning nomi (e'lon nomi).</param>
/// <param name="Salary">Taklif qilingan oylik ish haqi.</param>
/// <param name="StartDate">Ish boshlash sanasi.</param>
/// <param name="SentAt">Yuborilgan sana.</param>
/// <param name="ExpiresAt">Javob muddati.</param>
/// <param name="Status">Sent | Accepted | Declined | Expired.</param>
/// <param name="RespondedAt">Javob sanasi.</param>
/// <param name="ResponseNote">Javob izohi.</param>
/// <param name="Note">Ichki izoh.</param>
public sealed record JobOfferDto(
    Guid Id,
    Guid CandidateId,
    string? CandidateName,
    Guid VacancyId,
    string? Vacancy,
    MoneyDto Salary,
    DateOnly StartDate,
    DateOnly SentAt,
    DateOnly? ExpiresAt,
    JobOfferStatus Status,
    DateOnly? RespondedAt,
    string? ResponseNote,
    string? Note);

/// <summary>
/// Taklif qabul qilinganining natijasi: yopilgan nomzod va yaratilgan xodim.
/// </summary>
/// <remarks>
/// Ikkalasi BIRGA qaytadi, chunki bu bitta amalning ikki tomoni: HR
/// «taklif qabul qilindi» tugmasini bosgach, ekranda darhol yangi
/// xodimning kartasi (va uning sinov muddati) ochilishi kerak — qo'shimcha
/// so'rovsiz. <see cref="Hire"/> ichida ishga qabulning odatiy javobi
/// turadi: mehnat munosabati, lavozim vazifalari va ogohlantirishlar
/// (masalan stavka chegarasi).
/// </remarks>
/// <param name="Offer">Yakunlangan taklif.</param>
/// <param name="Candidate">Yangilangan nomzod (holati — «ishga olingan»).</param>
/// <param name="Hire">Ishga qabul natijasi — 1-bosqichning odatiy javobi.</param>
public sealed record CandidateHireResultDto(
    JobOfferDto Offer,
    CandidateDto Candidate,
    HireResultDto Hire);
