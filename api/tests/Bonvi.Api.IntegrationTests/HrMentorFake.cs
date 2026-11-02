using System.Collections.Concurrent;
using System.Security.Cryptography;
using Bonvi.Application.Blanks;
using Bonvi.Domain.Common;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// HRMentor o'rnida — testlar holatini o'zi beradi va chaqiruvlarni sanaydi
/// (SPEC-blankalar-hrmentor §8: «sync job against a fake HRMentor»).
/// </summary>
internal sealed class HrMentorFake : IHrMentorClient
{
    private readonly ConcurrentDictionary<Guid, byte[]> _files = new();

    /// <summary>
    /// HRMentor HR-F02 ni biladimi: katalog qabul qilingach — ha, <see cref="Forget"/> dan keyin — yo'q.
    /// Haqiqiy HRMentor ham shunday: forma yo'q bo'lsa matnlar va ta'rif 404, bor bo'lsa — 200.
    /// </summary>
    private volatile bool _formKnown;

    /// <summary>Hamma chaqiruv shu xato bilan qaytadi (HRMentor o'chiq); <c>null</c> — ishlaydi.</summary>
    public Error? Down { get; set; }

    /// <summary>
    /// <c>GET …/texts</c> javobi; <c>null</c> — HR hali saqlamagan (reviziya 0). Forma noma'lum
    /// bo'lsa (katalog hali yo'q yoki <see cref="Forget"/>) — 404.
    /// </summary>
    public string? ServedTexts { get; set; }

    /// <summary>Yuborilgan kataloglar.</summary>
    public ConcurrentQueue<string> PushedCatalogs { get; } = new();

    /// <summary>Yuborilgan profillar (SPEC-anketa-konstruktori §3).</summary>
    public ConcurrentQueue<string> PushedProfiles { get; } = new();

    /// <summary><c>GET …/definition</c> javobi; <c>null</c> yoki forma noma'lum — 404.</summary>
    public string? PublishedDefinition { get; set; }

    /// <summary><c>GET …/draft</c> javobi; <c>null</c> — 404.</summary>
    public string? DraftDefinition { get; set; }

    /// <summary>Yuklab olingan fayllar soni.</summary>
    public int Downloads => _downloads;

    /// <summary>Oxirgi chaqiruvdagi ulanish (manzil va kalit Sozlamalardan kelganini ko'rish uchun).</summary>
    public HrMentorConnection? LastConnection { get; private set; }

    /// <summary>Ro'yxat — joriy versiyalar.</summary>
    public List<HrMentorBlankItem> Blanks { get; } = [];

    /// <summary>
    /// <c>GET /platform/documents</c> javobi (SPEC-hujjat-manbai §2): <c>null</c> — platforma
    /// <see cref="Blanks"/> ni beradi; aks holda shu xato. Odatiy — kalit hali bog'lanmagan
    /// (<see cref="HrMentorErrors.PlatformNotReady"/>), shuning uchun eski testlar eski ro'yxatda yuradi.
    /// </summary>
    public Error? PlatformAnswer { get; set; } = HrMentorErrors.PlatformNotReady;

    /// <summary>
    /// <c>true</c> — platformaga hali hech narsa biriktirilmagan: <c>/platform/documents</c> bo'sh
    /// ro'yxat beradi, eski <c>/blanks</c> esa <see cref="Blanks"/> ni berishda davom etadi.
    /// </summary>
    public bool PlatformEmpty { get; set; }

    /// <summary><c>PUT /platform/holdings</c> javobi: <c>null</c> — qabul qilindi; aks holda shu xato.</summary>
    public Error? HoldingsAnswer { get; set; }

    /// <summary>Platforma yo'li orqali yuklangan fayllar soni.</summary>
    public int PlatformDownloads => _platformDownloads;

    /// <summary>Yuborilgan holdings to'plamlari — har yurishda bittadan.</summary>
    public ConcurrentQueue<IReadOnlyList<HrMentorHolding>> ReportedHoldings { get; } = new();

    private int _downloads;
    private int _platformDownloads;

    /// <summary>Blanka qo'shadi yoki yangi versiyasini qo'yadi; <paramref name="sha256"/> — ataylab noto'g'ri qilish uchun.</summary>
    public HrMentorBlankItem Publish(Guid documentId, string code, string title, int versionNo, byte[] content, string? sha256 = null)
    {
        var item = new HrMentorBlankItem(
            documentId,
            code,
            title,
            Guid.NewGuid(),
            versionNo,
            DateTimeOffset.UtcNow,
            $"{code}.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            content.LongLength,
            sha256 ?? Convert.ToHexStringLower(SHA256.HashData(content)));

        Blanks.RemoveAll(blank => blank.DocumentId == documentId);
        Blanks.Add(item);
        _files[documentId] = content;

        return item;
    }

    public void Remove(Guid documentId)
    {
        Blanks.RemoveAll(blank => blank.DocumentId == documentId);
        _files.TryRemove(documentId, out _);
    }

    public Task<Result> PushCatalogAsync(HrMentorConnection connection, string formCode, string catalogJson, CancellationToken cancellationToken)
    {
        LastConnection = connection;
        if (Down is { } error)
        {
            return Task.FromResult(Result.Failure(error));
        }

        PushedCatalogs.Enqueue(catalogJson);
        _formKnown = true;
        return Task.FromResult(Result.Success());
    }

    public Task<Result> PushProfileAsync(HrMentorConnection connection, string formCode, string profileJson, CancellationToken cancellationToken)
    {
        LastConnection = connection;
        if (Down is { } error)
        {
            return Task.FromResult(Result.Failure(error));
        }

        PushedProfiles.Enqueue(profileJson);
        return Task.FromResult(Result.Success());
    }

    public Task<Result<string?>> GetPublishedDefinitionAsync(HrMentorConnection connection, string formCode, CancellationToken cancellationToken)
        => Task.FromResult(Down is { } error ? Result.Failure<string?>(error) : Result.Success(_formKnown ? PublishedDefinition : null));

    public Task<Result<string?>> GetDraftDefinitionAsync(HrMentorConnection connection, string formCode, CancellationToken cancellationToken)
        => Task.FromResult(Down is { } error ? Result.Failure<string?>(error) : Result.Success(DraftDefinition));

    public Task<Result<string?>> GetServedTextsAsync(HrMentorConnection connection, string formCode, CancellationToken cancellationToken)
        => Task.FromResult(Down is { } error
            ? Result.Failure<string?>(error)
            : Result.Success(_formKnown ? ServedTexts ?? UntouchedTexts : null));

    /// <summary>HRMentor bazasi tiklandi yoki forma qayta ulandi: HR-F02 endi unda yo'q.</summary>
    public void Forget() => _formKnown = false;

    /// <summary>Forma bor, HR hali hech narsa saqlamagan (HRMentor SPEC §18.5, reviziya 0).</summary>
    private const string UntouchedTexts = """{"code":"HR-F02","revision":0,"texts":{},"lists":{}}""";

    public Task<Result<IReadOnlyList<HrMentorBlankItem>>> ListBlanksAsync(HrMentorConnection connection, CancellationToken cancellationToken)
        => Task.FromResult(Down is { } error
            ? Result.Failure<IReadOnlyList<HrMentorBlankItem>>(error)
            : Result.Success<IReadOnlyList<HrMentorBlankItem>>([.. Blanks]));

    public Task<Result<byte[]>> DownloadBlankAsync(HrMentorConnection connection, Guid documentId, long maxBytes, CancellationToken cancellationToken)
    {
        if (Down is { } error)
        {
            return Task.FromResult(Result.Failure<byte[]>(error));
        }

        Interlocked.Increment(ref _downloads);

        return Task.FromResult(_files.TryGetValue(documentId, out var content)
            ? Result.Success(content)
            : Result.Failure<byte[]>(HrMentorErrors.NotFound));
    }

    public Task<Result<HrMentorPlatformDocuments>> ListPlatformDocumentsAsync(HrMentorConnection connection, CancellationToken cancellationToken)
    {
        LastConnection = connection;
        if ((Down ?? PlatformAnswer) is { } error)
        {
            return Task.FromResult(Result.Failure<HrMentorPlatformDocuments>(error));
        }

        IReadOnlyList<HrMentorBlankItem> documents = PlatformEmpty ? [] : [.. Blanks];
        return Task.FromResult(Result.Success(new HrMentorPlatformDocuments(PlatformId, "hr.bonvi.uz", documents)));
    }

    public Task<Result<byte[]>> DownloadPlatformDocumentAsync(HrMentorConnection connection, Guid documentId, long maxBytes, CancellationToken cancellationToken)
    {
        if (Down is { } error)
        {
            return Task.FromResult(Result.Failure<byte[]>(error));
        }

        Interlocked.Increment(ref _platformDownloads);

        return Task.FromResult(_files.TryGetValue(documentId, out var content)
            ? Result.Success(content)
            : Result.Failure<byte[]>(HrMentorErrors.NotFound));
    }

    public Task<Result<HrMentorHoldingsReport>> ReportHoldingsAsync(HrMentorConnection connection, IReadOnlyList<HrMentorHolding> holdings, CancellationToken cancellationToken)
    {
        if ((Down ?? HoldingsAnswer) is { } error)
        {
            return Task.FromResult(Result.Failure<HrMentorHoldingsReport>(error));
        }

        ReportedHoldings.Enqueue([.. holdings]);
        var mine = Blanks.ToDictionary(blank => blank.DocumentId, blank => blank.VersionId);
        var recorded = holdings.Count(h => mine.TryGetValue(h.DocumentId, out var version) && version == h.VersionId);

        return Task.FromResult(Result.Success(new HrMentorHoldingsReport(recorded, holdings.Count - recorded)));
    }

    /// <summary>Platformaning hozirgi reviziyasi (<c>/platform/changes</c> javobi).</summary>
    public string Revision { get; set; } = new('1', 64);

    /// <summary>Kutish so'rovi shu xato bilan qaytadi; <c>null</c> — ro'yxat javobiga ergashadi.</summary>
    public Error? ChangesAnswer { get; set; }

    /// <summary>Kutishga berilgan <c>since</c> lar, tartib bilan.</summary>
    public ConcurrentQueue<string?> ChangesAsked { get; } = new();

    public Task<Result<string>> WaitForChangesAsync(HrMentorConnection connection, string? since, TimeSpan wait, CancellationToken cancellationToken)
    {
        LastConnection = connection;
        ChangesAsked.Enqueue(since);
        if ((Down ?? ChangesAnswer ?? PlatformAnswer) is { } error)
        {
            return Task.FromResult(Result.Failure<string>(error));
        }

        return Task.FromResult(Result.Success(Revision));
    }

    /// <summary>Soxta platformaning identifikatori.</summary>
    public static readonly Guid PlatformId = Guid.Parse("0193aaaa-0000-7000-8000-000000000001");
}
