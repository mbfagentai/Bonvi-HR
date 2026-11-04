using System.Text;
using System.Text.Json.Nodes;
using Bonvi.Application.Blanks.Definitions;
using Bonvi.Application.Blanks.Pdf;
using Bonvi.Domain.Blanks;
using Bonvi.Infrastructure.Blanks.Pdf;
using Microsoft.Extensions.Logging.Abstractions;
using PdfSharp.Pdf.IO;
using SkiaSharp;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Generic blank PDF (SPEC-blankalar §10) — no database, renderer only, every wave-1 template.
/// </summary>
/// <remarks>
/// Answers are GENERATED from each definition (every field filled, free tables at <c>maxRows</c>), so a
/// new field type or template shape is exercised without touching this file. With <c>BONVI_PDF_OUT</c>
/// set the samples are written there for a visual check against the paper blanks.
/// </remarks>
public sealed class BlankPdfRendererTests
{
    private const string CyrillicName = "Каримов Акмал Ўткирович";
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 30, 0, TimeSpan.FromHours(5));

    private readonly BlankPdfRenderer _renderer = new(NullLogger<BlankPdfRenderer>.Instance);

    public static TheoryData<string> Templates => ["HR-F06", "HR-F07", "HR-F08", "HR-F09", "HR-F13"];

    [Theory]
    [MemberData(nameof(Templates))]
    public async Task Full_model_renders_every_field_signature_and_attachment(string code)
    {
        var definition = Load(code);
        var model = FullModel(definition);

        var pdf = await _renderer.RenderAsync(model, TestContext.Current.CancellationToken);
        Save($"{code}-full.pdf", pdf);

        var pages = AssertValidPdf(pdf);
        var text = QuestionnairePdfRendererTests.Matn(pdf);

        text.ShouldContain(definition.Title);
        text.ShouldContain(CyrillicName, customMessage: "Cyrillic glyphs come from the bundled fonts");
        text.ShouldContain("Imzolar");
        text.ShouldContain($"{code} №4821");
        text.ShouldContain("Tasdiqlangan");
        text.ShouldContain("Elektron tasdiqlandi");
        TestContext.Current.TestOutputHelper?.WriteLine($"{code} full: {pages} page(s)");

        if (definition.Fields.Any(field => field.Type == BlankFieldType.File))
        {
            text.ShouldContain("Ilova: dalil-");
        }

        if (definition.Fields.Any(field => field.Type == BlankFieldType.Computed))
        {
            text.ShouldContain("(hisoblangan)");
        }

        AssertDraftLabel(definition, text);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public async Task Minimal_model_renders_an_empty_paper_form(string code)
    {
        var definition = Load(code);
        var model = new BlankPdfModel
        {
            Definition = definition,
            Answers = [],
            GeneratedAt = Now,
        };

        var pdf = await _renderer.RenderAsync(model, TestContext.Current.CancellationToken);
        Save($"{code}-minimal.pdf", pdf);

        var pages = AssertValidPdf(pdf);
        var text = QuestionnairePdfRendererTests.Matn(pdf);

        text.ShouldContain(definition.Title);
        text.ShouldContain("Qoralama");
        TestContext.Current.TestOutputHelper?.WriteLine($"{code} minimal: {pages} page(s)");

        AssertDraftLabel(definition, text);
    }

    [Fact]
    public async Task Broken_signature_image_does_not_fail_the_document()
    {
        var definition = Load("HR-F13");
        var model = FullModel(definition) with
        {
            Steps = [new BlankPdfStep("signEmployee", "Xodim oldim", "subjectEmployee", BlankStepState.Signed)
            {
                Method = BlankSignMethod.InPerson,
                SignerName = CyrillicName,
                ActedAt = Now,
                SignaturePng = [0x89, 0x50, 0x4E, 0x47, 0x00, 0x01],
            }],
        };

        var pdf = await _renderer.RenderAsync(model, TestContext.Current.CancellationToken);

        AssertValidPdf(pdf);
    }

    [Fact]
    public async Task Hidden_field_prints_a_dash_and_choice_prints_every_option()
    {
        // F13 «latenessMinutes»-style extras and the F08 initiative condition are template details;
        // a tiny inline definition pins the generic rules instead.
        var definition = BlankDefinitionValidator.Load("""
            { "schema": 1, "code": "HR-T01", "version": "1.0", "title": "Sinov blankasi", "subject": "none",
              "sections": [ { "id": "s", "title": "Bo'lim", "fields": [
                { "id": "kind", "type": "choice", "label": "Turi", "options": [
                  { "value": "alpha", "label": "Birinchi variant" },
                  { "value": "beta", "label": "Ikkinchi variant", "extra": { "type": "text", "label": "Izoh", "required": true } } ] },
                { "id": "detail", "type": "text", "label": "Tafsilot", "when": { "field": "kind", "eq": "alpha" } },
                { "id": "salary", "type": "money", "label": "Oklad" } ] } ] }
            """);

        var model = new BlankPdfModel
        {
            Definition = definition,
            Answers = JsonNode.Parse("""{ "kind": { "value": "beta", "extra": "maxsus holat" }, "detail": "yashirin", "salary": 3000000 }""")!.AsObject(),
            GeneratedAt = Now,
        };

        var text = QuestionnairePdfRendererTests.Matn(await _renderer.RenderAsync(model, TestContext.Current.CancellationToken));

        text.ShouldContain("Birinchi variant");
        text.ShouldContain("Ikkinchi variant: maxsus holat");
        text.ShouldNotContain("yashirin");
        text.ShouldContain("Tafsilot —");
        text.ShouldMatch(@"3\s?000\s?000 so'm", "money is grouped by thousands");
    }

    // ════════════════════════════════════════════════ helpers

    private static void AssertDraftLabel(BlankDefinition definition, string text)
    {
        if (definition.LegalStatus == "draft")
        {
            text.ShouldContain("Loyiha — yurist tasdig'idan o'tmagan");
        }
        else
        {
            text.ShouldNotContain("Loyiha");
        }
    }

    private static int AssertValidPdf(byte[] pdf)
    {
        Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");

        using var stream = new MemoryStream(pdf);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        document.PageCount.ShouldBeGreaterThanOrEqualTo(1);

        return document.PageCount;
    }

    private static BlankDefinition Load(string code)
    {
        var assembly = typeof(BlankPdfRenderer).Assembly;
        var name = assembly.GetManifestResourceNames().Single(resource => resource.EndsWith($".{code}.json", StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);

        return BlankDefinitionValidator.Load(reader.ReadToEnd());
    }

    private static void Save(string name, byte[] pdf)
    {
        if (Environment.GetEnvironmentVariable("BONVI_PDF_OUT") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name), pdf);
        }
    }

    // ════════════════════════════════════════════════ generated full model

    private static BlankPdfModel FullModel(BlankDefinition definition)
    {
        var attachments = new List<BlankPdfAttachment>();
        var answers = new JsonObject();

        foreach (var field in definition.Fields)
        {
            if (Sample(field, field.Id, attachments) is { } value)
            {
                answers[field.Id] = value;
            }
        }

        var signature = SignaturePng();
        var steps = definition.Steps
            .Select((step, index) =>
            {
                var inPerson = step.Mode == Application.Blanks.Definitions.BlankStepMode.InPerson;

                return new BlankPdfStep(step.Id, step.Title, step.Actor, BlankStepState.Signed)
                {
                    Decision = step.Decision?.Options[0].Value,
                    Note = step.Decision is null ? null : "Izoh: hammasi joyida",
                    SignerName = index % 2 == 0 ? CyrillicName : "Toshmatova Dilnoza Rustamovna",
                    ActedAt = Now.AddDays(index - definition.Steps.Count),
                    Method = inPerson ? BlankSignMethod.InPerson : BlankSignMethod.Electronic,
                    SignaturePng = inPerson ? signature : null,
                };
            })
            .ToList();

        return new BlankPdfModel
        {
            Definition = definition,
            Answers = answers,
            Computed = BlankComputedEvaluator.ComputeAll(definition, answers),
            Number = 4821,
            Status = BlankSubmissionStatus.Approved,
            DocumentDate = new DateOnly(2026, 10, 24),
            SubjectName = CyrillicName,
            DepartmentName = "Metan zavodi — ishlab chiqarish bo'limi",
            Steps = steps,
            Attachments = attachments,
            GeneratedAt = Now,
        };
    }

    /// <summary>A realistic stored value for any field type; <c>null</c> for non-stored types.</summary>
    private static JsonNode? Sample(BlankField field, string path, List<BlankPdfAttachment> attachments)
    {
        switch (field.Type)
        {
            case BlankFieldType.Text:
                return Clip($"{field.Label ?? field.Id}: qiymat — {CyrillicName}", field.EffectiveMaxLength);
            case BlankFieldType.LongText:
                return Clip(string.Join('\n', Enumerable.Repeat(
                    "Xodim vazifani o'z vaqtida bajardi, ustoz bilan har kuni uchrashdi. Ходим вазифани ўз вақтида бажарди.", 4)),
                    field.EffectiveMaxLength);
            case BlankFieldType.WholeNumber:
                return (int)(field.Min?.Number ?? 0) + 3;
            case BlankFieldType.Percent:
                return 85.5m;
            case BlankFieldType.Money:
                return 3_000_000;
            case BlankFieldType.Date:
                return "2026-10-24";
            case BlankFieldType.DateTime:
                return "2026-10-24T09:15:00+05:00";
            case BlankFieldType.Month:
                return "2026-10";
            case BlankFieldType.Year:
                return 2026;
            case BlankFieldType.DateRange:
                return new JsonObject { ["from"] = "2026-10-01", ["to"] = "2026-10-24" };
            case BlankFieldType.YesNo:
            case BlankFieldType.Check:
                return true;
            case BlankFieldType.Score:
                return 4;
            case BlankFieldType.Choice:
                return Pick(field.Options[^1], path, attachments);
            case BlankFieldType.Multi:
                return new JsonArray([.. field.Options.Take(field.MaxSelected ?? 2).Select(option => (JsonNode)Pick(option, path, attachments))]);
            case BlankFieldType.Ref:
                return new JsonObject { ["id"] = Guid.CreateVersion7().ToString(), ["label"] = CyrillicName };
            case BlankFieldType.DocRef:
                return new JsonObject
                {
                    ["submissionId"] = Guid.CreateVersion7().ToString(),
                    ["templateCode"] = field.TemplateCodes.Count > 0 ? field.TemplateCodes[0] : "HR-F06",
                    ["number"] = 12,
                };
            case BlankFieldType.Order:
                return new JsonObject { ["number"] = "215-k", ["date"] = "2026-10-25" };
            case BlankFieldType.File:
                var ids = new JsonArray();
                for (var index = 0; index < Math.Min(field.EffectiveMaxFiles, 3); index++)
                {
                    var id = Guid.CreateVersion7();
                    attachments.Add(new BlankPdfAttachment(id, path, $"dalil-{index + 1}.pdf", BlankAttachmentKind.Pdf, 1_250_000 + (index * 4096)));
                    ids.Add(id.ToString());
                }

                return ids;
            case BlankFieldType.Table:
                return TableRows(field, path, attachments);
            default:
                return null; // static, computed
        }
    }

    private static JsonArray TableRows(BlankField table, string path, List<BlankPdfAttachment> attachments)
    {
        var rows = new JsonArray();
        var count = table.FixedRows.Count > 0 ? table.FixedRows.Count : table.EffectiveMaxRows;

        for (var index = 0; index < count; index++)
        {
            var row = new JsonObject();

            if (table.FixedRows.Count > 0)
            {
                row["_row"] = table.FixedRows[index].Id;
            }

            foreach (var column in table.Columns)
            {
                if (Sample(column, $"{path}[{index}].{column.Id}", attachments) is { } value)
                {
                    row[column.Id] = value;
                }
            }

            rows.Add(row);
        }

        return rows;
    }

    private static JsonObject Pick(BlankOption option, string path, List<BlankPdfAttachment> attachments)
    {
        var pick = new JsonObject { ["value"] = option.Value };

        if (option.Extra is { } extra)
        {
            pick["extra"] = Sample(new BlankField { Id = "extra", Type = extra.Type, RefKind = extra.RefKind, TemplateCodes = extra.TemplateCodes }, path, attachments);
        }

        return pick;
    }

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];

    private static byte[] SignaturePng()
    {
        var info = new SKImageInfo(300, 100, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint { Color = SKColors.Navy, StrokeWidth = 4, IsAntialias = true, Style = SKPaintStyle.Stroke };
        surface.Canvas.DrawLine(10, 80, 120, 20, paint);
        surface.Canvas.DrawLine(120, 20, 290, 70, paint);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }
}
