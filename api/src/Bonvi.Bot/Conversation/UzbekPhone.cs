namespace Bonvi.Bot.Conversation;

/// <summary>
/// O'zbekiston telefon raqami — server qabul qiladigan shaklda.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Qoida serverdagi <c>PhoneNumber.Create</c> ning NUSXASI</b>
/// (9 xona → «998» qo'shiladi; to'liq raqam «998» bilan boshlanadi va 12
/// xona; operator kodi 0 yoki 1 bilan boshlanmaydi). Bot domen
/// loyihasiga bog'lanmaydi (izohi <c>Bonvi.Bot.csproj</c> da), shu
/// sababli qoida shu yerda takrorlanadi — ikkalasi BIRGA o'zgaradi.
/// </para>
/// <para>
/// <b>Nega bot ham tekshiradi.</b> Prod jurnali (2026-09-29): Rossiya
/// raqamli Telegram hisobidan kelgan nomzod telefon qadamidan bemalol
/// o'tardi va 22 ta savoldan KEYIN «ariza yuborilmadi» ni olardi — server
/// faqat +998 ni qabul qiladi. Endi bu telefon qadamining o'zida aytiladi.
/// </para>
/// </remarks>
public static class UzbekPhone
{
    private const string CountryCode = "998";

    /// <summary>
    /// Raqamni <c>+998XXXXXXXXX</c> ko'rinishiga keltiradi. O'zbekiston
    /// raqami bo'lmasa yoki noto'g'ri bo'lsa — <c>null</c>.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var digits = new string([.. input.Where(char.IsAsciiDigit)]);

        if (digits.Length == 9)
        {
            digits = CountryCode + digits;
        }

        return digits.Length == 12
            && digits.StartsWith(CountryCode, StringComparison.Ordinal)
            && digits[3] is not ('0' or '1')
                ? "+" + digits
                : null;
    }
}
