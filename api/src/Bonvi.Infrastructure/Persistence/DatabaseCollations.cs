namespace Bonvi.Infrastructure.Persistence;

/// <summary>
/// Matn ustunlari uchun saralash qoidalari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu kerak.</b> PostgreSQL bazasi <c>en_US.utf8</c> kolatsiyasida
/// yaratilgan va u kirill harflarini <b>bayt tartibida</b> solishtiradi.
/// Natijada familiya bo'yicha saralangan xodimlar ro'yxati shunday chiqadi:
/// </para>
/// <code>
/// Ўринов &lt; Абдуллаев &lt; Юсупов &lt; Ғаниев &lt; Қосимов &lt; Ҳакимов
/// </code>
/// <para>
/// «Ў» — «А» dan oldin, «Ғ», «Қ», «Ҳ» esa alifboning oxirida. Bu alifbo
/// tartibi emas, va HR ning asosiy ekrani — xodimlar ro'yxati — shu tartibda
/// ishlatib bo'lmaydigan holga keladi.
/// </para>
/// <para>
/// ICU kolatsiyasi bilan tartib to'g'ri bo'ladi:
/// </para>
/// <code>
/// Абдуллаев &lt; Ғаниев &lt; Қосимов &lt; Ўринов &lt; Ҳакимов &lt; Юсупов
/// </code>
/// <para>
/// <b>Nega ustun darajasida, baza darajasida emas.</b> PostgreSQL mavjud
/// bazaning kolatsiyasini o'zgartirishga ruxsat bermaydi — buning uchun bazani
/// qayta yaratish kerak bo'lardi. Ustun darajasidagi kolatsiya esa migratsiya
/// bilan qo'llanadi, indekslar avtomatik qayta quriladi va qoida <b>kodda</b>
/// e'lon qilinadi: yangi muhit ko'tarilganda u o'z-o'zidan to'g'ri bo'ladi.
/// </para>
/// <para>
/// <b>Nega faqat ism va ma'lumotnoma nomlari.</b> Saralash aynan shu
/// ustunlarda ma'noga ega. JSHSHIR, pasport va telefon — raqam va lotin
/// harflari, ularda ICU hech narsa o'zgartirmaydi; manzil va izohlar esa
/// hech qachon saralanmaydi. Kolatsiyani keraksiz joyga tarqatish
/// migratsiyani og'irlashtiradi va foyda bermaydi.
/// </para>
/// </remarks>
internal static class DatabaseCollations
{
    /// <summary>
    /// O'zbek (kirill) alifbo tartibi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PostgreSQL bilan birga keladigan ICU kolatsiyasi — alohida yaratish
    /// kerak emas. Mintaqasiz shakl (<c>uz-Cyrl-UZ-x-icu</c> emas) ataylab:
    /// mintaqa o'zbek uchun saralash qoidasini o'zgartirmaydi.
    /// </para>
    /// <para>
    /// Kolatsiya <b>deterministik</b>, ya'ni tenglik semantikasi o'zgarmaydi:
    /// unikal indekslar va <c>ILIKE</c> avvalgidek ishlaydi.
    /// </para>
    /// <para>
    /// <b>Bir xulqni bilib turish kerak.</b> ICU o'zbek alifbosining
    /// qo'shimcha harflaridan uchtasini alohida harf deb hisoblaydi, «Ў» ni
    /// esa «У» ning varianti deb:
    /// </para>
    /// <code>
    /// Ганиев &lt; Ғаниев      ← «Ғ» alohida, «Г» dan keyin
    /// Кудратов &lt; Қосимов   ← «Қ» alohida, «К» dan keyin
    /// Хакимов &lt; Ҳакимов    ← «Ҳ» alohida, «Х» dan keyin
    /// Ўринов &lt; Усманов     ← «Ў» va «У» bir darajada; keyingi harf hal qiladi
    /// </code>
    /// <para>
    /// Lug'at tartibida «Ў» «У» dan keyin alohida turishi kerak edi, va buni
    /// o'z qoidalari bilan kolatsiya yaratib tuzatsa bo'lardi. <b>Ataylab
    /// tuzatilmadi.</b> Sabab bloknotda: bir familiya ikki xil yozilgan
    /// («Тошпулатов» va «Тўшпулатов»), va ETL ularni ataylab birlashtirmaydi —
    /// ular turli odam bo'lishi mumkin. Joriy xulqda shu ikki yozuv ro'yxatda
    /// <b>yonma-yon</b> tushadi va HR ikkalasini birga ko'radi; «Ў» ni alohida
    /// harf qilsak, ular ro'yxatning ikki uchiga tarqab ketardi.
    /// </para>
    /// </remarks>
    public const string UzbekNames = "uz-Cyrl-x-icu";
}
