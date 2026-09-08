namespace Bonvi.Domain.Recruiting;

/// <summary>
/// Nomzod bilan ishlash qoidalarining SUKUT qiymatlari.
/// </summary>
/// <remarks>
/// <para>
/// <c>NotificationRules</c> bilan bir xil naqsh va bir xil ogohlantirish:
/// bu qiymatlar to'g'ridan-to'g'ri ISHLATILMAYDI. Ular tizim
/// sozlamalarining (<c>SystemSettingKeys</c>) sukut qiymati bo'lib
/// turadi, ya'ni sozlamalar ekrani hech qachon ochilmagan tizimda aynan
/// shu sonlar ishlaydi.
/// </para>
/// <para>
/// Konstanta baribir shu yerda qoladi: sonning KELIB CHIQISHI qoida
/// yashaydigan joyda izohlanishi kerak, sozlamalar ro'yxatida emas —
/// u yerda o'nlab kalit yonma-yon turadi va izoh ko'zdan yo'qolardi.
/// </para>
/// </remarks>
public static class RecruitingRules
{
    /// <summary>
    /// Yopilgan arizadan keyin necha oy o'tgach bot yangi anketani
    /// qabul qiladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega 6 oy.</b> Ikkita chekka nuqta orasidagi tanlov. Bir-ikki
    /// oy — bu muddat emas: ekranda ko'rilgan holat (ikki KUN ichida
    /// ikkita anketa) baribir takrorlanardi, faqat sekinroq. Bir necha
    /// yil esa amalda mutlaq taqiq va u boshqa nomga loyiq.
    /// </para>
    /// <para>
    /// Yarim yil — vakansiya almashadigan, odamning tajribasi
    /// o'zgaradigan va HR ning qarori eskiradigan muddat. Aynan
    /// shundan keyin qayta urinish yangi ma'lumot olib keladi.
    /// </para>
    /// </remarks>
    public const int ReapplyMonths = 6;
}
