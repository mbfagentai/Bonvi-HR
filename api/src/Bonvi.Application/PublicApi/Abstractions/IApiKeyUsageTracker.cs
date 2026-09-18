namespace Bonvi.Application.PublicApi.Abstractions;

/// <summary>
/// «Kalit oxirgi marta qachon ishlatilgan» belgisini yozadi — <b>so'rov
/// yo'lidan tashqarida</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega alohida port, repozitoriyning o'zi emas.</b> Bu yozuv
/// so'rovning natijasiga umuman ta'sir qilmaydi va uni kutib turish
/// mantiqsiz: har bir O'QISH so'rovi bitta YOZISHga aylanardi (batafsil
/// <c>ApiKey.MarkUsed</c> izohida). Ya'ni bu «saqlash» emas, «chetdan
/// yozib qo'yish» — va u boshqa hayot davriga (<c>Singleton</c>) hamda
/// boshqa xato siyosatiga (yiqilsa — jim) ega.
/// </para>
/// <para>
/// Amalga oshirilishi Infrastructure qatlamida: u so'rov doirasidan
/// mustaqil doira ochadi, chunki so'rov tugagach uning
/// <c>DbContext</c> i yopiladi.
/// </para>
/// </remarks>
public interface IApiKeyUsageTracker
{
    /// <summary>
    /// Kalitning ishlatilganini belgilashni NAVBATGA qo'yadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Chaqiruv <b>bloklamaydi</b> va <b>hech qachon istisno tashlamaydi</b>:
    /// u autentifikatsiya yo'lidan chaqiriladi va o'sha yerda yiqilsa,
    /// yaroqli kalit bilan kelgan so'rov 500 olardi — ya'ni ma'lumot
    /// beruvchi ustun butun API ni to'xtatardi.
    /// </para>
    /// <para>
    /// Daqiqada bir martadan ko'p yozmaslik ham shu yerda hal qilinadi:
    /// chaqiruvchi har so'rovda bemalol chaqiradi.
    /// </para>
    /// </remarks>
    /// <param name="keyId">Kalit yozuvi.</param>
    /// <param name="ip">So'rov kelgan IP (loglarga tushmaydi, faqat jadvalga).</param>
    void Schedule(Guid keyId, string? ip);
}
