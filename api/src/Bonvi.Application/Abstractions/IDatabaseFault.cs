namespace Bonvi.Application.Abstractions;

/// <summary>
/// Bazadan kelgan nosozlikni <b>ma'noli</b> holatga o'giradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida port.</b> Ba'zi qoidalar faqat bazada ushlanadi — masalan
/// nomning unikalligi. Odatda uni use-case qatlami oldindan tekshiradi, lekin
/// tekshirish bilan yozish orasida qisqa oyna qoladi: ikki foydalanuvchi bir
/// vaqtda bir xil nom yuborsa, ikkalasi ham «bo'sh» degan javob oladi va
/// ikkinchisi bazadagi unikal indeksga uriladi.
/// </para>
/// <para>
/// Bunday istisnoni tanish uchun drayverning xato kodini bilish kerak
/// (PostgreSQL'da <c>23505</c>). Bu bilim Infrastructure qatlamiga tegishli:
/// Api uni bilsa, ilova ma'lumotlar bazasi drayveriga bog'lanib qolardi va
/// arxitektura testi buni ataylab taqiqlaydi.
/// </para>
/// <para>
/// Shu sababli tanib olish shu port ortida qoladi, natija esa oddiy
/// mantiqiy qiymat — uni istalgan qatlam tushunadi.
/// </para>
/// </remarks>
public interface IDatabaseFault
{
    /// <summary>
    /// Istisno <b>nom takrorlangani</b> sababli yuzaga kelganmi.
    /// </summary>
    /// <remarks>
    /// Har qanday unikallik buzilishi emas: JSHSHIR yoki telefon
    /// takrorlangani butunlay boshqa xabar talab qiladi va uni «bunday nom
    /// bor» deb ko'rsatish foydalanuvchini adashtirardi.
    /// </remarks>
    bool IsDuplicateName(Exception exception);
}
