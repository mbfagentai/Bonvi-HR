using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Abstractions;

/// <summary>
/// Qurilmaga yozish uchun kerakli o'qishlar — «kim bo'lishi kerak» (SPEC-agent §D2).
/// </summary>
/// <remarks>
/// <para>
/// Alohida port: bu savol na jurnalga, na tabelga tegishli, va
/// <c>IAttendanceReadModel</c> ga qo'shilsa u yana o'ttizta metodga
/// yaqinlashardi. Bu yerda ikkita so'rov bor va ikkalasi ham bitta
/// maqsadga xizmat qiladi.
/// </para>
/// </remarks>
public interface IEnrollmentReadModel
{
    /// <summary>
    /// Bugun qurilmada bo'lishi KERAK bo'lgan xodimlar.
    /// </summary>
    /// <remarks>
    /// Shart: bugunni qamrab oluvchi mehnat munosabati bor. Rasm bu yerda
    /// SHART EMAS — rasmi yo'q odam ham qaytadi, chunki HR ekrani «rasm
    /// yo'q» deb ko'rsatishi kerak. Agentga yuborishdan oldin esa u
    /// chetlatiladi (yuz terminaliga rasmsiz yozib bo'lmaydi).
    /// </remarks>
    /// <param name="today">Toshkent vaqti bo'yicha bugun.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<RosterCandidate>> RosterAsync(
        DateOnly today,
        CancellationToken cancellationToken = default);

    /// <summary>Xodimlarning amaldagi shaxs raqamlari — bitta so'rovda.</summary>
    /// <remarks>
    /// Xodim boshiga <c>ListByEmployeeAsync</c> chaqirish 950 ta so'rov
    /// degani bo'lardi va ro'yxat har 60 soniyada so'raladi.
    /// </remarks>
    /// <param name="provider">Raqamlar fazasi.</param>
    /// <param name="employeeIds">Xodimlar; bo'sh bo'lsa — bo'sh natija.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> PersonNumbersAsync(
        AttendanceIdentityProvider provider,
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken = default);

    /// <summary>Bitta qurilmaning barcha qatorlari — kerakli va haqiqiy holat birga.</summary>
    /// <param name="deviceId">Qurilma.</param>
    /// <param name="today">Toshkent vaqti bo'yicha bugun.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<EnrollmentRow>> DeviceRowsAsync(
        Guid deviceId,
        DateOnly today,
        CancellationToken cancellationToken = default);

    /// <summary>Bitta xodimning har bir FAOL qurilmadagi qatori — xodim kartasi uchun.</summary>
    /// <param name="employeeId">Xodim.</param>
    /// <param name="today">Toshkent vaqti bo'yicha bugun.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<EnrollmentRow>> EmployeeRowsAsync(
        Guid employeeId,
        DateOnly today,
        CancellationToken cancellationToken = default);
}

/// <summary>Ro'yxatga da'vogar xodim.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="FullName">F.I.Sh. — qurilma ro'yxatida ko'rinadi.</param>
/// <param name="PhotoKey">
/// Rasm kaliti; <c>null</c> — rasm yo'q. Kalitning O'ZI versiya belgisi
/// bo'lib ishlaydi: yangi rasm yangi kalit oladi.
/// </param>
public sealed record RosterCandidate(Guid EmployeeId, string FullName, string? PhotoKey);

/// <summary>
/// HR ekranlari uchun bitta qator: xodim × qurilma — kerakli holat va
/// oxirgi ma'lum holat BIRGA.
/// </summary>
/// <remarks>
/// <para>
/// Ikkalasi bitta so'rovda keladi, chunki holat ularning FARQidan
/// hisoblanadi (<c>EnrollmentState</c>). Alohida o'qilsa, ekran «yozilgan»
/// deb turgan xodim aslida rasmi almashgan bo'lib chiqardi.
/// </para>
/// <para>
/// <see cref="OnRoster"/> — bugun qurilmada bo'lishi kerakmi. Ishdan
/// ketgan odam uchun <c>false</c>, lekin qatori qoladi: agent uni
/// qurilmadan olib tashlaguncha HR buni ko'rib turishi kerak.
/// </para>
/// </remarks>
/// <param name="DeviceId">Qurilma.</param>
/// <param name="DeviceName">Qurilma nomi.</param>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="EmployeeName">Xodim F.I.Sh.</param>
/// <param name="PhotoKey">Rasm kaliti yoki <c>null</c>.</param>
/// <param name="OnRoster">Bugun ro'yxatda bormi.</param>
/// <param name="PersonNo">Ajratilgan shaxs raqami.</param>
/// <param name="Status">Agent oxirgi marta nima degan; qator yo'q bo'lsa <c>null</c>.</param>
/// <param name="AppliedVersion">Agent yozgan belgi.</param>
/// <param name="ForceVersion">«Qayta yozish» necha marta bosilgan.</param>
/// <param name="Error">Qurilmaning xato matni.</param>
/// <param name="SyncedAt">Oxirgi yozuv lahzasi.</param>
public sealed record EnrollmentRow(
    Guid DeviceId,
    string DeviceName,
    Guid EmployeeId,
    string EmployeeName,
    string? PhotoKey,
    bool OnRoster,
    string? PersonNo,
    DeviceEnrollmentStatus? Status,
    string? AppliedVersion,
    int ForceVersion,
    string? Error,
    DateTimeOffset? SyncedAt);
