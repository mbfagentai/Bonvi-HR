using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Queries;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Tests.Processes.Queries;

/// <summary>The read rows and definitions of these tests: everything a test does not name is the plain default.</summary>
internal static class ProcessReadRows
{
    public static ProcessStepRow Step(
        ProcessStepState state = ProcessStepState.Active,
        string? role = ProcessRoles.Hr2,
        Guid? holderEmployeeId = null,
        DateOnly? due = null,
        int orderNo = 1,
        Guid? caseId = null,
        ProcessStepResolution? resolution = null,
        DateTimeOffset? doneAt = null,
        Guid? doneBy = null)
        => new(
            caseId ?? Guid.NewGuid(),
            "s-" + orderNo,
            orderNo,
            "column",
            state,
            resolution,
            role,
            holderEmployeeId,
            due,
            doneAt,
            doneBy,
            Payload: null,
            BlockedGateCode: null,
            LinkedBlankSubmissionId: null,
            LinkedRecordId: null);

    public static ProcessCaseRow Case(ProcessCaseStatus status = ProcessCaseStatus.Active, bool legacy = false)
        => new(
            Guid.NewGuid(),
            ProcessCodes.HireFormalise,
            Guid.NewGuid(),
            status,
            Outcome: null,
            legacy,
            ProcessSubjectKind.Employment,
            VacancyRequestId: null,
            CandidateId: null,
            EmploymentId: Guid.NewGuid(),
            ProbationId: null,
            EmployeeId: Guid.NewGuid(),
            DepartmentId: null,
            ParentCaseId: null,
            AnchorDate: null,
            StartedOn: new DateOnly(2026, 11, 2),
            CompletedAt: null,
            CancelledAt: null,
            CancelReason: null,
            Version: 1);

    public static ProcessStep Definition(
        ProcessStepKind kind = ProcessStepKind.Manual,
        ProcessManual? manual = null,
        bool skippable = false,
        bool legacySkippable = false,
        params ProcessGateRef[] gates)
        => new()
        {
            Id = "s-1",
            Title = "Step",
            Column = "column",
            Role = ProcessRoles.Hr2,
            Kind = kind,
            Manual = manual,
            Skippable = skippable,
            LegacySkippable = legacySkippable,
            Gates = gates,
            Link = new ProcessLink("screen", new Dictionary<string, string>()),
        };

    public static ProcessViewer Viewer(
        Guid? userId = null,
        Guid? employeeId = null,
        bool canWork = true,
        bool canManage = false,
        params string[] officeRoles)
        => new(userId ?? Guid.NewGuid(), employeeId, officeRoles.ToHashSet(StringComparer.Ordinal), canWork, canManage);
}
