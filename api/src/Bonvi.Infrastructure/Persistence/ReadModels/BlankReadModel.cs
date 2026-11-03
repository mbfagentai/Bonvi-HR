using System.Text.Json;
using Bonvi.Application.Blanks.Definitions;
using BlankStepMode = Bonvi.Domain.Blanks.BlankStepMode;
using Bonvi.Application.Blanks.Submissions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Blanks;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>«Blankalar» o'qish modeli (SPEC-blankalar §7) — sahifa bo'yicha bir nechta to'plamli so'rov, N+1 siz.</summary>
public sealed class BlankReadModel : IBlankReadModel
{
    private readonly AppDbContext _context;

    public BlankReadModel(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<BlankTemplateDto>> ListTemplatesAsync(CancellationToken cancellationToken = default)
        => await TemplatesQuery(null).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<BlankTemplateDto?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default)
        => TemplatesQuery(id).FirstOrDefaultAsync(cancellationToken);

    public async Task<BlankTemplateVersionDto?> GetVersionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await (
                from version in _context.BlankTemplateVersions.AsNoTracking()
                join template in _context.BlankTemplates.AsNoTracking() on version.TemplateId equals template.Id
                where version.Id == id
                select new { version.Id, version.TemplateId, template.Code, version.Version, version.Status, version.Definition })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? null
            : new BlankTemplateVersionDto(row.Id, row.TemplateId, row.Code, row.Version, row.Status.ToString(), Json(row.Definition));
    }

    public Task<string?> GetDefinitionJsonAsync(Guid versionId, CancellationToken cancellationToken = default)
        => _context.BlankTemplateVersions
            .AsNoTracking()
            .Where(version => version.Id == versionId)
            .Select(version => version.Definition)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BlankRoleDto>> ListRolesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await (
                from assignment in _context.BlankRoleAssignments.AsNoTracking()
                join user in _context.Users.AsNoTracking() on assignment.UserId equals user.Id
                select new { assignment.RoleCode, user.Id, user.FullName.LastName, user.FullName.FirstName, user.FullName.MiddleName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return BlankActorRoles.OfficeRoles
            .Select(role => new BlankRoleDto(
                role,
                rows.Where(row => row.RoleCode == role)
                    .Select(row => new BlankRoleUserDto(row.Id, FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full))
                    .OrderBy(user => user.FullName, StringComparer.Ordinal)
                    .ToList()))
            .ToList();
    }

    public async Task<BlankSubmissionPage> SearchAsync(BlankSubmissionFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = _context.BlankSubmissions.AsNoTracking();

        if (filter.TemplateCode is { } code)
        {
            query = query.Where(submission => submission.TemplateCode == code);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(submission => submission.Status == status);
        }

        if (filter.EmployeeId is { } employeeId)
        {
            query = query.Where(submission => submission.EmployeeId == employeeId);
        }

        if (filter.CandidateId is { } candidateId)
        {
            query = query.Where(submission => submission.CandidateId == candidateId);
        }

        if (filter.DepartmentIds is { } departmentIds)
        {
            var scoped = departmentIds.ToList();
            query = query.Where(submission => submission.DepartmentId != null && scoped.Contains(submission.DepartmentId.Value));
        }

        if (filter.CreatedFrom is { } from)
        {
            query = query.Where(submission => submission.CreatedAt >= from);
        }

        if (filter.CreatedBefore is { } before)
        {
            query = query.Where(submission => submission.CreatedAt < before);
        }

        if (filter.Incomplete)
        {
            query = query.Where(submission => submission.MissingCount > 0);
        }

        if (filter.OverdueBefore is { } today)
        {
            query = query.Where(submission => submission.DueDate < today
                && (submission.Status == BlankSubmissionStatus.Draft || submission.Status == BlankSubmissionStatus.InApproval));
        }

        // Qidiruv: raqam yoki sub'yekt ismi (xodim qidiruvi — lotin/kirill farqsiz search_key qoidasi).
        if (filter.Search is { } search)
        {
            var people = EmployeeSearchQuery.MatchingIds(_context, search);
            var pattern = SearchPattern.For(search);
            var candidates = _context.Candidates
                .Where(candidate => EF.Functions.ILike(candidate.FullName.LastName, pattern, SearchPattern.Escape)
                    || EF.Functions.ILike(candidate.FullName.FirstName, pattern, SearchPattern.Escape))
                .Select(candidate => candidate.Id);
            int? number = int.TryParse(search.TrimStart('№', '#', ' '), out var parsed) ? parsed : null;

            query = query.Where(submission =>
                (number != null && submission.Number == number)
                || (submission.EmployeeId != null && people.Contains(submission.EmployeeId.Value))
                || (submission.CandidateId != null && candidates.Contains(submission.CandidateId.Value)));
        }

        if (filter.WaitingFor is { } waiting)
        {
            // §6.2 elektron yo'l: ofis roli (ruxsat + biriktirish Application'da hisoblangan) yoki
            // shaxs roli chaqiruvchining xodimiga teng. field:x rollari bu filtrga kirmaydi (ASSUMPTIONS).
            var roles = waiting.OfficeRoles.ToList();
            var me = waiting.EmployeeId;
            query = query.Where(submission => _context.BlankSubmissionSteps.Any(step =>
                step.SubmissionId == submission.Id
                && step.State == BlankStepState.Open
                && step.Mode != BlankStepMode.InPerson
                && (roles.Contains(step.ActorRole)
                    || (me != null && step.ActorRole == BlankActorRoles.SubjectEmployee && submission.EmployeeId == me)
                    || (me != null && step.ActorRole == BlankActorRoles.DepartmentHead
                        && _context.Departments.Any(department => department.Id == submission.DepartmentId
                            && department.HeadEmployeeId == me)))));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await query
            .OrderByDescending(submission => submission.CreatedAt)
            .ThenByDescending(submission => submission.Id)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return new BlankSubmissionPage([], total);
        }

        var names = await NamesAsync(rows, cancellationToken).ConfigureAwait(false);
        var ids = rows.Select(row => row.Id).ToList();
        var openSteps = (await _context.BlankSubmissionSteps
                .AsNoTracking()
                .Where(step => ids.Contains(step.SubmissionId) && step.State == BlankStepState.Open)
                .OrderBy(step => step.OrderNo)
                .ThenBy(step => step.StepCode)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToLookup(step => step.SubmissionId);

        var items = rows.Select(row => new BlankSubmissionListItemDto(
                row.Id,
                row.TemplateCode,
                names.Template(row.TemplateId),
                row.Number,
                row.Status.ToString(),
                names.Subject(row),
                names.Department(row.DepartmentId),
                openSteps[row.Id].Select(step => new BlankCurrentStepDto(step.StepCode, step.Title, step.ActorRole, step.DueDate)).ToList(),
                row.MissingCount,
                row.DueDate,
                row.CreatedAt,
                row.SubmittedAt,
                row.CompletedAt))
            .ToList();

        return new BlankSubmissionPage(items, total);
    }

    public async Task<BlankSubmissionDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _context.BlankSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(submission => submission.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var steps = await _context.BlankSubmissionSteps
            .AsNoTracking()
            .Where(step => step.SubmissionId == id)
            .OrderBy(step => step.OrderNo)
            .ThenBy(step => step.StepCode)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var attachments = await _context.BlankAttachments
            .AsNoTracking()
            .Where(attachment => attachment.SubmissionId == id && attachment.RemovedAt == null)
            .OrderBy(attachment => attachment.UploadedAt)
            .Select(attachment => new BlankAttachmentDto(
                attachment.Id,
                attachment.FieldPath,
                attachment.Kind.ToString(),
                attachment.FileName,
                attachment.SizeBytes,
                attachment.UploadedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var names = await NamesAsync([row], cancellationToken).ConfigureAwait(false);
        var actorIds = steps.Where(step => step.ActedByUserId != null).Select(step => step.ActedByUserId!.Value).Distinct().ToList();
        var actors = actorIds.Count == 0
            ? []
            : await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user => actorIds.Contains(user.Id))
                .Select(user => new { user.Id, user.FullName.LastName, user.FullName.FirstName, user.FullName.MiddleName })
                .ToDictionaryAsync(
                    user => user.Id,
                    user => FullName.FromTrusted(user.LastName, user.FirstName, user.MiddleName).Full,
                    cancellationToken)
                .ConfigureAwait(false);

        var missing = JsonSerializer.Deserialize<List<string>>(row.Missing) ?? [];

        return new BlankSubmissionDetailDto(
            row.Id,
            row.TemplateId,
            row.TemplateVersionId,
            row.TemplateCode,
            names.Template(row.TemplateId),
            row.Number,
            row.Status.ToString(),
            names.Subject(row),
            row.DepartmentId,
            names.Department(row.DepartmentId),
            row.Version,
            Json(row.Answers),
            Json(row.Computed),
            missing,
            row.MissingCount,
            row.DueDate,
            row.CreatedAt,
            row.CreatedBy,
            row.SubmittedAt,
            row.CompletedAt,
            row.CancelledAt,
            row.CancelReason,
            row.ReturnComment,
            steps.Select(step => new BlankStepDto(
                    step.Id,
                    step.StepCode,
                    step.OrderNo,
                    step.Title,
                    step.ActorRole,
                    step.Mode.ToString(),
                    step.State.ToString(),
                    step.Decision,
                    step.Comment,
                    step.DueDate,
                    step.Method?.ToString(),
                    step.ActedByUserId,
                    step.ActedByUserId is { } actor ? actors.GetValueOrDefault(actor) : null,
                    step.SignerEmployeeId,
                    step.SignerName,
                    step.SignatureStorageKey != null,
                    step.AnswersPatch is null ? null : Json(step.AnswersPatch),
                    step.ActedAt))
                .ToList(),
            attachments,
            BlankCanDto.Nothing);
    }

    public Task<Guid?> FindIdByClientRequestAsync(Guid clientRequestId, CancellationToken cancellationToken = default)
        => _context.BlankSubmissions
            .AsNoTracking()
            .Where(submission => submission.ClientRequestId == clientRequestId)
            .Select(submission => (Guid?)submission.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<BlankEmployeeSubject?> FindEmployeeSubjectAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        if (!await _context.Employees.AnyAsync(employee => employee.Id == employeeId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var employment = await BlankPrefillSource.CurrentEmploymentAsync(_context, employeeId, cancellationToken).ConfigureAwait(false);
        return new BlankEmployeeSubject(employeeId, employment?.Id, employment?.DepartmentId);
    }

    public async Task<(bool Found, Guid? DepartmentId)> FindCandidateSubjectAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        // Vakansiyada bo'lim ustuni yo'q — nomzod blankasining bo'limi bo'sh qoladi (ASSUMPTIONS).
        var found = await _context.Candidates.AnyAsync(candidate => candidate.Id == candidateId, cancellationToken).ConfigureAwait(false);
        return (found, null);
    }

    public async Task<string?> FirstMissingReferenceAsync(
        IReadOnlyList<BlankAnswerReference> references,
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(references);

        foreach (var reference in references)
        {
            var id = reference.Id;
            var exists = reference.Kind switch
            {
                "employee" => await _context.Employees.IgnoreQueryFilters().AnyAsync(row => row.Id == id, cancellationToken).ConfigureAwait(false),
                "candidate" => await _context.Candidates.IgnoreQueryFilters().AnyAsync(row => row.Id == id, cancellationToken).ConfigureAwait(false),
                "department" => await _context.Departments.IgnoreQueryFilters().AnyAsync(row => row.Id == id, cancellationToken).ConfigureAwait(false),
                "position" => await _context.Positions.IgnoreQueryFilters().AnyAsync(row => row.Id == id, cancellationToken).ConfigureAwait(false),
                "direction" => await _context.Directions.IgnoreQueryFilters().AnyAsync(row => row.Id == id, cancellationToken).ConfigureAwait(false),
                "docRef" => await _context.BlankSubmissions.AnyAsync(
                        row => row.Id == id
                            && row.Id != submissionId
                            && row.Status != BlankSubmissionStatus.Cancelled
                            && (reference.TemplateCode == null || row.TemplateCode == reference.TemplateCode),
                        cancellationToken)
                    .ConfigureAwait(false),
                "file" => await _context.BlankAttachments.AnyAsync(
                        row => row.Id == id && row.SubmissionId == submissionId && row.RemovedAt == null
                            && row.FieldPath == reference.Path,
                        cancellationToken)
                    .ConfigureAwait(false),
                _ => false,
            };

            if (!exists)
            {
                return reference.Path;
            }
        }

        return null;
    }

    public Task<Guid?> GetUserEmployeeIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.EmployeeId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> MissingUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var wanted = userIds.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        var found = await _context.Users
            .AsNoTracking()
            .Where(user => wanted.Contains(user.Id))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return wanted.Except(found).ToList();
    }

    private IQueryable<BlankTemplateDto> TemplatesQuery(Guid? id)
    {
        var templates = _context.BlankTemplates.AsNoTracking();
        if (id is { } templateId)
        {
            templates = templates.Where(template => template.Id == templateId);
        }

        return from template in templates
               let published = _context.BlankTemplateVersions
                   .Where(version => version.TemplateId == template.Id && version.Status == BlankTemplateVersionStatus.Published)
                   .Select(version => new { version.Id, version.Version })
                   .FirstOrDefault()
               orderby template.SortOrder, template.Code
               select new BlankTemplateDto(
                   template.Id,
                   template.Code,
                   template.Title,
                   template.SubjectKind.ToString(),
                   template.IsActive,
                   published == null ? null : published.Version,
                   published == null ? null : published.Id);
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>Sahifa qatorlari uchun nomlar — har tur bitta so'rov bilan.</summary>
    private async Task<NameBook> NamesAsync(IReadOnlyList<BlankSubmission> rows, CancellationToken cancellationToken)
    {
        var templateIds = rows.Select(row => row.TemplateId).Distinct().ToList();
        var employeeIds = rows.Where(row => row.EmployeeId != null).Select(row => row.EmployeeId!.Value).Distinct().ToList();
        var candidateIds = rows.Where(row => row.CandidateId != null).Select(row => row.CandidateId!.Value).Distinct().ToList();
        var departmentIds = rows.Where(row => row.DepartmentId != null).Select(row => row.DepartmentId!.Value).Distinct().ToList();

        var templates = await _context.BlankTemplates
            .AsNoTracking()
            .Where(template => templateIds.Contains(template.Id))
            .ToDictionaryAsync(template => template.Id, template => template.Title, cancellationToken)
            .ConfigureAwait(false);

        // O'chirilgan xodim, nomzod yoki bo'lim ham nomi bilan ko'rinadi — blanka tarix hujjati.
        var employees = employeeIds.Count == 0
            ? []
            : await _context.Employees
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(employee => employeeIds.Contains(employee.Id))
                .Select(employee => new { employee.Id, employee.FullName.LastName, employee.FullName.FirstName, employee.FullName.MiddleName })
                .ToDictionaryAsync(
                    employee => employee.Id,
                    employee => FullName.FromTrusted(employee.LastName, employee.FirstName, employee.MiddleName).Full,
                    cancellationToken)
                .ConfigureAwait(false);

        var candidates = candidateIds.Count == 0
            ? []
            : await _context.Candidates
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(candidate => candidateIds.Contains(candidate.Id))
                .Select(candidate => new { candidate.Id, candidate.FullName.LastName, candidate.FullName.FirstName, candidate.FullName.MiddleName })
                .ToDictionaryAsync(
                    candidate => candidate.Id,
                    candidate => FullName.FromTrusted(candidate.LastName, candidate.FirstName, candidate.MiddleName).Full,
                    cancellationToken)
                .ConfigureAwait(false);

        var departments = departmentIds.Count == 0
            ? []
            : await _context.Departments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(department => departmentIds.Contains(department.Id))
                .ToDictionaryAsync(department => department.Id, department => department.Name, cancellationToken)
                .ConfigureAwait(false);

        return new NameBook(templates, employees, candidates, departments);
    }

    private sealed record NameBook(
        Dictionary<Guid, string> Templates,
        Dictionary<Guid, string> Employees,
        Dictionary<Guid, string> Candidates,
        Dictionary<Guid, string> Departments)
    {
        public string Template(Guid id) => Templates.GetValueOrDefault(id) ?? string.Empty;

        public string? Department(Guid? id) => id is { } departmentId ? Departments.GetValueOrDefault(departmentId) : null;

        public BlankSubjectDto Subject(BlankSubmission row) => row.SubjectKind switch
        {
            BlankSubjectKind.Employee => new BlankSubjectDto(
                nameof(BlankSubjectKind.Employee),
                row.EmployeeId,
                row.EmployeeId is { } employeeId ? Employees.GetValueOrDefault(employeeId) : null),
            BlankSubjectKind.Candidate => new BlankSubjectDto(
                nameof(BlankSubjectKind.Candidate),
                row.CandidateId,
                row.CandidateId is { } candidateId ? Candidates.GetValueOrDefault(candidateId) : null),
            _ => new BlankSubjectDto(nameof(BlankSubjectKind.None), null, null),
        };
    }
}
