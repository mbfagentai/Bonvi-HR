namespace Bonvi.Application.Attendance.Ingest;

/// <summary>Multipart tanasining bitta qismi — transportdan xoli ko'rinishda.</summary>
/// <param name="Name">Qism nomi (<c>Content-Disposition: name</c>) — masalan <c>event_log</c>.</param>
/// <param name="FileName">Fayl nomi, bo'lsa.</param>
/// <param name="ContentType">Qism E'LON QILGAN turi. Unga ishonilmaydi — faqat qismni tanlashga yordam beradi.</param>
/// <param name="Content">Qism baytlari.</param>
public sealed record IngestFilePart(string? Name, string? FileName, string? ContentType, byte[] Content);

/// <summary>
/// Qurilma yuborgan so'rov tanasi — HTTP'siz ko'rinishda.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega bu yozuv bor.</b> Application qatlami <c>HttpRequest</c> ni
/// bilmasligi shart (arxitektura testi buni tekshiradi). Tana bu yerga Api
/// qatlamida bir marta o'qilib, baytlar ko'rinishida keladi: tahlilchi
/// sinovda ham, kelajakdagi boshqa transportda ham aynan shu yozuvni oladi.
/// </para>
/// <para>
/// <see cref="Body"/> — XOM, tahlil qilinmagan baytlar. HMAC imzosi aynan
/// shular ustida tekshiriladi: multipart qayta yig'ilsa, chegara satrlari va
/// bo'sh joylar boshqacha chiqib, imzo hech qachon mos kelmasdi.
/// </para>
/// <para>
/// <see cref="Parts"/> — Api qatlami ajratgan qismlar. Tana multipart
/// bo'lmasa yoki uni ajratib bo'lmasa — bo'sh ro'yxat. ⚠️ Ajratish xatosi Api
/// da 400 bo'lib QAYTMAYDI: javob autentifikatsiyadan OLDIN chiqib ketardi va
/// tokensiz so'rov «401» o'rniga «400» olardi — ya'ni to'rt xil nosozlikning
/// bir xil javobi degan qoida buzilardi. Bo'sh ro'yxatni tahlilchi
/// autentifikatsiyadan KEYIN «o'qib bo'lmadi» deb qaytaradi.
/// </para>
/// </remarks>
/// <param name="Body">So'rov tanasining xom baytlari.</param>
/// <param name="ContentType">So'rovning <c>Content-Type</c> sarlavhasi.</param>
/// <param name="Parts">Multipart qismlari; multipart bo'lmasa — bo'sh.</param>
public sealed record AttendanceIngestPayload(
    byte[] Body,
    string? ContentType,
    IReadOnlyList<IngestFilePart> Parts)
{
    /// <summary>Tana <c>multipart/*</c> deb e'lon qilinganmi.</summary>
    public bool IsMultipart
        => ContentType is { } type && type.TrimStart().StartsWith("multipart/", StringComparison.OrdinalIgnoreCase);
}
