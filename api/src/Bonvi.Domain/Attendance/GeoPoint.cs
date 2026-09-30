namespace Bonvi.Domain.Attendance;

/// <summary>Xaritadagi bitta nuqta — o'nlik gradusda.</summary>
/// <remarks>
/// <para>
/// <c>decimal</c>, <c>double</c> emas — sababi <see cref="WorkSite.Latitude"/>
/// izohidagi bilan bir xil: koordinatani odam o'qiydi, nusxalaydi va
/// solishtiradi. Ko'pburchak tekshiruvi ham shu sababli ANIQ bajariladi
/// (<see cref="WorkSitePolygon"/>): olti kasr xonali sonlarning ko'paytmasi
/// <c>decimal</c> ga to'liq sig'adi va «deyarli kesishadi» degan holat yo'q.
/// </para>
/// <para>
/// Bazada <c>work_sites.polygon</c> ustunida <c>{ "lat": …, "lng": … }</c>
/// ko'rinishida yotadi — JSON nomlarini infratuzilma beradi, domen emas.
/// </para>
/// </remarks>
/// <param name="Latitude">Kenglik (−90…90).</param>
/// <param name="Longitude">Uzunlik (−180…180).</param>
public readonly record struct GeoPoint(decimal Latitude, decimal Longitude);
