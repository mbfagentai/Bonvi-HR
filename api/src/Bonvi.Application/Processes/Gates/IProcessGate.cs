using Bonvi.Application.Employees.Common;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Processes.Gates;

/// <summary>
/// The hook gates (docs/SPEC-mening-ishlarim.md §8.2): one call inside the Hire and Terminate handlers,
/// next to <c>EmployeeFlagGuard</c>. Evaluates the facts at call time, not the reconciled state.
/// </summary>
/// <remarks>
/// A usable override is consumed on the handler's tracked entities, so it is written by the handler's own
/// <c>SaveChangesAsync</c>: a command that fails after this call leaves the override unused.
/// </remarks>
public interface IProcessGate
{
    Task<GateResult> CheckAsync(GateContext context, CancellationToken cancellationToken = default);
}

/// <summary>The guarded command.</summary>
public enum GateOperation
{
    Hire,
    Terminate,
}

/// <summary>What the guarded command is about to do.</summary>
/// <remarks>
/// The acting user of §8.2 is not here: the gate reads it from <c>ICurrentUser</c>, so the handlers gain one
/// dependency and one call, not two.
/// </remarks>
public sealed record GateContext
{
    public required GateOperation Operation { get; init; }

    public required Guid EmployeeId { get; init; }

    public Guid? CandidateId { get; init; }

    public Guid? EmploymentId { get; init; }

    public Guid? LegalEntityId { get; init; }

    public DateOnly? TerminationDate { get; init; }

    /// <summary>The hire order number: Q01 warns when it is missing.</summary>
    public string? OrderNo { get; init; }

    public static GateContext ForHire(Guid? candidateId, Guid employeeId, Guid legalEntityId, string? orderNo)
        => new()
        {
            Operation = GateOperation.Hire,
            CandidateId = candidateId,
            EmployeeId = employeeId,
            LegalEntityId = legalEntityId,
            OrderNo = orderNo,
        };

    public static GateContext ForTerminate(Guid employmentId, Guid employeeId, Guid legalEntityId, DateOnly terminationDate)
        => new()
        {
            Operation = GateOperation.Terminate,
            EmploymentId = employmentId,
            EmployeeId = employeeId,
            LegalEntityId = legalEntityId,
            TerminationDate = terminationDate,
        };
}

/// <summary>Allow, Warn or Block (§8.2).</summary>
public sealed class GateResult
{
    private GateResult(Error? blocking, IReadOnlyList<HrWarning> warnings)
    {
        Blocking = blocking;
        Warnings = warnings;
    }

    public static GateResult Allow { get; } = new(null, []);

    /// <summary>The 409 to return, carrying every other missing thing in <see cref="Error.Related"/>; <c>null</c> → go on.</summary>
    public Error? Blocking { get; }

    /// <summary>What the command adds to its <see cref="HrWarning"/> list.</summary>
    public IReadOnlyList<HrWarning> Warnings { get; }

    public static GateResult Warn(IReadOnlyList<HrWarning> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);

        return warnings.Count == 0 ? Allow : new GateResult(null, warnings);
    }

    public static GateResult Block(IReadOnlyList<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new GateResult(errors[0].With([.. errors.Select(error => error.Code)]), []);
    }
}
