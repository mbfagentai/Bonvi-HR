using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>
/// Obyekt — telefondan kelgan belgi QAYERDA qabul qilinadi.
/// </summary>
/// <remarks>
/// <para>
/// Markaz (<see cref="Latitude"/>, <see cref="Longitude"/>) va radius bitta
/// doira beradi; belgi shu doira ichida bo'lsa qabul qilinadi. Masofani
/// SERVER hisoblaydi (<see cref="Geo"/>) — telefonning «men ichkaridaman»
/// degan gapi hech qayerda ishlatilmaydi.
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

    private WorkSite(Guid id, string name, decimal latitude, decimal longitude, int radiusMeters)
        : base(id, name)
    {
        Latitude = latitude;
        Longitude = longitude;
        RadiusMeters = radiusMeters;
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
    public int RadiusMeters { get; private set; }

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

    /// <summary>Markaz va radiusni qayta yozadi. Nom bu yerdan O'ZGARMAYDI.</summary>
    /// <remarks>
    /// Nom <see cref="ReferenceEntity.Rename"/> orqali — u takrorni
    /// tekshiradigan use-case bilan birga ketadi (<c>AttendanceDevice</c>
    /// dagi bilan bir xil ajratish).
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

    /// <summary>Shu masofa to'siq ichidami.</summary>
    /// <remarks>
    /// Chegara QO'SHIB olinadi (<c>&lt;=</c>): «200 metrlik obyekt» degani
    /// 200 metrda turgan odam ichkarida degani. Qat'iy kichik bo'lsa,
    /// chegaraning o'zida turgan belgi rad etilardi va buni tushuntirib
    /// bo'lmasdi.
    /// </remarks>
    /// <param name="distanceMeters">O'lchangan masofa.</param>
    public bool Covers(double distanceMeters) => distanceMeters <= RadiusMeters;

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
