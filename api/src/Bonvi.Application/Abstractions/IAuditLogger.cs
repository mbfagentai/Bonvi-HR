using Bonvi.Domain.Auditing;

namespace Bonvi.Application.Abstractions;

/// <summary>
/// HODISA yozuvini navbatga qo'yish porti — entity o'zgarishi bo'lmagan narsalar uchun.
/// </summary>
/// <remarks>
/// <para>
/// <b>Entity o'zgarishlarini bu yerdan YOZMANG.</b> Ularni
/// <c>AuditTrailInterceptor</c> har bir <c>SaveChanges</c> da o'zi yozadi —
/// eski → yangi qiymatlar, yashirilgan maydonlar, xodim va guruh bilan. Bu
/// port faqat interceptor KO'RMAYDIGAN narsalar uchun: kirish/chiqish,
/// ruxsat rad etilishi, Excel eksporti, bir martalik parolni ko'rish.
/// Ikkalasidan yozilsa, har bir o'zgarish jurnalda ikki marta turardi.
/// </para>
/// <para>
/// <b>Nega saqlamaydi.</b> Bu metod yozuvni FAQAT navbatga qo'yadi, bazaga
/// yozmaydi. Sabab: audit yozuvi haqiqiy amal bilan BITTA tranzaksiyada
/// borishi kerak. Agar bu yerda alohida saqlash chaqirilsa-yu, asosiy amal
/// keyinroq muvaffaqiyatsiz tugasa — jurnalda "bo'lmagan" hodisa qolib
/// ketardi. Yozuv chaqiruvchining <c>IUnitOfWork.SaveChangesAsync</c> ida
/// bazaga tushadi; amalning o'zida o'zgarish bo'lmasa ham (eksport) — o'sha
/// chaqiruv uni yozadi.
/// </para>
/// <para>
/// <b>Kim, qachon va qayerdan</b> — bajaruvchi (ismi va roli bilan), vaqt,
/// amal nomi, guruh, IP va brauzer amalga oshirilishning o'zi tomonidan
/// (<see cref="IAuditActorSource"/>, <see cref="IRequestContext"/>) to'ldiriladi.
/// </para>
/// </remarks>
public interface IAuditLogger
{
    /// <summary>
    /// Joriy bajaruvchi nomidan hodisa yozuvini navbatga qo'yadi.
    /// </summary>
    /// <param name="action">Amal turi.</param>
    /// <param name="entityName">Qaysi obyekt: <c>EmployeeAccount</c>, <c>Employee</c>, <c>Endpoint</c>…</param>
    /// <param name="entityId">Obyektning identifikatori (bo'lmasa <c>null</c>).</param>
    /// <param name="oldValues">Oldingi qiymatlar (JSON, ixtiyoriy).</param>
    /// <param name="newValues">
    /// Tafsilot (JSON, ixtiyoriy). Parol, uning xeshi va shaxsiy ma'lumot
    /// BU YERGA HECH QACHON YOZILMAYDI.
    /// </param>
    void Record(
        AuditAction action,
        string entityName,
        string? entityId,
        string? oldValues = null,
        string? newValues = null);

    /// <summary>
    /// Kirish bilan bog'liq hodisa — so'rovda tasdiqlangan foydalanuvchi YO'Q joylar uchun.
    /// </summary>
    /// <remarks>
    /// Kirish, kirish xatosi, chiqish va token qayta ishlatilishi tokensiz
    /// so'rovda bo'ladi — bajaruvchi so'rovdan emas, ishlovchi topgan hisobdan
    /// olinadi. Hisob topilmasa (noma'lum login) — <see cref="AuditActorKind.Anonymous"/>
    /// va kiritilgan login.
    /// </remarks>
    /// <param name="action">Amal turi.</param>
    /// <param name="userId">Hisob; topilmagan bo'lsa <c>null</c>.</param>
    /// <param name="login">Login nusxasi (hisob topilmasa — kiritilgani).</param>
    /// <param name="detailJson">Tafsilot (JSON) — masalan <c>{"reason":"locked"}</c>.</param>
    void RecordSecurityEvent(AuditAction action, Guid? userId, string? login, string? detailJson);
}
