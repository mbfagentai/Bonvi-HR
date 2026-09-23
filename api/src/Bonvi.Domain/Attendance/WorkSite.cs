using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>
/// Obyekt — telefondan kelgan belgi QAYERDA qabul qilinadi.
/// </summary>
/// <remarks>
/// <para>
/// To'siq ikki shaklda bo'ladi (<see cref="Shape"/>, SPEC-joylashuvlar §A):
/// <b>doira</b> — markaz (<see cref="Latitude"/>, <see cref="Longitude"/>) va
/// radius; <b>ko'pburchak</b> — xaritada chizilgan uchlar
/// (<see cref="Polygon"/>) va chetidan <see cref="EdgeBufferMeters"/>
/// metrlik zaxira. Masofani SERVER hisoblaydi (<see cref="Geo"/>) —
/// telefonning «men ichkaridaman» degan gapi hech qayerda ishlatilmaydi.
/// </para>
/// <para>
/// <b>Nega ma'lumotnoma (<see cref="ReferenceEntity"/>).</b> Qolgan to'qqizta
/// ma'lumotnoma bilan bir xil savollarga javob beradi: nomi unikal,
/// o'chirilmaydi — faolsizlantiriladi, kim va qachon o'zgartirgani yoziladi.
/// «To'siqni kim 3 km ga kengaytirdi?» degan savol albatta beriladi va
/// javobi faqat <c>updated_by</c> da bo'ladi.
/// </para>
/// <para>
/// ⚠️ <see cref="ReferenceEntity.IsActive"/> va <c>deleted_at</c> — IKKI xil
/// gap: birinchisi «bu yerda endi belgi qabul qilinmaydi, lekin joy hamon
/// bizniki» (mavsumiy ombor), ikkinchisi «bu yozuv xato edi». Belgini qabul
/// qilish yo'li ikkalasini ham chetlaydi, HR ro'yxati esa faolsizlarni
/// ko'rsatadi.
/// </para>
/// </remarks>
public sealed class WorkSite : ReferenceEntity
{
    /// <summary>Sukut radius — metr.</summary>
    public const int DefaultRadiusMeters = 200;

    /// <summary>
    /// Eng kichik radius — metr.
    /// </summary>
    /// <remarks>
    /// Bino ichida GPS aniqligi 100 metrgacha yetadi. 20 metrlik to'siq
    /// binoning ICHIDA turgan odamni rad etardi va bu unga «ilova buzuq»
    /// bo'lib ko'rinardi. 50 — kafolatlangan yolg'on rad etishni
    /// bermaydigan eng kichik qiymat.
    /// </remarks>
    public const int MinRadiusMeters = 50;

    /// <summary>
    /// Eng katta radius — metr.
    /// </summary>
    /// <remarks>
    /// Bir necha kilometrdan keyin to'siq «obyektda» emas, «shaharda»
    /// degan ma'noni beradi — ya'ni nazorat emas, kimdir qo'ygan belgi.
    /// Ayni cheklov «u doim rad etilyapti» degan murojaatga
    /// <c>radius = 50000</c> deb javob berishning oldini oladi.
    /// </remarks>
    public const int MaxRadiusMeters = 5000;

    /// <summary>Ko'pburchak chetidagi sukut zaxira — metr (SPEC §0, D2).</summary>
    /// <remarks>
    /// Bino ichida GPS 30–100 m «suzadi»: devorning ichida turgan odamning
    /// nuqtasi tez-tez tashqariga tushadi. 50 — egasi tanlagan qiymat.
    /// </remarks>
    public const int DefaultEdgeBufferMeters = 50;

    /// <summary>Eng kichik zaxira — metr. Nol ruxsat etiladi: chiziq aynan devor bo'lsa.</summary>
    public const int MinEdgeBufferMeters = 0;

    /// <summary>
    /// Eng katta zaxira — metr.
    /// </summary>
    /// <remarks>
    /// <see cref="MaxRadiusMeters"/> bilan bir xil mulohaza: zaxira hududni
    /// jimgina kengaytirish vositasiga aylanmasligi kerak.
    /// </remarks>
    public const int MaxEdgeBufferMeters = 500;

    /// <summary>Manzil matnining eng katta uzunligi.</summary>
    public const int MaxAddressLength = 300;

    private WorkSite(Guid id, string name, decimal latitude, decimal longitude, int radiusMeters)
        : base(id, name)
    {
        Latitude = latitude;
        Longitude = longitude;
        RadiusMeters = radiusMeters;
        Shape = WorkSiteShape.Circle;
        EdgeBufferMeters = DefaultEdgeBufferMeters;
    }

    /// <summary>EF Core uchun.</summary>
    private WorkSite()
    {
    }

    /// <summary>Kenglik, o'nlik gradus (−90…90).</summary>
    /// <remarks>
    /// <c>decimal</c>, <c>double</c> emas — va sabab pul qoidasi emas
    /// (bu pul emas). Birinchidan, koordinatani ODAM o'qiydi, qayta
    /// kiritadi va solishtiradi: JSON javobida <c>41.31108099999999</c>
    /// bo'lib qaytadigan qiymat abadiy murojaat manbai. Ikkinchidan,
    /// kelajakda kimdir SQL'da <c>latitude = 41.311081</c> deb yozsa,
    /// suzuvchi ustunda bu jimgina hech narsa topmasdi.
    /// </remarks>
    public decimal Latitude { get; private set; }

    /// <summary>Uzunlik, o'nlik gradus (−180…180).</summary>
    public decimal Longitude { get; private set; }

    /// <summary>To'siq radiusi, metr.</summary>
    /// <remarks>
    /// Faqat <see cref="WorkSiteShape.Circle"/> da ishlatiladi. Ko'pburchakda
    /// oldingi qiymati (yangi yozuvda <see cref="DefaultRadiusMeters"/>)
    /// o'zgarmay turadi — bazadagi <c>ck_work_sites_radius</c> cheklovi
    /// shakldan qat'i nazar amal qiladi (SPEC §A).
    /// </remarks>
    public int RadiusMeters { get; private set; }

    /// <summary>Erkin matnli manzil — HR ro'yxati uchun; qabul qilish qaroriga ta'sir qilmaydi.</summary>
    public string? Address { get; private set; }

    /// <summary>To'siq shakli.</summary>
    public WorkSiteShape Shape { get; private set; }

    /// <summary>
    /// Ko'pburchak uchlari — faqat <see cref="WorkSiteShape.Polygon"/> da; doirada <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Ochiq halqa, 3–50 uch, o'zini kesib o'tmaydi (<see cref="WorkSitePolygon"/>).
    /// Ko'pburchakda <see cref="Latitude"/>/<see cref="Longitude"/> uchlarning
    /// o'rtacha nuqtasini saqlaydi — uni SERVER hisoblaydi, mijoz yubormaydi.
    /// </remarks>
    public IReadOnlyList<GeoPoint>? Polygon { get; private set; }

    /// <summary>
    /// Ko'pburchak chetidan tashqaridagi qabul zaxirasi, metr (0…500).
    /// </summary>
    /// <remarks>
    /// Faqat ko'pburchakda ma'noli. Doirada ham qiymati turadi (ustun
    /// <c>NOT NULL</c>) va shakl qayta ko'pburchakka o'tkazilganda qaytib
    /// keladi.
    /// </remarks>
    public int EdgeBufferMeters { get; private set; }

    /// <summary>Yangi obyekt yozuvini yaratadi.</summary>
    /// <param name="name">Nom — majburiy va takrorlanmas.</param>
    /// <param name="latitude">Kenglik.</param>
    /// <param name="longitude">Uzunlik.</param>
    /// <param name="radiusMeters">Radius; berilmasa <see cref="DefaultRadiusMeters"/>.</param>
    public static Result<WorkSite> Create(
        string? name,
        decimal latitude,
        decimal longitude,
        int? radiusMeters = null)
    {
        var normalized = Normalize(name);
        if (normalized is null)
        {
            return Error.Validation("malumotnoma.nom_bosh", "Nom kiritilishi shart.");
        }

        var radius = radiusMeters ?? DefaultRadiusMeters;

        var valid = Validate(latitude, longitude, radius);
        if (valid.IsFailure)
        {
            return valid.Error;
        }

        return new WorkSite(NewId(), normalized, latitude, longitude, radius);
    }

    /// <summary>Ko'pburchak shaklidagi yangi obyekt yozuvini yaratadi.</summary>
    /// <remarks>
    /// Markaz (<see cref="Latitude"/>, <see cref="Longitude"/>) uchlardan
    /// hisoblanadi; radius <see cref="DefaultRadiusMeters"/> bo'lib turadi va
    /// ishlatilmaydi (SPEC §A).
    /// </remarks>
    /// <param name="name">Nom — majburiy va takrorlanmas.</param>
    /// <param name="vertices">Uchlar — ochiq halqa, 3–50 ta.</param>
    /// <param name="edgeBufferMeters">Chet zaxirasi; berilmasa <see cref="DefaultEdgeBufferMeters"/>.</param>
    public static Result<WorkSite> CreatePolygon(
        string? name,
        IReadOnlyList<GeoPoint>? vertices,
        int? edgeBufferMeters = null)
    {
        var normalized = Normalize(name);
        if (normalized is null)
        {
            return Error.Validation("malumotnoma.nom_bosh", "Nom kiritilishi shart.");
        }

        var buffer = edgeBufferMeters ?? DefaultEdgeBufferMeters;

        var polygon = ValidatePolygon(vertices, buffer);
        if (polygon.IsFailure)
        {
            return polygon.Error;
        }

        var (ring, centroid) = polygon.Value;

        var site = new WorkSite(NewId(), normalized, centroid.Latitude, centroid.Longitude, DefaultRadiusMeters);
        site.ApplyPolygon(ring, centroid, buffer);

        return site;
    }

    /// <summary>Doira: markaz va radiusni qayta yozadi. Nom bu yerdan O'ZGARMAYDI.</summary>
    /// <remarks>
    /// <para>
    /// Nom <see cref="ReferenceEntity.Rename"/> orqali — u takrorni
    /// tekshiradigan use-case bilan birga ketadi (<c>AttendanceDevice</c>
    /// dagi bilan bir xil ajratish).
    /// </para>
    /// <para>
    /// Ko'pburchak bo'lgan obyekt shu chaqiruv bilan DOIRAGA aylanadi:
    /// uchlar o'chadi, <see cref="EdgeBufferMeters"/> esa joyida qoladi.
    /// </para>
    /// </remarks>
    /// <param name="latitude">Yangi kenglik.</param>
    /// <param name="longitude">Yangi uzunlik.</param>
    /// <param name="radiusMeters">Yangi radius.</param>
    public Result Reconfigure(decimal latitude, decimal longitude, int radiusMeters)
    {
        var valid = Validate(latitude, longitude, radiusMeters);
        if (valid.IsFailure)
        {
            return valid;
        }

        Latitude = latitude;
        Longitude = longitude;
        RadiusMeters = radiusMeters;
        Shape = WorkSiteShape.Circle;
        Polygon = null;

        return Result.Success();
    }

    /// <summary>Ko'pburchak: uchlar va zaxirani qayta yozadi. Nom bu yerdan O'ZGARMAYDI.</summary>
    /// <remarks>
    /// Doira bo'lgan obyekt shu chaqiruv bilan KO'PBURCHAKKA aylanadi;
    /// <see cref="RadiusMeters"/> oldingi qiymatida qoladi (ishlatilmaydi).
    /// </remarks>
    /// <param name="vertices">Yangi uchlar.</param>
    /// <param name="edgeBufferMeters">Yangi chet zaxirasi.</param>
    public Result ReconfigurePolygon(IReadOnlyList<GeoPoint>? vertices, int edgeBufferMeters)
    {
        var polygon = ValidatePolygon(vertices, edgeBufferMeters);
        if (polygon.IsFailure)
        {
            return polygon.Error;
        }

        var (ring, centroid) = polygon.Value;
        ApplyPolygon(ring, centroid, edgeBufferMeters);

        return Result.Success();
    }

    /// <summary>Manzilni yozadi: chetdagi bo'shliqlar olinadi, bo'sh matn — <c>null</c>.</summary>
    /// <param name="address">Erkin matn.</param>
    public Result SetAddress(string? address)
    {
        var trimmed = string.IsNullOrWhiteSpace(address) ? null : address.Trim();

        if (trimmed?.Length > MaxAddressLength)
        {
            return Error.Validation(
                "obyekt.manzil_uzun",
                $"Manzil {MaxAddressLength} belgidan oshmasligi kerak.");
        }

        Address = trimmed;
        return Result.Success();
    }

    /// <summary>Berilgan nuqtadan obyekt markazigacha bo'lgan masofa, metr.</summary>
    /// <param name="latitude">Nuqta kengligi.</param>
    /// <param name="longitude">Nuqta uzunligi.</param>
    public double DistanceMetersTo(decimal latitude, decimal longitude)
        => Geo.DistanceMeters(
            (double)Latitude,
            (double)Longitude,
            (double)latitude,
            (double)longitude);

    /// <summary>
    /// Nuqtani SHU obyektning to'sig'i bilan o'lchaydi (SPEC-joylashuvlar §B).
    /// </summary>
    /// <remarks>
    /// Qaysi obyekt tanlanishi bu yerda emas — <see cref="Geofence.Locate"/> da.
    /// </remarks>
    /// <param name="latitude">Nuqta kengligi.</param>
    /// <param name="longitude">Nuqta uzunligi.</param>
    public GeofenceReading Measure(decimal latitude, decimal longitude)
    {
        switch (Shape)
        {
            case WorkSiteShape.Circle:
                var toCentre = DistanceMetersTo(latitude, longitude);

                return new GeofenceReading(
                    this,
                    OutsideMeters: toCentre - RadiusMeters,
                    ToleranceMeters: 0,
                    FrozenDistanceMeters: toCentre,
                    FrozenAllowanceMeters: RadiusMeters);

            case WorkSiteShape.Polygon:
                // `ck_work_sites_polygon` bu holatni bazada to'sadi. Bu yerda
                // jimgina doira deb o'lchash noto'g'ri odamni qabul qilardi.
                var ring = Polygon ?? throw new InvalidOperationException(
                    $"Obyekt {Id}: shakli ko'pburchak, lekin uchlari yo'q.");

                var outside = Geo.PolygonOutsideMeters((double)latitude, (double)longitude, ring);

                return new GeofenceReading(
                    this,
                    OutsideMeters: outside,
                    ToleranceMeters: EdgeBufferMeters,
                    FrozenDistanceMeters: outside,
                    FrozenAllowanceMeters: EdgeBufferMeters);

            default:
                throw new InvalidOperationException($"Obyekt {Id}: noma'lum shakl {Shape}.");
        }
    }

    /// <summary>Ko'pburchak va zaxirani tekshiradi; uchlar va markazni qaytaradi.</summary>
    /// <remarks>
    /// <para>
    /// Tekshiruv tartibi: zaxira, uchlar (soni, oralig'i, takrori, kesishishi),
    /// keyin o'lcham. Hammasi arzon — bazaga murojaat yo'q.
    /// </para>
    /// <para>
    /// ⚠️ <b>O'lcham chegarasi</b> (har bir uch markazdan
    /// <see cref="MaxRadiusMeters"/> ichida) SPEC da yo'q va ATAYLAB
    /// qo'shildi. U bo'lmasa, doiraning 5 km shifti ko'pburchak orqali
    /// aylanib o'tilardi — «u doim rad etilyapti» degan murojaatga butun
    /// shaharni chizib javob berish mumkin bo'lardi. Ikkinchi sabab —
    /// <see cref="Geo.PolygonOutsideMeters"/> ning tekis proyeksiyasi aynan
    /// shu o'lchamgacha aniq.
    /// </para>
    /// </remarks>
    private static Result<(GeoPoint[] Ring, GeoPoint Centroid)> ValidatePolygon(
        IReadOnlyList<GeoPoint>? vertices,
        int edgeBufferMeters)
    {
        if (edgeBufferMeters is < MinEdgeBufferMeters or > MaxEdgeBufferMeters)
        {
            return Error.Validation(
                "obyekt.polygon_zaxira",
                $"Chet zaxirasi {MinEdgeBufferMeters} dan {MaxEdgeBufferMeters} metrgacha bo'lishi kerak.");
        }

        var ring = WorkSitePolygon.Normalize(vertices);
        if (ring.IsFailure)
        {
            return ring.Error;
        }

        var centroid = WorkSitePolygon.Centroid(ring.Value);

        if (WorkSitePolygon.MaxSpreadMeters(ring.Value, centroid) > MaxRadiusMeters)
        {
            return Error.Validation(
                "obyekt.polygon_katta",
                $"Ko'pburchak juda katta: har bir nuqta markazdan {MaxRadiusMeters} metr ichida bo'lishi kerak.");
        }

        return (ring.Value, centroid);
    }

    private void ApplyPolygon(GeoPoint[] ring, GeoPoint centroid, int edgeBufferMeters)
    {
        Shape = WorkSiteShape.Polygon;
        Polygon = ring;
        Latitude = centroid.Latitude;
        Longitude = centroid.Longitude;
        EdgeBufferMeters = edgeBufferMeters;
    }

    /// <summary>
    /// Koordinata va radius chegaralari.
    /// </summary>
    /// <remarks>
    /// ⚠️ Xuddi shu qoidalar bazada ham CHECK bo'lib turadi
    /// (<c>work_schedule_days</c> dagi bilan bir xil sabab): domen tekshiruvi
    /// migratsiya yoki qo'lda yozilgan SQL orqali kelgan qatorni qo'riqlay
    /// olmaydi.
    /// </remarks>
    private static Result Validate(decimal latitude, decimal longitude, int radiusMeters)
    {
        if (latitude is < -90 or > 90)
        {
            return Error.Validation("davomat.obyekt_kenglik", "Kenglik −90 dan 90 gacha bo'lishi kerak.");
        }

        if (longitude is < -180 or > 180)
        {
            return Error.Validation("davomat.obyekt_uzunlik", "Uzunlik −180 dan 180 gacha bo'lishi kerak.");
        }

        if (radiusMeters is < MinRadiusMeters or > MaxRadiusMeters)
        {
            return Error.Validation(
                "davomat.obyekt_radiusi",
                $"Radius {MinRadiusMeters} dan {MaxRadiusMeters} metrgacha bo'lishi kerak.");
        }

        return Result.Success();
    }
}
