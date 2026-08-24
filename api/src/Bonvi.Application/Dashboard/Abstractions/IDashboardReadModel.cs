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
}

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
