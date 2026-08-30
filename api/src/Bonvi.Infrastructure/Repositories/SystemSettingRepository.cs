using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Settings;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="ISystemSettingRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
/// <remarks>
/// <b>Nega <c>AsNoTracking()</c> YO'Q.</b> Sabab
/// <c>NotificationStateRepository</c> dagi bilan aynan bir xil: bu port
/// ikki tomonlama ishlatiladi. O'qish (sozlamalar ekrani, bildirishnoma
/// yig'ilishi) faqat qiymatni oladi, yangilash esa qaytgan yozuvni
/// O'ZGARTIRADI va saqlaydi. Kuzatuvsiz qaytgan obyektdagi o'zgarish
/// <c>SaveChanges</c> ga umuman yetib bormasdi — amal muvaffaqiyatli
/// ko'rinib, bazada esa eski qiymat qolib ketardi.
/// </remarks>
public sealed class SystemSettingRepository : Repository<SystemSetting>, ISystemSettingRepository
{
    public SystemSettingRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<SystemSetting>> GetAllAsync(
        CancellationToken cancellationToken = default)
        => await Set
            .OrderBy(setting => setting.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<SystemSetting?> FindAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        // Solishtirish AYNIY (ordinal) — kalitlar kodda konstanta bo'lib
        // turadi va ular hech qachon registri bilan farq qilmaydi.
        // `lower(key)` bo'yicha izlash bu yerda faqat unikal indeksni
        // chetlab o'tishga imkon berardi (ma'lumotnomalardagi holat
        // BOSHQA: u yerda nomni odam yozadi).
        return Set.FirstOrDefaultAsync(setting => setting.Key == key, cancellationToken);
    }
}
