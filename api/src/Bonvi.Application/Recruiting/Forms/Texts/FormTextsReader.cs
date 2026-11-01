using Bonvi.Application.Blanks;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Recruiting.Forms.Texts;

/// <summary>
/// HR-F02 ning HR matnlarini bazadan oladi (SPEC-blankalar-hrmentor §5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Yangi anketa</b> (<see cref="CurrentAsync"/>) — eng yangi reviziya, lekin faqat HRMentor
/// ulanishi yoqiq bo'lsa: ikki sozlamadan birini tozalash = o'rnatilgan matnlarga qaytish (§9).
/// </para>
/// <para>
/// <b>Topshirilgan anketa</b> (<see cref="ForRevisionAsync"/>) — o'z reviziyasi, ulanishdan
/// qat'i nazar: imzolangan anketaning so'zlari keyin o'zgarmasligi kerak (§5.3).
/// </para>
/// </remarks>
public sealed class FormTextsReader
{
    private readonly IFormTextSnapshotRepository _snapshots;
    private readonly SystemSettingsReader _settings;

    public FormTextsReader(IFormTextSnapshotRepository snapshots, SystemSettingsReader settings)
    {
        _snapshots = snapshots;
        _settings = settings;
    }

    /// <summary>Hozir amaldagi matnlar; ulanish o'chiq yoki reviziya yo'q — <see cref="FormTexts.Empty"/>.</summary>
    public async Task<FormTexts> CurrentAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        var connection = HrMentorConnection.From(
            settings.Text(SystemSettingKeys.HrMentorBaseUrl),
            settings.Secret(SystemSettingKeys.HrMentorApiKey));

        if (connection is null)
        {
            return FormTexts.Empty;
        }

        var latest = await _snapshots.LatestAsync(CandidateForm.HrF02, cancellationToken).ConfigureAwait(false);

        return latest is null ? FormTexts.Empty : FormTexts.FromPayload(latest.Revision, latest.Payload);
    }

    /// <summary>
    /// Topshirilgan anketaning matnlari; <c>null</c> reviziya yoki topilmagani — o'rnatilgan matnlar.
    /// </summary>
    public async Task<FormTexts> ForRevisionAsync(int? revision, CancellationToken cancellationToken = default)
    {
        if (revision is not { } number)
        {
            return FormTexts.Empty;
        }

        var snapshot = await _snapshots.FindAsync(CandidateForm.HrF02, number, cancellationToken).ConfigureAwait(false);

        return snapshot is null ? FormTexts.Empty : FormTexts.FromPayload(snapshot.Revision, snapshot.Payload);
    }
}
