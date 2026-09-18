using Bonvi.Domain.PublicApi;

namespace Bonvi.Domain.Abstractions;

/// <summary>Tashqi API kalitlari uchun saqlash porti.</summary>
public interface IApiKeyRepository : IRepository<ApiKey>
{
    /// <summary>
    /// Prefiks bo'yicha topadi — kalitni tekshirishning YAGONA yo'li.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Qidiruv xesh bo'yicha EMAS, prefiks bo'yicha: xesh bo'yicha izlash
    /// har bir so'rovda indeks bo'ylab 32 baytlik taqqoslash bo'lardi va
    /// eng muhimi — bekor qilingan kalitni «topilmadi» dan ajratib
    /// bo'lmasdi. Prefiks esa ochiq, qisqa va unikal.
    /// </para>
    /// <para>
    /// Bekor qilinganlar ham QAYTADI: ular «noma'lum kalit» bilan bir xil
    /// xato olishi kerak, lekin buni hal qilish use-case ishi —
    /// repozitoriy yozuvni yashirmaydi.
    /// </para>
    /// </remarks>
    Task<ApiKey?> FindByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>Shu nomdagi BEKOR QILINMAGAN kalit bormi.</summary>
    /// <remarks>
    /// Bekor qilinganlar hisobga olinmaydi: «BonviCall» kaliti sizib ketib
    /// bekor qilinsa, o'sha loyihaga yangi kalitni AYNAN o'sha nom bilan
    /// berish mumkin bo'lishi kerak.
    /// </remarks>
    Task<bool> ActiveNameExistsAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Barcha kalitlar — bekor qilinganlar bilan birga, yangisidan eskisiga.
    /// </summary>
    /// <remarks>
    /// Sahifalash ATAYLAB yo'q: kalitlar soni holdingdagi loyihalar soniga
    /// teng va u hech qachon o'nlab qatordan oshmaydi.
    /// </remarks>
    Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken cancellationToken = default);
}
