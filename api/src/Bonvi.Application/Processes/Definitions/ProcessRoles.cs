namespace Bonvi.Application.Processes.Definitions;

/// <summary>Who holds a step (<c>role</c> / <c>fallbackRole</c>, SPEC-mening-ishlarim §9.1, holder rules §7.7).</summary>
/// <remarks>
/// Office roles are the blank engine's assignable roles plus <see cref="SafetyEngineer"/> and
/// <see cref="Council"/>, which P1 adds to <c>BlankActorRoles.OfficeRoles</c> (H12). The list is kept here
/// so a template is validated against the process catalogue even before that merge.
/// </remarks>
public static class ProcessRoles
{
    public const string Hr1 = "hr1";
    public const string Hr2 = "hr2";
    public const string Hr3 = "hr3";
    public const string Hr4 = "hr4";
    public const string Hr5 = "hr5";
    public const string Hr8 = "hr8";
    public const string PayrollAccountant = "payrollAccountant";
    public const string ChiefAccountant = "chiefAccountant";
    public const string Director = "director";
    public const string Lawyer = "lawyer";
    public const string SafetyEngineer = "safetyEngineer";
    public const string Council = "council";

    /// <summary>Head of the case department → that employee's user; none → <c>fallbackRole</c>.</summary>
    public const string DepartmentHead = "departmentHead";

    /// <summary>The case employee's own user; none → <c>fallbackRole</c>.</summary>
    public const string SubjectEmployee = "subjectEmployee";

    /// <summary>«Tizim» — never a task.</summary>
    public const string System = "system";

    public static IReadOnlySet<string> Office { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Hr1, Hr2, Hr3, Hr4, Hr5, Hr8, PayrollAccountant, ChiefAccountant, Director, Lawyer, SafetyEngineer, Council,
    };

    public static IReadOnlySet<string> Person { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        DepartmentHead, SubjectEmployee,
    };

    public static bool IsKnown(string role) => Office.Contains(role) || Person.Contains(role) || role == System;
}
