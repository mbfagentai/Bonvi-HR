using Bonvi.Domain.Attendance;

namespace Bonvi.Application.PublicApi.Abstractions;

/// <summary>
/// Bo'lim davomati kaliti uchun o'tishlar oqimi (<c>GET /api/public/v1/team/passes</c>).
/// </summary>
/// <remarks>
/// O'qish modeli doirani BILMAYDI: u qabul qilingan tartibda xom qatorlarni beradi, qaysi biri
/// kalitning bo'limiga tegishli ekanini handler xodimning ish tarixidan hal qiladi
/// (<c>TeamScope.CoversOn</c>) — «Jamoa» tabi bilan bir xil qoida, ko'chirilgan xodim ham to'g'ri.
/// </remarks>
public interface IPublicTeamReadModel
{
    /// <summary>
    /// Kursordan KEYIN serverga kelgan, xodimga bog'langan o'tishlar — <c>received_at</c> bo'yicha
    /// o'sish tartibida.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kursor kelish (qabul) vaqti bo'yicha, o'tish vaqti bo'yicha emas: agent uzilib, ertalabki
    /// o'tishlarni tushda yuborsa ham, bot ularni o'tkazib yubormaydi.
    /// </para>
    /// <para>
    /// Bir lahzada kelgan guruh (agentning bitta paketi) sahifa chegarasida BO'LINMAYDI: oxirgi
    /// lahzaning hammasi yo qo'shiladi, yo keyingi sahifaga qoladi — shunda kursor faqat lahzadan
    /// iborat bo'la oladi (uuid tartibi Postgres va .NET da har xil, uni kursorga qo'yib bo'lmaydi).
    /// </para>
    /// </remarks>
    /// <param name="after">Kursor lahzasi.</param>
    /// <param name="inclusive"><c>true</c> — lahzaning o'zi ham kiradi (birinchi so'rov: kun boshidan).</param>
    /// <param name="limit">Bir so'rovda ko'rib chiqiladigan qatorlar (to'liq guruh uchun oshishi mumkin).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<PublicPassBatch> PassesReceivedAfterAsync(
        DateTimeOffset after,
        bool inclusive,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xodimning shu kunlardagi BARCHA o'tishlari bo'yicha hisob-kitobdagi yo'nalish
    /// (<c>PassPairing.EffectiveDirections</c>) — HR «O'tishlar» ro'yxati bilan bir xil.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, PassDirection>> EffectiveDirectionsAsync(
        IReadOnlyCollection<(Guid EmployeeId, DateOnly Date)> days,
        CancellationToken cancellationToken = default);
}

/// <summary>Oqimning bir bo'lagi.</summary>
/// <param name="Rows">Qatorlar, kelish tartibida.</param>
/// <param name="HasMore">Chegaradan keyin yana qatorlar bor.</param>
public sealed record PublicPassBatch(IReadOnlyList<PublicPassRow> Rows, bool HasMore);

/// <summary>Oqimning xom qatori.</summary>
/// <param name="Id">O'tish.</param>
/// <param name="EmployeeId">Bog'langan xodim.</param>
/// <param name="FullName">Xodimning to'liq F.I.Sh.</param>
/// <param name="OccurredAt">O'tish lahzasi (UTC).</param>
/// <param name="LocalDate">O'tishning Toshkent kalendar kuni.</param>
/// <param name="ReceivedAt">Serverga kelgan lahza (UTC) — kursor.</param>
/// <param name="Direction">Qurilma bergan yo'nalish (noma'lum bo'lishi mumkin).</param>
/// <param name="Method">Terminal yoki telefon.</param>
public sealed record PublicPassRow(
    Guid Id,
    Guid EmployeeId,
    string FullName,
    DateTimeOffset OccurredAt,
    DateOnly LocalDate,
    DateTimeOffset ReceivedAt,
    PassDirection Direction,
    PassMethod Method);
