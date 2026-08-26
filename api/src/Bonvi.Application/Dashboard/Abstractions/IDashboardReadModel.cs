namespace Bonvi.Application.Dashboard.Abstractions;

/// <summary>
/// Bosh sahifa raqamlari uchun <b>o'qish</b> porti.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida port, <c>IEmployeeReadModel</c> ga qo'shilmadi.</b>
/// U port ro'yxat ekranlariga xizmat qiladi: sahifa, saralash, filtr,
/// qatorlar. Bu yerda esa birorta qator ham qaytmaydi — faqat sonlar,
/// va ular xodimlardan tashqari ma'lumotnomalarni ham qamraydi
/// (bo'lim, lavozim, yo'nalish, yuridik shaxs). Ikkisi bitta interfeysda
/// tursa, xodimlar porti ma'lumotnomalarni ham bilib qolardi.
/// </para>
/// <para>
/// <b>Nega barcha sanoq bazada.</b> Bosh sahifa har kirishda ochiladi va
/// davr almashtirilganda qayta so'raladi. Yozuvlarni yuklab, keyin C# da
/// sanash 2000+ munosabatni har safar tarmoqdan olib o'tish demak —
/// natija esa atigi o'nta son. Shu sababli bu portning har bir soni
/// <c>COUNT</c> bo'lib bazada bajariladi.
/// </para>
/// <para>
/// <b>Nega davr shu yerga tayyor sana bo'lib keladi.</b> «1 hafta» yoki
/// «1 oy» ning qayerdan boshlanishi — biznes qarori va u
/// <c>GetDashboardStatsQueryHandler</c> da, <see cref="Domain.Abstractions.IClock"/>
/// yordamida hal qilinadi. O'qish modeli faqat berilgan oraliqni sanaydi,
/// «bugun» qachonligini o'zi bilmaydi — shuning uchun uni test qilish
/// uchun soatni siljitish shart emas.
/// </para>
/// <para>
/// Amalga oshirilishi Infrastructure qatlamida (proyeksiya so'rovi).
/// </para>
/// </remarks>
public interface IDashboardReadModel
{
    /// <summary>Bosh sahifaning barcha sonlarini bitta chaqiruvda qaytaradi.</summary>
    Task<DashboardCounts> GetCountsAsync(
        DashboardCountsRange range,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Faol xodimlarning bo'lim va yo'nalish bo'yicha taqsimoti — bitta
    /// chaqiruvda ikkalasi ham.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>«Faol» — <see cref="DashboardCounts.ActiveEmployees"/> bilan AYNAN
    /// bir xil ta'rif</b> (mehnat munosabati <c>Status == Active</c>, ya'ni
    /// doimiy ishlayotgan). Sinovdagilar va arxiv bu yerga kirmaydi. Sabab:
    /// bosh sahifada allaqachon shu nom bilan tanish son bor va ikkinchi
    /// «faol» ta'rifini kiritish (masalan sinovni ham qo'shib) ekranda ikki
    /// xil «faol» so'zini uchratib, HR ni chalkashtirardi. Bu tanlov
    /// natijasida taqsimot bo'yicha yig'indi <c>ActiveEmployees</c> soniga
    /// AYNAN teng bo'ladi — ikkala raqam bir-birini tekshiradi.
    /// </para>
    /// </remarks>
    Task<DashboardBreakdown> GetBreakdownAsync(CancellationToken cancellationToken = default);
}

/// <summary>Bo'lim va yo'nalish bo'yicha taqsimot — ikkalasi bitta javobda.</summary>
/// <param name="Departments">
/// YUQORI DARAJA bo'yicha (bo'lim daraxtining ildizlari): 73 ta barg bo'lim
/// emas, taxminan 10 ta departament. Sabab — <see cref="DashboardBreakdownItem"/>
/// izohida.
/// </param>
/// <param name="Directions">Yo'nalish bo'yicha — daraxt emas, tekis ro'yxat.</param>
public sealed record DashboardBreakdown(
    IReadOnlyList<DashboardBreakdownItem> Departments,
    IReadOnlyList<DashboardBreakdownItem> Directions);

/// <summary>Taqsimotdagi bitta bo'lak: nom va shu bo'lakka tushgan xodimlar soni.</summary>
/// <remarks>
/// <para>
/// <b>Bo'lim nega BARG emas, ILDIZ bo'yicha yig'iladi.</b> Bo'lim ma'lumotnomasi
/// daraxt (<c>Department.ParentId</c>) va bargda ~73 ta yozuv bor —
/// «7-Департамент / Метан завод / Плазмада кесиш бўлими» kabi uch darajali
/// zanjirning eng pastki uchi. Shunча ko'p bo'lakli doira/ustun diagrammasi
/// o'qib bo'lmas darajada mayda kesimlarga bo'linib ketardi va HR uchun
/// «qaysi biznes qancha odam» degan asosiy savolga javob bermasdi. Har bir
/// bo'limning ILDIZ ajdodi esa aynan tashkiliy sxemadagi yirik bo'linmaga
/// mos keladi (departament), ular atigi bir necha o'nlab — diagramma uchun
/// mos hajm. Xodim to'g'ridan-to'g'ri ildiz darajasiga biriktirilgan bo'lsa,
/// u o'zining nomi bilan qoladi (ildiz — o'ziga ildiz).
/// </para>
/// <para>
/// <b>«Belgilanmagan» bo'lagi.</b> <see cref="Id"/> <c>null</c> bo'lsa —
/// bo'limi (yoki yo'nalishi) umuman ko'rsatilmagan faol xodimlar. Bu bo'lak
/// jimgina tashlab yuborilmaydi: yig'indi <c>ActiveEmployees</c> soniga teng
/// bo'lishi kerak, aks holda «necha kishi qayerda ishlaydi» degan savolga
/// noto'liq javob berilardi.
/// </para>
/// <para>
/// <b>Yo'nalish uchun yig'indi ActiveEmployees dan OSHISHI mumkin.</b>
/// Yo'nalish — ko'p-ga-ko'p bog'lanish (bitta xodim bir nechta yo'nalishga
/// tegishli bo'lishi mumkin), bo'lim esa bitta. Bu ataylab: yo'nalish
/// kesimi «necha kishi qayerda ishlaydi» emas, «necha kishi qaysi biznesga
/// aloqador» degan boshqa savolga javob beradi.
/// </para>
/// </remarks>
/// <param name="Id">Bo'lim/yo'nalishning identifikatori. <c>null</c> — «Belgilanmagan».</param>
/// <param name="Name">Ko'rsatiladigan nom.</param>
/// <param name="Count">Shu bo'lakka tushgan faol xodimlar soni.</param>
public sealed record DashboardBreakdownItem(Guid? Id, string Name, int Count);

/// <summary>
/// Sanoq oralig'i — bosh sahifadagi davr almashtirgichining natijasi.
/// </summary>
/// <remarks>
/// Oraliqlar ikki tomondan YOPIQ: <c>From</c> va <c>To</c> kunlarining
/// o'zi ham kiradi. Sabab: sanalar <c>DateOnly</c>, ya'ni vaqt qismi yo'q
/// va «kunning oxirigacha» degan chekka holat umuman tug'ilmaydi.
/// </remarks>
/// <param name="Today">Bugungi sana (Toshkent) — tug'ilgan kunlar shu bo'yicha.</param>
/// <param name="From">Tanlangan davrning boshi.</param>
/// <param name="To">Tanlangan davrning oxiri (odatda bugun).</param>
/// <param name="PreviousFrom">Oldingi — shu uzunlikdagi — davrning boshi.</param>
/// <param name="PreviousTo">Oldingi davrning oxiri (<c>From</c> dan bir kun oldin).</param>
public sealed record DashboardCountsRange(
    DateOnly Today,
    DateOnly From,
    DateOnly To,
    DateOnly PreviousFrom,
    DateOnly PreviousTo);

/// <summary>
/// Bosh sahifadagi barcha sonlar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sonlar ODAM bo'yicha, shartnoma bo'yicha emas.</b> Bitta xodim ikki
/// firmada ishlashi mumkin (o'rindoshlik) va u ikki marta sanalmasligi
/// kerak: HR «540 xodim» deb hisoblaydi, «610 shartnoma» deb emas.
/// </para>
/// <para>
/// Foizlar bu yerda YO'Q: qaysi son qanday ko'rsatilishi (o'sish belgisi,
/// rang, yashirish) — ko'rinish qarori. Bu yozuvda faqat faktlar turadi.
/// </para>
/// </remarks>
/// <param name="ActiveEmployees">Kamida bitta <b>doimiy</b> ish joyi bor xodimlar.</param>
/// <param name="OnProbation">Kamida bitta munosabati sinov muddatida bo'lgan xodimlar.</param>
/// <param name="FormerEmployees">
/// Endi hech qayerda ishlamaydigan xodimlar — arxiv.
/// Bitta firmadan ketib, ikkinchisida qolgan odam bu yerga TUSHMAYDI.
/// </param>
/// <param name="Hired">Tanlangan davrda ishga qabul qilingan xodimlar.</param>
/// <param name="Left">Tanlangan davrda ishdan bo'shatilgan xodimlar.</param>
/// <param name="HiredPrevious">Oldingi shunday davrda ishga qabul qilinganlar.</param>
/// <param name="LeftPrevious">Oldingi shunday davrda ishdan bo'shatilganlar.</param>
/// <param name="BirthdaysToday">Bugun tug'ilgan kuni bo'lgan ishlayotgan xodimlar.</param>
/// <param name="Departments">Ma'lumotnomadagi faol bo'limlar.</param>
/// <param name="Positions">Ma'lumotnomadagi faol lavozimlar.</param>
/// <param name="Directions">Ma'lumotnomadagi faol yo'nalishlar.</param>
/// <param name="LegalEntities">Ma'lumotnomadagi faol yuridik shaxslar.</param>
public sealed record DashboardCounts(
    int ActiveEmployees,
    int OnProbation,
    int FormerEmployees,
    int Hired,
    int Left,
    int HiredPrevious,
    int LeftPrevious,
    int BirthdaysToday,
    int Departments,
    int Positions,
    int Directions,
    int LegalEntities);
