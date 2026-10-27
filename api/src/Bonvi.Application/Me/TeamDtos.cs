using Bonvi.Application.Attendance.Dtos;

namespace Bonvi.Application.Me;

/// <summary>Qamrovdagi bitta bo'lim — ilovadagi bo'lim tanlagichi uchun.</summary>
/// <param name="Id">Bo'lim.</param>
/// <param name="Name">Nom.</param>
/// <param name="Path">To'liq yo'l: «7-Departament / Metan zavod / Plazma bo'limi».</param>
/// <param name="Level">Ierarxiyadagi chuqurlik: 0 — eng yuqori (butun daraxtda, qamrovda emas).</param>
/// <param name="ParentId">Ota bo'lim; qamrov ildizida u qamrovdan TASHQARIDA bo'lishi mumkin.</param>
public sealed record TeamDepartmentDto(Guid Id, string Name, string? Path, int Level, Guid? ParentId);

/// <summary>Boshliqning qamrovi — <c>GET /api/me/team</c> (SPEC-jamoam §C1).</summary>
/// <param name="Departments">Qamrovdagi barcha bo'limlar, daraxt tartibida tekislangan.</param>
public sealed record TeamScopeDto(IReadOnlyList<TeamDepartmentDto> Departments);

/// <summary>Jamoa panelidagi bitta xodim.</summary>
/// <remarks>
/// <para>
/// HR panelining qatori (<see cref="AttendanceDashboardItemDto"/>) dan hosil qilinadi, ustiga
/// telefon. ⚠️ Rasm KALITI yo'q — u ichki saqlash nomi; o'rniga <see cref="HasPhoto"/>, rasmning
/// o'zi <c>GET /api/me/team/members/{id}/photo</c> dan. JSHSHIR, pasport, tug'ilgan sana,
/// manzil, oklad, belgilar va qurilmalar holati ATAYLAB yo'q (§C2): bu eshik faqat davomatni
/// ko'rsatadi.
/// </para>
/// </remarks>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="FullName">To'liq ism.</param>
/// <param name="HasPhoto">Kartada rasm bormi.</param>
/// <param name="PositionName">Shu sanadagi asosiy munosabatning lavozimi.</param>
/// <param name="DepartmentName">Shu sanadagi asosiy munosabatning bo'limi.</param>
/// <param name="ArrivedAt">Kunning birinchi kirishi (UTC); <c>null</c> — o'tish yo'q.</param>
/// <param name="LeftAt">Oxirgi chiqish (UTC); <c>null</c> — chiqish yo'q.</param>
/// <param name="Status">Holat — HR panelidagi bilan AYNAN bir qoida (<c>DashboardStatusRule</c>).</param>
/// <param name="LateMinutes">Faqat <see cref="AttendanceDashboardStatus.Late"/> da.</param>
/// <param name="LeftAuto">Jadvalsiz kunning avtomatik «ketdi»si — haqiqiy chiqish emas.</param>
/// <param name="Phone">Asosiy telefon, «+998 90 123 45 67» ko'rinishida; yo'q bo'lsa <c>null</c>.</param>
public sealed record TeamBoardItemDto(
    Guid EmployeeId,
    string FullName,
    bool HasPhoto,
    string? PositionName,
    string? DepartmentName,
    DateTimeOffset? ArrivedAt,
    DateTimeOffset? LeftAt,
    AttendanceDashboardStatus Status,
    int? LateMinutes,
    bool LeftAuto,
    string? Phone);

/// <summary>Jamoa paneli — <c>GET /api/me/team/board</c> (SPEC-jamoam §C2).</summary>
/// <remarks>
/// HR panelining (<see cref="AttendanceDashboardDto"/>) o'sha hisobi, boshliq qamrovida —
/// ikki xil «jami» ham o'sha ma'noda. Qurilmalar holati (<c>deviceHealth</c>) yo'q: u HR ning
/// ishi.
/// </remarks>
/// <param name="Date">Qaysi Toshkent sanasi.</param>
/// <param name="IsToday">Sana bugunmi.</param>
/// <param name="AsOf">Server holatlarni hisoblagan lahza — «yangilandi HH:mm».</param>
/// <param name="Total">To'plamdagi odamlar — qidiruv va holat filtridan oldin.</param>
/// <param name="Counts">Shu to'plam holatlar bo'yicha.</param>
/// <param name="Items">Shu sahifadagi xodimlar.</param>
/// <param name="Page">Sahifa.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
/// <param name="TotalItems">Hamma filtrdan keyingi qatorlar.</param>
/// <param name="PageCount">Sahifalar soni.</param>
public sealed record TeamBoardDto(
    DateOnly Date,
    bool IsToday,
    DateTimeOffset AsOf,
    int Total,
    AttendanceDashboardCountsDto Counts,
    IReadOnlyList<TeamBoardItemDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int PageCount);

/// <summary>Jamoa a'zosi — oy ekranining sarlavhasi.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="FullName">To'liq ism.</param>
/// <param name="HasPhoto">Kartada rasm bormi.</param>
/// <param name="PositionName">Lavozim — bugungi (o'tgan oy so'ralsa, o'sha oyning oxirgi kunidagi).</param>
/// <param name="DepartmentName">Bo'lim — lavozim bilan bir xil sana bo'yicha.</param>
/// <param name="Phone">Asosiy telefon, «+998 90 123 45 67»; yo'q bo'lsa <c>null</c>.</param>
public sealed record TeamMemberDto(
    Guid EmployeeId,
    string FullName,
    bool HasPhoto,
    string? PositionName,
    string? DepartmentName,
    string? Phone);

/// <summary>A'zoning oyi — <c>GET /api/me/team/members/{employeeId}/attendance</c> (SPEC-jamoam §C3).</summary>
/// <param name="Employee">Kim.</param>
/// <param name="Attendance"><c>/api/me/attendance</c> ning AYNAN o'zi — a'zo o'z ilovasida ko'radigan oy.</param>
public sealed record TeamMemberAttendanceDto(TeamMemberDto Employee, MyAttendanceDto Attendance);
