using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

public sealed class BlankTemplateRepository : Repository<BlankTemplate>, IBlankTemplateRepository
{
    public BlankTemplateRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public Task<BlankTemplate?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(template => template.Code == code, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<BlankTemplate>> ListAsync(CancellationToken cancellationToken = default)
        => await Set
            .OrderBy(template => template.SortOrder)
            .ThenBy(template => template.Code)
            .ToListAsync(cancellationToken);
}

public sealed class BlankTemplateVersionRepository : Repository<BlankTemplateVersion>, IBlankTemplateVersionRepository
{
    public BlankTemplateVersionRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public Task<BlankTemplateVersion?> GetPublishedAsync(Guid templateId, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(
            version => version.TemplateId == templateId && version.Status == BlankTemplateVersionStatus.Published,
            cancellationToken);

    /// <inheritdoc />
    public Task<BlankTemplateVersion?> GetAsync(Guid templateId, string version, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(
            row => row.TemplateId == templateId && row.Version == version,
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<BlankTemplateVersion>> ListByTemplateAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
        => await Set
            .Where(version => version.TemplateId == templateId)
            .OrderBy(version => version.CreatedAt)
            .ThenBy(version => version.Id)
            .ToListAsync(cancellationToken);
}

public sealed class BlankSubmissionRepository : Repository<BlankSubmission>, IBlankSubmissionRepository
{
    public BlankSubmissionRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    /// <remarks>
    /// <c>AsSplitQuery()</c> — ikki to'plam bitta JOIN'da qatorlarni ko'paytirardi
    /// (qadamlar × fayllar). Tartib <c>Include</c> ichida: qadamlar guruh tartibida,
    /// fayllar yuklanish tartibida (<c>Id</c> — UUID v7, teng vaqtda ham ketma-ketlikni saqlaydi).
    /// </remarks>
    public Task<BlankSubmission?> GetWithChildrenAsync(Guid id, CancellationToken cancellationToken = default)
        => Set
            .Include(submission => submission.Steps.OrderBy(step => step.OrderNo).ThenBy(step => step.StepCode))
            .Include(submission => submission.Attachments.OrderBy(file => file.UploadedAt).ThenBy(file => file.Id))
            .AsSplitQuery()
            .FirstOrDefaultAsync(submission => submission.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<BlankSubmission?> GetByClientRequestIdAsync(Guid clientRequestId, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(submission => submission.ClientRequestId == clientRequestId, cancellationToken);
}

public sealed class BlankRoleAssignmentRepository : Repository<BlankRoleAssignment>, IBlankRoleAssignmentRepository
{
    public BlankRoleAssignmentRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BlankRoleAssignment>> ListAsync(CancellationToken cancellationToken = default)
        => await Set
            .OrderBy(assignment => assignment.RoleCode)
            .ThenBy(assignment => assignment.CreatedAt)
            .ThenBy(assignment => assignment.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListUserIdsAsync(string roleCode, CancellationToken cancellationToken = default)
        => await Set
            .Where(assignment => assignment.RoleCode == roleCode)
            .Select(assignment => assignment.UserId)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<BlankRoleAssignment?> GetAsync(string roleCode, Guid userId, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(
            assignment => assignment.RoleCode == roleCode && assignment.UserId == userId,
            cancellationToken);
}

/// <summary>
/// <see cref="IBlankNumberCounter"/> — bitta <c>INSERT … ON CONFLICT … RETURNING</c> (SPEC-blankalar §3.7).
/// </summary>
/// <remarks>
/// <para>
/// <c>ON CONFLICT DO UPDATE</c> qatorni tranzaksiya oxirigacha qulflaydi: bir vaqtda yuborilgan
/// ikkinchi blanka birinchisining tranzaksiyasi tugashini kutadi va keyingi raqamni oladi,
/// birinchisi qaytsa — o'sha raqamni oladi (bo'shliq yo'q).
/// </para>
/// <para>
/// Natija <c>ToListAsync</c> bilan o'qiladi, <c>FirstAsync</c> bilan emas: EF birinchisiga
/// <c>LIMIT</c> qo'shib so'rovni ichki so'rovga o'raydi, <c>INSERT</c> esa ichki so'rov bo'la olmaydi.
/// </para>
/// </remarks>
public sealed class BlankNumberCounterStore : IBlankNumberCounter
{
    private readonly AppDbContext _context;

    public BlankNumberCounterStore(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<int> NextAsync(string templateCode, CancellationToken cancellationToken = default)
    {
        var numbers = await _context.Database
            .SqlQuery<int>($"""
                INSERT INTO blank_number_counters (template_code, last_number)
                VALUES ({templateCode}, 1)
                ON CONFLICT (template_code)
                DO UPDATE SET last_number = blank_number_counters.last_number + 1
                RETURNING last_number AS "Value"
                """)
            .ToListAsync(cancellationToken);

        return numbers[0];
    }
}
