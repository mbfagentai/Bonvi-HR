using Bonvi.Domain.Attendance;

namespace Bonvi.Domain.Tests.Attendance;

/// <summary>
/// Obyekt testlarida qayta ishlatiladigan shakllar — Toshkent atrofida.
/// </summary>
/// <remarks>
/// Kenglikda 0,001° ≈ 111 m, uzunlikda (41,3° da) 0,001° ≈ 83,5 m.
/// </remarks>
internal static class ObyektShakllari
{
    /// <summary>~220 × 220 m kvadrat, markazi (41.3, 69.28), soat miliga qarshi.</summary>
    public static readonly GeoPoint[] Kvadrat =
    [
        new(41.299m, 69.2787m),
        new(41.299m, 69.2813m),
        new(41.301m, 69.2813m),
        new(41.301m, 69.2787m),
    ];

    /// <summary>
    /// L-shakl: <see cref="Kvadrat"/> dan shimoli-sharqiy chorak
    /// (kenglik 41.300–41.301, uzunlik 69.2800–69.2813) kesib olingan.
    /// </summary>
    public static readonly GeoPoint[] LShakl =
    [
        new(41.299m, 69.2787m),
        new(41.299m, 69.2813m),
        new(41.300m, 69.2813m),
        new(41.300m, 69.2800m),
        new(41.301m, 69.2800m),
        new(41.301m, 69.2787m),
    ];

    /// <summary>Bir metr kenglik — gradusda (<see cref="Geo.MetersPerDegree"/> ning teskarisi).</summary>
    public static decimal MetrKenglikda(double metr) => (decimal)Math.Round(metr / Geo.MetersPerDegree, 9);
}
