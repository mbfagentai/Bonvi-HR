using Bonvi.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bonvi.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Har bir muvaffaqiyatli saqlashdan keyin
/// <see cref="IDataChangeSignal"/> ni uyg'otadi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega interceptor, use-case'larda qo'lda emas</b> —
/// <c>AuditableInterceptor</c> dagi bilan AYNAN bir xil sabab: qo'lda
/// yozilgan chaqiruv bitta joyda unutiladi va o'sha joydagi o'zgarish
/// jonli bildirishnomaga tushmay qoladi. Bu yerda esa chetlab o'tish
/// yo'li yo'q — bazaga yozishning hamma yo'li <c>SaveChanges</c> dan
/// o'tadi.
/// </para>
/// <para>
/// <b>Nega <c>SavedChangesAsync</c> (saqlangandan KEYIN), oldin emas.</b>
/// Tranzaksiya yiqilishi mumkin. Oldin uyg'otilsa, ulangan brauzerlar
/// hech qachon sodir bo'lmagan o'zgarish uchun ro'yxatni qayta
/// hisoblardi — eng yomoni, ular buni tranzaksiya orqaga qaytishidan
/// OLDIN qilib, eski holatni «yangi» deb ko'rsatishi mumkin edi.
/// </para>
/// <para>
/// <b>Nega FILTR yo'q</b> («faqat nomzod yozilganda uyg'ot»): signal
/// «bildirishnoma paydo bo'ldi» degani emas, «ma'lumot o'zgardi»
/// degani. Qaysi o'zgarish bildirishnomaga aylanishini faqat
/// <c>NotificationCollector</c> biladi va u qoidani bu yerda
/// TAKRORLASH — ikkita haqiqat manbai yasash demak. Ortiqcha
/// uyg'otishning narxi esa arzon: qayta hisoblangan ro'yxat oldingisi
/// bilan bir xil chiqsa, u brauzerga UMUMAN yuborilmaydi
/// (<c>NotificationEndpoints</c> dagi solishtiruv).
/// </para>
/// </remarks>
public sealed class DataChangeInterceptor : SaveChangesInterceptor
{
    private readonly IDataChangeSignal _signal;

    public DataChangeInterceptor(IDataChangeSignal signal) => _signal = signal;

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        // Nol qator o'zgargan saqlash ham bo'ladi (masalan hech narsa
        // o'zgarmagan formani saqlash). Uni uyg'otish bekorga ish.
        if (result > 0)
        {
            _signal.Notify();
        }

        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
