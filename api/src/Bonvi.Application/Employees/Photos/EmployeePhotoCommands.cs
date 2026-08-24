using Bonvi.Application.Common.Photos;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Employees.Photos;

/// <summary>Xodim rasmini yuklash buyrug'i.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="Content">Yuklangan faylning oqimi.</param>
/// <param name="ContentType">Mijoz e'lon qilgan MIME turi.</param>
/// <param name="SizeBytes">Faylning e'lon qilingan hajmi.</param>
public sealed record UploadEmployeePhotoCommand(
    Guid EmployeeId,
    Stream Content,
    string? ContentType,
    long SizeBytes);

/// <summary>
/// Xodim rasmini qabul qiladi, kvadrat qilib kichraytiradi va saqlaydi.
/// </summary>
/// <remarks>
/// <para>
/// Rasmni tekshirish, qirqish va diskka yozish <see cref="SquarePhotoStore"/>
/// ishi — bu handler faqat XODIMGA tegishli qismini bajaradi: kim uchun,
/// qaysi kalit va eski fayl bilan nima bo'ladi.
/// </para>
/// <para>
/// Eski fayl faqat ma'lumotlar bazasi muvaffaqiyatli yangilangandan KEYIN
/// o'chiriladi. Teskari tartibda saqlash yiqilsa, karta mavjud bo'lmagan
/// faylga ishora qilib qolardi.
/// </para>
/// </remarks>
public sealed class UploadEmployeePhotoCommandHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly SquarePhotoStore _photos;
    private readonly IUnitOfWork _unitOfWork;

    public UploadEmployeePhotoCommandHandler(
        IEmployeeRepository employees,
        SquarePhotoStore photos,
        IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _photos = photos;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeePhotoDto>> HandleAsync(
        UploadEmployeePhotoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Xodim eng birinchi tekshiriladi: noma'lum identifikator uchun
        // rasmni ochib, kichraytirib o'tirish keraksiz sarf bo'lardi.
        var employee = await _employees.GetByIdAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        var accepted = await _photos
            .AcceptAsync(command.Content, command.ContentType, command.SizeBytes, cancellationToken)
            .ConfigureAwait(false);

        if (accepted.Key is not { } key)
        {
            return EmployeeErrors.PhotoRejected(accepted.Rejection!.Value);
        }

        var previousKey = employee.PhotoKey;
        employee.SetPhoto(key);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (previousKey is not null)
        {
            await _photos.DeleteAsync(previousKey, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new EmployeePhotoDto(key));
    }
}

/// <summary>
/// Xodim rasmini o'chiradi.
/// </summary>
/// <remarks>
/// Amal <b>idempotent</b>: rasm allaqachon yo'q bo'lsa ham muvaffaqiyat
/// qaytadi. Sabab: «o'chir» so'rovining maqsadi — rasm QOLMASLIGI, va bu
/// shart allaqachon bajarilgan. Xato qaytarilsa, interfeys ikki marta
/// bosilgan tugma uchun keraksiz ogohlantirish ko'rsatardi.
/// </remarks>
public sealed class DeleteEmployeePhotoCommandHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly SquarePhotoStore _photos;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteEmployeePhotoCommandHandler(
        IEmployeeRepository employees,
        SquarePhotoStore photos,
        IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _photos = photos;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employees.GetByIdAsync(employeeId, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        if (employee.PhotoKey is not { } key)
        {
            return Result.Success();
        }

        employee.SetPhoto(null);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Yuklashdagi kabi: fayl faqat karta yangilangandan keyin o'chadi.
        await _photos.DeleteAsync(key, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
