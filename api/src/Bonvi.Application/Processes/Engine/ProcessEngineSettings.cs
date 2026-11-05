using System.Globalization;
using Bonvi.Application.Processes.Calendar;
using Bonvi.Application.Settings;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Processes.Engine;

/// <summary>
/// What the engine reads from the settings (docs/SPEC-mening-ishlarim.md §6.6, §7.6); take it from
/// <see cref="SystemSettingsReader.ReadAsync"/> once per pass.
/// </summary>
/// <param name="StartedOn">Tashkent date the engine went live; <c>null</c> = dormant.</param>
/// <param name="GatesMode"><c>enforce</c>, <c>warn</c> or <c>off</c>.</param>
/// <param name="CouncilDay">The weekday of the hiring council.</param>
public sealed record ProcessEngineSettings(DateOnly? StartedOn, string GatesMode, DayOfWeek CouncilDay)
{
    /// <summary>Dormant: the engine creates nothing and gates allow everything (§7.6).</summary>
    public bool IsDormant => StartedOn is null;

    /// <summary>Whether a failing <c>block</c> gate blocks; <c>warn</c> and <c>off</c> never do.</summary>
    public bool GatesEnforced => GatesMode == SystemSettingKeys.ProcessesGatesEnforce;

    public static ProcessEngineSettings From(SystemSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new ProcessEngineSettings(
            ParseStartedOn(settings.Hidden(SystemSettingKeys.ProcessesStartedOn)),
            settings.Text(SystemSettingKeys.ProcessesGatesMode),
            WorkingDayCalendar.FromIsoWeekday(settings.Number(SystemSettingKeys.ProcessesCouncilWeekday)));
    }

    /// <summary>
    /// Only the seeder writes this value. A value it could not have written counts as absent: a wrong date
    /// would start cases for the wrong period, and dormant leaves every existing flow exactly as it was.
    /// </summary>
    private static DateOnly? ParseStartedOn(string value)
        => DateOnly.TryParseExact(
            value,
            SystemSettingKeys.ProcessesStartedOnFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var startedOn)
            ? startedOn
            : null;
}
