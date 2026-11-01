using System.Text;

namespace Bonvi.Application.Recruiting.Forms.Texts;

/// <summary>
/// O'zbek lotin matnidagi tutuq belgilarini qog'ozdagidek qiladi: <c>o'</c> → <c>o‘</c>,
/// <c>g'</c> → <c>g‘</c>, qolgan harfdan keyingi tutuq → <c>’</c> (<c>ma’lumot</c>).
/// </summary>
/// <remarks>
/// HR HRMentor da oddiy klaviaturadan yozadi (<c>'</c>, <c>`</c>, <c>ʻ</c>, <c>ʼ</c>), anketa va
/// PDF esa hamma joyda <c>‘</c> va <c>’</c> bilan (tarjimalar va blanka shunday). Faqat HARFDAN
/// keyingi belgi o'zgaradi — «'Ha'» kabi qo'shtirnoq o'rnidagi tutuq va o'rin belgilari
/// (<c>{{nom}}</c>) tegilmaydi.
/// </remarks>
public static class UzbekTypography
{
    public static string Apply(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        StringBuilder? result = null;
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (current is not ('\'' or '`' or 'ʻ' or 'ʼ' or '‘' or '’') || index == 0 || !char.IsLetter(text[index - 1]))
            {
                result?.Append(current);
                continue;
            }

            var mark = text[index - 1] is 'o' or 'O' or 'g' or 'G' ? '‘' : '’';
            if (mark == current)
            {
                result?.Append(current);
                continue;
            }

            result ??= new StringBuilder(text, 0, index, text.Length);
            result.Append(mark);
        }

        return result?.ToString() ?? text;
    }
}
