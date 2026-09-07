namespace Bonvi.Application.Abstractions;

/// <summary>
/// «Bazada nimadir o'zgardi» degan jarayon ichidagi signal.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nima uchun kerak.</b> Bildirishnomalar HODISA emas, HOLAT: ular
/// hech qayerda saqlanmaydi, so'rov kelganda joriy ma'lumotdan
/// hisoblanadi (<c>NotificationCollector</c>). Shu sababli «yangi
/// bildirishnoma keldi» degan voqeani kuzatib bo'lmaydi — kuzatish
/// mumkin bo'lgan yagona narsa MA'LUMOTNING o'zgarishi.
/// </para>
/// <para>
/// Bu signal aynan shuni beradi: har bir muvaffaqiyatli <c>SaveChanges</c>
/// versiyani bir pog'ona oshiradi
/// (<c>DataChangeInterceptor</c>), <c>/api/notifications/stream</c> esa
/// versiya o'zgarishini kutib turadi va o'zgargach ro'yxatni QAYTA
/// HISOBLAB, ochiq turgan brauzerlarga yuboradi.
/// </para>
/// <para>
/// <b>Nega davriy qayta hisoblash emas.</b> Bitta hisob to'rtta so'rov
/// (sinov muddatlari, yozuvsiz munosabatlar, tug'ilgan kunlar,
/// nomzodlar). Har o'n soniyada, har bir ulangan
/// foydalanuvchi uchun takrorlansa — kun bo'yi bo'sh ish. Signal bilan
/// esa hisob FAQAT haqiqatan bir narsa yozilganda bajariladi.
/// </para>
/// <para>
/// <b>Chegarasi ochiq aytiladi: bu JARAYON ICHIDAGI signal.</b> Ikkinchi
/// API nusxasi ko'tarilsa (masalan yuk taqsimlagich ortida ikkita
/// konteyner), bir nusxadagi yozuv ikkinchisiga ulangan brauzerga
/// yetib bormaydi. Bugun bu muammo emas — tizim bitta konteynerda
/// ishlaydi va undan bir necha kishi foydalanadi. Kerak bo'lganda
/// almashtirish nuqtasi shu interfeys: uning ortiga Redis
/// <c>pub/sub</c> qo'yiladi va chaqiruvchi kod umuman o'zgarmaydi
/// (Redis allaqachon compose'da bor).
/// </para>
/// </remarks>
public interface IDataChangeSignal
{
    /// <summary>Joriy versiya. Har o'zgarishda ortadi.</summary>
    /// <remarks>
    /// Ma'nosi YO'Q son — u faqat SOLISHTIRISH uchun. «Nechta o'zgarish
    /// bo'ldi» degan savolga javob bermaydi va bermasligi ham kerak:
    /// bir nechta yozuv bitta versiyaga siqilishi mumkin.
    /// </remarks>
    long Version { get; }

    /// <summary>Versiyani oshiradi va kutayotganlarni uyg'otadi.</summary>
    void Notify();

    /// <summary>
    /// Versiya <paramref name="known"/> dan farq qilguncha kutadi.
    /// </summary>
    /// <param name="known">Chaqiruvchi bilgan oxirgi versiya.</param>
    /// <param name="timeout">
    /// Shu muddatdan keyin o'zgarish BO'LMASA ham qaytadi. Kutish
    /// cheksiz bo'lmasligi kerak: <c>stream</c> vaqti-vaqti bilan
    /// «tirikman» belgisini yuborishi va ulanish uzilganini sezishi
    /// kerak.
    /// </param>
    /// <param name="cancellationToken">Ulanish uzilganda kutish to'xtaydi.</param>
    /// <returns>
    /// Kutishdan keyingi versiya. <paramref name="known"/> ga TENG
    /// bo'lsa — muddat tugadi, o'zgarish yo'q.
    /// </returns>
    Task<long> WaitAsync(long known, TimeSpan timeout, CancellationToken cancellationToken);
}
