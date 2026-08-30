using System.Globalization;
using Bonvi.Application.Settings.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Settings;

/// <summary>
/// Sozlamalarni bazadan o'qiydi va YETISHMAGANI o'rniga sukut qiymat beradi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Eng muhim xossasi: sozlamalar ekrani hech qachon ochilmagan
/// bo'lsa ham tizim to'liq ishlaydi.</b> Jadval bo'sh — bildirishnomalar
/// baribir kelaveradi, bot suhbat muddatini baribir biladi. Bunga
/// urug'lantirish (seed) bilan emas, O'QISH paytida erishiladi: yozuv
/// topilmasa <see cref="SystemSettingKeys"/> dagi sukut qiymat
/// ishlatiladi. Seed bilan qilinganda migratsiyadan keyin qo'shilgan
/// yangi sozlama eski bazalarda YO'Q bo'lib qolardi va o'sha yerda
/// tizim yiqilardi.
/// </para>
/// <para>
/// <b>Nega natija bir marta o'qib, obyekt bo'lib qaytariladi</b>
/// (<see cref="SystemSettings"/>), har bir kalit uchun alohida
/// <c>async</c> metod emas. Sozlamalar oltita va ular BIRGA kerak
/// bo'ladi: bildirishnoma yig'ilishi uchtasini ketma-ket so'raydi.
/// Har biri alohida so'rov bo'lganda bitta ekran ochilishi uchta
/// bir xil <c>SELECT</c> yuborardi. Bitta so'rov esa butun jadvalni
/// oladi — u kodda belgilangan hajmda va hech qachon o'smaydi.
/// </para>
/// <para>
/// <b>So'rov davomida eslab qolinadi.</b> Sinf <c>Scoped</c>, ya'ni
/// keshning umri bitta HTTP so'rovi. Bu «hammasini o'qildi» amali
/// uchun kerak: u bildirishnomalarni IKKI marta yig'adi (amaldan
/// oldin va keyin) va ikkalasida ham bir xil chegaralar bo'lishi
/// shart. Kesh uzoqroq yashasa (Singleton, Redis), bosh rahbar
/// o'zgartirgan qiymat eski jarayonlarda qolib ketardi.
/// </para>
/// </remarks>
public sealed class SystemSettingsReader
{
    private readonly ISystemSettingRepository _settings;
    private readonly ISecretProtector _protector;

    private SystemSettings? _cached;

    public SystemSettingsReader(ISystemSettingRepository settings, ISecretProtector protector)
    {
        _settings = settings;
        _protector = protector;
    }

    /// <summary>Barcha sozlamalarni o'qiydi (so'rov davomida bir marta).</summary>
    public async Task<SystemSettings> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var rows = await _settings.GetAllAsync(cancellationToken).ConfigureAwait(false);

        _cached = SystemSettings.From(rows, _protector);

        return _cached;
    }

    /// <summary>
    /// Keshni tashlaydi — yozishdan KEYIN yangi qiymatlar o'qilishi uchun.
    /// </summary>
    /// <remarks>
    /// Bitta so'rov ichida ham yozib, ham o'qish holati bor: sozlama
    /// saqlangach javobda YANGI ro'yxat qaytadi. Kesh tozalanmasa,
    /// foydalanuvchi «saqlandi» degan javobda o'zi endigina
    /// o'zgartirgan eski qiymatni ko'rardi.
    /// </remarks>
    public void Forget() => _cached = null;
}

/// <summary>
/// O'qilgan sozlamalar — bir lahzalik nusxa (snapshot).
/// </summary>
/// <remarks>
/// <para>
/// Bazaga murojaat qilmaydi: hamma qiymat allaqachon xotirada.
/// Shu sababli <see cref="Text"/> va <see cref="Number"/> — oddiy,
/// <c>async</c> BO'LMAGAN metodlar. Chaqiruv joyi shundan yutadi:
/// <c>settings.Number(SystemSettingKeys.ProbationWarningDays)</c> —
/// bu shunchaki qiymat, kutish nuqtasi emas.
/// </para>
/// <para>
/// <b>Noma'lum kalitda ISTISNO tashlanadi, <c>Result</c> emas.</b> Bu —
/// dasturchi xatosi, foydalanuvchi xatosi emas: kalitlar kodda
/// konstanta bo'lib turadi va ular <see cref="SystemSettingKeys"/> dan
/// keladi. Ya'ni bu holatga faqat kalit ro'yxatdan olib tashlanib,
/// uni ishlatgan joy qolib ketganda yetib boriladi — va u JIMGINA
/// sukut qiymat qaytarib emas, darhol ko'rinib turishi kerak.
/// </para>
/// </remarks>
public sealed class SystemSettings
{
    private readonly IReadOnlyDictionary<string, string> _stored;
    private readonly ISecretProtector _protector;

    private SystemSettings(IReadOnlyDictionary<string, string> stored, ISecretProtector protector)
    {
        _stored = stored;
        _protector = protector;
    }

    /// <summary>Bazadan kelgan qatorlardan nusxa yasaydi.</summary>
    /// <remarks>
    /// Noma'lum kalitli qatorlar (sozlama bekor qilingan, lekin qator
    /// qolgan) shu yerda TASHLANADI: ular hech qachon o'qilmaydi va
    /// jadvalda turgani hech narsani buzmaydi.
    /// </remarks>
    public static SystemSettings From(IEnumerable<SystemSetting> rows, ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(protector);

        var stored = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (SystemSettingKeys.IsKnown(row.Key))
            {
                stored[row.Key] = row.Value;
            }
        }

        return new SystemSettings(stored, protector);
    }

    /// <summary>Matnli sozlama. Yozilmagan bo'lsa — sukut qiymat.</summary>
    public string Text(string key) => Raw(Definition(key, SystemSettingKind.Text));

    /// <summary>
    /// Sirning OCHIQ qiymati. Qo'yilmagan yoki ochib bo'lmasa — <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega bo'sh satr emas, <c>null</c>.</b> Bu yagona metod sirni
    /// ochiq holda qaytaradi va uni chaqirgan joy «bor/yo'q» ni ANIQ
    /// ajratishi kerak: bot token bo'lmasa <c>.env</c> dagi zaxiraga
    /// tushadi, bo'sh satr bilan esa Telegram'ga bemaqsad murojaat
    /// qilardi.
    /// </para>
    /// <para>
    /// ⚠️ Ochib bo'lmagan qiymat ham <c>null</c> qaytaradi — «kalit
    /// almashgan» holati «qo'yilmagan» dan farq qilmaydi
    /// (<see cref="ISecretProtector.Unprotect"/> izohi). Bu ataylab:
    /// ikkalasining ham yagona yechimi bitta — qiymatni qayta qo'yish.
    /// </para>
    /// </remarks>
    public string? Secret(string key)
    {
        var definition = Definition(key, SystemSettingKind.Secret);

        if (!_stored.TryGetValue(definition.Key, out var protectedValue)
            || protectedValue.Length == 0)
        {
            return null;
        }

        var plain = _protector.Unprotect(protectedValue);

        return string.IsNullOrEmpty(plain) ? null : plain;
    }

    /// <summary>
    /// Sonli sozlama. Yozilmagan yoki BUZILGAN qiymatda — sukut qiymat.
    /// </summary>
    /// <remarks>
    /// <b>Nega buzilgan qiymat ham sukutga tushadi.</b> Yozishning
    /// hamma yo'li tekshiruvdan o'tadi, ya'ni bazada son bo'lmagan
    /// qiymat paydo bo'lishi mumkin emas — deyarli. Qolgan yo'l:
    /// bazaga qo'lda SQL bilan aralashish (migratsiya, tiklash,
    /// nosozlikni tuzatish). Bunda tizim yiqilmasligi kerak: eng
    /// yomon holatda u sukut chegara bilan ishlaydi va HR buni
    /// sozlamalar ekranida ko'radi.
    /// </remarks>
    public int Number(string key)
    {
        var definition = Definition(key, SystemSettingKind.Number);

        if (_stored.TryGetValue(definition.Key, out var value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stored))
        {
            return stored;
        }

        if (!int.TryParse(
                definition.DefaultValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var fallback))
        {
            // Bu yerga yetib borish — `SystemSettingKeys` dagi ta'rif
            // xatosi: sonli sozlamaga son bo'lmagan sukut qiymat
            // yozilgan. Uni jimgina nol bilan almashtirish eng yomon
            // yechim bo'lardi — chegara nolga tushib, bildirishnomalar
            // butunlay yo'qolardi.
            throw new InvalidOperationException(
                $"«{definition.Key}» sozlamasining sukut qiymati butun son emas.");
        }

        return fallback;
    }

    /// <summary>
    /// Barcha sozlamalar — ta'rifi va joriy qiymati bilan.
    /// </summary>
    /// <remarks>
    /// Tartib <see cref="SystemSettingKeys.All"/> dan keladi (ma'no
    /// bo'yicha guruhlangan), bazadagi tartibdan emas: bazada faqat
    /// O'ZGARTIRILGAN sozlamalar bor va ular ekrandagi ketma-ketlikni
    /// belgilay olmaydi.
    /// </remarks>
    public IReadOnlyList<SystemSettingDto> Describe() =>
    [
        .. SystemSettingKeys.All.Select(Describe),
    ];

    /// <summary>Bitta sozlamaning tashqi ko'rinishi.</summary>
    /// <remarks>
    /// Sir bu yerda ochiq qiymatdan AJRATILADI: <c>value</c> bo'sh
    /// qoladi va uning o'rniga «qo'yilganmi» hamda niqoblangan
    /// ko'rinish beriladi (sabab <see cref="SystemSettingDto"/> da).
    /// Shu sababli javobni yig'ayotgan hech bir joy «sirni qo'shishni
    /// unutib qo'yish» ham, «tasodifan qo'shib yuborish» ham qila
    /// olmaydi — qaror shu yagona metodda.
    /// </remarks>
    private SystemSettingDto Describe(SystemSettingDefinition definition)
    {
        if (definition.Kind != SystemSettingKind.Secret)
        {
            return new SystemSettingDto(
                definition.Key,
                Raw(definition),
                definition.Kind,
                definition.DefaultValue,
                definition.Min,
                definition.Max,
                definition.MaxLength,
                IsSet: _stored.ContainsKey(definition.Key),
                Preview: null);
        }

        var plain = Secret(definition.Key);

        return new SystemSettingDto(
            definition.Key,
            string.Empty,
            definition.Kind,
            definition.DefaultValue,
            definition.Min,
            definition.Max,
            definition.MaxLength,
            IsSet: plain is not null,
            Preview: plain is null ? null : Mask(plain));
    }

    /// <summary>
    /// Sirni niqoblaydi: <c>8123456789:…mQ4t</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Telegram tokenida ikki nuqtagacha bo'lgan qism — botning ochiq
    /// identifikatori, undan keyingisi — sir. Shu sababli birinchi
    /// bo'lak TO'LIQ, ikkinchisidan esa faqat oxirgi to'rt belgi
    /// qoladi.
    /// </para>
    /// <para>
    /// Ikki nuqtasi yo'q sirda (kelajakdagi boshqa kalit) faqat oxirgi
    /// to'rt belgi ko'rsatiladi. Sir juda qisqa bo'lsa — umuman hech
    /// narsa: to'rt belgi olti belgili sirning uchdan ikkisi bo'lardi.
    /// </para>
    /// </remarks>
    private static string Mask(string plain)
    {
        const int Visible = 4;

        var separator = plain.IndexOf(':', StringComparison.Ordinal);

        var head = separator > 0 ? plain[..separator] + ":" : string.Empty;
        var tail = plain[(separator + 1)..];

        return tail.Length >= Visible * 2
            ? head + "…" + tail[^Visible..]
            : head + "…";
    }

    /// <summary>Xom qiymat: yozilgani, bo'lmasa sukut qiymat.</summary>
    private string Raw(SystemSettingDefinition definition)
        => _stored.TryGetValue(definition.Key, out var value) ? value : definition.DefaultValue;

    /// <summary>Kalitning ta'rifini topadi va turini tekshiradi.</summary>
    private static SystemSettingDefinition Definition(string key, SystemSettingKind expected)
    {
        if (SystemSettingKeys.Find(key) is not { } definition)
        {
            throw new ArgumentException($"«{key}» — noma'lum sozlama kaliti.", nameof(key));
        }

        if (definition.Kind != expected)
        {
            // Matnli sozlamani son deb o'qish (yoki aksincha) — kodda
            // yo'l qo'yilgan chalkashlik. U jimgina sukut qiymat
            // qaytarsa, xato faqat ekranda g'alati son bo'lib ko'rinardi.
            throw new InvalidOperationException(
                $"«{key}» sozlamasi {definition.Kind} turida, {expected} deb o'qilyapti.");
        }

        return definition;
    }
}
