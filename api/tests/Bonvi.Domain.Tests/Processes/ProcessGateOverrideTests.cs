using Bonvi.Domain.Processes;

namespace Bonvi.Domain.Tests.Processes;

/// <summary>Gate overrides: single use, 24 h lifetime (docs/SPEC-mening-ishlarim.md §8.4).</summary>
public sealed class ProcessGateOverrideTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Case = Guid.NewGuid();
    private static readonly Guid Hr1 = Guid.NewGuid();
    private const string Reason = "Direktor og'zaki ruxsat berdi";

    private static ProcessGateOverride Granted()
        => ProcessGateOverride.GrantForCase(ProcessGateCodes.Q12, Case, "f01-4", null, null, Reason, Hr1, At).Value;

    [Fact]
    public void Grant_lives_24_hours_and_is_usable_until_then()
    {
        var row = Granted();

        row.GateCode.ShouldBe(ProcessGateCodes.Q12);
        row.ExpiresAt.ShouldBe(At + ProcessGateOverride.Lifetime);
        row.IsUsableAt(At.AddHours(23)).ShouldBeTrue();
        row.IsUsableAt(row.ExpiresAt).ShouldBeFalse("ExpiresAt is exclusive");
    }

    [Fact]
    public void Consume_is_single_use()
    {
        var row = Granted();

        row.Consume(ProcessGateOperations.Hire, At.AddHours(1)).IsSuccess.ShouldBeTrue();

        row.ConsumedAt.ShouldBe(At.AddHours(1));
        row.ConsumedOperation.ShouldBe(ProcessGateOperations.Hire);
        row.IsUsableAt(At.AddHours(2)).ShouldBeFalse();
        row.Consume(ProcessGateOperations.Hire, At.AddHours(2)).Error.ShouldBe(ProcessErrors.OverrideNotUsable);
    }

    [Fact]
    public void Consume_after_expiry_is_refused()
    {
        var row = Granted();

        row.Consume(ProcessGateOperations.Terminate, At.AddHours(25)).Error.ShouldBe(ProcessErrors.OverrideNotUsable);
        row.ConsumedAt.ShouldBeNull();
    }

    [Fact]
    public void Consume_needs_a_known_operation()
    {
        Granted().Consume("delete", At).Error.ShouldBe(ProcessErrors.Invalid);
    }

    [Theory]
    [InlineData("Q99", Reason)]
    [InlineData(" ", Reason)]
    [InlineData(ProcessGateCodes.Q12, "qisqa")]
    [InlineData(ProcessGateCodes.Q12, "   ")]
    public void Grant_needs_a_known_gate_and_a_reason_of_10_chars(string gate, string reason)
    {
        ProcessGateOverride.GrantForCase(gate, Case, null, null, null, reason, Hr1, At)
            .Error.ShouldBe(ProcessErrors.Invalid);
    }

    [Fact]
    public void Grant_needs_a_case_and_a_granting_user()
    {
        ProcessGateOverride.GrantForCase(ProcessGateCodes.Q12, Guid.Empty, null, null, null, Reason, Hr1, At)
            .Error.ShouldBe(ProcessErrors.Invalid);
        ProcessGateOverride.GrantForCase(ProcessGateCodes.Q12, Case, null, null, null, Reason, Guid.Empty, At)
            .Error.ShouldBe(ProcessErrors.Invalid);
    }

    [Fact]
    public void Direct_hire_override_is_Q01_for_one_user_without_a_case()
    {
        var user = Guid.NewGuid();

        var row = ProcessGateOverride.GrantForDirectHire(user, Reason, Hr1, At).Value;

        row.GateCode.ShouldBe(ProcessGateCodes.Q01);
        row.ForUserId.ShouldBe(user);
        row.CaseId.ShouldBeNull();
        ProcessGateOverride.GrantForDirectHire(Guid.Empty, Reason, Hr1, At).Error.ShouldBe(ProcessErrors.Invalid);
    }
}
