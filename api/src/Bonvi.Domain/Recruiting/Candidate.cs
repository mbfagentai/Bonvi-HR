using Bonvi.Domain.Common;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Recruiting;

/// <summary>Nomzod voronkadagi holati.</summary>
public enum CandidateStatus
{
    /// <summary>Jarayonda — voronkada, bosqichlardan o'tmoqda.</summary>
    InProgress = 1,

    /// <summary>Rad etilgan — korxona qarori.</summary>
    Rejected = 2,

    /// <summary>Ishga olingan — voronka yakunlandi.</summary>
    Hired = 3,

    /// <summary>O'zi voz kechgan — nomzod qarori.</summary>
    /// <remarks>
    /// <see cref="Rejected"/> dan ATAYLAB ajratilgan: voronka hisobotida
    /// «biz rad etdik» va «u ketib qoldi» butunlay boshqa ma'no beradi.
    /// Birinchisi tanlov sifati haqida, ikkinchisi esa taklif shartlari
    /// yoki jarayon uzunligi haqida gapiradi. Bitta holatga yig'ilsa,
    /// ikkala muammo ham ko'rinmay qolardi.
    /// </remarks>
    Withdrawn = 4,
}

/// <summary>
/// Nomzod qayerdan kelgan.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu maydon HOZIR kerak.</b> Keyingi bosqichda Telegram anketa
/// boti quriladi va nomzodlar o'sha yerdan avtomatik tushadi. Manba
/// keyinchalik qo'shilsa, bugungi barcha yozuvlar «noma'lum» bo'lib
/// qolardi va «bot qancha nomzod keltirdi» degan asosiy savolga hech qachon
/// javob bo'lmasdi — bot uchun to'lanadigan mehnat aynan shu raqam bilan
/// oqlanadi.
/// </para>
/// <para>
/// Ro'yxat kengaytiriladi (hh.uz, tavsiya, ish yarmarkasi), shu sababli
/// sanoq qiymatlari aniq raqamlar bilan qadab qo'yilgan: yangisi
/// O'RTAGA emas, OXIRIGA qo'shiladi — aks holda bazadagi eski qiymatlar
/// jimgina siljib ketardi.
/// </para>
/// </remarks>
public enum CandidateSource
{
    /// <summary>HR qo'lda kiritgan.</summary>
    Manual = 1,

    /// <summary>Telegram anketa boti orqali (3-agent quradi).</summary>
    TelegramBot = 2,
}

/// <summary>
/// Nomzod — voronkadagi shaxs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <see cref="Employees.Employee"/> dan alohida.</b> Nomzod hali
/// xodim emas va ularning ma'lumot talablari butunlay boshqa: xodimda
/// JSHSHIR, pasport va mehnat munosabati bo'lishi kerak, nomzodda esa
/// ko'pincha faqat ism va telefon bor. Ikkalasini bitta jadvalga yig'ish
/// xodimlar ro'yxatini (540 qator) rad etilgan nomzodlar bilan
/// («ishga olinmagan odamlar») aralashtirib yuborardi va har bir hisobotga
/// «bu haqiqiy xodimmi?» degan shart qo'shilardi.
/// </para>
/// <para>
/// <b>Nomzoddan xodimga o'tish — <c>HireEmployeeCommand</c> orqali.</b>
/// Taklif qabul qilinganda nomzodning ma'lumoti (F.I.Sh., telefon,
/// tug'ilgan sana, manzil, mutaxassislik) ishga qabul buyrug'iga ko'chadi
/// va 1-bosqichning MAVJUD oqimi ishlaydi: xodim kartasi, mehnat
/// munosabati, birinchi buyruq va MAJBURIY sinov muddati. Rekruting
/// o'zining «ishga qabul» yo'lini yozmaydi — ikkinchi yo'l bo'lganida
/// sinov muddati qoidasi («hamma sinovdan o'tadi, istisnosiz») jimgina
/// chetlab o'tilardi.
/// </para>
/// <para>
/// <b>Bog'lanish nomzodda emas, ISHGA QABUL HODISASIDA saqlanadi</b>
/// (<c>EmploymentEvent.CandidateId</c>) — sabab
/// <c>AcceptJobOfferCommandHandler</c> izohida batafsil.
/// </para>
/// </remarks>
public sealed class Candidate : AggregateRoot, IAuditable, ISoftDeletable
{
    private readonly List<CandidateStageEntry> _stageEntries = [];

    private Candidate(
        Guid id,
        FullName fullName,
        Guid vacancyId,
        CandidateSource source)
        : base(id)
    {
        FullName = fullName;
        VacancyId = vacancyId;
        Source = source;
        Status = CandidateStatus.InProgress;
    }

    /// <summary>EF Core uchun.</summary>
    private Candidate() => FullName = null!;

    public FullName FullName { get; private set; }

    /// <summary>Qaysi vakansiyaga.</summary>
    public Guid VacancyId { get; private set; }

    /// <summary>
    /// Hozir turgan bosqich.
    /// </summary>
    /// <remarks>
    /// Bu qiymat <see cref="StageEntries"/> dan HOSIL BO'LADI (oxirgi
    /// yozuvning bosqichi), lekin bu yerda ham saqlanadi — xuddi
    /// <c>Employment.Status</c> kabi. Sabab: «voronkaning har bosqichida
    /// nechta odam bor» degan asosiy ekran shu ustun bo'yicha guruhlanadi
    /// va uni har so'rovda tarixdan hisoblash butun jadvalni skanerlashni
    /// talab qilardi.
    /// </remarks>
    public Guid? CurrentStageId { get; private set; }

    public CandidateStatus Status { get; private set; }

    public CandidateSource Source { get; private set; }

    public DateOnly? BirthDate { get; private set; }

    /// <summary>Telefon — nomzod bilan bog'lanishning ASOSIY yo'li.</summary>
    /// <remarks>
    /// Nomzodda JSHSHIR ham, pasport ham bo'lmasligi mumkin (u hali hujjat
    /// topshirmagan), shu sababli qidiruvning ikkinchi kaliti aynan telefon.
    /// Majburiy EMAS: Telegram botdan kelgan anketada raqam bo'lmasligi
    /// mumkin, HR esa uni keyin to'ldiradi.
    /// </remarks>
    public PhoneNumber? Phone { get; private set; }

    public string? Address { get; private set; }

    /// <summary>Mutaxassisligi — ta'lim va diplom haqidagi erkin matn.</summary>
    /// <remarks>
    /// <c>Employee.Speciality</c> bilan AYNAN bir xil ma'no va shakl:
    /// ishga qabul paytida u to'g'ridan-to'g'ri xodim kartasiga ko'chadi.
    /// Boshqacha tuzilgan bo'lsa (masalan ma'lumotnoma qilingan bo'lsa),
    /// ko'chirishda moslashtirish kerak bo'lardi va ma'lumot yo'qolardi.
    /// </remarks>
    public string? Speciality { get; private set; }

    /// <summary>Rezyume fayli — <c>IFileStorage</c> kaliti.</summary>
    public string? ResumeFileKey { get; private set; }

    /// <summary>Rezyumening asl fayl nomi — faqat KO'RSATISH uchun.</summary>
    public string? ResumeFileName { get; private set; }

    /// <summary>Rezyume faylining MAZMUNIGA qarab aniqlangan MIME turi.</summary>
    public string? ResumeContentType { get; private set; }

    public long? ResumeSizeBytes { get; private set; }

    /// <summary>Erkin izoh.</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Telegram suhbatining raqami — botdan kelgan nomzodda.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nima uchun kerak.</b> Rad etilgan nomzodga javob yozish
    /// uchun. Busiz bot uni topa olmaydi: telefon raqami Telegram'da
    /// manzil emas va u orqali xabar yuborib bo'lmaydi.
    /// </para>
    /// <para>
    /// <b>Bo'sh bo'lishi ODATIY hol</b> — HR qo'lda kiritgan nomzodda u
    /// yo'q. Bunday nomzodga bot xabar yubormaydi va bu xato emas:
    /// u tizimga Telegram orqali kelmagan, demak u yerda kutmayapti
    /// ham.
    /// </para>
    /// <para>
    /// ⚠️ <b>Raqam SHAXSIY ma'lumot emas, lekin manzil.</b> U bilan
    /// nomzodga istalgan xabarni yuborib bo'ladi — shu sababli u
    /// ekranda ko'rsatilmaydi va faqat bot xizmat hisobiga beriladi.
    /// </para>
    /// </remarks>
    public long? TelegramChatId { get; private set; }

    /// <summary>
    /// Nomzod botda tanlagan til: <c>uz-Cyrl</c>, <c>uz-Latn</c>, <c>ru</c>.
    /// </summary>
    /// <remarks>
    /// Javob AYNAN o'sha tilda yozilishi kerak. Tilni taxmin qilish
    /// («hammaga kirillda yozamiz») ruscha yozgan nomzodga tushunarsiz
    /// xabar yuborardi — u esa arizasi rad etilgani haqidagi xabar.
    /// </remarks>
    public string? TelegramLanguage { get; private set; }

    /// <summary>
    /// Rad etish haqidagi xabar Telegram'ga QACHON yuborilgani.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Bu — navbat (outbox), oddiy bayroq emas.</b> Bot alohida
    /// jarayon: API rad etish paytida unga to'g'ridan-to'g'ri xabar
    /// yubora olmaydi (bot ulanishni O'ZI ochadi, teskarisi emas).
    /// Shu sababli API faqat FAKTNI yozadi, bot esa «yuborilmaganlar»
    /// ro'yxatini davriy so'rab turadi.
    /// </para>
    /// <para>
    /// <b>Nega alohida jadval emas.</b> Navbatda bir vaqtda faqat
    /// rad etilgan va hali xabar olmagan nomzodlar turadi — ular
    /// o'nlab bo'ladi. Alohida jadval bu ma'lumotni ikkiga bo'lib,
    /// «nomzod o'chdi, navbatdagi yozuv qoldi» degan holatni
    /// tug'dirardi.
    /// </para>
    /// <para>
    /// <c>null</c> + <see cref="CandidateStatus.Rejected"/> +
    /// <see cref="TelegramChatId"/> bor = xabar yuborilishi kerak.
    /// </para>
    /// </remarks>
    public DateTimeOffset? RejectionNoticeSentAt { get; private set; }

    /// <summary>Voronkadan chiqqan sana (rad etilgan / ishga olingan / voz kechgan).</summary>
    public DateOnly? ClosedAt { get; private set; }

    /// <summary>Voronkadan chiqish sababi — rad etish yoki voz kechish izohi.</summary>
    public string? ClosingNote { get; private set; }

    /// <summary>Bosqichlar tarixi — yozilgan tartibda.</summary>
    public IReadOnlyList<CandidateStageEntry> StageEntries => _stageEntries.AsReadOnly();

    /// <summary>Voronkada faolmi.</summary>
    public bool IsActive => Status == CandidateStatus.InProgress;

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public static Result<Candidate> Register(
        FullName fullName,
        Guid vacancyId,
        CandidateSource source = CandidateSource.Manual,
        DateOnly? birthDate = null,
        PhoneNumber? phone = null,
        string? address = null,
        string? speciality = null,
        string? note = null)
    {
        ArgumentNullException.ThrowIfNull(fullName);

        if (vacancyId == Guid.Empty)
        {
            return Error.Validation("nomzod.vakansiya_yoq", "Vakansiya ko'rsatilishi shart.");
        }

        if (birthDate is { } date)
        {
            var valid = ValidateBirthDate(date);
            if (valid.IsFailure)
            {
                return valid.Error;
            }
        }

        return new Candidate(NewId(), fullName, vacancyId, source)
        {
            BirthDate = birthDate,
            Phone = phone,
            Address = Trim(address),
            Speciality = Trim(speciality),
            Note = Trim(note),
        };
    }

    /// <summary>
    /// Shaxsiy ma'lumotni tuzatadi.
    /// </summary>
    /// <remarks>
    /// Vakansiya BU YERDA o'zgarmaydi (<see cref="Reassign"/> bor), manba
    /// esa umuman o'zgarmaydi: «bu yozuv qayerdan keldi» — bu tarixiy fakt
    /// va uni tahrirlash mumkin bo'lsa, bot samaradorligi haqidagi hisobot
    /// qo'lda «tuzatilishi» mumkin bo'lib qolardi.
    /// </remarks>
    public Result Update(
        FullName fullName,
        DateOnly? birthDate = null,
        PhoneNumber? phone = null,
        string? address = null,
        string? speciality = null,
        string? note = null)
    {
        ArgumentNullException.ThrowIfNull(fullName);

        if (birthDate is { } date)
        {
            var valid = ValidateBirthDate(date);
            if (valid.IsFailure)
            {
                return valid.Error;
            }
        }

        FullName = fullName;
        BirthDate = birthDate;
        Phone = phone;
        Address = Trim(address);
        Speciality = Trim(speciality);
        Note = Trim(note);

        return Result.Success();
    }

    /// <summary>
    /// Nomzodni boshqa vakansiyaga o'tkazadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Alohida amal, <see cref="Update"/> ning bir qismi emas — bo'limni
    /// daraxtda ko'chirish bilan bir xil sabab: bu tahrir emas, QAROR. Uni
    /// tahrirlash formasiga qo'shish tasodifan boshqa vakansiyaga
    /// o'tkazib yuborish yo'lini ochardi.
    /// </para>
    /// <para>
    /// Bosqichlar tarixi SAQLANADI va joriy bosqich o'zgarmaydi: telefon
    /// suhbatidan o'tgan odam boshqa o'ringa taklif qilinganda uni qaytadan
    /// qo'ng'iroq qilishga majburlash ma'nosiz.
    /// </para>
    /// </remarks>
    public Result Reassign(Guid vacancyId)
    {
        if (vacancyId == Guid.Empty)
        {
            return Error.Validation("nomzod.vakansiya_yoq", "Vakansiya ko'rsatilishi shart.");
        }

        if (!IsActive)
        {
            return NotActive();
        }

        VacancyId = vacancyId;
        return Result.Success();
    }

    /// <summary>
    /// Bosqich natijasini yozadi — voronkaning yagona harakati.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ikkita ish jarayoni, bitta metod.</b> HR ba'zan bosqichni oldindan
    /// ochadi («ertaga kengash») va natijani keyin yozadi, ba'zan esa
    /// hammasini bir vaqtda kiritadi («telefon qildim — o'tmadi»). Agar
    /// oxirgi yozuv AYNAN shu bosqich uchun va hali «kutilmoqda» bo'lsa, u
    /// TO'LDIRILADI; aks holda yangi yozuv qo'shiladi. Ikkita alohida metod
    /// bo'lganida mijoz «bu bosqich ochilganmi?» degan holatni o'zi
    /// kuzatishi kerak bo'lardi.
    /// </para>
    /// <para>
    /// <b>Voronka OLDINGA yurishga majburlamaydi.</b> Bosqichlarning
    /// tartibi bor, lekin nomzodni faqat keyingi bosqichga o'tkazish
    /// qoidasi ATAYLAB qo'yilmagan: amalda HR telefon suhbatini o'tkazib
    /// yuboradi (tanish odam), yoki nomzodni kengashdan keyin ikkinchi
    /// suhbatga qaytaradi. Qat'iy ketma-ketlik bunday holatlarda tizimni
    /// chetlab o'tishga majbur qilardi — va o'shanda tarix umuman
    /// yozilmasdan qolardi.
    /// </para>
    /// <para>
    /// Bosqichning MAVJUDLIGI va FAOLLIGI bu yerda tekshirilmaydi: domen
    /// ma'lumotnomani ko'rmaydi. U use-case qatlamida tekshiriladi
    /// («bosqich.topilmadi» / «bosqich.faol_emas»).
    /// </para>
    /// </remarks>
    public Result<CandidateStageEntry> RecordStage(
        Guid stageId,
        DateOnly date,
        StageOutcome outcome = StageOutcome.Pending,
        string? conclusion = null,
        Guid? recordedBy = null)
    {
        if (!IsActive)
        {
            return NotActive();
        }

        var last = _stageEntries.Count > 0 ? _stageEntries[^1] : null;

        if (last is not null && last.StageId == stageId && last.Outcome == StageOutcome.Pending)
        {
            var resolved = last.Resolve(outcome, date, conclusion, recordedBy);
            if (resolved.IsFailure)
            {
                return resolved.Error;
            }

            CurrentStageId = stageId;
            return last;
        }

        var created = CandidateStageEntry.Create(Id, stageId, date, outcome, conclusion, recordedBy);
        if (created.IsFailure)
        {
            return created.Error;
        }

        _stageEntries.Add(created.Value);
        CurrentStageId = stageId;

        return created.Value;
    }

    /// <summary>Korxona qarori bilan rad etadi.</summary>
    public Result Reject(DateOnly at, string? reason = null)
        => Close(CandidateStatus.Rejected, at, reason);

    /// <summary>Telegram'dan kelgan nomzodning suhbatini belgilaydi.</summary>
    /// <remarks>
    /// Faqat BIR MARTA yoziladi va keyin o'zgarmaydi: ikkinchi ariza
    /// bergan odamning yozuvi allaqachon bor va uni yangi suhbatga
    /// ko'chirish eski suhbatdagi javobni yo'qotardi.
    /// </remarks>
    public void SetTelegram(long chatId, string? language)
    {
        TelegramChatId ??= chatId;
        TelegramLanguage ??= Trim(language);
    }

    /// <summary>
    /// Rad etish xabari yuborilganini belgilaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Xabar HAQIQATAN ketgandan KEYIN chaqiriladi. Teskarisi bo'lsa
    /// (avval belgilab, keyin yuborish) tarmoq uzilganda nomzod
    /// javobsiz qolardi va buni hech kim bilmasdi.
    /// </para>
    /// <para>
    /// Ikkinchi marta chaqirilsa hech narsa qilmaydi: navbat
    /// «kamida bir marta» yuborishga qurilgan va takroriy xabar
    /// nomzod uchun eng yoqimsiz nosozlik bo'lardi.
    /// </para>
    /// </remarks>
    public void MarkRejectionNoticeSent(DateTimeOffset at) => RejectionNoticeSentAt ??= at;

    /// <summary>Nomzod o'zi voz kechdi.</summary>
    public Result Withdraw(DateOnly at, string? reason = null)
        => Close(CandidateStatus.Withdrawn, at, reason);

    /// <summary>
    /// Ishga olindi — voronkadan chiqadi.
    /// </summary>
    /// <remarks>
    /// Bu metodni FAQAT <c>AcceptJobOfferCommandHandler</c> chaqiradi va
    /// faqat xodim haqiqatan yaratilgandan keyin. Alohida endpoint yo'q:
    /// «ishga olindi» deb belgilash, lekin xodim kartasini yaratmaslik —
    /// bu aynan Excel'dagi holat (nomzod ro'yxatida «принят», xodimlar
    /// ro'yxatida yo'q). <see cref="CandidateStatus.Hired"/> holati esa
    /// nomzod voronkada faol bo'lib qolmasligini kafolatlaydi.
    /// </remarks>
    public Result MarkHired(DateOnly at) => Close(CandidateStatus.Hired, at, null);

    /// <summary>Tekshirilgan rezyume faylini biriktiradi.</summary>
    /// <remarks>
    /// Fayl mazmuni bu bosqichda ALLAQACHON tekshirilgan bo'lishi kerak
    /// (hajm va haqiqiy tur) — <c>EmployeeDocument.Attach</c> bilan bir xil
    /// mas'uliyat taqsimoti: domen faqat metama'lumotni yozadi.
    /// Eski fayl bo'lsa, uni diskdan olib tashlash chaqiruvchining ishi.
    /// </remarks>
    public Result AttachResume(string fileKey, string? fileName, string contentType, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(fileKey) || string.IsNullOrWhiteSpace(contentType) || sizeBytes <= 0)
        {
            return Error.Validation("nomzod.rezyume_bosh", "Rezyume fayli biriktirilishi shart.");
        }

        ResumeFileKey = fileKey;
        ResumeFileName = NormalizeFileName(fileName);
        ResumeContentType = contentType;
        ResumeSizeBytes = sizeBytes;

        return Result.Success();
    }

    /// <summary>Rezyume havolasini olib tashlaydi. Diskdagi faylni chaqiruvchi o'chiradi.</summary>
    public void RemoveResume()
    {
        ResumeFileKey = null;
        ResumeFileName = null;
        ResumeContentType = null;
        ResumeSizeBytes = null;
    }

    /// <summary>Voronkadan chiqarish — uchala yakun uchun umumiy qoida.</summary>
    /// <remarks>
    /// Faqat JARAYONDAGI nomzod yopiladi. Ikkinchi marta yopishga urinish
    /// xato bo'lib qaytadi: aks holda rad etilgan nomzodni «ishga olindi»
    /// deb belgilash mumkin bo'lardi va voronka hisoboti jimgina buzilardi.
    /// </remarks>
    private Result Close(CandidateStatus status, DateOnly at, string? reason)
    {
        if (!IsActive)
        {
            return NotActive();
        }

        Status = status;
        ClosedAt = at;
        ClosingNote = Trim(reason);

        return Result.Success();
    }

    private Error NotActive() => Error.Conflict(
        "nomzod.jarayonda_emas",
        $"Nomzod voronkadan chiqqan (holati: {Status}), bu amalni bajarib bo'lmaydi.");

    /// <summary>
    /// Tug'ilgan sana tekshiruvi — <c>Employee.ValidateBirthDate</c> bilan
    /// bir xil chegaralar.
    /// </summary>
    /// <remarks>
    /// Nusxa ATAYLAB: xodimdagi qoida <c>Employee</c> ning ichki (private)
    /// qismi va uni ochish domen chegarasini buzardi. Chegaralar esa aynan
    /// bir xil bo'lishi SHART — nomzod ishga olinganda o'sha sana xodim
    /// kartasiga ko'chadi va u yerda rad etilsa, ishga qabul yarim yo'lda
    /// to'xtardi. Xato kodi ham bir xil sababdan boshqa
    /// («nomzod.tugilgan_sana»): xato nomzod formasida ko'rsatiladi.
    /// </remarks>
    private static Result ValidateBirthDate(DateOnly birthDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (birthDate > today)
        {
            return Error.Validation("nomzod.tugilgan_sana", "Tug'ilgan sana kelajakda bo'lishi mumkin emas.");
        }

        return today.Year - birthDate.Year > 100
            ? Error.Validation("nomzod.tugilgan_sana", "Tug'ilgan sana haqiqiy emas.")
            : Result.Success();
    }

    /// <summary>Fayl nomini ko'rsatish uchun tozalaydi (yo'l qismi tashlanadi).</summary>
    private static string NormalizeFileName(string? fileName)
    {
        var trimmed = Trim(fileName);
        if (trimmed is null)
        {
            return "rezyume";
        }

        // Mijoz to'liq yo'l yuborishi mumkin («C:\Users\...\rezyume.pdf») —
        // saqlashda u ishlatilmaydi, lekin ekranda ham ko'rinmasligi kerak.
        var name = Path.GetFileName(trimmed.Replace('\\', '/'));

        return string.IsNullOrWhiteSpace(name) ? "rezyume" : name;
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
