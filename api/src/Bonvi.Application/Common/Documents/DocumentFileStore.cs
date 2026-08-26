using Bonvi.Application.Abstractions;

namespace Bonvi.Application.Common.Documents;

/// <summary>Hujjat nima uchun qabul qilinmadi.</summary>
public enum DocumentRejection
{
    /// <summary>Fayl chegaradan (<see cref="DocumentLimits.MaxBytes"/>) katta.</summary>
    TooLarge = 1,

    /// <summary>Fayl mazmuni tanilmadi (PDF/JPEG/PNG/WebP emas yoki buzilgan).</summary>
    Invalid = 2,
}

/// <summary>Hujjatni qabul qilish natijasi: saqlash kaliti yoki rad etish sababi.</summary>
public sealed record DocumentAcceptance
{
    private DocumentAcceptance(string? key, string? contentType, DocumentRejection? rejection)
    {
        Key = key;
        ContentType = contentType;
        Rejection = rejection;
    }

    /// <summary>Saqlangan faylning kaliti. Rad etilgan bo'lsa — <c>null</c>.</summary>
    public string? Key { get; }

    /// <summary>Mazmun bo'yicha aniqlangan MIME turi. Rad etilgan bo'lsa — <c>null</c>.</summary>
    public string? ContentType { get; }

    /// <summary>Rad etish sababi. Qabul qilingan bo'lsa — <c>null</c>.</summary>
    public DocumentRejection? Rejection { get; }

    public static DocumentAcceptance Accepted(string key, string contentType) => new(key, contentType, null);

    public static DocumentAcceptance Rejected(DocumentRejection rejection) => new(null, null, rejection);
}

/// <summary>
/// Xodim hujjatining umumiy quvuri: tekshirish → saqlash → berish → o'chirish.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida sinf.</b> <c>SquarePhotoStore</c> bilan bir xil sabab:
/// tekshirish qoidasi bitta joyda tursin, ikki marta yozilib, ikkisi
/// bir-biridan uzoqlashib qolmasin.
/// </para>
/// <para>
/// <b><c>SquarePhotoStore</c>dan farqi.</b> Fayl QAYTA KODLANMAYDI va
/// QIRQILMAYDI — hujjat (shartnoma, buyruq) mazmuni o'zgarishsiz saqlanishi
/// SHART. Shu sababli bu yerda rasm kutubxonasi emas,
/// <see cref="IFileContentInspector"/> ishlatiladi: u faylni faqat
/// TEKSHIRADI, o'zgartirmaydi.
/// </para>
/// </remarks>
public sealed class DocumentFileStore
{
    private readonly IFileStorage _storage;
    private readonly IFileContentInspector _inspector;

    public DocumentFileStore(IFileStorage storage, IFileContentInspector inspector)
    {
        _storage = storage;
        _inspector = inspector;
    }

    /// <summary>
    /// Yuklangan faylni tekshiradi va o'zgarishsiz saqlaydi.
    /// </summary>
    public async Task<DocumentAcceptance> AcceptAsync(
        Stream content,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Arzon tekshiruv oldinda: e'lon qilingan hajm chegaradan katta
        // bo'lsa, faylni butunlay o'qib o'tirishning ma'nosi yo'q.
        if (sizeBytes > DocumentLimits.MaxBytes)
        {
            return DocumentAcceptance.Rejected(DocumentRejection.TooLarge);
        }

        // Butunlay xotiraga o'qiladi: mazmunni tekshirish uchun ham (kalitni
        // ikki marta — avval tekshirish, keyin saqlash uchun — o'qish o'rniga),
        // ham saqlash uchun ASL baytlarning o'zi kerak.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (buffer.Length == 0)
        {
            return DocumentAcceptance.Rejected(DocumentRejection.Invalid);
        }

        var bytes = buffer.ToArray();
        var kind = _inspector.Detect(bytes);

        if (kind is not { } detected)
        {
            return DocumentAcceptance.Rejected(DocumentRejection.Invalid);
        }

        var contentType = DocumentLimits.ContentTypeFor(detected);

        // Fayl nomi mijozdan emas, ANIQLANGAN turdan yasaladi: kengaytma
        // ham mazmunga mos, mijoz yozgan nom saqlash yo'liga umuman
        // tushmaydi (`LocalFileStorage` GUID kalit yasaydi).
        await using var toSave = new MemoryStream(bytes, writable: false);
        var stored = await _storage
            .SaveAsync(toSave, "hujjat" + DocumentLimits.ExtensionFor(detected), contentType, cancellationToken)
            .ConfigureAwait(false);

        return DocumentAcceptance.Accepted(stored.Key, contentType);
    }

    /// <summary>Saqlangan faylni ochadi. Topilmasa — <c>null</c>.</summary>
    public Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken = default)
        => _storage.OpenAsync(key, cancellationToken);

    /// <summary>Saqlangan faylni diskdan o'chiradi.</summary>
    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
        => _storage.DeleteAsync(key, cancellationToken);
}
