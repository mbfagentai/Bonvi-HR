using Bonvi.Application.Abstractions;
using SkiaSharp;

namespace Bonvi.Infrastructure.Storage;

/// <summary>
/// <see cref="IFileContentInspector"/> ning amalga oshirilishi.
/// </summary>
/// <remarks>
/// <para>
/// <b>PDF — imzo bo'yicha.</b> Fayl <c>%PDF-</c> bilan boshlanadimi,
/// tekshiriladi. To'liq PDF tuzilmasini tahlil qiladigan kutubxona
/// ATAYLAB qo'shilmagan: server PDF'ni hech qachon ochib o'qimaydi yoki
/// ko'rsatmaydi (faqat saqlaydi va baytlarni qaytaradi), shu sababli
/// chuqurroq tekshiruv real xavfsizlik foyda bermaydi — faqat murakkablik
/// qo'shardi.
/// </para>
/// <para>
/// <b>Rasm formatlari — <c>SkiaImageProcessor</c> bilan bir xil dekoder.</b>
/// <c>SKCodec.Create</c> faylni haqiqatan formatga mos ekanini tekshiradi
/// (sarlavhani o'qib, ichki tuzilmani tasdiqlaydi) — soxta kengaytma yoki
/// «.jpg» deb nomlangan boshqa fayl shu yerda to'xtaydi. Bu yerda, foto
/// quvuridan farqli, rasm QAYTA KODLANMAYDI: faqat tekshiriladi, xolos —
/// hujjat mazmuni o'zgarishsiz qoladi.
/// </para>
/// <para>
/// Holatsiz, shu sababli singleton sifatida ro'yxatga olinadi.
/// </para>
/// </remarks>
public sealed class FileContentInspector : IFileContentInspector
{
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    public DetectedFileKind? Detect(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (StartsWith(content, PdfSignature))
        {
            return DetectedFileKind.Pdf;
        }

        using var data = SKData.CreateCopy(content);
        using var codec = SKCodec.Create(data);

        return codec?.EncodedFormat switch
        {
            SKEncodedImageFormat.Jpeg => DetectedFileKind.Jpeg,
            SKEncodedImageFormat.Png => DetectedFileKind.Png,
            SKEncodedImageFormat.Webp => DetectedFileKind.Webp,
            _ => null,
        };
    }

    private static bool StartsWith(byte[] content, byte[] prefix)
        => content.Length >= prefix.Length && content.AsSpan(0, prefix.Length).SequenceEqual(prefix);
}
