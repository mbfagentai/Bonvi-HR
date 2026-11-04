using System.IO.Compression;
using System.Text;
using Bonvi.Application.Blanks.Files;
using Bonvi.Infrastructure.Blanks;
using Bonvi.Infrastructure.Storage;
using SkiaSharp;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// The blank attachment inspector matrix (SPEC-blankalar §8, §15): what is accepted, what is
/// refused, and with which reason.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why here.</b> <see cref="BlankFileInspector"/> lives in <c>Bonvi.Infrastructure</c>, and this
/// is the only test project that sees it (same reason as <c>ScaledDocumentStoreTests</c>). No
/// PostgreSQL is needed: <see cref="BonviApiFactory"/> is not used.
/// </para>
/// <para>
/// <b>Every sample is built in code</b> — OOXML archives with <see cref="ZipArchive"/>, images with
/// SkiaSharp, other formats from their header bytes. No binary fixture is committed, so each case
/// states exactly which bytes make it pass or fail.
/// </para>
/// </remarks>
public sealed class BlankFileInspectorTests
{
    private const string DocxMain = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
    private const string XlsxMain = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
    private const string PptxMain = "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml";
    private const string DocmMain = "application/vnd.ms-word.document.macroEnabled.main+xml";
    private const string XlsmMain = "application/vnd.ms-excel.sheet.macroEnabled.main+xml";
    private const string PptmMain = "application/vnd.ms-powerpoint.presentation.macroEnabled.main+xml";
    private const string DotxMain = "application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml";

    private static readonly byte[] CfbHeader = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    private static readonly BlankFileInspector Inspector = new(new FileContentInspector());

    // ────────────────────────────────────────────────────────────
    //  Accepted
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Pdf_is_accepted()
        => AssertAccepted("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF\n"u8.ToArray(), BlankFileKind.Pdf, "application/pdf", ".pdf");

    [Fact]
    public void Jpeg_is_accepted()
        => AssertAccepted(TestImages.JpegYasa(32, 24, sifat: 80), BlankFileKind.Jpeg, "image/jpeg", ".jpg");

    [Fact]
    public void Png_is_accepted()
        => AssertAccepted(TestImages.PngYasa(32, 24), BlankFileKind.Png, "image/png", ".png");

    [Fact]
    public void Webp_is_accepted()
        => AssertAccepted(Webp(32, 24), BlankFileKind.Webp, "image/webp", ".webp");

    [Fact]
    public void Docx_is_accepted()
        => AssertAccepted(
            Ooxml("word/document.xml", DocxMain),
            BlankFileKind.Docx,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".docx");

    [Fact]
    public void Xlsx_is_accepted()
        => AssertAccepted(
            Ooxml("xl/workbook.xml", XlsxMain),
            BlankFileKind.Xlsx,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xlsx");

    [Fact]
    public void Pptx_is_accepted()
        => AssertAccepted(
            Ooxml("ppt/presentation.xml", PptxMain),
            BlankFileKind.Pptx,
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".pptx");

    /// <summary>OPC part names are case-insensitive: <c>/Word/Document.xml</c> names <c>word/document.xml</c>.</summary>
    [Fact]
    public void Docx_part_name_matches_case_insensitively()
    {
        var contentTypes = ContentTypes(("/Word/Document.xml", DocxMain));
        var archive = Zip(("[Content_Types].xml", Utf8(contentTypes)), ("word/document.xml", Utf8("<w:document/>")));

        AssertAccepted(archive, BlankFileKind.Docx, BlankFileKinds.ContentTypeFor(BlankFileKind.Docx), ".docx");
    }

    // ────────────────────────────────────────────────────────────
    //  Macros
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Docm_with_vba_project_is_rejected_as_macro()
        => AssertRejected(
            Ooxml("word/document.xml", DocmMain, ("word/vbaProject.bin", CfbHeader)),
            BlankFileRejection.MacroEnabled);

    /// <summary>The main type alone is enough — even without the VBA part.</summary>
    [Theory]
    [InlineData("xl/workbook.xml", XlsmMain)]
    [InlineData("ppt/presentation.xml", PptmMain)]
    [InlineData("word/document.xml", DocmMain)]
    public void Macro_enabled_main_type_is_rejected(string mainPart, string mainType)
        => AssertRejected(Ooxml(mainPart, mainType), BlankFileRejection.MacroEnabled);

    /// <summary>A .docx main type does not launder a VBA project hidden in the archive.</summary>
    [Fact]
    public void Docx_carrying_vba_project_is_rejected_as_macro()
        => AssertRejected(
            Ooxml("word/document.xml", DocxMain, ("word/vbaProject.bin", CfbHeader)),
            BlankFileRejection.MacroEnabled);

    [Fact]
    public void Vba_content_type_default_is_rejected_as_macro()
    {
        var contentTypes = ContentTypesWithDefaults(
            [("/word/document.xml", DocxMain)],
            [("bin", "application/vnd.ms-office.vbaProject")]);
        var archive = Zip(("[Content_Types].xml", Utf8(contentTypes)), ("word/document.xml", Utf8("<w:document/>")));

        AssertRejected(archive, BlankFileRejection.MacroEnabled);
    }

    // ────────────────────────────────────────────────────────────
    //  Legacy / encrypted (OLE compound file)
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Legacy .doc/.xls and password-protected .docx/.xlsx share the CFB container: both are refused
    /// with the «save as .docx/.xlsx» reason.
    /// </summary>
    [Theory]
    [InlineData("legacy .doc")]
    [InlineData("encrypted .docx")]
    public void Ole_compound_file_is_rejected_as_legacy_or_encrypted(string label)
    {
        var bytes = new byte[4096];
        CfbHeader.CopyTo(bytes, 0);
        Encoding.Unicode.GetBytes(label).CopyTo(bytes, 512);

        AssertRejected(bytes, BlankFileRejection.LegacyOrEncrypted);
    }

    // ────────────────────────────────────────────────────────────
    //  Zip bombs
    // ────────────────────────────────────────────────────────────

    /// <summary>Three 40 MB parts of zeros: each under the per-entry cap, together over 100 MB.</summary>
    [Fact]
    public void Archive_over_total_uncompressed_cap_is_rejected()
        => AssertRejected(
            Ooxml(
                "word/document.xml",
                DocxMain,
                ("word/media/a.bin", Zeros(40)),
                ("word/media/b.bin", Zeros(40)),
                ("word/media/c.bin", Zeros(40))),
            BlankFileRejection.ArchiveTooLarge);

    [Fact]
    public void Entry_over_single_entry_cap_is_rejected()
        => AssertRejected(
            Ooxml("word/document.xml", DocxMain, ("word/media/big.bin", Zeros(51))),
            BlankFileRejection.ArchiveTooLarge);

    [Fact]
    public void Archive_with_too_many_entries_is_rejected()
    {
        var extra = Enumerable.Range(0, BlankFileInspector.MaxEntries)
            .Select(i => ($"word/media/{i}.txt", new byte[] { 1 }))
            .ToArray();

        AssertRejected(Ooxml("word/document.xml", DocxMain, extra), BlankFileRejection.ArchiveTooLarge);
    }

    /// <summary>The single part that is inflated has its own cap — an oversized one is never parsed.</summary>
    [Fact]
    public void Oversized_content_types_part_is_rejected()
    {
        var padding = new string(' ', BlankFileInspector.MaxContentTypesBytes);
        var contentTypes = ContentTypes(("/word/document.xml", DocxMain)).Replace("</Types>", padding + "</Types>", StringComparison.Ordinal);
        var archive = Zip(("[Content_Types].xml", Utf8(contentTypes)), ("word/document.xml", Utf8("<w:document/>")));

        AssertRejected(archive, BlankFileRejection.Unrecognized);
    }

    // ────────────────────────────────────────────────────────────
    //  Unrecognized
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Renamed_executable_is_rejected()
    {
        var exe = new byte[1024];
        "MZ"u8.CopyTo(exe);
        "PE\0\0"u8.CopyTo(exe.AsSpan(0x80));
        exe[0x3C] = 0x80;

        AssertRejected(exe, BlankFileRejection.Unrecognized);
    }

    [Fact]
    public void Plain_zip_without_content_types_is_rejected()
        => AssertRejected(Zip(("readme.txt", Utf8("hello"))), BlankFileRejection.Unrecognized);

    /// <summary>A JAR/APK-like archive with a fake extension still has no OOXML main part.</summary>
    [Fact]
    public void Zip_with_content_types_but_no_main_part_type_is_rejected()
    {
        var contentTypes = ContentTypes(("/META-INF/MANIFEST.MF", "text/plain"));
        AssertRejected(
            Zip(("[Content_Types].xml", Utf8(contentTypes)), ("META-INF/MANIFEST.MF", Utf8("Main-Class: X"))),
            BlankFileRejection.Unrecognized);
    }

    [Fact]
    public void Declared_main_part_missing_from_archive_is_rejected()
    {
        var contentTypes = ContentTypes(("/word/document.xml", DocxMain));
        AssertRejected(Zip(("[Content_Types].xml", Utf8(contentTypes))), BlankFileRejection.Unrecognized);
    }

    [Fact]
    public void Two_different_main_types_are_rejected()
    {
        var contentTypes = ContentTypes(("/word/document.xml", DocxMain), ("/xl/workbook.xml", XlsxMain));
        var archive = Zip(
            ("[Content_Types].xml", Utf8(contentTypes)),
            ("word/document.xml", Utf8("<w:document/>")),
            ("xl/workbook.xml", Utf8("<workbook/>")));

        AssertRejected(archive, BlankFileRejection.Unrecognized);
    }

    [Fact]
    public void Word_template_is_rejected()
        => AssertRejected(Ooxml("word/document.xml", DotxMain), BlankFileRejection.Unrecognized);

    /// <summary>DTDs are prohibited — no entity expansion, no external resolution.</summary>
    [Fact]
    public void Content_types_with_doctype_is_rejected()
    {
        var contentTypes = "<?xml version=\"1.0\"?><!DOCTYPE Types [<!ENTITY x \"y\">]>"
            + ContentTypes(("/word/document.xml", DocxMain)).Replace("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", string.Empty, StringComparison.Ordinal);
        var archive = Zip(("[Content_Types].xml", Utf8(contentTypes)), ("word/document.xml", Utf8("<w:document/>")));

        AssertRejected(archive, BlankFileRejection.Unrecognized);
    }

    [Fact]
    public void Truncated_zip_is_rejected()
    {
        var full = Ooxml("word/document.xml", DocxMain);
        AssertRejected(full[..(full.Length / 2)], BlankFileRejection.Unrecognized);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 })]
    [InlineData(new byte[] { 0x7F, 0x45, 0x4C, 0x46, 0x02, 0x01, 0x01, 0x00 })]
    public void Empty_or_unknown_bytes_are_rejected(byte[] bytes)
        => AssertRejected(bytes, BlankFileRejection.Unrecognized);

    // ────────────────────────────────────────────────────────────
    //  accept groups
    // ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(BlankFileKind.Pdf, BlankFileAccept.Pdf, true)]
    [InlineData(BlankFileKind.Pdf, BlankFileAccept.Image, false)]
    [InlineData(BlankFileKind.Webp, BlankFileAccept.Image, true)]
    [InlineData(BlankFileKind.Jpeg, BlankFileAccept.Pdf | BlankFileAccept.Office, false)]
    [InlineData(BlankFileKind.Xlsx, BlankFileAccept.Office, true)]
    [InlineData(BlankFileKind.Docx, BlankFileAccept.Pdf | BlankFileAccept.Image, false)]
    [InlineData(BlankFileKind.Pptx, BlankFileAccept.Any, true)]
    [InlineData(BlankFileKind.Png, BlankFileAccept.None, false)]
    public void Accept_groups_match_kinds(BlankFileKind kind, BlankFileAccept accept, bool expected)
        => BlankFileKinds.IsAccepted(kind, accept).ShouldBe(expected);

    // ────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────

    private static void AssertAccepted(byte[] bytes, BlankFileKind kind, string contentType, string extension)
    {
        var result = Inspector.Inspect(bytes);

        result.Rejection.ShouldBeNull();
        result.IsAccepted.ShouldBeTrue();
        result.Kind.ShouldBe(kind);
        result.ContentType.ShouldBe(contentType);
        result.Extension.ShouldBe(extension);
    }

    private static void AssertRejected(byte[] bytes, BlankFileRejection rejection)
    {
        var result = Inspector.Inspect(bytes);

        result.IsAccepted.ShouldBeFalse();
        result.Kind.ShouldBeNull();
        result.ContentType.ShouldBeNull();
        result.Extension.ShouldBeNull();
        result.Rejection.ShouldBe(rejection);
    }

    /// <summary>A minimal OOXML package: content types, package rels, the main part and any extra parts.</summary>
    private static byte[] Ooxml(string mainPart, string mainType, params (string Name, byte[] Data)[] extra)
    {
        var parts = new List<(string, byte[])>
        {
            ("[Content_Types].xml", Utf8(ContentTypes(("/" + mainPart, mainType)))),
            ("_rels/.rels", Utf8(
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + $"<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"{mainPart}\"/>"
                + "</Relationships>")),
            (mainPart, Utf8("<root/>")),
        };
        parts.AddRange(extra);

        return Zip([.. parts]);
    }

    private static string ContentTypes(params (string PartName, string ContentType)[] overrides)
        => ContentTypesWithDefaults(overrides, []);

    private static string ContentTypesWithDefaults(
        (string PartName, string ContentType)[] overrides,
        (string Extension, string ContentType)[] defaults)
    {
        var xml = new StringBuilder()
            .Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>")
            .Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">")
            .Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>")
            .Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");

        foreach (var (extension, contentType) in defaults)
        {
            xml.Append("<Default Extension=\"").Append(extension).Append("\" ContentType=\"").Append(contentType).Append("\"/>");
        }

        foreach (var (partName, contentType) in overrides)
        {
            xml.Append("<Override PartName=\"").Append(partName).Append("\" ContentType=\"").Append(contentType).Append("\"/>");
        }

        return xml.Append("</Types>").ToString();
    }

    private static byte[] Zip(params (string Name, byte[] Data)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in entries)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
                using var stream = entry.Open();
                stream.Write(data);
            }
        }

        return buffer.ToArray();
    }

    private static byte[] Zeros(int megabytes) => new byte[megabytes * 1024 * 1024];

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static byte[] Webp(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 80);
        return data.ToArray();
    }
}
