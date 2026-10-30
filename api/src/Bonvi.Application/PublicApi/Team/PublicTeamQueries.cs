using System.Globalization;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Queries;
using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Application.Common.Photos;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Photos;
using Bonvi.Application.Me;
using Bonvi.Application.PublicApi.Abstractions;
using Bonvi.Application.PublicApi.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.PublicApi.Team;

/*
  ⭐ Bo'lim davomati API'si — bo'lim rahbarining boti uchun (egasi, 2026-10-30: Rispetto zavodi
  rahbari o'z botida xodimlarining keldi-ketdisini va telefonlarini ko'rmoqchi).

  Ma'lumot xodim ilovasidagi «Jamoa» tabi bilan AYNAN bir xil va o'sha qurilish bloklaridan
  (`GetDashboardAsync`, `ITeamReadModel`, `GetMyAttendanceQueryHandler.MonthAsync`) — faqat
  qamrovning ildizi boshliqlikdan emas, kalitning bo'limidan keladi (`TeamScope.OfRoot`).
  «Jamoa» handlerlari ataylab o'zgartirilmadi: ular prod'da ishlab turibdi.
*/

/// <summary>So'rov qilayotgan bo'lim kaliti (claim'lardan).</summary>
/// <param name="KeyId">Kalit.</param>
/// <param name="DepartmentId">Kalitning bo'limi.</param>
public sealed record PublicTeamKey(Guid KeyId, Guid DepartmentId);

/// <summary>Kalitning bo'limidan qamrov — har so'rovda bazadan (bo'lim daraxti o'zgarsa darhol amalda).</summary>
public sealed class PublicTeamAccess
{
    private readonly IDepartmentRepository _departments;

    public PublicTeamAccess(IDepartmentRepository departments) => _departments = departments;

    public async Task<Result<TeamScope>> ResolveAsync(PublicTeamKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        var departments = await _departments.ListAsync(onlyActive: false, cancellationToken).ConfigureAwait(false);
        var scope = TeamScope.OfRoot(key.DepartmentId, departments);

        // Bo'lim o'chirilgan — kalit hech narsani ko'rmaydi (bekor qilinmagan bo'lsa ham)
        return scope.IsHead ? scope : PublicApiErrors.DepartmentNotFound;
    }
}

// ════════════════════════════════════════════════════════════ qamrov

/// <summary><c>GET /team</c> — kalit ko'radigan bo'limlar (daraxt tartibida).</summary>
public sealed class GetPublicTeamQueryHandler
{
    private readonly PublicTeamAccess _access;

    public GetPublicTeamQueryHandler(PublicTeamAccess access) => _access = access;

    public async Task<Result<TeamScopeDto>> HandleAsync(PublicTeamKey key, CancellationToken cancellationToken = default)
    {
        var scope = await _access.ResolveAsync(key, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return scope.Error;
        }

        return new TeamScopeDto(
        [
            .. scope.Value.Departments.Select(department => new TeamDepartmentDto(
                department.Id,
                department.Name,
                department.Path,
                department.Level,
                department.ParentId)),
        ]);
    }
}

// ════════════════════════════════════════════════════════════ taxta

/// <summary><c>GET /team/board</c> so'rovi — «Jamoa» taxtasi bilan bir xil parametrlar.</summary>
public sealed record GetPublicTeamBoardQuery(
    DateOnly? Date = null,
    Guid? DepartmentId = null,
    AttendanceDashboardStatus? Status = null,
    string? Search = null,
    int? Page = null,
    int? PageSize = null);

/// <summary>Kunlik taxta: kim keldi, qachon, kim kechikdi/kelmadi — telefoni bilan.</summary>
public sealed class GetPublicTeamBoardQueryHandler
{
    private static readonly DateOnly MinDate = new(AttendanceQueryParsing.MinYear, 1, 1);

    private readonly PublicTeamAccess _access;
    private readonly IAttendanceReadModel _readModel;
    private readonly IAttendanceComputationSource _employments;
    private readonly ITeamReadModel _team;
    private readonly IClock _clock;

    public GetPublicTeamBoardQueryHandler(
        PublicTeamAccess access,
        IAttendanceReadModel readModel,
        IAttendanceComputationSource employments,
        ITeamReadModel team,
        IClock clock)
    {
        _access = access;
        _readModel = readModel;
        _employments = employments;
        _team = team;
        _clock = clock;
    }

    public async Task<Result<TeamBoardDto>> HandleAsync(
        PublicTeamKey key,
        GetPublicTeamBoardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var resolved = await _access.ResolveAsync(key, cancellationToken).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            return resolved.Error;
        }

        var scope = resolved.Value;
        var today = _clock.Today;

        if (query.Date is { } requested && (requested < MinDate || requested > today))
        {
            return TeamErrors.DateInvalid;
        }

        if (query.DepartmentId is { } departmentId && !scope.Contains(departmentId))
        {
            return PublicApiErrors.DepartmentNotFound;
        }

        var page = AttendanceQueryParsing.Page(query.Page);
        var pageSize = AttendanceQueryParsing.PageSize(query.PageSize ?? AttendanceQueryParsing.DashboardPageSize);

        var filter = GetAttendanceDashboardQueryHandler.BuildFilter(
                new GetAttendanceDashboardQuery(Date: query.Date, Search: query.Search, Status: query.Status),
                today,
                page,
                pageSize) with
        {
            DepartmentRoots = query.DepartmentId is { } narrowed ? [narrowed] : scope.Roots,
            // Qidiruv — ism va telefon, JSHSHIR/pasport emas (tashqi API qoidasi, «Jamoa» bilan bir xil)
            SearchNameAndPhoneOnly = true,
        };

        var found = await _readModel.GetDashboardAsync(filter, cancellationToken).ConfigureAwait(false);

        var pageIds = found.Items.Select(item => item.EmployeeId).ToList();
        var employments = await _employments.LoadEmploymentsAsync(pageIds, cancellationToken).ConfigureAwait(false);

        // Telefon va rasm faqat BUGUN qamrovda bo'lganlarga — o'tgan kun taxtasidagi ketgan xodimga emas
        var inScopeToday = pageIds
            .Where(id => scope.CoversOn(employments.GetValueOrDefault(id) ?? [], today))
            .ToHashSet();

        var phones = await _team.PhonesAsync(inScopeToday, cancellationToken).ConfigureAwait(false);

        return new TeamBoardDto(
            found.Date,
            found.IsToday,
            found.AsOf,
            found.Total,
            found.Counts,
            [
                .. found.Items.Select(item => new TeamBoardItemDto(
                    item.EmployeeId,
                    item.FullName,
                    inScopeToday.Contains(item.EmployeeId) && item.PhotoKey is not null,
                    item.PositionName,
                    item.DepartmentName,
                    item.ArrivedAt,
                    item.LeftAt,
                    item.Status,
                    item.LateMinutes,
                    item.LeftAuto,
                    phones.TryGetValue(item.EmployeeId, out var phone) ? phone.Formatted : null)),
            ],
            page,
            pageSize,
            found.TotalItems,
            AttendanceQueryParsing.PageCount(found.TotalItems, pageSize));
    }
}

// ════════════════════════════════════════════════════════════ xodimning oyi

/// <summary>Bitta xodimning oylik tabeli va o'tishlari — «Jamoa» kartasi bilan bir xil.</summary>
public sealed class GetPublicTeamMemberAttendanceQueryHandler
{
    private readonly PublicTeamAccess _access;
    private readonly IAttendanceComputationSource _employments;
    private readonly IEmployeeAppReadModel _profiles;
    private readonly ITeamReadModel _team;
    private readonly GetMyAttendanceQueryHandler _month;
    private readonly IClock _clock;

    public GetPublicTeamMemberAttendanceQueryHandler(
        PublicTeamAccess access,
        IAttendanceComputationSource employments,
        IEmployeeAppReadModel profiles,
        ITeamReadModel team,
        GetMyAttendanceQueryHandler month,
        IClock clock)
    {
        _access = access;
        _employments = employments;
        _profiles = profiles;
        _team = team;
        _month = month;
        _clock = clock;
    }

    public async Task<Result<TeamMemberAttendanceDto>> HandleAsync(
        PublicTeamKey key,
        GetTeamMemberAttendanceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var resolved = await _access.ResolveAsync(key, cancellationToken).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            return resolved.Error;
        }

        var valid = AttendanceQueryParsing.Month(query.Year, query.Month);
        if (valid.IsFailure)
        {
            return valid.Error;
        }

        var today = _clock.Today;
        var monthStart = new DateOnly(query.Year, query.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        if (monthStart > today)
        {
            return TeamErrors.FutureMonth;
        }

        var scope = resolved.Value;
        var loaded = await _employments.LoadEmploymentsAsync([query.EmployeeId], cancellationToken).ConfigureAwait(false);
        var employments = loaded.GetValueOrDefault(query.EmployeeId) ?? [];

        // Faqat qamrovdagi kunlar — xodim bu bo'limga kelguncha yoki ketgandan keyingi kunlar yashirin
        var covered = scope.DaysCovered(employments, monthStart, monthEnd);
        var pastCovered = covered.Where(date => date <= today).ToList();

        if (pastCovered.Count == 0)
        {
            return PublicApiErrors.TeamMemberNotFound;
        }

        var profile = await _profiles.ProfileAsync(query.EmployeeId, today, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return PublicApiErrors.TeamMemberNotFound;
        }

        var headerDay = pastCovered.Max();
        var placement = PrimaryEmployment.Select(employments, headerDay) is { } primary
            ? EmploymentPlacement.On(primary, headerDay)
            : default;
        var names = await _team
            .PlacementNamesAsync(placement.DepartmentId, placement.PositionId, cancellationToken)
            .ConfigureAwait(false);

        var inScopeToday = scope.CoversOn(employments, today);
        var phones = inScopeToday
            ? await _team.PhonesAsync([query.EmployeeId], cancellationToken).ConfigureAwait(false)
            : new Dictionary<Guid, PhoneNumber>();

        var (days, _) = await _month
            .MonthAsync(query.EmployeeId, query.Year, query.Month, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<MyAttendanceDayDto> visible =
        [
            .. days.Select(day => covered.Contains(day.Day.Date) ? day : OutsideScope(day.Day.Date)),
        ];

        return new TeamMemberAttendanceDto(
            new TeamMemberDto(
                query.EmployeeId,
                profile.FullName,
                inScopeToday && profile.PhotoKey is not null,
                names.Position,
                names.Department,
                phones.TryGetValue(query.EmployeeId, out var phone) ? phone.Formatted : null),
            new TeamMemberMonthDto(
                query.Year,
                query.Month,
                visible,
                TimesheetTotalsDto.Of([.. visible.Select(day => day.Day)])));
    }

    private static MyAttendanceDayDto OutsideScope(DateOnly date)
        => new(
            new TimesheetCellDto(
                date,
                AttendanceDayStatus.NotEmployed,
                WorkedMinutes: 0,
                LateMinutes: 0,
                EarlyLeaveMinutes: 0,
                FirstInAt: null,
                LastOutAt: null,
                BreakTakenMinutes: 0,
                BreakDeductionMinutes: 0,
                PlanStartAt: null,
                PlanEndAt: null),
            []);
}

// ════════════════════════════════════════════════════════════ rasm

/// <summary>Xodim rasmi — faqat BUGUN qamrovdagi xodimniki («Jamoa» qoidasi).</summary>
public sealed class GetPublicTeamMemberPhotoQueryHandler
{
    private readonly PublicTeamAccess _access;
    private readonly IAttendanceComputationSource _employments;
    private readonly GetEmployeePhotoQueryHandler _photos;
    private readonly IClock _clock;

    public GetPublicTeamMemberPhotoQueryHandler(
        PublicTeamAccess access,
        IAttendanceComputationSource employments,
        GetEmployeePhotoQueryHandler photos,
        IClock clock)
    {
        _access = access;
        _employments = employments;
        _photos = photos;
        _clock = clock;
    }

    public async Task<Result<PhotoContent>> HandleAsync(
        PublicTeamKey key,
        Guid employeeId,
        string? knownVersion,
        CancellationToken cancellationToken = default)
    {
        var resolved = await _access.ResolveAsync(key, cancellationToken).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            return resolved.Error;
        }

        var employments = await _employments.LoadEmploymentsAsync([employeeId], cancellationToken).ConfigureAwait(false);
        if (!resolved.Value.CoversOn(employments.GetValueOrDefault(employeeId) ?? [], _clock.Today))
        {
            return PublicApiErrors.TeamMemberNotFound;
        }

        var photo = await _photos
            .HandleAsync(new GetEmployeePhotoQuery(employeeId, knownVersion), cancellationToken)
            .ConfigureAwait(false);

        return photo.IsFailure && photo.Error.Code == EmployeeErrors.NotFound.Code
            ? PublicApiErrors.TeamMemberNotFound
            : photo;
    }
}

// ════════════════════════════════════════════════════════════ o'tishlar oqimi

/// <summary>Oqimdagi bitta o'tish — bot «Karimov 08:52 da keldi» deb yozishi uchun.</summary>
/// <param name="Id">O'tish — takroriy xabarni ushlash uchun kalit.</param>
/// <param name="EmployeeId">Xodim (<c>/team/members/{id}</c> uchun).</param>
/// <param name="FullName">F.I.Sh.</param>
/// <param name="PositionName">O'sha kundagi lavozim.</param>
/// <param name="DepartmentName">O'sha kundagi bo'lim.</param>
/// <param name="At">O'tish lahzasi (UTC; Toshkent +05:00).</param>
/// <param name="Date">Toshkent kalendar kuni.</param>
/// <param name="Direction">Kirish yoki chiqish — hisob-kitobdagi yo'nalish (noma'lum terminalda navbat bilan).</param>
/// <param name="Source">Terminal yoki telefon (<c>Terminal</c> | <c>Mobile</c>).</param>
public sealed record PublicTeamPassDto(
    Guid Id,
    Guid EmployeeId,
    string FullName,
    string? PositionName,
    string? DepartmentName,
    DateTimeOffset At,
    DateOnly Date,
    PassDirection Direction,
    MarkSource Source);

/// <summary>Oqim sahifasi.</summary>
/// <param name="Items">Yangi o'tishlar, kelish tartibida.</param>
/// <param name="Next">Keyingi so'rovga <c>after</c> qilib beriladigan kursor (bo'sh sahifada ham yangilanadi).</param>
/// <param name="HasMore">Yana o'tishlar bor — darhol qayta so'rash mumkin.</param>
public sealed record PublicTeamPassesDto(IReadOnlyList<PublicTeamPassDto> Items, string Next, bool HasMore);

/// <summary>
/// <c>GET /team/passes?after=</c> — kursordan keyin kelgan, kalitning bo'limidagi o'tishlar.
/// </summary>
/// <remarks>
/// <para>
/// Bot har 1–2 daqiqada so'raydi va javobdagi <c>next</c> ni saqlaydi. Kursor — serverga KELISH
/// tartibi: agent uzilib qolib, o'tishlarni keyin yuborsa ham bot ularni oladi (kechikkan
/// o'tishning <c>at</c> i eski bo'ladi — bot buni hisobga oladi).
/// </para>
/// <para>
/// Kursorsiz birinchi so'rov — bugungi (Toshkent) kun boshidan. Bir so'rovda ko'pi bilan
/// <see cref="BatchSize"/> qator ko'riladi; boshqa bo'limlarniki tashlanadi, lekin kursor ulardan
/// ham o'tadi — bot bir joyda aylanib qolmaydi.
/// </para>
/// </remarks>
public sealed class GetPublicTeamPassesQueryHandler
{
    /// <summary>Bir so'rovda ko'rib chiqiladigan qatorlar.</summary>
    public const int BatchSize = 500;

    private readonly PublicTeamAccess _access;
    private readonly IPublicTeamReadModel _passes;
    private readonly IAttendanceComputationSource _employments;
    private readonly ITeamReadModel _team;
    private readonly IClock _clock;

    public GetPublicTeamPassesQueryHandler(
        PublicTeamAccess access,
        IPublicTeamReadModel passes,
        IAttendanceComputationSource employments,
        ITeamReadModel team,
        IClock clock)
    {
        _access = access;
        _passes = passes;
        _employments = employments;
        _team = team;
        _clock = clock;
    }

    public async Task<Result<PublicTeamPassesDto>> HandleAsync(
        PublicTeamKey key,
        string? after,
        CancellationToken cancellationToken = default)
    {
        var resolved = await _access.ResolveAsync(key, cancellationToken).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            return resolved.Error;
        }

        var scope = resolved.Value;

        DateTimeOffset afterAt;
        var inclusive = false;

        if (string.IsNullOrWhiteSpace(after))
        {
            afterAt = new DateTimeOffset(_clock.Today.ToDateTime(TimeOnly.MinValue), BonviTime.TashkentOffset).ToUniversalTime();
            inclusive = true;
        }
        else if (!Cursor.TryRead(after, out afterAt))
        {
            return Error.Validation("tashqi.kursor_notogri", "«after» kursori noto'g'ri — javobdagi «next» ni o'zgartirmasdan bering.");
        }

        var batch = await _passes
            .PassesReceivedAfterAsync(afterAt, inclusive, BatchSize, cancellationToken)
            .ConfigureAwait(false);
        var rows = batch.Rows;

        // Bo'sh sahifada kursor joyida qoladi; birinchi so'rov bo'sh bo'lsa ham keyingisi kun boshidan emas
        var next = Cursor.Write(rows.Count == 0 ? afterAt : rows[^1].ReceivedAt);

        var employeeIds = rows.Select(row => row.EmployeeId).Distinct().ToList();
        var employments = await _employments.LoadEmploymentsAsync(employeeIds, cancellationToken).ConfigureAwait(false);

        var mine = rows
            .Where(row => scope.CoversOn(employments.GetValueOrDefault(row.EmployeeId) ?? [], row.LocalDate))
            .ToList();

        var directions = await _passes
            .EffectiveDirectionsAsync([.. mine.Select(row => (row.EmployeeId, row.LocalDate)).Distinct()], cancellationToken)
            .ConfigureAwait(false);

        // Lavozim va bo'lim o'sha KUNdagi asosiy ish joyidan — juftlar bir martadan so'raladi
        var placements = new Dictionary<(Guid? DepartmentId, Guid? PositionId), TeamPlacementNames>();
        var items = new List<PublicTeamPassDto>(mine.Count);

        foreach (var row in mine)
        {
            var placement = PrimaryEmployment.Select(employments.GetValueOrDefault(row.EmployeeId) ?? [], row.LocalDate) is { } primary
                ? EmploymentPlacement.On(primary, row.LocalDate)
                : default;

            var pair = (placement.DepartmentId, placement.PositionId);
            if (!placements.TryGetValue(pair, out var names))
            {
                names = await _team.PlacementNamesAsync(pair.DepartmentId, pair.PositionId, cancellationToken).ConfigureAwait(false);
                placements[pair] = names;
            }

            items.Add(new PublicTeamPassDto(
                row.Id,
                row.EmployeeId,
                row.FullName,
                names.Position,
                names.Department,
                row.OccurredAt,
                row.LocalDate,
                directions.GetValueOrDefault(row.Id, row.Direction),
                row.Method == PassMethod.Mobile ? MarkSource.Mobile : MarkSource.Terminal));
        }

        return new PublicTeamPassesDto(items, next, batch.HasMore);
    }

    /// <summary>
    /// Kursor: <c>received_at</c> ning UTC tick'lari, «t» bilan — ochiq, lekin bot uni o'zgartirmaydi.
    /// Tick — bazaning mikrosekund aniqligini yo'qotmaydi (millisekund kursor bir lahzadagi o'tishni
    /// ikki marta berardi).
    /// </summary>
    internal static class Cursor
    {
        public static string Write(DateTimeOffset at)
            => string.Create(CultureInfo.InvariantCulture, $"t{at.UtcTicks}");

        public static bool TryRead(string raw, out DateTimeOffset at)
        {
            at = default;
            var trimmed = raw.Trim();

            if (trimmed.Length < 2
                || trimmed[0] != 't'
                || !long.TryParse(trimmed.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                || ticks < DateTimeOffset.UnixEpoch.UtcTicks
                || ticks > DateTimeOffset.MaxValue.UtcTicks)
            {
                return false;
            }

            at = new DateTimeOffset(ticks, TimeSpan.Zero);
            return true;
        }
    }
}
