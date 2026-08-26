using Bonvi.Application.Common.Documents;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Employees.Documents;

/// <summary>Xodimning barcha hujjatlari — kartadagi «Hujjatlar» tabi uchun.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="DocumentTypeId">Faqat shu turdagilar. Bo'sh — hammasi («tur bo'yicha guruh»).</param>
public sealed record GetEmployeeDocumentsQuery(Guid EmployeeId, Guid? DocumentTypeId = null);

public sealed class GetEmployeeDocumentsQueryHandler
{
    private readonly IEmployeeDocumentRepository _documents;
    private readonly IEmployeeRepository _employees;
    private readonly EmployeeMapper _mapper;

    public GetEmployeeDocumentsQueryHandler(
        IEmployeeDocumentRepository documents,
        IEmployeeRepository employees,
        EmployeeMapper mapper)
    {
        _documents = documents;
        _employees = employees;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<EmployeeDocumentDto>>> HandleAsync(
        GetEmployeeDocumentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var exists = await _employees.ExistsAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return EmployeeErrors.NotFound;
        }

        var rows = await _documents
            .GetByEmployeeAsync(query.EmployeeId, query.DocumentTypeId, cancellationToken)
            .ConfigureAwait(false);

        var items = new List<EmployeeDocumentDto>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(await _mapper.ToDtoAsync(row, cancellationToken).ConfigureAwait(false));
        }

        return Result.Success<IReadOnlyList<EmployeeDocumentDto>>(items);
    }
}

/// <summary>Hujjat faylini o'qish so'rovi.</summary>
public sealed record GetEmployeeDocumentFileQuery(Guid DocumentId);

/// <summary>Yuklab olishga tayyor fayl: nomi, turi va oqimi.</summary>
public sealed record EmployeeDocumentFile(string FileName, string ContentType, Stream Content);

/// <summary>
/// Hujjat faylini beradi.
/// </summary>
/// <remarks>
/// Kalitni topish — shu handler ishi, faylni ochish esa
/// <see cref="DocumentFileStore"/>niki. Fayl yo'qolgan holat (kalit bazada
/// bor, fayl esa yo'q) ham «hujjat topilmadi» deb qaraladi — xodim rasmi
/// bilan bir xil naqsh (<c>GetEmployeePhotoQueryHandler</c>).
/// </remarks>
public sealed class GetEmployeeDocumentFileQueryHandler
{
    private readonly IEmployeeDocumentRepository _documents;
    private readonly DocumentFileStore _files;

    public GetEmployeeDocumentFileQueryHandler(IEmployeeDocumentRepository documents, DocumentFileStore files)
    {
        _documents = documents;
        _files = files;
    }

    public async Task<Result<EmployeeDocumentFile>> HandleAsync(
        GetEmployeeDocumentFileQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var document = await _documents.GetByIdAsync(query.DocumentId, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return EmployeeErrors.DocumentNotFound;
        }

        var content = await _files.OpenAsync(document.FileKey, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            return EmployeeErrors.DocumentNotFound;
        }

        return Result.Success(new EmployeeDocumentFile(document.FileName, document.ContentType, content));
    }
}
