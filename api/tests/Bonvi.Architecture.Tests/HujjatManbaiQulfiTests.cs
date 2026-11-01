using System.Reflection;
using System.Text.RegularExpressions;
using Bonvi.Domain.Common;
using NetArchTest.Rules;

namespace Bonvi.Architecture.Tests;

/// <summary>
/// Hujjat va blankalarning yagona manbai — HRMentor (SPEC-hujjat-manbai §1, §3). Bu tizim o'zining
/// hujjat SHABLONINI yaratmaydi: blanka, nizom, anketa va qo'llanma avval HRMentor ga yuklanadi va
/// shu yerdan olinadi. Hozir dasturda qolganlari — qarz (§4, D1–D5): ro'yxatda turadi va faqat
/// kamayadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Qanday ishlaydi.</b> Har bir tekshiruv topilganini ro'yxat bilan SOLISHTIRADI: yangi PDF
/// shabloni yoki shablon fayli qo'shilsa — yiqiladi («avval HRMentor ga»); qarz yo'qolib, ro'yxatda
/// qolsa ham yiqiladi (ro'yxat faqat qisqaradi, keyingi odam unga yashirincha qaytib yozolmaydi).
/// </para>
/// <para>
/// <b>Fayllar ombor ildizidan o'qiladi</b> (<c>web/</c>, <c>app/</c> ham kerak).
/// <c>./bonvi test</c> ildizni <c>/repo</c> ga faqat o'qish uchun ulaydi va
/// <c>BONVI_REPO_DIR</c> beradi; xostda esa ildiz test papkasidan yuqoriga qarab topiladi.
/// </para>
/// </remarks>
public sealed class HujjatManbaiQulfiTests
{
    /// <summary>
    /// PDF kutubxonasiga (PdfSharp / MigraDoc) bog'lanishi DOIMIY ruxsat etilgan turlar:
    /// HRMentor da e'lon qilingan ta'rifni chop etuvchi va shriftlar.
    /// </summary>
    private static readonly string[] PdfDoimiy = ["DynamicFormPdfRenderer", "BundledFontResolver"];

    /// <summary>
    /// Qarz D1: dasturga yozilgan HR-F02 v3.1 anketasining PDF i. HRMentor ta'rifi ishlamay qolsa
    /// (S2) zaxira; HR-F02 HRMentor da 60 kun xizmat qilgach olib tashlash hal qilinadi.
    /// </summary>
    private static readonly string[] PdfQarz = ["QuestionnairePdfRenderer"];

    /// <summary>Qarz D5: qo'llanma PDF lari — HRMentor ga yuklanib, shu saytga biriktirilguncha.</summary>
    private static readonly string[] ShablonFaylQarz =
    [
        "web/public/qollanma/bonvi-hr-qollanma-uz-Cyrl.pdf",
        "web/public/qollanma/bonvi-hr-qollanma-uz-Latn.pdf",
    ];

    /// <summary>Qarz D1–D3: dasturdagi HR-F02 manbalari (urug', matnlar, PDF matnlari).</summary>
    private static readonly string[] IchkiManbaQarzTurlar = ["HrF02Seed", "HrF02TextCatalog", "QuestionnairePdfTexts"];

    /// <summary>Qarz D4: botning anketa savollari — bot HRMentor ta'rifini so'ramaguncha.</summary>
    private static readonly string[] IchkiManbaQarzFayllar = ["api/src/Bonvi.Bot/Conversation/BotTexts.cs"];

    /// <summary>Shablon hisoblanadigan fayllar (§3.2).</summary>
    private static readonly HashSet<string> ShablonKengaytmalar = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".dotx", ".odt", ".pdf", ".rtf", ".xls", ".xlsx", ".xltx",
    };

    /// <summary>Qaraladigan papkalar — manba va brauzerga beriladigan fayllar.</summary>
    private static readonly string[] ShablonPapkalar = ["api/src", "web/src", "web/public", "app/src", "app/public"];

    /// <summary>Yig'ish va paket papkalari — ular manba emas.</summary>
    private static readonly HashSet<string> OtkazibYuboriladi = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", "dist", "artifacts",
    };

    /// <summary>Kodda (izohda emas) PDF kutubxonasining nomlar fazosi: <c>using PdfSharp.Pdf;</c>, <c>MigraDoc.Rendering.X</c>.</summary>
    private static readonly Regex PdfKodi = new(@"\b(PdfSharp|MigraDoc)\.", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Assembly[] Yigmalar =
    [
        typeof(Entity).Assembly,
        Bonvi.Application.Common.DependencyInjection.Assembly,
        Bonvi.Infrastructure.DependencyInjection.Assembly,
        typeof(Program).Assembly,
    ];

    [Fact]
    public void PDF_kutubxonasiga_faqat_royxatdagi_turlar_boglanadi()
    {
        var topildi = Types.InAssemblies(Yigmalar)
            .That()
            .HaveDependencyOnAny("PdfSharp", "MigraDoc")
            .GetTypes()
            .Select(TashqiTur)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Solishtir(
            topildi,
            [.. PdfDoimiy, .. PdfQarz],
            "PDF shabloni dasturda yaratilmaydi — HRMentor da tuziladi (SPEC-hujjat-manbai §3.1)");
    }

    [Fact]
    public void PDF_kutubxonasi_manbada_faqat_royxatdagi_turlarning_fayllarida()
    {
        // Reflection faqat to'rt yig'mani ko'radi; bu esa Bot, DeviceAgent va boshqa loyihalarni ham.
        var root = Ildiz();
        var topildi = Fayllar(Path.Combine(root, "api", "src"))
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadLines(path)
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Any(PdfKodi.IsMatch))
            .Select(path => Path.GetFileNameWithoutExtension(path).Split('.')[0])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Solishtir(
            topildi,
            [.. PdfDoimiy, .. PdfQarz],
            "PdfSharp / MigraDoc faqat ro'yxatdagi turlarda (SPEC-hujjat-manbai §3.1)");
    }

    [Fact]
    public void Shablon_fayllari_faqat_qarz_royxatida()
    {
        var root = Ildiz();
        var topildi = ShablonPapkalar
            .Select(folder => Path.Combine(root, folder))
            .Where(Directory.Exists)
            .SelectMany(Fayllar)
            .Where(path => ShablonKengaytmalar.Contains(Path.GetExtension(path)))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        Solishtir(
            topildi,
            ShablonFaylQarz,
            "Hujjat yoki blanka fayli dasturga qo'yilmaydi — HRMentor ga yuklanadi va shu saytga biriktiriladi (SPEC-hujjat-manbai §3.2)");
    }

    [Fact]
    public void Dasturdagi_anketa_manbalari_royxatdagicha_hali_bor()
    {
        // Ratchet (§3.4): yo'qolgan qarz ro'yxatdan ham o'chiriladi — ro'yxat faqat qisqaradi.
        var turlar = Yigmalar
            .SelectMany(assembly => assembly.GetTypes())
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);
        var root = Ildiz();

        var yoq = IchkiManbaQarzTurlar.Where(name => !turlar.Contains(name))
            .Concat(IchkiManbaQarzFayllar.Where(path => !File.Exists(Path.Combine(root, path))))
            .ToList();

        yoq.ShouldBeEmpty(
            "Qarz yopildi — uni HujjatManbaiQulfiTests ro'yxatidan va SPEC-hujjat-manbai §4 jadvalidan olib tashlang: "
            + string.Join(", ", yoq));
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Topilgan va ro'yxat bir xil bo'lishi shart: ortiqchasi — yangi shablon, yetishmagani — yopilgan qarz.</summary>
    private static void Solishtir(IReadOnlyList<string> topildi, IReadOnlyList<string> royxat, string qoida)
    {
        var yangi = topildi.Except(royxat, StringComparer.Ordinal).ToList();
        var yopilgan = royxat.Except(topildi, StringComparer.Ordinal).ToList();

        yangi.ShouldBeEmpty($"{qoida}. Ro'yxatda yo'q: {string.Join(", ", yangi)}");
        yopilgan.ShouldBeEmpty(
            $"Ro'yxatda bor, lekin endi topilmadi — qarz yopilgan bo'lsa, uni ro'yxatdan olib tashlang: {string.Join(", ", yopilgan)}");
    }

    /// <summary>Ichki va kompilyator yasagan turlar (<c>&lt;&gt;c</c>, async holat mashinasi) — eng tashqi turining nomi.</summary>
    private static string TashqiTur(Type type)
    {
        while (type.DeclaringType is { } outer)
        {
            type = outer;
        }

        return type.Name;
    }

    private static IEnumerable<string> Fayllar(string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            yield return file;
        }

        foreach (var sub in Directory.EnumerateDirectories(folder))
        {
            if (OtkazibYuboriladi.Contains(Path.GetFileName(sub)))
            {
                continue;
            }

            foreach (var file in Fayllar(sub))
            {
                yield return file;
            }
        }
    }

    /// <summary>Ombor ildizi: <c>BONVI_REPO_DIR</c>, aks holda test papkasidan yuqoriga.</summary>
    private static string Ildiz()
    {
        var fromEnv = Environment.GetEnvironmentVariable("BONVI_REPO_DIR");
        if (!string.IsNullOrWhiteSpace(fromEnv) && Ildizmi(fromEnv))
        {
            return fromEnv;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Ildizmi(dir.FullName))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException(
            "Ombor ildizi topilmadi (api/src va web/ birga). Testni ./bonvi test bilan yurgizing — u ildizni /repo ga ulaydi.");
    }

    private static bool Ildizmi(string path)
        => Directory.Exists(Path.Combine(path, "api", "src")) && Directory.Exists(Path.Combine(path, "web"));
}
