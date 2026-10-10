using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Bonvi.IsupGateway;

/// <summary>ISUP shlyuzining sozlamalari (<c>Isup__*</c> muhit o'zgaruvchilari).</summary>
internal sealed class IsupOptions
{
    public const string SectionName = "Isup";

    /// <summary>Hikvision HCISUPSDK fayllari turgan jild (<c>libHCISUPCMS.so</c> va hamrohlari).</summary>
    [Required(AllowEmptyStrings = false)]
    public string NativeDirectory { get; set; } = "/app/native";

    /// <summary>CMS tinglash manzili. <c>0.0.0.0</c> — konteynerning hamma interfeysi.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ListenAddress { get; set; } = "0.0.0.0";

    /// <summary>CMS porti — terminalning «Server port» maydoni shu.</summary>
    [Range(1, 65535)]
    public int ListenPort { get; set; } = 7660;

    /// <summary>
    /// Terminalga beriladigan yurak urishi oralig'i (soniya). <c>0</c> — SDK sukuti.
    /// </summary>
    [Range(0, 600)]
    public int KeepAliveSeconds { get; set; }

    /// <summary>Necha yurak urishi o'tkazib yuborilsa terminal «uzilgan» (<c>0</c> — SDK sukuti).</summary>
    [Range(0, 20)]
    public int KeepAliveTimeoutCount { get; set; }

    /// <summary>
    /// Terminal qayta yo'naltirish (DAS) so'raganda beriladigan ochiq IP. Bo'sh — javob berilmaydi.
    /// </summary>
    public string? PublicAddress { get; set; }

    /// <summary>DAS javobidagi domen (masalan <c>hr.bonvi.uz</c>). Ixtiyoriy.</summary>
    public string? PublicDomain { get; set; }

    /// <summary>Bonvi API — ichki tarmoq manzili.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ApiBaseUrl { get; set; } = "http://api:8080";

    /// <summary>
    /// Yuz rasmlari beriladigan OCHIQ manzil, <c>/</c> bilan tugaydi
    /// (masalan <c>https://hr.bonvi.uz/isup-face/</c>). Terminal rasmni shu
    /// yerdan o'zi yuklab oladi — ISUP orqali faqat matn o'tadi.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string FaceUrlBase { get; set; } = string.Empty;

    /// <summary>Bir martalik rasm havolasining umri (soniya).</summary>
    [Range(30, 3600)]
    public int FaceUrlTtlSeconds { get; set; } = 300;

    /// <summary>Bitta ISAPI tunnel so'rovining javob kutish muddati (ms).</summary>
    [Range(1000, 120_000)]
    public int PassthroughTimeoutMs { get; set; } = 20_000;

    /// <summary>Agentlarning holat fayllari (hodisa kursori) — TOMga ulanadi.</summary>
    [Required(AllowEmptyStrings = false)]
    public string StateDirectory { get; set; } = "/data/agents";

    /// <summary>SDK o'z jurnalini yozadimi: 0 — yo'q, 1 — xatolar, 2 — +ogohlantirish, 3 — hammasi.</summary>
    [Range(0, 3)]
    public int SdkLogLevel { get; set; } = 1;

    /// <summary>SDK jurnali jildi.</summary>
    [Required(AllowEmptyStrings = false)]
    public string SdkLogDirectory { get; set; } = "/data/sdk-logs";

    /// <summary>Agentning hodisa halqasi (soniya) — LAN agentidagi bilan bir xil ma'no.</summary>
    [Range(1, 120)]
    public int EventsIntervalSeconds { get; set; } = 2;

    /// <summary>Agentning ro'yxat halqasi (soniya).</summary>
    [Range(10, 3600)]
    public int RosterIntervalSeconds { get; set; } = 60;

    /// <summary>Holat fayli yo'q bo'lganda terminal jurnalidan necha soat orqaga o'qiladi.</summary>
    [Range(1, 24 * 30)]
    public int ColdStartHours { get; set; } = 48;

    /// <summary>Ulanishi mumkin bo'lgan terminallar. Ro'yxatda yo'q qurilma RAD etiladi.</summary>
    public IList<IsupDeviceOptions> Devices { get; init; } = [];
}

/// <summary>Bitta terminal: ISUP identifikatori, kaliti va Bonvi qurilma tokeni.</summary>
internal sealed class IsupDeviceOptions
{
    /// <summary>Terminalning «Device ID» maydoni (1–32 belgi: harf, raqam, <c>-</c>, <c>_</c>).</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// ISUP 5.0 kaliti — terminalning «Encryption key» maydoni (8–16 belgi).
    /// ⚠️ SIR: jurnalga va git'ga tushmaydi.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Bonvi qurilma kartasidagi token. ⚠️ SIR.</summary>
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Atributlar bilan ifodalab bo'lmaydigan tekshiruvlar: ro'yxat, takror va
/// terminalning o'z chegaralari (<c>/ISAPI/System/Network/Ehome/capabilities</c>).
/// </summary>
internal sealed partial class IsupOptionsValidator : IValidateOptions<IsupOptions>
{
    public ValidateOptionsResult Validate(string? name, IsupOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (!Uri.TryCreate(options.FaceUrlBase, UriKind.Absolute, out var faceBase)
            || faceBase.Scheme is not ("http" or "https")
            || !options.FaceUrlBase.EndsWith('/'))
        {
            failures.Add("Isup:FaceUrlBase — http(s) manzil va '/' bilan tugashi kerak (masalan https://hr.bonvi.uz/isup-face/).");
        }

        if (!Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out _))
        {
            failures.Add("Isup:ApiBaseUrl — to'liq manzil bo'lishi kerak (masalan http://api:8080).");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < options.Devices.Count; i++)
        {
            var device = options.Devices[i];
            var at = $"Isup:Devices:{i}";

            if (!DeviceIdPattern().IsMatch(device.DeviceId ?? string.Empty))
            {
                failures.Add($"{at}:DeviceId — 1–32 belgi: harf, raqam, '-' yoki '_'.");
            }
            else if (!seen.Add(device.DeviceId!))
            {
                failures.Add($"{at}:DeviceId — '{device.DeviceId}' takrorlangan.");
            }

            // Kalit qiymati xabarda KO'RSATILMAYDI — faqat qoida.
            if (!KeyPattern().IsMatch(device.Key ?? string.Empty))
            {
                failures.Add($"{at}:Key — 8–16 belgi, faqat lotin harfi, raqam va belgilar (bo'shliqsiz).");
            }

            if (string.IsNullOrWhiteSpace(device.Token))
            {
                failures.Add($"{at}:Token — qurilma kartasidagi token berilmagan.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,32}$")]
    private static partial Regex DeviceIdPattern();

    [GeneratedRegex("^[\\x21-\\x7E]{8,16}$")]
    private static partial Regex KeyPattern();
}
