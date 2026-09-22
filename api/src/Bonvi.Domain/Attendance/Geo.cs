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
}
