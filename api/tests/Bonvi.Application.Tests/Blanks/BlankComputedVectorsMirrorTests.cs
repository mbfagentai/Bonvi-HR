namespace Bonvi.Application.Tests.Blanks;

/// <summary>
/// The web evaluator runs the same vectors from its own copy
/// (<c>web/src/features/blanks/lib/__fixtures__/computed-vectors.json</c>) because the web
/// container mounts only <c>web/</c>. This test keeps the two files byte-identical, so the
/// C# reference and the TS twin can never be tested against different cases (SPEC-blankalar §4.5).
/// </summary>
public sealed class BlankComputedVectorsMirrorTests
{
    private const string WebCopy = "src/features/blanks/lib/__fixtures__/computed-vectors.json";

    [Fact]
    public void Web_nusxasi_api_vektorlari_bilan_aynan_bir_xil()
    {
        var reference = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Blanks", "computed-vectors.json"));
        var webDir = FindWebDir();

        webDir.ShouldNotBeNull(
            "web/ not found: run from the repo, or set BONVI_WEB_DIR (./bonvi test mounts web/ read-only at /web).");

        var mirror = File.ReadAllBytes(Path.Combine(webDir, WebCopy));

        mirror.ShouldBe(reference,
            $"web/{WebCopy} drifted from api/tests/Bonvi.Application.Tests/Blanks/computed-vectors.json — copy the api file over it.");
    }

    private static string? FindWebDir()
    {
        var fromEnv = Environment.GetEnvironmentVariable("BONVI_WEB_DIR");

        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(Path.Combine(fromEnv, WebCopy)))
        {
            return fromEnv;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "web");

            if (File.Exists(Path.Combine(candidate, WebCopy)))
            {
                return candidate;
            }
        }

        return null;
    }
}
