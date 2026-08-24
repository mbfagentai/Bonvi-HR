using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Organization;

namespace Bonvi.Application.Catalog.Departments;

/// <summary>Bo'limni daraxtning boshqa joyiga ko'chirish.</summary>
/// <param name="Id">Ko'chiriladigan bo'lim.</param>
/// <param name="NewParentId">
/// Yangi ota bo'lim. <c>null</c> — bo'lim eng yuqori darajaga chiqariladi.
/// </param>
public sealed record MoveDepartmentCommand(Guid Id, Guid? NewParentId);

/// <summary>
/// Bo'limni butun avlodlari bilan birga boshqa ota bo'limga ko'chiradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Halqa tekshiruvi — bu amalning asosiy mazmuni.</b> Bo'limni o'zining
/// avlodiga ko'chirish daraxtda yopiq halqa hosil qiladi: A → B → A.
/// Bunday daraxtni aylanib chiquvchi har qanday kod (yo'lni hisoblash,
/// ro'yxat qurish, hisobot) cheksiz rekursiyaga tushadi. Ma'lumot buzilgandan
/// keyin uni tuzatish qo'lda bajariladigan ish, shu sababli halqa
/// <b>paydo bo'lishiga umuman yo'l qo'yilmaydi</b>.
/// </para>
/// <para>
/// <b>Nega domen o'zi tekshira olmaydi.</b> <c>Department</c> faqat o'zining
/// <c>ParentId</c> sini biladi — avlodlari kimligini bilmaydi. Buning uchun
/// butun daraxt kerak, ya'ni saqlashga murojaat. Domen esa saqlashni
/// bilmaydi. Shu sababli tekshiruv aynan shu qatlamda.
/// </para>
/// <para>
/// Ko'chirishdan keyin butun daraxtning <c>Level</c> va <c>Path</c> maydonlari
/// qayta hisoblanadi: ko'chgan bo'limning HAMMA avlodlari chuqurligini
/// o'zgartiradi, faqat bo'limning o'zi emas.
/// </para>
/// </remarks>
public sealed class MoveDepartmentCommandHandler
{
    private readonly IDepartmentRepository _departments;
    private readonly IReferenceDefinition<Department, DepartmentDto> _definition;
    private readonly IUnitOfWork _unitOfWork;

    public MoveDepartmentCommandHandler(
        IDepartmentRepository departments,
        IReferenceDefinition<Department, DepartmentDto> definition,
        IUnitOfWork unitOfWork)
    {
        _departments = departments;
        _definition = definition;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DepartmentDto>> HandleAsync(
        MoveDepartmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var department = await _departments.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (department is null)
        {
            return CatalogErrors.DepartmentNotFound;
        }

        if (command.NewParentId is { } newParentId)
        {
            // O'zini o'ziga ko'chirish — halqaning eng qisqa ko'rinishi.
            // Uni ham AYNAN SHU kod bilan qaytaramiz: foydalanuvchi uchun
            // bu bitta va o'sha xato, ikkita emas.
            if (newParentId == command.Id)
            {
                return CatalogErrors.DepartmentCycle;
            }

            var parentExists = await _departments
                .ExistsAsync(newParentId, cancellationToken)
                .ConfigureAwait(false);

            if (!parentExists)
            {
                return CatalogErrors.ParentDepartmentNotFound;
            }

            var descendants = await _departments
                .GetDescendantIdsAsync(command.Id, cancellationToken)
                .ConfigureAwait(false);

            if (descendants.Contains(newParentId))
            {
                return CatalogErrors.DepartmentCycle;
            }
        }

        var moved = department.MoveTo(command.NewParentId);
        if (moved.IsFailure)
        {
            return moved.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Faqat shundan keyin: qayta hisoblash bazadagi YANGI ota-bola
        // bog'lanishini o'qiydi, xotiradagi holatni emas.
        await _departments.RebuildHierarchyAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(_definition.ToDto(department));
    }
}
