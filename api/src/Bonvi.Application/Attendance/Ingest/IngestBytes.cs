namespace Bonvi.Application.Attendance.Ingest;

/// <summary>Qurilma yuborgan baytlar bilan ishlashning umumiy mayda qoidalari.</summary>
internal static class IngestBytes
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Boshidagi UTF-8 BOM ni tashlaydi.
    /// </summary>
    /// <remarks>
    /// Ba'zi qurilma va Windows'dagi agentlar JSON oldiga BOM qo'yadi.
    /// <c>JsonDocument</c>/<c>JsonNode</c> baytlar ustida uni o'tkazib
    /// yubormaydi (xato beradi), PostgreSQL <c>jsonb</c> esa matn boshidagi
    /// U+FEFF ni rad etadi — ya'ni BOM li hodisa yo «o'qib bo'lmadi», yo 500
    /// bo'lardi.
    /// </remarks>
    /// <param name="content">Xom baytlar.</param>
    /// <returns>BOM siz baytlar (nusxa emas — o'sha xotira bo'lagi).</returns>
    public static ReadOnlyMemory<byte> WithoutUtf8Bom(byte[] content)
        => content.AsSpan().StartsWith(Utf8Bom)
            ? content.AsMemory(Utf8Bom.Length)
            : content.AsMemory();
}
