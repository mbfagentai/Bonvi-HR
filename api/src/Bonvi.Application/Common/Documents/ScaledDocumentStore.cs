using Bonvi.Application.Abstractions;

namespace Bonvi.Application.Common.Documents;

/// <summary>
/// Kichraytirib saqlangan hujjatni qabul qilish natijasi: kalit, aniqlangan
/// tur va DISKKA TUSHGAN hajm — yoki rad etish sababi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <see cref="DocumentAcceptance"/> dan alohida yozuv.</b> Farqi
/// bitta maydonda: <see cref="StoredBytes"/>. Uni eskisiga qo'shib qo'yish
/// mumkin edi, lekin o'shanda <c>DocumentFileStore</c> ni ishlatadigan
/// chaqiruvchilar (xodim hujjatlari) hech qachon ma'nosi bo'lmagan maydonni
/// ko'rardi va ertami-kechmi kimdir uni o'qib qo'yardi.
/// </para>
/// <para>
/// <b>Nega <c>Result</c> emas.</b> Xato KODI — bo'limning shartnomasi.
/// Umumiy quvur faqat SABABNI biladi, uni qanday nomlashni chaqiruvchi hal
/// qiladi — bu <c>PhotoRejection</c> va <see cref="DocumentRejection"/>
/// da allaqachon qabul qilingan qoida. Nomzod bo'limida moslik shunday:
/// <see cref="DocumentRejection.TooLarge"/> → <c>nomzod.rezyume_katta</c>,
/// <see cref="DocumentRejection.Invalid"/> → <c>nomzod.rezyume_yaroqsiz</c>.
/// </para>
/// </remarks>
public sealed record ScaledDocumentAcceptance
{
    private ScaledDocumentAcceptance(
        string? key,
        string? contentType,
        long storedBytes,
        DocumentRejection? rejection)
    {
        Key = key;
        ContentType = contentType;
        StoredBytes = storedBytes;
        Rejection = rejection;
    }

    /// <summary>Saqlangan faylning kaliti. Rad etilgan bo'lsa — <c>null</c>.</summary>
    public string? Key { get; }

    /// <summary>Mazmun bo'yicha aniqlangan MIME turi. Rad etilgan bo'lsa — <c>null</c>.</summary>
    /// <remarks>
    /// Rasm uchun bu DOIM <c>image/jpeg</c>, manba PNG yoki WebP bo'lganda
    /// ham: quvur natijani bir turga keltiradi. PDF uchun —
    /// <c>application/pdf</c>.
    /// </remarks>
    public string? ContentType { get; }

    /// <summary>
    /// Diskka YOZILGAN hajm (bayt) — yuklangan hajm emas.
    /// </summary>
    /// <remarks>
    /// Aynan shu son bazaga tushadi. 4 MB lik telefon surati 0,3-0,7 MB
    /// bo'lib saqlanadi; yuklangan hajm yozilsa, karta HR ga yolg'on raqam
    /// ko'rsatardi. Rad etilgan holatda — 0.
    /// </remarks>
    public long StoredBytes { get; }

    /// <summary>Rad etish sababi. Qabul qilingan bo'lsa — <c>null</c>.</summary>
    public DocumentRejection? Rejection { get; }

    public static ScaledDocumentAcceptance Accepted(string key, string contentType, long storedBytes)
        => new(key, contentType, storedBytes, null);

    public static ScaledDocumentAcceptance Rejected(DocumentRejection rejection)
        => new(null, null, 0, rejection);
}

/// <summary>
/// Nomzod fayli quvuri: tekshirish → rasmni kichraytirish → saqlash.
/// </summary>
/// <remarks>
/// <para>
/// <b>⚠️ Nega <see cref="DocumentFileStore"/> o'zgartirilmadi.</b> U yerda
/// fayl QAYTA KODLANMASLIGI SHART — xodim hujjati (shartnoma, buyruq)
/// mazmuni o'zgarishsiz saqlanadigan hujjat. Kichraytirishni o'sha sinfga
/// qo'shish butun tashkilotning shartnoma skanlarini jimgina qayta
/// kodlashni boshlardi. Shu sababli bu — YONIDA turgan YANGI sinf, eskisi
/// bir bayt ham o'zgarmagan.
/// </para>
/// <para>
/// <b>Nima uchun nomzod fayliga boshqacha munosabat.</b> Nomzod fayli —
/// hujjat NUSXASI (pasport, diplom sahifasi telefonda suratga olingan), asl
/// nusxa emas: uni o'qib bo'lsa bas.
/// </para>
/// <para>
/// <b>Amaliy natija — o'lchangan.</b> Matni zich hujjat surati: 3,55 MB →
/// 718 KB (19,7%), xuddi shu rasm sifati 100 da yuklansa 9,76 MB → 711 KB
/// (7,1%), A4 diplom 2,11 MB → 587 KB. Ya'ni bitta nomzodning o'nta skani
/// 40 MB emas, 3-7 MB joy oladi. Raqamlar SPEC dagi «300-500 KB»
/// taxminidan biroz yuqori, chunki o'lchov ATAYLAB eng og'ir holatda
/// bajarilgan (butun varaq matn bilan to'la va ustiga shovqin qo'shilgan);
/// haqiqiy suratda bo'sh joy ko'proq bo'ladi.
/// </para>
/// <para>
/// <b>Kichik fayl KATTALASHISHI ham mumkin.</b> Chegaradan kichik, bir
/// tekis fonli PNG (masalan ekran surati) JPEG'ga o'tganda bir necha
/// kilobayt o'sadi: JPEG bir tekis maydonni PNG'chalik yaxshi siqmaydi.
/// Bu — «natija doim JPEG» qaroridan kelib chiqadigan va QABUL QILINGAN
/// natija: manba formatini saqlash ikkita chiqish turi, ikkita kengaytma
/// va ikkita kod yo'li degani bo'lardi. Bazadagi hajm baribir haqiqiy —
/// diskka tushgan son yoziladi.
/// </para>
/// <para>
/// <b>PDF baytma-bayt saqlanadi.</b> Serverda PDF kutubxonasi yo'q va uni
/// qayta yozish — aynan <see cref="DocumentFileStore"/> oldini oladigan ish.
/// PDF odatda allaqachon siqilgan bo'ladi, ya'ni yutuq ham kam edi.
/// </para>
/// <para>
/// <b>Nima QILMAYDI.</b> Bazaga tegmaydi va tranzaksiyani bilmaydi: kalitni
/// qayerga yozish va domen faylni rad etganda uni qachon o'chirish —
/// chaqiruvchining ishi (<c>SquarePhotoStore</c> dagi bilan bir xil qoida).
/// </para>
/// </remarks>
public sealed class ScaledDocumentStore
{
    /// <summary>Saqlanadigan rasmning eng uzun tomoni (piksel) — 2000.</summary>
    /// <remarks>
    /// <para>
    /// 2000 px dagi A4 varaq ≈ 170 dpi: pasport raqami, diplomdagi mayda
    /// matn va muhr o'qiladi — bu fayllar faqat shuning uchun bor. 1600 px
    /// (≈135 dpi) muhr va qo'lda yozilgan kirill matnini yoya boshlaydi,
    /// 2000 px dan yuqorisida esa fayl kvadratik o'sadi, lekin ekranda
    /// hech kim ko'rmaydigan tafsilot uchun.
    /// </para>
    /// <para>
    /// Doimiy sifatida qoladi, tizim sozlamasi EMAS: bitta mijoz, bitta
    /// server, va bu son saqlash qoidasi — uni ekrandan tanlash imkoniyati
    /// faqat behuda sozlashga taklif qilardi.
    /// </para>
    /// </remarks>
    public const int MaxImageEdge = 2000;

    private readonly IFileStorage _storage;
    private readonly IFileContentInspector _inspector;
    private readonly IImageProcessor _images;

    public ScaledDocumentStore(IFileStorage storage, IFileContentInspector inspector, IImageProcessor images)
    {
        _storage = storage;
        _inspector = inspector;
        _images = images;
    }

    /// <summary>
    /// Yuklangan faylni tekshiradi, rasm bo'lsa kichraytiradi va saqlaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ikki xil hajm, ikki xil vazifa.</b> <paramref name="sizeBytes"/> —
    /// YUKLANGAN hajm va u faqat 20 MB chegarasini tekshirish uchun: faylni
    /// dekodlashdan OLDIN rad etish kerak. Natijadagi
    /// <see cref="ScaledDocumentAcceptance.StoredBytes"/> esa — diskka
    /// TUSHGAN hajm va u bazaga yoziladi. Ikkalasini adashtirish — nomzod
    /// kartasida yolg'on raqam degani.
    /// </para>
    /// <para>
    /// <b>Tur faqat MAZMUNDAN aniqlanadi.</b> Kengaytma ham, mijozning
    /// <c>Content-Type</c> sarlavhasi ham dalil emas — ikkalasini ham
    /// yuklovchining o'zi yozadi. «.jpg» deb nomlangan skript
    /// <see cref="IFileContentInspector"/> da to'xtaydi.
    /// </para>
    /// <para>
    /// <b>Buferning chegarasi.</b> Oqim to'liq xotiraga o'qiladi. Uni
    /// e'lon qilingan hajm emas, HTTP qatlamidagi so'rov tanasi chegarasi
    /// (<c>RequestSizeLimitAttribute</c>) ushlab turadi: mijoz «1 KB» deb
    /// e'lon qilib, 100 MB yuborsa, Kestrel tanani oxirigacha o'qimaydi.
    /// <see cref="DocumentFileStore"/> ham aynan shu himoyaga tayanadi.
    /// </para>
    /// </remarks>
    /// <param name="content">Yuklangan faylning oqimi.</param>
    /// <param name="sizeBytes">Faylning e'lon qilingan (yuklangan) hajmi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<ScaledDocumentAcceptance> AcceptAsync(
        Stream content,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Arzon tekshiruv oldinda: e'lon qilingan hajm chegaradan katta
        // bo'lsa, faylni butunlay o'qib, dekodlab o'tirishning ma'nosi yo'q.
        if (sizeBytes > DocumentLimits.MaxBytes)
        {
            return ScaledDocumentAcceptance.Rejected(DocumentRejection.TooLarge);
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (buffer.Length == 0)
        {
            return ScaledDocumentAcceptance.Rejected(DocumentRejection.Invalid);
        }

        var bytes = buffer.ToArray();

        if (_inspector.Detect(bytes) is not { } detected)
        {
            return ScaledDocumentAcceptance.Rejected(DocumentRejection.Invalid);
        }

        return detected == DetectedFileKind.Pdf
            ? await StorePdfAsync(bytes, cancellationToken).ConfigureAwait(false)
            : await StoreImageAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Saqlangan faylni ochadi. Topilmasa — <c>null</c>.</summary>
    /// <remarks>
    /// O'qish yo'lida kichraytirish yo'q va bo'lishi ham mumkin emas: fayl
    /// diskka allaqachon yakuniy holatda tushgan. Metod
    /// <see cref="DocumentFileStore"/> dagining aynan o'zi — chaqiruvchi
    /// faqat bitta quvurga bog'lansin, xodim hujjatlari sinfini nomzod
    /// bo'limiga sudrab kelmasin.
    /// </remarks>
    public Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken = default)
        => _storage.OpenAsync(key, cancellationToken);

    /// <summary>Saqlangan faylni diskdan o'chiradi.</summary>
    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
        => _storage.DeleteAsync(key, cancellationToken);

    /// <summary>PDF — o'zgarishsiz saqlanadi.</summary>
    private async Task<ScaledDocumentAcceptance> StorePdfAsync(
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        // Fayl nomi mijozdan emas, ANIQLANGAN turdan yasaladi: mijoz yozgan
        // nom saqlash yo'liga umuman tushmaydi («../../etc/passwd» aynan
        // shunday paydo bo'lardi). Asl nom ko'rsatish uchun bazada alohida
        // saqlanadi.
        await using var toSave = new MemoryStream(bytes, writable: false);

        var stored = await _storage
            .SaveAsync(
                toSave,
                "hujjat" + DocumentLimits.ExtensionFor(DetectedFileKind.Pdf),
                DocumentLimits.ContentTypeFor(DetectedFileKind.Pdf),
                cancellationToken)
            .ConfigureAwait(false);

        return ScaledDocumentAcceptance.Accepted(
            stored.Key,
            DocumentLimits.ContentTypeFor(DetectedFileKind.Pdf),
            bytes.Length);
    }

    /// <summary>JPEG/PNG/WebP — chegara ichiga sig'diriladi va JPEG bo'lib saqlanadi.</summary>
    private async Task<ScaledDocumentAcceptance> StoreImageAsync(
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        // YANGI oqim: yuqoridagi bufer `Detect` uchun allaqachon o'qib
        // bo'lingan va uni orqaga qaytarish o'rniga baytlardan yangisini
        // yasash ravshanroq.
        await using var source = new MemoryStream(bytes, writable: false);

        var processed = await _images
            .ToBoundedAsync(source, MaxImageEdge, cancellationToken)
            .ConfigureAwait(false);

        // Tekshiruvchi «rasm» dedi, dekoder esa ocholmadi. Uch holat:
        // ichki tuzilmasi buzilgan fayl, piksel «bomba»si va Skia'ning
        // kanvas yasay olmasligi. Uchalasi uchun ham javob bitta —
        // «yaroqsiz fayl»; istisno YUQORIGA CHIQMAYDI, chunki bu kutilgan
        // holat va u 500 emas, 400 bo'lishi kerak.
        if (processed is null)
        {
            return ScaledDocumentAcceptance.Rejected(DocumentRejection.Invalid);
        }

        await using var image = processed.Content;

        // Hajm SAQLASHDAN OLDIN olinadi: `SaveAsync` oqimni oxirigacha
        // o'qiydi va undan keyin `Length` ni o'qish saqlash amalga
        // oshirilishiga bog'lanib qolardi. `StoredFile.SizeBytes` ham xuddi
        // shu son, lekin u adapter TOMONIDAN xabar qilinadi — biz esa
        // domenning «hajm 0 dan katta» shartiga tayanamiz, ya'ni sonni o'z
        // qo'limizda ushlab turgan ma'qul.
        var storedBytes = image.Length;

        var stored = await _storage
            .SaveAsync(
                image,
                "hujjat" + DocumentLimits.ExtensionFor(DetectedFileKind.Jpeg),
                processed.ContentType,
                cancellationToken)
            .ConfigureAwait(false);

        return ScaledDocumentAcceptance.Accepted(stored.Key, processed.ContentType, storedBytes);
    }
}
