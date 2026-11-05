using System.Text.Json;
using Bonvi.Application.Notifications.Dtos;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Inputs;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Queries;

/*
  Responses of GET /api/processes/* (docs/SPEC-mening-ishlarim.md §10 #1–#8). The web client is generated from these
  records: a renamed field is a contract change. Enums go out as their names (the API's JsonStringEnumConverter).
*/

/// <summary>Who or what a case is about, as a card shows it: «{Name} · {Position}».</summary>
/// <remarks>
/// <para><c>Kind</c>: Which of the ids below names the subject.</para>
/// <para><c>Name</c>: Employee or candidate name, or the request's title; <c>null</c> if the subject was deleted.</para>
/// <para><c>Position</c>: Employment's position, or the title of the candidate's vacancy.</para>
/// </remarks>
public sealed record ProcessSubjectDto(
    ProcessSubjectKind Kind,
    string? Name,
    string? Position,
    Guid? VacancyRequestId,
    Guid? CandidateId,
    Guid? EmploymentId,
    Guid? ProbationId,
    Guid? EmployeeId);

public sealed record ProcessUserNameDto(Guid UserId, string Name);

/// <summary>Who holds a step or the thing a Blocked step misses (§7.7).</summary>
/// <remarks>
/// <para><c>Role</c>: §9.1 code; <c>null</c> = «Tizim».</para>
/// <para><c>EmployeeId</c>: The resolved person of a person role.</para>
/// <para><c>EmployeeName</c>: That person's name.</para>
/// <para><c>Users</c>: Users assigned to an office role (HR-5/HR-8 unassigned → those of HR-1).</para>
/// <para><c>Unassigned</c>: An office role nobody is assigned to: every <c>processes.manage</c> holder sees it.</para>
/// </remarks>
public sealed record ProcessHolderDto(
    string? Role,
    Guid? EmployeeId,
    string? EmployeeName,
    IReadOnlyList<ProcessUserNameDto> Users,
    bool Unassigned);

public sealed record ProcessLinkDto(string Screen, IReadOnlyDictionary<string, string> Args);

/// <summary>One step on the viewer's list or in «Kim kechikdi».</summary>
/// <remarks>
/// <para><c>DaysLate</c>: Whole days past <paramref name="DueDate"/>; 0 if not late or no due date.</para>
/// <para><c>GateCode</c>: §8.1 code of a Blocked step.</para>
/// <para><c>GateErrorCode</c>: The <c>jarayon.*</c> code whose translation is the gate's text.</para>
/// <para><c>Resolver</c>: Holder of the missing thing of a Blocked step (§8.3).</para>
/// <para><c>Warnings</c>: §8.1 warn codes firing now (Q02, Q05, Q06, AKT).</para>
/// </remarks>
public sealed record ProcessTaskDto(
    Guid CaseId,
    string ProcessCode,
    string ProcessTitle,
    string StepCode,
    string StepTitle,
    ProcessStepState State,
    DateOnly? DueDate,
    int DaysLate,
    ProcessSubjectDto Subject,
    Guid? DepartmentId,
    string? DepartmentName,
    string? BlankCode,
    string? BlankTitle,
    Guid? LinkedBlankSubmissionId,
    bool Legacy,
    string? GateCode,
    string? GateErrorCode,
    ProcessHolderDto Holder,
    ProcessHolderDto? Resolver,
    IReadOnlyList<string> Warnings,
    ProcessLinkDto Link);

/// <summary>A count card of §11.8.</summary>
/// <remarks>
/// <para><c>Key</c>: <c>unmatchedTerminalPersons</c>, <c>employeesWithoutPhoto</c>, <c>employeesWithoutSchedule</c> or <c>employeesWithIncompleteFiles</c>.</para>
/// <para><c>Count</c>: Always above zero: an empty card is not sent.</para>
/// </remarks>
public sealed record ProcessTartibCountDto(string Key, int Count);

/// <summary>«Tartib» (§11.8): read on request, not stored; each part appears only under its own permission.</summary>
public sealed record ProcessTartibDto(
    IReadOnlyList<NotificationDto> Notifications,
    IReadOnlyList<ProcessTartibCountDto> Counts);

/// <summary>#1 — tab «Mening ishlarim» (§11.2).</summary>
/// <remarks>
/// <para><c>Today</c>: Tashkent date the sections were cut on.</para>
/// <para><c>Count</c>: Urgent + later: the same number as #2.</para>
/// <para><c>LateCount</c>: Of those, past their due date.</para>
/// <para><c>Urgent</c>: Due today or late, and Blocked steps whose missing thing is the viewer's own.</para>
/// <para><c>Later</c>: Active, due later or with no due date.</para>
/// <para><c>Held</c>: The viewer's Blocked steps whose missing thing someone else holds.</para>
/// <para><c>Waiting</c>: The viewer's Locked steps due within 7 days, an open prerequisite of which is held by someone else.</para>
/// <para>
/// Each section returns at most <see cref="ProcessWorkSections.MaxSectionRows"/> cards, the soonest first.
/// <c>UrgentTotal</c>, <c>LaterTotal</c>, <c>HeldTotal</c>, <c>WaitingTotal</c> are the sizes before that cut; <c>Count</c>
/// and <c>LateCount</c> are over everything too.
/// </para>
/// </remarks>
public sealed record ProcessMyWorkDto(
    DateOnly Today,
    int Count,
    int LateCount,
    IReadOnlyList<ProcessTaskDto> Urgent,
    IReadOnlyList<ProcessTaskDto> Later,
    IReadOnlyList<ProcessTaskDto> Held,
    IReadOnlyList<ProcessTaskDto> Waiting,
    ProcessTartibDto Tartib,
    int UrgentTotal,
    int LaterTotal,
    int HeldTotal,
    int WaitingTotal);

/// <summary>#2 — the menu badge.</summary>
public sealed record ProcessMyCountDto(int Count);

/// <summary>A step a case is on now (Active, Blocked, Waiting).</summary>
public sealed record ProcessCurrentStepDto(
    string StepCode,
    string Title,
    string Column,
    ProcessStepState State,
    ProcessHolderDto Holder,
    DateOnly? DueDate,
    int DaysLate,
    string? GateCode,
    IReadOnlyList<string> Warnings);

/// <summary>#3 — one row of the case list.</summary>
/// <remarks>
/// <para><c>DaysLate</c>: The latest of <paramref name="CurrentSteps"/>.</para>
/// </remarks>
public sealed record ProcessCaseListItemDto(
    Guid Id,
    string ProcessCode,
    string ProcessTitle,
    ProcessCaseStatus Status,
    string? Outcome,
    bool Legacy,
    ProcessSubjectDto Subject,
    Guid? DepartmentId,
    string? DepartmentName,
    DateOnly StartedOn,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    IReadOnlyList<ProcessCurrentStepDto> CurrentSteps,
    int DaysLate);

public sealed record ProcessBoardCardDto(
    Guid CaseId,
    ProcessSubjectDto Subject,
    Guid? DepartmentId,
    string? DepartmentName,
    bool Legacy,
    IReadOnlyList<ProcessCurrentStepDto> CurrentSteps,
    DateOnly? DueDate,
    int DaysLate);

public sealed record ProcessBoardColumnDto(string Key, string Title, IReadOnlyList<ProcessBoardCardDto> Cards);

/// <summary>#4 — running cases of one process; a card stands in the column of its earliest open step.</summary>
public sealed record ProcessBoardDto(string ProcessCode, string ProcessTitle, IReadOnlyList<ProcessBoardColumnDto> Columns);

public sealed record ProcessOptionDto(string Value, string Label);

public sealed record ProcessRequiredWhenDto(string? Input, string? Fact, string EqualsValue);

public sealed record ProcessManualInputDto(
    string Id,
    string Label,
    ProcessInputType Type,
    bool Required,
    ProcessRequiredWhenDto? RequiredWhen,
    IReadOnlyList<ProcessOptionDto> Options,
    int? Min,
    int? Max,
    IReadOnlyList<ProcessAutoItemDto>? AutoItems);

/// <summary>A checklist item the system answers from the subject's records; the client's mark on it is not read.</summary>
/// <remarks>
/// <para><c>Value</c>: The option's value in <c>Options</c>.</para>
/// <para><c>AutoValue</c>: The mark «Bajarildi» stores: <c>done</c> when Known, <c>na</c> when NotApplicable, <c>null</c> when Missing.</para>
/// <para><c>AutoSource</c>: Known only: where it was found; the screen words it.</para>
/// <para><c>SourceDetail</c>: Known only: the document type's or the step's name.</para>
/// <para><c>Reason</c>: NotApplicable only: why.</para>
/// <para><c>Fill</c>: Missing only: where «To'ldirish» goes.</para>
/// </remarks>
public sealed record ProcessAutoItemDto(
    string Value,
    ProcessAutoItemState State,
    string? AutoValue,
    ProcessAutoSource? AutoSource,
    string? SourceDetail,
    ProcessAutoNotApplicableReason? Reason,
    ProcessInputFill? Fill);

public sealed record ProcessManualDto(bool Allowed, IReadOnlyList<ProcessManualInputDto> Inputs);

/// <summary>One step of a case. Definition fields are empty if the case's template version does not load.</summary>
/// <remarks>
/// <para><c>ViewerHolds</c>: The viewer is the resolved holder (§7.7); if not — «Bu qadamni {role} bajaradi».</para>
/// <para><c>CanComplete</c>: «Bajarildi» would be accepted: Active, closable by hand, holder or manager (§9.3).</para>
/// <para><c>CanSkip</c>: «Kerak emas» would be accepted (skippable, or legacySkippable in a legacy case).</para>
/// <para><c>CanUndo</c>: Undo would be accepted: own user-closed step within 24 h, or manager.</para>
/// </remarks>
public sealed record ProcessCaseStepDto(
    string StepCode,
    int OrderNo,
    string Title,
    string Column,
    ProcessStepKind? Kind,
    ProcessStepState State,
    ProcessStepResolution? Resolution,
    ProcessHolderDto Holder,
    DateOnly? DueDate,
    int DaysLate,
    DateTimeOffset? DoneAt,
    Guid? DoneBy,
    string? DoneByName,
    JsonElement? Payload,
    string? BlankCode,
    string? BlankTitle,
    string? Signature,
    Guid? LinkedBlankSubmissionId,
    Guid? LinkedRecordId,
    string? GateCode,
    string? GateErrorCode,
    ProcessHolderDto? Resolver,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Prereqs,
    bool Optional,
    IReadOnlyList<string> Guide,
    string? SourceRef,
    ProcessLinkDto Link,
    ProcessManualDto? Manual,
    bool ViewerHolds,
    bool CanComplete,
    bool CanSkip,
    bool CanUndo);

/// <summary>A blank a step of the case reads.</summary>
/// <remarks>
/// <para><c>Status</c>: Name of <c>BlankSubmissionStatus</c>.</para>
/// </remarks>
public sealed record ProcessLinkedBlankDto(
    Guid Id,
    string TemplateCode,
    string? TemplateTitle,
    int? Number,
    string Status,
    DateOnly? DueDate,
    DateTimeOffset? CompletedAt);

public sealed record ProcessColumnDto(string Key, string Title);

/// <summary>#5 — the case screen (§11.5).</summary>
/// <remarks>
/// <para><c>Version</c>: Optimistic concurrency token of the write endpoints.</para>
/// <para><c>Columns</c>: Board columns of the case's own template version.</para>
/// </remarks>
public sealed record ProcessCaseDetailDto(
    Guid Id,
    string ProcessCode,
    string ProcessTitle,
    string? Source,
    ProcessCaseStatus Status,
    string? Outcome,
    bool Legacy,
    ProcessSubjectDto Subject,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? ParentCaseId,
    DateOnly? AnchorDate,
    DateOnly StartedOn,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    int Version,
    IReadOnlyList<ProcessColumnDto> Columns,
    IReadOnlyList<ProcessCaseStepDto> Steps,
    IReadOnlyList<ProcessLinkedBlankDto> Blanks);

/// <summary>A gate override of the last 30 days («Kim kechikdi»).</summary>
public sealed record ProcessGateOverrideRowDto(
    Guid Id,
    string GateCode,
    Guid? CaseId,
    string? StepCode,
    Guid? EmploymentId,
    Guid? CandidateId,
    Guid? ForUserId,
    string? ForUserName,
    string Reason,
    Guid GrantedBy,
    string? GrantedByName,
    DateTimeOffset GrantedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConsumedAt,
    string? ConsumedOperation);

/// <summary>#6 — tab «Kim kechikdi» (§11.4).</summary>
/// <remarks>
/// <para><c>Blocked</c>: Every Blocked step; the reason is <c>GateCode</c>.</para>
/// <para><c>Late</c>: Every Active step past its due date.</para>
/// <para><c>Warn</c>: Every open step with a firing warn gate (it may also be in <paramref name="Late"/>); the reason is <c>Warnings</c>.</para>
/// </remarks>
public sealed record ProcessLateDto(
    IReadOnlyList<ProcessTaskDto> Blocked,
    IReadOnlyList<ProcessTaskDto> Late,
    IReadOnlyList<ProcessTaskDto> Warn,
    IReadOnlyList<ProcessGateOverrideRowDto> Overrides);

public sealed record ProcessTemplateGateDto(string Code, ProcessGateMode Mode);

public sealed record ProcessTemplateStepDto(
    string Id,
    string Title,
    string Column,
    string Role,
    string? FallbackRole,
    string? Blank,
    string? Signature,
    ProcessStepKind Kind,
    IReadOnlyList<string> Prereqs,
    bool Optional,
    bool Skippable,
    bool LegacySkippable,
    IReadOnlyList<ProcessTemplateGateDto> Gates,
    ProcessManualDto? Manual,
    IReadOnlyList<string> Guide,
    string? SourceRef,
    ProcessLinkDto Link);

/// <summary>#7 — a published definition.</summary>
public sealed record ProcessTemplateDto(
    string Code,
    string Version,
    string Title,
    string Source,
    ProcessSubjectKind SubjectKind,
    IReadOnlyList<ProcessColumnDto> Board,
    IReadOnlyList<ProcessTemplateStepDto> Steps);

/// <summary>#8 — engine state.</summary>
/// <remarks>
/// <para><c>Engine</c>: <c>dormant</c> or <c>active</c> (§7.6).</para>
/// <para><c>StartedOn</c>: The date the engine went live.</para>
/// <para><c>GatesMode</c>: <c>enforce</c>, <c>warn</c> or <c>off</c>.</para>
/// <para><c>CouncilWeekday</c>: ISO weekday of the hiring council.</para>
/// <para><c>LastPassAt</c>: When the last full pass ended; <c>null</c> if none ran.</para>
/// </remarks>
public sealed record ProcessStatusDto(
    string Engine,
    DateOnly? StartedOn,
    string GatesMode,
    int CouncilWeekday,
    DateTimeOffset? LastPassAt);
