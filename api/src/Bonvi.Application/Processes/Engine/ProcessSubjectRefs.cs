using System.Globalization;
using Bonvi.Domain.Abstractions;

namespace Bonvi.Application.Processes.Engine;

/// <summary>Process codes of Phase 1 (docs/SPEC-mening-ishlarim.md §3.2).</summary>
public static class ProcessCodes
{
    public const string HireRequest = "hire-request";
    public const string HireCandidate = "hire-candidate";
    public const string HireFormalise = "hire-formalise";
    public const string Probation = "probation";
    public const string Dismissal = "dismissal";
}

/// <summary>Step ids the engine itself reads — every other step is driven by its definition alone.</summary>
public static class ProcessStepIds
{
    /// <summary>Request closed: carries the days-to-close payload (§3.3).</summary>
    public const string RequestClosed = "r02-14";

    /// <summary>Offer: its accept branch starts hire-formalise (a), its decline branch ends the case.</summary>
    public const string Offer = "r02-11";

    /// <summary>First-day step of the candidate case; Q04 gate; with the hire it completes the case.</summary>
    public const string FirstDay = "r02-13";

    /// <summary>Safety instruction of the formalise case, read by Q04.</summary>
    public const string SafetyInstruction = "mm";

    /// <summary>Payment input step, Q03 gate.</summary>
    public const string PayrollHandover = "r01-9";

    /// <summary>Final settlement, engine part of Q12.</summary>
    public const string FinalSettlement = "ex-10";

    /// <summary>The application of a dismissal: a manual start records it here (§3.7).</summary>
    public const string DismissalApplication = "ex-1";

    /// <summary>F09 opened.</summary>
    public const string ActOpened = "ex-2";

    /// <summary>Dismissal recorded.</summary>
    public const string Terminated = "ex-8";

    /// <summary>The act entered in the register: a dismissal whose steps all closed with this one Done is <c>terminated</c>.</summary>
    public const string ActRegistered = "ex-11";
}

/// <summary>Case outcomes the engine writes (§6.2, §6.9, §7.5).</summary>
public static class ProcessOutcomes
{
    public const string Hired = "hired";
    public const string Declined = "declined";
    public const string Reapplied = "reapplied";
    public const string Filled = "filled";
    public const string Terminated = "terminated";
    public const string Withdrawn = "withdrawn";
}

/// <summary>
/// <c>subject_ref</c> formats (§3.2). The reference, not a timestamp column, carries a candidate's
/// application date: one candidate has one case per application.
/// </summary>
public static class ProcessSubjectRefs
{
    private const string DateFormat = "yyyyMMdd";
    private const string CandidatePrefix = "c:";
    private const string FormaliseCandidatePrefix = "hf:c:";
    private const string FormaliseEmploymentPrefix = "hf:e:";

    public static string VacancyRequest(Guid id) => $"vr:{id}";

    public static string Candidate(Guid id, DateOnly appliedOn) => $"{CandidatePrefix}{id}:{Format(appliedOn)}";

    public static string FormaliseFromCandidate(Guid candidateId, DateOnly appliedOn)
        => $"{FormaliseCandidatePrefix}{candidateId}:{Format(appliedOn)}";

    public static string FormaliseFromEmployment(Guid employmentId) => $"{FormaliseEmploymentPrefix}{employmentId}";

    public static string Probation(Guid probationId) => $"p:{probationId}";

    public static string Dismissal(Guid employmentId) => $"d:{employmentId}";

    /// <summary>A hire-formalise case started by the direct-hire scan (§3.2 (b)), with no offer before it.</summary>
    public static bool IsDirectHire(string subjectRef)
    {
        ArgumentNullException.ThrowIfNull(subjectRef);
        return subjectRef.StartsWith(FormaliseEmploymentPrefix, StringComparison.Ordinal);
    }

    /// <summary>The application date inside a candidate reference; <c>null</c> for every other reference.</summary>
    public static DateOnly? AppliedOn(string subjectRef)
    {
        ArgumentNullException.ThrowIfNull(subjectRef);

        if (!subjectRef.StartsWith(CandidatePrefix, StringComparison.Ordinal)
            && !subjectRef.StartsWith(FormaliseCandidatePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var date = subjectRef[(subjectRef.LastIndexOf(':') + 1)..];
        return DateOnly.TryParseExact(date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Start of the Tashkent day as an instant — what the candidate facts compare with. The exact
    /// timestamp of an older application is gone once the candidate re-applies, the date is not;
    /// two applications on one day share one case anyway.
    /// </summary>
    public static DateTimeOffset StartOfDay(DateOnly date)
        => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), BonviTime.TashkentOffset).ToUniversalTime();

    private static string Format(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);
}
