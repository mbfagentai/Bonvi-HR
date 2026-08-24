namespace Bonvi.Application.Abstractions;

/// <summary>Qayta ishlangan rasm — saqlashga tayyor holat.</summary>
/// <param name="Content">Bayt oqimi, boshidan o'qishga tayyor.</param>
/// <param name="ContentType">Natijaning MIME turi.</param>
/// <param name="Width">Eni (piksel).</param>
/// <param name="Height">Bo'yi (piksel).</param>
public sealed record ProcessedImage(Stream Content, string ContentType, int Width, int Height);

/// <summary>
/// Rasm qayta ishlash porti: xodim suratini bir xil o'lchamga keltiradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega port.</b> Rasm kutubxonasi — texnologiya tafsiloti va u
/// Infrastructure qatlamida qoladi. Use-case esa faqat «markazdan kvadrat
/// qirqib, shu o'lchamga keltir» degan talabni biladi. Kutubxona
/// almashtirilsa (yoki rasm ishlash alohida servisga chiqarilsa) biznes
/// mantiq umuman o'zgarmaydi.
/// </para>
/// <para>
/// <b>Fayl turi ham shu yerda aniqlanadi.</b> Kengaytma ham, mijoz yuborgan
/// <c>Content-Type</c> ham dalil emas: ikkalasini ham yuklovchining o'zi
/// yozadi. Yagona ishonchli tekshiruv — faylni haqiqatan rasm sifatida
/// ocholish. Shu sababli «bu rasm emas» degan javob aynan shu portdan
/// chiqadi, chaqiruvchi taxmin qilmaydi.
/// </para>
/// </remarks>
public interface IImageProcessor
{
    /// <summary>
    /// Markazdan kvadrat qirqib, <paramref name="size"/>×<paramref name="size"/> ga keltiradi.
    /// </summary>
    /// <param name="source">Manba oqimi.</param>
    /// <param name="size">Natijaning tomoni (piksel).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>
    /// Tayyor rasm; manba rasm bo'lmasa yoki qo'llab-quvvatlanmaydigan
    /// formatda bo'lsa — <c>null</c>.
    /// </returns>
    Task<ProcessedImage?> ToSquareAsync(Stream source, int size, CancellationToken cancellationToken = default);
}
