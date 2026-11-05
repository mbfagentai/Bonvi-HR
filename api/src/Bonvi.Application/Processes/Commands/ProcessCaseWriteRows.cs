using Bonvi.Application.Processes.Queries;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Commands;

/// <summary>
/// The aggregate as the rows <see cref="ProcessStepAbility"/> takes, so a write answers exactly what the screen's
/// buttons promised, from the state the write will change.
/// </summary>
internal static class ProcessCaseWriteRows
{
    public static ProcessCaseRow CaseRow(ProcessCase processCase)
        => new(
            processCase.Id,
            processCase.ProcessCode,
            processCase.TemplateVersionId,
            processCase.Status,
            processCase.Outcome,
            processCase.Legacy,
            processCase.SubjectKind,
            processCase.VacancyRequestId,
            processCase.CandidateId,
            processCase.EmploymentId,
            processCase.ProbationId,
            processCase.EmployeeId,
            processCase.DepartmentId,
            processCase.ParentCaseId,
            processCase.AnchorDate,
            processCase.StartedOn,
            processCase.CompletedAt,
            processCase.CancelledAt,
            processCase.CancelReason,
            processCase.Version);

    public static ProcessStepRow StepRow(ProcessCaseStep step)
        => new(
            step.CaseId,
            step.StepCode,
            step.OrderNo,
            step.BoardColumn,
            step.State,
            step.Resolution,
            step.HolderRole,
            step.HolderEmployeeId,
            step.DueDate,
            step.DoneAt,
            step.DoneBy,
            step.Payload,
            step.BlockedGateCode,
            step.LinkedBlankSubmissionId,
            step.LinkedRecordId);
}
