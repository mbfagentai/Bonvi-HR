using Bonvi.Application.Abstractions;

namespace Bonvi.Application.Attendance.Ingest;

/// <summary>
/// O'tish suratini tekshirib, o'zgartirmasdan saqlaydi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega <c>SquarePhotoStore</c> emas.</b> U rasmni markazdan kvadrat
/// qilib qirqadi — bu avatar uchun to'g'ri, lekin yuz surati DALIL: kim
/// o'tganini isbotlaydigan qism aynan qirqilib ketishi mumkin edi.
/// </para>
/// <para>
/// <b>Nega <c>DocumentFileStore</c> ham emas.</b> U foydalanuvchi yuklagan
/// hujjat uchun: PDF ni ham qabul qiladi va 20 MB gacha ruxsat beradi.
/// Bu yerda qoida torroq — faqat JPEG/PNG, 1 MB gacha
/// (<see cref="IngestLimits.MaxPhotoBytes"/>) — va u o'sha umumiy quvurning
/// o'zidan qurilgan: saqlash <see cref="IFileStorage"/>, tur esa
/// <see cref="IFileContentInspector"/> BAYTLARGA qarab aniqlaydi, qism
/// e'lon qilgan <c>Content-Type</c> ga emas (SPEC §3.10).
/// </para>
/// <para>
/// Yaroqsiz surat XATO EMAS: u shunchaki saqlanmaydi va o'tish suratsiz
/// yoziladi. Suratsiz o'tish ham davomat.
/// </para>
/// </remarks>
public sealed class PassPhotoStore
{
    private readonly IFileStorage _storage;
    private readonly IFileContentInspector _inspector;

    public PassPhotoStore(IFileStorage storage, IFileContentInspector inspector)
    {
        _storage = storage;
        _inspector = inspector;
    }

    /// <summary>Suratni tekshiradi va saqlaydi.</summary>
    /// <param name="photo">Qurilma yuborgan baytlar.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>Saqlash kaliti; surat yo'q yoki yaroqsiz bo'lsa <c>null</c>.</returns>
    public async Task<string?> TrySaveAsync(byte[]? photo, CancellationToken cancellationToken)
    {
        if (photo is null || photo.Length == 0 || photo.Length > IngestLimits.MaxPhotoBytes)
        {
            return null;
        }

        var (fileName, contentType) = _inspector.Detect(photo) switch
        {
            DetectedFileKind.Jpeg => ("otish.jpg", "image/jpeg"),
            DetectedFileKind.Png => ("otish.png", "image/png"),
            _ => (null, null),
        };

        if (fileName is null || contentType is null)
        {
            return null;
        }

        // Nom mijozdan emas, ANIQLANGAN turdan: kengaytma mazmunga mos,
        // qurilma yozgan nom esa saqlash yo'liga umuman tushmaydi
        // (`LocalFileStorage` GUID kalit yasaydi, sana bo'yicha papkalaydi).
        await using var content = new MemoryStream(photo, writable: false);

        var stored = await _storage
            .SaveAsync(content, fileName, contentType, cancellationToken)
            .ConfigureAwait(false);

        return stored.Key;
    }

    /// <summary>
    /// Saqlangan suratni o'chiradi — o'tish takror bo'lib chiqqanda.
    /// </summary>
    /// <remarks>
    /// Kalit qator uchun kerak, shu sababli surat qatordan OLDIN yoziladi
    /// (SPEC §3.6). Qator takror bo'lsa, fayl shu so'rovning o'zida
    /// o'chiriladi — birinchi o'tishning surati tegilmaydi, chunki uning
    /// kaliti boshqa.
    /// </remarks>
    /// <param name="key">Saqlash kaliti.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public Task DeleteAsync(string key, CancellationToken cancellationToken)
        => _storage.DeleteAsync(key, cancellationToken);
}
