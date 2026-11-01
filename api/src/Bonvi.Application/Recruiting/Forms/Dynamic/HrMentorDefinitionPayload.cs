using System.Text.Json.Nodes;
using Bonvi.Application.Blanks;
using Bonvi.Domain.Common;
using static Bonvi.Application.Recruiting.Forms.Dynamic.FormDefinitionChecker;

namespace Bonvi.Application.Recruiting.Forms.Dynamic;

/// <summary>HRMentor e'lon qilgan versiya (<c>GET …/definition</c> → <c>published</c>).</summary>
/// <param name="Version">HRMentor dagi versiya raqami.</param>
/// <param name="Label">HR bergan yorliq — ta'rifning <c>label</c> i bilan bir xil.</param>
/// <param name="Sha256">HRMentor hisoblagan kanonik JSON xeshi.</param>
/// <param name="Definition">Ta'rif — kelgan JSON, sonlar matni o'zgarmagan.</param>
public sealed record PublishedFormDefinition(int Version, string Label, string Sha256, JsonObject Definition);

/// <summary>Konstruktordagi qoralama (<c>GET …/draft</c>) — faqat ko'rinish uchun, hech qayerda saqlanmaydi.</summary>
/// <param name="Revision">HRMentor dagi qoralama reviziyasi.</param>
/// <param name="Definition">Tekshirilgan ta'rif.</param>
/// <param name="Json">Ta'rif JSON i (forma shundan chiziladi; yorlig'i yo'q bo'lsa — <see cref="HrMentorDefinitionPayload.DraftLabel"/>).</param>
public sealed record DraftFormDefinition(int Revision, FormDefinition Definition, JsonObject Json);

/// <summary>
/// HRMentor javoblarini o'qish (SPEC-anketa-konstruktori §5.2, §9). Shakl buzuq bo'lsa
/// <see cref="HrMentorErrors.BadResponse"/>, mazmun qabul qilinmasa
/// <see cref="HrMentorErrors.DefinitionInvalid"/>.
/// </summary>
public static class HrMentorDefinitionPayload
{
    /// <summary>
    /// <c>{ code, published: { version, label, published_at, sha256, definition } | null }</c>.
    /// <c>published: null</c> — e'lon yo'q yoki qaytarib olingan (o'rnatilgan anketa).
    /// </summary>
    public static Result<PublishedFormDefinition?> Published(string json, string formCode)
    {
        if (FormDefinitionReader.Parse(json) is not { } root
            || Str(root["code"]) != formCode
            || !root.ContainsKey("published"))
        {
            return HrMentorErrors.BadResponse;
        }

        if (root["published"] is null)
        {
            return Result.Success<PublishedFormDefinition?>(null);
        }

        if (root["published"] is not JsonObject published
            || Long(published["version"]) is not { } version
            || version is <= 0 or > int.MaxValue
            || Str(published["label"]) is not { } label
            || Str(published["sha256"]) is not { } sha256
            || published["definition"] is not JsonObject definition)
        {
            return HrMentorErrors.BadResponse;
        }

        // SHA-256 — kelgan ta'rifning o'zidan; yorliq ta'rifdagisi bilan bir xil bo'lishi shart.
        if (!string.Equals(CanonicalJson.Sha256(definition), sha256, StringComparison.Ordinal)
            || !string.Equals(Str(definition["label"]), label, StringComparison.Ordinal))
        {
            return HrMentorErrors.DefinitionInvalid.At("sha256");
        }

        return new PublishedFormDefinition((int)version, label, sha256, definition);
    }

    /// <summary>
    /// <c>{ code, draft_revision, updated_at, definition, issues }</c> — ko'rinish uchun qoralama.
    /// HRMentor muammo ko'rsatgan qoralama ham, BonviHR tekshiruvidan o'tmagani ham rad etiladi.
    /// </summary>
    public static Result<DraftFormDefinition> Draft(string json, string formCode)
    {
        if (FormDefinitionReader.Parse(json) is not { } root
            || Str(root["code"]) != formCode
            || Long(root["draft_revision"]) is not { } revision
            || revision is < 0 or > int.MaxValue
            || root["definition"] is not JsonObject definition
            || root["issues"] is not JsonArray issues)
        {
            return HrMentorErrors.BadResponse;
        }

        if (issues.Count > 0)
        {
            return HrMentorErrors.DefinitionInvalid.At("issues");
        }

        // Qoralamada yorliq hali berilmagan bo'lishi mumkin — ko'rinishda «qoralama».
        if (Str(definition["label"]) is not { } label || !LabelPattern().IsMatch(label))
        {
            definition["label"] = DraftLabel;
        }

        var read = FormDefinitionReader.Read(definition);
        return read.IsFailure ? read.Error : new DraftFormDefinition((int)revision, read.Value, definition);
    }

    /// <summary>Yorlig'i yo'q qoralamaning ko'rinishdagi yorlig'i.</summary>
    public const string DraftLabel = "draft";
}
