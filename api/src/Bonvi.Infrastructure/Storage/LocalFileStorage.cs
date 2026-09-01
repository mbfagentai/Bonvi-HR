using Bonvi.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bonvi.Infrastructure.Storage;

/// <summary>Fayl saqlash sozlamalari.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Fayllar saqlanadigan ildiz papka (Docker volume).</summary>
    public string RootPath { get; set; } = "/data/uploads";

    /// <summary>Bitta faylning eng katta hajmi (bayt). Standart: 25 MB.</summary>
    public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;
}

/// <summary>
/// Fayllarni lokal diskda (Docker volume) saqlaydi.
/// </summary>
/// <remarks>
/// Fayl nomi sifatida GUID ishlatiladi — asl nom alohida saqlanadi.
/// Sabab: foydalanuvchi yuklagan nom xavfsiz emas (<c>../../etc/passwd</c> kabi hujum).
/// Fayllar sana bo'yicha papkalarga taqsimlanadi — bitta papkada millionlab fayl to'planmaydi.
/// </remarks>
public sealed partial class LocalFileStorage : IFileStorage
{
    private readonly StorageOptions _options;
    private readonly ILogger<LocalFileStorage> _logger;

    // Manba-generator orqali yaratilgan log metodlari:
    // satr formatlash faqat log darajasi yoqilgan bo'lsa bajariladi.
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Fayl saqlandi: {Key} ({SizeBytes} bayt)")]
    private partial void LogFileSaved(string key, long sizeBytes);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Fayl o'chirildi: {Key}")]
    private partial void LogFileDeleted(string key);

    public LocalFileStorage(IOptions<StorageOptions> options, ILogger<LocalFileStorage> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var today = DateTimeOffset.UtcNow;
        var relativeDir = Path.Combine(today.Year.ToString("D4", null), today.Month.ToString("D2", null));
        var extension = Path.GetExtension(fileName);
        var key = Path.Combine(relativeDir, $"{Guid.CreateVersion7():N}{extension}");

        var fullPath = Path.Combine(_options.RootPath, key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var target = File.Create(fullPath);
        await content.CopyToAsync(target, cancellationToken).ConfigureAwait(false);

        LogFileSaved(key, target.Length);

        return new StoredFile(key, Path.GetFileName(fileName), contentType, target.Length);
    }

    public Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(key);

        if (fullPath is null || !File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(key);

        if (fullPath is null || !File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        File.Delete(fullPath);
        LogFileDeleted(key);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Kalitni to'liq manzilga aylantiradi va ildiz papkadan chiqib ketmasligini tekshiradi
    /// (path traversal hujumidan himoya).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ildiz oxiriga ajratgich MAJBURIY qo'shiladi.</b> Usiz tekshiruv
    /// «boshlanadimi» degan oddiy satr taqqoslash bo'lib qolardi va
    /// <c>/data/uploads</c> ildizi ostida <c>/data/uploads-eski/parol.txt</c>
    /// ham «ichkarida» deb hisoblanardi — u butunlay boshqa papka.
    /// </para>
    /// <para>
    /// <b>Bugun kalit ishonchli, ertaga shart emas.</b> Hozir kalitni
    /// <see cref="SaveAsync"/> ning o'zi UUID'dan yasaydi, ya'ni tashqaridan
    /// kelmaydi. Lekin bu sinf umumiy saqlash porti va kalit kelajakda
    /// so'rovdan (masalan eksport yoki tiklash oqimida) kelishi mumkin.
    /// Himoya aynan shunday kunga yozilgan.
    /// </para>
    /// </remarks>
    private string? ResolveSafePath(string key)
    {
        var root = Path.GetFullPath(_options.RootPath);

        // `TrimEnd` + qo'shish: `RootPath` sozlamada ajratgich bilan ham,
        // ajratgichsiz ham yozilishi mumkin va ikkalasi bir xil ishlashi
        // kerak.
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        var candidate = Path.GetFullPath(Path.Combine(root, key));

        return candidate.StartsWith(rootWithSeparator, StringComparison.Ordinal)
            ? candidate
            : null;
    }
}
