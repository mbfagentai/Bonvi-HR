using PdfSharp.Fonts;

namespace Bonvi.Infrastructure.Reports.Pdf;

/// <summary>
/// PDF shriftlari — yig'malaning ICHIDAN (tizim shriftlari umuman ishlatilmaydi).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega o'zimizniki.</b> Konteynerda shrift yo'q va PDFsharp'ning Core
/// yig'malasi shriftni o'zi qidirmaydi — hal qiluvchisiz u BIRINCHI matndayoq
/// yiqiladi. Shriftlar yig'malada: Docker tasviriga nusxalash unutilib, prod'da
/// birinchi anketa yiqilishi mumkin bo'lgan yo'l yo'q.
/// </para>
/// <para>
/// <b>Liberation Sans</b> — Arial bilan metrikasi bir xil (blanka Arial'da
/// terilgan), ya'ni qatorlar qog'ozdagidek sinadi. <b>DejaVu Sans</b> — faqat
/// ☐ ☑ belgilari uchun: Liberation'da ular yo'q. Har qanday boshqa oila nomi
/// (Arial, Helvetica) Liberation Sans'ga tushadi — bo'sh kvadrat chizilgandan
/// ko'ra yaqin shrift yaxshi.
/// </para>
/// <para>
/// ⚠️ <c>GlobalFontSettings.FontResolver</c> — jarayon uchun BITTA va birinchi
/// ishlatilgandan keyin almashtirib bo'lmaydi. Shu sababli o'rnatish
/// <see cref="EnsureInstalled"/> da, qulf ostida va faqat bir marta.
/// </para>
/// </remarks>
internal sealed class BundledFontResolver : IFontResolver
{
    /// <summary>Asosiy matn oilasi.</summary>
    public const string Sans = "Liberation Sans";

    /// <summary>☐ ☑ belgilari uchun oila.</summary>
    public const string Symbols = "DejaVu Sans";

    private const string Regular = "LiberationSans-Regular";
    private const string Bold = "LiberationSans-Bold";
    private const string Italic = "LiberationSans-Italic";
    private const string BoldItalic = "LiberationSans-BoldItalic";
    private const string DejaVu = "DejaVuSans";

    private static readonly Lock Gate = new();
    private static bool _installed;

    private readonly Dictionary<string, byte[]> _cache = new(StringComparer.Ordinal);

    /// <summary>Hal qiluvchini jarayonga bir marta o'rnatadi.</summary>
    public static void EnsureInstalled()
    {
        lock (Gate)
        {
            if (_installed)
            {
                return;
            }

            GlobalFontSettings.FontResolver = new BundledFontResolver();
            _installed = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        if (string.Equals(familyName, Symbols, StringComparison.OrdinalIgnoreCase))
        {
            // DejaVu'ning bitta yuzi bor — qalin/qiya kerak bo'lsa PDFsharp o'zi taqlid qiladi
            return new FontResolverInfo(DejaVu, bold, italic);
        }

        var face = (bold, italic) switch
        {
            (true, true) => BoldItalic,
            (true, false) => Bold,
            (false, true) => Italic,
            _ => Regular,
        };

        return new FontResolverInfo(face);
    }

    public byte[]? GetFont(string faceName)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(faceName, out var cached))
            {
                return cached;
            }

            var bytes = ReadResource($"Fonts.{faceName}.ttf");
            _cache[faceName] = bytes;

            return bytes;
        }
    }

    /// <summary>Yig'madagi resursni o'qiydi (<c>Reports/Pdf/…</c>).</summary>
    internal static byte[] ReadResource(string relativeName)
    {
        var assembly = typeof(BundledFontResolver).Assembly;
        var name = $"{typeof(BundledFontResolver).Namespace}.{relativeName}";

        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"PDF resursi yig'malada yo'q: {name}");

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return buffer.ToArray();
    }
}
