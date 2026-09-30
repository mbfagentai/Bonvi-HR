namespace Bonvi.Domain.Attendance;

/// <summary>
/// Bitta nuqtaning bitta obyekt to'sig'iga nisbatan o'lchovi (SPEC-joylashuvlar §B).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Ikki juft son — ikki xil savolga.</b>
/// <see cref="OutsideMeters"/> va <see cref="ToleranceMeters"/> — QAROR:
/// belgi <c>OutsideMeters ≤ ToleranceMeters</c> bo'lsa qabul qilinadi.
/// <see cref="FrozenDistanceMeters"/> va <see cref="FrozenAllowanceMeters"/> —
/// DALIL: <c>mobile_check_ins</c> ga yoziladigan ikki operand va ular uchun
/// ham xuddi shu tengsizlik <c>masofa ≤ ruxsat</c> bajariladi, shakl
/// qanday bo'lmasin.
/// </para>
/// <list type="table">
///   <listheader><term>Shakl</term><description>Qaror · Dalil</description></listheader>
///   <item>
///     <term>Doira</term>
///     <description>
///       markazgacha − radius ≤ 0 · markazgacha masofa va radius
///     </description>
///   </item>
///   <item>
///     <term>Ko'pburchak</term>
///     <description>
///       chetgacha (ichida 0) ≤ zaxira · chetgacha masofa va zaxira
///     </description>
///   </item>
/// </list>
/// <para>
/// ⚠️ Doirada dalil MARKAZGACHA masofa, chegaragacha emas — va bu ataylab.
/// Chegaragacha bo'lsa, qabul qilingan har bir doira belgisida u 0 bo'lardi
/// (ichkarida turgan odam chegaradan «0 m tashqarida») va dalil hech narsa
/// aytmasdi. Markazgacha masofa esa «radius 200 dan 2000 ga kengaytirilgan
/// kunning ertasi» savoliga javob beradigan yagona son (ma'lumot modeli §E).
/// Qaysi o'lchov ekani <c>mobile_check_ins.site_shape</c> dan o'qiladi.
/// </para>
/// </remarks>
/// <param name="Site">O'lchangan obyekt.</param>
/// <param name="OutsideMeters">
/// SPEC §B dagi <c>d</c>: doirada markazgacha minus radius (ichida manfiy),
/// ko'pburchakda ichida 0, tashqarida eng yaqin qirragacha.
/// </param>
/// <param name="ToleranceMeters">Qabul zaxirasi: doirada 0, ko'pburchakda chet zaxirasi.</param>
/// <param name="FrozenDistanceMeters">Dalil uchun masofa (sinf izohi).</param>
/// <param name="FrozenAllowanceMeters">Dalil uchun ruxsat: doira radiusi yoki ko'pburchak zaxirasi.</param>
public sealed record GeofenceReading(
    WorkSite Site,
    double OutsideMeters,
    int ToleranceMeters,
    double FrozenDistanceMeters,
    int FrozenAllowanceMeters)
{
    /// <summary>Belgi shu obyektda qabul qilinadimi.</summary>
    /// <remarks>
    /// Chegara QO'SHIB olinadi (<c>&lt;=</c>): «200 metrlik obyekt» degani
    /// 200 metrda turgan odam ichkarida degani. Qat'iy kichik bo'lsa,
    /// chegaraning o'zida turgan belgi rad etilardi va buni tushuntirib
    /// bo'lmasdi.
    /// <para>
    /// ⚠️ Masofa SANTIMETRGACHA yaxlitlanib solishtiriladi: qiya qirra
    /// ustidagi nuqta uchun suzuvchi nuqta hisobi 0 o'rniga 3·10⁻¹⁰ m
    /// beradi va 0 zaxirali ko'pburchak chegarada turgan odamni «0 m
    /// tashqarida» deb rad etardi.
    /// </para>
    /// </remarks>
    public bool Accepted => Margin <= 0;

    /// <summary>
    /// Saralash kaliti — zaxira AYIRILGANDAN keyingi masofa, santimetrgacha
    /// yaxlitlangan. Manfiy yoki 0 — qabul.
    /// </summary>
    public double Margin => Math.Round(OutsideMeters, 2) - ToleranceMeters;
}

/// <summary>
/// Belgini qaysi obyekt qabul qiladi — tizimdagi YAGONA qoida (SPEC-joylashuvlar §B).
/// </summary>
public static class Geofence
{
    /// <summary>
    /// Har bir obyektni o'lchab, eng qulayini qaytaradi; obyekt yo'q bo'lsa <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Tartib <see cref="GeofenceReading.Margin"/> bo'yicha — har bir
    /// obyektning O'Z zaxirasi ayirilgandan keyin. Sof masofa bo'yicha
    /// saralansa, 5 m tashqaridagi doira 40 m tashqaridagi (lekin 50 m
    /// zaxirali) ko'pburchakdan «yaqinroq» bo'lib chiqib, belgi rad
    /// etilardi — holbuki ko'pburchak uni qabul qiladi.
    /// </para>
    /// <para>
    /// Hech biri qabul qilmasa, qaytgan o'lchov rad etish xabariga boradi:
    /// o'sha obyektning nomi va CHEGARASIGACHA masofa
    /// (<see cref="GeofenceReading.OutsideMeters"/>) — «Zavoddan 120 m
    /// tashqaridasiz», markazgacha emas.
    /// </para>
    /// <para>
    /// Teng kalitlarda kirish tartibi saqlanadi (<c>OrderBy</c> barqaror) —
    /// ombor obyektlarni nom bo'yicha beradi, ya'ni natija takrorlanadi.
    /// </para>
    /// </remarks>
    /// <param name="sites">Faol obyektlar.</param>
    /// <param name="latitude">Nuqta kengligi.</param>
    /// <param name="longitude">Nuqta uzunligi.</param>
    public static GeofenceReading? Locate(IEnumerable<WorkSite> sites, decimal latitude, decimal longitude)
    {
        ArgumentNullException.ThrowIfNull(sites);

        return sites
            .Select(site => site.Measure(latitude, longitude))
            .OrderBy(reading => reading.Margin)
            .FirstOrDefault();
    }
}
