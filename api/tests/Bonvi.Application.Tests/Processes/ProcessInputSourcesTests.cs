using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Processes.Commands;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Facts;
using Bonvi.Application.Processes.Inputs;
using Bonvi.Domain.Common;
using Bonvi.Domain.Processes;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Tests.Processes;

/// <summary>Which checklist items the system answers, from what, and what «Bajarildi» does with them.</summary>
public sealed class ProcessInputSourcesTests
{
    private const string Diploma = "Маълумот тўғрисида ҳужжат (диплом)";
    private const string Reason = "Hujjat keyin keladi";

    private static readonly Guid EmployeeId = Guid.Parse("019a0000-0000-7000-8000-00000000000a");
    private static readonly Guid CandidateId = Guid.Parse("019a0000-0000-7000-8000-00000000000b");
    private static readonly Guid TypeId = Guid.Parse("019a0000-0000-7000-8000-00000000000c");

    private static readonly string[] PlainDocs = ["certificates", "bankCard", "liability"];

    private static readonly string[] AutoDocs =
    [
        "passport", "pinfl", "diploma", "workBook", "military", "medical", "photo", "application", "councilMinutes",
    ];

    private static readonly ProcessDefinition Formalise = Load(ProcessCodes.HireFormalise);

    private static ProcessStep Docs => Formalise.Steps.Single(step => step.Id == "r01-2");

    private static ProcessStep File7 => Formalise.Steps.Single(step => step.Id == "r01-7");

    private static ProcessDefinition Load(string code)
        => ProcessDefinitionValidator.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Processes", "Templates", code + ".json")));

    private static ProcessSubjectData Employee(params string[] documents) => new()
    {
        EmployeeId = EmployeeId,
        DocumentTypes = documents.Select(ProcessInputSources.NormaliseTypeName).ToHashSet(StringComparer.Ordinal),
    };

    /// <summary>Everything r01-2's items can read is there.</summary>
    private static ProcessSubjectData Complete() => Employee(
        Diploma,
        "Меҳнат дафтарчаси",
        "Ҳарбий ҳисоб варақаси",
        "Тиббий кўрик хулосаси",
        "Ариза",
        "Анкета + кенгаш хулосаси") with { HasPassport = true, HasPinfl = true, HasPhoto = true };

    private static ProcessStepAutoItems Resolve(ProcessStep step, ProcessSubjectData data, params (string Step, ProcessStepState State)[] states)
        => ProcessInputSources.Resolve(
            ProcessCodes.HireFormalise,
            step,
            Formalise,
            data,
            states.ToDictionary(state => state.Step, state => state.State, StringComparer.Ordinal));

    private static ProcessAutoItem DocsItem(ProcessSubjectData data, string item)
        => Resolve(Docs, data).Of("docs").Single(row => row.Value == item);

    private static ProcessAutoItem FileItem(ProcessSubjectData data, string item, params (string Step, ProcessStepState State)[] states)
        => Resolve(File7, data, states).Of("items").Single(row => row.Value == item);

    private static Result<ProcessManualOutcome> Close(
        ProcessManual? manual,
        ProcessStepAutoItems? autoItems,
        string? json = null,
        string? reason = null,
        bool canManage = false)
    {
        JsonElement? inputs = null;
        if (json is not null)
        {
            using var document = JsonDocument.Parse(json);
            inputs = document.RootElement.Clone();
        }

        return ProcessManualInputs.Validate(manual, inputs, _ => FactValue.NotHeld, autoItems, reason, canManage);
    }

    private static string PlainTicks(string mark = "done")
        => "{\"docs\":{" + string.Join(',', PlainDocs.Select(item => $"\"{item}\":\"{mark}\"")) + "}}";

    private static List<string> MissingItemsOf(Error error)
    {
        var rows = (IEnumerable<Dictionary<string, string>>)error.Details["missing"];
        return [.. rows.Select(row => row["item"])];
    }

    // ─────────────────────────── The map is held to the templates and the seed ───────────────────────────

    [Fact]
    public void Every_key_of_the_map_is_a_checklist_option_of_its_template()
    {
        ProcessInputSources.Keys.ShouldNotBeEmpty();

        foreach (var key in ProcessInputSources.Keys)
        {
            var step = Load(key.Process).Steps.SingleOrDefault(row => row.Id == key.Step);
            var input = step.ShouldNotBeNull(key.ToString()).Manual.ShouldNotBeNull(key.ToString()).Inputs.SingleOrDefault(row => row.Id == key.Input);

            input.ShouldNotBeNull(key.ToString()).Type.ShouldBe(ProcessInputType.Checklist, key.ToString());
            input.Options.Select(option => option.Value).ShouldContain(key.Item, key.ToString());
        }
    }

    [Fact]
    public void Every_document_type_name_of_the_map_is_in_the_seed_file()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Seed", "document-types.json");
        var seeded = JsonNode.Parse(File.ReadAllText(path))!.AsArray()
            .Select(row => ProcessInputSources.NormaliseTypeName(row!["name"]!.GetValue<string>()))
            .ToHashSet(StringComparer.Ordinal);

        ProcessInputSources.DocumentTypeNames.ShouldNotBeEmpty();
        ProcessInputSources.DocumentTypeNames
            .Where(name => !seeded.Contains(ProcessInputSources.NormaliseTypeName(name)))
            .ShouldBeEmpty();
    }

    [Theory]
    [InlineData("hire-request")]
    [InlineData("hire-candidate")]
    [InlineData("hire-formalise")]
    [InlineData("probation")]
    [InlineData("dismissal")]
    public void No_template_names_an_input_as_the_payloads_own_keys(string code)
        => Load(code).Steps
            .SelectMany(step => step.Manual?.Inputs ?? [])
            .Select(input => input.Id)
            .ShouldNotContain(id => id == ProcessManualInputs.AutoSourcesKey || id == ProcessManualInputs.MissingOverrideKey);

    // ─────────────────────────── Each source ───────────────────────────

    [Fact]
    public void Passport_is_known_from_the_card_or_from_the_copy()
    {
        var card = DocsItem(Employee() with { HasPassport = true }, "passport");
        card.State.ShouldBe(ProcessAutoItemState.Known);
        card.Source.ShouldBe(ProcessAutoSource.EmployeeCard);
        card.SourceToken.ShouldBe("employee.passport");
        card.Mark.ShouldBe(ProcessManualInputs.ChecklistDone);

        var copy = DocsItem(Employee("Паспорт нусхаси"), "passport");
        copy.State.ShouldBe(ProcessAutoItemState.Known);
        copy.Source.ShouldBe(ProcessAutoSource.EmployeeDocument);
        copy.SourceDetail.ShouldBe("Паспорт нусхаси");
        copy.SourceToken.ShouldBe("doc:Паспорт нусхаси");
    }

    [Fact]
    public void Pinfl_and_photo_are_read_from_the_card()
    {
        DocsItem(Employee() with { HasPinfl = true }, "pinfl").State.ShouldBe(ProcessAutoItemState.Known);
        DocsItem(Employee() with { HasPhoto = true }, "photo").State.ShouldBe(ProcessAutoItemState.Known);
        DocsItem(Employee(), "pinfl").State.ShouldBe(ProcessAutoItemState.Missing);
        DocsItem(Employee(), "photo").State.ShouldBe(ProcessAutoItemState.Missing);
    }

    [Theory]
    [InlineData("diploma", Diploma)]
    [InlineData("workBook", "Меҳнат дафтарчаси")]
    [InlineData("military", "Ҳарбий ҳисоб варақаси")]
    [InlineData("medical", "Тиббий кўрик хулосаси")]
    [InlineData("application", "Ариза")]
    [InlineData("councilMinutes", "Анкета + кенгаш хулосаси")]
    [InlineData("councilMinutes", "Анкета + кенгаш хулосаси + тест жавоблари")]
    public void A_document_item_is_known_when_its_type_is_held(string item, string type)
    {
        var known = DocsItem(Employee(type), item);

        known.State.ShouldBe(ProcessAutoItemState.Known);
        known.Source.ShouldBe(ProcessAutoSource.EmployeeDocument);
        known.SourceDetail.ShouldBe(type);
        DocsItem(Employee("Другой"), item).State.ShouldBe(ProcessAutoItemState.Missing);
    }

    [Fact]
    public void Work_book_is_known_from_the_mehnat_link_too()
    {
        var item = DocsItem(Employee() with { HasMehnatUzUrl = true }, "workBook");

        item.State.ShouldBe(ProcessAutoItemState.Known);
        item.Source.ShouldBe(ProcessAutoSource.EmployeeCard);
    }

    [Fact]
    public void A_type_is_matched_whatever_its_case_and_outer_spaces_and_a_renamed_one_reads_missing()
    {
        var data = Employee() with { DocumentTypes = new HashSet<string>(StringComparer.Ordinal) { ProcessInputSources.NormaliseTypeName("  АРИЗА ") } };

        DocsItem(data, "application").State.ShouldBe(ProcessAutoItemState.Known);
        DocsItem(Employee("Ариза (янги)"), "application").State.ShouldBe(ProcessAutoItemState.Missing, "never a false green");
    }

    [Fact]
    public void File_items_read_the_contract_the_order_and_the_form()
    {
        FileItem(Employee("Меҳнат шартномаси"), "contract").State.ShouldBe(ProcessAutoItemState.Known);
        FileItem(Employee(), "contract").State.ShouldBe(ProcessAutoItemState.Missing);

        var order = FileItem(Employee() with { HasHireOrder = true }, "hireOrder");
        order.State.ShouldBe(ProcessAutoItemState.Known);
        order.Source.ShouldBe(ProcessAutoSource.Employment);
        FileItem(Employee("Буйруқ"), "hireOrder").Source.ShouldBe(ProcessAutoSource.EmployeeDocument);

        var form = FileItem(Employee() with { CandidateId = CandidateId, CandidateHasForm = true }, "formF02");
        form.State.ShouldBe(ProcessAutoItemState.Known);
        form.Source.ShouldBe(ProcessAutoSource.CandidateForm);
        FileItem(Employee("Анкета"), "formF02").State.ShouldBe(ProcessAutoItemState.Known);
        FileItem(Employee("Анкета + кенгаш хулосаси"), "formF02").State.ShouldBe(ProcessAutoItemState.Known);
        FileItem(Employee(), "formF02").State.ShouldBe(ProcessAutoItemState.Missing);
    }

    [Theory]
    [InlineData("docCopies", "r01-2")]
    [InlineData("safetyMark", "mm")]
    public void An_item_read_from_a_step_is_known_only_when_that_step_is_done(string item, string step)
    {
        var known = FileItem(Employee(), item, (step, ProcessStepState.Done));
        known.State.ShouldBe(ProcessAutoItemState.Known);
        known.Source.ShouldBe(ProcessAutoSource.CaseStep);
        known.SourceDetail.ShouldBe(Formalise.Steps.Single(row => row.Id == step).Title);

        foreach (var state in new[] { ProcessStepState.Active, ProcessStepState.Locked, ProcessStepState.Skipped })
        {
            var open = FileItem(Employee(), item, (step, state));
            open.State.ShouldBe(ProcessAutoItemState.Missing);
            open.Fill.ShouldBe(new ProcessInputFill(ProcessInputFillKind.CaseStep, null, null, null, null, step));
        }

        FileItem(Employee(), item).State.ShouldBe(ProcessAutoItemState.Missing, "a step this version lacks is not a done one");
    }

    [Fact]
    public void Military_does_not_apply_to_a_woman_and_applies_otherwise()
    {
        var woman = DocsItem(Employee() with { Gender = Gender.Female }, "military");
        woman.State.ShouldBe(ProcessAutoItemState.NotApplicable);
        woman.Reason.ShouldBe(ProcessAutoNotApplicableReason.Female);
        woman.Mark.ShouldBe(ProcessManualInputs.ChecklistNotNeeded);

        DocsItem(Employee() with { Gender = Gender.Male }, "military").State.ShouldBe(ProcessAutoItemState.Missing);
        DocsItem(Employee(), "military").State.ShouldBe(ProcessAutoItemState.Missing, "an unknown gender applies");
    }

    [Fact]
    public void A_missing_item_says_where_it_is_filled_in()
    {
        var withType = Employee() with
        {
            ActiveDocumentTypes = new Dictionary<string, Guid>(StringComparer.Ordinal) { [ProcessInputSources.NormaliseTypeName(Diploma)] = TypeId },
        };

        DocsItem(withType, "diploma").Fill.ShouldBe(new ProcessInputFill(ProcessInputFillKind.EmployeeDocument, EmployeeId, null, TypeId, Diploma, null));
        DocsItem(Employee(), "diploma").Fill.ShouldBe(new ProcessInputFill(ProcessInputFillKind.EmployeeDocument, EmployeeId, null, null, Diploma, null), "an inactive type: the dialog opens unchosen");
        DocsItem(Employee(), "passport").Fill.ShouldBe(new ProcessInputFill(ProcessInputFillKind.EmployeeCard, EmployeeId, null, null, null, null));
    }

    [Fact]
    public void A_candidate_before_hire_is_asked_only_for_the_photo()
    {
        var data = new ProcessSubjectData { CandidateId = CandidateId };

        var item = Resolve(Docs, data).Of("docs").ShouldHaveSingleItem();

        item.Value.ShouldBe("photo");
        item.State.ShouldBe(ProcessAutoItemState.Missing);
        item.Fill.ShouldBe(new ProcessInputFill(ProcessInputFillKind.CandidateCard, null, CandidateId, null, null, null));

        var known = DocsItem(data with { CandidateHasPhoto = true }, "photo");
        known.State.ShouldBe(ProcessAutoItemState.Known);
        known.Source.ShouldBe(ProcessAutoSource.CandidateCard);
    }

    // ─────────────────────────── What the map does not know ───────────────────────────

    [Fact]
    public void Items_with_no_place_in_the_system_stay_plain_ticks()
    {
        Resolve(Docs, Complete()).Of("docs").Select(item => item.Value).ShouldBe(AutoDocs, ignoreOrder: true);

        Resolve(File7, Employee()).Of("items").Select(item => item.Value).ShouldNotContain("sheetF11");
    }

    [Fact]
    public void An_option_the_map_does_not_know_stays_a_plain_tick_and_one_the_template_lacks_is_not_invented()
    {
        var input = new ProcessManualInput
        {
            Id = "docs",
            Label = "Hujjatlar",
            Type = ProcessInputType.Checklist,
            Required = true,
            Options = [new("passport", "Pasport"), new("brandNew", "Yangi")],
        };
        var step = Docs with { Manual = new ProcessManual(true, [input]) };

        Resolve(step, Complete()).Of("docs").ShouldHaveSingleItem().Value.ShouldBe("passport");
    }

    [Fact]
    public void A_step_or_process_outside_the_map_has_no_items_and_spends_no_query()
    {
        ProcessInputSources.Covers(ProcessCodes.Dismissal, Docs).ShouldBeFalse();
        ProcessInputSources.Covers(ProcessCodes.HireFormalise, Formalise.Steps.Single(step => step.Id == "r01-9")).ShouldBeFalse();
        ProcessInputSources.Covers(ProcessCodes.HireFormalise, Docs).ShouldBeTrue();

        ProcessInputSources
            .Resolve(ProcessCodes.Dismissal, Docs, Formalise, Complete(), new Dictionary<string, ProcessStepState>(StringComparer.Ordinal))
            .ByInput.ShouldBeEmpty();
    }

    [Fact]
    public void A_case_on_an_older_pinned_version_gets_the_same_items()
    {
        var older = Formalise with { Version = "0.9" };

        var items = ProcessInputSources.Resolve(
            ProcessCodes.HireFormalise,
            Docs,
            older,
            Employee() with { HasPinfl = true },
            new Dictionary<string, ProcessStepState>(StringComparer.Ordinal));

        items.Of("docs").Single(item => item.Value == "pinfl").State.ShouldBe(ProcessAutoItemState.Known);
        items.Of("docs").Single(item => item.Value == "passport").State.ShouldBe(ProcessAutoItemState.Missing);
    }

    // ─────────────────────────── «Bajarildi»: the client's word does not count ───────────────────────────

    [Fact]
    public void The_clients_marks_on_data_backed_items_are_dropped_and_recomputed()
    {
        var data = Complete() with { Gender = Gender.Female };
        var json = """{"docs":{"passport":"na","pinfl":"na","military":"done","certificates":"done","bankCard":"na","liability":"done"}}""";

        var outcome = Close(Docs.Manual, Resolve(Docs, data), json);

        outcome.IsSuccess.ShouldBeTrue();
        var docs = JsonNode.Parse(outcome.Value.Payload!)!["docs"]!;
        docs["passport"]!.GetValue<string>().ShouldBe("done");
        docs["pinfl"]!.GetValue<string>().ShouldBe("done");
        docs["military"]!.GetValue<string>().ShouldBe("na");
        docs["bankCard"]!.GetValue<string>().ShouldBe("na", "a plain item keeps the client's mark");
    }

    [Fact]
    public void A_tick_on_a_missing_item_does_not_make_it_there()
    {
        var everything = "{\"docs\":{" + string.Join(',', AutoDocs.Concat(PlainDocs).Select(item => $"\"{item}\":\"done\"")) + "}}";

        var outcome = Close(Docs.Manual, Resolve(Docs, Employee()), everything);

        outcome.IsFailure.ShouldBeTrue();
        outcome.Error.Code.ShouldBe("jarayon.malumot_yetishmaydi");
        outcome.Error.Type.ShouldBe(ErrorType.Conflict);
        MissingItemsOf(outcome.Error).ShouldBe(AutoDocs, ignoreOrder: true);
    }

    [Fact]
    public void The_refusal_lists_each_missing_item_with_its_input_and_label()
    {
        var data = Complete() with { HasPassport = false };

        var outcome = Close(Docs.Manual, Resolve(Docs, data), PlainTicks());

        var row = ((IEnumerable<Dictionary<string, string>>)outcome.Error.Details["missing"]).ShouldHaveSingleItem();
        row["input"].ShouldBe("docs");
        row["item"].ShouldBe("passport");
        row["label"].ShouldBe(Docs.Manual!.Inputs[0].Options.Single(option => option.Value == "passport").Label);
    }

    [Fact]
    public void An_unticked_plain_item_is_still_a_wrong_input_with_or_without_a_reason()
    {
        var outcome = Close(Docs.Manual, Resolve(Docs, Employee()), """{"docs":{"certificates":"done"}}""", Reason, canManage: true);

        outcome.Error.ShouldBe(ProcessErrors.Invalid);
    }

    [Fact]
    public void A_full_step_stores_its_marks_with_their_sources_in_canonical_order()
    {
        var data = Employee("Ариза") with { HasPassport = true };
        var input = new ProcessManualInput
        {
            Id = "docs",
            Label = "Hujjatlar",
            Type = ProcessInputType.Checklist,
            Required = true,
            Options = [new("passport", "Pasport"), new("application", "Ariza"), new("bankCard", "Karta")],
        };
        var step = Docs with { Manual = new ProcessManual(true, [input]) };

        var outcome = Close(step.Manual, Resolve(step, data), """{"docs":{"bankCard":"done"}}""");

        outcome.IsSuccess.ShouldBeTrue();
        outcome.Value.MissingOverride.ShouldBeNull();
        outcome.Value.Payload.ShouldBe(
            "{\"autoSources\":{\"docs\":{\"application\":\"doc:\\u0410\\u0440\\u0438\\u0437\\u0430\",\"passport\":\"employee.passport\"}},\"docs\":{\"application\":\"done\",\"bankCard\":\"done\",\"passport\":\"done\"}}");

        ProcessStepPayload.ReadInput(outcome.Value.Payload, "autoSources").ShouldBeNull("the engine's readers don't see it as a scalar input");
    }

    [Fact]
    public void The_hash_covers_the_sources_and_the_override()
    {
        var plain = Close(Docs.Manual, Resolve(Docs, Complete()), PlainTicks()).Value.Payload!;
        var other = Close(Docs.Manual, Resolve(Docs, Complete() with { HasPassport = false }), PlainTicks(), Reason, canManage: true).Value.Payload!;

        JsonNode.Parse(plain)![ProcessManualInputs.AutoSourcesKey].ShouldNotBeNull();
        JsonNode.Parse(other)![ProcessManualInputs.MissingOverrideKey].ShouldNotBeNull();
        ProcessManualInputs.EvidenceOf(plain).ShouldNotBe(ProcessManualInputs.EvidenceOf(other));
    }

    [Theory]
    [InlineData(ProcessManualInputs.AutoSourcesKey)]
    [InlineData(ProcessManualInputs.MissingOverrideKey)]
    public void A_request_cannot_write_the_payloads_own_keys(string key)
    {
        var json = PlainTicks()[..^1] + $",\"{key}\":{{\"docs\":{{\"passport\":\"forged\"}}}}}}";

        Close(Docs.Manual, Resolve(Docs, Complete()), json).Error.ShouldBe(ProcessErrors.Invalid);
    }

    [Fact]
    public void An_optional_input_with_missing_items_does_not_block()
    {
        var optional = Docs.Manual!.Inputs[0] with { Required = false };
        var step = Docs with { Manual = new ProcessManual(true, [optional]) };

        var outcome = Close(step.Manual, Resolve(step, Employee()));

        outcome.IsSuccess.ShouldBeTrue();
        outcome.Value.Payload.ShouldBeNull();
    }

    // ─────────────────────────── The manager's way out ───────────────────────────

    [Fact]
    public void A_manager_closes_with_a_reason_and_the_payload_says_what_was_let_through()
    {
        var data = Complete() with { HasPassport = false, HasPhoto = false };

        var outcome = Close(Docs.Manual, Resolve(Docs, data), PlainTicks(), "  " + Reason + "  ", canManage: true);

        outcome.IsSuccess.ShouldBeTrue();
        outcome.Value.MissingOverride.ShouldNotBeNull().Reason.ShouldBe(Reason);
        outcome.Value.MissingOverride.Items.Select(item => item.Value).ShouldBe(["passport", "photo"]);

        var payload = JsonNode.Parse(outcome.Value.Payload!)!;
        payload[ProcessManualInputs.MissingOverrideKey]!["reason"]!.GetValue<string>().ShouldBe(Reason);
        payload[ProcessManualInputs.MissingOverrideKey]!["items"]!.AsArray().Select(row => row!["item"]!.GetValue<string>()).ShouldBe(["passport", "photo"]);
        payload[ProcessManualInputs.MissingOverrideKey]!["items"]![0]!["input"]!.GetValue<string>().ShouldBe("docs");

        var docs = payload["docs"]!.AsObject();
        docs.ContainsKey("passport").ShouldBeFalse("what is missing gets no mark");
        docs.ContainsKey("photo").ShouldBeFalse();
    }

    [Fact]
    public void A_manager_can_close_when_every_item_of_the_input_is_missing()
    {
        var input = new ProcessManualInput
        {
            Id = "docs",
            Label = "Hujjatlar",
            Type = ProcessInputType.Checklist,
            Required = true,
            Options = [new("passport", "Pasport")],
        };
        var step = Docs with { Manual = new ProcessManual(true, [input]) };

        var outcome = Close(step.Manual, Resolve(step, Employee()), reason: Reason, canManage: true);

        outcome.IsSuccess.ShouldBeTrue();
        var payload = JsonNode.Parse(outcome.Value.Payload!)!.AsObject();
        payload.ContainsKey("docs").ShouldBeFalse();
        payload.ContainsKey(ProcessManualInputs.MissingOverrideKey).ShouldBeTrue();
    }

    [Fact]
    public void A_holder_without_manage_is_forbidden_to_give_a_reason()
        => Close(Docs.Manual, Resolve(Docs, Employee()), PlainTicks(), Reason, canManage: false).Error.ShouldBe(ProcessErrors.Forbidden);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_reason_is_no_reason(string? reason)
        => Close(Docs.Manual, Resolve(Docs, Employee()), PlainTicks(), reason, canManage: true).Error.Code.ShouldBe("jarayon.malumot_yetishmaydi");

    [Fact]
    public void A_reason_too_long_is_refused()
        => Close(Docs.Manual, Resolve(Docs, Employee()), PlainTicks(), new string('a', ProcessCase.MaxCancelReasonLength + 1), canManage: true).Error.ShouldBe(ProcessErrors.Invalid);

    [Fact]
    public void The_longest_reason_is_accepted()
        => Close(Docs.Manual, Resolve(Docs, Employee()), PlainTicks(), new string('a', ProcessCase.MaxCancelReasonLength), canManage: true).IsSuccess.ShouldBeTrue();

    [Fact]
    public void A_reason_with_a_nul_is_refused()
        => Close(Docs.Manual, Resolve(Docs, Employee()), PlainTicks(), "a\0b", canManage: true).Error.ShouldBe(ProcessErrors.Invalid);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_reason_with_nothing_missing_is_ignored(bool canManage)
    {
        var outcome = Close(Docs.Manual, Resolve(Docs, Complete()), PlainTicks(), new string('a', ProcessCase.MaxCancelReasonLength + 1) + "\0", canManage);

        outcome.IsSuccess.ShouldBeTrue();
        outcome.Value.MissingOverride.ShouldBeNull();
        JsonNode.Parse(outcome.Value.Payload!)!.AsObject().ContainsKey(ProcessManualInputs.MissingOverrideKey).ShouldBeFalse();
    }

    [Fact]
    public void A_step_with_no_data_backed_items_closes_as_it_always_did()
    {
        var input = new ProcessManualInput { Id = "payment", Label = "To'lov", Type = ProcessInputType.Text, Required = true };

        Close(new ProcessManual(true, [input]), autoItems: null, """{"payment":"cash"}""").Value.Payload.ShouldBe("""{"payment":"cash"}""");
    }
}
