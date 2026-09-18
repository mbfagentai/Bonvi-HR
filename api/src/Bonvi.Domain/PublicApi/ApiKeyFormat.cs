using System.Security.Cryptography;
using System.Text;

namespace Bonvi.Domain.PublicApi;

/// <summary>
/// Yangi yaratilgan kalit — ochiq matni va uning bazaga tushadigan qismlari.
/// </summary>
/// <remarks>
/// <see cref="PlainKey"/> — tizimdagi eng qisqa umrli qiymat: u faqat
/// yaratish javobida bir marta ko'rinadi va hech qayerda saqlanmaydi.
/// Bazaga faqat <see cref="Prefix"/> (ochiq) va <see cref="Hash"/> tushadi.
/// </remarks>
/// <param name="PlainKey">To'liq kalit: <c>bonvi_&lt;prefiks&gt;_&lt;sir&gt;</c>.</param>
/// <param name="Prefix">Qidiruv tutqichi — 8 belgi, bazada ochiq yotadi.</param>
/// <param name="Hash">To'liq kalitning SHA-256 xeshi (32 bayt).</param>
public sealed record ApiKeySecret(string PlainKey, string Prefix, byte[] Hash);

/// <summary>
/// Tashqi API kalitining formati: yaratish, o'qish va xeshlash.
/// </summary>
/// <remarks>
/// <para>
/// <b>Format:</b> <c>bonvi_&lt;prefiks8&gt;_&lt;sir32&gt;</c>. Uch qism uch xil
/// ish qiladi:
/// </para>
/// <list type="bullet">
///   <item><c>bonvi</c> — kalitni ko'rgan odam (yoki sir qidiruvchi skaner)
///         u qaysi tizimga tegishli ekanini darhol bilsin;</item>
///   <item><b>prefiks</b> — bazadagi QIDIRUV tutqichi. U ochiq saqlanadi va
///         ekranda hamda loglarda kalitning yagona ko'rinadigan qismi
///         bo'ladi;</item>
///   <item><b>sir</b> — kalitning o'zi. U hech qayerda saqlanmaydi.</item>
/// </list>
/// <para>
/// <b>Alifbo</b> chalkashadigan belgilarsiz: <c>0</c>/<c>O</c>,
/// <c>1</c>/<c>l</c>/<c>I</c> yo'q. Kalit qo'lda ko'chiriladi va telefon
/// orqali aytiladi — bir belgi noto'g'ri o'qilsa, natija «kalit noto'g'ri»
/// degan tushunarsiz 401 bo'lardi.
/// </para>
/// <para>
/// <b>Uzunlik.</b> 32 belgi × log2(31) ≈ 158 bit entropiya. Bu tanlab
/// topish (brute force) uchun yetarlicha katta, kalit esa hali ham bitta
/// qatorga sig'adi.
/// </para>
/// </remarks>
public static class ApiKeyFormat
{
    /// <summary>Kalit boshidagi belgi — u qaysi tizimniki ekanini aytadi.</summary>
    public const string Scheme = "bonvi";

    /// <summary>Prefiks uzunligi (bazada <c>key_prefix</c> ustuni).</summary>
    public const int PrefixLength = 8;

    /// <summary>Sir qismining uzunligi.</summary>
    public const int SecretLength = 32;

    /// <summary>SHA-256 natijasi — <c>key_hash</c> ustunining uzunligi.</summary>
    public const int HashLength = 32;

    /// <summary>
    /// Chalkashmaydigan alifbo: <c>0</c>, <c>1</c>, <c>i</c>, <c>l</c>,
    /// <c>o</c> ataylab yo'q.
    /// </summary>
    private const string Alphabet = "23456789abcdefghjkmnpqrstuvwxyz";

    /// <summary>
    /// Topilmagan prefiks uchun «soxta» xesh — vaqt bo'yicha tenglashtirish.
    /// </summary>
    /// <remarks>
    /// ⭐ Noma'lum prefiks bilan kelgan so'rov bazadan hech narsa topmaydi va
    /// taqqoslashsiz darhol qaytardi; mavjud prefiks bilan kelgani esa
    /// <see cref="CryptographicOperations.FixedTimeEquals"/> ni bajarardi.
    /// Farq kichik, lekin u hujumchiga «bu prefiks bazada BOR» degan
    /// javobni beradi — ya'ni qidiruv maydonini keskin toraytiradi.
    /// Shu sababli topilmagan holatda ham AYNAN shu qiymatga qarshi
    /// taqqoslash bajariladi.
    /// </remarks>
    private static readonly byte[] DecoyHash = new byte[HashLength];

    /// <summary>Kriptografik tasodifdan yangi kalit yasaydi.</summary>
    public static ApiKeySecret Generate()
    {
        var prefix = RandomNumberGenerator.GetString(Alphabet, PrefixLength);
        var secret = RandomNumberGenerator.GetString(Alphabet, SecretLength);

        var plain = $"{Scheme}_{prefix}_{secret}";

        return new ApiKeySecret(plain, prefix, ComputeHash(plain));
    }

    /// <summary>To'liq kalitning SHA-256 xeshi.</summary>
    /// <remarks>
    /// Xesh KALITNING BUTUNIDAN olinadi (<c>bonvi_</c> va ajratgichlar bilan
    /// birga), faqat sir qismidan emas: shundagina bazadagi xesh bilan mos
    /// keladigan yagona matn — foydalanuvchi ko'chirib qo'ygan kalitning
    /// o'zi bo'ladi.
    /// </remarks>
    public static byte[] ComputeHash(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return SHA256.HashData(Encoding.UTF8.GetBytes(key));
    }

    /// <summary>
    /// Sarlavhadan kelgan xom matnni prefiks va xeshga ajratadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shakli buzilgan matn bu yerda to'xtaydi va bazaga umuman bormaydi —
    /// ya'ni tasodifiy satr bilan yuborilgan so'rov hech qanday so'rov
    /// yuklamasi bermaydi.
    /// </para>
    /// <para>
    /// Chaqiruvchi <c>false</c> ni ham, keyinroq xesh mos kelmasligini ham
    /// BIR XIL xato bilan qaytarishi shart: mijoz «formatingiz noto'g'ri»
    /// va «siringiz noto'g'ri» ni ajrata olmasligi kerak.
    /// </para>
    /// </remarks>
    /// <param name="raw">Xom sarlavha qiymati.</param>
    /// <param name="prefix">Ajratilgan prefiks.</param>
    /// <param name="hash">To'liq kalitning xeshi.</param>
    /// <returns>Matn formatga mos kelsa — <c>true</c>.</returns>
    public static bool TryRead(string? raw, out string prefix, out byte[] hash)
    {
        prefix = string.Empty;
        hash = DecoyHash;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var value = raw.Trim();

        // Uzunlik AVVAL tekshiriladi: keyingi bo'laklashlar shundagina
        // ma'noli va juda uzun sarlavha ustida ortiqcha ish bajarilmaydi.
        if (value.Length != Scheme.Length + 1 + PrefixLength + 1 + SecretLength)
        {
            return false;
        }

        var parts = value.Split('_');

        if (parts.Length != 3
            || !string.Equals(parts[0], Scheme, StringComparison.Ordinal)
            || parts[1].Length != PrefixLength
            || parts[2].Length != SecretLength
            || !OnlyAlphabet(parts[1])
            || !OnlyAlphabet(parts[2]))
        {
            return false;
        }

        prefix = parts[1];
        hash = ComputeHash(value);
        return true;
    }

    /// <summary>
    /// Nomzod xeshini saqlangani bilan vaqt bo'yicha barqaror solishtiradi.
    /// </summary>
    /// <remarks>
    /// <paramref name="stored"/> <c>null</c> bo'lishi mumkin (prefiks
    /// topilmadi) — u holda soxta xeshga qarshi taqqoslanadi va natija
    /// baribir <c>false</c> bo'ladi, lekin AYNI vaqt sarflanadi
    /// (<see cref="DecoyHash"/> izohi).
    /// </remarks>
    public static bool Matches(byte[]? stored, byte[] candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return CryptographicOperations.FixedTimeEquals(stored ?? DecoyHash, candidate);
    }

    private static bool OnlyAlphabet(string value)
    {
        foreach (var symbol in value)
        {
            if (!Alphabet.Contains(symbol, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
