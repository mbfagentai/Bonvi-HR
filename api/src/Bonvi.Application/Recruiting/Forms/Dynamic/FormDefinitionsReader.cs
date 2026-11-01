using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Bonvi.Application.Blanks;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Recruiting.Forms.Dynamic;

/// <summary>Saqlangan, tekshirilgan anketa versiyasi.</summary>
/// <param name="Version">HRMentor dagi versiya raqami.</param>
/// <param name="Label">Yorliq («4.0») — anketaning <c>formVersion</c> i.</param>
/// <param name="ReceivedAt">Olingan lahza.</param>
/// <param name="Definition">O'qilgan ta'rif.</param>
/// <param name="Json">Ta'rifning o'zi (o'zgarmas) — sahifaga shu ketadi.</param>
public sealed record StoredFormDefinition(
    int Version,
    string Label,
    DateTimeOffset ReceivedAt,
    FormDefinition Definition,
    JsonElement Json);

/// <summary>
/// Konstruktor anketasining versiyalarini bazadan oladi (SPEC-anketa-konstruktori §5.3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Amaldagisi</b> (<see cref="CurrentAsync"/>) — <c>hrmentor.definitionVersion</c>, faqat
/// HRMentor ulanishi yoqiq bo'lsa: ikki sozlamadan birini tozalash = o'rnatilgan anketaga qaytish
/// (HR matnlari bilan bir qoida).
/// </para>
/// <para>
/// <b>Aniq versiya</b> (<see cref="FindAsync"/>) — ulanishdan qat'i nazar: oldin ochilgan sahifa
/// va topshirilgan anketaning PDF i o'z versiyasini ishlatadi.
/// </para>
/// <para>
/// O'qilgan ta'rif SHA-256 bo'yicha xotirada saqlanadi: versiya hech qachon o'zgarmaydi, xesh esa
/// mazmunning o'zi (boshqa bazadagi o'sha raqamli versiya boshqa kalit bo'ladi).
/// </para>
/// </remarks>
public sealed class FormDefinitionsReader
{
    private static readonly ConcurrentDictionary<string, StoredFormDefinition?> Parsed = new(StringComparer.Ordinal);

    private readonly IFormDefinitionRepository _definitions;
    private readonly SystemSettingsReader _settings;

    public FormDefinitionsReader(IFormDefinitionRepository definitions, SystemSettingsReader settings)
    {
        _definitions = definitions;
        _settings = settings;
    }

    /// <summary>Hozir amaldagi versiya; ulanish o'chiq, e'lon yo'q yoki qaytarib olingan — <c>null</c>.</summary>
    public async Task<StoredFormDefinition?> CurrentAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        var connection = HrMentorConnection.From(
            settings.Text(SystemSettingKeys.HrMentorBaseUrl),
            settings.Secret(SystemSettingKeys.HrMentorApiKey));

        if (connection is null
            || !int.TryParse(
                settings.Hidden(SystemSettingKeys.HrMentorDefinitionVersion),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var version))
        {
            return null;
        }

        return await FindAsync(version, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Aniq versiya; saqlanmagan bo'lsa <c>null</c>.</summary>
    public async Task<StoredFormDefinition?> FindAsync(int version, CancellationToken cancellationToken = default)
    {
        if (version <= 0)
        {
            return null;
        }

        var snapshot = await _definitions.FindAsync(CandidateForm.HrF02, version, cancellationToken).ConfigureAwait(false);

        return snapshot is null ? null : Parsed.GetOrAdd(snapshot.Sha256, _ => Parse(snapshot));
    }

    private static StoredFormDefinition? Parse(FormDefinitionSnapshot snapshot)
    {
        // Saqlashdan oldin tekshirilgan; bu yerda yiqilsa — baza qo'lda buzilgan: o'rnatilgan anketa.
        var definition = FormDefinitionReader.Stored(snapshot.Definition);
        if (definition is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(snapshot.Definition);

        return new StoredFormDefinition(
            snapshot.Version,
            snapshot.Label,
            snapshot.ReceivedAt,
            definition,
            document.RootElement.Clone());
    }
}
