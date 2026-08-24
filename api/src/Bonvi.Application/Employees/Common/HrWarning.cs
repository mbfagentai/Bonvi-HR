using System.Globalization;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Employees.Common;

/// <summary>
/// Ogohlantirish — amal bajarildi, lekin HR bilishi kerak bo'lgan holat bor.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega xato emas.</b> «Stavkalar yig'indisi 1.5 dan oshdi» yoki «xodim
/// nazoratda» — bular ish jarayonini to'xtatmaydi. Excel davrida HR bunday
/// holatlarni umuman ko'rmasdi; endi ko'radi, lekin ruxsat so'ralmaydi.
/// Blok qiladigan holatlar (qora ro'yxat) esa <see cref="Error"/> bo'lib qaytadi.
/// </para>
/// <para>
/// Kodi xato kodlari bilan bir xil lug'atdan olinadi — interfeys ikkalasini
/// bir xil tarjima qiladi.
/// </para>
/// </remarks>
/// <param name="Code">Mashina o'qiydigan kod, masalan <c>mehnat.stavka_oshdi</c>.</param>
/// <param name="Message">Foydalanuvchiga ko'rsatiladigan matn.</param>
public sealed record HrWarning(string Code, string Message)
{
    /// <summary>Xatodan ogohlantirish yasaydi — matn takrorlanmasligi uchun.</summary>
    public static HrWarning From(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new HrWarning(error.Code, error.Message);
    }

    /// <summary>Umumiy stavka chegaradan oshgani haqida.</summary>
    public static HrWarning RateExceeded(decimal total) => new(
        "mehnat.stavka_oshdi",
        "Faol stavkalar yig'indisi " + total.ToString("0.##", CultureInfo.InvariantCulture)
        + " ga yetdi — 1.5 chegarasidan oshdi.");
}
