using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Employees;
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
/// <param name="InvitesToInterview">
/// Shu bosqichga o'tkazilganda nomzoddan suhbat vaqti so'ralib, unga
/// Telegram taklifi yuboriladimi.
/// </param>
public sealed record PipelineStageDto(
    Guid Id,
    string Name,
    bool IsActive,
    string? Note,
    int SortOrder,
    bool InvitesToInterview);

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
/// uchtasi nomzod ISHGA OLINAYOTGANDA tanlanadi va o'sha yerdan
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
/// Suhbat taklifi kutayotgan nomzod — FAQAT botga beriladigan ko'rinish.
/// </summary>
/// <remarks>
/// <see cref="RejectionNoticeDto"/> ning egizagi va bir xil qoidaga
/// bo'ysunadi: javobda faqat xabar yozish uchun kerakli maydonlar bor,
/// telefon va ichki izohlar bu yerga TUSHMAYDI.
/// </remarks>
/// <param name="CandidateId">Xabar ketgach shu bilan belgilanadi.</param>
/// <param name="ChatId">Telegram suhbati.</param>
/// <param name="Language">Nomzod tanlagan til.</param>
/// <param name="FullName">Murojaat uchun ism.</param>
/// <param name="VacancyTitle">Qaysi vakansiyaga.</param>
/// <param name="InterviewAt">
/// Suhbat lahzasi — UTC da. Toshkent vaqtiga botning O'ZI keltiradi
/// (<c>BotClock</c>): shu tufayli siljish faqat KO'RSATISH joyida
/// qo'llanadi va lahzaning o'zi hech qayerda o'zgarmaydi.
/// </param>
public sealed record InterviewNoticeDto(
    Guid CandidateId,
    long ChatId,
    string? Language,
    string FullName,
    string VacancyTitle,
    DateTimeOffset InterviewAt);

/// <summary>
/// Nomzodlar ro'yxatining bitta qatori.
/// </summary>
/// <remarks>
/// Bosqichlar tarixi bu yerda YO'Q: ro'yxat yuzlab qatorni tortadi va
/// har biriga tarix yig'ish keraksiz yuk bo'lardi
/// (<c>EmployeeListItemDto</c> bilan bir xil qoida). U kartada.
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
/// <param name="HasPhoto">
/// Surati bormi. Anketaning O'ZI bu yerda YO'Q — u og'ir va ro'yxatda
/// kerak emas.
/// <para>
/// ⚠️ Bayroq HOZIRCHA hech qayerda o'qilmaydi: nomzodlar ro'yxati
/// (<c>candidates-page.tsx</c>) rasmni ko'rsatmaydi, surat faqat
/// nomzod kartasida chiziladi. Maydon oldindan qo'yilgan, chunki uni
/// keyin qo'shish ro'yxat so'rovini o'zgartirishni talab qilardi;
/// bayroqsiz esa interfeys har bir qator uchun surat so'rab,
/// mavjud emasligini 404 orqali bilib olardi. Ro'yxatda rasm kerak
/// bo'lganda ma'lumot allaqachon joyida bo'ladi.
/// </para>
/// </param>
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
    bool HasPhoto,
    DateTimeOffset CreatedAt,
    Guid? HiredEmployeeId);

/// <summary>
/// Nomzodning botda to'ldirgan anketasi — nomzod kartasining bir qismi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Alohida endpoint YO'Q</b> va bu ataylab: HR anketani har doim
/// nomzod bilan birga ko'radi, ya'ni ikkinchi so'rov faqat ekranni
/// sekinlashtirardi. Shu sababli u <see cref="CandidateDto"/> ning
/// ichida, nullable blok bo'lib qaytadi.
/// </para>
/// <para>
/// <b>Bo'sh maydonlar NORMAL</b>: botda beshta qadamni o'tkazib yuborish
/// mumkin (izohi <c>CandidateQuestionnaire</c> da). Interfeys ularni
/// umuman chizmaydi — «ma'lumot yo'q» degan bo'sh qator emas.
/// </para>
/// </remarks>
/// <param name="Education">Ma'lumot darajasi.</param>
/// <param name="WorkExperience">Ish tajribasi — erkin matn.</param>
/// <param name="MaritalStatus">Oilaviy holati.</param>
/// <param name="RussianLevel">
/// Rus tilini bilish darajasi — xodim kartasidagi til darajasi bilan
/// AYNAN bir xil sanoq (sababi domen izohida).
/// </param>
/// <param name="OtherLanguages">Boshqa tillar — erkin matn.</param>
/// <param name="OfficeSkills">Office dasturlari — erkin matn.</param>
/// <param name="LastSalary">Oxirgi maosh — SON EMAS, matn.</param>
/// <param name="ExtraNote">Nomzodning o'zi haqida qo'shimcha aytgani.</param>
/// <param name="HasDriverLicense">Haydovchilik guvohnomasi bormi.</param>
/// <param name="DriverLicenseCategories">Guvohnoma toifasi: B, BC, BCE, BCED.</param>
/// <param name="ExpectedSalary">Kutilayotgan maosh ORALIG'I.</param>
/// <param name="ReferralSource">Ish haqida qayerdan bilgan.</param>
/// <param name="OfferAcceptedAt">
/// Ommaviy oferta bilan rozilik VAQTI — huquqiy fakt, bayroq emas.
/// <c>null</c> bo'lishi ODATIY hol: oferta havolasi sozlanmaganda bot
/// bu qadamni umuman chizmaydi va rozilik so'ralmaydi. Interfeys u
/// holda qatorni chizmaydi (yuqoridagi «bo'sh maydonlar» qoidasi).
/// </param>
public sealed record CandidateQuestionnaireDto(
    EducationLevel Education,
    string? WorkExperience,
    MaritalStatus MaritalStatus,
    LanguageLevel RussianLevel,
    string? OtherLanguages,
    string? OfficeSkills,
    string? LastSalary,
    string? ExtraNote,
    bool HasDriverLicense,
    string? DriverLicenseCategories,
    SalaryExpectation ExpectedSalary,
    ReferralSource ReferralSource,
    DateTimeOffset? OfferAcceptedAt);

/// <summary>Yuklangan nomzod suratining saqlash kaliti.</summary>
/// <remarks>
/// Shakl <c>EmployeePhotoDto</c> bilan AYNAN bir xil va bu ataylab: rasm
/// quvuri ham bitta (<c>SquarePhotoStore</c>). Rasmning O'ZI qaytarilmaydi —
/// interfeys uni odatdagi <c>GET .../photo</c> orqali oladi va o'sha yo'l
/// brauzer keshiga tushadi; kalit esa kartani sahifani qayta yuklamasdan
/// yangilash uchun kerak.
/// </remarks>
public sealed record CandidatePhotoDto(string PhotoKey);

/// <summary>Nomzod kartasi — bosqichlar tarixi bilan.</summary>
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
/// <param name="HasPhoto">
/// Surati bormi. Kalitning O'ZI qaytarilmaydi: u ichki saqlash manzili
/// va rasm baribir alohida endpoint orqali olinadi.
/// </param>
/// <param name="HiredEmployeeId">Ishga olingan bo'lsa — xodim kartasi.</param>
/// <param name="HiredEmploymentId">Ishga olingan bo'lsa — mehnat munosabati.</param>
/// <param name="InterviewAt">
/// Chaqirilgan suhbat vaqti — UTC da. <c>null</c> — chaqirilmagan.
/// </param>
/// <param name="InterviewNoticeSentAt">
/// Taklif Telegram'ga qachon ketgani. Vaqt qo'yilgan, lekin bu bo'sh —
/// xabar hali navbatda (yoki nomzodning Telegram'i yo'q).
/// </param>
/// <param name="Stages">Bosqichlar tarixi — yozilgan tartibda.</param>
/// <param name="Questionnaire">
/// Botda to'ldirilgan anketa. <c>null</c> BO'LISHI ODATIY hol: HR qo'lda
/// kiritgan nomzodda ham, bu funksiya paydo bo'lgunicha yozilgan
/// nomzodlarda ham u yo'q.
/// </param>
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
    bool HasPhoto,
    Guid? HiredEmployeeId,
    Guid? HiredEmploymentId,
    DateTimeOffset? InterviewAt,
    DateTimeOffset? InterviewNoticeSentAt,
    IReadOnlyList<CandidateStageEntryDto> Stages,
    CandidateQuestionnaireDto? Questionnaire);

/// <summary>
/// To'g'ridan-to'g'ri ishga olishning natijasi: yopilgan nomzod va
/// yaratilgan xodim.
/// </summary>
/// <remarks>
/// <para>
/// Ikkalasi BIRGA qaytadi, chunki bu bitta amalning ikki tomoni: HR
/// «Ishga olish» tugmasini bosgach, ekranda darhol yangi xodimning
/// kartasi (va uning sinov muddati) ochilishi kerak — qo'shimcha
/// so'rovsiz.
/// </para>
/// <para>
/// <see cref="Hire"/> ichida ishga qabulning odatiy javobi turadi:
/// mehnat munosabati, lavozim vazifalari va ogohlantirishlar (masalan
/// stavka chegarasi).
/// </para>
/// </remarks>
/// <param name="Candidate">Yangilangan nomzod (holati — «ishga olingan»).</param>
/// <param name="Hire">Ishga qabul natijasi — 1-bosqichning odatiy javobi.</param>
public sealed record DirectHireResultDto(
    CandidateDto Candidate,
    HireResultDto Hire);
