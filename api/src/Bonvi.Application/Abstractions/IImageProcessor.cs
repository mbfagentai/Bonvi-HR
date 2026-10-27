namespace Bonvi.Application.Abstractions;

/// <summary>Qayta ishlangan rasm — saqlashga tayyor holat.</summary>
/// <param name="Content">Bayt oqimi, boshidan o'qishga tayyor.</param>
/// <param name="ContentType">Natijaning MIME turi.</param>
/// <param name="Width">Eni (piksel).</param>
/// <param name="Height">Bo'yi (piksel).</param>
public sealed record ProcessedImage(Stream Content, string ContentType, int Width, int Height);

/// <summary>
/// Rasm qayta ishlash porti: rasmni bir xil o'lchamga keltiradi.
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
/// <para>
/// <b>Ikki shakl, ikki maqsad.</b> <see cref="ToSquareAsync"/> — AVATAR:
/// o'lcham qat'iy, chunki u ro'yxatdagi doirada va yuqori paneldagi kichik
/// suratda ko'rinadi, ya'ni nisbatni saqlashning ma'nosi yo'q va markazdan
/// qirqish eng yaxshi natija beradi. <see cref="ToBoundedAsync"/> — HUJJAT
/// SURATI (pasport, diplom sahifasi): u yerda qirqish MA'LUMOTNI YO'QOTADI
/// (chetdagi seriya raqami yoki muhr kesilib ketardi), shu sababli faqat
/// chegara qo'yiladi va nisbat o'zgarmaydi. Ikkita alohida metod aynan shu
/// farq uchun: bitta «universal» metod ikkita mantiqni bayroq bilan
/// boshqarardi va chaqiruvchi har safar qaysi bayroqni qo'yishni o'ylardi.
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

    /// <summary>
    /// Rasmni <paramref name="maxEdge"/>×<paramref name="maxEdge"/> qutisiga
    /// sig'diradi: nisbat saqlanadi, qirqilmaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Kattalashtirilmaydi.</b> Chegaradan kichik rasm o'lchamini
    /// saqlaydi, lekin QAYTA KODLANADI: mijozning shikoyati aynan shu
    /// holatdan boshlandi — 1200 px, ammo sifati 100 ga qo'yilgan 5 MB lik
    /// JPEG. Kattalashtirish esa bir bayt ma'lumot qo'shmaydi, faqat faylni
    /// o'stiradi.
    /// </para>
    /// <para>
    /// <b>Natija doim JPEG.</b> Nomzod biriktirgan fayl — surat yoki skan;
    /// shaffoflik u yerda hech qanday ma'no tashimaydi. PNG/WebP ni saqlash
    /// ikkita chiqish formati, ikkita MIME turi va ikkita kod yo'li degani
    /// bo'lardi — foydasi yo'q. Shaffof fon oq bo'lib qoladi
    /// (<c>SquarePhotoStore</c> ham xuddi shunday qiladi).
    /// </para>
    /// </remarks>
    /// <param name="source">Manba oqimi.</param>
    /// <param name="maxEdge">Eng uzun tomonning chegarasi (piksel).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>
    /// Tayyor rasm; manba rasm bo'lmasa, qo'llab-quvvatlanmaydigan formatda
    /// bo'lsa, buzilgan bo'lsa yoki piksel hisobi aql bovar qilmas darajada
    /// katta bo'lsa (siqilgan «bomba» fayl) — <c>null</c>. Ya'ni chaqiruvchi
    /// istisno TUTMAYDI: bu yerda «yaroqsiz fayl» kutilgan javob, favqulodda
    /// holat emas.
    /// </returns>
    Task<ProcessedImage?> ToBoundedAsync(Stream source, int maxEdge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Barmoq bilan chizilgan imzoni QAYTA CHIZADI: faqat PNG, o'lchami
    /// <paramref name="maxWidth"/>×<paramref name="maxHeight"/> dan oshmaydi,
    /// shaffof joylari oq fonga yotqiziladi, natija — serverning o'zi kodlagan PNG.
    /// </summary>
    /// <remarks>
    /// Mijoz yuborgan baytlar HECH QACHON saqlanmaydi: imzo keyin har safar HR PDF'ni
    /// ochganda dekodlanadi va kichik fayl ichida ulkan o'lcham e'lon qilgan PNG
    /// («bomba») server xotirasini tugatardi. O'lcham sarlavhadan, dekodlashdan OLDIN
    /// tekshiriladi.
    /// </remarks>
    /// <param name="source">Manba oqimi.</param>
    /// <param name="maxWidth">Eng katta eni (piksel).</param>
    /// <param name="maxHeight">Eng katta bo'yi (piksel).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>Oq fondagi PNG; PNG bo'lmasa, buzilgan yoki o'lchamdan katta bo'lsa — <c>null</c>.</returns>
    Task<ProcessedImage?> ToFlatPngAsync(Stream source, int maxWidth, int maxHeight, CancellationToken cancellationToken = default);
}
