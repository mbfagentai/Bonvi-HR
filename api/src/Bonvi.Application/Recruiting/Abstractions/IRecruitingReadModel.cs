using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Abstractions;

/// <summary>
/// Rekruting ro'yxatlari uchun <b>o'qish</b> porti.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega repozitoriylar yetmaydi.</b> <c>ICandidateRepository</c> va
/// <c>IVacancyRepository</c> agregatni identifikator bo'yicha yuklaydi —
/// bu yozish tomonining vazifasi. Ro'yxat ekrani esa boshqa narsani
/// so'raydi: vakansiya, bosqich va lavozim NOMLARI bilan birlashtirilgan,
/// filtrlangan, saralangan va sahifalangan jadval. Bu proyeksiyani
/// repozitoriyga qo'shish domenni ko'rsatish modeliga bog'lab qo'yardi
/// (<c>IEmployeeReadModel</c> va <c>IIncentiveReadModel</c> bilan bir xil
/// sabab).
/// </para>
/// <para>
/// <b>Filtr, saralash va sahifalash — BAZADA.</b> Nomzodlar soni xodimlar
/// sonidan tez o'sadi: har bir vakansiyaga o'nlab anketa tushadi va rad
/// etilganlar HECH QACHON o'chirilmaydi (ular kelasi yilgi izlanishning
/// asosiy bazasi). Xotirada filtrlash bir yildan keyin butun jadvalni har
/// sahifa uchun tarmoqdan olib o'tishga aylanardi.
/// </para>
/// </remarks>
public interface IRecruitingReadModel
{
    /// <summary>Nomzodlar ro'yxati — rekrutingning asosiy ekrani.</summary>
    Task<CandidatePage> SearchCandidatesAsync(
        CandidateSearchFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rad etilgan, lekin Telegram xabarini hali olmagan nomzodlar.
    /// </summary>
    /// <remarks>
    /// Faqat botga kerak va faqat uchta shart birga bajarilganda:
    /// nomzod rad etilgan, Telegram suhbati ma'lum va xabar hali
    /// yuborilmagan. Uchalasi ham qisman indeks bilan qoplangan
    /// (<c>RecruitingConfigurations</c>).
    /// </remarks>
    /// <summary>
    /// Shu odamning ENG DOLZARB arizasi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// «Dolzarb» tartibi: avval JARAYONDAGI ariza (u har qanday
    /// muddatdan kuchliroq), keyin eng OXIRGI yopilgani. Muddatni
    /// oxirgi arizadan hisoblash shart — aks holda 2019-yilgi eski
    /// yozuv bugungi qarorni bekor qilardi.
    /// </para>
    /// <para>
    /// ⭐ <b>Ikkita kalit: telefon VA Telegram hisobi.</b> Faqat
    /// telefon yetmaydi — bir odamda ikkita SIM bo'lishi mumkin va u
    /// o'sha hisobdan yangi raqam bilan qaytib kelardi. Ikkalasi OR
    /// bilan birlashtiriladi.
    /// </para>
    /// <para>
    /// ⚠️ Vakansiya bo'yicha AJRATILMAYDI (2026-09-08 qarori): to'siq
    /// odamga qo'yiladi, arizaga emas. Vakansiya bo'yicha ajratilsa,
    /// bir odam har kuni boshqa vakansiyaga anketa tashlab, o'sha
    /// muammoni qaytarardi.
    /// </para>
    /// </remarks>
    /// <para>
    /// ⭐ <b>Uchinchi kalit — F.I. + tug'ilgan sana (2026-10-30).</b> Prod'da
    /// bir odam uch marta topshirgan: har safar BOSHQA raqam va BOSHQA
    /// Telegram hisobi bilan. Ikkala kalit ham begona edi, ism va sana
    /// esa bir xil. Shart <see cref="CandidatePersonKey"/> izohida.
    /// </para>
    Task<CandidateApplicationRow?> FindLatestApplicationAsync(
        string phoneDigits,
        long? telegramChatId,
        CandidatePersonKey? person = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Shu telefon raqamli nomzodlar — HAMMA holatda, Excel arxivi ham.
    /// </summary>
    /// <remarks>
    /// <para>
    /// HR nomzodni qo'lda kiritayotganda «bu odam bizda bormi?» degan
    /// savolga javob. Ro'yxat endpoint'i bunga yaramaydi: u sukut bo'yicha
    /// rezervdagi, rad etilgan va ishga olinganlarni yashiradi
    /// (<see cref="CandidateSearchFilter.HideReserve"/> va egizaklari).
    /// </para>
    /// <para>
    /// Tartib <see cref="FindLatestApplicationAsync"/> bilan bir xil:
    /// avval jarayondagi, keyin eng yangisi.
    /// </para>
    /// </remarks>
    /// <param name="phoneDigits">Kanonik raqamning faqat raqamlari: <c>998901234567</c>.</param>
    /// <param name="exceptId">Tahrirlanayotgan nomzodning o'zi — natijaga tushmaydi.</param>
    /// <param name="limit">Eng ko'p nechta.</param>
    /// <param name="cancellationToken">Bekor qilish.</param>
    Task<IReadOnlyList<CandidatePhoneMatchDto>> FindPhoneMatchesAsync(
        string phoneDigits,
        Guid? exceptId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// F.I. va tug'ilgan sanasi mos JARAYONDAGI nomzodlar — eng yangisi birinchi.
    /// </summary>
    /// <remarks>
    /// Onlayn anketa uchun (egasi, 2026-10-31): odam botga bir raqam bilan yozib, anketaga boshqasini
    /// kiritsa, telefon kaliti ushlamaydi va ikkinchi karta ochilardi. Kalit botdagi uchinchi kalitning
    /// o'zi (<see cref="CandidatePersonKey"/>).
    /// </remarks>
    Task<IReadOnlyList<Guid>> FindInProgressByPersonAsync(
        CandidatePersonKey person,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RejectionNoticeDto>> ListPendingRejectionNoticesAsync(
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suhbatga chaqirilgan, lekin taklif xabarini hali olmagan nomzodlar.
    /// </summary>
    /// <remarks>
    /// Yuqoridagining egizagi, lekin sharti nomzodning HOLATIGA bog'liq
    /// emas: taklif voronkada turgan nomzodga yuboriladi. Uchta shart —
    /// suhbat vaqti qo'yilgan, Telegram suhbati ma'lum va xabar hali
    /// ketmagan.
    /// </remarks>
    Task<IReadOnlyList<InterviewNoticeDto>> ListPendingInterviewNoticesAsync(
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Vakansiyalar ro'yxati — nomzodlar sanog'i bilan.</summary>
    Task<VacancyPage> ListVacanciesAsync(
        VacancyListFilter filter,
        CancellationToken cancellationToken = default);
}

/// <summary>Nomzodlar ro'yxatini saralash ustunlari.</summary>
/// <remarks>
/// Ataylab enum (<c>EmployeeSortField</c> bilan bir xil sabab): mijozdan
/// kelgan matn to'g'ridan-to'g'ri SQL'ga tushmaydi va noma'lum ustun
/// so'ralganda sukut bo'yicha tartib ishlaydi.
/// </remarks>
public enum CandidateSortField
{
    /// <summary>
    /// Oxirgi murojaat (sukut bo'yicha, yangidan eskiga): kartaga anketa keyin qo'shilgan bo'lsa —
    /// o'sha lahza, bo'lmasa karta ochilgan vaqt (<c>Candidate.LastAppliedAt</c>, 2026-10-30).
    /// </summary>
    CreatedAt = 0,

    /// <summary>Familiya, ism.</summary>
    Name = 1,

    /// <summary>Voronkadagi bosqich tartibi.</summary>
    Stage = 2,

    /// <summary>
    /// Keyingi aloqa sanasi — «bugun kim bilan gaplashish kerak».
    /// </summary>
    /// <remarks>
    /// Sanasi YO'Q qatorlar IKKALA yo'nalishda ham oxirida turadi
    /// (amalga oshirilishi <c>RecruitingReadModel</c> da). PostgreSQL
    /// o'z-o'zidan <c>NULL</c> ni o'sishda oxiriga, kamayishda esa
    /// BOSHIGA qo'yadi — ya'ni «keyingi aloqa, yangidan eskiga» tanlovi
    /// bir ekran chiziqcha bilan ochilardi.
    /// </remarks>
    NextContact = 3,
}

/// <summary>Vakansiyalar ro'yxatini saralash ustunlari.</summary>
public enum VacancySortField
{
    /// <summary>Ochilgan sana (sukut bo'yicha, yangidan eskiga).</summary>
    OpenedAt = 0,

    /// <summary>
    /// Vakansiya nomi (e'lon nomi).
    /// </summary>
    /// <remarks>
    /// Ilgari bu <c>Position</c> edi va lavozim nomi bo'yicha saralardi.
    /// Vakansiyada lavozim qolmagach (sababi <c>Vacancy</c> izohida),
    /// ro'yxatning yagona matnli ustuni e'lon nomi bo'lib qoldi.
    /// </remarks>
    Title = 1,
}

/// <summary>
/// Nomzodlar ro'yxatining filtri.
/// </summary>
/// <param name="Search">
/// F.I.Sh. yoki TELEFON bo'yicha qidiruv. JSHSHIR va pasport ATAYLAB yo'q:
/// nomzodda ular umuman bo'lmaydi — u hali hujjat topshirmagan. Qoidaning
/// qolgan qismi xodimlar ro'yxatidagi bilan aynan bir xil (kirill imlo
/// yig'ilishi, telefon ajratgichlari) va u bitta joydan —
/// <c>SearchFolding</c> dan keladi.
/// </param>
/// <param name="VacancyId">Vakansiya bo'yicha filtr.</param>
/// <param name="StageId">Bosqich bo'yicha filtr — voronka ustunini chizish uchun.</param>
/// <param name="Status">
/// Holat bo'yicha filtr. Bo'sh — ISHGA OLINGANLAR, RAD ETILGANLAR, O'ZI
/// VOZ KECHGANLAR va REZERVDAGILARDAN tashqari hammasi (sabablari
/// <see cref="CandidateSearchFilter.HideHired"/>,
/// <see cref="CandidateSearchFilter.HideRejected"/>,
/// <see cref="CandidateSearchFilter.HideWithdrawn"/> va
/// <see cref="CandidateSearchFilter.HideReserve"/> izohlarida).
/// </param>
/// <param name="Source">Manba bo'yicha filtr: qo'lda kiritilgan / botdan kelgan.</param>
/// <param name="Page">Sahifa raqami, 1 dan.</param>
/// <param name="PageSize">Sahifadagi yozuvlar soni.</param>
/// <param name="Sort">Saralash ustuni.</param>
/// <param name="Descending">Teskari tartib.</param>
/// <param name="IncludeArchive">Excel arxivi ham ko'rsatilsinmi (sukut: yo'q).</param>
/// <param name="ContactDueOn">
/// Shu SANAGA (yoki undan oldin) aloqa rejalashtirilgan nomzodlar —
/// «bugun kim bilan gaplashish kerak». <c>null</c> — filtr yo'q.
/// <para>
/// ⭐ <b>Nega bayroq emas, SANA.</b> O'qish modeli faqat
/// <c>AppDbContext</c> ni oladi va uning soati YO'Q: «bugun» — use-case
/// qatlamining bilimi (butun loyihada shunday, <c>IClock</c>
/// handlerlarda turadi). <c>SearchCandidatesQueryHandler</c> shu sababli
/// <c>_clock.Today</c> ni O'ZI beradi. Teskarisi — o'qish modeliga soat
/// ulash — uni «faqat so'rovni biladi» qoidasidan chiqarardi.
/// </para>
/// </param>
/// <param name="RejectedSection">
/// «Rad etilganlar» BO'LIMI: <c>Rejected</c> VA <c>Withdrawn</c>.
/// <para>
/// Nega ko'p qiymatli holat filtri emas — <see cref="HideRejected"/>
/// izohida. Bayroq holat tanlovi bilan BIRGA ishlaydi:
/// <c>rejected=true</c> + <c>status=Withdrawn</c> faqat voz kechganlarni
/// beradi.
/// </para>
/// </param>
/// <param name="AllStatuses">
/// BARCHA holatlar — barcha <c>Hide…</c> qoidalari o'chadi (global qidiruv).
/// </param>
/// <param name="IncludeRejected">
/// Rad etilganlar va o'zi voz kechganlar ham qo'shiladi — faqat
/// <see cref="HideRejected"/> va <see cref="HideWithdrawn"/> o'chadi
/// («Nomzodlar» bo'limidagi qidiruv, <c>SearchCandidatesQuery.IncludeRejected</c>).
/// </param>
/// <param name="CreatedFrom">
/// Ariza (yozuv yaratilgan) lahzasining quyi chegarasi, UTC — KIRADI.
/// Toshkent kunining boshi handler'da hisoblanadi (<c>ListDateRange</c>):
/// o'qish modelida mintaqa yo'q (<see cref="ContactDueOn"/> dagi bilan bir
/// xil sabab).
/// </param>
/// <param name="CreatedBefore">Ariza lahzasining yuqori chegarasi, UTC — KIRMAYDI.</param>
/// <param name="ClosedFrom">
/// Voronkadan chiqqan sananing (<c>closed_at</c>) quyi chegarasi — kiradi.
/// «Rad etilganlar» bo'limi shu sana bo'yicha filtrlanadi; qaysi juftlik
/// to'ldirilishini handler hal qiladi.
/// </param>
/// <param name="ClosedTo">Voronkadan chiqqan sananing yuqori chegarasi — kiradi.</param>
/// <param name="ReferralChannelId">«Qayerdan bilgan» kanali bo'yicha filtr (faolsizlantirilgani ham).</param>
/// <param name="ReferralMissing">
/// Faqat kanali KO'RSATILMAGAN nomzodlar (eski yozuvlar). <see cref="ReferralChannelId"/> bilan
/// birga berilsa ikkalasi ham qo'llanadi — natija bo'sh (ikki shart bir-birini istisno qiladi).
/// </param>
/// <param name="Reapplied">
/// Faqat mavjud kartasiga keyin onlayn anketa qo'shilganlar (<c>Candidate.ReappliedAt</c>) —
/// «avval botdan, keyin ofisda anketa» (egasi, 2026-10-30).
/// </param>
public sealed record CandidateSearchFilter(
    string? Search = null,
    Guid? VacancyId = null,
    Guid? StageId = null,
    CandidateStatus? Status = null,
    CandidateSource? Source = null,
    int Page = 1,
    int PageSize = 25,
    CandidateSortField Sort = CandidateSortField.CreatedAt,
    bool Descending = true,
    bool IncludeArchive = false,

    /*
      ⚠️ YANGI parametrlar FAQAT oxiriga qo'shiladi.
      `SearchCandidatesQueryHandler` bu rekordni POZITSIYA bo'yicha
      yasaydi (nom bilan emas), ya'ni o'rtaga qo'yilgan parametr butun
      argument qatorini jimgina bir pog'ona surib yuborardi: `search`
      `vacancyId` ga, `page` `pageSize` ga tushardi va kompilyator
      indamasdi (turlari mos kelib qolgan joyda).
    */
    DateOnly? ContactDueOn = null,
    bool RejectedSection = false,
    /*
      ⭐ Global qidiruv uchun (`GlobalSearchQueryHandler`). Sukut bo'yicha
      ro'yxat ishga olinganlar, qora ro'yxat, rezerv va rad etilganlarni
      yashiradi (har biri o'z bo'limida). Global qidiruv esa «bu odam bizda
      qayerda?» degan savolga javob beradi va hech kimni yashirmasligi
      kerak. Excel arxivi `IncludeArchive` bilan alohida boshqariladi.
    */
    bool AllStatuses = false,
    bool IncludeRejected = false,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedBefore = null,
    DateOnly? ClosedFrom = null,
    DateOnly? ClosedTo = null,
    Guid? ReferralChannelId = null,
    bool ReferralMissing = false,
    bool Reapplied = false)
{
    /// <summary>Nechta yozuv o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Excel arxivi ro'yxatdan chiqarilsinmi.
    /// </summary>
    /// <remarks>
    /// Manba ATAYLAB tanlangan bo'lsa chiqarilmaydi: HR «Excel arxivi»ni
    /// tanlagan ekan, aynan o'shani ko'rmoqchi. Aks holda tanlov jimgina
    /// bo'sh ro'yxat berardi.
    /// </remarks>
    public bool HideArchive => !IncludeArchive && Source is null;

    /// <summary>
    /// Ishga olinganlar ro'yxatdan chiqarilsinmi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Sukut bo'yicha CHIQARILADI</b> (foydalanuvchi talabi,
    /// 2026-09-07). Ishga olingan nomzod endi XODIM va uning o'rni
    /// «Xodimlar» bo'limida; nomzodlar ro'yxati esa «hozir kim ish
    /// ustida?» degan savolga javob beradi. «Ishga olingan» belgisi bilan
    /// turgan qatorlar shu savolni ko'mib yuborardi.
    /// </para>
    /// <para>
    /// <b>Yozuv O'CHIRILMAYDI</b> — u vakansiyaning «2/3 to'ldirildi»
    /// hisobiga (<c>VacancyDto.HiredCount</c>), bot samaradorligi
    /// hisobotiga (<c>Source</c>) va xodim kartasidan nomzodga
    /// qaytishga kerak. Faqat SUKUT ro'yxatdan chiqariladi.
    /// </para>
    /// <para>
    /// <b>Holat ataylab tanlangan bo'lsa chiqarilmaydi</b> — <c>HideArchive</c>
    /// bilan aynan bir xil qoida: HR «Ishga olingan» ni tanlagan ekan,
    /// aynan o'shani ko'rmoqchi va bo'sh ro'yxat olishi mumkin emas.
    /// </para>
    /// <para>
    /// Rezervdagilar uchun alohida qoida bor — <see cref="HideReserve"/>.
    /// </para>
    /// </remarks>
    public bool HideHired => Status is null && !AllStatuses;

    /// <summary>
    /// Rad etilganlar ro'yxatdan chiqarilsinmi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Sukut bo'yicha CHIQARILADI</b> (foydalanuvchi talabi,
    /// 2026-09-08: «отказ бўлганлар алоҳида рад бўлганлар бўлимига ўтиши
    /// керак»). <see cref="HideHired"/> bilan AYNAN bir xil mulohaza:
    /// nomzodlar ro'yxati «hozir kim ish ustida?» degan savolga javob
    /// beradi va rad etilganlar shu savolni ko'mib yuborardi. Ular
    /// yillar davomida to'planadi — ishga olinganlardan ham tezroq.
    /// </para>
    /// <para>
    /// <b>Yozuv O'CHIRILMAYDI</b> va bu ataylab: rad etilgan nomzod
    /// kelasi izlanishning bazasi (<c>DeleteCandidateCommand</c> izohi).
    /// Faqat SUKUT ro'yxatdan chiqariladi — menyudagi «Rad etilganlar»
    /// bandi <c>?rejected=true</c> bilan aynan shularni ochadi
    /// (<see cref="RejectedSection"/>).
    /// </para>
    /// <para>
    /// <b>Holat ataylab tanlangan bo'lsa chiqarilmaydi</b> —
    /// <see cref="HideHired"/> va <see cref="HideArchive"/> bilan bir xil
    /// qoida, aks holda o'sha band jimgina bo'sh ro'yxat berardi. Bo'lim
    /// bayrog'i ham qoidani ochadi va SABABI o'sha: bo'lim aynan shu
    /// qatorlarni ko'rsatish uchun ochiladi.
    /// </para>
    /// <para>
    /// ⭐ <b>«O'zi voz kechgan» ham SHU qoidaga tushadi</b> (foydalanuvchi
    /// qarori, 2026-09-10) — qoida <see cref="HideWithdrawn"/> da alohida
    /// turadi. Ilgari bu yerda «Withdrawn qoidaga tushmaydi, chunki kam
    /// uchraydi» deb yozilgan edi; amalda u kam uchramadi va kundalik
    /// ro'yxatni to'ldirib qo'ydi. Kundalik ro'yxat «hozir kim ish
    /// ustida?» degan savolga javob beradi, o'zi ketib qolgan odam esa bu
    /// savolga javob emas.
    /// </para>
    /// <para>
    /// ⚠️ Lekin <see cref="CandidateStatus.Rejected"/> va
    /// <see cref="CandidateStatus.Withdrawn"/> AYRIM holatlar bo'lib
    /// qoladi: alohida belgi (qizil va sariq), voronka hisobotida alohida
    /// sanoq — ular faqat RO'YXATDA KO'RINISHNI baham ko'radi. Ajratishning
    /// sababi <see cref="CandidateStatus.Withdrawn"/> izohida: «biz rad
    /// etdik» va «u ketib qoldi» butunlay boshqa ma'no beradi.
    /// </para>
    /// <para>
    /// REZERVDAGILAR bu qoidaga tushmaydi — ularning o'z qoidasi bor
    /// (<see cref="HideReserve"/>) va sabab boshqa: ular yo'qolmaydi,
    /// «Rezerv» bo'limiga ko'chadi.
    /// </para>
    /// <para>
    /// ⚠️ <b>Excel ARXIVIDA qoida ishlamaydi.</b> Kengash arxividagi 3110
    /// yozuvning deyarli hammasi aynan RAD ETILGAN
    /// (<c>CouncilStep.Close</c>: xodimga ulanmagani rad etiladi). Ikkala
    /// qoida birga qo'llansa, «Excel arxivi» ni tanlagan HR bo'm-bo'sh
    /// ro'yxat olardi — arxivning o'zi ko'rinmas bo'lib qolardi.
    /// </para>
    /// <para>
    /// ⚠️ Shart <see cref="HideArchive"/> ga bog'lanmagan va bu ATAYLAB:
    /// u har qanday MANBA tanlovida ochiladi, ya'ni «manba: Telegram bot»
    /// ni tanlagan HR ga rad etilganlar qaytib kelardi. Bu qoidaning
    /// <see cref="HideHired"/> bilan bo'lgan qarindoshligini buzardi —
    /// unda faqat HOLAT tanlovi qoidani ochadi.
    /// </para>
    /// </remarks>
    public bool HideRejected =>
        Status is null && !ArchiveRequested && !RejectedSection && !AllStatuses && !IncludeRejected;

    /// <summary>
    /// O'zi voz kechganlar ro'yxatdan chiqarilsinmi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="HideRejected"/> ning egizagi va u bilan AYNAN bir xil
    /// shartlarga bo'ysunadi — sabablari (nega yashiriladi, nega holat
    /// tanlovi qoidani ochadi, nega Excel arxivida ishlamaydi) o'sha
    /// izohda, bu yerda takrorlanmaydi.
    /// </para>
    /// <para>
    /// ⚠️ <c>!ArchiveRequested</c> bo'lagi MAJBURIY va aynan shu yerda
    /// unutish oson: kengash arxivi deyarli butunlay yopilgan yozuvlardan
    /// iborat, ya'ni arxivga yetib boradigan yashirish qoidasi «Excel
    /// arxivi» tanlovini qisqa yoki BO'SH qilib qo'yardi. Qoida endi ikkala
    /// holatni ham qamraydi.
    /// </para>
    /// <para>
    /// <see cref="HideHired"/> ga esa <c>!RejectedSection</c> bo'lagi
    /// ATAYLAB qo'shilmadi: bo'lim ichidagi
    /// <c>status IN (Rejected, Withdrawn)</c> sharti ishga olinganlarni
    /// allaqachon chiqarib tashlaydi va qo'shimcha shart hech qanday
    /// natijani o'zgartirmasdi — ya'ni ko'rinmas ta'sirli o'zgarish bo'lardi.
    /// </para>
    /// </remarks>
    public bool HideWithdrawn =>
        Status is null && !ArchiveRequested && !RejectedSection && !AllStatuses && !IncludeRejected;

    /// <summary>
    /// Rezervdagilar ro'yxatdan chiqarilsinmi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Sukut bo'yicha CHIQARILADI</b> (foydalanuvchi qarori,
    /// 2026-09-26: «резервга олинган номзодлар номзодлар бўлимида
    /// чиқмаслиги керак, фақат резерв бўлимида бўлиши керак»). Ilgari
    /// ular ataylab ko'rinib turardi — «kerak bo'lganda shu yerdan
    /// chaqiramiz». Endi o'sha ma'no «Rezerv» sahifasiga ko'chdi: u
    /// <c>?status=Reserve</c> bilan so'raydi va qoida holat tanlovida
    /// ochiladi (<see cref="HideHired"/> bilan bir xil qoida).
    /// </para>
    /// <para>
    /// ⚠️ <b>«Bugun aloqa kerak» filtri qoidani OCHADI.</b> Bu HR ning
    /// kunlik ish ro'yxati va rezerv uning bir qismi: bildirishnoma
    /// ham rezervdagilarni sanaydi (<c>NotificationReadModel</c>).
    /// Yashirilsa, bildirishnomadagi son ro'yxatdagidan ko'p chiqardi.
    /// </para>
    /// <para>
    /// Excel arxivida ishlamaydi — egizak qoidalar bilan bir xil sabab
    /// (<see cref="HideRejected"/> izohi). <see cref="RejectedSection"/>
    /// shartga ATAYLAB qo'shilmadi: bo'lim o'zi faqat rad etilgan va voz
    /// kechganlarni oladi (<see cref="HideWithdrawn"/> izohidagi mulohaza).
    /// </para>
    /// </remarks>
    public bool HideReserve => Status is null && !ArchiveRequested && ContactDueOn is null && !AllStatuses;

    /// <summary>
    /// Qora ro'yxatdagilar ro'yxatdan chiqarilsinmi.
    /// </summary>
    /// <remarks>
    /// ⭐ Holat tanlanmagan bo'lsa DOIM chiqariladi — arxiv tanlovida ham,
    /// «Rad etilganlar» bo'limida ham (2026-10-07). Ularning yagona o'rni —
    /// «Qora ro'yxat» menyusining «Nomzodlar» tabi, u
    /// <c>?status=Blacklisted</c> bilan so'raydi. Kundalik ro'yxat va rad
    /// etilganlar bo'limi «kim bilan ishlash mumkin» degan savolga javob
    /// beradi — qora ro'yxatdagi odam bu savolga kirmaydi.
    /// </remarks>
    public bool HideBlacklisted => Status is null && !AllStatuses;

    /// <summary>
    /// Excel arxivi ATAYLAB so'ralganmi.
    /// </summary>
    /// <remarks>
    /// <see cref="HideArchive"/> dan farqi: u istalgan manba tanlovida
    /// ochiladi, bu esa faqat arxivning O'ZI so'ralganda. Farqning sababi
    /// <see cref="HideRejected"/> izohida.
    /// </remarks>
    private bool ArchiveRequested => IncludeArchive || Source == CandidateSource.Excel;
}

/// <summary>
/// Takroriy ariza tekshiruvining «odam» kaliti: familiya, ism va tug'ilgan sana.
/// </summary>
/// <remarks>
/// <para>
/// Ism lotin/kirill farqsiz solishtiriladi (bazadagi <c>search_key()</c>):
/// «Valiyev Ali» va «Валиев Али» bitta odam. Familiya va ism o'rni
/// almashgani ham tanildi — botda odam ko'pincha avval ismini yozadi.
/// </para>
/// <para>
/// ⚠️ Otasining ismi ATAYLAB solishtirilmaydi: prod'dagi takror
/// juftliklarning yarmida u bir arizada bor, ikkinchisida yo'q yoki
/// «o'g'li» bilan yozilgan. Tug'ilgan sana bilan birga F.I. ikki begona
/// odamni adashtirmaydigan darajada tor.
/// </para>
/// </remarks>
/// <param name="LastName">Familiya — qanday yozilgan bo'lsa.</param>
/// <param name="FirstName">Ism.</param>
/// <param name="BirthDate">Tug'ilgan sana.</param>
public sealed record CandidatePersonKey(string LastName, string FirstName, DateOnly BirthDate);

/// <summary>
/// Takroriy ariza tekshiruvi uchun kerakli MINIMUM.
/// </summary>
/// <remarks>
/// Maydonlar oz: javob botga ketadi va u yerda faqat «qachon topshirgan,
/// qaysi vakansiyaga, qaysi bosqichda» kerak. Telefon, manzil va ichki
/// izohlar bu qatorga TUSHMAYDI — <c>RejectionNoticeDto</c> dagi bilan
/// aynan bir xil qoida.
/// </remarks>
/// <param name="Status">Jarayondami yoki yopilganmi.</param>
/// <param name="ClosedAt">
/// Voronkadan chiqqan sana — muddat SHUNDAN hisoblanadi. Ariza
/// topshirilgan sanadan emas: HR qarorni bir oy keyin yozishi mumkin
/// va o'shanda hisob qaror kunidan boshlanishi kerak.
/// </param>
/// <param name="AppliedAt">Ariza topshirilgan lahza — nomzodga ko'rsatiladi.</param>
/// <param name="VacancyTitle">Qaysi vakansiyaga.</param>
/// <param name="StageName">Qaysi bosqichda to'xtagan.</param>
public sealed record CandidateApplicationRow(
    CandidateStatus Status,
    DateOnly? ClosedAt,
    DateTimeOffset AppliedAt,
    string? VacancyTitle,
    string? StageName);

/// <summary>Bitta sahifa: nomzodlar va umumiy soni.</summary>
public sealed record CandidatePage(IReadOnlyList<CandidateRow> Items, int Total);

/// <summary>
/// Nomzodlar ro'yxatining bitta xom qatori.
/// </summary>
/// <remarks>
/// Qiymat obyektlari (F.I.Sh., telefon) XOM holida qaytadi: o'qish modeli
/// ko'rsatish modelini bilmasligi kerak, ularni DTO'ga yig'ish Application
/// qatlamining ishi (<c>IncentiveListRow</c> dagi bilan bir xil qoida).
/// </remarks>
/// <param name="Id">Identifikator.</param>
/// <param name="LastName">Familiya.</param>
/// <param name="FirstName">Ism.</param>
/// <param name="MiddleName">Otasining ismi.</param>
/// <param name="Phone">Telefon (kanonik satr).</param>
/// <param name="BirthDate">Tug'ilgan sana.</param>
/// <param name="Speciality">Mutaxassisligi.</param>
/// <param name="VacancyId">Vakansiya.</param>
/// <param name="VacancyName">Vakansiyaning nomi (e'lon nomi).</param>
/// <param name="CurrentStageId">Joriy bosqich.</param>
/// <param name="CurrentStageName">Joriy bosqich nomi.</param>
/// <param name="Status">Voronkadagi holati.</param>
/// <param name="Source">Qayerdan kelgan.</param>
/// <param name="HasResume">
/// Kamida bitta fayl biriktirilganmi. Nomi ATAYLAB o'zgarmadi — sababi
/// <see cref="CandidateListItemDto.HasResume"/> izohida.
/// </param>
/// <param name="HasPhoto">Surati bormi — ro'yxat qatorida rasm ko'rsatiladi.</param>
/// <param name="CreatedAt">Kiritilgan vaqt.</param>
/// <param name="HiredEmployeeId">Ishga olingan bo'lsa — xodim kartasi.</param>
/// <param name="NextContactDate">
/// Keyingi aloqa sanasi. <c>null</c> — reja yo'q.
///
/// ⚠️ Yangi maydon OXIRIGA qo'shildi: qator <c>RecruitingReadModel</c> da
/// POZITSIYA bo'yicha yasaladi va o'rtaga qo'yilgan parametr butun
/// argument qatorini surib yuborardi.
/// </param>
/// <param name="ClosedAt">Voronkadan chiqqan sana.</param>
/// <param name="ClosingNote">Chiqish sababi (qora ro'yxat tabi uchun).</param>
/// <param name="ReferralChannelId">«Qayerdan bilgan» kanali.</param>
/// <param name="ReferralChannelName">Kanal nomi.</param>
/// <param name="ReferralDetail">Aniqlik.</param>
/// <param name="ReappliedAt">Kartaga keyin onlayn anketa qo'shilgan lahza (<c>Candidate.ReappliedAt</c>).</param>
public sealed record CandidateRow(
    Guid Id,
    string LastName,
    string FirstName,
    string? MiddleName,
    string? Phone,
    DateOnly? BirthDate,
    string? Speciality,
    Guid VacancyId,
    string? VacancyName,
    Guid? CurrentStageId,
    string? CurrentStageName,
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
/// Vakansiyalar ro'yxatining filtri.
/// </summary>
/// <param name="Status">Holat bo'yicha filtr. Bo'sh — hammasi.</param>
/// <param name="Search">
/// Nom bo'yicha qidiruv (qismiy, registrga va «қ/к» farqiga sezgir
/// emas). Bo'sh — hammasi.
///
/// Lavozim, bo'lim va firma filtrlari OLIB TASHLANDI: vakansiyada
/// bunday maydonlar endi umuman yo'q (sababi <c>Vacancy</c> izohida).
/// Ular «kim ishga olindi» degan savolga tegishli va o'sha savolga
/// xodimlar ro'yxati javob beradi.
/// </param>
/// <param name="Page">Sahifa raqami, 1 dan.</param>
/// <param name="PageSize">Sahifadagi yozuvlar soni.</param>
/// <param name="Sort">Saralash ustuni.</param>
/// <param name="Descending">Teskari tartib.</param>
/// <param name="ExcludedVacancyId">
/// Ro'yxatdan CHIQARIB tashlanadigan bitta vakansiya. <c>null</c> —
/// hech kim chiqarilmaydi.
///
/// Nega umumiy «id emas» filtri, «umumiy anketani yashir» emas:
/// o'qish modeli qaysi vakansiya nima uchun xizmat qilishini
/// BILMASLIGI kerak — bu use-case qatlamining bilimi. Bu yerda faqat
/// «shu qatorni qo'shma» degan mexanik shart qoladi.
/// </param>
public sealed record VacancyListFilter(
    VacancyStatus? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 25,
    VacancySortField Sort = VacancySortField.OpenedAt,
    bool Descending = true,
    Guid? ExcludedVacancyId = null)
{
    /// <summary>Nechta yozuv o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>Bitta sahifa: vakansiyalar va umumiy soni.</summary>
public sealed record VacancyPage(IReadOnlyList<VacancyRow> Items, int Total);

/// <summary>Vakansiyalar ro'yxatining bitta xom qatori.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="Title">Vakansiya nomi.</param>
/// <param name="Openings">Nechta odam kerak.</param>
/// <param name="Status">Holati.</param>
/// <param name="OpenedAt">Ochilgan sana.</param>
/// <param name="ClosedAt">Yopilgan sana.</param>
/// <param name="Note">Izoh.</param>
/// <param name="CandidateCount">Nomzodlar soni.</param>
/// <param name="HiredCount">Ishga olinganlar soni.</param>
public sealed record VacancyRow(
    Guid Id,
    string Title,
    int Openings,
    VacancyStatus Status,
    DateOnly OpenedAt,
    DateOnly? ClosedAt,
    string? Note,
    int CandidateCount,
    int HiredCount);
