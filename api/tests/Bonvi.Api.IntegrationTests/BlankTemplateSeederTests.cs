using System.Text.Json.Nodes;
using Bonvi.Application.Blanks.Definitions;
using Bonvi.Domain.Blanks;
using Bonvi.Infrastructure.Blanks;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <see cref="BlankTemplateSeeder"/> haqiqiy PostgreSQL ustida (SPEC-blankalar §3.2, §14 S3).
/// </summary>
/// <remarks>
/// Fabrika ishga tushganda <c>DatabaseInitializer</c> 1-to'lqin shablonlarini o'zi urug'laydi — birinchi test
/// shuni tekshiradi. Versiya almashinuvi esa alohida sinov kodi (<c>HR-T99</c>) bilan: haqiqiy shablonlarning
/// e'lon qilingan versiyasiga tegilmaydi.
/// </remarks>
public sealed class BlankTemplateSeederTests : IClassFixture<BonviApiFactory>
{
    private static readonly string[] WaveOne = ["HR-F06", "HR-F07", "HR-F08", "HR-F09", "HR-F13"];

    private readonly BonviApiFactory _factory;

    public BlankTemplateSeederTests(BonviApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Ishga_tushishda_5_shablon_va_5_ta_eLon_qilingan_versiya_paydo_boladi()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var templates = await context.BlankTemplates.AsNoTracking()
            .Where(row => WaveOne.Contains(row.Code))
            .ToListAsync(ct);
        templates.Select(row => row.Code).ShouldBe(WaveOne, ignoreOrder: true);
        templates.ShouldAllBe(row => row.IsActive && row.SubjectKind == BlankSubjectKind.Employee);

        var ids = templates.Select(row => row.Id).ToList();
        var versions = await context.BlankTemplateVersions.AsNoTracking()
            .Where(row => ids.Contains(row.TemplateId))
            .ToListAsync(ct);

        versions.Count.ShouldBe(5);
        versions.ShouldAllBe(row => row.Status == BlankTemplateVersionStatus.Published
            && row.Version == "1.0"
            && row.Source == BlankTemplateSource.Seed
            && row.PublishedAt != null);

        // jsonb'dan qaytgan ta'rif hamon yaroqli va xeshi kanonik shakliga mos
        foreach (var version in versions)
        {
            BlankDefinitionValidator.Load(version.Definition).Version.ShouldBe("1.0");
            BlankTemplateSeeder.Sha256Hex(BlankTemplateSeeder.Canonicalize(version.Definition))
                .ShouldBe(version.DefinitionSha256);
        }
    }

    [Fact]
    public async Task Qayta_urugLash_hech_narsa_qoshmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<BlankTemplateSeeder>();

        var before = await context.BlankTemplateVersions.CountAsync(ct);

        var result = await seeder.SeedAsync(ct);

        result.ShouldBe(new BlankTemplateSeedResult(0, 0, 5, 0, 0));
        (await context.BlankTemplateVersions.CountAsync(ct)).ShouldBe(before);
        (await context.BlankTemplates.CountAsync(row => WaveOne.Contains(row.Code), ct)).ShouldBe(5);
    }

    [Fact]
    public async Task Versiya_almashinuvi_va_bir_xil_versiyada_boshqa_xesh()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<BlankTemplateSeeder>();

        // 1. Yangi shablon → yaratiladi va e'lon qilinadi; ikkinchi marta — o'zgarishsiz
        (await seeder.SeedAsync([Probe("1.0", "Sinov blankasi")], ct))
            .ShouldBe(new BlankTemplateSeedResult(1, 1, 0, 0, 0));
        (await seeder.SeedAsync([Probe("1.0", "Sinov blankasi", indented: true)], ct))
            .ShouldBe(new BlankTemplateSeedResult(0, 0, 1, 0, 0), "formatlash xeshni o'zgartirmaydi");

        var original = await VersionsAsync(context, ct);
        var stored = original.Single();

        // 2. O'sha versiya, boshqa mazmun → ziddiyat, bazadagisi QAYTA YOZILMAYDI
        (await seeder.SeedAsync([Probe("1.0", "Boshqa nom")], ct))
            .ShouldBe(new BlankTemplateSeedResult(0, 0, 0, 1, 0));

        context.ChangeTracker.Clear();
        var afterConflict = (await VersionsAsync(context, ct)).Single();
        afterConflict.DefinitionSha256.ShouldBe(stored.DefinitionSha256);
        afterConflict.Definition.ShouldBe(stored.Definition);
        (await context.BlankTemplates.AsNoTracking().SingleAsync(row => row.Code == "HR-T99", ct))
            .Title.ShouldBe("Sinov blankasi");

        // 3. Yangi versiya → e'lon qilinadi, avvalgisi eskiradi, nom yangilanadi
        (await seeder.SeedAsync([Probe("1.1", "Sinov blankasi (yangi)")], ct))
            .ShouldBe(new BlankTemplateSeedResult(0, 1, 0, 0, 0));
        (await seeder.SeedAsync([Probe("1.1", "Sinov blankasi (yangi)")], ct))
            .ShouldBe(new BlankTemplateSeedResult(0, 0, 1, 0, 0));

        context.ChangeTracker.Clear();
        var versions = await VersionsAsync(context, ct);
        versions.Count.ShouldBe(2);
        versions.Single(row => row.Version == "1.0").Status.ShouldBe(BlankTemplateVersionStatus.Retired);
        versions.Single(row => row.Version == "1.1").Status.ShouldBe(BlankTemplateVersionStatus.Published);
        (await context.BlankTemplates.AsNoTracking().SingleAsync(row => row.Code == "HR-T99", ct))
            .Title.ShouldBe("Sinov blankasi (yangi)");

        // 4. E'lon qilingandan eski, bazada yo'q versiya → urug'lanmaydi
        (await seeder.SeedAsync([Probe("0.9", "Eski")], ct))
            .ShouldBe(new BlankTemplateSeedResult(0, 0, 0, 1, 0));
        (await VersionsAsync(context, ct)).Count.ShouldBe(2);

        // 5. Yaroqsiz ta'rif → hech narsa yozilmaydi
        (await seeder.SeedAsync([new BlankTemplateFile("HR-T98", Probe("1.0", "X").Json)], ct))
            .ShouldBe(new BlankTemplateSeedResult(0, 0, 0, 0, 1), "fayl nomi va code teng emas");
    }

    private static Task<List<BlankTemplateVersion>> VersionsAsync(AppDbContext context, CancellationToken ct)
        => context.BlankTemplateVersions.AsNoTracking()
            .Where(row => context.BlankTemplates.Any(template => template.Id == row.TemplateId && template.Code == "HR-T99"))
            .ToListAsync(ct);

    private static BlankTemplateFile Probe(string version, string title, bool indented = false)
    {
        var definition = new JsonObject
        {
            ["title"] = title,
            ["schema"] = 1,
            ["code"] = "HR-T99",
            ["version"] = version,
            ["subject"] = "none",
            ["sections"] = new JsonArray(new JsonObject
            {
                ["id"] = "main",
                ["title"] = "Asosiy",
                ["fields"] = new JsonArray(new JsonObject { ["id"] = "note", ["type"] = "text", ["label"] = "Izoh" }),
            }),
        };

        return new BlankTemplateFile(
            "HR-T99",
            definition.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = indented }));
    }
}
