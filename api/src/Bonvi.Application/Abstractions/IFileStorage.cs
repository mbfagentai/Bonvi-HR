namespace Bonvi.Application.Abstractions;

/// <summary>Saqlangan fayl haqidagi ma'lumot.</summary>
/// <param name="Key">Saqlash kaliti (ichki manzil).</param>
/// <param name="FileName">Asl fayl nomi.</param>
/// <param name="ContentType">MIME turi.</param>
/// <param name="SizeBytes">Hajmi (bayt).</param>
public sealed record StoredFile(string Key, string FileName, string ContentType, long SizeBytes);

/// <summary>
/// Fayl saqlash porti: xodim rasmi, pasport skani, buyruq, shartnoma.
/// </summary>
/// <remarks>
/// Hozircha lokal disk (Docker volume) orqali amalga oshiriladi — ortiqcha konteyner yo'q.
/// Kelajakda S3 yoki MinIO'ga o'tish uchun faqat shu interfeysning yangi
/// amalga oshirilishini yozish kifoya; qolgan kod o'zgarmaydi.
/// </remarks>
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
}
