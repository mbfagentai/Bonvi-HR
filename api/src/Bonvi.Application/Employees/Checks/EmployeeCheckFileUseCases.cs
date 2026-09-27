using Bonvi.Application.Common.Documents;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Checks;

/// <summary>Xodimning tekshiruv materiallari ro'yxati.</summary>
public sealed record GetEmployeeCheckFilesQuery(Guid EmployeeId);

/// <summary>Bitta materialning fayli.</summary>
public sealed record GetEmployeeCheckFileContentQuery(Guid EmployeeId, Guid FileId);

/// <summary>Berishga tayyor fayl: nomi, turi va oqimi.</summary>
public sealed record EmployeeCheckFileContent(string FileName, string ContentType, Stream Content);

/// <summary>Material yuklash buyrug'i.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="Content">Yuklangan faylning oqimi.</param>
/// <param name="FileName">Asl nomi — faqat ko'rsatish uchun.</param>
/// <param name="SizeBytes">Faylning e'lon qilingan hajmi.</param>
public sealed record UploadEmployeeCheckFileCommand(Guid EmployeeId, Stream Content, string? FileName, long SizeBytes);

/// <summary>Materialni o'chirish buyrug'i.</summary>
public sealed record DeleteEmployeeCheckFileCommand(Guid EmployeeId, Guid FileId);

/// <summary>
/// «Tekshiruvlar» kartasidagi materiallar — metama'lumot, baytlarsiz.
/// </summary>
/// <remarks>
/// Fayllarning o'zi ro'yxatda YO'Q va bu ataylab (foydalanuvchi talabi,
/// 2026-09-27): karta ixcham turadi, fayl esa HR uni bosgandagina
/// alohida so'rov bilan olinadi (<see cref="GetEmployeeCheckFileContentQueryHandler"/>).
/// </remarks>
public sealed class GetEmployeeCheckFilesQueryHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeCheckFileRepository _files;

    public GetEmployeeCheckFilesQueryHandler(IEmployeeRepository employees, IEmployeeCheckFileRepository files)
    {
        _employees = employees;
        _files = files;
    }

    public async Task<Result<IReadOnlyList<EmployeeCheckFileDto>>> HandleAsync(
        GetEmployeeCheckFilesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!await _employees.ExistsAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false))
        {
            return EmployeeErrors.NotFound;
        }

        var rows = await _files.GetByEmployeeAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false);

        return Result.Success<IReadOnlyList<EmployeeCheckFileDto>>([.. rows.Select(ToDto)]);
    }

    internal static EmployeeCheckFileDto ToDto(EmployeeCheckFile file)
        => new(file.Id, file.FileName, file.ContentType, file.SizeBytes, file.CreatedAt);
}

/// <summary>
/// Materialning faylini beradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Material marshrutdagi xodimniki bo'lishi SHART.</b> Aks holda
/// istalgan xodimning manzili orqali boshqa odamning dalilini ochish
/// mumkin bo'lardi — begona material ham «topilmadi» javobini oladi
/// (<see cref="EmployeeErrors.CheckFileNotFound"/> izohi).
/// </para>
/// <para>
/// Kalit bazada bor, fayl esa diskda yo'q holati ham «topilmadi» —
/// hujjatdagi bilan bir xil naqsh (<c>GetEmployeeDocumentFileQueryHandler</c>).
/// </para>
/// </remarks>
public sealed class GetEmployeeCheckFileContentQueryHandler
{
    private readonly IEmployeeCheckFileRepository _files;
    private readonly DocumentFileStore _store;

    public GetEmployeeCheckFileContentQueryHandler(IEmployeeCheckFileRepository files, DocumentFileStore store)
    {
        _files = files;
        _store = store;
    }

    public async Task<Result<EmployeeCheckFileContent>> HandleAsync(
        GetEmployeeCheckFileContentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var file = await _files.GetByIdAsync(query.FileId, cancellationToken).ConfigureAwait(false);
        if (file is null || file.EmployeeId != query.EmployeeId)
        {
            return EmployeeErrors.CheckFileNotFound;
        }

        var content = await _store.OpenAsync(file.FileKey, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            return EmployeeErrors.CheckFileNotFound;
        }

        return Result.Success(new EmployeeCheckFileContent(file.FileName, file.ContentType, content));
    }
}

/// <summary>
/// Materialni qabul qiladi va ro'yxatga QO'SHADI.
/// </summary>
/// <remarks>
/// <para>
/// Hech narsa almashtirilmaydi — eski yagona skrinshotdan farqi shu:
/// har bir fayl alohida dalil (sababi <see cref="EmployeeCheckFile"/> izohida).
/// </para>
/// <para>
/// <b>Hujjat quvuri</b> (<see cref="DocumentFileStore"/>): mazmun bo'yicha
/// tur (PDF, JPEG, PNG, WebP), 20 MB chegara, baytlar o'zgarishsiz —
/// skrinshotdagi matn o'qilishi kerak.
/// </para>
/// </remarks>
public sealed class UploadEmployeeCheckFileCommandHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeCheckFileRepository _files;
    private readonly DocumentFileStore _store;
    private readonly IUnitOfWork _unitOfWork;

    public UploadEmployeeCheckFileCommandHandler(
        IEmployeeRepository employees,
        IEmployeeCheckFileRepository files,
        DocumentFileStore store,
        IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _files = files;
        _store = store;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeCheckFileDto>> HandleAsync(
        UploadEmployeeCheckFileCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Arzon tekshiruv oldinda: noma'lum xodim uchun faylni o'qib,
        // tekshirib va diskka yozib o'tirishning ma'nosi yo'q.
        if (!await _employees.ExistsAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false))
        {
            return EmployeeErrors.NotFound;
        }

        var accepted = await _store
            .AcceptAsync(command.Content, command.SizeBytes, cancellationToken)
            .ConfigureAwait(false);

        if (accepted.Key is not { } key)
        {
            return EmployeeErrors.DocumentRejected(accepted.Rejection!.Value);
        }

        var attached = EmployeeCheckFile.Attach(
            command.EmployeeId, key, command.FileName, accepted.ContentType!, command.SizeBytes);

        if (attached.IsFailure)
        {
            // Fayl ALLAQACHON diskka saqlangan — domen uni rad etsa, ortidan
            // yetim fayl qolmasligi kerak (hujjat yuklashdagi bilan bir xil).
            await _store.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
            return attached.Error;
        }

        _files.Add(attached.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return GetEmployeeCheckFilesQueryHandler.ToDto(attached.Value);
    }
}

/// <summary>
/// Materialni o'chiradi — YUMSHOQ: yozuv tarixda, fayl diskda qoladi.
/// </summary>
/// <remarks>
/// Sabab <see cref="EmployeeDocument"/> bilan bir xil: bu dalil va uning
/// izi yo'qolmasligi kerak. Material marshrutdagi xodimniki bo'lmasa —
/// «topilmadi» (<see cref="GetEmployeeCheckFileContentQueryHandler"/> izohi).
/// </remarks>
public sealed class DeleteEmployeeCheckFileCommandHandler
{
    private readonly IEmployeeCheckFileRepository _files;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteEmployeeCheckFileCommandHandler(IEmployeeCheckFileRepository files, IUnitOfWork unitOfWork)
    {
        _files = files;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        DeleteEmployeeCheckFileCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var file = await _files.GetByIdAsync(command.FileId, cancellationToken).ConfigureAwait(false);
        if (file is null || file.EmployeeId != command.EmployeeId)
        {
            return EmployeeErrors.CheckFileNotFound;
        }

        _files.Remove(file);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
