using System.Text.Json;

namespace Bonvi.Application.Blanks.Submissions;

/// <summary>Shablon — joriy e'lon qilingan versiyasi bilan (SPEC-blankalar §7 #1).</summary>
/// <param name="Id">Shablon.</param>
/// <param name="Code">Kod (<c>HR-F06</c>).</param>
/// <param name="Title">Nomi.</param>
/// <param name="SubjectKind"><c>Employee</c> | <c>Candidate</c> | <c>None</c>.</param>
/// <param name="IsActive">Yangi blanka ochilishi mumkinmi.</param>
/// <param name="Version">Joriy e'lon qilingan versiya; yo'q bo'lsa <c>null</c>.</param>
/// <param name="VersionId">Shu versiyaning id'si.</param>
public sealed record BlankTemplateDto(
    Guid Id,
    string Code,
    string Title,
    string SubjectKind,
    bool IsActive,
    string? Version,
    Guid? VersionId);

/// <summary>Shablon versiyasi — ta'rif bilan (§7 #2).</summary>
/// <param name="Id">Versiya.</param>
/// <param name="TemplateId">Shablon.</param>
/// <param name="Code">Shablon kodi.</param>
/// <param name="Version">Versiya yozuvi.</param>
/// <param name="Status"><c>Draft</c> | <c>Published</c> | <c>Retired</c>.</param>
/// <param name="Definition">Ta'rif JSON (§4).</param>
public sealed record BlankTemplateVersionDto(
    Guid Id,
    Guid TemplateId,
    string Code,
    string Version,
    string Status,
    JsonElement Definition);

/// <summary>Rolga biriktirilgan foydalanuvchi.</summary>
/// <param name="Id">Foydalanuvchi.</param>
/// <param name="FullName">F.I.Sh.</param>
public sealed record BlankRoleUserDto(Guid Id, string FullName);

/// <summary>Ofis roli va unga biriktirilganlar (§7 #4). Bo'sh ro'yxat — har qanday <c>blanks.approve</c> egasi.</summary>
/// <param name="RoleCode">Rol (§6.1).</param>
/// <param name="Users">Biriktirilgan foydalanuvchilar.</param>
public sealed record BlankRoleDto(string RoleCode, IReadOnlyList<BlankRoleUserDto> Users);

/// <summary>Blanka kim haqida.</summary>
/// <param name="Kind"><c>Employee</c> | <c>Candidate</c> | <c>None</c>.</param>
/// <param name="Id">Xodim yoki nomzod.</param>
/// <param name="FullName">F.I.Sh.</param>
public sealed record BlankSubjectDto(string Kind, Guid? Id, string? FullName);

/// <summary>Ro'yxatdagi ochiq qadam.</summary>
/// <param name="StepCode">Qadam.</param>
/// <param name="Title">Sarlavha.</param>
/// <param name="ActorRole">Bajaruvchi roli.</param>
/// <param name="DueDate">Muddat.</param>
public sealed record BlankCurrentStepDto(string StepCode, string Title, string ActorRole, DateOnly? DueDate);

/// <summary>Ro'yxat qatori (§7 #6).</summary>
public sealed record BlankSubmissionListItemDto(
    Guid Id,
    string TemplateCode,
    string TemplateTitle,
    int? Number,
    string Status,
    BlankSubjectDto Subject,
    string? DepartmentName,
    IReadOnlyList<BlankCurrentStepDto> CurrentSteps,
    int MissingCount,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? CompletedAt);

/// <summary>Blanka qadami — barcha ustunlar, imzo kalitisiz (§7).</summary>
public sealed record BlankStepDto(
    Guid Id,
    string StepCode,
    int OrderNo,
    string Title,
    string ActorRole,
    string Mode,
    string State,
    string? Decision,
    string? Comment,
    DateOnly? DueDate,
    string? Method,
    Guid? ActedByUserId,
    string? ActedByName,
    Guid? SignerEmployeeId,
    string? SignerName,
    bool HasSignature,
    JsonElement? AnswersPatch,
    DateTimeOffset? ActedAt);

/// <summary>Biriktirilgan fayl (ombor kaliti va xeshsiz).</summary>
public sealed record BlankAttachmentDto(
    Guid Id,
    string FieldPath,
    string Kind,
    string FileName,
    long SizeBytes,
    DateTimeOffset UploadedAt);

/// <summary>Chaqiruvchi bajara oladigan qadam.</summary>
/// <param name="StepCode">Qadam.</param>
/// <param name="Electronic">Elektron tasdiqlay oladi.</param>
/// <param name="InPerson">Planshetda imzolata oladi.</param>
public sealed record BlankActionableStepDto(string StepCode, bool Electronic, bool InPerson);

/// <summary>Chaqiruvchi hozir nima qila oladi (UI tugmalari uchun; server baribir qayta tekshiradi).</summary>
public sealed record BlankCanDto(
    bool Edit,
    bool Submit,
    bool Cancel,
    IReadOnlyList<string> EditableSections,
    IReadOnlyList<BlankActionableStepDto> ActionableSteps)
{
    /// <summary>Hech narsa qilib bo'lmaydi.</summary>
    public static BlankCanDto Nothing { get; } = new(false, false, false, [], []);
}

/// <summary>Blanka to'liq ko'rinishi (§7 #8).</summary>
public sealed record BlankSubmissionDetailDto(
    Guid Id,
    Guid TemplateId,
    Guid TemplateVersionId,
    string TemplateCode,
    string TemplateTitle,
    int? Number,
    string Status,
    BlankSubjectDto Subject,
    Guid? DepartmentId,
    string? DepartmentName,
    int Version,
    JsonElement Answers,
    JsonElement Computed,
    IReadOnlyList<string> Missing,
    int MissingCount,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    string? ReturnComment,
    IReadOnlyList<BlankStepDto> Steps,
    IReadOnlyList<BlankAttachmentDto> Attachments,
    BlankCanDto Can);
