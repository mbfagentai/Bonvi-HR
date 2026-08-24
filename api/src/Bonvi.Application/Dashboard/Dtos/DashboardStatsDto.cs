namespace Bonvi.Application.Dashboard.Dtos;

/// <summary>
/// Bosh sahifadagi davr almashtirgichining qiymatlari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega sanoq (enum), oddiy son emas.</b> Mijozdan kelgan matn
/// to'g'ridan-to'g'ri sana arifmetikasiga tushmaydi: noma'lum qiymat
/// 400 bo'lib qaytadi va «nol kunlik davr» kabi jimgina xato bo'lmaydi.
/// </para>
/// <para>
/// Qiymatlar ekrandagi to'rtta tugmaga AYNAN mos: 1 kun, 1 hafta,
/// 15 kun, 1 oy. Bular kalendar oralig'i emas, <b>siljuvchi oyna</b>:
/// «1 oy» — oy boshidan emas, bugundan orqaga bir oy. Sabab: HR
/// «oxirgi bir oyda nechta odam keldi» deb so'raydi, «avgustda» deb emas —
/// oyning 1-sanasida kalendar hisobi doim nolga tushib qolardi.
/// </para>
/// </remarks>
public enum DashboardPeriod
{
    /// <summary>Bugun.</summary>
    Day = 1,

    /// <summary>Oxirgi 7 kun (bugun ham kiradi).</summary>
    Week = 2,

    /// <summary>Oxirgi 15 kun (bugun ham kiradi).</summary>
    Days15 = 3,

    /// <summary>Oxirgi bir oy (bugun ham kiradi).</summary>
    Month = 4,
}

/// <summary>
/// Bosh sahifaning ko'rsatkichlari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega davr chegaralari javobda ham bor.</b> Mijoz «1 oy» deb so'raydi,
/// lekin oyning qayerdan boshlanishini server hal qiladi. Chegara qaytmasa,
/// frontend uni QAYTA hisoblashga majbur bo'lardi va ikkita hisob
/// ertami-kechmi ajralib ketardi. Endi ekranda «25.07 – 24.08» yozuvi
/// aynan sanalgan oraliqni ko'rsatadi.
/// </para>
/// <para>
/// <b>Nega o'sish foizi YO'Q.</b> Bu yerda faqat sanalgan faktlar turadi:
/// joriy davr va oldingi davr sonlari. Foiz — ko'rinish qarori (0 ga
/// bo'linish, yaxlitlash, umuman ko'rsatish-ko'rsatmaslik) va u shu
/// ikki sondan ekranda hosil qilinadi.
/// </para>
/// </remarks>
/// <param name="Period">Qaysi davr so'ralgani — so'rov qaytib tasdiqlanadi.</param>
/// <param name="From">Davrning boshi (shu kun ham kiradi).</param>
/// <param name="To">Davrning oxiri (shu kun ham kiradi).</param>
/// <param name="PreviousFrom">Oldingi shunday davrning boshi.</param>
/// <param name="PreviousTo">Oldingi shunday davrning oxiri.</param>
/// <param name="ActiveEmployees">Hozir ishlayotgan (doimiy) xodimlar soni.</param>
/// <param name="OnProbation">Sinov muddatidagi xodimlar soni.</param>
/// <param name="FormerEmployees">Endi hech qayerda ishlamaydigan xodimlar (arxiv).</param>
/// <param name="Hired">Tanlangan davrda ishga qabul qilinganlar.</param>
/// <param name="Left">Tanlangan davrda ishdan ketganlar.</param>
/// <param name="HiredPrevious">Oldingi shunday davrda ishga qabul qilinganlar.</param>
/// <param name="LeftPrevious">Oldingi shunday davrda ishdan ketganlar.</param>
/// <param name="BirthdaysToday">Bugun tug'ilgan kuni bo'lgan ishlayotgan xodimlar.</param>
/// <param name="Catalog">Ma'lumotnomalardagi faol yozuvlar soni.</param>
public sealed record DashboardStatsDto(
    DashboardPeriod Period,
    DateOnly From,
    DateOnly To,
    DateOnly PreviousFrom,
    DateOnly PreviousTo,
    int ActiveEmployees,
    int OnProbation,
    int FormerEmployees,
    int Hired,
    int Left,
    int HiredPrevious,
    int LeftPrevious,
    int BirthdaysToday,
    DashboardCatalogSizesDto Catalog);

/// <summary>
/// Ma'lumotnomalardagi <b>faol</b> yozuvlar soni.
/// </summary>
/// <remarks>
/// Faolsizlantirilgan yozuv sanoqqa kirmaydi: u yangi buyruqda tanlanmaydi
/// va HR uchun «bugun nechta bo'lim bor» degan savolga javob bermaydi.
/// Eski buyruqlarda unga havola qolgani uchun yozuvning o'zi o'chirilmaydi
/// (<c>ReferenceEntity.Deactivate</c>), lekin sanoqda ko'rinmasligi kerak.
/// </remarks>
/// <param name="Departments">Faol bo'limlar.</param>
/// <param name="Positions">Faol lavozimlar.</param>
/// <param name="Directions">Faol yo'nalishlar.</param>
/// <param name="LegalEntities">Faol yuridik shaxslar.</param>
public sealed record DashboardCatalogSizesDto(
    int Departments,
    int Positions,
    int Directions,
    int LegalEntities);
