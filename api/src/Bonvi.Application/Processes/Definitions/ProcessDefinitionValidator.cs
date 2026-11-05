using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Bonvi.Application.Processes.Definitions;

/// <summary>Validation result: the model (only when there is no error) and every error found.</summary>
public sealed record ProcessDefinitionValidation(ProcessDefinition? Definition, IReadOnlyList<ProcessDefinitionError> Errors)
{
    public bool IsValid => Definition is not null && Errors.Count == 0;
}

/// <summary>
/// The single check of a process template (SPEC-mening-ishlarim §3.1 «Validator») — run by the seeder; an
/// invalid file is a startup error in Development and a logged, skipped template in Production.
/// </summary>
/// <remarks>
/// <para>
/// Rejects: unknown keys / kinds / roles / facts / anchors / screens (shape part in
/// <see cref="ProcessDefinitionReader"/>); duplicate ids; prereq cycles; a prereq or <c>step:</c> anchor
/// pointing to an unknown id; a <c>manual</c> step without <c>manual.allowed</c>; an <c>auto</c> step with
/// <c>due</c>, <c>manual</c> or <c>gate</c>; a gate code outside §8.1; a blank code outside HR-F01…F19.
/// </para>
/// <para>
/// Beyond the SPEC list it also rejects what would only fail later at run time: a column that is not on the
/// board, a fact / auto step without <c>completion</c>, a manual input reference that names no input, option
/// lists on a non-choice input, link args the screen does not take.
/// </para>
/// <para>Every error is returned at once, with its path, as in <c>BlankDefinitionValidator</c>.</para>
/// </remarks>
public static partial class ProcessDefinitionValidator
{
    private static readonly HashSet<string> Signatures = new(StringComparer.Ordinal) { "D1", "D2", "D3" };

    /// <summary>Reads and checks a template.</summary>
    /// <param name="json">Template text.</param>
    /// <param name="expectedCode">Code from the file name (when given, must be equal).</param>
    public static ProcessDefinitionValidation Validate(string json, string? expectedCode = null)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonNode? root;

        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException error)
        {
            return new ProcessDefinitionValidation(null, [new ProcessDefinitionError("$", $"Not valid JSON: {error.Message}")]);
        }

        var reader = new ProcessDefinitionReader();
        var definition = reader.ReadDefinition(root);

        // A shape error stops here: checking a half-read model would only add misleading errors
        if (reader.Errors.Count > 0 || definition is null)
        {
            return new ProcessDefinitionValidation(null, reader.Errors);
        }

        var errors = new Check(definition).Run(expectedCode);
        return new ProcessDefinitionValidation(errors.Count == 0 ? definition : null, errors);
    }

    /// <summary>Loads a template that was validated before (seeded); invalid now = a programming error.</summary>
    public static ProcessDefinition Load(string json)
    {
        var result = Validate(json);

        return result.IsValid
            ? result.Definition!
            : throw new InvalidOperationException("Process template is invalid: " + string.Join("; ", result.Errors));
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,47}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"^\d{1,3}\.\d{1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    /// <summary>Step ids: <c>r02-1</c>, <c>mm</c>, <c>ex-8b</c>, <c>p03-0</c>.</summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex StepIdPattern();

    /// <summary>Column keys, input ids and option values: camelCase.</summary>
    [GeneratedRegex("^[a-z][A-Za-z0-9]{0,47}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^HR-F(0[1-9]|1[0-9])$", RegexOptions.CultureInvariant)]
    private static partial Regex BlankCodePattern();

    /// <summary>One validation run.</summary>
    private sealed class Check(ProcessDefinition definition)
    {
        private readonly List<ProcessDefinitionError> errors = [];

        private readonly HashSet<string> stepIds = new(StringComparer.Ordinal);

        public List<ProcessDefinitionError> Run(string? expectedCode)
        {
            CheckHeader(expectedCode);
            CheckBoard();
            CheckStepIds();

            for (var i = 0; i < definition.Steps.Count; i++)
            {
                CheckStep(definition.Steps[i], $"steps[{i}]");
            }

            CheckCycles();
            return errors;
        }

        private void Add(string path, string message) => errors.Add(new ProcessDefinitionError(path, message));

        private void CheckHeader(string? expectedCode)
        {
            if (definition.Schema != ProcessDefinition.CurrentSchema)
            {
                Add("$.schema", $"Only schema {ProcessDefinition.CurrentSchema} is supported.");
            }

            if (!CodePattern().IsMatch(definition.Code))
            {
                Add("$.code", "Code must be kebab-case, e.g. hire-request.");
            }
            else if (expectedCode is not null && definition.Code != expectedCode)
            {
                Add("$.code", $"Code «{definition.Code}» does not match the file name «{expectedCode}».");
            }

            if (!VersionPattern().IsMatch(definition.Version))
            {
                Add("$.version", "Version must look like 1.0.");
            }

            if (!ProcessDefinition.SubjectKinds.Contains(definition.SubjectKind))
            {
                Add("$.subjectKind", $"Unknown subject kind «{definition.SubjectKind}».");
            }

            if (definition.Steps.Count == 0)
            {
                Add("$.steps", "A process needs at least one step.");
            }
        }

        private void CheckBoard()
        {
            if (definition.Board.Count == 0)
            {
                Add("$.board", "The board needs at least one column.");
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < definition.Board.Count; i++)
            {
                var key = definition.Board[i].Key;

                if (!KeyPattern().IsMatch(key))
                {
                    Add($"board[{i}].key", "Column key must be camelCase.");
                }
                else if (!keys.Add(key))
                {
                    Add($"board[{i}].key", $"Duplicate column «{key}».");
                }
            }
        }

        private void CheckStepIds()
        {
            for (var i = 0; i < definition.Steps.Count; i++)
            {
                var id = definition.Steps[i].Id;

                if (!StepIdPattern().IsMatch(id))
                {
                    Add($"steps[{i}].id", "Step id must be lower-case letters, digits and dashes.");
                }
                else if (!stepIds.Add(id))
                {
                    Add($"steps[{i}].id", $"Duplicate step id «{id}».");
                }
            }
        }

        private void CheckStep(ProcessStep step, string path)
        {
            if (!definition.Board.Any(column => column.Key == step.Column))
            {
                Add($"{path}.column", $"Column «{step.Column}» is not on the board.");
            }

            CheckRoles(step, path);

            if (step.Blank is not null && !BlankCodePattern().IsMatch(step.Blank))
            {
                Add($"{path}.blank", $"Blank «{step.Blank}» is not HR-F01…HR-F19.");
            }

            if (step.Signature is not null && !Signatures.Contains(step.Signature))
            {
                Add($"{path}.signature", $"Unknown signature «{step.Signature}» (D1, D2, D3).");
            }

            for (var i = 0; i < step.Prereqs.Count; i++)
            {
                var prereq = step.Prereqs[i];

                if (prereq == step.Id)
                {
                    Add($"{path}.prereqs[{i}]", "A step cannot be its own prereq.");
                }
                else if (!stepIds.Contains(prereq))
                {
                    Add($"{path}.prereqs[{i}]", $"Unknown step «{prereq}».");
                }
            }

            if (step.When is { } when)
            {
                CheckFact(when.Fact, $"{path}.when.fact");
            }

            if (step.Due is { } due)
            {
                CheckDue(due, step.Id, $"{path}.due");
            }

            if (step.SkipIfDueOnOrAfter is { } cutoff)
            {
                CheckDue(cutoff, step.Id, $"{path}.skipIfDueOnOrAfter");
            }

            CheckGates(step, path);
            CheckKind(step, path);

            if (step.Completion is { } completion)
            {
                CheckCompletion(completion, step, $"{path}.completion");
            }

            if (step.Manual is { } manual)
            {
                CheckInputs(manual.Inputs, $"{path}.manual");
            }

            CheckLink(step.Link, $"{path}.link");
        }

        private void CheckRoles(ProcessStep step, string path)
        {
            if (!ProcessRoles.IsKnown(step.Role))
            {
                Add($"{path}.role", $"Unknown role «{step.Role}».");
            }

            if (step.FallbackRole is not { } fallback)
            {
                return;
            }

            if (!ProcessRoles.IsKnown(fallback))
            {
                Add($"{path}.fallbackRole", $"Unknown role «{fallback}».");
            }
            else if (!ProcessRoles.Office.Contains(fallback))
            {
                // The fallback exists because a person role may resolve to nobody — it must be assignable
                Add($"{path}.fallbackRole", "Fallback must be an office role.");
            }
            else if (fallback == step.Role)
            {
                Add($"{path}.fallbackRole", "Fallback equals the role.");
            }
        }

        private void CheckFact(string fact, string path)
        {
            if (!ProcessFactNames.IsKnown(fact))
            {
                Add(path, $"Unknown fact «{fact}».");
            }
        }

        private void CheckDue(ProcessDueRule due, string stepId, string path)
        {
            var rulePath = path;

            foreach (var rule in due.Chain())
            {
                if (ProcessAnchors.TryGetStepId(rule.From, out var anchorStep))
                {
                    if (anchorStep == stepId)
                    {
                        Add($"{rulePath}.from", "A step cannot count its due from its own completion.");
                    }
                    else if (!stepIds.Contains(anchorStep))
                    {
                        Add($"{rulePath}.from", $"Anchor points to unknown step «{anchorStep}».");
                    }
                }
                else if (!ProcessAnchors.IsKnown(rule.From))
                {
                    Add($"{rulePath}.from", $"Unknown anchor «{rule.From}».");
                }

                rulePath += ".fallback";
            }
        }

        private void CheckGates(ProcessStep step, string path)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < step.Gates.Count; i++)
            {
                var code = step.Gates[i].Code;
                var gatePath = step.Gates.Count == 1 ? $"{path}.gate.code" : $"{path}.gate[{i}].code";

                if (!ProcessGateCodes.All.Contains(code))
                {
                    Add(gatePath, $"Gate «{code}» is not in the §8.1 catalogue.");
                }
                else if (!seen.Add(code))
                {
                    Add(gatePath, $"Gate «{code}» is listed twice.");
                }
            }

            // «Kerak emas» works before the step opens, so it would get past the gate unnoticed. legacySkippable is
            // fine: a legacy case has no blocking gates (§7.6).
            if (step.Skippable && step.Gates.Any(gate => gate.Mode == ProcessGateMode.Block))
            {
                Add($"{path}.skippable", "A step behind a block gate cannot be skippable.");
            }
        }

        private void CheckKind(ProcessStep step, string path)
        {
            if (step.Manual is { Allowed: false })
            {
                Add($"{path}.manual.allowed", "manual.allowed is false — drop the manual key instead.");
            }

            switch (step.Kind)
            {
                case ProcessStepKind.Auto:
                    if (step.Due is not null)
                    {
                        Add($"{path}.due", "An auto step has no due.");
                    }

                    if (step.Manual is not null)
                    {
                        Add($"{path}.manual", "An auto step cannot be closed by hand.");
                    }

                    if (step.Gates.Count > 0)
                    {
                        Add($"{path}.gate", "An auto step has no gate.");
                    }

                    if (step.Completion is null)
                    {
                        Add($"{path}.completion", "An auto step needs a completion fact.");
                    }

                    break;

                case ProcessStepKind.Fact:
                    if (step.Completion is null)
                    {
                        Add($"{path}.completion", "A fact step needs a completion fact.");
                    }

                    break;

                case ProcessStepKind.Manual:
                    if (step.Manual is not { Allowed: true })
                    {
                        Add($"{path}.manual", "A manual step needs manual.allowed = true.");
                    }

                    if (step.Completion is not null)
                    {
                        Add($"{path}.completion", "A manual step closes only by «Bajarildi» — no completion fact.");
                    }

                    break;
            }
        }

        private void CheckCompletion(ProcessCompletion completion, ProcessStep step, string path)
        {
            CheckFact(completion.Fact, $"{path}.fact");

            var argsPath = $"{path}.args";

            for (var i = 0; i < completion.AllOf.Count; i++)
            {
                CheckFact(completion.AllOf[i], $"{argsPath}.allOf[{i}]");
            }

            for (var i = 0; i < completion.LegacyAnyOf.Count; i++)
            {
                CheckFact(completion.LegacyAnyOf[i], $"{argsPath}.legacyAnyOf[{i}]");
            }

            var valueTests = (completion.EqualsValue is null ? 0 : 1) + (completion.InValues.Count == 0 ? 0 : 1)
                + (completion.ByValue.Count == 0 ? 0 : 1);

            if (valueTests > 1)
            {
                Add(argsPath, "Use only one of equals, in, byValue.");
            }

            if (completion.ManualValue is { } manualValue
                && step.Manual?.Inputs.Any(input => input.Id == manualValue) != true)
            {
                Add($"{argsPath}.manualValue", $"No manual input «{manualValue}» on this step.");
            }

            foreach (var (value, branch) in completion.ByValue)
            {
                CheckCompletion(branch, step, $"{argsPath}.byValue.{value}");
            }
        }

        private void CheckInputs(IReadOnlyList<ProcessManualInput> inputs, string path)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < inputs.Count; i++)
            {
                var input = inputs[i];
                var inputPath = $"{path}.inputs[{i}]";

                if (!KeyPattern().IsMatch(input.Id))
                {
                    Add($"{inputPath}.id", "Input id must be camelCase.");
                }
                else if (!ids.Add(input.Id))
                {
                    Add($"{inputPath}.id", $"Duplicate input id «{input.Id}».");
                }

                CheckOptions(input, inputPath);

                if (input.Type != ProcessInputType.WholeNumber && (input.Min is not null || input.Max is not null))
                {
                    Add(inputPath, "min / max are for int inputs only.");
                }
                else if (input.Min > input.Max)
                {
                    Add($"{inputPath}.min", "min is greater than max.");
                }

                if (input.RequiredWhen is { } when)
                {
                    CheckRequiredWhen(when, input, inputs, $"{inputPath}.requiredWhen");
                }
            }
        }

        private void CheckOptions(ProcessManualInput input, string path)
        {
            var hasOptions = input.Type is ProcessInputType.Choice or ProcessInputType.Checklist;

            if (!hasOptions)
            {
                if (input.Options.Count > 0)
                {
                    Add($"{path}.options", "Options are for choice and checklist inputs only.");
                }

                return;
            }

            if (input.Options.Count == 0)
            {
                Add($"{path}.options", "A choice or checklist input needs options.");
            }

            var values = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < input.Options.Count; i++)
            {
                var value = input.Options[i].Value;

                if (!KeyPattern().IsMatch(value))
                {
                    Add($"{path}.options[{i}].value", "Option value must be camelCase.");
                }
                else if (!values.Add(value))
                {
                    Add($"{path}.options[{i}].value", $"Duplicate option «{value}».");
                }
            }
        }

        private void CheckRequiredWhen(
            ProcessRequiredWhen when, ProcessManualInput input, IReadOnlyList<ProcessManualInput> inputs, string path)
        {
            if (input.Required)
            {
                Add(path, "required is true — requiredWhen would never apply.");
            }

            if ((when.Input is null) == (when.Fact is null))
            {
                Add(path, "Exactly one of input or fact is required.");
            }
            else if (when.Input is { } other)
            {
                if (other == input.Id || !inputs.Any(candidate => candidate.Id == other))
                {
                    Add($"{path}.input", $"No other input «{other}» on this step.");
                }
            }
            else
            {
                CheckFact(when.Fact!, $"{path}.fact");
            }
        }

        private void CheckLink(ProcessLink link, string path)
        {
            if (!ProcessScreens.AllowedArgs.TryGetValue(link.Screen, out var allowed))
            {
                Add($"{path}.screen", $"Unknown screen «{link.Screen}».");
                return;
            }

            foreach (var key in link.Args.Keys.Where(key => !allowed.Contains(key)))
            {
                Add($"{path}.args.{key}", $"Screen «{link.Screen}» takes no arg «{key}».");
            }

            if (ProcessScreens.RequiredArgs.TryGetValue(link.Screen, out var required))
            {
                foreach (var key in required.Where(key => !link.Args.ContainsKey(key)))
                {
                    Add($"{path}.args.{key}", $"Screen «{link.Screen}» needs arg «{key}».");
                }
            }

            if (link.Args.TryGetValue(ProcessScreens.TemplateArg, out var template) && !BlankCodePattern().IsMatch(template))
            {
                Add($"{path}.args.{ProcessScreens.TemplateArg}", $"Blank «{template}» is not HR-F01…HR-F19.");
            }

            if (link.Args.TryGetValue(ProcessScreens.SubjectArg, out var subject) && !ProcessScreens.BlankSubjects.Contains(subject))
            {
                Add($"{path}.args.{ProcessScreens.SubjectArg}", $"Unknown blank subject «{subject}» (candidate, employee).");
            }

            if (link.Args.TryGetValue(ProcessScreens.UrlArg, out var url)
                && !(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
            {
                Add($"{path}.args.{ProcessScreens.UrlArg}", "An external link must be an absolute https URL.");
            }
        }

        /// <summary>Prereq graph must be acyclic (a self-prereq is reported by CheckStep); a cycle is reported as a path.</summary>
        private void CheckCycles()
        {
            var graph = definition.Steps
                .GroupBy(step => step.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Prereqs.Where(id => id != group.Key && stepIds.Contains(id)).ToList(), StringComparer.Ordinal);

            // 0 = not visited, 1 = on the current path, 2 = done
            var state = new Dictionary<string, int>(StringComparer.Ordinal);
            var stack = new List<string>();

            foreach (var node in graph.Keys)
            {
                Visit(node, graph, state, stack);
            }
        }

        private void Visit(string node, Dictionary<string, List<string>> graph, Dictionary<string, int> state, List<string> stack)
        {
            var current = state.GetValueOrDefault(node);

            if (current == 2)
            {
                return;
            }

            if (current == 1)
            {
                var cycle = stack.Skip(stack.IndexOf(node)).Append(node);
                Add("$.steps", $"Prereq cycle: {string.Join(" → ", cycle)}.");
                return;
            }

            state[node] = 1;
            stack.Add(node);

            foreach (var next in graph[node])
            {
                Visit(next, graph, state, stack);
            }

            stack.RemoveAt(stack.Count - 1);
            state[node] = 2;
        }
    }
}
