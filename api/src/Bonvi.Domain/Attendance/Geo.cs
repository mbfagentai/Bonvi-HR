namespace Bonvi.Domain.Attendance;

/// <summary>
/// Ikki koordinata orasidagi masofa — tizimdagi YAGONA manba.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega bitta joyda.</b> Masofa ikki tomondan so'raladi: belgini qabul
/// qilish qarori («obyekt hududidami») va o'sha qarorni tushuntirish
/// («eng yaqin obyektgacha 420 m»). Ikki nusxa ikki xil Yer radiusi bilan
/// yozilsa, javoblar FARQ QILADI va farq aynan chegara yonida ko'rinadi —
/// ya'ni odamlar bahslashadigan joyda. <c>BonviTime</c> dagi bilan bir xil
/// mulohaza.
/// </para>
/// <para>
/// <b>Nega haversine, PostGIS emas.</b> Kuniga ~100 ta belgi, har birida
/// ≤ 20 ta obyekt bilan solishtirish — kuniga ikki minginchi hisob, ya'ni
/// mikrosoniyalar. PostGIS esa kutubxona emas, JOYLASHTIRISH bog'liqligi:
/// u tasvirga o'rnatilishi, har bir bazada superfoydalanuvchi tomonidan
/// yoqilishi va tiklashda versiyasi mos kelishi kerak. 12 qatorlik funksiya
/// uchun bunday narx birinchi kuni ko'rinmaydi, bazani ko'chirish kerak
/// bo'lgan kuni esa qimmat.
/// </para>
/// <para>
/// <b>Nega sfera, ellipsoid emas.</b> Sferik haversine WGS-84 ellipsoidiga
/// nisbatan ≤ 0,5 % xato beradi — 200 metrlik to'siqda bu bir metrdan kam.
/// O'lchovning o'zi (telefon GPS'i) 5–100 m aniqlikda, ya'ni aniqroq
/// formula yo'qolgan aniqlikni qaytarib bermaydi.
/// </para>
/// </remarks>
public static class Geo
{
    /// <summary>Yerning o'rtacha radiusi — IUGG, metr.</summary>
    /// <remarks>
    /// Aynan shu son tanlangani muhim emas, BITTA son bo'lgani muhim
    /// (sinf izohi). 6 371 008,8 — xalqaro kelishilgan o'rtacha qiymat,
    /// ya'ni «qayerdan olingan?» degan savolga javobi bor.
    /// </remarks>
    public const double EarthRadiusMeters = 6_371_008.8;

    /// <summary>Ikki nuqta orasidagi sirt masofasi, metrda.</summary>
    /// <param name="latitude1">Birinchi nuqta kengligi (gradus).</param>
    /// <param name="longitude1">Birinchi nuqta uzunligi (gradus).</param>
    /// <param name="latitude2">Ikkinchi nuqta kengligi (gradus).</param>
    /// <param name="longitude2">Ikkinchi nuqta uzunligi (gradus).</param>
    public static double DistanceMeters(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        var lat1 = double.DegreesToRadians(latitude1);
        var lat2 = double.DegreesToRadians(latitude2);
        var dLat = lat2 - lat1;
        var dLon = double.DegreesToRadians(longitude2 - longitude1);

        // Haversine ataylab: oddiy «kosinuslar teoremasi» formulasi kichik
        // masofalarda `double` ning aniqligiga urilib, o'nlab metrgacha
        // shovqin beradi — bizda esa masofalarning ko'pi aynan kichik.
        var h = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
            + (Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));

        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    /// <summary>Bir gradus kenglik — metr (shu sferada, har joyda bir xil).</summary>
    public const double MetersPerDegree = EarthRadiusMeters * Math.PI / 180;

    /// <summary>
    /// Nuqta ko'pburchakdan qancha TASHQARIDA — metr; ichida bo'lsa <c>0</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Mahalliy tekis proyeksiya</b> (SPEC-joylashuvlar §B): nuqtaning
    /// o'zi koordinata boshi, bir gradus kenglik <see cref="MetersPerDegree"/>,
    /// bir gradus uzunlik esa shu kenglikdagi kosinusga qisqargan. Obyekt
    /// ≤ 5 km bo'lgani uchun (<see cref="WorkSite.MaxRadiusMeters"/>) bu
    /// yaqinlashishning xatosi santimetrlarda — telefon GPS'ining 5–100 m
    /// xatosidan ming barobar kichik. Haversine har bir qirra uchun
    /// «nuqtadan kesmagacha» degan savolga javob bermaydi, sferik
    /// geometriya esa shu aniqlik uchun ortiqcha murakkablik.
    /// </para>
    /// <para>
    /// Ichkarilik — nur otish (ray casting): nuqtadan sharqqa ketgan nur
    /// qirralarni toq marta kesib o'tsa, nuqta ichkarida. Aynan qirraning
    /// ustida turgan nuqtada bu usul u yoki bu tomonga og'ishi mumkin —
    /// lekin o'sha holatda eng yaqin qirragacha masofa 0 chiqadi va natija
    /// baribir bir xil: <c>0</c>.
    /// </para>
    /// <para>
    /// Sana o'zgarish chizig'i (±180°) hisobga olinmaydi — bizning
    /// obyektlarimizdan 110° uzoqda.
    /// </para>
    /// </remarks>
    /// <param name="latitude">Nuqta kengligi (gradus).</param>
    /// <param name="longitude">Nuqta uzunligi (gradus).</param>
    /// <param name="ring">Ko'pburchak uchlari — ochiq halqa (birinchisi oxirgisiga teng emas).</param>
    public static double PolygonOutsideMeters(double latitude, double longitude, IReadOnlyList<GeoPoint> ring)
    {
        ArgumentNullException.ThrowIfNull(ring);

        var metersPerDegreeLongitude = MetersPerDegree * Math.Cos(double.DegreesToRadians(latitude));

        var inside = false;
        var nearest = double.PositiveInfinity;

        for (int current = 0, previous = ring.Count - 1; current < ring.Count; previous = current++)
        {
            var (ax, ay) = Project(ring[previous]);
            var (bx, by) = Project(ring[current]);

            // Nur y = 0 chizig'i bo'ylab +x tomonga ketadi. Qirra shu
            // chiziqni kesib o'tsa-yu, kesishish nuqtasi O'NG tomonda bo'lsa,
            // holat almashadi.
            if ((ay > 0) != (by > 0) && ax + ((bx - ax) * -ay / (by - ay)) > 0)
            {
                inside = !inside;
            }

            nearest = Math.Min(nearest, DistanceFromOriginToSegment(ax, ay, bx, by));
        }

        return inside ? 0 : nearest;

        (double X, double Y) Project(GeoPoint vertex)
            => (((double)vertex.Longitude - longitude) * metersPerDegreeLongitude,
                ((double)vertex.Latitude - latitude) * MetersPerDegree);
    }

    /// <summary>Koordinata boshidan (0, 0) AB kesmagacha bo'lgan eng qisqa masofa.</summary>
    private static double DistanceFromOriginToSegment(double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var lengthSquared = (dx * dx) + (dy * dy);

        // Kesmadagi eng yaqin nuqtaning ulushi (0 — A, 1 — B). Nol uzunlikdagi
        // kesma domen tekshiruvidan o'tmaydi, lekin bu yerda ham bo'linish
        // xatosi bermasin.
        var t = lengthSquared == 0
            ? 0
            : Math.Clamp(-((ax * dx) + (ay * dy)) / lengthSquared, 0, 1);

        var px = ax + (t * dx);
        var py = ay + (t * dy);

        return Math.Sqrt((px * px) + (py * py));
    }
}
