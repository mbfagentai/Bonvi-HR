using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>Xodimning qurilmadagi holati.</summary>
public enum DeviceEnrollmentStatus
{
    /// <summary>Qurilmaga yozilgan va ma'lumoti joriy.</summary>
    Synced = 1,

    /// <summary>Qurilma rad etdi — sababi <see cref="DeviceEnrollment.Error"/> da.</summary>
    Failed = 2,

    /// <summary>Qurilmadan o'chirilgan (ishdan ketgan yoki ro'yxatdan chiqqan).</summary>
    Removed = 3,

    /// <summary>
    /// O'CHIRIB bo'lmadi — odam hali ham terminalda va eshikni ocha oladi.
    /// </summary>
    /// <remarks>
    /// <see cref="Failed"/> dan alohida, chunki ma'nosi teskari: u «yozib
    /// bo'lmadi» (odam terminalda YO'Q yoki eski holatda), bu esa «olib
    /// tashlab bo'lmadi» (ishdan ketgan odam HALI terminalda). Ilgari agent
    /// muvaffaqiyatsiz o'chirishni ham «bajarildi» deb yuborardi va HR
    /// «O'chirilgan» ni ko'rardi — odam esa eshikni ochib yuraverardi.
    /// </remarks>
    RemoveFailed = 4,
}

/// <summary>
/// Xodim × qurilma — agent qurilmaga nima yozganining OXIRGI ma'lum holati
/// (SPEC-agent §D3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Bu navbat EMAS, holat.</b> Agent «ishga olindi», «rasm almashdi»,
/// «ishdan ketdi» xabarlarini qayta o'ynatmaydi: u KERAKLI ro'yxatni
/// (server) qurilmadagi HAQIQIY ro'yxat bilan solishtiradi va farqni
/// tuzatadi. Shu sababli bu yerda faqat «oxirgi marta nima bo'ldi» yozib
/// qo'yiladi — ekran uchun va qayta urinish kerakmi-yo'qmini bilish uchun.
/// Agent bir hafta o'chib tursa ham, yoqilganda holat o'z-o'zidan tiklanadi.
/// </para>
/// <para>
/// <see cref="AppliedVersion"/> — agent QAYSI holatni yozgani
/// (<c>photoKey:forceVersion:ism izi</c>). Serverdagi joriy qiymat undan farq qilsa,
/// xodim «kutilmoqda» bo'ladi: rasm almashgan yoki HR «Qayta yozish»
/// bosgan.
/// </para>
/// <para>
/// ⚠️ <b>Qurilmadagi raqam bu yerda saqlanmaydi.</b> U mosliklar jadvalida
/// (<see cref="AttendanceIdentity"/>) yashaydi — o'tish kelganda xodim
/// aynan o'sha jadval orqali topiladi. Raqamni ikkinchi joyda saqlash ikki
/// nusxa demak bo'lardi.
/// </para>
/// </remarks>
public sealed class DeviceEnrollment : Entity, IAuditable
{
    private DeviceEnrollment(Guid id, Guid deviceId, Guid employeeId)
        : base(id)
    {
        DeviceId = deviceId;
        EmployeeId = employeeId;
    }

    /// <summary>EF Core uchun.</summary>
    private DeviceEnrollment() { }

    public Guid DeviceId { get; private set; }

    public Guid EmployeeId { get; private set; }

    public DeviceEnrollmentStatus Status { get; private set; }

    /// <summary>Agent yozgan holat: <c>photoKey:forceVersion:ism izi</c>.</summary>
    public string? AppliedVersion { get; private set; }

    /// <summary>Qurilmaning o'z xato matni — HR ekranda aynan shuni o'qiydi.</summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Qurilma (yoki rasm qoidasi) aynan qaysi belgini RAD ETGANI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Bir xil belgi qayta urinilmaydi.</b> Yuz sifati past yoki rasm
    /// juda katta — bu qat'iy javob: har 60 soniyada qayta urinish kuniga
    /// ~1440 marta o'sha xatoni olardi va terminalni band qilardi. Belgi
    /// o'zgarsa (yangi rasm, yangi ism, «Qayta yozish»), qator yana
    /// urinishga tushadi.
    /// </para>
    /// <para>
    /// Vaqtinchalik xatoda (tarmoq, terminal javob bermadi) <c>null</c>:
    /// u keyingi tekshiruvda o'zi tuzalishi mumkin va qayta urinilishi kerak.
    /// </para>
    /// </remarks>
    public string? RejectedVersion { get; private set; }

    /// <summary>Oxirgi muvaffaqiyatli yozuv lahzasi (UTC).</summary>
    public DateTimeOffset? SyncedAt { get; private set; }

    /// <summary>
    /// «Qayta yozish» necha marta bosilgan.
    /// </summary>
    /// <remarks>
    /// Versiyaning bir qismi: bosilganda versiya o'zgaradi, ya'ni agent
    /// xodimni qayta yozadi. Boshqa hech narsa majburan qayta yozilmaydi
    /// (§E «In place»).
    /// </remarks>
    public int ForceVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static DeviceEnrollment Start(Guid deviceId, Guid employeeId)
        => new(NewId(), deviceId, employeeId);

    /// <summary>Agent yozdi.</summary>
    public void MarkSynced(string version, DateTimeOffset at)
    {
        Status = DeviceEnrollmentStatus.Synced;
        AppliedVersion = version;
        Error = null;
        RejectedVersion = null;
        SyncedAt = at;
    }

    /// <summary>Yozib bo'lmadi.</summary>
    /// <remarks>
    /// <see cref="AppliedVersion"/> TEGILMAYDI: xato bo'lgan urinish
    /// «yozildi» deb sanalmasligi kerak, aks holda keyingi tekshiruvda
    /// xodim qayta urinilmasdan o'tkazib yuborilardi.
    /// </remarks>
    /// <param name="error">Qurilmaning o'z xato matni.</param>
    /// <param name="at">Hisobot lahzasi.</param>
    /// <param name="rejectedVersion">
    /// Qat'iy rad etilgan belgi — shu belgi o'zgarmaguncha qayta urinilmaydi
    /// (<see cref="RejectedVersion"/>). Vaqtinchalik xatoda <c>null</c>.
    /// </param>
    public void MarkFailed(string? error, DateTimeOffset at, string? rejectedVersion = null)
    {
        Status = DeviceEnrollmentStatus.Failed;
        Error = Trim(error);
        RejectedVersion = Trim(rejectedVersion);
        SyncedAt = at;
    }

    /// <summary>Qurilmadan o'chirildi.</summary>
    public void MarkRemoved(DateTimeOffset at)
    {
        Status = DeviceEnrollmentStatus.Removed;
        AppliedVersion = null;
        Error = null;
        RejectedVersion = null;
        SyncedAt = at;
    }

    /// <summary>Qurilmadan o'chirib bo'lmadi — odam hali terminalda.</summary>
    /// <remarks>
    /// <see cref="AppliedVersion"/> TEGILMAYDI: odam terminalda o'sha holida
    /// qoldi. Agent keyingi tekshiruvda o'chirishni qayta uradi — bu yerda
    /// qayta urinishni to'xtatadigan belgi yo'q, chunki ishdan ketgan
    /// odamning eshigi yopilishi shart.
    /// </remarks>
    public void MarkRemoveFailed(string? error, DateTimeOffset at)
    {
        Status = DeviceEnrollmentStatus.RemoveFailed;
        Error = Trim(error);
        RejectedVersion = null;
        SyncedAt = at;
    }

    /// <summary>«Qayta yozish» — keyingi tekshiruvda qayta yoziladi.</summary>
    public void Force() => ForceVersion++;

    private static string? Trim(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length <= 500 ? trimmed : trimmed[..500];
    }
}
