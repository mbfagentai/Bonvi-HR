using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Competencies;

/// <summary>
/// Xodim qobiliyatlar profilini saqlaydi (yaratadi yoki bor bo'lsa yangilaydi).
/// </summary>
/// <remarks>
/// Bitta xodimda bitta profil bo'lgani uchun bu <b>upsert</b>: forma
/// har doim BUTUN profilni yuboradi (91.15 varag'idagi bitta qator kabi),
/// alohida «yaratish» va «tahrirlash» amallariga bo'linmaydi.
/// </remarks>
public sealed record SaveEmployeeCompetencyCommand(
    Guid EmployeeId,
    string? Languages = null,
    IReadOnlyList<LanguageSkill>? LanguageSkills = null,
    int? ComputerLiteracyPercent = null,
    string? Specialization = null,
    string? OtherAbilities = null,
    string? Interest = null,
    string? EducationPlace = null);

public sealed class SaveEmployeeCompetencyCommandHandler
{
    private readonly IEmployeeCompetencyRepository _competencies;
    private readonly IEmployeeRepository _employees;
    private readonly IUnitOfWork _unitOfWork;

    public SaveEmployeeCompetencyCommandHandler(
        IEmployeeCompetencyRepository competencies,
        IEmployeeRepository employees,
        IUnitOfWork unitOfWork)
    {
        _competencies = competencies;
        _employees = employees;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeCompetencyDto>> HandleAsync(
        SaveEmployeeCompetencyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var exists = await _employees.ExistsAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return EmployeeErrors.NotFound;
        }

        var competency = await _competencies
            .GetByEmployeeAsync(command.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        if (competency is null)
        {
            var created = EmployeeCompetency.Create(command.EmployeeId);
            if (created.IsFailure)
            {
                return created.Error;
            }

            competency = created.Value;
            _competencies.Add(competency);
        }

        var percent = competency.SetComputerLiteracy(command.ComputerLiteracyPercent);
        if (percent.IsFailure)
        {
            return percent.Error;
        }

        // `null` va bo'sh ro'yxat BIR XIL o'qiladi — «tillar yo'q».
        // Profil butunligicha almashtiriladi, «tegilmadi» degan holat
        // boshqa maydonlarda ham yo'q (`Languages = null` matnni bo'shatadi).
        var tillar = competency.SetLanguageSkills(command.LanguageSkills ?? []);
        if (tillar.IsFailure)
        {
            return tillar.Error;
        }

        competency.SetLanguages(command.Languages);
        competency.SetSpecialization(command.Specialization);
        competency.SetOtherAbilities(command.OtherAbilities);
        competency.SetInterest(command.Interest);
        competency.SetEducationPlace(command.EducationPlace);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(EmployeeMapper.ToDto(competency));
    }
}
