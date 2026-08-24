using Bonvi.Application.Abstractions;

namespace Bonvi.Application.Common.Photos;

/// <summary>Rasm nima uchun qabul qilinmadi.</summary>
/// <remarks>
/// Nega <c>Error</c> emas: xato KODI — bo'limning shartnomasi
/// («xodim.rasm_turi» va «hisob.rasm_turi» ikki xil kod, chunki
/// interfeys ularga boshqacha munosabatda bo'ladi). Umumiy quvur esa
/// faqat SABABNI biladi, uni qanday nomlashni chaqiruvchi hal qiladi.
/// </remarks>
public enum PhotoRejection
{
    /// <summary>Fayl chegaradan katta.</summary>
    TooLarge = 1,

    /// <summary>E'lon qilingan MIME turi ro'yxatda yo'q.</summary>
    UnsupportedType = 2,

    /// <summary>Fayl rasm sifatida ochilmadi (mazmun bo'yicha).</summary>
    Invalid = 3,
}

/// <summary>Rasmni qabul qilish natijasi: saqlash kaliti yoki rad etish sababi.</summary>
public sealed record PhotoAcceptance
{
    private PhotoAcceptance(string? key, PhotoRejection? rejection)
    {
        Key = key;
        Rejection = rejection;
    }

    /// <summary>Saqlangan faylning kaliti. Rad etilgan bo'lsa — <c>null</c>.</summary>
    public string? Key { get; }

    /// <summary>Rad etish sababi. Qabul qilingan bo'lsa — <c>null</c>.</summary>
    public PhotoRejection? Rejection { get; }

    public static PhotoAcceptance Accepted(string key) => new(key, null);

    public static PhotoAcceptance Rejected(PhotoRejection rejection) => new(null, rejection);
}

/// <summary>Topilgan rasm — HTTP javobiga tayyor holat.</summary>
/// <param name="Version">Barqaror versiya belgisi.</param>
/// <param name="ContentType">MIME turi.</param>
/// <param name="Content">
/// Bayt oqimi. <c>null</c> bo'lsa — mijozdagi nusxa hali dolzarb va
/// mazmunni qayta jo'natish shart emas.
/// </param>
public sealed record PhotoContent(string Version, string ContentType, Stream? Content);

/// <summary>
/// Kvadrat suratning umumiy quvuri: tekshirish → qirqish → saqlash → berish.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida sinf.</b> Xodim surati va hisob surati bir xil ishni
/// bajaradi va faqat «kalit qayerda saqlanadi» bilan farq qiladi. Quvur
/// har bir bo'limda takrorlansa, xavfsizlik tuzatishi (masalan yangi
/// format taqiqlash) ikki joyda qilinishi kerak bo'lardi — va bir joyi
/// unutilardi.
/// </para>
/// <para>
/// <b>Nima QILMAYDI.</b> Bu sinf ma'lumotlar bazasiga tegmaydi va
/// tranzaksiyani bilmaydi: kalitni qayerga yozish va eski faylni qachon
/// o'chirish — agregat egasining ishi. Shu sababli u <c>IUnitOfWork</c>
/// ga bog'lanmaydi va istalgan agregat bilan ishlaydi.
/// </para>
/// </remarks>
public sealed class SquarePhotoStore
{
    private readonly IFileStorage _storage;
    private readonly IImageProcessor _images;

    public SquarePhotoStore(IFileStorage storage, IImageProcessor images)
    {
        _storage = storage;
        _images = images;
    }

    /// <summary>
    /// Yuklangan faylni tekshiradi, markazdan kvadrat qirqib saqlaydi.
    /// </summary>
    /// <remarks>
    /// Manba fayl SAQLANMAYDI: 4 MB lik telefon surati ham, 200 KB lik skan
    /// ham bir xil 512×512 JPEG bo'lib qoladi. Natijada yuzlab surat jami
    /// bir necha o'nlab megabayt joy egallaydi va ro'yxat ekrani tez ochiladi.
    /// </remarks>
    public async Task<PhotoAcceptance> AcceptAsync(
        Stream content,
        string? contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        // Arzon tekshiruvlar oldinda: chegaradan katta yoki umuman rasm
        // bo'lmagan faylni ochib, dekodlab o'tirishning ma'nosi yo'q.
        if (sizeBytes > PhotoLimits.MaxBytes)
        {
            return PhotoAcceptance.Rejected(PhotoRejection.TooLarge);
        }

        if (!PhotoLimits.IsAllowedContentType(contentType))
        {
            return PhotoAcceptance.Rejected(PhotoRejection.UnsupportedType);
        }

        var processed = await _images
            .ToSquareAsync(content, PhotoLimits.SquareSize, cancellationToken)
            .ConfigureAwait(false);

        if (processed is null)
        {
            return PhotoAcceptance.Rejected(PhotoRejection.Invalid);
        }

        await using var square = processed.Content;

        var stored = await _storage
            .SaveAsync(square, PhotoLimits.StoredFileName, processed.ContentType, cancellationToken)
            .ConfigureAwait(false);

        return PhotoAcceptance.Accepted(stored.Key);
    }

    /// <summary>
    /// Saqlangan rasmni beradi; mijozdagi nusxa dolzarb bo'lsa — mazmunsiz.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega versiya taqqoslash shu yerda.</b> Rasm o'zgarmaydigan mazmun:
    /// yangi surat yuklansa yangi kalit paydo bo'ladi. Shu sababli mijozdagi
    /// nusxa dolzarbligini KALIT bo'yicha, faylni ochmasdan aniqlash mumkin.
    /// Tekshiruv HTTP qatlamida qolsa, u har safar faylni ochib, keyin uni
    /// tashlab yuborardi.
    /// </para>
    /// <para>
    /// Fayl yo'qolgan holat (kalit bazada bor, fayl esa yo'q) <c>null</c>
    /// beradi — chaqiruvchi uni «rasm yo'q» deb qaraydi. Bunday holat faqat
    /// qo'lda aralashuvda yuzaga keladi va interfeys uchun natija bir xil.
    /// </para>
    /// </remarks>
    public async Task<PhotoContent?> OpenAsync(
        string key,
        string? knownVersion,
        CancellationToken cancellationToken = default)
    {
        var version = PhotoLimits.VersionOf(key);

        if (string.Equals(knownVersion, version, StringComparison.Ordinal))
        {
            return new PhotoContent(version, PhotoLimits.StoredContentType, Content: null);
        }

        var content = await _storage.OpenAsync(key, cancellationToken).ConfigureAwait(false);

        if (content is null)
        {
            return null;
        }

        // Tur qat'iy: rasm faqat shu quvur orqali yoziladi va u doim
        // JPEG'ga keltiriladi, ya'ni kalitning kengaytmasiga qarash shart emas.
        return new PhotoContent(version, PhotoLimits.StoredContentType, content);
    }

    /// <summary>Saqlangan faylni o'chiradi.</summary>
    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
        => _storage.DeleteAsync(key, cancellationToken);
}
