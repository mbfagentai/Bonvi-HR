namespace Bonvi.Domain.Blanks;

/// <summary>
/// Blanka raqamlari hisoblagichi — har bir shablon kodiga bitta qator (SPEC-blankalar §3.7).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega ketma-ketlik (sequence) emas.</b> Raqam bo'shliqsiz bo'lishi kerak (qog'ozdagi
/// «№» kabi): sequence bekor qilingan tranzaksiyada ham qiymatni yoqib yuboradi. Hisoblagich
/// esa qatorni yuborish tranzaksiyasi ichida qulflaydi — tranzaksiya qaytsa raqam ham qaytadi,
/// bir vaqtda yuborilgan ikki blanka esa navbat bilan oladi.
/// </para>
/// <para>
/// Agregat EMAS va entity ham emas (kaliti — kod, <c>Guid</c> emas): unga faqat
/// <c>IBlankNumberCounter</c> bitta <c>INSERT … ON CONFLICT … RETURNING</c> bilan tegadi.
/// Auditda yo'q — texnik hisoblagich; raqamning o'zi blanka qatorida jurnalga tushadi.
/// </para>
/// </remarks>
public sealed class BlankNumberCounter
{
    private BlankNumberCounter() { }

    /// <summary>Shablon kodi (<c>HR-F06</c>) — kalit.</summary>
    public string TemplateCode { get; private set; } = string.Empty;

    /// <summary>Oxirgi berilgan raqam.</summary>
    public int LastNumber { get; private set; }
}
