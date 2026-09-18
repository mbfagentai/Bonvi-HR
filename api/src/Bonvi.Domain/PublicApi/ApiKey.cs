using Bonvi.Domain.Common;

namespace Bonvi.Domain.PublicApi;

/// <summary>
/// Tashqi API kaliti — holdingning boshqa loyihasiga berilgan O'QISH huquqi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Kalitning o'zi bu yerda YO'Q va hech qachon bo'lmaydi.</b> Saqlanadigan
/// narsa — prefiks (ochiq qidiruv tutqichi) va to'liq kalitning SHA-256
/// xeshi. Kalit sizib ketsa yoki yo'qolsa, uni qaytarib olib bo'lmaydi:
/// yagona yo'l — eskisini bekor qilib, yangisini yaratish. Bu ataylab,
/// sabab <see cref="ApiKeyFormat"/> izohida.
/// </para>
/// <para>
/// <b>Yozuv hech qachon O'CHIRILMAYDI</b> (<c>ISoftDeletable</c> ham emas).
/// Bekor qilingan kalit jadvalda qoladi: «bu kalit kimga berilgan edi va
/// qachon o'chirilgan?» degan savol xavfsizlik tekshiruvida birinchi
/// so'raladigan savol va unga javob yo'qolmasligi kerak.
/// </para>
/// <para>
/// <b>Doira yo'q (scope).</b> Kalit tizimdagi butun ommaviy yuzaga —
/// ya'ni <c>/api/public/v1</c> ostidagi barcha <c>GET</c> larga — teng
/// huquq beradi. Har bir iste'molchiga alohida maydon to'plami berish
/// v1 doirasidan tashqarida qoldirilgan: bugungi iste'molchilar soni
/// bir nechta va ularning hammasiga bir xil ma'lumot kerak.
/// </para>
/// </remarks>
public sealed class ApiKey : AggregateRoot, IAuditable
{
    /// <summary>Nom uzunligining chegarasi.</summary>
    public const int MaxNameLength = 120;

    /// <summary>Oxirgi ishlatilgan IP uzunligining chegarasi (IPv6 + zona).</summary>
    public const int MaxIpLength = 64;

    private ApiKey(Guid id, string name, string keyPrefix, byte[] keyHash)
        : base(id)
    {
        Name = name;
        KeyPrefix = keyPrefix;
        KeyHash = keyHash;
    }

    /// <summary>EF Core uchun.</summary>
    private ApiKey()
    {
        Name = null!;
        KeyPrefix = null!;
        KeyHash = null!;
    }

    /// <summary>Kalit qaysi loyihaga berilgan («BonviCall»).</summary>
    public string Name { get; private set; }

    /// <summary>Kalitning OCHIQ qismi — qidiruv tutqichi va loglardagi yagona ko'rinishi.</summary>
    public string KeyPrefix { get; private set; }

    /// <summary>To'liq kalitning SHA-256 xeshi.</summary>
    public byte[] KeyHash { get; private set; }

    /// <summary>Kalit oxirgi marta qachon ishlatilgan.</summary>
    /// <remarks>
    /// Aniqligi ATAYLAB past: yozuv daqiqada bir martadan ko'p yangilanmaydi
    /// (sababi <see cref="MarkUsed"/> da). «Bu kalit hali tirikmi?» degan
    /// savolga daqiqalik aniqlik ortig'i bilan yetadi.
    /// </remarks>
    public DateTimeOffset? LastUsedAt { get; private set; }

    /// <summary>So'rov kelgan oxirgi IP — kalit sizganda qayerdan ishlatilayotganini ko'rsatadi.</summary>
    public string? LastUsedIp { get; private set; }

    /// <summary>Bekor qilingan vaqt. <c>null</c> — kalit kuchda.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Kim bekor qildi.</summary>
    public Guid? RevokedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>Kalit bekor qilinganmi.</summary>
    public bool IsRevoked => RevokedAt is not null;

    /// <summary>
    /// Yangi kalit yaratadi.
    /// </summary>
    /// <remarks>
    /// Nom takrorlanmasligi BU YERDA tekshirilmaydi: buning uchun boshqa
    /// yozuvlarni bilish kerak, agregat esa faqat o'zini biladi. Tekshiruv
    /// use-case qatlamida va bazadagi qisman unikal indeksda
    /// (<c>ix_api_keys_name_active</c>) — ikkalasi birga.
    /// </remarks>
    public static Result<ApiKey> Issue(string? name, ApiKeySecret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        var trimmed = name?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return Error.Validation("tashqi.kalit_nomi_bosh", "Kalit nomi kiritilishi shart.");
        }

        return trimmed.Length > MaxNameLength
            ? Error.Validation(
                "tashqi.kalit_nomi_uzun",
                $"Kalit nomi {MaxNameLength} belgidan oshmasligi kerak.")
            : new ApiKey(NewId(), trimmed, secret.Prefix, secret.Hash);
    }

    /// <summary>
    /// Kalitni bekor qiladi.
    /// </summary>
    /// <remarks>
    /// IDEMPOTENT: allaqachon bekor qilingan kalitda hech narsa o'zgarmaydi
    /// va XATO ham qaytmaydi. So'rovning maqsadi — kalit ishlamasligi, u esa
    /// bajarilgan (xodim rasmini o'chirishdagi bilan bir xil qoida).
    /// Birinchi bekor qilishning vaqti SAQLANADI: aynan u audit uchun
    /// ma'noli sana.
    /// </remarks>
    public void Revoke(DateTimeOffset at, Guid? by)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAt = at;
        RevokedBy = by;
    }

    /// <summary>
    /// Berilgan xesh shu kalitnikimi.
    /// </summary>
    /// <remarks>
    /// Taqqoslash vaqt bo'yicha barqaror
    /// (<see cref="ApiKeyFormat.Matches"/>): oddiy <c>SequenceEqual</c>
    /// birinchi farqda to'xtaydi va javob vaqti bo'yicha xeshni bayt-bayt
    /// tiklash imkonini berardi.
    /// </remarks>
    public bool Matches(byte[] candidateHash) => ApiKeyFormat.Matches(KeyHash, candidateHash);

    /// <summary>
    /// «Oxirgi ishlatilgan» belgisini yangilaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Bu metod HAR SO'ROVDA chaqirilmaydi.</b> Chaqiruvchi (ya'ni
    /// <c>IApiKeyUsageTracker</c>) uni daqiqada bir martadan ko'p
    /// bajarmaydi va so'rov yo'lidan TASHQARIDA bajaradi. Sabab oddiy:
    /// aks holda har bir O'QISH so'rovi bitta YOZISHga aylanardi — 120
    /// so'rov/daqiqa chegarasida bu daqiqasiga 120 ta <c>UPDATE</c> va
    /// har birida jadval qatori qulflanishi demak. Ya'ni faqat
    /// ma'lumot berish uchun qo'yilgan ustun butun ommaviy API ning
    /// tor joyiga aylanardi.
    /// </para>
    /// <para>
    /// Shu sababli aniqlik ATAYLAB daqiqalik va yozuvning muvaffaqiyatsiz
    /// bo'lishi o'qishni YIQITMAYDI: «kalit oxirgi qachon ishlatilgan»
    /// — qulaylik, ma'lumotning o'zi emas.
    /// </para>
    /// </remarks>
    public void MarkUsed(DateTimeOffset at, string? ip)
    {
        LastUsedAt = at;

        var trimmed = ip?.Trim();

        LastUsedIp = string.IsNullOrWhiteSpace(trimmed)
            ? null
            : trimmed[..Math.Min(trimmed.Length, MaxIpLength)];
    }
}
