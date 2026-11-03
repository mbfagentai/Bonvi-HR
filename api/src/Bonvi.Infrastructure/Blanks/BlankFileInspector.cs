using System.IO.Compression;
using System.Xml;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Blanks.Files;

namespace Bonvi.Infrastructure.Blanks;

/// <summary>
/// <see cref="IBlankFileInspector"/> implementation (SPEC-blankalar §8).
/// </summary>
/// <remarks>
/// <para>
/// <b>Order of checks.</b> (1) OLE compound-file signature → legacy or encrypted Office, refused
/// before anything else looks at it. (2) ZIP local-file signature → OOXML path. (3) Everything else
/// goes to the existing <see cref="IFileContentInspector"/> (PDF signature, image decoder); whatever
/// it does not recognise — executables included — is refused.
/// </para>
/// <para>
/// <b>OOXML without extracting.</b> <see cref="ZipArchive"/> in read mode parses the central
/// directory only; the entry count and the DECLARED uncompressed sizes are checked against the caps
/// before a single byte is inflated. Exactly one part is ever inflated — <c>[Content_Types].xml</c> —
/// and that read is capped independently of what the archive declares, so a lying header cannot
/// make it unpack more. Nothing touches the disk.
/// </para>
/// <para>
/// <b>Type comes from the main part's content type</b>, not from the extension or from which folders
/// exist: a .docm renamed to .docx still declares <c>macroEnabled</c> and is caught; a JAR or APK has
/// no <c>[Content_Types].xml</c> and is refused.
/// </para>
/// <para>Stateless — register as a singleton.</para>
/// </remarks>
public sealed class BlankFileInspector : IBlankFileInspector
{
    /// <summary>Most entries an OOXML archive may have. Real documents have tens, rarely hundreds.</summary>
    public const int MaxEntries = 2_000;

    /// <summary>Most the whole archive may declare when unpacked — 100 MB.</summary>
    public const long MaxTotalUncompressedBytes = 100L * 1024 * 1024;

    /// <summary>Most a single entry may declare when unpacked — 50 MB.</summary>
    public const long MaxEntryUncompressedBytes = 50L * 1024 * 1024;

    /// <summary>
    /// Most bytes read out of <c>[Content_Types].xml</c> — 1 MB. A real one is a few kilobytes.
    /// </summary>
    public const int MaxContentTypesBytes = 1024 * 1024;

    private const string ContentTypesPartName = "[Content_Types].xml";
    private const string VbaProjectPartName = "vbaProject.bin";
    private const string VbaProjectContentType = "application/vnd.ms-office.vbaProject";

    private static readonly byte[] CfbSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    private static readonly Dictionary<string, BlankFileKind> MainPartKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"] = BlankFileKind.Docx,
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"] = BlankFileKind.Xlsx,
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml"] = BlankFileKind.Pptx,
    };

    private readonly IFileContentInspector _inner;

    public BlankFileInspector(IFileContentInspector inner)
    {
        _inner = inner;
    }

    public BlankFileInspection Inspect(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length == 0)
        {
            return BlankFileInspection.Rejected(BlankFileRejection.Unrecognized);
        }

        if (StartsWith(content, CfbSignature))
        {
            return BlankFileInspection.Rejected(BlankFileRejection.LegacyOrEncrypted);
        }

        if (StartsWith(content, ZipSignature))
        {
            return InspectOoxml(content);
        }

        return _inner.Detect(content) switch
        {
            DetectedFileKind.Pdf => BlankFileInspection.Accepted(BlankFileKind.Pdf),
            DetectedFileKind.Jpeg => BlankFileInspection.Accepted(BlankFileKind.Jpeg),
            DetectedFileKind.Png => BlankFileInspection.Accepted(BlankFileKind.Png),
            DetectedFileKind.Webp => BlankFileInspection.Accepted(BlankFileKind.Webp),
            _ => BlankFileInspection.Rejected(BlankFileRejection.Unrecognized),
        };
    }

    private static BlankFileInspection InspectOoxml(byte[] content)
    {
        try
        {
            using var archive = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
            return InspectArchive(archive);
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or NotSupportedException or IOException)
        {
            // Damaged ZIP, encrypted or unsupported entry, malformed XML: not a document we accept.
            return BlankFileInspection.Rejected(BlankFileRejection.Unrecognized);
        }
    }

    private static BlankFileInspection InspectArchive(ZipArchive archive)
    {
        // Central directory only — nothing is inflated until these caps pass.
        if (archive.Entries.Count > MaxEntries)
        {
            return BlankFileInspection.Rejected(BlankFileRejection.ArchiveTooLarge);
        }

        long total = 0;
        ZipArchiveEntry? contentTypesEntry = null;
        var hasVbaProject = false;

        foreach (var entry in archive.Entries)
        {
            if (entry.Length > MaxEntryUncompressedBytes)
            {
                return BlankFileInspection.Rejected(BlankFileRejection.ArchiveTooLarge);
            }

            total += entry.Length;
            if (total > MaxTotalUncompressedBytes)
            {
                return BlankFileInspection.Rejected(BlankFileRejection.ArchiveTooLarge);
            }

            if (string.Equals(entry.FullName, ContentTypesPartName, StringComparison.OrdinalIgnoreCase))
            {
                contentTypesEntry = entry;
            }

            if (entry.Name.Equals(VbaProjectPartName, StringComparison.OrdinalIgnoreCase))
            {
                hasVbaProject = true;
            }
        }

        if (hasVbaProject)
        {
            return BlankFileInspection.Rejected(BlankFileRejection.MacroEnabled);
        }

        if (contentTypesEntry is null || contentTypesEntry.Length > MaxContentTypesBytes)
        {
            return BlankFileInspection.Rejected(BlankFileRejection.Unrecognized);
        }

        var contentTypes = ReadContentTypes(contentTypesEntry);
        if (contentTypes is null)
        {
            return BlankFileInspection.Rejected(BlankFileRejection.Unrecognized);
        }

        if (contentTypes.Any(ct => IsMacroContentType(ct.ContentType)))
        {
            return BlankFileInspection.Rejected(BlankFileRejection.MacroEnabled);
        }

        // Exactly one kind of main part, and that part must really be in the archive.
        var mains = contentTypes
            .Where(ct => ct.PartName is not null && MainPartKinds.ContainsKey(ct.ContentType))
            .ToList();
        var kinds = mains.Select(ct => MainPartKinds[ct.ContentType]).Distinct().ToList();

        if (kinds.Count != 1 || !mains.TrueForAll(ct => PartExists(archive, ct.PartName!)))
        {
            return BlankFileInspection.Rejected(BlankFileRejection.Unrecognized);
        }

        return BlankFileInspection.Accepted(kinds[0]);
    }

    /// <summary>
    /// Reads <c>Default</c>/<c>Override</c> content types. The inflated bytes are capped by
    /// <see cref="MaxContentTypesBytes"/> whatever the archive declares; over the cap → <c>null</c>.
    /// </summary>
    private static List<ContentTypeEntry>? ReadContentTypes(ZipArchiveEntry entry)
    {
        var buffer = new byte[MaxContentTypesBytes + 1];
        var read = 0;

        using (var stream = entry.Open())
        {
            int n;
            while (read < buffer.Length && (n = stream.Read(buffer, read, buffer.Length - read)) > 0)
            {
                read += n;
            }
        }

        if (read > MaxContentTypesBytes)
        {
            return null;
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxContentTypesBytes,
            IgnoreComments = true,
            IgnoreWhitespace = true,
        };

        var result = new List<ContentTypeEntry>();
        using var xml = XmlReader.Create(new MemoryStream(buffer, 0, read, writable: false), settings);

        while (xml.Read())
        {
            if (xml.NodeType != XmlNodeType.Element || xml.LocalName is not ("Default" or "Override"))
            {
                continue;
            }

            var contentType = xml.GetAttribute("ContentType");
            if (string.IsNullOrWhiteSpace(contentType))
            {
                continue;
            }

            var partName = xml.LocalName == "Override" ? xml.GetAttribute("PartName") : null;
            result.Add(new ContentTypeEntry(contentType.Trim(), partName));
        }

        return result;
    }

    private static bool IsMacroContentType(string contentType)
        => contentType.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals(VbaProjectContentType, StringComparison.OrdinalIgnoreCase);

    /// <summary>OPC part name (<c>/word/document.xml</c>, case-insensitive, may be %-encoded) → ZIP entry.</summary>
    private static bool PartExists(ZipArchive archive, string partName)
    {
        var entryName = Uri.UnescapeDataString(partName).TrimStart('/');
        return archive.Entries.Any(e => string.Equals(e.FullName, entryName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool StartsWith(byte[] content, byte[] prefix)
        => content.Length >= prefix.Length && content.AsSpan(0, prefix.Length).SequenceEqual(prefix);

    private sealed record ContentTypeEntry(string ContentType, string? PartName);
}
