using Bonvi.Application.Common.Documents;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Documents;

/// <summary>Xodimga fayl biriktirish buyrug'i.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="DocumentTypeId">Hujjat turi — <c>document-types</c> ma'lumotnomasidan.</param>
/// <param name="Content">Yuklangan faylning oqimi.</param>
/// <param name="FileName">Mijoz yuborgan asl nom — faqat ko'rsatish uchun.</param>
/// <param name="SizeBytes">Faylning e'lon qilingan hajmi.</param>
/// <param name="Note">Ixtiyoriy izoh.</param>
public sealed record UploadEmployeeDocumentCommand(
    Guid EmployeeId,
    Guid DocumentTypeId,
    Stream Content,
    string? FileName,
    long SizeBytes,
    string? Note = null);

/// <summary>Hujjat turi va izohini tuzatadi. Fayl va xodim O'ZGARMAYDI.</summary>
public sealed record UpdateEmployeeDocumentCommand(Guid DocumentId, Guid DocumentTypeId, string? Note);

public sealed record DeleteEmployeeDocumentCommand(Guid DocumentId);

/// <summary>
/// Xodim hujjatini qabul qiladi, mazmunini tekshiradi va saqlaydi.
/// </summary>
/// <remarks>
/// Faylni tekshirish va saqlash <see cref="DocumentFileStore"/> ishi — bu
/// handler faqat XODIMGA va TURGA tegishli qismini bajaradi (fotosurat
/// bilan bir xil bo'linish, <c>UploadEmployeePhotoCommandHandler</c>ga
/// qarang).
/// </remarks>
public sealed class UploadEmployeeDocumentCommandHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IReferenceRepository<DocumentType> _documentTypes;
    private readonly IEmployeeDocumentRepository _documents;
    private readonly DocumentFileStore _files;
    private readonly EmployeeMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public UploadEmployeeDocumentCommandHandler(
        IEmployeeRepository employees,
        IReferenceRepository<DocumentType> documentTypes,
        IEmployeeDocumentRepository documents,
        DocumentFileStore files,
        EmployeeMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _documentTypes = documentTypes;
        _documents = documents;
        _files = files;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeDocumentDto>> HandleAsync(
        UploadEmployeeDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Arzon tekshiruvlar oldinda: noma'lum xodim yoki tur uchun
        // faylni butunlay o'qib, tekshirib o'tirishning ma'nosi yo'q.
        var employeeExists = await _employees
            .ExistsAsync(command.EmployeeId, cancellationToken)
            .ConfigureAwait(false);
        if (!employeeExists)
        {
            return EmployeeErrors.NotFound;
        }

        var typeExists = await _documentTypes
            .ExistsAsync(command.DocumentTypeId, cancellationToken)
            .ConfigureAwait(false);
        if (!typeExists)
        {
            return EmployeeErrors.DocumentTypeNotFound;
        }

        var accepted = await _files
            .AcceptAsync(command.Content, command.SizeBytes, cancellationToken)
            .ConfigureAwait(false);

        if (accepted.Key is not { } key)
        {
            return EmployeeErrors.DocumentRejected(accepted.Rejection!.Value);
        }

        var attached = EmployeeDocument.Attach(
            command.EmployeeId,
            command.DocumentTypeId,
            key,
            command.FileName,
            accepted.ContentType!,
            command.SizeBytes,
            command.Note);

        if (attached.IsFailure)
        {
            // Fayl ALLAQACHON diskka saqlangan (yuqorida) — domen uni rad
            // etsa, ortidan yetim fayl qolmasligi kerak.
            await _files.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
            return attached.Error;
        }

        _documents.Add(attached.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(attached.Value, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class UpdateEmployeeDocumentCommandHandler
{
    private readonly IEmployeeDocumentRepository _documents;
    private readonly IReferenceRepository<DocumentType> _documentTypes;
    private readonly EmployeeMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateEmployeeDocumentCommandHandler(
        IEmployeeDocumentRepository documents,
        IReferenceRepository<DocumentType> documentTypes,
        EmployeeMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _documents = documents;
        _documentTypes = documentTypes;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeDocumentDto>> HandleAsync(
        UpdateEmployeeDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var document = await _documents.GetByIdAsync(command.DocumentId, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return EmployeeErrors.DocumentNotFound;
        }

        var typeExists = await _documentTypes
            .ExistsAsync(command.DocumentTypeId, cancellationToken)
            .ConfigureAwait(false);
        if (!typeExists)
        {
            return EmployeeErrors.DocumentTypeNotFound;
        }

        var updated = document.Update(command.DocumentTypeId, command.Note);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(document, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Xodim hujjatini o'chiradi.
/// </summary>
/// <remarks>
/// Yumshoq o'chirish — yozuv bazada qoladi. Fayl ATAYLAB diskdan
/// o'chirilmaydi: bu <c>DeleteEmployeePhotoCommandHandler</c>dan tubdan
/// farq qiladi. Sabab — yozuv o'zi ham o'chmaydi (faqat <c>DeletedAt</c>
/// belgilanadi), ya'ni baza «bu hujjat bo'lgan» deb tarixni saqlab
/// qolaveradi; agar shu bilan birga fayl diskdan yo'qolsa, o'sha tarixiy
/// yozuv ishora qilgan fayl topilmay qoladi — bu <c>ISoftDeletable</c>ning
/// butun maqsadiga (hech narsa jismonan yo'qolmasligi) zid bo'lardi.
/// Xodim rasmida bunday muammo yo'q, chunki u soft-delete emas: eski
/// kalit qayta yozilganda hech qanday qatorda saqlanib qolmaydi.
/// </remarks>
public sealed class DeleteEmployeeDocumentCommandHandler
{
    private readonly IEmployeeDocumentRepository _documents;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteEmployeeDocumentCommandHandler(IEmployeeDocumentRepository documents, IUnitOfWork unitOfWork)
    {
        _documents = documents;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        DeleteEmployeeDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var document = await _documents.GetByIdAsync(command.DocumentId, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return EmployeeErrors.DocumentNotFound;
        }

        _documents.Remove(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
