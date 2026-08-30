using Bonvi.Domain.Settings;

namespace Bonvi.Domain.Abstractions;

/// <summary>
/// Tizim sozlamalari uchun saqlash porti.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega «hammasini olish» metodi bor</b> (bildirishnoma holatlarida u
/// ATAYLAB yo'q edi). Farq jadval hajmida: sozlamalar soni KODDA
/// belgilangan va u <see cref="SystemSettingKeys.All"/> dan oshmaydi —
/// bugun oltita. Bunday jadvalni bittalab so'rash faqat ortiqcha
/// murojaat bo'lardi: bitta ekran ham, bitta bildirishnoma yig'ilishi
/// ham baribir hammasini talab qiladi.
/// </para>
/// <para>
/// <b><see cref="FindAsync"/> esa YOZISH uchun.</b> Bir nechta sozlamani
/// birdan yangilashda kerak bo'lgani faqat o'zgarayotgan qatorlar —
/// ular esa kalit bo'yicha izlanadi, identifikator bo'yicha emas
/// (mijoz kalitni biladi, identifikatorni bilmaydi va bilishi ham
/// shart emas).
/// </para>
/// <para>
/// <c>Add</c> alohida e'lon qilinmaydi — u <see cref="IRepository{TAggregate}"/>
/// dan keladi. Sozlama YOZUVI birinchi marta qiymat berilganda paydo
/// bo'ladi, ya'ni «yo'q bo'lsa qo'shish» oddiy holat.
/// </para>
/// </remarks>
public interface ISystemSettingRepository : IRepository<SystemSetting>
{
    /// <summary>
    /// Bazadagi BARCHA sozlama yozuvlari.
    /// </summary>
    /// <remarks>
    /// Ro'yxat kalit bo'yicha tartiblangan — bu yerda tartib mazmun
    /// uchun emas, BARQARORLIK uchun: bir xil ma'lumotda javob har doim
    /// bir xil ketma-ketlikda chiqadi. Ekrandagi tartibni esa
    /// <see cref="SystemSettingKeys.All"/> belgilaydi (u ma'no bo'yicha
    /// guruhlangan).
    /// </remarks>
    Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Kalit bo'yicha bitta yozuv. Yozilmagan sozlamada <c>null</c>.</summary>
    /// <remarks>
    /// <c>null</c> — bu XATO EMAS, normal holat: sozlama hali hech qachon
    /// o'zgartirilmagan va tizim sukut qiymat bilan ishlab turibdi
    /// (<see cref="SystemSetting"/> izohi).
    /// </remarks>
    Task<SystemSetting?> FindAsync(string key, CancellationToken cancellationToken = default);
}
