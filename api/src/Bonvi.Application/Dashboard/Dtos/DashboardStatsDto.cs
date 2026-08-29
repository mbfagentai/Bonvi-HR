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
/// <param name="Recruiting">
/// Rekruting sonlari — <c>recruiting.view</c> ruxsati bo'lmasa <c>null</c>.
/// Sababi <see cref="DashboardRecruitingDto"/> izohida.
/// </param>
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
    DashboardCatalogSizesDto Catalog,
    DashboardRecruitingDto? Recruiting);

/// <summary>
/// Bosh sahifadagi rekruting kartalari: ochiq vakansiyalar va arizalar.
/// </summary>
/// <remarks>
/// <para>
/// <b>⚠️ Nega bu blok RUXSATGA bog'langan.</b> Bosh sahifa
/// <c>employees.view</c> bilan ochiladi (sababi <c>DashboardEndpoints</c>
/// izohida: bu birinchi ekran, uni ikkita ruxsatga bog'lash sahifani
/// sababsiz sindirardi). Rekruting ma'lumoti esa <c>recruiting.view</c>
/// talab qiladi va bu ikki ruxsat bir odamda birga bo'lishi SHART emas.
/// Shu sababli endpoint ruxsatini kuchaytirish o'rniga JAVOB torayadi:
/// rekruting soni faqat uni ko'rishga haqli odamga qaytadi.
/// </para>
/// <para>
/// <b>Bu loyihadagi mavjud naqsh, yangi ixtiro emas.</b> Oklad ham AYNAN
/// shunday maskalanadi (<c>SalaryAccess.Mask</c>: <c>salary.view</c>
/// yo'q — maydon <c>null</c>), bildirishnomalar ham ruxsatga qarab
/// filtrlanadi (<c>NotificationCollector</c>). Uchalasining qoidasi bir
/// xil: bitta ekran har xil huquqli odamga har xil TO'LIQLIKDA ochiladi,
/// lekin hech kimga xato ko'rsatmaydi.
/// </para>
/// <para>
/// <b>Nega butun blok <c>null</c>, to'rtta alohida <c>int?</c> emas.</b>
/// Ruxsat to'rtta songa BIRDAN tegishli — ular birga keladi yoki umuman
/// kelmaydi. To'rtta nullable maydonda mijoz to'rtta tekshiruv yozishi
/// kerak bo'lardi va bittasini unutish «— o'rin kerak» kabi yarim
/// chizilgan kartani berardi. Bir blok — bitta tekshiruv, ikkita karta.
/// </para>
/// <para>
/// <b>Nega nol emas, <c>null</c>.</b> Nol — haqiqiy javob («ochiq
/// vakansiya yo'q»), ruxsatsizlik esa javobning YO'QLIGI. Ekran ikkalasini
/// farqlashi shart: birinchisida «0» ko'rsatiladi, ikkinchisida karta
/// umuman chizilmaydi (bu ham <c>SalaryAccess</c> dagi qoida).
/// </para>
/// </remarks>
/// <param name="OpenVacancies">Ochiq (nomzod qabul qiladigan) vakansiyalar soni.</param>
/// <param name="Openings">
/// Shu vakansiyalarda jami nechta O'RIN kerakligi. Vakansiya soni bilan bir
/// xil emas: bitta e'londa bir nechta o'rin bo'lishi mumkin (batafsil sabab
/// <see cref="Abstractions.DashboardRecruitingCounts"/> izohida).
/// </param>
/// <param name="ActiveCandidates">Voronkadagi faol arizalar — joriy holat.</param>
/// <param name="NewCandidates">Tanlangan davrda kelgan yangi arizalar.</param>
public sealed record DashboardRecruitingDto(
    int OpenVacancies,
    int Openings,
    int ActiveCandidates,
    int NewCandidates);

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

/// <summary>
/// Bo'lim va yo'nalish bo'yicha taqsimot — bosh sahifadagi diagramma uchun.
/// </summary>
/// <remarks>
/// Nega <c>DashboardStatsDto</c> ga qo'shilmagan, alohida endpoint: bu
/// ma'lumot davrga bog'liq emas (har doim «hozir»ni ko'rsatadi, «period»
/// bilan o'zgarmaydi) va uni har «period» almashtirilganda qayta so'rash
/// ma'nosiz — alohida so'ralsa, sahifa faqat bir marta yuklaydi.
/// </remarks>
/// <param name="Departments">
/// Yuqori daraja (departament) bo'yicha, ko'pdan kamga tartiblangan.
/// </param>
/// <param name="Directions">Yo'nalish bo'yicha, ko'pdan kamga tartiblangan.</param>
public sealed record DashboardBreakdownDto(
    IReadOnlyList<DashboardBreakdownItemDto> Departments,
    IReadOnlyList<DashboardBreakdownItemDto> Directions);

/// <summary>Taqsimotdagi bitta bo'lak.</summary>
/// <param name="Id">Identifikator. <c>null</c> — «Belgilanmagan» (biriktirilmagan).</param>
/// <param name="Name">Ko'rsatiladigan nom.</param>
/// <param name="Count">Shu bo'lakka tushgan faol xodimlar soni.</param>
public sealed record DashboardBreakdownItemDto(Guid? Id, string Name, int Count);
