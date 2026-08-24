using Bonvi.Application.Catalog.Dtos;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Organization;

namespace Bonvi.Application.Catalog.Common;

/// <summary>Yuridik shaxs ma'lumotnomasining ta'rifi.</summary>
public sealed class LegalEntityDefinition : ReferenceDefinition<LegalEntity, LegalEntityDto>
{
    /// <inheritdoc />
    public override LegalEntityDto ToDto(LegalEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new LegalEntityDto(
            entity.Id,
            entity.Name,
            entity.IsActive,
            entity.Note,
            entity.TaxId,
            entity.FullName);
    }

    /// <inheritdoc />
    public override Result<LegalEntity> Create(SaveReferenceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return LegalEntity.Create(command.Name);
    }

    /// <inheritdoc />
    protected override Result ApplySpecific(LegalEntity entity, SaveReferenceCommand command)
    {
        // STIR formati domenda tekshiriladi (9 xona) — bu yerda takrorlanmaydi.
        var taxId = entity.SetTaxId(command.TaxId);
        if (taxId.IsFailure)
        {
            return taxId;
        }

        entity.SetFullName(command.FullName);
        return Result.Success();
    }
}

/// <summary>Yo'nalish ma'lumotnomasining ta'rifi.</summary>
public sealed class DirectionDefinition : ReferenceDefinition<Direction, DirectionDto>
{
    /// <inheritdoc />
    public override DirectionDto ToDto(Direction entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new DirectionDto(
            entity.Id,
            entity.Name,
            entity.IsActive,
            entity.Note,
            entity.Color);
    }

    /// <inheritdoc />
    public override Result<Direction> Create(SaveReferenceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Direction.Create(command.Name);
    }

    /// <inheritdoc />
    protected override Result ApplySpecific(Direction entity, SaveReferenceCommand command)
        => entity.SetColor(command.Color);
}

/// <summary>Bo'lim ma'lumotnomasining ta'rifi.</summary>
/// <remarks>
/// Ota bo'lim shu yerda O'ZGARTIRILMAYDI: ko'chirish alohida amal, chunki u
/// halqa tekshiruvi va butun daraxtni qayta hisoblashni talab qiladi.
/// Tahrirlash formasi orqali tasodifan ko'chirib yuborish mumkin emas.
/// </remarks>
public sealed class DepartmentDefinition : ReferenceDefinition<Department, DepartmentDto>
{
    /// <inheritdoc />
    public override DepartmentDto ToDto(Department entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new DepartmentDto(
            entity.Id,
            entity.Name,
            entity.IsActive,
            entity.Note,
            entity.ParentId,
            entity.Level,
            entity.Path,
            entity.LegalEntityId,
            entity.HeadEmployeeId);
    }

    /// <inheritdoc />
    public override Result<Department> Create(SaveReferenceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Department.Create(command.Name, command.ParentId);
    }

    /// <inheritdoc />
    protected override Result ApplySpecific(Department entity, SaveReferenceCommand command)
    {
        entity.AssignLegalEntity(command.LegalEntityId);
        entity.AssignHead(command.HeadEmployeeId);
        return Result.Success();
    }
}

/// <summary>Lavozim ma'lumotnomasining ta'rifi.</summary>
public sealed class PositionDefinition : ReferenceDefinition<Position, PositionDto>
{
    /// <summary>Vazifalarni tartiblab DTO'ga o'giradi.</summary>
    /// <remarks>
    /// Tartib bu yerda ATAYLAB qayta qo'llanadi: repozitoriy vazifalarni
    /// qaysi tartibda yuklashi kafolatlanmagan, foydalanuvchi esa ularni
    /// aynan o'zi qo'ygan ketma-ketlikda ko'rishi kerak.
    /// </remarks>
    public static IReadOnlyList<PositionDutyDto> ToDuties(Position entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return
        [
            .. entity.Duties
                .OrderBy(d => d.SortOrder)
                .Select(d => new PositionDutyDto(d.Id, d.Text, d.SortOrder)),
        ];
    }

    /// <summary>Vazifalari bilan to'liq ko'rinish.</summary>
    public static PositionDetailsDto ToDetails(Position entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new PositionDetailsDto(
            entity.Id,
            entity.Name,
            entity.IsActive,
            entity.Note,
            entity.DepartmentId,
            entity.Grade,
            entity.ProbationDays,
            ToDuties(entity));
    }

    /// <inheritdoc />
    public override PositionDto ToDto(Position entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new PositionDto(
            entity.Id,
            entity.Name,
            entity.IsActive,
            entity.Note,
            entity.DepartmentId,
            entity.Grade,
            entity.ProbationDays);
    }

    /// <inheritdoc />
    public override Result<Position> Create(SaveReferenceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Position.Create(command.Name, command.DepartmentId);
    }

    /// <inheritdoc />
    protected override Result ApplySpecific(Position entity, SaveReferenceCommand command)
    {
        entity.AssignDepartment(command.DepartmentId);

        if (command.Grade is { } grade)
        {
            var result = entity.SetGrade(grade);
            if (result.IsFailure)
            {
                return result;
            }
        }

        // Sinov muddati ATAYLAB «null bo'lsa ham qo'llanadi»: null — bu
        // «umumiy qoida (90 kun) ishlatilsin» degani, ya'ni haqiqiy qiymat.
        return entity.SetProbationDays(command.ProbationDays);
    }
}

/// <summary>Ishdan ketish sababi ma'lumotnomasining ta'rifi.</summary>
public sealed class TerminationReasonDefinition : ReferenceDefinition<TerminationReason, TerminationReasonDto>
{
    /// <inheritdoc />
    public override TerminationReasonDto ToDto(TerminationReason entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new TerminationReasonDto(
            entity.Id,
            entity.Name,
            entity.IsActive,
            entity.Note,
            entity.IsVoluntary,
            entity.IsRehireable);
    }

    /// <inheritdoc />
    public override Result<TerminationReason> Create(SaveReferenceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return TerminationReason.Create(command.Name);
    }

    /// <inheritdoc />
    protected override Result ApplySpecific(TerminationReason entity, SaveReferenceCommand command)
    {
        if (command.IsVoluntary is { } voluntary)
        {
            entity.SetVoluntary(voluntary);
        }

        if (command.IsRehireable is { } rehireable)
        {
            entity.SetRehireable(rehireable);
        }

        return Result.Success();
    }
}

/// <summary>Rag'bat turi ma'lumotnomasining ta'rifi.</summary>
public sealed class AwardTypeDefinition : ReferenceDefinition<AwardType, AwardTypeDto>
{
    /// <inheritdoc />
    public override AwardTypeDto ToDto(AwardType entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new AwardTypeDto(
            entity.Id,
            entity.Name,
            entity.IsActive,
            entity.Note,
            entity.HasAmount);
    }

    /// <inheritdoc />
    public override Result<AwardType> Create(SaveReferenceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return AwardType.Create(command.Name);
    }

    /// <inheritdoc />
    protected override Result ApplySpecific(AwardType entity, SaveReferenceCommand command)
    {
        if (command.HasAmount is { } hasAmount)
        {
            entity.SetHasAmount(hasAmount);
        }

        return Result.Success();
    }
}

/// <summary>Jarima turi ma'lumotnomasining ta'rifi.</summary>
public sealed class PenaltyTypeDefinition : ReferenceDefinition<PenaltyType, PenaltyTypeDto>
{
    /// <inheritdoc />
    public override PenaltyTypeDto ToDto(PenaltyType entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new PenaltyTypeDto(
            entity.Id,
            entity.Name,
            entity.IsActive,
            entity.Note,
            entity.HasAmount,
            entity.Severity);
    }

    /// <inheritdoc />
    public override Result<PenaltyType> Create(SaveReferenceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PenaltyType.Create(command.Name);
    }

    /// <inheritdoc />
    protected override Result ApplySpecific(PenaltyType entity, SaveReferenceCommand command)
    {
        if (command.HasAmount is { } hasAmount)
        {
            entity.SetHasAmount(hasAmount);
        }

        return command.Severity is { } severity
            ? entity.SetSeverity(severity)
            : Result.Success();
    }
}

/// <summary>Hujjat turi ma'lumotnomasining ta'rifi.</summary>
public sealed class DocumentTypeDefinition : ReferenceDefinition<DocumentType, DocumentTypeDto>
{
    /// <inheritdoc />
    public override DocumentTypeDto ToDto(DocumentType entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new DocumentTypeDto(
            entity.Id,
            entity.Name,
            entity.IsActive,
            entity.Note,
            entity.IsRequired);
    }

    /// <inheritdoc />
    public override Result<DocumentType> Create(SaveReferenceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return DocumentType.Create(command.Name);
    }

    /// <inheritdoc />
    protected override Result ApplySpecific(DocumentType entity, SaveReferenceCommand command)
    {
        if (command.IsRequired is { } required)
        {
            entity.SetRequired(required);
        }

        return Result.Success();
    }
}
