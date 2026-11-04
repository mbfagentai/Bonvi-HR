namespace Bonvi.Application.Processes.Definitions;

/// <summary>Gate catalogue codes a step may reference (<c>gate.code</c>, SPEC-mening-ishlarim §8.1).</summary>
/// <remarks>Q07–Q11 are absent from the prototype (C1) and are rejected until the owner supplies them.</remarks>
public static class ProcessGateCodes
{
    /// <summary>Hire needs form, council decision and (conditional) background check — hook H13.</summary>
    public const string Q01 = "Q01";

    /// <summary>Attendance enrolment not done by hire date — warning.</summary>
    public const string Q02 = "Q02";

    /// <summary>JSHSHIR and payment method before handing over to payroll — engine, r01-9.</summary>
    public const string Q03 = "Q03";

    /// <summary>Safety briefing (step <c>mm</c>) done — engine, r02-13.</summary>
    public const string Q04 = "Q04";

    /// <summary>mehnat.uz step past due — warning.</summary>
    public const string Q05 = "Q05";

    /// <summary>Personal file past due or incomplete — warning.</summary>
    public const string Q06 = "Q06";

    /// <summary>Approved HR-F09 before termination / final settlement — hook H14 + engine ex-10.</summary>
    public const string Q12 = "Q12";

    /// <summary>HR-F08 with the main K-code before termination — hook H14.</summary>
    public const string Q13 = "Q13";

    /// <summary>Empty HR-F09 sections — warning only (C8).</summary>
    public const string Akt = "AKT";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Q01, Q02, Q03, Q04, Q05, Q06, Q12, Q13, Akt,
    };
}
