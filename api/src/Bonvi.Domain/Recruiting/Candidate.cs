using Bonvi.Domain.Abstractions;
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

    /// <summary>Rezerv — hozir olinmadi, lekin kerak bo'lsa chaqiriladi.</summary>
    /// <remarks>
    /// <para>
    /// <see cref="Rejected"/> dan ATAYLAB ajratilgan: «bu odam bizga
    /// to'g'ri kelmadi» va «bu odam yaxshi, lekin bugun o'rin yo'q»
    /// butunlay boshqa qaror. Birinchisiga qaytilmaydi, ikkinchisiga esa
    /// aynan QAYTILADI — o'rin bo'shaganda HR shu ro'yxatdan qidiradi va
    /// izlanishni noldan boshlamaydi. Rad etilganlarga qo'shib yuborilsa,
    /// bu ro'yxat yo'qolardi.
    /// </para>
    /// <para>
    /// Nom tasodifiy emas: xodimlarda ham aynan shu tushuncha bor
    /// (<c>EmployeeFlagType.Reserve</c>, Excel «91.9 Резерв») va HR uni
    /// shu nom bilan biladi.
    /// </para>
    /// <para>
    /// Qiymat OXIRIGA qo'shildi — sanoqning umumiy qoidasi: o'rtaga
    /// qo'yilsa bazadagi eski qiymatlar jimgina siljib ketardi. Ustun
    /// <c>integer</c> bo'lgani uchun migratsiya kerak emas.
    /// </para>
    /// </remarks>
    Reserve = 5,
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

    /// <summary>
    /// Excel «91.1 Кенгаш хулосаси» varag'idan ko'chirilgan tarix.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bu nomzodlar bugun kelmagan — ular 2023–2024 yillarda kengashdan
    /// o'tgan va qarori allaqachon chiqarilgan odamlar. Manbani ajratish
    /// ikki joyda hal qiluvchi: bosh sahifadagi «davrda kelgan yangi
    /// ariza» hisoblagichi ularni sanamaydi (aks holda ko'chirish kuni
    /// 3110 ta «yangi ariza» ko'rinardi), va HR nomzodlar ro'yxatida
    /// arxivni bir bosishda yashira oladi.
    /// </para>
    /// <para>
    /// Qiymat OXIRIGA qo'shildi — sanoqning o'z qoidasi shuni talab
    /// qiladi: o'rtaga qo'yilsa bazadagi eski qiymatlar siljib ketardi.
    /// </para>
    /// </remarks>
    Excel = 3,
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
/// Nomzodning ma'lumoti (F.I.Sh., telefon, tug'ilgan sana, manzil,
/// mutaxassislik) ishga qabul buyrug'iga ko'chadi va 1-bosqichning MAVJUD
/// oqimi ishlaydi: xodim kartasi, mehnat munosabati, birinchi buyruq va
/// MAJBURIY sinov muddati. Rekruting o'zining «ishga qabul» yo'lini
/// yozmaydi — ikkinchi yo'l bo'lganida sinov muddati qoidasi («hamma
/// sinovdan o'tadi, istisnosiz») jimgina chetlab o'tilardi.
/// </para>
/// <para>
/// Bu o'tishning YAGONA kirish nuqtasi — <c>HireCandidateCommand</c>:
/// nomzod kartasidagi «Ishga olish» oynasi. Oklad va ish boshlash sanasi
/// ham o'sha formadan keladi. Ilgari ikkinchi yo'l ham bor edi (rasmiy
/// ish taklifi orqali), lekin u HR uchun ortiqcha oraliq qadam bo'lib
/// chiqdi va butunlay olib tashlandi
/// (<c>docs/SPEC-nomzod-yakuni.md</c>, 3-bo'lim).
/// </para>
/// <para>
/// <b>Bog'lanish nomzodda emas, ISHGA QABUL HODISASIDA saqlanadi</b>
/// (<c>EmploymentEvent.CandidateId</c>) — sabab
/// <c>HireCandidateCommandHandler</c> izohida batafsil.
/// </para>
/// </remarks>
public sealed class Candidate : AggregateRoot, IAuditable, ISoftDeletable
{
    private readonly List<CandidateStageEntry> _stageEntries = [];
    private readonly List<CandidateFile> _files = [];
    private readonly List<CandidateNote> _notes = [];

    /// <summary>Bitta nomzodga biriktiriladigan fayllarning eng katta soni.</summary>
    /// <remarks>
    /// <para>
    /// O'nta — pasport (2 varaq) + diplom (2 varaq) + rezyume + zaxira.
    /// Chegara kerak: fayllar diskda yotadi va chegarasiz bitta nomzod
    /// kartasi yuzlab telefon rasmiga aylanib ketishi mumkin.
    /// </para>
    /// <para>
    /// <b>Nega bu son shu yerda, <see cref="RecruitingRules"/> da emas.</b>
    /// U yerdagi qiymatlarning hammasi — tizim SOZLAMALARINING sukut
    /// qiymati va ular to'g'ridan-to'g'ri ishlatilmaydi (o'sha sinf izohi).
    /// Bu esa qat'iy chegara: uni <see cref="AttachFile"/> ning o'zi
    /// tekshiradi va son qoidadan ajralib turmasligi kerak.
    /// </para>
    /// </remarks>
    public const int MaxFiles = 10;

    /// <summary>Bitta nomzodning fayllari egallashi mumkin bo'lgan umumiy hajm.</summary>
    /// <remarks>
    /// <para>
    /// SAQLANGAN baytlar bo'yicha hisoblanadi (izohi
    /// <see cref="CandidateFile.SizeBytes"/> da): rasmlar serverda
    /// kichraytiriladi, ya'ni o'nta skan odatda ~4 MB bo'ladi va bu chegara
    /// faqat uzun PDF larda ishga tushadi. Chegarasiz esa bitta fayl
    /// chegarasi (20 MB) o'nta faylga 200 MB ruxsat berardi.
    /// </para>
    /// <para>
    /// Sozlamaga chiqarilmadi — bir mijoz, bir server: sozlamalar ekranidan
    /// diskni boshqarish taklifi shu yerda hech kimga foyda bermaydi.
    /// </para>
    /// </remarks>
    public const long MaxFilesTotalBytes = 60L * 1024 * 1024;

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

    /// <summary>
    /// Nomzod surati — <c>IFileStorage</c> kaliti.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rasm botdagi anketada so'raladi (selfi) va xodim rasmi bilan AYNAN
    /// bir quvurdan o'tadi: markazdan kvadrat qirqilib 512×512 JPEG
    /// bo'ladi. Ikkinchi quvur yozilmaydi.
    /// </para>
    /// <para>
    /// <b>Nega anketa jadvalida emas, shu yerda.</b> Rasm — nomzodning
    /// O'ZI, anketa savoliga javob emas: HR uni nomzodlar ro'yxatida ham
    /// ko'rsatishni so'rashi mumkin (xodimlar ro'yxatida shunday
    /// qilingan), o'shanda u allaqachon joyida bo'ladi. Anketasi yo'q,
    /// lekin rasmi bor nomzod ham mumkin — HR uni qo'lda yuklasa.
    /// </para>
    /// </remarks>
    public string? PhotoKey { get; private set; }

    /// <summary>Erkin izoh.</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// HR bu odam bilan KEYIN qachon bog'lanishni rejalashtirgani.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega vaqt emas, SANA.</b> «Qo'ng'iroq qildim, 5-6 kunda keladi
    /// dedi» — bu kalendar fakti, lahza emas: soati yo'q va bo'lishi ham
    /// kerak emas. Loyihada lahzalar <c>timestamptz</c>, kalendar faktlari
    /// esa <c>date</c> bo'lib saqlanadi (<see cref="ClosedAt"/> bilan bir
    /// xil qoida), taqqoslash esa Toshkent kalendarida bajariladi.
    /// </para>
    /// <para>
    /// <b>Nega bosqichlar tarixidan HOSIL QILINMAYDI.</b> Ro'yxat ekraniga
    /// ikki narsa kerak: saralanadigan ustun va <c>WHERE next_contact_date
    /// &lt;= bugun</c> filtri (u qator sonini ham beradi). Tarixdan
    /// hisoblash har qator uchun bog'langan ichki so'rov degani — indeksga
    /// tushmaydi va har sahifada bosqichlar jadvalini skanerlardi.
    /// <see cref="CurrentStageId"/> ham aynan shu sababdan bola
    /// yozuvlardan chiqarib olingan; bu — o'sha savdo, o'sha javob.
    /// Qabul qilingan narxi: bazaga qo'lda aralashilsa ustun tarixdan
    /// ajralib qolishi mumkin. Sanalarning TARIXI yuritilmaydi — «5-6
    /// kunda keladi» gapi baribir xulosa matnida turadi.
    /// </para>
    /// <para>
    /// <b>O'TGAN sana ham qabul qilinadi</b> —
    /// <see cref="InviteToInterview"/> dagi «kechagi kun terish xatosi»
    /// qoidasi bu yerga KO'CHIRILMAYDI. Sabab: maydonning butun vazifasi
    /// «bugun yoki kechikkan» ro'yxatini berish, ya'ni o'tgan sana bu yerda
    /// ODATIY holat. Bundan tashqari oldindan to'ldirilgan oyna sana o'tib
    /// ketgach saqlashdan bosh tortardi — HR umuman tegishni
    /// o'ylamagan maydon uchun. Suhbat vaqti boshqacha, chunki u
    /// NOMZODGA XABAR yuboradi; bu sana esa faqat HR ning o'z ro'yxatida
    /// qator paydo qiladi va xato ekranda ko'rinib turadi.
    /// </para>
    /// <para>
    /// Voronkadan chiqishda TOZALANADI, rezervda esa QOLADI — sababi
    /// <see cref="Close"/> izohida.
    /// </para>
    /// </remarks>
    public DateOnly? NextContactDate { get; private set; }

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

    /// <summary>
    /// Nomzod chaqirilgan SUHBAT VAQTI — nomzodga yuboriladigan taklifda
    /// aynan shu lahza turadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bazada UTC bo'lib yotadi, ekranda va Telegram xabarida Toshkent
    /// vaqtida chiziladi (<see cref="BonviTime"/>). Ikkisini aralashtirmaslik
    /// butun loyihada bir xil qoida.
    /// </para>
    /// <para>
    /// <c>null</c> — nomzod hali hech qanday suhbatga chaqirilmagan. Qiymat
    /// QAYTA yozilishi mumkin: suhbat ko'chirilsa HR yangi vaqt qo'yadi va
    /// nomzod yangi xabar oladi (<see cref="InviteToInterview"/>).
    /// </para>
    /// </remarks>
    public DateTimeOffset? InterviewAt { get; private set; }

    /// <summary>
    /// Suhbat taklifi Telegram'ga QACHON yuborilgani.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="RejectionNoticeSentAt"/> bilan AYNAN bir xil navbat
    /// (o'sha izohda batafsil): API faqat faktni yozadi, bot esa
    /// «yuborilmaganlar» ro'yxatini davriy so'rab turadi.
    /// </para>
    /// <para>
    /// Farqi bittada — rad etish bir marta bo'ladi, suhbat esa
    /// KO'CHIRILISHI mumkin. Shu sababli bu belgi yangi vaqt qo'yilganda
    /// TOZALANADI: nomzod o'zgargan vaqtni bilishi shart.
    /// </para>
    /// </remarks>
    public DateTimeOffset? InterviewNoticeSentAt { get; private set; }

    /// <summary>Voronkadan chiqqan sana (rad etilgan / ishga olingan / voz kechgan / rezerv).</summary>
    public DateOnly? ClosedAt { get; private set; }

    /// <summary>Voronkadan chiqish sababi — rad etish, voz kechish yoki rezerv izohi.</summary>
    public string? ClosingNote { get; private set; }

    /// <summary>Bosqichlar tarixi — yozilgan tartibda.</summary>
    public IReadOnlyList<CandidateStageEntry> StageEntries => _stageEntries.AsReadOnly();

    /// <summary>Biriktirilgan fayllar — YUKLASH tartibida.</summary>
    /// <remarks>
    /// Tartib <see cref="CandidateFile.CreatedAt"/> bo'yicha o'suvchi va
    /// buni repozitoriy ta'minlaydi (<c>Include</c> ichidagi
    /// <c>OrderBy</c>) — sababi <see cref="CandidateFile.CreatedAt"/>
    /// izohida: pasport 1-2-3 varaq bo'lib keladi.
    /// </remarks>
    public IReadOnlyList<CandidateFile> Files => _files.AsReadOnly();

    /// <summary>Amallar izohlari — yozilgan tartibda.</summary>
    /// <remarks>
    /// Bosqich yozuvi bo'lmagan amallarning izohi
    /// (<see cref="CandidateNote"/> izohida nega alohida jadval kerak
    /// bo'lgani yozilgan). Bo'sh bo'lishi ODATIY hol: izoh hech qachon
    /// majburiy emas.
    /// </remarks>
    public IReadOnlyList<CandidateNote> Notes => _notes.AsReadOnly();

    /// <summary>
    /// Botda to'ldirilgan anketa. Yo'q bo'lishi NORMAL.
    /// </summary>
    /// <remarks>
    /// Anketa faqat botdan keladi. HR qo'lda kiritgan nomzodda ham, bu
    /// funksiya paydo bo'lgunicha yozilgan nomzodlarda ham u <c>null</c> —
    /// va bu xato emas. Shu sababli uni o'qiydigan hech bir joy anketa
    /// bor deb faraz qilmasligi kerak (izohi
    /// <see cref="CandidateQuestionnaire"/> da).
    /// </remarks>
    public CandidateQuestionnaire? Questionnaire { get; private set; }

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
    /// ⭐ <b>HAR CHAQIRUV — YANGI YOZUV.</b> Mavjud yozuvning ustidan hech
    /// qachon yozilmaydi: bitta bosqich necha marta takrorlansa, tarixda
    /// shuncha qator bo'ladi («besh marta gaplashildi — beshta yozuv»,
    /// foydalanuvchi qarori 2026-09-16). Nomzodning joriy holati esa
    /// oxirgi yozuvning BOSQICHIdan kelib chiqadi — alohida «natija»
    /// maydoni yo'q (sababi <see cref="CandidateStageEntry"/> izohida).
    /// </para>
    /// <para>
    /// ⚠️ <b>Ilgari boshqacha edi va bu XATO edi.</b> Oxirgi yozuv ayni shu
    /// bosqich uchun va «kutilmoqda» bo'lsa, u JIMGINA to'ldirilardi. Ikki
    /// oqibati bor edi: (1) voronkada kartani sudrash yoki tugmani qayta
    /// bosish oldin yozilgan izohni <c>NULL</c> bilan almashtirardi — HR
    /// yozgan gap ogohlantirishsiz yo'qolardi; (2) HR lentadagi ESKIROQ
    /// «kutilmoqda» qatoriga natija yozsa, o'sha qator bo'sh qolib, pastga
    /// duplikat qator qo'shilardi. Endi mavjud yozuv faqat ATAYLAB, uning
    /// ID'si ko'rsatilib o'zgartiriladi (<see cref="EditStageEntry"/>).
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
        string? conclusion = null,
        Guid? recordedBy = null)
    {
        if (!IsActive)
        {
            return NotActive();
        }

        var created = CandidateStageEntry.Create(Id, stageId, date, conclusion, recordedBy);
        if (created.IsFailure)
        {
            return created.Error;
        }

        _stageEntries.Add(created.Value);
        CurrentStageId = stageId;

        return created.Value;
    }

    /// <summary>Tarixdagi BITTA yozuvni tuzatadi — sana va xulosa.</summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Yozuv ID bo'yicha topiladi, «oxirgisi» bo'yicha EMAS.</b> HR
    /// lentaning istalgan qatoriga bosadi va aynan o'sha qator ochiladi:
    /// bo'sh qolgan yozuvga xulosa yozish ham, ilgari yozilgan xulosadagi
    /// xatoni tuzatish ham shu metod. Mijoz qaysi qatorni tahrirlayotganini
    /// ANIQ aytadi, server esa taxmin qilmaydi.
    /// </para>
    /// <para>
    /// <b>Bosqich va tartib O'ZGARMAYDI</b> — sabab
    /// <see cref="CandidateStageEntry.Edit"/> va
    /// <see cref="CandidateStageEntry.RecordedAt"/> izohlarida. Shu sababli
    /// <see cref="CurrentStageId"/> ham tegilmaydi: tuzatish nomzodni
    /// voronkada SURMAYDI, aks holda eski qatordagi terish xatosini
    /// tuzatish odamni bir yil oldingi bosqichga qaytarib yuborardi.
    /// </para>
    /// <para>
    /// <b>Voronkadan chiqqan nomzodda ham ishlaydi</b> —
    /// <see cref="RecordStage"/> dan farqli. Rad etilgan nomzodning
    /// tarixidagi xato ham tuzatilishi kerak, tuzatish esa uni voronkaga
    /// qaytarmaydi: yangi bosqich yozish uchun baribir avval qaytarish
    /// kerak.
    /// </para>
    /// </remarks>
    public Result<CandidateStageEntry> EditStageEntry(
        Guid entryId,
        DateOnly date,
        string? conclusion)
    {
        var entry = _stageEntries.FirstOrDefault(item => item.Id == entryId);

        if (entry is null)
        {
            return Error.NotFound("bosqich_yozuvi.topilmadi", "Bosqich yozuvi topilmadi.");
        }

        var edited = entry.Edit(date, conclusion);

        return edited.IsFailure ? edited.Error : entry;
    }

    /// <summary>Korxona qarori bilan rad etadi.</summary>
    public Result Reject(DateOnly at, string? reason = null, Guid? closedBy = null)
        => Close(CandidateStatus.Rejected, at, reason, closedBy);

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

    /// <summary>
    /// Nomzodni belgilangan vaqtdagi suhbatga chaqiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Bosqichni bu metod O'ZGARTIRMAYDI.</b> Chaqirish odatda bosqich
    /// yozuvi bilan birga bajariladi («Кенгашга чақирилди»), lekin ikkisi
    /// alohida fakt: suhbat vaqtini ko'chirishda bosqich o'zgarmaydi, va
    /// aksincha — bayroqsiz bosqichga o'tkazishda taklif yuborilmaydi.
    /// Ikkalasini bitta tranzaksiyada bog'lash use-case ishi
    /// (<c>InviteCandidateToInterviewCommandHandler</c>).
    /// </para>
    /// <para>
    /// <b>O'tgan kun RAD ETILADI.</b> Bugungi kun ichidagi istalgan soat
    /// qabul qilinadi (HR ertalab «bugun soat 15:00» deb yozadi), lekin
    /// KECHAGI sana — bu deyarli har doim terish xatosi va u nomzodga
    /// allaqachon o'tib ketgan vaqtdagi taklifni yuborardi.
    /// </para>
    /// <para>
    /// Yuborilgan belgisi TOZALANADI: vaqt o'zgargan bo'lsa nomzod yangi
    /// xabar olishi shart, aks holda u eski vaqtga kelardi.
    /// </para>
    /// </remarks>
    public Result InviteToInterview(DateTimeOffset at, DateOnly today)
    {
        if (!IsActive)
        {
            return NotActive();
        }

        if (at.TashkentDate() < today)
        {
            return Error.Validation(
                "nomzod.suhbat_vaqti_otgan",
                "Suhbat vaqti o'tgan kunga qo'yilmaydi.");
        }

        InterviewAt = at.ToUniversalTime();
        InterviewNoticeSentAt = null;

        return Result.Success();
    }

    /// <summary>
    /// Suhbat taklifi yuborilganini belgilaydi.
    /// </summary>
    /// <remarks>
    /// <see cref="MarkRejectionNoticeSent"/> bilan bir xil qoida — xabar
    /// HAQIQATAN ketgandan keyin chaqiriladi. Farqi: bu yerda <c>??=</c>
    /// EMAS, oddiy o'zlashtirish. Sabab — belgi yangi taklifda tozalanadi
    /// va navbatga qayta tushadi; <c>??=</c> ikkinchi taklifni yuborilgan
    /// deb hisoblab, nomzodni eski vaqt bilan qoldirardi.
    /// </remarks>
    public void MarkInterviewNoticeSent(DateTimeOffset at) => InterviewNoticeSentAt = at;

    /// <summary>Nomzod o'zi voz kechdi.</summary>
    public Result Withdraw(DateOnly at, string? reason = null, Guid? closedBy = null)
        => Close(CandidateStatus.Withdrawn, at, reason, closedBy);

    /// <summary>
    /// Rezervga o'tkazadi — «hozir olinmadi, lekin kerak bo'lsa chaqiramiz».
    /// </summary>
    /// <remarks>
    /// <para>
    /// Qolgan yakunlar bilan bir xil qoidaga bo'ysunadi (<see cref="Close"/>):
    /// faqat JARAYONDAGI nomzod rezervga o'tadi. Rad etilgan nomzodni
    /// «aslida rezerv edi» deb o'zgartirish yo'li ataylab yopiq — u qarorni
    /// keyinchalik qayta yozish bo'lardi va voronka hisoboti jimgina
    /// buzilardi.
    /// </para>
    /// <para>
    /// Sabab <see cref="ClosingNote"/> ga yoziladi — bu CHIQISH izohi va
    /// nomzod voronkaga qaytganda u tozalanadi
    /// (<see cref="ReturnToFunnel"/>). Ya'ni u qarorning izohi, nomzod
    /// haqidagi uzoq muddatli tavsif emas; tavsif uchun <see cref="Note"/>
    /// bor va unga bu amal tegmaydi.
    /// </para>
    /// <para>
    /// ⭐ <b>Keyingi aloqa sanasi SAQLANADI</b> — qolgan yakunlardan
    /// ataylab farq (sababi <see cref="Close"/> izohida). Yangi sana
    /// berilsa u qo'yiladi, berilmasa esa mavjudi o'z joyida qoladi:
    /// rezervga o'tkazish sanani bekor qilish emas.
    /// </para>
    /// </remarks>
    public Result MoveToReserve(
        DateOnly at,
        string? reason = null,
        DateOnly? nextContactDate = null,
        Guid? closedBy = null)
    {
        var closed = Close(CandidateStatus.Reserve, at, reason, closedBy);
        if (closed.IsFailure)
        {
            return closed;
        }

        if (nextContactDate is { } date)
        {
            NextContactDate = date;
        }

        return Result.Success();
    }

    /// <summary>
    /// Voronkadan chiqqan nomzodni QAYTARADI — rezervdan, rad etilgandan
    /// yoki o'zi voz kechgandan keyin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Uchta manba va bu foydalanuvchi talabi (2026-09-16).</b>
    /// Ilgari faqat rezervdan qaytarish bor edi. Amalda esa rad etilgan
    /// odam o'zi qayta aloqaga chiqadi yoki sharoit o'zgaradi («o'shanda
    /// o'rin yo'q edi»), va HR uni qaytadan noldan kiritishga majbur
    /// bo'lardi — o'shanda butun tarix, anketa va fayllar yangi yozuvda
    /// yo'q bo'lardi.
    /// </para>
    /// <para>
    /// <b>«Ishga olindi» dan QAYTARILMAYDI.</b> U endi xodim: qaytarilsa,
    /// bitta odam bir vaqtning o'zida ham xodim, ham faol nomzod bo'lib
    /// qolardi. Qayta ishga olish uchun yangi nomzod yozuvi ochiladi.
    /// </para>
    /// <para>
    /// ⭐ <b>SABAB — rad etish va voz kechishdan qaytarishda MAJBURIY.</b>
    /// Bu qarorni BEKOR QILISH, ya'ni «nega fikr o'zgardi» degan savol
    /// albatta tug'iladi — olti oydan keyin ham. Rezervdan qaytarishda
    /// esa ixtiyoriy: rezervning butun ma'nosi «o'rin bo'shasa
    /// chaqiramiz», ya'ni qaytish o'sha holatning TABIIY yakuni va
    /// tushuntirish talab qilmaydi.
    /// </para>
    /// <para>
    /// ⭐ <b>Sabab LENTAGA yoziladi</b> (<see cref="CandidateNoteKind.ReturnToFunnel"/>)
    /// — <see cref="ClosingNote"/> ga EMAS: qaytarish o'sha maydonni
    /// tozalaydi va izoh yozilgan zahoti o'chib ketardi. Yozuvni domen
    /// o'zi qo'shadi, chaqiruvchi emas: «qaytarildi, lekin tarixda izi
    /// yo'q» degan holat umuman bo'lmasligi kerak.
    /// </para>
    /// <para>
    /// <b>Bosqich SAQLANADI.</b> Nomzod qaysi bosqichda to'xtagan bo'lsa,
    /// o'sha yerdan davom etadi: kengashdan o'tgan odamni qaytadan
    /// telefon suhbatiga tushirish ma'nosiz (bir xil sabab
    /// <see cref="Reassign"/> da izohlangan).
    /// </para>
    /// <para>
    /// <b>Chiqish sanasi va sababi TOZALANADI.</b> Ular «nomzod
    /// voronkadan nega chiqqan» degan savolning javobi; nomzod qaytgach
    /// bunday fakt endi yo'q. Qoldirilsa, faol nomzod kartasida
    /// «voronkadan chiqqan: …» yozuvi turaverardi va
    /// <see cref="ClosedAt"/> bo'yicha hisoblaydigan hisobot uni
    /// yopilganlar qatorida sanardi. Sabab esa lentada qoladi — yuqoriga
    /// qarang.
    /// </para>
    /// <para>
    /// ⭐ <b>Rad etish xabarining belgisi ham TOZALANADI.</b> Nomzod
    /// qaytarilgach jarayon boshidan ketadi; agar u yana rad etilsa,
    /// Telegram xabari QAYTA yuborilishi kerak
    /// (<see cref="MarkRejectionNoticeSent"/> dagi <c>??=</c> aks holda
    /// uni «allaqachon xabardor» deb hisoblab, odamni javobsiz
    /// qoldirardi).
    /// </para>
    /// <para>
    /// <b>Keyingi aloqa sanasiga esa TEGILMAYDI</b> (yangisi berilmasa):
    /// rezervda turgan odamga qo'ng'iroq qilish rejasi u voronkaga
    /// qaytgach ham o'z kuchida qoladi.
    /// </para>
    /// </remarks>
    /// <param name="reason">
    /// Qaytarish sababi. Rad etilgan va voz kechgan nomzodda MAJBURIY.
    /// </param>
    /// <param name="returnedBy">Kim qaytardi — lentadagi yozuv uchun.</param>
    /// <param name="nextContactDate">Keyingi aloqa sanasi; berilmasa tegilmaydi.</param>
    public Result ReturnToFunnel(
        string? reason = null,
        Guid? returnedBy = null,
        DateOnly? nextContactDate = null)
    {
        if (Status is not (CandidateStatus.Reserve or CandidateStatus.Rejected or CandidateStatus.Withdrawn))
        {
            return Error.Conflict(
                "nomzod.qaytarilmaydi",
                $"Nomzodni voronkaga qaytarib bo'lmaydi (holati: {Status}).");
        }

        var izoh = Trim(reason);

        if (izoh is null && Status is not CandidateStatus.Reserve)
        {
            return Error.Validation(
                "nomzod.qaytarish_sababi_yoq",
                "Qaytarish sababini yozing.");
        }

        Status = CandidateStatus.InProgress;
        ClosedAt = null;
        ClosingNote = null;
        RejectionNoticeSentAt = null;

        if (izoh is not null)
        {
            AddNote(CandidateNoteKind.ReturnToFunnel, izoh, returnedBy);
        }

        if (nextContactDate is { } date)
        {
            NextContactDate = date;
        }

        return Result.Success();
    }

    /// <summary>
    /// Ishga olindi — voronkadan chiqadi.
    /// </summary>
    /// <remarks>
    /// Bu metodni FAQAT ishga qabul oqimini chaqiradigan use-case
    /// ishlatadi (<c>HireCandidateCommandHandler</c>) va faqat xodim
    /// haqiqatan yaratilgandan keyin. «Ishga
    /// olindi» deb belgilaydigan ALOHIDA endpoint yo'q: holatni qo'yib,
    /// xodim kartasini yaratmaslik — bu aynan Excel'dagi holat (nomzod
    /// ro'yxatida «принят», xodimlar ro'yxatida yo'q).
    /// <see cref="CandidateStatus.Hired"/> holati esa nomzod voronkada
    /// faol bo'lib qolmasligini kafolatlaydi.
    /// </remarks>
    public Result MarkHired(DateOnly at) => Close(CandidateStatus.Hired, at, null);

    /// <summary>
    /// Tekshirilgan faylni nomzodga biriktiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fayl mazmuni bu bosqichda ALLAQACHON tekshirilgan va rasm bo'lsa
    /// kichraytirilgan bo'lishi kerak (<c>ScaledDocumentStore</c>) —
    /// <c>EmployeeDocument.Attach</c> bilan bir xil mas'uliyat taqsimoti:
    /// domen faqat metama'lumotni yozadi.
    /// </para>
    /// <para>
    /// <b>Tekshiruvlar tartibi muhim.</b> Avval soni, keyin umumiy hajmi,
    /// eng oxirida faylning o'zi: birinchi ikkitasi nomzodning invarianti
    /// va ular AYNAN shu yerda tekshirilishi shart (sababi
    /// <see cref="CandidateFile"/> izohida — sanoqni so'rov bilan olish
    /// poyga bo'lardi). Chegaraga TENG kelgan fayl qabul qilinadi, oshgani
    /// esa rad etiladi.
    /// </para>
    /// <para>
    /// ⭐ <b>Nomzodning JARAYONDA bo'lishi TALAB QILINMAYDI</b> — bu
    /// <see cref="RecordStage"/> va yopish amallaridan ataylab farq
    /// qiladi. HR diplom nusxasini rad etilgan nomzodga ertasi kuni ham
    /// yuklaydi (hujjat keyin keladi) va bunga to'sqinlik qilish faylni
    /// tizimdan tashqariga — telefondagi galereyaga — surib qo'yardi.
    /// </para>
    /// </remarks>
    public Result<CandidateFile> AttachFile(
        string fileKey,
        string? fileName,
        string contentType,
        long storedBytes,
        Guid? createdBy = null)
    {
        if (_files.Count >= MaxFiles)
        {
            return Error.Validation(
                "nomzod.rezyume_kop",
                $"Bitta nomzodga eng ko'pi {MaxFiles} ta fayl biriktiriladi.");
        }

        if (_files.Sum(file => file.SizeBytes) + storedBytes > MaxFilesTotalBytes)
        {
            return Error.Validation(
                "nomzod.rezyume_joy",
                $"Nomzod fayllarining umumiy hajmi {MaxFilesTotalBytes / (1024 * 1024)} MB dan oshib ketadi.");
        }

        var created = CandidateFile.Create(Id, fileKey, fileName, contentType, storedBytes, createdBy);
        if (created.IsFailure)
        {
            return created.Error;
        }

        _files.Add(created.Value);

        return created.Value;
    }

    /// <summary>
    /// Fayllarni XODIM kartasiga topshiradi: ro'yxatdan chiqaradi va
    /// ularni qaytaradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega «o'chirish» emas, TOPSHIRISH.</b> Foydalanuvchi qarori
    /// (2026-09-20): nomzod xodimga aylanganda uning hujjatlari xodim
    /// kartasiga o'tsin va «Номзодлик ҳужжатлари» turi bilan saqlansin.
    /// Ilgari ular nomzodda qolib ketardi va HR hammasini qaytadan
    /// yuklashga majbur bo'lardi.
    /// </para>
    /// <para>
    /// ⚠️ <b>Diskdagi baytlar TEGILMAYDI.</b> Bu
    /// <see cref="RemoveFile"/> dan asosiy farq: u yerda yozuv ham,
    /// fayl ham o'chadi, bu yerda esa fayl JOYIDA qoladi va unga endi
    /// xodim hujjati ishora qiladi (kalit AYNAN o'sha, nusxa
    /// olinmaydi — disk ikki barobar band bo'lmaydi). Chaqiruvchi
    /// baytlarni o'chirmasligi SHART: o'chirsa, ko'chirilgan hujjat
    /// birinchi ochilishdayoq «fayl yo'q» bo'lib chiqardi.
    /// </para>
    /// <para>
    /// Nomzod yozuvining o'zi qoladi — u rekrutingning tarixi (vakansiya
    /// sanog'i, kengash xulosasi va bosh sahifa statistikasi unga
    /// tayanadi). Faqat fayllar ko'chadi, chunki hujjatning egasi endi
    /// xodim kartasi.
    /// </para>
    /// <para>
    /// Holat TEKSHIRILMAYDI: metodni faqat ishga qabul oqimi va eski
    /// yozuvlarni tuzatuvchi bir martalik ko'chirish chaqiradi,
    /// ikkalasi ham nomzod xodimga aylanganini allaqachon biladi.
    /// </para>
    /// </remarks>
    public IReadOnlyList<CandidateFile> HandOverFiles()
    {
        var taken = _files.ToArray();
        _files.Clear();

        return taken;
    }

    /// <summary>
    /// Faylni olib tashlaydi va uning saqlash kalitini qaytaradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Yozuv butunlay o'chadi (yumshoq o'chirish yo'q — sababi
    /// <see cref="CandidateFile"/> izohida), diskdagi baytlarni esa
    /// CHAQIRUVCHI o'chiradi va aynan <c>SaveChangesAsync</c> dan KEYIN:
    /// saqlash yiqilsa, yozuv hali ham joyida turgan faylga ishora qilib
    /// qolishi kerak. Shu sababli metod kalitni qaytaradi — chaqiruvchi
    /// uni o'zi izlab yurmaydi.
    /// </para>
    /// <para>
    /// Noma'lum identifikator — 404. Bu amal IDEMPOTENT emas
    /// (surat o'chirishdan farqli): chaqiruvchi aniq bir qatorni nomlagan
    /// va uni jimgina «muvaffaqiyat» deb qaytarish ekranda allaqachon
    /// yo'q faylni ko'rsatib turgan eski ro'yxatni yashirardi.
    /// </para>
    /// </remarks>
    public Result<string> RemoveFile(Guid fileId)
    {
        var file = _files.Find(candidateFile => candidateFile.Id == fileId);
        if (file is null)
        {
            return Error.NotFound("nomzod.rezyume_topilmadi", "Nomzodning bunday fayli yo'q.");
        }

        _files.Remove(file);

        return file.FileKey;
    }

    /// <summary>Suratni biriktiradi yoki olib tashlaydi (<c>null</c>).</summary>
    /// <remarks>
    /// Rasm ALLAQACHON tekshirilgan va qirqilgan bo'lishi kerak — domen
    /// faqat kalitni yozadi (<see cref="AttachFile"/> bilan bir xil
    /// mas'uliyat taqsimoti). Eski faylni diskdan olib tashlash
    /// chaqiruvchining ishi.
    /// </remarks>
    public void SetPhoto(string? photoKey) => PhotoKey = photoKey;

    /// <summary>
    /// Anketani biriktiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Anketa bir marta, nomzod yaratilayotgan tranzaksiyada
    /// biriktiriladi va keyin o'zgarmaydi (sabab
    /// <see cref="CandidateQuestionnaire"/> izohida). Ikkinchi marta
    /// biriktirishga urinish — chaqiruvchi qatlamdagi xato: nomzod
    /// anketani ikki marta to'ldira olmaydi, takroriy ariza telefon
    /// qadamida ushlanadi.
    /// </para>
    /// <para>
    /// Anketa <see cref="Entity.Id"/> ga bog'lanishi tekshiriladi: boshqa
    /// nomzodning anketasi ulanib qolsa, HR bir odamning javoblarini
    /// ikkinchisining kartasida ko'rardi.
    /// </para>
    /// </remarks>
    public Result AttachQuestionnaire(CandidateQuestionnaire questionnaire)
    {
        ArgumentNullException.ThrowIfNull(questionnaire);

        if (Questionnaire is not null)
        {
            return Error.Conflict("nomzod.anketa_bor", "Nomzodda anketa allaqachon bor.");
        }

        if (questionnaire.CandidateId != Id)
        {
            return Error.Validation("nomzod.anketa_begona", "Anketa boshqa nomzodga tegishli.");
        }

        Questionnaire = questionnaire;
        return Result.Success();
    }

    /// <summary>
    /// Amal izohini yozadi — bosqich yozuvi TEGISHLI bo'lmagan amallar
    /// uchun.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nega alohida jadval va nega uchta mavjud maydonning bittasi ham
    /// yaramaganini <see cref="CandidateNote"/> izohi tushuntiradi.
    /// </para>
    /// <para>
    /// ⚠️ <b>Faqat BO'SH BO'LMAGAN matn bilan chaqiriladi.</b> Izoh
    /// hamma joyda IXTIYORIY, ya'ni «izoh yozilmadi» degan holat yozuvsiz
    /// ko'rinadi — ishlovchi <c>string.IsNullOrWhiteSpace</c> ni tekshirib
    /// bu metodni umuman chaqirmaydi. Shartnoma buzilsa
    /// <see cref="ArgumentException"/> keladi (nega xato kodi emasligi
    /// <see cref="CandidateNote.Create"/> izohida). Aynan shu sababdan
    /// javob <c>Result</c> ga O'RALMAYDI: yiqiladigan yo'l yo'q, o'ram
    /// esa chaqiruvchini mavjud bo'lmagan xatoni tekshirishga undardi.
    /// </para>
    /// <para>
    /// Nomzodning holati TEKSHIRILMAYDI: izohni tug'diradigan amalning
    /// o'zi (o'tkazish, rezervdan qaytarish, suhbatni ko'chirish) allaqachon
    /// o'z qoidasini tekshirgan bo'ladi va ikkinchi tekshiruv shu qoidani
    /// ikki joyda saqlash degani bo'lardi.
    /// </para>
    /// </remarks>
    public CandidateNote AddNote(CandidateNoteKind kind, string text, Guid? createdBy = null)
    {
        var note = CandidateNote.Create(Id, kind, text, createdBy);
        _notes.Add(note);

        return note;
    }

    /// <summary>
    /// Keyingi aloqa sanasini qo'yadi yoki tozalaydi (<c>null</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tekshiruv YO'Q — o'tgan sana ham qabul qilinadi (sababi
    /// <see cref="NextContactDate"/> izohida).
    /// </para>
    /// <para>
    /// ⚠️ <c>null</c> — «tozalash», «tegilmasin» EMAS. «Tegilmasin»
    /// holatini chaqiruvchi metodni umuman chaqirmasdan bildiradi: HTTP
    /// shartnomasida shu sababli ikkita alohida maydon bor
    /// (<c>nextContactDate</c> va <c>clearNextContact</c>) — voronkadagi
    /// «sudrab o'tkazish» harakati oynasiz ishlaydi va u HR besh daqiqa
    /// oldin qo'ygan sanani jimgina o'chirib yuborishi mumkin emas.
    /// </para>
    /// </remarks>
    public void SetNextContactDate(DateOnly? date) => NextContactDate = date;

    /// <summary>Voronkadan chiqarish — barcha yakunlar uchun umumiy qoida.</summary>
    /// <remarks>
    /// <para>
    /// Faqat JARAYONDAGI nomzod yopiladi. Ikkinchi marta yopishga urinish
    /// xato bo'lib qaytadi: aks holda rad etilgan nomzodni «ishga olindi»
    /// deb belgilash mumkin bo'lardi va voronka hisoboti jimgina buzilardi.
    /// </para>
    /// <para>
    /// ⭐ <b>Keyingi aloqa sanasi shu yerda TOZALANADI</b> — barcha
    /// chiqish yo'llari bitta joydan o'tgani uchun qoidani ham bitta
    /// joyda ushlab turish mumkin. Sabab: rad etilgan nomzod aloqa sanasi
    /// bilan «bugun bog'lanish kerak» ro'yxatida abadiy qolib ketardi va
    /// HR ro'yxatga umuman ishonmay qo'yardi.
    /// </para>
    /// <para>
    /// <b>Yagona istisno — <see cref="CandidateStatus.Reserve"/>.</b>
    /// Rezerv chiqish emas, KECHIKTIRISH: holatning butun ma'nosi «o'rin
    /// bo'shaganda chaqiramiz» (o'sha qiymat izohi), ya'ni aloqa sanasi
    /// aynan o'sha yerda o'rinli. Shu sababli u saqlanib qoladi.
    /// </para>
    /// </remarks>
    private Result Close(CandidateStatus status, DateOnly at, string? reason, Guid? closedBy = null)
    {
        if (!IsActive)
        {
            return NotActive();
        }

        Status = status;
        ClosedAt = at;
        ClosingNote = Trim(reason);

        if (status != CandidateStatus.Reserve)
        {
            NextContactDate = null;
        }

        /*
          ⭐ Sabab LENTAGA ham tushadi (2026-09-16).

          `ClosingNote` — nomzodning HOZIRGI holatini tushuntiradigan
          bitta maydon va u qaytarishda tozalanadi
          (`ReturnToFunnel`), ya'ni «nega rad etilgan edi» degan fakt
          o'sha lahzada yo'qolardi. Lentadagi yozuv esa hech qachon
          o'chmaydi: qaytarilgan nomzodning kartasi «08.09 rad etildi —
          sabab X» → «16.09 qaytarildi — sabab Y» bo'lib o'qiladi.

          Ikki joyda turgani takror EMAS, ikki xil savolning javobi:
          biri «hozir nima bo'lyapti», ikkinchisi «nima bo'lgan edi».

          Sabab yozilmagan bo'lsa qator ham yozilmaydi: `CandidateNote`
          bo'sh matnni qabul qilmaydi va «sababsiz rad etildi» degan
          bo'sh qator lentaga hech narsa qo'shmasdi — holatning o'zi
          kartaning tepasida turadi.
        */
        if (ClosingNote is { } izoh && NoteKindFor(status) is { } kind)
        {
            AddNote(kind, izoh, closedBy);
        }

        return Result.Success();
    }

    /// <summary>Yakun turi uchun lenta yorlig'i; «ishga olindi» da — yo'q.</summary>
    /// <remarks>
    /// Ishga qabul mehnat hodisasi bilan rasmiylashtiriladi va xodim
    /// kartasida ko'rinadi — nomzod lentasidagi ikkinchi nusxa ortiqcha
    /// bo'lardi.
    /// </remarks>
    private static CandidateNoteKind? NoteKindFor(CandidateStatus status) => status switch
    {
        CandidateStatus.Rejected => CandidateNoteKind.Rejected,
        CandidateStatus.Withdrawn => CandidateNoteKind.Withdrawn,
        CandidateStatus.Reserve => CandidateNoteKind.Reserved,
        _ => null,
    };

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

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
