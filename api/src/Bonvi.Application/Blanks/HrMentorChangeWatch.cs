using Bonvi.Application.Settings;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Blanks;

/// <summary>HRMentor platforma reviziyasi — 64 ta kichik hex belgi (HRMentor SPEC §21.4 R1).</summary>
public static class HrMentorRevision
{
    /// <summary>Uzunligi.</summary>
    public const int Length = 64;

    /// <summary>Reviziya shaklidami: aynan 64 ta <c>0-9a-f</c>.</summary>
    /// <param name="value">Tekshiriladigan qiymat.</param>
    public static bool IsValid(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Length == Length && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    }
}

/// <summary>Kutishning bitta qadami nima bilan tugadi.</summary>
public enum HrMentorWatchOutcome
{
    /// <summary>Kutish vaqti tugadi, o'zgarish yo'q.</summary>
    Unchanged = 1,

    /// <summary>Reviziya yangi edi — sinxronlash xatosiz o'tdi.</summary>
    Synced = 2,

    /// <summary>Manzil yoki kalit qo'yilmagan.</summary>
    NotConnected = 3,

    /// <summary>Kalit hali platformaga bog'lanmagan yoki HRMentor kutish yo'lini bilmaydi.</summary>
    NotReady = 4,

    /// <summary>HRMentor da platforma o'chirilgan.</summary>
    Off = 5,

    /// <summary>Reviziya yangi edi, lekin sinxronlash xato bilan tugadi yoki band edi.</summary>
    SyncFailed = 6,

    /// <summary>Kutish so'rovining o'zi yiqildi (tarmoq, vaqt, server).</summary>
    Failed = 7,

    /// <summary>
    /// Reviziya yangi edi, sinxronlash o'tdi, lekin bir qadam doimiy xato berdi
    /// (<see cref="HrMentorErrors.IsLasting"/>): reviziya olinadi — keyingi o'zgarish ham soniyalarda
    /// keladi; o'sha qadamni besh daqiqalik yurish qayta tekshiradi.
    /// </summary>
    SyncedWithError = 8,
}

/// <summary>Bitta qadamning natijasi.</summary>
/// <param name="Outcome">Nima bilan tugadi.</param>
/// <param name="Since">Keyingi kutishga beriladigan reviziya; <c>null</c> — birinchi javob bilan sinxronlash.</param>
/// <param name="ErrorCode">Xato kodi; bo'lmasa <c>null</c>.</param>
public sealed record HrMentorWatchStep(HrMentorWatchOutcome Outcome, string? Since, string? ErrorCode = null);

/// <summary>
/// HRMentor dagi o'zgarishni kutishning bitta qadami (SPEC-hujjat-manbai §7, HRMentor SPEC §21.4 R4):
/// <c>GET /platform/changes</c> ni bizdagi oxirgi reviziya bilan kutadi; javobdagi reviziya boshqacha
/// bo'lsa — darhol to'liq sinxronlash (<see cref="HrMentorSync"/>, besh daqiqalik yurish bilan bir xil
/// kod). Halqani <c>HrMentorChangeWatcher</c> (Api) aylantiradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hech narsa o'tkazib yuborilmaydi.</b> Reviziya holatning izi, hodisa emas: HRMentor yoki biz
/// qayta ishga tushsak, birinchi javob baribir yangi holatni aytadi. Sinxronlash o'tkinchi xato bilan
/// yiqilsa reviziya YANGILANMAYDI — keyingi qadam yana sinab ko'radi (oraliq <see cref="HrMentorWatchDelay"/>
/// da). Xato faqat doimiy bo'lsa (HR saqlagan narsani qabul qila olmadik) reviziya olinadi: xuddi shu
/// holatni qayta sinash foyda bermaydi, kutish esa HR ning keyingi o'zgarishini besh daqiqagacha
/// kechiktirardi.
/// </para>
/// <para>
/// <b>Reviziya HRMentor da faqat HR ishidan o'zgaradi</b> — bizning katalog, profil va hisobotimiz uni
/// o'zgartirmaydi (HRMentor SPEC §21.4 R1 maydonlari), shuning uchun sinxronlash o'zini o'zi
/// qayta chaqirmaydi.
/// </para>
/// </remarks>
public sealed class HrMentorChangeWatch
{
    /// <summary>Bitta kutish so'rovi qancha kutadi.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(25);

    /// <summary>Boshqa yurish (besh daqiqalik yoki «Hozir yangilash») tugashini shuncha kutadi — hech qachon o'tkazib yubormaydi.</summary>
    public static readonly TimeSpan SyncWait = TimeSpan.FromMinutes(2);

    private readonly SystemSettingsReader _settings;
    private readonly IHrMentorClient _client;
    private readonly HrMentorSync _sync;

    public HrMentorChangeWatch(SystemSettingsReader settings, IHrMentorClient client, HrMentorSync sync)
    {
        _settings = settings;
        _client = client;
        _sync = sync;
    }

    /// <summary>Bitta qadam: kutish, kerak bo'lsa sinxronlash.</summary>
    /// <param name="since">Bizdagi oxirgi reviziya; <c>null</c> — birinchi javobning o'zi sinxronlashga sabab.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<HrMentorWatchStep> RunOnceAsync(string? since, CancellationToken cancellationToken)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        var connection = HrMentorConnection.From(
            settings.Text(SystemSettingKeys.HrMentorBaseUrl),
            settings.Secret(SystemSettingKeys.HrMentorApiKey));

        if (connection is null)
        {
            // Qayta ulanganda boshqa platforma bo'lishi mumkin — eski reviziya tashlanadi.
            return new HrMentorWatchStep(HrMentorWatchOutcome.NotConnected, null);
        }

        var answer = await _client.WaitForChangesAsync(connection, since, Wait, cancellationToken).ConfigureAwait(false);
        if (answer.IsFailure)
        {
            var outcome = answer.Error.Code == HrMentorErrors.PlatformNotReady.Code ? HrMentorWatchOutcome.NotReady
                : answer.Error.Code == HrMentorErrors.PlatformOff.Code ? HrMentorWatchOutcome.Off
                : HrMentorWatchOutcome.Failed;
            return new HrMentorWatchStep(outcome, since, answer.Error.Code);
        }

        var revision = answer.Value;
        if (since is not null && string.Equals(revision, since, StringComparison.Ordinal))
        {
            return new HrMentorWatchStep(HrMentorWatchOutcome.Unchanged, since);
        }

        var run = await _sync.RunAsync(SyncWait, cancellationToken).ConfigureAwait(false);
        if (run.IsFailure)
        {
            return new HrMentorWatchStep(HrMentorWatchOutcome.SyncFailed, since, run.Error.Code);
        }

        if (run.Value.ErrorCode is not { } code)
        {
            return new HrMentorWatchStep(HrMentorWatchOutcome.Synced, revision);
        }

        return run.Value.ErrorsLasting
            ? new HrMentorWatchStep(HrMentorWatchOutcome.SyncedWithError, revision, code)
            : new HrMentorWatchStep(HrMentorWatchOutcome.SyncFailed, since, code);
    }
}

/// <summary>Qadamdan keyin keyingisigacha qancha kutiladi (HRMentor SPEC §21.4 R4).</summary>
public static class HrMentorWatchDelay
{
    /// <summary>Birinchi xatodan keyin.</summary>
    public static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(5);

    /// <summary>Ketma-ket xatolarda eng uzoq oraliq — besh daqiqalik yurish bilan teng.</summary>
    public static readonly TimeSpan MaxRetry = TimeSpan.FromMinutes(5);

    /// <summary>Ulanmagan: sozlama qo'yilishi bilan tez boshlash uchun.</summary>
    public static readonly TimeSpan NotConnected = TimeSpan.FromSeconds(30);

    /// <summary>Kalit bog'lanmagan yoki HRMentor eski: besh daqiqalik yurish ishlayveradi.</summary>
    public static readonly TimeSpan NotReady = TimeSpan.FromMinutes(5);

    /// <summary>Platforma o'chirilgan.</summary>
    public static readonly TimeSpan Off = TimeSpan.FromMinutes(1);

    /// <summary>
    /// O'zgarishsiz javobdan keyin: odatda u 25 soniya kutib keladi; darhol qaytaveradigan
    /// noto'g'ri server halqani qizdirib yubormasin.
    /// </summary>
    public static readonly TimeSpan AfterUnchanged = TimeSpan.FromSeconds(1);

    /// <summary>Keyingi qadamgacha.</summary>
    /// <param name="outcome">Oxirgi qadam natijasi.</param>
    /// <param name="inARow">
    /// Shu turdagi natija ketma-ket necha marta (shu qadam bilan; <see cref="HrMentorWatchOutcome.SyncFailed"/>
    /// va <see cref="HrMentorWatchOutcome.Failed"/> — bitta tur).
    /// </param>
    /// <remarks>
    /// Sinxronlashdan keyin (doimiy xato qolgan bo'lsa ham) birinchi marta darhol; orasida «o'zgarmadi»
    /// bo'lmay ketma-ket kelsa —
    /// xatodagidek 5 s dan ikki barobar: har safar yangi reviziya qaytaradigan nosoz server
    /// to'xtovsiz sinxronlashga majbur qilolmaydi. HR ketma-ket bir necha marta saqlasa ham
    /// kechikish soniyalarda qoladi.
    /// </remarks>
    public static TimeSpan After(HrMentorWatchOutcome outcome, int inARow) => outcome switch
    {
        HrMentorWatchOutcome.Synced or HrMentorWatchOutcome.SyncedWithError => inARow <= 1 ? TimeSpan.Zero : Retry(inARow - 1),
        HrMentorWatchOutcome.Unchanged => AfterUnchanged,
        HrMentorWatchOutcome.NotConnected => NotConnected,
        HrMentorWatchOutcome.NotReady => NotReady,
        HrMentorWatchOutcome.Off => Off,
        _ => Retry(inARow),
    };

    /// <summary>5 s, 10 s, 20 s … ikki barobardan, eng ko'pi <see cref="MaxRetry"/>.</summary>
    /// <param name="failuresInARow">Ketma-ket xatolar soni.</param>
    public static TimeSpan Retry(int failuresInARow)
    {
        var doublings = Math.Clamp(failuresInARow, 1, 16) - 1;
        var delay = FirstRetry * Math.Pow(2, doublings);
        return delay < MaxRetry ? delay : MaxRetry;
    }
}
