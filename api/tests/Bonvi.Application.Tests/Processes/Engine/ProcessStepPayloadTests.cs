using Bonvi.Application.Processes.Engine;

namespace Bonvi.Application.Tests.Processes.Engine;

/// <summary><c>process_case_steps.payload</c> text and <c>subject_ref</c> formats the pass relies on.</summary>
public sealed class ProcessStepPayloadTests
{
    [Theory]
    [InlineData("""{"value":"x"}""", """{"value": "x"}""")]
    [InlineData("""{"a":1,"b":"y"}""", """{"b": "y", "a": 1}""")]
    public void Jsonb_spacing_and_key_order_are_the_same_payload(string written, string read)
        => ProcessStepPayload.AreEqual(written, read).ShouldBeTrue();

    [Theory]
    [InlineData("""{"value":"x"}""", """{"value":"y"}""")]
    [InlineData("""{"value":"x"}""", null)]
    [InlineData(null, """{"value":"x"}""")]
    public void Different_payloads_differ(string? left, string? right)
        => ProcessStepPayload.AreEqual(left, right).ShouldBeFalse();

    [Fact]
    public void Two_empty_payloads_are_equal()
        => ProcessStepPayload.AreEqual(null, null).ShouldBeTrue();

    [Fact]
    public void A_fact_with_no_value_has_no_payload()
        => ProcessStepPayload.ForFactValue(null).ShouldBeNull();

    [Theory]
    [InlineData("""{"startDate":"2026-11-16"}""", "startDate", "2026-11-16")]
    [InlineData("""{"score":4}""", "score", "4")]
    [InlineData("""{"score":null}""", "score", null)]
    [InlineData("""{"other":"x"}""", "score", null)]
    [InlineData("""{"score":{"nested":1}}""", "score", null)]
    [InlineData("""[1,2]""", "score", null)]
    [InlineData("not json", "score", null)]
    [InlineData(null, "score", null)]
    public void Read_input_answers_scalars_and_nothing_else(string? payload, string input, string? expected)
        => ProcessStepPayload.ReadInput(payload, input).ShouldBe(expected);

    [Fact]
    public void The_application_date_is_read_back_from_both_candidate_references()
    {
        var id = Guid.CreateVersion7();
        var on = new DateOnly(2026, 11, 5);

        ProcessSubjectRefs.AppliedOn(ProcessSubjectRefs.Candidate(id, on)).ShouldBe(on);
        ProcessSubjectRefs.AppliedOn(ProcessSubjectRefs.FormaliseFromCandidate(id, on)).ShouldBe(on);
    }

    [Fact]
    public void Other_references_carry_no_application_date()
    {
        var id = Guid.CreateVersion7();

        ProcessSubjectRefs.AppliedOn(ProcessSubjectRefs.VacancyRequest(id)).ShouldBeNull();
        ProcessSubjectRefs.AppliedOn(ProcessSubjectRefs.FormaliseFromEmployment(id)).ShouldBeNull();
        ProcessSubjectRefs.AppliedOn(ProcessSubjectRefs.Dismissal(id)).ShouldBeNull();
    }

    [Fact]
    public void Only_the_employment_reference_is_a_direct_hire()
    {
        var id = Guid.CreateVersion7();

        ProcessSubjectRefs.IsDirectHire(ProcessSubjectRefs.FormaliseFromEmployment(id)).ShouldBeTrue();
        ProcessSubjectRefs.IsDirectHire(ProcessSubjectRefs.FormaliseFromCandidate(id, new DateOnly(2026, 11, 5))).ShouldBeFalse();
    }

    [Fact]
    public void The_start_of_a_day_is_midnight_in_tashkent()
        => ProcessSubjectRefs.StartOfDay(new DateOnly(2026, 11, 5))
            .ShouldBe(new DateTimeOffset(2026, 11, 4, 19, 0, 0, TimeSpan.Zero));
}
