using Bonvi.Application.Common.Documents;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Employees.MehnatUz;

/// <summary>mehnat.uz skrinshotini o'qish so'rovi.</summary>
public sealed record GetMehnatUzScreenshotQuery(Guid EmployeeId);

/// <summary>Berishga tayyor skrinshot: turi va oqimi.</summary>
/// <remarks>
/// Fayl NOMI yo'q — xodim hujjatidan farqi shu. Skrinshot yuklab olish
/// uchun emas, EKRANDA ko'rish uchun: mijoz uni <c>blob</c> qilib olib
/// modal oynada chizadi, ya'ni foydalanuvchi hech qachon fayl nomini
/// ko'rmaydi. Nomni saqlash faqat foydasiz ustun bo'lardi.
/// </remarks>
public sealed record MehnatUzScreenshot(string ContentType, Stream Content);

/// <summary>mehnat.uz skrinshotini yuklash buyrug'i.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="Content">Yuklangan faylning oqimi.</param>
/// <param name="SizeBytes">Faylning e'lon qilingan hajmi.</param>
public sealed record UploadMehnatUzScreenshotCommand(Guid EmployeeId, Stream Content, long SizeBytes);

/// <summary>mehnat.uz skrinshotini o'chirish buyrug'i.</summary>
public sealed record DeleteMehnatUzScreenshotCommand(Guid EmployeeId);

/// <summary>
/// mehnat.uz profilining skrinshotini beradi.
/// </summary>
/// <remarks>
/// Kalitni topish — shu handler ishi, faylni ochish esa
/// <see cref="DocumentFileStore"/> niki (<c>GetEmployeeDocumentFileQueryHandler</c>
/// bilan aynan bir xil bo'linish). Kalit bazada bor, fayl esa yo'q holati
/// ham «skrinshot yo'q» deb qaraladi: interfeys uchun natija bir xil.
/// </remarks>
public sealed class GetMehnatUzScreenshotQueryHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly DocumentFileStore _files;

    public GetMehnatUzScreenshotQueryHandler(IEmployeeRepository employees, DocumentFileStore files)
    {
        _employees = employees;
        _files = files;
    }

    public async Task<Result<MehnatUzScreenshot>> HandleAsync(
        GetMehnatUzScreenshotQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var employee = await _employees.GetByIdAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        if (employee.MehnatUzScreenshotKey is not { } key)
        {
            return EmployeeErrors.MehnatUzScreenshotNotFound;
        }

        var content = await _files.OpenAsync(key, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            return EmployeeErrors.MehnatUzScreenshotNotFound;
        }

        // Tur bazadan olinadi, fayldan qayta aniqlanmaydi: u yozilayotganda
        // allaqachon MAZMUN bo'yicha aniqlangan (`DocumentFileStore`), va
        // uni har o'qishda takrorlash butun faylni xotiraga olishni talab
        // qilardi. Yo'qolgan holat uchun zaxira — brauzer uchun eng
        // xavfsiz umumiy tur.
        var contentType = employee.MehnatUzScreenshotContentType ?? "application/octet-stream";

        return Result.Success(new MehnatUzScreenshot(contentType, content));
    }
}

/// <summary>
/// mehnat.uz profilining skrinshotini qabul qiladi va saqlaydi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega XODIM RASMI quvuri emas.</b> <c>SquarePhotoStore</c> suratni
/// markazdan kvadrat qirqib 512×512 JPEG qiladi — skrinshot uchun bu
/// halokat: rasm kvadrat emas, uning qiymati esa chetlaridagi MATNDA
/// (ism, JSHSHIR, ish joyi). Qirqilganda dalil yo'qoladi, kichraytirilganda
/// esa qolgani o'qilmay qoladi. Shu sababli HUJJAT quvuri
/// (<see cref="DocumentFileStore"/>) ishlatiladi: u faylni faqat
/// TEKSHIRADI (mazmun bo'yicha tur, 20 MB chegara) va baytlarni
/// o'zgarishsiz saqlaydi. To'liq asos <c>Employee.MehnatUzScreenshotKey</c>
/// izohida.
/// </para>
/// <para>
/// Eski fayl faqat baza muvaffaqiyatli yangilangandan KEYIN o'chiriladi —
/// <c>UploadEmployeePhotoCommandHandler</c> dagi bilan bir xil tartib:
/// teskarisida saqlash yiqilsa, karta mavjud bo'lmagan faylga ishora
/// qilib qolardi.
/// </para>
/// </remarks>
public sealed class UploadMehnatUzScreenshotCommandHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly DocumentFileStore _files;
    private readonly IUnitOfWork _unitOfWork;

    public UploadMehnatUzScreenshotCommandHandler(
        IEmployeeRepository employees,
        DocumentFileStore files,
        IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _files = files;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MehnatUzScreenshotDto>> HandleAsync(
        UploadMehnatUzScreenshotCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Arzon tekshiruv oldinda: noma'lum xodim uchun faylni o'qib,
        // tekshirib va diskka yozib o'tirishning ma'nosi yo'q.
        var employee = await _employees.GetByIdAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        var accepted = await _files
            .AcceptAsync(command.Content, command.SizeBytes, cancellationToken)
            .ConfigureAwait(false);

        if (accepted.Key is not { } key)
        {
            return EmployeeErrors.DocumentRejected(accepted.Rejection!.Value);
        }

        var previousKey = employee.MehnatUzScreenshotKey;
        employee.SetMehnatUzScreenshot(key, accepted.ContentType);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Eski fayl DISKDAN ham o'chadi — xodim hujjatlaridan farqli.
        // Sabab: hujjat yozuvi yumshoq o'chiriladi va bazada qolgani uchun
        // fayli ham turishi kerak, bu yerda esa eski kalit hech qanday
        // qatorda saqlanib qolmaydi (xodim rasmidagi bilan bir xil holat).
        if (previousKey is not null)
        {
            await _files.DeleteAsync(previousKey, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new MehnatUzScreenshotDto(key));
    }
}

/// <summary>
/// mehnat.uz skrinshotini o'chiradi.
/// </summary>
/// <remarks>
/// Amal <b>idempotent</b>: skrinshot allaqachon yo'q bo'lsa ham
/// muvaffaqiyat qaytadi — so'rovning maqsadi fayl QOLMASLIGI va u
/// bajarilgan (<c>DeleteEmployeePhotoCommandHandler</c> bilan bir xil
/// mulohaza).
/// </remarks>
public sealed class DeleteMehnatUzScreenshotCommandHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly DocumentFileStore _files;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteMehnatUzScreenshotCommandHandler(
        IEmployeeRepository employees,
        DocumentFileStore files,
        IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _files = files;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        DeleteMehnatUzScreenshotCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var employee = await _employees.GetByIdAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        if (employee.MehnatUzScreenshotKey is not { } key)
        {
            return Result.Success();
        }

        employee.SetMehnatUzScreenshot(null, null);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _files.DeleteAsync(key, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
