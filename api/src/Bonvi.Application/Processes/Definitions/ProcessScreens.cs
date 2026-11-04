namespace Bonvi.Application.Processes.Definitions;

/// <summary>
/// Deep-link targets of a step (<c>link.screen</c>, SPEC-mening-ishlarim §3.1, routes §11.6) and the
/// <c>link.args</c> each one takes.
/// </summary>
public static class ProcessScreens
{
    /// <summary><c>/vakansiya-sorovlari?id=</c></summary>
    public const string VacancyRequests = "vacancyRequests";

    /// <summary>Candidate card.</summary>
    public const string Candidate = "candidate";

    /// <summary>Candidate card, form tab.</summary>
    public const string CandidateForm = "candidateForm";

    /// <summary><c>/blankalar/{id}</c>, create-or-open; args <c>template</c> (required), <c>subject</c>.</summary>
    public const string Blank = "blank";

    /// <summary><c>/xodimlar/{id}</c></summary>
    public const string Employee = "employee";

    /// <summary><c>/buyruqlar</c> filtered.</summary>
    public const string Employment = "employment";

    /// <summary><c>/sinov</c></summary>
    public const string Probations = "probations";

    /// <summary><c>/verifikatsiya/moslashtirish</c></summary>
    public const string AttendanceMapping = "attendanceMapping";

    /// <summary><c>/verifikatsiya/jadvallar</c></summary>
    public const string AttendanceSchedules = "attendanceSchedules";

    /// <summary>New tab; args <c>url</c> (required, https).</summary>
    public const string External = "external";

    public const string None = "none";

    public const string TemplateArg = "template";

    public const string SubjectArg = "subject";

    public const string UrlArg = "url";

    /// <summary>Values of the <c>blank</c> screen's <c>subject</c> arg.</summary>
    public static IReadOnlySet<string> BlankSubjects { get; } = new HashSet<string>(StringComparer.Ordinal) { "candidate", "employee" };

    /// <summary>Screen → the arg keys it accepts; <see cref="RequiredArgs"/> lists the mandatory ones.</summary>
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedArgs { get; } =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [VacancyRequests] = new HashSet<string>(),
            [Candidate] = new HashSet<string>(),
            [CandidateForm] = new HashSet<string>(),
            [Blank] = new HashSet<string>(StringComparer.Ordinal) { TemplateArg, SubjectArg },
            [Employee] = new HashSet<string>(),
            [Employment] = new HashSet<string>(),
            [Probations] = new HashSet<string>(),
            [AttendanceMapping] = new HashSet<string>(),
            [AttendanceSchedules] = new HashSet<string>(),
            [External] = new HashSet<string>(StringComparer.Ordinal) { UrlArg },
            [None] = new HashSet<string>(),
        };

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredArgs { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Blank] = [TemplateArg],
            [External] = [UrlArg],
        };

    public static bool IsKnown(string screen) => AllowedArgs.ContainsKey(screen);
}
