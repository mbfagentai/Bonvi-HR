using Bonvi.Domain.Auditing;

namespace Bonvi.Application.Abstractions;

/// <summary>
/// «Kim, qachon, nimani o'zgartirdi» yozuvini navbatga qo'yish porti.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega saqlamaydi.</b> Bu metod yozuvni FAQAT joriy o'zgarish
/// izlagichiga (change tracker) qo'shadi, bazaga yozmaydi. Sabab: audit
/// yozuvi haqiqiy o'zgarish bilan BITTA tranzaksiyada borishi kerak. Agar
/// bu yerda alohida saqlash chaqirilsa-yu, asosiy amal keyinroq
/// muvaffaqiyatsiz tugasa — jurnalda "bo'lmagan" hodisa qolib ketardi.
/// Shu sababli yozuvni haqiqatan saqlash use-case'ning o'zi
/// <c>IUnitOfWork.SaveChangesAsync</c> chaqirganda amalga oshadi.
/// </para>
/// <para>
/// <b>Kim va qachon.</b> Bajaruvchi (<c>UserId</c>/<c>UserLogin</c>) va vaqt
/// amalga oshirilishning o'zi tomonidan — <c>ICurrentUser</c> va
/// <c>IClock</c> orqali — avtomatik to'ldiriladi, chaqiruvchi ularni
/// qo'lda uzatmaydi.
/// </para>
/// </remarks>
public interface IAuditLogger
{
    /// <summary>
    /// Audit yozuvini navbatga qo'yadi.
    /// </summary>
    /// <param name="action">Amal turi.</param>
    /// <param name="entityName">Qaysi entity: <c>User</c>, <c>Employee</c>, …</param>
    /// <param name="entityId">O'zgargan yozuvning identifikatori.</param>
    /// <param name="oldValues">O'zgarishdan oldingi qiymatlar (JSON, ixtiyoriy).</param>
    /// <param name="newValues">
    /// O'zgarishdan keyingi qiymatlar (JSON, ixtiyoriy). Parol yoki uning
    /// xeshi BU YERGA HECH QACHON YOZILMAYDI.
    /// </param>
    void Record(
        AuditAction action,
        string entityName,
        string? entityId,
        string? oldValues = null,
        string? newValues = null);
}
