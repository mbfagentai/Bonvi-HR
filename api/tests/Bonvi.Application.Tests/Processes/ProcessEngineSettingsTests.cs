using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Settings;
using Bonvi.Application.Tests.Settings;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Tests.Processes;

/// <summary>The engine's view of its settings (docs/SPEC-mening-ishlarim.md §6.6, §7.6).</summary>
public sealed class ProcessEngineSettingsTests
{
    private static readonly FakeSecretProtector Protector = new();

    [Fact]
    public void No_rows_means_dormant_with_the_defaults()
    {
        var settings = Read();

        settings.StartedOn.ShouldBeNull();
        settings.IsDormant.ShouldBeTrue();
        settings.GatesMode.ShouldBe(SystemSettingKeys.ProcessesGatesEnforce);
        settings.GatesEnforced.ShouldBeTrue();
        settings.CouncilDay.ShouldBe(DayOfWeek.Thursday);
    }

    [Theory]
    [InlineData("")]
    [InlineData("05.11.2026")]
    [InlineData("2026-13-45")]
    public void An_empty_or_unreadable_start_date_means_dormant(string value)
    {
        Read(StartedOn(value)).IsDormant.ShouldBeTrue();
    }

    [Fact]
    public void A_stored_start_date_wakes_the_engine()
    {
        var settings = Read(StartedOn("2026-11-05"));

        settings.StartedOn.ShouldBe(new DateOnly(2026, 11, 5));
        settings.IsDormant.ShouldBeFalse();
    }

    [Theory]
    [InlineData("enforce", true)]
    [InlineData("warn", false)]
    [InlineData("off", false)]
    public void Only_enforce_mode_blocks(string mode, bool enforced)
    {
        var settings = Read(Row(SystemSettingKeys.ProcessesGatesMode, mode));

        settings.GatesMode.ShouldBe(mode);
        settings.GatesEnforced.ShouldBe(enforced);
    }

    [Fact]
    public void Council_weekday_is_iso()
    {
        Read(Row(SystemSettingKeys.ProcessesCouncilWeekday, "1")).CouncilDay.ShouldBe(DayOfWeek.Monday);
    }

    private static ProcessEngineSettings Read(params SystemSetting[] rows)
        => ProcessEngineSettings.From(SystemSettings.From(rows, Protector));

    private static SystemSetting StartedOn(string value)
        => SystemSetting.CreateServerValue(SystemSettingKeys.ProcessesStartedOn, value).Value;

    private static SystemSetting Row(string key, string value)
        => SystemSetting.Create(key, value, Protector).Value;
}
