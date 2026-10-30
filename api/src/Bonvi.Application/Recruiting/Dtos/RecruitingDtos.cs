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
/// <param name="Title">Vakansiya nomi — e'londa turadigan matn.</param>
/// <param name="Openings">Nechta odam kerak.</param>
/// <param name="Status">Open | Closed.</param>
/// <param name="OpenedAt">Ochilgan sana.</param>
/// <param name="ClosedAt">Yopilgan sana.</param>
/// <param name="Note">Izoh.</param>
/// <param name="CandidateCount">Vakansiyaga kiritilgan nomzodlar soni (jami).</param>
/// <param name="HiredCount">
/// Shu vakansiyadan ishga olinganlar soni. <see cref="Openings"/> bilan
/// birga «2/3 to'ldirildi» ko'rinishini beradi — vakansiya O'ZI yopilmaydi
/// (sababi <see cref="Vacancy"/> izohida), qaror HR'niki.
/// </param>
/// <param name="IsSystem">
/// «Umumiy anketa» — tizim vakansiyasi: yopilmaydi va o'chirilmaydi
/// (sababi <c>GeneralPoolVacancy</c> izohida). Ekran unda almashtirgich
/// o'rniga qulf ko'rsatadi.
/// </param>
public sealed record VacancyDto(
    Guid Id,
    string Title,
    int Openings,
    VacancyStatus Status,
    DateOnly OpenedAt,
    DateOnly? ClosedAt,
    string? Note,
    int CandidateCount,
    int HiredCount,
    bool IsSystem);

/// <summary>Nomzodning bitta bosqichdagi yozuvi.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="StageId">Bosqich.</param>
/// <param name="Stage">Bosqich nomi — faolsizlantirilgan bosqich uchun ham qaytadi.</param>
/// <param name="Date">Bosqich sanasi.</param>
/// <param name="Conclusion">Xulosa matni (kengash xulosasi, suhbat natijasi).</param>
/// <param name="RecordedBy">Kim yozdi — tizim foydalanuvchisi.</param>
/// <param name="RecordedAt">
/// Qachon KIRITILGAN — lenta shu bo'yicha saralanadi va tuzatishdan keyin
/// ham o'zgarmaydi (sababi <c>CandidateStageEntry.RecordedAt</c> izohida).
/// </param>
/// <param name="UpdatedAt">
/// Qachon tuzatilgan; tegilmagan yozuvda <c>null</c> — interfeys o'shanda
/// «tahrirlandi» belgisini umuman chizmaydi.
/// </param>
public sealed record CandidateStageEntryDto(
    Guid Id,
    Guid StageId,
    string? Stage,
    DateOnly Date,
    string? Conclusion,
    Guid? RecordedBy,
    DateTimeOffset RecordedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// Nomzodga biriktirilgan BITTA fayl — rezyume, pasport varag'i, diplom skani.
/// </summary>
/// <remarks>
/// <para>
/// <b>Alohida ro'yxat endpointi YO'Q</b>: fayllar
/// <see cref="CandidateDto.Files"/> ichida, karta bilan BIRGA keladi —
/// <see cref="CandidateQuestionnaireDto"/> va bosqichlar tarixi bilan
/// aynan bir xil qoida (HR ularni doim karta bilan birga ko'radi, ikkinchi
/// so'rov faqat ekranni sekinlashtirardi). Xodim hujjatlarida alohida
/// ro'yxat BOR, chunki u o'z tabi va o'z <c>documentTypeId</c> filtri
/// bilan yashaydi — bu yerda ikkalasi ham yo'q.
/// </para>
/// <para>
/// <b>Saqlash kaliti QAYTARILMAYDI</b> (<see cref="CandidatePhotoDto"/>
/// dan farqli): u ichki manzil va yuklab olish uchun
/// <see cref="Id"/> yetadi.
/// </para>
/// <para>
/// <c>isImage</c> kabi bayroq ATAYLAB yo'q: mijoz uni
/// <c>contentType.startsWith('image/')</c> bilan o'zi hisoblaydi va
/// server ikkinchi haqiqat manbaini yaratmaydi.
/// </para>
/// </remarks>
/// <param name="Id">Fayl identifikatori — yuklab olish va o'chirish shu bo'yicha.</param>
/// <param name="FileName">Asl nom — FAQAT ko'rsatish uchun.</param>
/// <param name="ContentType">MIME turi — fayl MAZMUNIDAN aniqlangan.</param>
/// <param name="SizeBytes">
/// SAQLANGAN hajm — kichraytirilgandan KEYINGI baytlar, yuklangan fayl
/// hajmi emas (sababi <see cref="CandidateFile.SizeBytes"/> izohida).
/// <c>0</c> — noma'lum: eski rezyumeda hajm bo'lmagan bo'lishi mumkin edi
/// va ko'chirish o'shanda nol yozadi; ekranda chiziqcha chiziladi.
/// </param>
/// <param name="UploadedAt">Qachon biriktirilgani.</param>
public sealed record CandidateFileDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt);

/// <summary>
/// Bosqichi bo'lmagan amaldan qolgan izoh.
/// </summary>
/// <remarks>
/// Nega alohida jadval va nega mavjud uchta maydonning bittasi ham
/// yaramaganini <see cref="CandidateNote"/> izohi tushuntiradi.
/// </remarks>
/// <param name="Id">Identifikator.</param>
/// <param name="Kind">Qaysi amal shu izohni tug'dirgani — tarixdagi yorliq shundan.</param>
/// <param name="Text">
/// Izohning o'zi — ICHKI qayd, nomzodga HECH QACHON yuborilmaydi (na
/// Telegram xabarida, na rad etish matnida). Bot bu DTO ni umuman
/// o'qimaydi: uning javoblari <see cref="RejectionNoticeDto"/> va
/// <see cref="InterviewNoticeDto"/>.
/// </param>
/// <param name="CreatedBy">Kim yozgani — tizim foydalanuvchisi.</param>
/// <param name="CreatedAt">Qachon yozilgani.</param>
public sealed record CandidateNoteDto(
    Guid Id,
    CandidateNoteKind Kind,
    string Text,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt);

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
/// <para>
/// ⚠️ <b>Yangi maydonlar bu yerga QO'SHILMAYDI</b> (mijoz talabi,
/// 2026-09-10). Bosqich xulosasi
/// (<see cref="CandidateStageEntryDto.Conclusion"/>), amal izohi
/// (<see cref="CandidateNoteDto.Text"/>), keyingi aloqa sanasi
/// (<see cref="CandidateDto.NextContactDate"/>) va biriktirilgan fayllar
/// — hammasi ICHKI. Va'dani DTO chegarasining O'ZI ushlab turadi: bot
/// <see cref="CandidateDto"/> ni umuman o'qimaydi, ya'ni u yerga maydon
/// qo'shish bu javobga hech narsa qo'shmaydi.
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

/// <summary>Yangi ariza nega qabul qilinmadi.</summary>
public enum ApplicationBlockReason
{
    /// <summary>To'siq yo'q.</summary>
    None = 0,

    /// <summary>Shu raqam bilan HALI KO'RILAYOTGAN ariza bor.</summary>
    Active = 1,

    /// <summary>Oldingi ariza yopilgan, lekin muddat hali tugamagan.</summary>
    Cooldown = 2,

    /// <summary>
    /// Odam qora ro'yxatda — muddatsiz (2026-10-07). Nomzodga sabab
    /// AYTILMAYDI: bot umumiy muloyim rad javobini ko'rsatadi.
    /// </summary>
    Blacklisted = 3,
}

/// <summary>
/// «Bu odam yangi anketa topshira oladimi» degan savolning javobi.
/// </summary>
/// <remarks>
/// <para>
/// Botga ketadi. Maydonlar ataylab OZ va ular faqat nomzodga
/// ko'rsatiladigan xabarni yozish uchun yetadi —
/// <c>RejectionNoticeDto</c> dagi bilan bir xil qoida: nomzodning
/// ismi, manzili va ichki izohlari bu javobga TUSHMAYDI.
/// </para>
/// </remarks>
/// <param name="Allowed">Yangi anketa qabul qilinadimi.</param>
/// <param name="Reason">To'siq sababi.</param>
/// <param name="AppliedAt">Oldingi ariza qachon topshirilgan.</param>
/// <param name="VacancyTitle">Qaysi vakansiyaga edi.</param>
/// <param name="StageName">Qaysi bosqichda to'xtagan.</param>
/// <param name="AvailableFrom">
/// Qaysi sanadan keyin qayta topshirish mumkin. Faqat
/// <see cref="ApplicationBlockReason.Cooldown"/> da to'ladi.
/// </param>
public sealed record ApplicationCheckDto(
    bool Allowed,
    ApplicationBlockReason Reason,
    DateTimeOffset? AppliedAt,
    string? VacancyTitle,
    string? StageName,
    DateOnly? AvailableFrom)
{
    /// <summary>To'siq yo'q — anketa davom etadi.</summary>
    public static ApplicationCheckDto Allow()
        => new(true, ApplicationBlockReason.None, null, null, null, null);

    /// <summary>To'siq bor — sabab va (kerak bo'lsa) tafsilotlar bilan.</summary>

    public static ApplicationCheckDto Blocked(
        ApplicationBlockReason reason,
        DateTimeOffset? appliedAt = null,
        string? vacancyTitle = null,
        string? stageName = null,
        DateOnly? availableFrom = null)
        => new(false, reason, appliedAt, vacancyTitle, stageName, availableFrom);
}

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
/// <param name="HasResume">
/// Kamida BITTA fayl biriktirilganmi.
/// <para>
/// ⚠️ Nomi ATAYLAB o'zgarmadi, garchi nomzodda endi bitta rezyume emas,
/// o'ntagacha fayl bo'lsa ham (<see cref="CandidateFileDto"/>). Qayta
/// nomlash mijozdagi o'quvchini ham, mavjud testlarni ham surardi, foydasi
/// esa nol: jadvalda SON emas, qisqich belgisi chiziladi. Loyihaning
/// qoidasi — qo'shimcha o'zgarish, keraksiz qayta nomlash emas.
/// </para>
/// </param>
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
/// <param name="NextContactDate">
/// HR shu odam bilan keyingi marta qachon gaplashishni rejalashtirgani —
/// kalendar sanasi, vaqtsiz. <c>null</c> — reja yo'q (ko'pchilik qatorda
/// shunday).
/// <para>
/// Ro'yxatda ALOHIDA USTUN bo'lib chiziladi va <c>sort=nextContact</c>
/// bilan saralanadi; <c>contactDue=true</c> filtri aynan shu maydonga
/// tayanadi. Fayllar SONI kabi bu yerda faqat ko'rsatiladigan qiymat
/// turadi — hisob-kitob serverda (sababi
/// <see cref="Candidate.NextContactDate"/> izohida: ustun ATAYLAB
/// denormallashtirilgan, aks holda filtr indeksga tushmasdi).
/// </para>
/// </param>
/// <param name="ClosedAt">Voronkadan chiqqan sana — yopilmagan bo'lsa <c>null</c>.</param>
/// <param name="ClosingNote">
/// Chiqish sababi. Ro'yxatga «Qora ro'yxat → Nomzodlar» tabi uchun qo'shildi
/// (2026-10-07): u yerda SABAB — eng muhim ustun («nega bu odam olinmaydi»).
/// </param>
/// <param name="ReferralChannelId">
/// «Qayerdan bilgan» kanali (SPEC-qayerdan-bilgan §6). <c>null</c> — ko'rsatilmagan (eski
/// yozuv yoki Excel arxivi). Ro'yxatda alohida USTUN yo'q (qator bir qatorli) — maydon
/// filtrlangan ro'yxat va eksport bilan bir xil shaklda bo'lishi uchun.
/// </param>
/// <param name="ReferralChannelName">Kanal nomi — faolsizlantirilgani ham.</param>
/// <param name="ReferralDetail">Aniqlik: «kim tavsiya qildi / qaysi manba».</param>
/// <param name="ReappliedAt">
/// Kartaga keyin onlayn anketa qo'shilgan oxirgi lahza (<c>Candidate.ReappliedAt</c>); <c>null</c> —
/// qayta murojaat yo'q. Ro'yxatdagi sana ustuni va sukut tartibi — <c>reappliedAt ?? createdAt</c>.
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
    Guid? HiredEmployeeId,
    DateOnly? NextContactDate,
    DateOnly? ClosedAt,
    string? ClosingNote,
    Guid? ReferralChannelId,
    string? ReferralChannelName,
    string? ReferralDetail,
    DateTimeOffset? ReappliedAt = null);

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
/// <param name="ExpectedSalary">
/// Kutilayotgan maosh ORALIG'I.
/// <para>
/// «Ish haqida qayerdan bilgan» bu blokda ENDI YO'Q — u nomzodning o'zida
/// (<see cref="CandidateDto.ReferralChannelName"/>), chunki har bir nomzodga kerak.
/// </para>
/// </param>
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
/// <param name="NextContactDate">
/// Keyingi aloqa sanasi — kalendar sanasi, vaqtsiz. <c>null</c> — reja
/// qo'yilmagan.
/// <para>
/// O'TGAN sana ODATIY hol va xato EMAS: maydonning butun maqsadi «bugun
/// yoki muddati o'tgan» ro'yxatini berish (sababi
/// <see cref="Candidate.NextContactDate"/> izohida). Qaysi amal uni
/// qo'yadi, qaysi biri tozalaydi — <c>Candidate.Close</c> izohida:
/// qisqasi rad etish / voz kechish / ishga olish TOZALAYDI, rezervga
/// o'tkazish esa SAQLAYDI.
/// </para>
/// <para>
/// ⚠️ ICHKI maydon: botga hech qachon ketmaydi (sababi
/// <see cref="RejectionNoticeDto"/> izohida).
/// </para>
/// </param>
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
/// <param name="InterviewNoticeDelivered">
/// Taklif nomzodga yetib bordimi: <c>false</c> — Telegram rad etdi (botni
/// bloklagan va hokazo), HR telefon qilishi kerak; <c>null</c> — hali
/// urinilmagan yoki eski yozuv (o'shanda faqat yetkazilganlar belgilanardi).
/// </param>
/// <param name="Stages">Bosqichlar tarixi — yozilgan tartibda.</param>
/// <param name="Files">
/// Biriktirilgan fayllar — YUKLASH tartibida (eskisidan yangisiga).
/// <para>
/// Tartib xodim hujjatlaridagidan (yangisi tepada) ATAYLAB teskari:
/// pasport uch varaq bo'lib 1-2-3 tartibda yuklanadi va teskari tartibda
/// karta o'qilmas bo'lib qolardi (sababi <see cref="CandidateFile.CreatedAt"/>
/// izohida ham yozilgan). Bo'sh massiv — fayl yo'q, odatiy hol.
/// </para>
/// </param>
/// <param name="Notes">
/// Bosqichi bo'lmagan amallardan qolgan izohlar — yozilgan tartibda.
/// <para>
/// Bosqich XULOSALARI bu yerda EMAS, <see cref="Stages"/> ichida: ikkisi
/// boshqa-boshqa fakt (sababi <see cref="CandidateNote"/> izohida).
/// </para>
/// </param>
/// <param name="Questionnaire">
/// Botda to'ldirilgan anketa. <c>null</c> BO'LISHI ODATIY hol: HR qo'lda
/// kiritgan nomzodda ham, bu funksiya paydo bo'lgunicha yozilgan
/// nomzodlarda ham u yo'q.
/// </param>
/// <param name="ReferralChannelId">
/// «Ish haqida qayerdan bilgan» kanali (SPEC-qayerdan-bilgan §4). <c>null</c> faqat eski
/// yozuvda va Excel arxivida — yangi nomzodda u majburiy.
/// </param>
/// <param name="ReferralChannelName">
/// Kanal nomi — faolsizlantirilgan bo'lsa ham (eski nomzod o'sha kanal bilan qoladi).
/// </param>
/// <param name="ReferralDetail">Aniqlik: «kim tavsiya qildi / qaysi manba».</param>
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
    DateOnly? NextContactDate,
    bool HasPhoto,
    Guid? HiredEmployeeId,
    Guid? HiredEmploymentId,
    DateTimeOffset? InterviewAt,
    DateTimeOffset? InterviewNoticeSentAt,
    bool? InterviewNoticeDelivered,
    IReadOnlyList<CandidateStageEntryDto> Stages,
    IReadOnlyList<CandidateFileDto> Files,
    IReadOnlyList<CandidateNoteDto> Notes,
    CandidateQuestionnaireDto? Questionnaire,
    Guid? ReferralChannelId,
    string? ReferralChannelName,
    string? ReferralDetail);

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

/// <summary>
/// Shu telefon raqamli mavjud nomzod — qo'lda kiritishdagi ogohlantirish uchun.
/// </summary>
/// <remarks>
/// Kiritish TO'SILMAYDI (<c>RegisterCandidateCommandHandler</c> izohi:
/// telefon oilada bitta bo'lishi mumkin) — HR ro'yxatni ko'rib, o'zi
/// hal qiladi. Javob shu qarorga yetadigan darajada qisqa.
/// </remarks>
/// <param name="Id">Nomzod.</param>
/// <param name="FullName">F.I.Sh.</param>
/// <param name="Status">Holati — rezervda, rad etilgan va h.k.</param>
/// <param name="VacancyTitle">Qaysi vakansiyaga.</param>
/// <param name="CreatedAt">Qachon kiritilgan.</param>
public sealed record CandidatePhoneMatchDto(
    Guid Id,
    string FullName,
    CandidateStatus Status,
    string? VacancyTitle,
    DateTimeOffset CreatedAt);
