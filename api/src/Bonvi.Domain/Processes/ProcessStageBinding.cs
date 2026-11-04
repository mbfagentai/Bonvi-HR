using Bonvi.Domain.Common;

namespace Bonvi.Domain.Processes;

/// <summary>
/// Which recruiting pipeline stage means «contacted», «invited», «council», «practical test»
/// (<c>process_stage_bindings</c>), docs/SPEC-mening-ishlarim.md §6.5.
/// </summary>
/// <remarks>
/// The owner binds them on the settings page (§11.7). Unbound → the stage fact is false and the step
/// closes by its manual fallback only. The SPEC's natural key is <see cref="BindingCode"/>; the row
/// still has a <see cref="Entity.Id"/> because every audited entity is keyed by one Guid
/// (<c>AuditCatalogCoverageTests</c>) — <c>binding_code</c> is UNIQUE.
/// </remarks>
public sealed class ProcessStageBinding : AggregateRoot
{
    public const string Contacted = "contacted";
    public const string Invited = "invited";
    public const string Council = "council";
    public const string PracticalTest = "practicalTest";

    /// <summary>Column length.</summary>
    public const int MaxCodeLength = 32;

    /// <summary>Every binding code — settings page order.</summary>
    public static IReadOnlyList<string> Codes { get; } = [Contacted, Invited, Council, PracticalTest];

    private ProcessStageBinding(Guid id, string bindingCode, Guid? pipelineStageId)
        : base(id)
    {
        BindingCode = bindingCode;
        PipelineStageId = pipelineStageId;
    }

    private ProcessStageBinding() { }

    /// <summary>One of <see cref="Codes"/>.</summary>
    public string BindingCode { get; private set; } = string.Empty;

    /// <summary>The bound pipeline stage; <c>null</c> — unbound.</summary>
    public Guid? PipelineStageId { get; private set; }

    public static Result<ProcessStageBinding> Create(string? bindingCode, Guid? pipelineStageId)
    {
        if (bindingCode is null || !Codes.Contains(bindingCode, StringComparer.Ordinal) || pipelineStageId == Guid.Empty)
        {
            return ProcessErrors.Invalid;
        }

        return new ProcessStageBinding(NewId(), bindingCode, pipelineStageId);
    }

    /// <summary>Binds (or with <c>null</c> unbinds) the stage.</summary>
    public Result Bind(Guid? pipelineStageId)
    {
        if (pipelineStageId == Guid.Empty)
        {
            return ProcessErrors.Invalid;
        }

        PipelineStageId = pipelineStageId;
        return Result.Success();
    }
}
