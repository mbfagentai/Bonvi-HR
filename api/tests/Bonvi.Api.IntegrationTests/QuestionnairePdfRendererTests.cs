using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Infrastructure.Reports.Pdf;
using Microsoft.Extensions.Logging.Abstractions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Onlayn anketaning PDF'i (SPEC-planshet-anketa §6.3) — bazasiz, faqat chizuvchi.
/// </summary>
/// <remarks>
/// <c>BONVI_PDF_OUT</c> o'rnatilgan bo'lsa namunalar o'sha jildga yoziladi —
/// ko'z bilan qog'oz blanka bilan solishtirish uchun (testning o'zi ko'rinishni
/// tekshira olmaydi — faqat hujjat yaroqliligini, betlarini va matnini).
/// </remarks>
public sealed partial class QuestionnairePdfRendererTests
{
    private static readonly DateOnly Bugun = new(2026, 10, 24);

    private readonly QuestionnairePdfRenderer _renderer = new(NullLogger<QuestionnairePdfRenderer>.Instance);

    [Fact]
    public async Task Toliq_anketa_yaroqli_kop_betli_PDF_beradi()
    {
        var pdf = await _renderer.RenderAsync(Model(QuestionnaireSamples.Full(Bugun), QuestionnaireSamples.Photo(), QuestionnaireSamples.Signature()), TestContext.Current.CancellationToken);

        Saqla("anketa-toliq.pdf", pdf);

        Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");

        // Blanka v3.0 (pasport qatorlarisiz, qisqa kompyuter jadvali): A qism kamida 4 bet,
        // B qism — alohida oxirgi betda, A qism bilan aralashmaydi
        var betlar = Sahifalar(pdf);
        betlar.Count.ShouldBeGreaterThanOrEqualTo(5, "A qism kamida 4 bet + B qism alohida betda");
        betlar[^1].ShouldContain("B QISM HR BO‘LIMI TOMONIDAN TO‘LDIRILADI");
        betlar[^1].ShouldNotContain("11. TILXAT");
        betlar.Take(betlar.Count - 1).ShouldContain(bet => bet.Contains("11. TILXAT", StringComparison.Ordinal), "tilxat A qismning oxirida");
    }

    [Fact]
    public async Task Minimal_anketa_ham_rasm_va_imzosiz_ham_chiziladi()
    {
        var pdf = await _renderer.RenderAsync(Model(QuestionnaireSamples.Minimal(Bugun), photo: null, signature: null), TestContext.Current.CancellationToken);

        Saqla("anketa-minimal.pdf", pdf);

        Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");
        Betlar(pdf).ShouldBeGreaterThanOrEqualTo(5);
    }

    /// <summary>
    /// 4-bo'lim — qisqartirilgan jadval (egasi, 2026-10-26): 3 ustun, 11 dastur darajasi bilan,
    /// ostida «Boshqa dasturlar va dasturlash tillari» qatori. Olib tashlangan dasturlar va
    /// «Dasturlash» ustuni PDF'da yo'q. Blanka versiyasi — 3.1 (2026-10-27).
    /// </summary>
    [Fact]
    public async Task Kompyuter_jadvali_3_ustun_11_dastur_darajasi_bilan_va_boshqa_dasturlar_qatori()
    {
        var javoblar = QuestionnaireSamples.Full(Bugun);
        var pdf = await _renderer.RenderAsync(Model(javoblar, photo: null, signature: null), TestContext.Current.CancellationToken);
        var matn = Matn(pdf);

        matn.ShouldContain("HR-F02 · Nomzod anketasi · v3.1");
        matn.ShouldContain("Blanka HR-F02 · Versiya 3.1");
        matn.ShouldContain("4. KOMPYUTER SAVODXONLIGI");
        matn.ShouldContain("Darajani raqam bilan yozing: 1 – boshlang‘ich, 2 – o‘rta, 3 – yaxshi, 4 – mukammal. Bilmagan dasturingizni bo‘sh qoldiring.");
        matn.ShouldContain("Operatsion tizim Ofis dasturlari Maxsus dasturlar", Case.Sensitive, "uch ustun sarlavhasi, shu tartibda");

        // Har dastur katagidan keyin uning darajasi (jadval qatorma-qator chiziladi)
        foreach (var dastur in QuestionnaireFormCatalog.ComputerGroups.SelectMany(group => group.Items))
        {
            var daraja = javoblar.ComputerSkills![dastur.Key].ToString(CultureInfo.InvariantCulture);
            matn.ShouldContain($"{dastur.Label} {daraja}", Case.Sensitive, dastur.Key);
        }

        matn.ShouldContain("Boshqa dasturlar va dasturlash tillari: KOMPAS-3D o‘quv versiyasi");

        foreach (var olibTashlangan in new[]
                 {
                     "Python", "SolidWorks", "MS Access", "MS Outlook", "Linux", "macOS", "Power BI", "Adobe Acrobat",
                     "JavaScript", "Kotlin / Swift", "1C dasturlash tili", "Operatsion tizimlar", "Dasturlash",
                 })
        {
            matn.ShouldNotContain(olibTashlangan, Case.Sensitive);
        }
    }

    [Fact]
    public async Task Bosh_kompyuter_jadvali_qogozdagidek_chiziqcha_bilan()
    {
        var pdf = await _renderer.RenderAsync(Model(QuestionnaireSamples.Minimal(Bugun), photo: null, signature: null), TestContext.Current.CancellationToken);
        var matn = Matn(pdf);

        matn.ShouldContain("Windows MS Word 1C Smartfon (Android / iOS) MS Excel", Case.Sensitive, "darajasiz — kataklar bo'sh");
        matn.ShouldContain("Boshqa dasturlar va dasturlash tillari: ____");
    }

    [Fact]
    public async Task Buzuq_surat_PDFni_yiqitmaydi()
    {
        var pdf = await _renderer.RenderAsync(Model(QuestionnaireSamples.Minimal(Bugun), photo: [1, 2, 3, 4], signature: [9, 9, 9]), TestContext.Current.CancellationToken);

        Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");
    }

    /// <summary>
    /// Chegaradan katta saqlangan rasm (imzo 1200×600 dan, surat 4096 dan katta) DEKODLANMAYDI —
    /// PDF rasmsiz chiziladi (diskka boshqa yo'l bilan tushgan fayl uchun ikkinchi qatlam).
    /// </summary>
    [Fact]
    public async Task Chegaradan_katta_rasm_dekodlanmaydi_PDF_rasmsiz_chiziladi()
    {
        var pdf = await _renderer.RenderAsync(
            Model(QuestionnaireSamples.Minimal(Bugun), photo: Png(4100, 8), signature: Png(1300, 200)),
            TestContext.Current.CancellationToken);

        Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");
    }

    private static byte[] Png(int width, int height)
    {
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(width, height));
        surface.Canvas.Clear(SkiaSharp.SKColors.Gray);

        using var image = surface.Snapshot();
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    /// <summary>Namuna avval tekshiruvchidan o'tadi — PDF'ga faqat tozalangan javob boradi (haqiqiy oqimdagidek).</summary>
    private static QuestionnairePdfModel Model(QuestionnaireFormAnswers answers, byte[]? photo, byte[]? signature)
        => new(
            QuestionnaireFormValidator.Validate(answers, Bugun, ReferralSeeds.Options).Value,
            Number: 1024,
            SubmittedAt: new DateTimeOffset(2026, 10, 24, 14, 41, 0, TimeSpan.FromHours(5)),
            VacancyTitle: "Payvandchi (Metan zavodi)",
            Language: "uz-Latn",
            Photo: photo,
            Signature: signature,
            ReferralChoices: ReferralSeeds.PdfChoices(answers.ReferralChannelId));

    private static int Betlar(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

        return document.PageCount;
    }

    /// <summary>
    /// PDF'ning ko'rinadigan matni — so'zlar chizilish tartibida, bitta bo'shliq bilan.
    /// </summary>
    /// <remarks>
    /// Testga yetarli soddalashtirish, umumiy PDF o'quvchi emas. MigraDoc har so'zni alohida
    /// <c>Tj</c> bilan chizadi (chiziqchadan keyin ham bo'ladi: «HR-» «F02» — ular qayta
    /// qo'shiladi). WinAnsi shriftidagi satr — Windows-1252 baytlari; Identity-H (<c>Type0</c>)
    /// shriftida (kirill, №, ☐ ☑) — ikki baytli glif raqamlari, ular shriftning
    /// <c>/ToUnicode</c> jadvali orqali harfga aylanadi. API testlari ham HR yuklagan PDF'ni shu bilan o'qiydi.
    /// </remarks>
    internal static string Matn(byte[] pdf) => string.Join(' ', Sahifalar(pdf));

    /// <inheritdoc cref="Matn"/>
    private static List<string> Sahifalar(byte[] pdf)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var ansi = Encoding.GetEncoding(1252);

        using var stream = new MemoryStream(pdf);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

        var pages = new List<string>();

        foreach (var page in document.Pages)
        {
            var text = new StringBuilder();
            var fonts = page.Resources.Elements.GetDictionary("/Font");
            Dictionary<int, string>? glyphs = null;

            foreach (var op in ContentReader.ReadContent(page).OfType<COperator>())
            {
                switch (op.OpCode.Name)
                {
                    case "Tf":
                        glyphs = Glyphs(fonts?.Elements.GetDictionary(((CName)op.Operands[0]).Name));
                        break;
                    case "Tj":
                        Add(Decode((CString)op.Operands[0]));
                        break;
                    case "TJ":
                        foreach (var part in ((CArray)op.Operands[0]).OfType<CString>())
                        {
                            Add(Decode(part));
                        }

                        break;
                }
            }

            string Decode(CString value)
            {
                if (glyphs is null)
                {
                    return ansi.GetString(value.Value.Select(c => (byte)c).ToArray());
                }

                var result = new StringBuilder();
                for (var index = 0; index + 1 < value.Value.Length; index += 2)
                {
                    result.Append(glyphs.GetValueOrDefault((value.Value[index] << 8) | value.Value[index + 1], "\uFFFD"));
                }

                return result.ToString();
            }

            void Add(string word)
            {
                var hyphenated = text.Length > 1 && text[^1] == '-' && !char.IsWhiteSpace(text[^2]);
                if (text.Length > 0 && !hyphenated)
                {
                    text.Append(' ');
                }

                text.Append(word);
            }

            pages.Add(Bosliq().Replace(text.ToString(), " ").Trim());
        }

        return pages;
    }

    /// <summary>Identity-H shriftining glif → harf jadvali; oddiy (WinAnsi) shrift — <c>null</c>.</summary>
    private static Dictionary<int, string>? Glyphs(PdfDictionary? font)
    {
        if (font is null || font.Elements.GetName("/Subtype") != "/Type0")
        {
            return null;
        }

        var map = new Dictionary<int, string>();
        if (font.Elements.GetDictionary("/ToUnicode")?.Stream is not { } cmap)
        {
            return map;
        }

        foreach (Match block in CmapBlock().Matches(Encoding.ASCII.GetString(cmap.UnfilteredValue)))
        {
            var tokens = HexToken().Matches(block.Groups[2].Value).Select(token => token.Groups[1].Value).ToList();

            if (block.Groups[1].Value == "bfchar")
            {
                for (var index = 0; index + 1 < tokens.Count; index += 2)
                {
                    map[Hex(tokens[index])] = Utf16(tokens[index + 1]);
                }

                continue;
            }

            for (var index = 0; index + 2 < tokens.Count; index += 3)
            {
                var first = Hex(tokens[index]);
                var target = Utf16(tokens[index + 2]);

                for (var glyph = first; glyph <= Hex(tokens[index + 1]); glyph++)
                {
                    map[glyph] = target[..^1] + (char)(target[^1] + (glyph - first));
                }
            }
        }

        return map;

        static int Hex(string value) => int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        static string Utf16(string value) => Encoding.BigEndianUnicode.GetString(Convert.FromHexString(value));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Bosliq();

    /// <summary><c>beginbfrange … endbfrange</c> va <c>beginbfchar … endbfchar</c> bo'limlari.</summary>
    [GeneratedRegex(@"begin(bfrange|bfchar)([\s\S]*?)end\1")]
    private static partial Regex CmapBlock();

    [GeneratedRegex("<([0-9A-Fa-f]+)>")]
    private static partial Regex HexToken();

    private static void Saqla(string name, byte[] pdf)
    {
        var folder = Environment.GetEnvironmentVariable("BONVI_PDF_OUT");

        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            File.WriteAllBytes(Path.Combine(folder, name), pdf);
        }
    }
}
