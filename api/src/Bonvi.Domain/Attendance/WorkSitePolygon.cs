using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>
/// Obyekt ko'pburchagining tekshiruvi va markazi (SPEC-joylashuvlar §A).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega domenda, API da emas.</b> Buzuq ko'pburchak — o'zini kesib
/// o'tgan «kapalak» — nur otish usulida ichki va tashqi qismlari
/// almashinib ketgan hudud beradi: omborning qoq o'rtasida turgan odam
/// «tashqarida» bo'lib chiqishi mumkin. Bunday shakl bazaga umuman
/// tushmasligi kerak, u qaysi yo'l bilan kelishidan qat'i nazar.
/// </para>
/// <para>
/// ⭐ <b>Hisob ANIQ — <c>decimal</c> da.</b> Uchlar olti kasr xonagacha
/// yaxlitlanadi, ya'ni har bir ko'paytma ~18 raqamli son va <c>decimal</c>
/// ga to'liq sig'adi. Natijada «uchta nuqta bir chiziqdami» degan savolga
/// javob «deyarli» emas, aniq «ha» yoki «yo'q». Gradus fazosida tekshirish
/// to'g'ri: mahalliy proyeksiya (<see cref="Geo.PolygonOutsideMeters"/>)
/// chiziqli cho'zish, kesishish esa cho'zishda o'zgarmaydi.
/// </para>
/// </remarks>
internal static class WorkSitePolygon
{
    /// <summary>Kam uch — kamida uchburchak.</summary>
    public const int MinVertices = 3;

    /// <summary>
    /// Ko'p uch — 50.
    /// </summary>
    /// <remarks>
    /// Ombor yoki zavod hududini chizish uchun o'nta nuqta yetadi; 50 —
    /// murakkab devorni ham sig'diradigan, lekin O(n²) kesishish
    /// tekshiruvini (≤ 1 225 juftlik) arzimas qilib qoldiradigan chegara.
    /// </remarks>
    public const int MaxVertices = 50;

    /// <summary>
    /// Uchlarni tekshiradi va saqlanadigan ko'rinishga keltiradi (olti kasr xona).
    /// </summary>
    /// <param name="vertices">HR chizgan uchlar — ochiq halqa.</param>
    public static Result<GeoPoint[]> Normalize(IReadOnlyList<GeoPoint>? vertices)
    {
        if (vertices is null || vertices.Count is < MinVertices or > MaxVertices)
        {
            return Error.Validation(
                "obyekt.polygon_nuqtalar_soni",
                $"Ko'pburchakda {MinVertices} tadan {MaxVertices} tagacha nuqta bo'lishi kerak.");
        }

        if (vertices.Any(vertex => vertex.Latitude is < -90 or > 90 || vertex.Longitude is < -180 or > 180))
        {
            return CoordinateError;
        }

        var ring = vertices
            .Select(vertex => new GeoPoint(Round(vertex.Latitude), Round(vertex.Longitude)))
            .ToArray();

        // Ketma-ket ikki bir xil uch — nol uzunlikdagi qirra. Oxirgi va
        // birinchi uch ham QO'SHNI: yopiq halqa (GeoJSON uslubi, birinchi =
        // oxirgi) shu yerda rad etiladi — SPEC ochiq halqani talab qiladi va
        // uni jimgina «tuzatish» boshqa manbadan kelgan xatoni yashirardi.
        for (var index = 0; index < ring.Length; index++)
        {
            if (ring[index] == ring[(index + 1) % ring.Length])
            {
                return CoordinateError;
            }
        }

        if (SelfIntersects(ring) || TwiceSignedArea(ring) == 0)
        {
            return Error.Validation(
                "obyekt.polygon_kesishadi",
                "Ko'pburchak o'zini kesib o'tmasligi kerak: chiziqlar faqat qo'shni nuqtalarda tutashadi.");
        }

        return ring;
    }

    /// <summary>
    /// Uchlarning o'rtacha nuqtasi — «vertex centroid».
    /// </summary>
    /// <remarks>
    /// Yuza markazi EMAS va bu ataylab (SPEC §A): u faqat xaritani markazlash
    /// va ro'yxatda «qayerda» degan savolga javob uchun kerak, qabul qilish
    /// qarori unga umuman qaramaydi. O'rtacha qiymat esa bir qarashda
    /// tekshiriladi.
    /// </remarks>
    /// <param name="ring">Tekshirilgan uchlar.</param>
    public static GeoPoint Centroid(IReadOnlyList<GeoPoint> ring)
        => new(
            Round(ring.Average(vertex => vertex.Latitude)),
            Round(ring.Average(vertex => vertex.Longitude)));

    /// <summary>Uchning markazdan eng katta uzoqligi, metr.</summary>
    /// <param name="ring">Tekshirilgan uchlar.</param>
    /// <param name="centroid">Ularning markazi.</param>
    public static double MaxSpreadMeters(IReadOnlyList<GeoPoint> ring, GeoPoint centroid)
        => ring.Max(vertex => Geo.DistanceMeters(
            (double)centroid.Latitude,
            (double)centroid.Longitude,
            (double)vertex.Latitude,
            (double)vertex.Longitude));

    /// <summary>Kenglik/uzunlik oralig'i yoki takroriy uch.</summary>
    private static Error CoordinateError { get; } = Error.Validation(
        "obyekt.polygon_koordinata",
        "Ko'pburchak nuqtasi noto'g'ri: kenglik −90…90, uzunlik −180…180 bo'lishi va " +
        "ketma-ket ikki nuqta bir xil bo'lmasligi kerak.");

    /// <summary>
    /// Olti kasr xona — <c>numeric(9,6)</c> va <c>MobileCheckInRules.CoordinateScale</c> bilan bir xil.
    /// </summary>
    private static decimal Round(decimal value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Halqa oddiymi — qirralar faqat qo'shni uchlarda tutashadimi.
    /// </summary>
    /// <remarks>
    /// Ikki xil holat tekshiriladi. (1) Qo'shni qirralar umumiy uchda
    /// orqaga «qaytib» bir-birining ustiga tushadi (nayza: A → B → A ga
    /// tomon). Bu faqat uchta nuqta bir chiziqda VA burilish 180° bo'lganda
    /// yuz beradi; to'g'ri davom etgan uch (A → B → C bir chiziqda) zarar
    /// qilmaydi va qabul qilinadi. (2) Qo'shni bo'lmagan har qanday ikki qirra
    /// umuman tegmasligi kerak — hatto uchi bilan ham («sakkiz» shakli).
    /// </remarks>
    private static bool SelfIntersects(GeoPoint[] ring)
    {
        var count = ring.Length;

        for (var index = 0; index < count; index++)
        {
            var previous = ring[(index + count - 1) % count];
            var vertex = ring[index];
            var next = ring[(index + 1) % count];

            var foldsBack = Orientation(previous, vertex, next) == 0
                && Dot(previous, vertex, next) > 0;

            if (foldsBack)
            {
                return true;
            }
        }

        for (var first = 0; first < count; first++)
        {
            // Qo'shni qirralar (first + 1) va halqaning oxiri (first = 0 da
            // oxirgi qirra) tashlab o'tiladi — ular yuqorida tekshirildi.
            var lastPartner = first == 0 ? count - 1 : count;

            for (var second = first + 2; second < lastPartner; second++)
            {
                if (SegmentsTouch(
                        ring[first],
                        ring[(first + 1) % count],
                        ring[second],
                        ring[(second + 1) % count]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Ikki kesma kesishadimi yoki tegadimi (uchi bilan ham).</summary>
    private static bool SegmentsTouch(GeoPoint a, GeoPoint b, GeoPoint c, GeoPoint d)
    {
        var abc = Math.Sign(Orientation(a, b, c));
        var abd = Math.Sign(Orientation(a, b, d));
        var cda = Math.Sign(Orientation(c, d, a));
        var cdb = Math.Sign(Orientation(c, d, b));

        if (abc * abd < 0 && cda * cdb < 0)
        {
            return true;
        }

        return (abc == 0 && WithinBounds(a, b, c))
            || (abd == 0 && WithinBounds(a, b, d))
            || (cda == 0 && WithinBounds(c, d, a))
            || (cdb == 0 && WithinBounds(c, d, b));
    }

    /// <summary>
    /// Burilish yo'nalishi: musbat — chapga, manfiy — o'ngga, nol — bir chiziqda.
    /// </summary>
    /// <remarks>x — uzunlik, y — kenglik.</remarks>
    private static decimal Orientation(GeoPoint a, GeoPoint b, GeoPoint c)
        => ((b.Longitude - a.Longitude) * (c.Latitude - a.Latitude))
            - ((b.Latitude - a.Latitude) * (c.Longitude - a.Longitude));

    /// <summary>(a − b) · (c − b) — musbat bo'lsa, a va c b dan bir tomonda.</summary>
    private static decimal Dot(GeoPoint a, GeoPoint b, GeoPoint c)
        => ((a.Longitude - b.Longitude) * (c.Longitude - b.Longitude))
            + ((a.Latitude - b.Latitude) * (c.Latitude - b.Latitude));

    /// <summary>Bir chiziqdagi <paramref name="point"/> AB kesmaning to'rtburchagi ichidami.</summary>
    private static bool WithinBounds(GeoPoint a, GeoPoint b, GeoPoint point)
        => point.Longitude >= Math.Min(a.Longitude, b.Longitude)
            && point.Longitude <= Math.Max(a.Longitude, b.Longitude)
            && point.Latitude >= Math.Min(a.Latitude, b.Latitude)
            && point.Latitude <= Math.Max(a.Latitude, b.Latitude);

    /// <summary>Ikkilangan ishorali yuza (shoelace) — nol bo'lsa, ko'pburchak yassi.</summary>
    private static decimal TwiceSignedArea(GeoPoint[] ring)
    {
        var sum = 0m;

        for (var index = 0; index < ring.Length; index++)
        {
            var current = ring[index];
            var next = ring[(index + 1) % ring.Length];

            sum += (current.Longitude * next.Latitude) - (next.Longitude * current.Latitude);
        }

        return sum;
    }
}
