using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Blanks;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>Real blanks for the engine's tests: submitted and signed through the aggregate, as the facts read them.</summary>
internal static class ProcessTestBlanks
{
    /// <summary>A blank in the state a test needs: draft, submitted, signed by the named steps, or cancelled.</summary>
    public static async Task<Guid> CreateAsync(
        AppDbContext db,
        string code,
        BlankSubject subject,
        string answers,
        string computed,
        bool submit,
        string[]? sign,
        IReadOnlyDictionary<string, string>? decisions,
        bool cancel,
        CancellationToken cancellationToken)
    {
        var template = await db.BlankTemplates.SingleAsync(row => row.Code == code, cancellationToken);
        var version = await db.BlankTemplateVersions
            .SingleAsync(row => row.TemplateId == template.Id && row.Status == BlankTemplateVersionStatus.Published, cancellationToken);

        var state = new BlankAnswersState(answers, computed, "[]", 0, null);
        var blank = BlankSubmission.Create(template, version, subject, state).Value;
        var now = DateTimeOffset.UtcNow;

        if (submit)
        {
            blank.Submit(RandomNumberGenerator.GetInt32(100_000, int.MaxValue), StepPlan(version.Definition), state, now)
                .IsSuccess.ShouldBeTrue();

            foreach (var stepCode in sign ?? [])
            {
                SignOpenStep(blank, stepCode, decisions?.GetValueOrDefault(stepCode), now);
            }
        }

        if (cancel)
        {
            blank.Cancel("test", now).IsSuccess.ShouldBeTrue();
        }

        db.BlankSubmissions.Add(blank);
        await db.SaveChangesAsync(cancellationToken);
        return blank.Id;
    }

    public static async Task<int> SignAsync(
        AppDbContext db,
        Guid blankId,
        string[] stepCodes,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? decisions = null)
    {
        var blank = await db.BlankSubmissions.Include(row => row.Steps).SingleAsync(row => row.Id == blankId, cancellationToken);
        foreach (var stepCode in stepCodes)
        {
            SignOpenStep(blank, stepCode, decisions?.GetValueOrDefault(stepCode), DateTimeOffset.UtcNow);
        }

        return await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>HR-F04 answers: the council's table of candidates and decisions.</summary>
    public static string CouncilAnswers(params (Guid Candidate, string Decision)[] rows)
    {
        var table = new JsonArray();
        foreach (var (candidate, decision) in rows)
        {
            table.Add(new JsonObject
            {
                ["candidate"] = new JsonObject { ["id"] = candidate.ToString(), ["label"] = "Test" },
                ["decision"] = new JsonObject { ["value"] = decision },
            });
        }

        return new JsonObject { ["meetingDate"] = "2026-11-05", ["candidates"] = table }.ToJsonString();
    }

    /// <summary>
    /// HR-F03/F04/F05/F11 stand-ins (only the §5.2 step ids — the facts read nothing else of the
    /// definition). They live in the calling class's database only; idempotent across its tests.
    /// </summary>
    public static async Task<int> EnsureStandInsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        (string Code, BlankSubjectKind Subject, string Steps)[] standIns =
        [
            (ProcessBlanks.F03, BlankSubjectKind.Candidate,
                """[{"id":"consent","order":1,"actor":"candidate","mode":"inPerson"},{"id":"hr4Check","order":2,"actor":"hr4","mode":"electronic"},{"id":"hr1Approve","order":3,"actor":"hr1","mode":"electronic"}]"""),
            (ProcessBlanks.F04, BlankSubjectKind.None,
                """[{"id":"secretary","order":1,"actor":"hr1","mode":"electronic"},{"id":"chair","order":2,"actor":"council","mode":"either"}]"""),
            (ProcessBlanks.F05, BlankSubjectKind.Candidate,
                """[{"id":"offerHr4","order":1,"actor":"hr4","mode":"electronic"},{"id":"offerCandidate","order":2,"actor":"candidate","mode":"inPerson"},{"id":"docsHandover","order":3,"actor":"hr4","mode":"electronic"},{"id":"docsHr2","order":4,"actor":"hr2","mode":"electronic"},{"id":"docsCandidate","order":4,"actor":"candidate","mode":"inPerson"}]"""),
            (ProcessBlanks.F11, BlankSubjectKind.Employee,
                """[{"id":"signEmployee","order":1,"actor":"subjectEmployee","mode":"either"},{"id":"signIntroducer","order":1,"actor":"hr4","mode":"electronic"}]"""),
        ];

        foreach (var (code, subject, steps) in standIns)
        {
            if (await db.BlankTemplates.AnyAsync(row => row.Code == code, cancellationToken))
            {
                continue;
            }

            var template = BlankTemplate.Create(code, code + " (test stand-in)", subject).Value;
            db.BlankTemplates.Add(template);

            var definition = $$$"""{"schema":1,"code":"{{{code}}}","version":"1.0","steps":{{{steps}}}}""";
            var sha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(definition)));
            var version = BlankTemplateVersion.Create(template.Id, "1.0", definition, sha, BlankTemplateSource.Imported).Value;
            version.Publish(null, DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
            db.BlankTemplateVersions.Add(version);
        }

        return await db.SaveChangesAsync(cancellationToken);
    }

    private static void SignOpenStep(BlankSubmission blank, string stepCode, string? decision, DateTimeOffset at)
    {
        var step = blank.Steps.Single(row => row.StepCode == stepCode);
        var act = step.Mode == BlankStepMode.InPerson
            ? new BlankStepAct(BlankSignMethod.InPerson, null, SignatureStorageKey: "test/signature.png", Decision: decision)
            : new BlankStepAct(BlankSignMethod.Electronic, null, Decision: decision);

        blank.SignStep(step.Id, act, state: null, at).IsSuccess.ShouldBeTrue($"step {stepCode} should be open");
    }

    /// <summary>The step plan of a template version: its definition's <c>steps</c> (id, order, actor, mode).</summary>
    private static List<BlankStepPlan> StepPlan(string definition)
    {
        var steps = JsonNode.Parse(definition)!["steps"]!.AsArray();
        return steps.Select(node => new BlankStepPlan(
                (string)node!["id"]!,
                (int)node["order"]!,
                (string?)node["title"] ?? (string)node["id"]!,
                (string)node["actor"]!,
                (string)node["mode"]! switch
                {
                    "electronic" => BlankStepMode.Electronic,
                    "inPerson" => BlankStepMode.InPerson,
                    _ => BlankStepMode.Either,
                }))
            .ToList();
    }
}
