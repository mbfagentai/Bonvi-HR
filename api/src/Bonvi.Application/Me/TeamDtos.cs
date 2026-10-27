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
/// <para>
/// ⚠️ <see cref="Phone"/> va <see cref="HasPhoto"/> — «jonli» ma'lumot, faqat BUGUN qamrovdagi
/// odamda. O'tgan kun ochilganda u kuni jamoada bo'lib, keyin boshqa bo'limga ketgan odam
/// qatorida ikkalasi ham yo'q (<c>null</c> / <c>false</c>): boshliq endi uniki bo'lmagan odamning
/// raqamini va rasmini olmasin.
/// </para>
/// </remarks>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="FullName">To'liq ism.</param>
/// <param name="HasPhoto">Kartada rasm bormi — faqat bugun qamrovdagi odamda <c>true</c> bo'la oladi.</param>
/// <param name="PositionName">Shu sanadagi asosiy munosabatning lavozimi (ko'chirish hodisalaridan).</param>
/// <param name="DepartmentName">Shu sanadagi asosiy munosabatning bo'limi (ko'chirish hodisalaridan).</param>
/// <param name="ArrivedAt">Kunning birinchi kirishi (UTC); <c>null</c> — o'tish yo'q.</param>
/// <param name="LeftAt">Oxirgi chiqish (UTC); <c>null</c> — chiqish yo'q.</param>
/// <param name="Status">Holat — HR panelidagi bilan AYNAN bir qoida (<c>DashboardStatusRule</c>).</param>
/// <param name="LateMinutes">Faqat <see cref="AttendanceDashboardStatus.Late"/> da.</param>
/// <param name="LeftAuto">Jadvalsiz kunning avtomatik «ketdi»si — haqiqiy chiqish emas.</param>
/// <param name="Phone">
/// Asosiy telefon, «+998 90 123 45 67» ko'rinishida; raqam yo'q yoki odam bugun qamrovda
/// bo'lmasa <c>null</c>.
/// </param>
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
/// <remarks>
/// Telefon va rasm — panel qatoridagi kabi faqat BUGUN qamrovdagi odamda
/// (<see cref="TeamBoardItemDto"/> izohi).
/// </remarks>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="FullName">To'liq ism.</param>
/// <param name="HasPhoto">Kartada rasm bormi — faqat bugun qamrovdagi odamda <c>true</c> bo'la oladi.</param>
/// <param name="PositionName">
/// Lavozim — oyning qamrovdagi OXIRGI kunidagi (bugundan keyin emas), ko'chirish hodisalaridan:
/// boshqa bo'limga ketgan odamning yangi joyi bu yerda ko'rinmaydi.
/// </param>
/// <param name="DepartmentName">Bo'lim — lavozim bilan bir xil sana bo'yicha.</param>
/// <param name="Phone">Asosiy telefon, «+998 90 123 45 67»; raqam yo'q yoki odam bugun qamrovda bo'lmasa <c>null</c>.</param>
public sealed record TeamMemberDto(
    Guid EmployeeId,
    string FullName,
    bool HasPhoto,
    string? PositionName,
    string? DepartmentName,
    string? Phone);

/// <summary>
/// A'zoning oyi — <see cref="MyAttendanceDto"/> ning kunlari va jami, «hozir» siz.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega <c>current</c> yo'q.</b> U oyning emas, xodimning BUGUNGI holati (oxirgi belgi,
/// keyingi tugma) — o'tgan oyini ko'rish huquqi bugungi harakatini kuzatish huquqi emas.
/// </para>
/// <para>
/// ⚠️ Oyning qamrovdan TASHQARIDAGI kunlari (odam u kuni boshqa bo'limda bo'lgan) —
/// «ishlamagan» kunning o'zi: holat <c>NotEmployed</c>, sonlar 0, vaqtlar <c>null</c>,
/// belgilar yo'q; jami faqat qamrovdagi kunlardan (<see cref="TimesheetTotalsDto.Of"/>).
/// </para>
/// </remarks>
/// <param name="Year">Yil.</param>
/// <param name="Month">Oy (1–12).</param>
/// <param name="Days">Oyning har bir kuni — katak va belgilar bilan (<see cref="MyAttendanceDto.Days"/> shakli).</param>
/// <param name="Totals">Qamrovdagi kunlarning jami — HR tabelidagi bilan bir xil hisob.</param>
public sealed record TeamMemberMonthDto(
    int Year,
    int Month,
    IReadOnlyList<MyAttendanceDayDto> Days,
    TimesheetTotalsDto Totals);

/// <summary>A'zoning oyi — <c>GET /api/me/team/members/{employeeId}/attendance</c> (SPEC-jamoam §C3).</summary>
/// <param name="Employee">Kim.</param>
/// <param name="Attendance">A'zo o'z ilovasida ko'radigan oy — qamrovdagi kunlari bilan, «hozir» siz.</param>
public sealed record TeamMemberAttendanceDto(TeamMemberDto Employee, TeamMemberMonthDto Attendance);
