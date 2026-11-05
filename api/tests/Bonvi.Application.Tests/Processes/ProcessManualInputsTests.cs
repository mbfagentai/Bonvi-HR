using System.Text.Json;
using Bonvi.Application.Processes.Commands;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Common;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Tests.Processes;

/// <summary>§10 #10: what «Bajarildi» sends against the step's <c>manual.inputs</c>, and the canonical payload it becomes.</summary>
public sealed class ProcessManualInputsTests
{
    private const string Fixed = "fixed";

    private static readonly IReadOnlyList<ProcessOption> Ways = [new("card", "Karta"), new("cash", "Naqd")];

    private static readonly IReadOnlyList<ProcessOption> Items = [new("pass", "Propusk"), new("key", "Kalit")];

    private static ProcessManualInput Input(string id, ProcessInputType type, bool required = false)
        => new() { Id = id, Label = id, Type = type, Required = required };

    private static ProcessManual Manual(params ProcessManualInput[] inputs) => new(Allowed: true, inputs);

    private static Result<string?> Validate(ProcessManual? manual, string? json, Func<string, FactValue>? factOf = null)
    {
        JsonElement? inputs = null;
        if (json is not null)
        {
            using var document = JsonDocument.Parse(json);
            inputs = document.RootElement.Clone();
        }

        return ProcessManualInputs.Validate(manual, inputs, factOf ?? (_ => FactValue.NotHeld));
    }

    private static void ShouldBeRefused(Result<string?> result)
    {
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProcessErrors.Invalid);
    }

    private static void ShouldStore(Result<string?> result, string? canonical)
    {
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(canonical);
    }

    private static string Quoted(string text) => JsonSerializer.Serialize(text);

    // ─────────────────────────── The shape of the request ───────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("{}")]
    public void Nothing_given_and_nothing_asked_stores_nothing(string? json)
    {
        ShouldStore(Validate(null, json), null);
        ShouldStore(Validate(Manual(Input("note", ProcessInputType.Text)), json), null);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("1")]
    [InlineData("true")]
    public void Inputs_that_are_not_an_object_are_refused(string json)
        => ShouldBeRefused(Validate(Manual(Input("note", ProcessInputType.Text)), json));

    [Fact]
    public void A_repeated_key_is_refused()
        => ShouldBeRefused(Validate(Manual(Input("note", ProcessInputType.Text)), """{"note":"a","note":"b"}"""));

    [Theory]
    [InlineData("""{"other":"x"}""")]
    [InlineData("""{"note":"a","other":"x"}""")]
    [InlineData("""{"note":"a","other":null}""")]
    [InlineData("""{"other":null}""")]
    [InlineData("""{"NOTE":"a"}""")]
    public void An_unknown_key_is_refused_whatever_its_value(string json)
        => ShouldBeRefused(Validate(Manual(Input("note", ProcessInputType.Text)), json));

    [Fact]
    public void A_step_that_asks_for_nothing_refuses_any_key()
        => ShouldBeRefused(Validate(null, """{"note":null}"""));

    [Fact]
    public void A_known_key_with_a_null_value_reads_as_absent()
    {
        ShouldStore(Validate(Manual(Input("note", ProcessInputType.Text)), """{"note":null}"""), null);
        ShouldBeRefused(Validate(Manual(Input("note", ProcessInputType.Text, required: true)), """{"note":null}"""));
    }

    // ─────────────────────────── Text ───────────────────────────

    [Theory]
    [InlineData(ProcessInputType.Text, ProcessManualInputs.MaxTextLength)]
    [InlineData(ProcessInputType.LongText, ProcessManualInputs.MaxLongTextLength)]
    public void Text_may_reach_its_limit_and_not_pass_it(ProcessInputType type, int limit)
    {
        var manual = Manual(Input("note", type));
        var full = new string('t', limit);

        ShouldStore(Validate(manual, $$"""{"note":"{{full}}"}"""), $$"""{"note":"{{full}}"}""");
        ShouldStore(Validate(manual, $$"""{"note":"  {{full}}  "}"""), $$"""{"note":"{{full}}"}""");
        ShouldBeRefused(Validate(manual, $$"""{"note":"{{full}}t"}"""));
    }

    [Fact]
    public void Text_is_stored_trimmed()
        => ShouldStore(Validate(Manual(Input("note", ProcessInputType.Text)), """{"note":"  a b  "}"""), """{"note":"a b"}""");

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    [InlineData("null")]
    public void Blank_text_is_the_same_as_absent(string value)
    {
        ShouldStore(Validate(Manual(Input("note", ProcessInputType.Text)), $$"""{"note":{{value}}}"""), null);
        ShouldBeRefused(Validate(Manual(Input("note", ProcessInputType.LongText, required: true)), $$"""{"note":{{value}}}"""));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void Text_of_another_json_type_is_refused(string value)
        => ShouldBeRefused(Validate(Manual(Input("note", ProcessInputType.Text)), $$"""{"note":{{value}}}"""));

    [Theory]
    [InlineData(ProcessInputType.Text)]
    [InlineData(ProcessInputType.LongText)]
    public void Text_with_a_nul_character_is_refused_wherever_it_stands(ProcessInputType type)
    {
        var manual = Manual(Input("note", type));

        ShouldBeRefused(Validate(manual, """{"note":"a\u0000b"}"""));
        ShouldBeRefused(Validate(manual, """{"note":"\u0000"}"""));
        ShouldBeRefused(Validate(manual, """{"note":"  a\u0000  "}"""));
    }

    // ─────────────────────────── Date ───────────────────────────

    [Fact]
    public void A_date_is_kept_in_its_one_form()
        => ShouldStore(Validate(Manual(Input("on", ProcessInputType.Date)), """{"on":"2026-11-05"}"""), """{"on":"2026-11-05"}""");

    [Theory]
    [InlineData("\"2026-02-31\"")]
    [InlineData("\"2026/11/05\"")]
    [InlineData("\"05.11.2026\"")]
    [InlineData("\"2026-11-05T00:00:00\"")]
    [InlineData("\"\"")]
    [InlineData("20261105")]
    public void Anything_else_is_not_a_date(string value)
        => ShouldBeRefused(Validate(Manual(Input("on", ProcessInputType.Date)), $$"""{"on":{{value}}}"""));

    // ─────────────────────────── Whole number ───────────────────────────

    [Theory]
    [InlineData("2", true)]
    [InlineData("5", true)]
    [InlineData("1", false)]
    [InlineData("6", false)]
    [InlineData("2.5", false)]
    [InlineData("\"3\"", false)]
    [InlineData("3000000000", false)]
    public void A_whole_number_stays_inside_its_range(string value, bool accepted)
    {
        var manual = Manual(new ProcessManualInput { Id = "count", Label = "count", Type = ProcessInputType.WholeNumber, Min = 2, Max = 5 });

        var result = Validate(manual, $$"""{"count":{{value}}}""");

        if (accepted)
        {
            ShouldStore(result, $$"""{"count":{{value}}}""");
        }
        else
        {
            ShouldBeRefused(result);
        }
    }

    [Fact]
    public void A_whole_number_with_no_range_takes_any_int()
    {
        var manual = Manual(Input("count", ProcessInputType.WholeNumber));

        ShouldStore(Validate(manual, """{"count":-7}"""), """{"count":-7}""");
        ShouldStore(Validate(manual, """{"count":0}"""), """{"count":0}""");
    }

    // ─────────────────────────── Choice ───────────────────────────

    private static ProcessManual Payment(bool required = true)
        => Manual(new ProcessManualInput { Id = "payment", Label = "payment", Type = ProcessInputType.Choice, Required = required, Options = Ways });

    [Fact]
    public void A_choice_takes_one_of_its_options()
        => ShouldStore(Validate(Payment(), """{"payment":"cash"}"""), """{"payment":"cash"}""");

    [Theory]
    [InlineData("""{"payment":"bitcoin"}""")]
    [InlineData("""{"payment":"CASH"}""")]
    [InlineData("""{"payment":" cash "}""")]
    [InlineData("""{"payment":""}""")]
    [InlineData("""{"payment":1}""")]
    public void A_choice_refuses_the_rest(string json)
        => ShouldBeRefused(Validate(Payment(), json));

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("""{"payment":null}""")]
    public void A_required_choice_has_to_be_there(string? json)
    {
        ShouldBeRefused(Validate(Payment(), json));
        ShouldStore(Validate(Payment(required: false), json), null);
    }

    // ─────────────────────────── Checklist ───────────────────────────

    private static ProcessManual Docs(bool required)
        => Manual(new ProcessManualInput { Id = "docs", Label = "docs", Type = ProcessInputType.Checklist, Required = required, Options = Items });

    [Fact]
    public void A_required_checklist_needs_every_item_ticked_or_marked()
    {
        ShouldStore(Validate(Docs(required: true), """{"docs":{"pass":"done","key":"na"}}"""), """{"docs":{"key":"na","pass":"done"}}""");
        ShouldBeRefused(Validate(Docs(required: true), """{"docs":{"pass":"done"}}"""));
        ShouldBeRefused(Validate(Docs(required: true), """{"docs":{}}"""));
        ShouldBeRefused(Validate(Docs(required: true), null));
    }

    [Fact]
    public void An_optional_checklist_may_be_part_done_and_empty_is_absent()
    {
        ShouldStore(Validate(Docs(required: false), """{"docs":{"pass":"done"}}"""), """{"docs":{"pass":"done"}}""");
        ShouldStore(Validate(Docs(required: false), """{"docs":{}}"""), null);
    }

    [Theory]
    [InlineData("""{"docs":{"other":"done"}}""")]
    [InlineData("""{"docs":{"pass":"yes"}}""")]
    [InlineData("""{"docs":{"pass":true}}""")]
    [InlineData("""{"docs":{"pass":null}}""")]
    [InlineData("""{"docs":{"pass":"done","pass":"na"}}""")]
    [InlineData("""{"docs":["pass"]}""")]
    [InlineData("""{"docs":"pass"}""")]
    public void A_checklist_refuses_anything_but_its_items_and_marks(string json)
        => ShouldBeRefused(Validate(Docs(required: false), json));

    // ─────────────────────────── requiredWhen ───────────────────────────

    private static ProcessManual Contract(ProcessRequiredWhen when)
        => Manual(
            new ProcessManualInput { Id = "term", Label = "term", Type = ProcessInputType.Text },
            new ProcessManualInput { Id = "basis", Label = "basis", Type = ProcessInputType.Text, RequiredWhen = when });

    [Fact]
    public void Required_when_follows_the_other_inputs_value()
    {
        var manual = Contract(new ProcessRequiredWhen(Input: "term", Fact: null, ProcessScalar.Of(Fixed)));

        ShouldBeRefused(Validate(manual, """{"term":"fixed"}"""));
        ShouldStore(Validate(manual, """{"term":"fixed","basis":"Sabab"}"""), """{"basis":"Sabab","term":"fixed"}""");
        ShouldStore(Validate(manual, """{"term":"permanent"}"""), """{"term":"permanent"}""");
        ShouldStore(Validate(manual, null), null);
    }

    [Fact]
    public void Required_when_compares_a_number_in_its_plain_form()
    {
        var manual = Manual(
            Input("months", ProcessInputType.WholeNumber),
            new ProcessManualInput
            {
                Id = "basis",
                Label = "basis",
                Type = ProcessInputType.Text,
                RequiredWhen = new ProcessRequiredWhen(Input: "months", Fact: null, ProcessScalar.Of(3m)),
            });

        ShouldBeRefused(Validate(manual, """{"months":3}"""));
        ShouldStore(Validate(manual, """{"months":4}"""), """{"months":4}""");
    }

    [Fact]
    public void Required_when_never_matches_a_checklist()
    {
        var manual = Manual(
            new ProcessManualInput { Id = "docs", Label = "docs", Type = ProcessInputType.Checklist, Options = Items },
            new ProcessManualInput
            {
                Id = "basis",
                Label = "basis",
                Type = ProcessInputType.Text,
                RequiredWhen = new ProcessRequiredWhen(Input: "docs", Fact: null, ProcessScalar.Of("done")),
            });

        ShouldStore(Validate(manual, """{"docs":{"pass":"done"}}"""), """{"docs":{"pass":"done"}}""");
    }

    [Fact]
    public void Required_when_reads_the_fact_it_names_and_asks_only_on_its_value()
    {
        var manual = Manual(new ProcessManualInput
        {
            Id = "basis",
            Label = "basis",
            Type = ProcessInputType.Text,
            RequiredWhen = new ProcessRequiredWhen(Input: null, Fact: "emp.term", ProcessScalar.Of(Fixed)),
        });

        ShouldBeRefused(Validate(manual, null, name => name == "emp.term" ? new FactValue(true, Value: Fixed) : FactValue.NotHeld));
        ShouldStore(Validate(manual, null, name => name == "other" ? new FactValue(true, Value: Fixed) : FactValue.NotHeld), null);
        ShouldStore(Validate(manual, null, _ => new FactValue(true, Value: "permanent")), null);
        ShouldStore(Validate(manual, null, _ => new FactValue(false, Value: Fixed)), null);
        ShouldStore(Validate(manual, null, _ => new FactValue(true)), null);
    }

    // ─────────────────────────── Canonical form and §6.3 ───────────────────────────

    private static ProcessManual Mixed()
        => Manual(
            Input("zeta", ProcessInputType.Text),
            Input("alpha", ProcessInputType.WholeNumber),
            new ProcessManualInput { Id = "docs", Label = "docs", Type = ProcessInputType.Checklist, Options = Items });

    [Fact]
    public void The_payload_has_sorted_keys_and_no_spacing_whatever_order_it_came_in()
    {
        const string Canonical = """{"alpha":1,"docs":{"key":"na","pass":"done"},"zeta":"z"}""";

        ShouldStore(Validate(Mixed(), """{ "zeta" : " z ", "docs" : { "pass" : "done", "key" : "na" }, "alpha" : 1 }"""), Canonical);
        ShouldStore(Validate(Mixed(), """{"alpha":1,"docs":{"key":"na","pass":"done"},"zeta":"z"}"""), Canonical);
    }

    [Fact]
    public void A_stored_payload_validates_to_itself_so_the_hash_can_be_recomputed()
    {
        var first = Validate(Mixed(), """{"zeta":"z","alpha":1,"docs":{"pass":"done","key":"na"}}""").Value!;

        var again = Validate(Mixed(), first).Value!;

        again.ShouldBe(first);
        ProcessManualInputs.EvidenceOf(again).ShouldBe(ProcessManualInputs.EvidenceOf(first));
    }

    [Fact]
    public void Evidence_is_lower_case_sha256_of_the_utf8_bytes()
    {
        // FIPS 180-2 test vector.
        ProcessManualInputs.EvidenceOf("abc").ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");

        ProcessManualInputs.EvidenceOf("ў").ShouldNotBe(ProcessManualInputs.EvidenceOf("у"));
    }

    [Fact]
    public void Non_ascii_text_survives_the_round_trip()
    {
        var result = Validate(Manual(Input("note", ProcessInputType.Text)), $$"""{"note":{{Quoted("Oʻzgardi ўзгарди")}}}""");

        result.IsSuccess.ShouldBeTrue();
        using var stored = JsonDocument.Parse(result.Value!);
        stored.RootElement.GetProperty("note").GetString().ShouldBe("Oʻzgardi ўзгарди");
    }
}
