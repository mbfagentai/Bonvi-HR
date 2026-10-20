using System.Linq.Expressions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Computation;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>
/// <see cref="IAttendanceReadModel"/> ning EF Core orqali amalga oshirilishi.
/// </summary>
/// <remarks>
/// <para>
/// <b>So'rov shakli hamma bo'limda bir xil:</b> filtr, sanash, saralash va
/// sahifalash BAZADA; sahifa olingandan keyin nomlar (xodim, qurilma, bo'lim,
/// lavozim) SAHIFADAGI identifikatorlar bo'yicha bittadan so'rov bilan
/// yig'iladi. So'rovlar soni sahifa hajmiga bog'liq EMAS — 25 qatorda ham,
/// 20 000 qatorlik eksportda ham bir xil (N+1 yo'q).
/// </para>
/// <para>
/// <b>Nomlar o'chirilganlari bilan birga</b> (<c>IgnoreQueryFilters</c>):
/// kun yozuvidagi bo'lim SURAT, va o'tgan yilning tabeli bo'lim keyinroq
/// o'chirilgani uchun bo'sh ustun bilan chiqmasligi kerak.
/// </para>
/// <para>
/// <b>Vaqt.</b> Bazada UTC. «Qaysi kun» — Toshkent sanasi
/// (<c>local_date</c> ustuni yoki <see cref="BonviTime"/>), «qaysi lahza» —
/// <see cref="BonviTime.TashkentOffset"/> bilan UTC ga o'girilgan chegara.
/// Ikkinchi siljish manbai yo'q.
/// </para>
/// <para>
/// ⭐ <b>§5.1 va §5.2 bu yerda QAYTA YOZILMAGAN.</b> Asosiy munosabat
/// (<see cref="PrimaryEmployment"/>), jadval ustuvorligi
/// (<see cref="ScheduleResolver"/>) va yozuvi yo'q katakning holati — tungi
/// qayta hisoblash ishlatadigan AYNAN o'sha kod. Yozuvi yo'q katak
/// <see cref="AttendanceDayBuilder"/> ni NOL o'tish bilan yurgizib olinadi:
/// saqlangan katak va hosil qilingan katak bitta funksiyadan chiqadi, ya'ni
/// durang qoidasi, bo'limlar daraxti yoki <c>IsActive</c> bo'yicha hech
/// qachon bir-biriga zid gapira olmaydi. Hisoblash kun yozuvini qayta
/// YOZMAYDI — bu yerda faqat o'qiladi.
/// </para>
/// </remarks>
public sealed partial class AttendanceReadModel : IAttendanceReadModel
{
    private readonly AppDbContext _context;
    private readonly IDepartmentRepository _departments;
    private readonly IClock _clock;

    public AttendanceReadModel(AppDbContext context, IDepartmentRepository departments, IClock clock)
    {
        _context = context;
        _departments = departments;
        _clock = clock;
    }

    // ════════════════════════════════════════════════════════════
    //  1. O'tishlar jurnali
    // ════════════════════════════════════════════════════════════

    /// <summary>Jurnal qatorining bazadan olinadigan qismi — nomlarsiz.</summary>
    private sealed record PassRow(
        Guid Id,
        DateTimeOffset OccurredAt,
        DateOnly LocalDate,
        DateTimeOffset ReceivedAt,
        int ClockSkewSeconds,
        int? ClockOffsetSeconds,
        bool OffsetAssumed,
        PassDirection Direction,
        DirectionSource DirectionSource,
        PassMethod Method,
        Guid DeviceId,
        AttendanceIdentityProvider IdentityProvider,
        string RawPersonId,
        Guid? EmployeeId,
        string? PhotoKey,
        DateTimeOffset? PhotoPurgedAt);

    /// <summary>
    /// Ro'yxat ham, bitta o'tish ham SHU proyeksiyadan — ikkalasi bir xil ko'rinishi uchun.
    /// </summary>
    private static readonly Expression<Func<AttendancePass, PassRow>> PassRowProjection = pass => new PassRow(
        pass.Id,
        pass.OccurredAt,
        pass.LocalDate,
        pass.ReceivedAt,
        pass.ClockSkewSeconds,
        pass.ClockOffsetSeconds,
        pass.OffsetAssumed,
        pass.Direction,
        pass.DirectionSource,
        pass.Method,
        pass.DeviceId,
        pass.IdentityProvider,
        pass.RawPersonId,
        pass.ResolvedEmployeeId,
        pass.PhotoKey,
        pass.PhotoPurgedAt);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Davr <c>local_date</c> bo'yicha kesiladi va tartib
    /// <c>(local_date, occurred_at, id)</c>: <c>local_date</c> lahzaning
    /// Toshkent sanasi, ya'ni tartib aynan vaqt bo'yicha tartib bilan bir xil,
    /// lekin <c>ix_attendance_passes_local_date</c> indeksiga tushadi —
    /// faqat <c>occurred_at</c> bo'yicha alohida indeks yo'q va 2 mln qatorni
    /// har sahifa uchun saralash kerak bo'lardi.
    /// </para>
    /// <para>
    /// Firma/bo'lim filtri — xodimning shu DAVRDA munosabati bo'lganmi
    /// degan savol: o'tishning o'zida tuzilma yo'q (u qurilma aytganini
    /// saqlaydi), bo'lim esa oy ichida o'zgarishi mumkin.
    /// </para>
    /// </remarks>
    public async Task<AttendancePage<AttendancePassDto>> SearchPassesAsync(
        PassSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var passes = _context.AttendancePasses
            .AsNoTracking()
            .Where(pass => pass.LocalDate >= filter.From && pass.LocalDate <= filter.To);

        if (filter.EmployeeId is { } employeeId)
        {
            passes = passes.Where(pass => pass.ResolvedEmployeeId == employeeId);
        }

        if (filter.DeviceId is { } deviceId)
        {
            passes = passes.Where(pass => pass.DeviceId == deviceId);
        }

        if (filter.Direction is { } direction)
        {
            passes = passes.Where(pass => pass.Direction == direction);
        }

        if (filter.Method is { } method)
        {
            passes = passes.Where(pass => pass.Method == method);
        }

        if (filter.Matched is { } matched)
        {
            passes = matched
                ? passes.Where(pass => pass.ResolvedEmployeeId != null)
                : passes.Where(pass => pass.ResolvedEmployeeId == null);
        }

        if (filter.LegalEntityId is not null || filter.DepartmentId is not null)
        {
            var employments = _context.Employments
                .AsNoTracking()
                .Where(employment =>
                    employment.HireDate <= filter.To &&
                    (employment.TerminationDate == null || employment.TerminationDate >= filter.From));

            employments = await ApplyEmploymentOrgFilterAsync(
                    employments,
                    filter.LegalEntityId,
                    filter.DepartmentId,
                    positionId: null,
                    cancellationToken)
                .ConfigureAwait(false);

            var employeeIds = employments.Select(employment => employment.EmployeeId);

            passes = passes.Where(pass =>
                pass.ResolvedEmployeeId != null && employeeIds.Contains(pass.ResolvedEmployeeId.Value));
        }

        if (filter.Search is { } search)
        {
            // Bitta qidiruv maydoni ikki savolga javob beradi: «Каримов qachon
            // o'tdi» (xodim) va «00731 kimligini bilmayman» (qurilma raqami).
            // Xodim sharti barcha ro'yxatlardagi bilan AYNAN bir xil
            // (`EmployeeSearchQuery`), raqam esa ichida qidiriladi.
            var matching = EmployeeSearchQuery.MatchingIds(_context, search);
            var pattern = SearchPattern.For(search);

            passes = passes.Where(pass =>
                (pass.ResolvedEmployeeId != null && matching.Contains(pass.ResolvedEmployeeId.Value)) ||
                EF.Functions.ILike(pass.RawPersonId, pattern, SearchPattern.Escape));
        }

        var total = await passes.CountAsync(cancellationToken).ConfigureAwait(false);

        var ordered = filter.Descending
            ? passes
                .OrderByDescending(pass => pass.LocalDate)
                .ThenByDescending(pass => pass.OccurredAt)
                .ThenByDescending(pass => pass.Id)
            : passes
                .OrderBy(pass => pass.LocalDate)
                .ThenBy(pass => pass.OccurredAt)
                .ThenBy(pass => pass.Id);

        var rows = await ordered
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .Select(PassRowProjection)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = await ToPassDtosAsync(rows, cancellationToken).ConfigureAwait(false);

        return new AttendancePage<AttendancePassDto>(items, total);
    }

    /// <inheritdoc />
    public async Task<AttendancePassDto?> FindPassAsync(
        Guid passId,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.AttendancePasses
            .AsNoTracking()
            .Where(pass => pass.Id == passId)
            .Select(PassRowProjection)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var items = await ToPassDtosAsync([row], cancellationToken).ConfigureAwait(false);
        return items[0];
    }

    /// <summary>
    /// Sahifa qatorlarini nomlar bilan to'ldiradi — sahifa hajmidan qat'i nazar to'rtta so'rov.
    /// </summary>
    /// <remarks>
    /// Bo'lim va lavozim o'tish SANASIDAGI asosiy munosabatdan (§5.1,
    /// <see cref="PrimaryEmployment.Select"/>) olinadi: sahifadagi xodimlarning
    /// munosabatlari bitta so'rovda yuklanib, har bir qatorga xotirada tanlanadi.
    /// </remarks>
    private async Task<IReadOnlyList<AttendancePassDto>> ToPassDtosAsync(
        List<PassRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var employeeIds = rows
            .Where(row => row.EmployeeId is not null)
            .Select(row => row.EmployeeId!.Value)
            .Distinct()
            .ToList();

        var cards = await EmployeeCardsAsync(employeeIds, cancellationToken).ConfigureAwait(false);
        var devices = await DeviceLabelsAsync(rows.Select(row => row.DeviceId), cancellationToken).ConfigureAwait(false);

        var employments = await EmploymentsAsync(employeeIds, cancellationToken).ConfigureAwait(false);

        var primaries = rows
            .Where(row => row.EmployeeId is not null)
            .Select(row => (EmployeeId: row.EmployeeId!.Value, row.LocalDate))
            .Distinct()
            .ToDictionary(
                key => key,
                key => PrimaryEmployment.Select(
                    employments.GetValueOrDefault(key.EmployeeId) ?? [],
                    key.LocalDate));

        var names = await CatalogNamesAsync(
                primaries.Values.Select(employment => employment?.DepartmentId),
                primaries.Values.Select(employment => employment?.PositionId),
                [],
                cancellationToken)
            .ConfigureAwait(false);

        var lateness = await LateMinutesByPassAsync(rows, employments, cancellationToken).ConfigureAwait(false);

        var evidence = await MobileEvidenceAsync(rows, cancellationToken).ConfigureAwait(false);

        var inferred = await InferredDirectionsAsync(rows, cancellationToken).ConfigureAwait(false);

        return
        [
            .. rows.Select(row =>
            {
                EmployeeCard? card = null;
                Employment? employment = null;

                if (row.EmployeeId is { } id)
                {
                    cards.TryGetValue(id, out card);
                    employment = primaries[(id, row.LocalDate)];
                }

                // Soat hukmi `AttendancePass.EffectiveClockSkewSeconds` qoidasi
                // bilan: agent o'lchagan og'ish bo'lsa — u, bo'lmasa qabul
                // ayirmasi. Ikkinchisi internet uzilishidan keyin kelgan har
                // bir o'tishni «soat adashgan» qilib ko'rsatardi.
                var skew = row.ClockOffsetSeconds is { } offset ? -offset : row.ClockSkewSeconds;

                // Yo'nalishi berilmagan terminal — navbat bilan (kunning 1-o'tishi kirish…), faqat ko'rsatish.
                var (direction, directionSource) = row.Direction == PassDirection.Unknown && inferred.TryGetValue(row.Id, out var guess)
                    ? (guess, DirectionSource.Alternation)
                    : (row.Direction, row.DirectionSource);

                return new AttendancePassDto(
                    row.Id,
                    row.OccurredAt,
                    row.LocalDate,
                    row.ReceivedAt,
                    skew,
                    Math.Abs(skew) > AttendanceRules.ClockSkewToleranceSeconds,
                    row.OffsetAssumed,
                    direction,
                    directionSource,
                    row.Method,
                    row.DeviceId,
                    devices.GetValueOrDefault(row.DeviceId)?.Name,
                    devices.GetValueOrDefault(row.DeviceId)?.Location,
                    devices.GetValueOrDefault(row.DeviceId)?.Provider,
                    row.IdentityProvider,
                    row.RawPersonId,
                    row.EmployeeId,
                    card?.FullName,
                    card?.PhotoKey,
                    names.Department(employment?.DepartmentId),
                    names.Position(employment?.PositionId),
                    row.PhotoKey,
                    row.PhotoPurgedAt,
                    lateness.TryGetValue(row.Id, out var lateMinutes) ? lateMinutes : null,
                    evidence.GetValueOrDefault(row.Id));
            }),
        ];
    }

    /// <summary>
    /// Sahifadagi «noma'lum» o'tishlarning navbat bilan aniqlangan yo'nalishi.
    /// </summary>
    /// <remarks>
    /// Hisob-kitob (`PassPairing`) bilan AYNAN bir qoida: xodimning o'sha kalendar
    /// kunidagi barcha o'tishlari olinadi va navbat shu kun ichida sanaladi. Bitta
    /// so'rov — sahifa hajmidan qat'i nazar. Tungi smenada (yarim tundan o'tadigan)
    /// kalendar kuni smena oynasi bilan farq qilishi mumkin — u holda tabeldagi juftlash
    /// ustun (bu yerda faqat ko'rsatish).
    /// </remarks>
    private async Task<Dictionary<Guid, PassDirection>> InferredDirectionsAsync(
        List<PassRow> rows,
        CancellationToken cancellationToken)
    {
        var keys = rows
            .Where(row => row.Direction == PassDirection.Unknown && row.EmployeeId is not null)
            .Select(row => (EmployeeId: row.EmployeeId!.Value, row.LocalDate))
            .ToHashSet();

        if (keys.Count == 0)
        {
            return [];
        }

        var employeeIds = keys.Select(key => key.EmployeeId).Distinct().ToList();
        var dates = keys.Select(key => key.LocalDate).Distinct().ToList();

        var passes = await _context.AttendancePasses
            .AsNoTracking()
            .Where(pass =>
                pass.ResolvedEmployeeId != null &&
                employeeIds.Contains(pass.ResolvedEmployeeId.Value) &&
                dates.Contains(pass.LocalDate))
            .Select(pass => new { pass.Id, pass.ResolvedEmployeeId, pass.OccurredAt, pass.Direction, pass.LocalDate })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new Dictionary<Guid, PassDirection>();

        foreach (var day in passes.GroupBy(pass => (EmployeeId: pass.ResolvedEmployeeId!.Value, pass.LocalDate)))
        {
            if (!keys.Contains(day.Key))
            {
                continue;
            }

            var directions = PassPairing.EffectiveDirections(
                day.Select(pass => new PassReading(pass.Id, pass.ResolvedEmployeeId, pass.OccurredAt, pass.Direction)));

            foreach (var (id, direction) in directions)
            {
                result[id] = direction;
            }
        }

        return result;
    }

    /// <summary>
    /// Sahifadagi TELEFON belgilarining dalili — o'tish → dalil (SPEC-xodim-ilovasi §F).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bitta so'rov (<c>ux_mobile_check_ins_pass</c> bo'yicha
    /// <c>WHERE pass_id = ANY(…)</c>) va faqat <c>method = Mobile</c> qatorlar
    /// uchun: terminal o'tishida koordinata umuman yo'q, sahifada telefon
    /// belgisi bo'lmasa so'rov yuborilmaydi ham.
    /// </para>
    /// <para>
    /// Obyektlar o'chirilganlari bilan (<c>IgnoreQueryFilters</c>): keyin
    /// yumshoq o'chirilgan obyekt belgining qayerda qabul qilinganini aytishda
    /// davom etishi kerak — xodim ilovasidagi bilan bir xil qaror.
    /// </para>
    /// </remarks>
    private async Task<Dictionary<Guid, MobilePassEvidenceDto>> MobileEvidenceAsync(
        List<PassRow> rows,
        CancellationToken cancellationToken)
    {
        var mobileIds = rows
            .Where(row => row.Method == PassMethod.Mobile)
            .Select(row => row.Id)
            .ToList();

        if (mobileIds.Count == 0)
        {
            return [];
        }

        return await _context.MobileCheckIns
            .AsNoTracking()
            .Where(checkIn => mobileIds.Contains(checkIn.PassId))
            .Join(
                _context.WorkSites.AsNoTracking().IgnoreQueryFilters(),
                checkIn => checkIn.WorkSiteId,
                site => site.Id,
                (checkIn, site) => new
                {
                    checkIn.PassId,
                    Evidence = new MobilePassEvidenceDto(
                        site.Id,
                        site.Name,
                        checkIn.SiteShape,
                        checkIn.DistanceMeters,
                        checkIn.SiteRadiusMeters,
                        checkIn.AccuracyMeters,
                        checkIn.Latitude,
                        checkIn.Longitude),
                })
            .ToDictionaryAsync(row => row.PassId, row => row.Evidence, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Kun yozuvining kechikish uchun kerakli qismi.</summary>
    private sealed record StoredFirstIn(Guid EmployeeId, DateOnly LocalDate, DateTimeOffset? FirstInAt, int LateMinutes);

    /// <summary>
    /// Sahifadagi qaysi o'tish kunning KECHIKKAN birinchi kirishi — o'tish → daqiqa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Birinchi kirish lahza bo'yicha topiladi.</b> Hisoblash
    /// <c>first_in_at</c> ga aynan birinchi kirish o'tishining <c>occurred_at</c>
    /// ini yozadi (yo'nalish qurilmadan bo'lsin yoki navbatdan taxmin qilingan).
    /// Shu sababli «xodim + lahza» teng bo'lsa, o'tish o'sha kunning birinchi
    /// kirishi — sana yoki yo'nalish bo'yicha qayta taxmin qilinmaydi.
    /// </para>
    /// <para>
    /// <b>Saqlangan kun bor bo'lsa — faqat u.</b> Kun oralig'i sahifa sanalaridan
    /// bir kun oldin va keyin: tungi smenaning kirishi oldingi sanaga tegishli
    /// bo'lishi mumkin (§5.3).
    /// </para>
    /// <para>
    /// <b>Bugungi, hali qayta hisoblanmagan kun</b> uchun — jonli taxtadagi AYNAN
    /// o'sha hisoblash (<see cref="GetBoardAsync"/>): xuddi shu vaqt oynasidagi
    /// o'tishlar, xuddi shu hal qiluvchi va <see cref="AttendanceDayBuilder"/>. Aks
    /// holda taxta «kechikdi» desa-yu, jurnal jim tursa, HR qaysi biriga ishonishini
    /// bilmasdi. Eski kunlar (bugundan oldingi, qatori yo'q) hisoblanmaydi: ularda
    /// qator yo'qligi — navbat ishlamayotgani belgisi, va jurnal bir sahifa uchun
    /// oylar bo'yicha o'tishlarni yuklamasligi kerak.
    /// </para>
    /// <para>
    /// So'rovlar soni sahifa hajmiga bog'liq emas: bittasi doim (kun yozuvlari), va
    /// sahifada bugungi hisoblanmagan o'tish bo'lsagina yana to'rttasi (hal qiluvchi
    /// uchun uchta, o'tishlar uchun bitta).
    /// </para>
    /// </remarks>
    private async Task<Dictionary<Guid, int>> LateMinutesByPassAsync(
        List<PassRow> rows,
        Dictionary<Guid, List<Employment>> employments,
        CancellationToken cancellationToken)
    {
        var matched = rows.Where(row => row.EmployeeId is not null).ToList();
        var result = new Dictionary<Guid, int>();

        if (matched.Count == 0)
        {
            return result;
        }

        var employeeIds = matched.Select(row => row.EmployeeId!.Value).Distinct().ToList();
        var from = matched.Min(row => row.LocalDate).AddDays(-1);
        var to = matched.Max(row => row.LocalDate).AddDays(1);

        var stored = await _context.AttendanceDays
            .AsNoTracking()
            .Where(day => employeeIds.Contains(day.EmployeeId) && day.LocalDate >= from && day.LocalDate <= to)
            .Select(day => new StoredFirstIn(day.EmployeeId, day.LocalDate, day.FirstInAt, day.LateMinutes))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var lateByFirstIn = new Dictionary<(Guid EmployeeId, DateTimeOffset FirstInAt), int>();

        foreach (var day in stored)
        {
            if (day.FirstInAt is { } firstIn && day.LateMinutes > 0)
            {
                lateByFirstIn.TryAdd((day.EmployeeId, firstIn), day.LateMinutes);
            }
        }

        foreach (var row in matched)
        {
            if (lateByFirstIn.TryGetValue((row.EmployeeId!.Value, row.OccurredAt), out var late))
            {
                result[row.Id] = late;
            }
        }

        var today = _clock.Today;
        var storedToday = stored.Where(day => day.LocalDate == today).Select(day => day.EmployeeId).ToHashSet();

        var pending = matched
            .Where(row =>
                row.LocalDate >= today.AddDays(-1) &&
                !result.ContainsKey(row.Id) &&
                !storedToday.Contains(row.EmployeeId!.Value))
            .Select(row => row.EmployeeId!.Value)
            .Distinct()
            .ToList();

        if (pending.Count == 0)
        {
            return result;
        }

        // Taxtadagi oyna bilan AYNAN bir xil (`GetBoardAsync`).
        var now = _clock.UtcNow.ToUniversalTime();
        var insideSince = now.AddHours(-AttendanceRules.BoardLookbackHours);
        var dayWindowSince = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), BonviTime.TashkentOffset)
            .ToUniversalTime()
            .AddHours(-AttendanceRules.DayWindowPreHours);
        var since = insideSince < dayWindowSince ? insideSince : dayWindowSince;
        var sinceDate = since.TashkentDate();

        var readings = (await _context.AttendancePasses
                .AsNoTracking()
                .Where(pass =>
                    pass.ResolvedEmployeeId != null &&
                    pending.Contains(pass.ResolvedEmployeeId.Value) &&
                    pass.LocalDate >= sinceDate &&
                    pass.OccurredAt >= since)
                .Select(pass => new PassReading(pass.Id, pass.ResolvedEmployeeId, pass.OccurredAt, pass.Direction))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .GroupBy(pass => pass.EmployeeId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var builder = new AttendanceDayBuilder(
            await LoadScheduleResolverAsync(today.AddDays(-1), today.AddDays(1), cancellationToken).ConfigureAwait(false),
            _clock.Today,
            now);

        // «Ish vaqti» so'rovi kechikishni yopishi mumkin — tabeldagi bilan bir xil (§B3 AMENDED).
        var (creditFrom, creditTo) = WorkCreditRule.RequestDatesNeeded(today, today);
        var credits = await AbsenceQuery
            .WorkCreditsAsync(_context, pending, creditFrom, creditTo, cancellationToken)
            .ConfigureAwait(false);

        foreach (var employeeId in pending)
        {
            if (!readings.TryGetValue(employeeId, out var passes))
            {
                continue;
            }

            var facts = builder
                .Build(
                    employeeId,
                    employments.GetValueOrDefault(employeeId) ?? [],
                    passes,
                    [today],
                    workCredits: credits.GetValueOrDefault(employeeId))[0]
                .Facts;

            if (facts is not { FirstInAt: { } firstIn, LateMinutes: > 0 })
            {
                continue;
            }

            foreach (var row in matched.Where(row => row.EmployeeId == employeeId && row.OccurredAt == firstIn))
            {
                result[row.Id] = facts.LateMinutes;
            }
        }

        return result;
    }

    // ════════════════════════════════════════════════════════════
    //  2. Jonli taxta va ishdan ketganlar ogohlantirishi
    // ════════════════════════════════════════════════════════════

    /// <summary>Taxta uchun o'tishning tor nusxasi.</summary>
    private sealed record BoardPass(
        Guid Id,
        Guid EmployeeId,
        DateTimeOffset OccurredAt,
        PassDirection Direction,
        Guid DeviceId);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>So'rovlar:</b> ogohlantirishlar; so'nggi o'tishlar (eng ko'pi ~30
    /// soatlik: kuniga ~5 400 × 30/24 ≈ 7 ming tor qator); kecha–ertaga
    /// oralig'idagi munosabatlar (~600); biriktirmalar, jadvallar kunlari
    /// bilan va bo'limlar; ekrandagi odamlarning nomlari; soati adashgan
    /// qurilmalar soni. Xodim soniga bog'liq so'rov yo'q — «kutilganlar»
    /// xotirada hisoblanadi.
    /// </para>
    /// <para>
    /// ⭐ <b>«Kelmagan» va «kechikkan» — tabel bilan BIR XIL kod.</b> Kutilgan
    /// har bir xodimning so'nggi o'tishlari <see cref="AttendanceDayBuilder"/>
    /// ga bugungi sana uchun beriladi: qaysi o'tish bugunga tegishli (§5.3
    /// kun oynasi, tungi smena), birinchi kirish (§5.5 — sakrash va
    /// yo'nalishi noma'lum o'tishning navbat bilan taxmini) va kechikish
    /// daqiqasi aynan tungi hisoblashdagidek. Taxta ertalab «kechikdi»
    /// deb, tabel kechqurun «vaqtida kelgan» deb yozishi mumkin emas.
    /// Bugungi taxmin keyingi o'tishlarga bog'liq emas (birinchi kirish
    /// chapdan o'ngga aniqlanadi), shuning uchun kun tugashini kutish shart
    /// emas.
    /// </para>
    /// <para>
    /// Yo'nalishi noma'lum o'tishlar faqat «ichkarida» ro'yxatiga KIRMAYDI
    /// (§6.5). «Kelmagan» sanog'iga esa kiradi: yo'nalishsiz turniketdan
    /// o'tgan odam baribir kelgan.
    /// </para>
    /// </remarks>
    public async Task<AttendanceBoardDto> GetBoardAsync(
        BoardFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var now = _clock.UtcNow.ToUniversalTime();
        var today = _clock.Today;

        var alerts = await FormerEmployeeAlertsAsync(now, cancellationToken).ConfigureAwait(false);

        var insideSince = now.AddHours(-AttendanceRules.BoardLookbackHours);
        // Bugungi eng erta smena ham kun oynasidan (§5.3) tashqarida qolmasin:
        // Toshkent yarim tunidan DayWindowPreHours soat oldingacha.
        var dayWindowSince = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), BonviTime.TashkentOffset)
            .ToUniversalTime()
            .AddHours(-AttendanceRules.DayWindowPreHours);
        var since = insideSince < dayWindowSince ? insideSince : dayWindowSince;
        var sinceDate = since.TashkentDate();

        // `local_date` sharti ortiqcha ko'rinadi, lekin u indeksga tushadi:
        // `occurred_at` ning o'zida alohida indeks yo'q.
        var passes = await _context.AttendancePasses
            .AsNoTracking()
            .Where(pass =>
                pass.LocalDate >= sinceDate &&
                pass.OccurredAt >= since &&
                pass.ResolvedEmployeeId != null)
            .OrderBy(pass => pass.OccurredAt)
            .ThenBy(pass => pass.Id)
            .Select(pass => new BoardPass(
                pass.Id,
                pass.ResolvedEmployeeId!.Value,
                pass.OccurredAt,
                pass.Direction,
                pass.DeviceId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var passesByEmployee = passes
            .GroupBy(pass => pass.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(pass => new PassReading(pass.Id, pass.EmployeeId, pass.OccurredAt, pass.Direction))
                    .ToList());

        // Kecha va ertaga ham: tungi smenadagi o'tish qo'shni sananing
        // rejasiga qarab bugunga yoki kechaga tegishli bo'ladi (§5.3), va
        // hisoblash o'sha sanalardagi munosabatni ham so'raydi.
        var yesterday = today.AddDays(-1);
        var tomorrow = today.AddDays(1);

        // Hodisalari bilan — jadval sanadagi lavozim/bo'lim bo'yicha hal qilinadi (`EmploymentPlacement`).
        var employments = await _context.Employments
            .AsNoTracking()
            .Include(employment => employment.Events)
            .AsSplitQuery()
            .Where(employment =>
                employment.HireDate <= tomorrow &&
                (employment.TerminationDate == null || employment.TerminationDate >= yesterday))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var employmentsByEmployee = employments
            .GroupBy(employment => employment.EmployeeId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var primaryToday = employmentsByEmployee
            .Select(pair => (EmployeeId: pair.Key, Employment: PrimaryEmployment.Select(pair.Value, today)))
            .Where(pair => pair.Employment is not null)
            .ToDictionary(pair => pair.EmployeeId, pair => pair.Employment!);

        HashSet<Guid>? branch = filter.DepartmentId is { } departmentId
            ? [.. await BranchIdsAsync(departmentId, cancellationToken).ConfigureAwait(false)]
            : null;

        HashSet<Guid>? matching = filter.Search is { } search
            ? [.. await EmployeeSearchQuery.MatchingIds(_context, search)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)]
            : null;

        // Filtr BUGUNGI asosiy munosabatga qo'llanadi. Bugun munosabati yo'q
        // odam firma/bo'lim filtri bilan hech qachon mos kelmaydi — u
        // ogohlantirishda turibdi, filtrsiz.
        bool MatchesFilter(Guid employeeId)
        {
            if (matching is not null && !matching.Contains(employeeId))
            {
                return false;
            }

            if (filter.LegalEntityId is null && branch is null)
            {
                return true;
            }

            if (!primaryToday.TryGetValue(employeeId, out var employment))
            {
                return false;
            }

            return (filter.LegalEntityId is not { } legalEntityId || employment.LegalEntityId == legalEntityId)
                && (branch is null || (employment.DepartmentId is { } department && branch.Contains(department)));
        }

        var resolver = await LoadScheduleResolverAsync(yesterday, tomorrow, cancellationToken).ConfigureAwait(false);
        var builder = new AttendanceDayBuilder(resolver, _clock.Today, now);

        // «Ish vaqti» so'rovi bilan boshqa zavoddagi odam «kelmagan» ham, «kechikkan» ham emas.
        var (creditFrom, creditTo) = WorkCreditRule.RequestDatesNeeded(today, today);
        var credits = await AbsenceQuery
            .WorkCreditsAsync(_context, [.. primaryToday.Keys], creditFrom, creditTo, cancellationToken)
            .ConfigureAwait(false);

        int expected = 0, absent = 0, late = 0;

        foreach (var (employeeId, employment) in primaryToday)
        {
            if (!MatchesFilter(employeeId))
            {
                continue;
            }

            // «Kutilgan» — §5.4 (b) to'plami: bugun munosabat bor va jadval
            // bugunni ish kuni deydi.
            if (resolver.Resolve(employment, today)?.Day is not { IsWorking: true, PlanStartAt: { } planStart } plan)
            {
                continue;
            }

            expected++;

            var personCredits = credits.GetValueOrDefault(employeeId);
            var day = passesByEmployee.TryGetValue(employeeId, out var readings) || personCredits is not null
                ? builder.Build(
                    employeeId,
                    employmentsByEmployee[employeeId],
                    readings ?? [],
                    [today],
                    workCredits: personCredits)[0]
                : null;
            var computed = day?.Facts;

            // Ertalabki shaxsiy vaqt kutilgan kelishni suradi (SPEC-sorov-tahriri §5.6).
            var lateAfter = (day?.Personal?.ExpectedStartAt ?? planStart).AddMinutes(plan.GraceInMinutes);

            if (computed is null || (computed.PassCount == 0 && computed.CreditedMinutes == 0))
            {
                absent++;

                // Hali kelmagan, lekin yon berish vaqti o'tib ketgan — kechikkan.
                // SPEC §6.5: 09:30 dagi «5 kechikdi» yetib kelmaganlarni ham sanaydi.
                if (now > lateAfter)
                {
                    late++;
                }

                continue;
            }

            if (computed.LateMinutes > 0)
            {
                late++;
            }
        }

        var inside = passes
            .Where(pass => pass.OccurredAt >= insideSince && pass.Direction != PassDirection.Unknown)
            .GroupBy(pass => pass.EmployeeId)
            .Select(group => group.Last())
            .Where(pass => pass.Direction == PassDirection.In && MatchesFilter(pass.EmployeeId))
            .ToList();

        var cards = await EmployeeCardsAsync([.. inside.Select(pass => pass.EmployeeId)], cancellationToken)
            .ConfigureAwait(false);
        var devices = await DeviceLabelsAsync(inside.Select(pass => pass.DeviceId), cancellationToken)
            .ConfigureAwait(false);

        var insideEmployments = inside
            .Select(pass => primaryToday.GetValueOrDefault(pass.EmployeeId))
            .ToList();

        var names = await CatalogNamesAsync(
                insideEmployments.Select(employment => employment?.DepartmentId),
                insideEmployments.Select(employment => employment?.PositionId),
                [],
                cancellationToken)
            .ConfigureAwait(false);

        // Ism bo'yicha — kartalar har 30 soniyada JOYIDA qolishi uchun. «Oxirgi
        // kirgan birinchi» tartibida har yangi kirishda butun panjara siljirdi
        // va stoldan qarab odamni topib bo'lmasdi.
        IReadOnlyList<BoardItemDto> items =
        [
            .. inside
                .Select(pass =>
                {
                    var employment = primaryToday.GetValueOrDefault(pass.EmployeeId);
                    var card = cards.GetValueOrDefault(pass.EmployeeId);

                    return new BoardItemDto(
                        pass.EmployeeId,
                        card?.FullName ?? string.Empty,
                        card?.PhotoKey,
                        names.Department(employment?.DepartmentId),
                        names.Position(employment?.PositionId),
                        pass.OccurredAt,
                        pass.Id,
                        devices.GetValueOrDefault(pass.DeviceId)?.Name);
                })
                .OrderBy(item => item.FullName, StringComparer.InvariantCulture)
                .ThenBy(item => item.EmployeeId),
        ];

        /*
          Soat ogohlantirishi — qurilmalar va Statistika sahifalari bilan BITTA
          qoidadan (`AttendanceDevice.ClockWarningAt`). Ilgari bu yerda
          `last_skew_seconds` (qabul − sodir) sanalardi: agent internet
          uzilishidan keyin eski o'tishlarni yuborganda soati to'g'ri terminal
          ham «soat og'ishi» banneriga tushardi. Qurilmalar o'ntadan oshmaydi —
          xotirada sanash qoidani SQL'da ikkinchi marta yozishdan xavfsizroq.
        */
        var activeDevices = await _context.AttendanceDevices
            .AsNoTracking()
            .Where(device => device.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var clockWarnings = activeDevices.Count(device => !device.IsSynthetic && device.ClockWarningAt(now));

        return new AttendanceBoardDto(
            now,
            items.Count,
            expected,
            absent,
            late,
            clockWarnings,
            items,
            alerts);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FormerEmployeeAlertDto>> GetFormerEmployeeAlertsAsync(
        CancellationToken cancellationToken = default)
        => await FormerEmployeeAlertsAsync(_clock.UtcNow.ToUniversalTime(), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Ishdan ketganlar ogohlantirishi — taxta ham, nishon ham FAQAT shu metodni chaqiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shart bazada: so'nggi <see cref="AttendanceRules.FormerEmployeeAlertHours"/> soatdagi
    /// moslangan o'tish, uning SANASINI qamraydigan (o'chirilmagan) mehnat
    /// munosabati YO'Q — <see cref="AttendanceDayStatus.NotEmployed"/> bilan
    /// aynan bir xil shart, lekin kun yozuvini kutmasdan. Odatda natija
    /// bo'sh, ya'ni har 30 soniyada so'ralishi arzon.
    /// </para>
    /// <para>
    /// ⚠️ Bu yerdagi SQL sharti — <see cref="PrimaryEmployment.Covers"/> ning
    /// bazadagi ko'rinishi (ikki chekka ham ichida, o'chirilgan munosabat
    /// hisobga olinmaydi). Xotirada <see cref="PrimaryEmployment.Select"/> ni
    /// chaqirish uchun nishon har 30 soniyada har bir ekrandan kunlik
    /// o'tishlarning HAMMASINI va ularning munosabatlarini tortib kelardi;
    /// qamrov qoidasi o'zgarsa, shu shart ham birga o'zgaradi.
    /// </para>
    /// <para>
    /// Xodim kartasi O'CHIRILGANLARI bilan birga o'qiladi: kartasi yumshoq
    /// o'chirilgan odam turniketdan o'tsa, bu ogohlantirishni yo'q qilish
    /// uchun sabab emas — aksincha.
    /// </para>
    /// </remarks>
    private async Task<IReadOnlyList<FormerEmployeeAlertDto>> FormerEmployeeAlertsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var since = now.AddHours(-AttendanceRules.FormerEmployeeAlertHours).ToUniversalTime();
        var sinceDate = since.TashkentDate();

        var passes = await _context.AttendancePasses
            .AsNoTracking()
            .Where(pass =>
                pass.LocalDate >= sinceDate &&
                pass.OccurredAt >= since &&
                pass.ResolvedEmployeeId != null &&
                !_context.Employments.Any(employment =>
                    employment.EmployeeId == pass.ResolvedEmployeeId &&
                    employment.HireDate <= pass.LocalDate &&
                    (employment.TerminationDate == null || employment.TerminationDate >= pass.LocalDate)))
            .OrderByDescending(pass => pass.OccurredAt)
            .ThenByDescending(pass => pass.Id)
            .Select(pass => new
            {
                pass.Id,
                EmployeeId = pass.ResolvedEmployeeId!.Value,
                pass.OccurredAt,
                pass.DeviceId,
                pass.Direction,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (passes.Count == 0)
        {
            return [];
        }

        // Bir ODAM — bir qator, uning eng so'nggi o'tishi bilan (SPEC §6.5).
        // Qatorlar allaqachon yangidan eskiga, `GroupBy` esa guruhlarni
        // birinchi uchragan tartibda qoldiradi — ya'ni tartib ham saqlanadi.
        var newest = passes
            .GroupBy(pass => pass.EmployeeId)
            .Select(group => (Pass: group.First(), Count: group.Count()))
            .ToList();

        var employeeIds = newest.Select(entry => entry.Pass.EmployeeId).ToList();

        var cards = await EmployeeCardsAsync(employeeIds, cancellationToken).ConfigureAwait(false);
        var devices = await DeviceLabelsAsync(newest.Select(entry => entry.Pass.DeviceId), cancellationToken)
            .ConfigureAwait(false);

        // «Eng so'nggi tugagan munosabat» (SPEC §6.5) — bo'shash sanasi eng
        // kechi. Bitta tor so'rov: ogohlantirishdagi odamlar odatda bir-ikkita.
        var ended = await _context.Employments
            .AsNoTracking()
            .Where(employment => employeeIds.Contains(employment.EmployeeId) && employment.TerminationDate != null)
            .Select(employment => new
            {
                employment.EmployeeId,
                TerminationDate = employment.TerminationDate!.Value,
                employment.LegalEntityId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var lastEnded = ended
            .GroupBy(employment => employment.EmployeeId)
            .ToDictionary(group => group.Key, group => group.MaxBy(employment => employment.TerminationDate)!);

        var names = await CatalogNamesAsync(
                [],
                [],
                lastEnded.Values.Select(employment => (Guid?)employment.LegalEntityId),
                cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. newest.Select(entry =>
            {
                var pass = entry.Pass;
                var card = cards.GetValueOrDefault(pass.EmployeeId);
                var employment = lastEnded.GetValueOrDefault(pass.EmployeeId);

                return new FormerEmployeeAlertDto(
                    pass.Id,
                    pass.EmployeeId,
                    card?.FullName ?? string.Empty,
                    card?.PhotoKey,
                    pass.OccurredAt,
                    pass.DeviceId,
                    devices.GetValueOrDefault(pass.DeviceId)?.Name,
                    pass.Direction,
                    employment?.TerminationDate,
                    names.LegalEntity(employment?.LegalEntityId),
                    entry.Count);
            }),
        ];
    }

    // ════════════════════════════════════════════════════════════
    //  2a. Asosiy sahifa paneli
    // ════════════════════════════════════════════════════════════

    /// <summary>Panel qatori — holati hisoblangan, nomlari hali yig'ilmagan.</summary>
    private sealed record DashboardRow(
        Guid EmployeeId,
        string FullName,
        string? PhotoKey,
        Guid? DepartmentId,
        Guid? PositionId,
        DateTimeOffset? ArrivedAt,
        DateTimeOffset? LeftAt,
        DashboardVerdict Verdict);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>So'rovlar</b> — to'plam va sahifa hajmidan qat'i nazar: to'plam (sanani
    /// qamraydigan, filtrga mos munosabati bor xodimlar; bo'lim filtrida daraxt
    /// uchun yana bittasi), qidiruv (berilsa), to'plamning BARCHA munosabatlari,
    /// [sana − 1, sana + 1] o'tishlari, jadval hal qiluvchisi (uchtagacha) va
    /// sahifadagi bo'lim/lavozim nomlari (ikkitagacha). ~540 odamda o'tishlar ~16
    /// ming tor qator (kuniga 10 o'tish × 3 kun) — tabel va taxtadagidan og'ir emas.
    /// </para>
    /// <para>
    /// ⭐ <b>Yuklash shartnomasi va hisoblash — tabel va taxtadagining o'zi.</b>
    /// Jadvallar kunlari bilan, jadval va bo'limlar o'chirilganlari bilan
    /// (<see cref="LoadScheduleResolverAsync"/>); o'tishlar
    /// <see cref="AttendanceDayBuilder.PassDatesNeeded"/> oralig'ida; birinchi
    /// kirish va oxirgi chiqish <see cref="AttendanceDayBuilder"/> dan — ya'ni
    /// tungi smenaning ertalabki chiqishi oldingi sananing qatoriga tushadi,
    /// sakrash tashlanadi, noma'lum yo'nalish navbat bilan to'ldiriladi. Holat
    /// qoidasining o'zi — <see cref="DashboardStatusRule"/>.
    /// </para>
    /// <para>
    /// <b>Nega butun to'plam xotirada.</b> Sanoqlar har bir odamni talab qiladi,
    /// holat esa SQL ustuni emas — uni bazada filtrlab ham, sanab ham bo'lmaydi.
    /// Shu sababli hammasi BIR MARTA hisoblanadi, keyin qidiruv va holat bilan
    /// filtrlanib sahifalanadi; nomlar faqat sahifadagilar uchun yig'iladi.
    /// </para>
    /// </remarks>
    public async Task<DashboardPage> GetDashboardAsync(
        DashboardFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var now = _clock.UtcNow.ToUniversalTime();
        var today = _clock.Today;
        var date = filter.Date;

        // ── 1. To'plam: sanani qamraydigan munosabat, org-filtr tabeldagidek ──
        var covering = _context.Employments
            .AsNoTracking()
            .Where(employment =>
                employment.HireDate <= date &&
                (employment.TerminationDate == null || employment.TerminationDate >= date));

        covering = await ApplyEmploymentOrgFilterAsync(
                covering,
                filter.LegalEntityId,
                filter.DepartmentId,
                filter.PositionId,
                cancellationToken)
            .ConfigureAwait(false);

        var coveringEmployeeIds = covering.Select(employment => employment.EmployeeId);

        var people = await _context.Employees
            .AsNoTracking()
            .Where(employee => coveringEmployeeIds.Contains(employee.Id))
            .Select(employee => new
            {
                employee.Id,
                employee.FullName.LastName,
                employee.FullName.FirstName,
                employee.FullName.MiddleName,
                employee.PhotoKey,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (people.Count == 0)
        {
            return new DashboardPage(date, date == today, now, 0, new AttendanceDashboardCountsDto(0, 0, 0, 0, 0, 0, 0, 0), [], 0);
        }

        var populationIds = people.Select(person => person.Id).ToList();

        // Qidiruv sharti barcha ro'yxatlardagi bilan AYNAN bir xil (`EmployeeSearchQuery`).
        HashSet<Guid>? matching = filter.Search is { } search
            ? [.. await EmployeeSearchQuery.Matching(_context, search)
                .Where(employee => populationIds.Contains(employee.Id))
                .Select(employee => employee.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)]
            : null;

        var employments = await EmploymentsAsync(populationIds, cancellationToken).ConfigureAwait(false);

        var (passesFrom, passesTo) = AttendanceDayBuilder.PassDatesNeeded(date, date);

        // `local_date` sharti indeksga tushadi; `occurred_at` ning o'zida alohida indeks yo'q.
        var passes = (await _context.AttendancePasses
                .AsNoTracking()
                .Where(pass =>
                    pass.LocalDate >= passesFrom &&
                    pass.LocalDate <= passesTo &&
                    pass.ResolvedEmployeeId != null &&
                    populationIds.Contains(pass.ResolvedEmployeeId.Value))
                .Select(pass => new PassReading(pass.Id, pass.ResolvedEmployeeId, pass.OccurredAt, pass.Direction))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .GroupBy(pass => pass.EmployeeId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var resolver = await LoadScheduleResolverAsync(date, date, cancellationToken).ConfigureAwait(false);
        var builder = new AttendanceDayBuilder(resolver, _clock.Today, now);

        // Tasdiqlangan so'rovlar — tabel bilan BITTA shartdan (`AbsenceQuery`).
        // Ularsiz panel «Kelmadi», kalendar esa «Sababli» deb ko'rsatardi.
        var excusedDates = await AbsenceQuery
            .ExcusedDatesAsync(_context, populationIds, date, date, cancellationToken)
            .ConfigureAwait(false);

        // «Ish vaqti» so'rovi — boshqa zavodda bo'lgan odam «Kelmadi» emas (§B3 AMENDED).
        var (creditFrom, creditTo) = WorkCreditRule.RequestDatesNeeded(date, date);
        var credits = await AbsenceQuery
            .WorkCreditsAsync(_context, populationIds, creditFrom, creditTo, cancellationToken)
            .ConfigureAwait(false);

        // ── 2. Holatlar — butun to'plam, xotirada, bir marta ─────────
        var rows = new List<DashboardRow>(people.Count);

        foreach (var person in people)
        {
            var personEmployments = employments.GetValueOrDefault(person.Id) ?? [];

            // To'plam so'rovi bilan shu yuklash orasida munosabat o'zgargan bo'lsa —
            // odam bu safar chiqmaydi. «Munosabatsiz» holat yo'q (panel shartnomasi).
            if (PrimaryEmployment.Select(personEmployments, date) is not { } primary)
            {
                continue;
            }

            DateTimeOffset? arrivedAt = null;
            DateTimeOffset? leftAt = null;
            ComputedAttendanceDay? day = null;
            var personCredits = credits.GetValueOrDefault(person.Id);

            if (passes.TryGetValue(person.Id, out var readings) || personCredits is not null)
            {
                day = builder.Build(person.Id, personEmployments, readings ?? [], [date], workCredits: personCredits)[0];

                if (day.Facts.PassCount > 0)
                {
                    arrivedAt = ArrivedAt(day);
                    leftAt = day.Facts.LastOutAt;
                }
            }

            rows.Add(new DashboardRow(
                person.Id,
                FullName.FromTrusted(person.LastName, person.FirstName, person.MiddleName).Full,
                person.PhotoKey,
                primary.DepartmentId,
                primary.PositionId,
                arrivedAt,
                leftAt,
                DashboardStatusRule.Classify(
                    resolver.Resolve(primary, date)?.Day,
                    EffectiveArrival(day),
                    today,
                    now,
                    excusedDates.GetValueOrDefault(person.Id)?.Contains(date) ?? false,
                    day?.Personal)));
        }

        var byStatus = rows.CountBy(row => row.Verdict.Status).ToDictionary();
        int CountOf(AttendanceDashboardStatus status) => byStatus.GetValueOrDefault(status);

        var counts = new AttendanceDashboardCountsDto(
            CountOf(AttendanceDashboardStatus.OnTime),
            CountOf(AttendanceDashboardStatus.Late),
            CountOf(AttendanceDashboardStatus.Absent),
            CountOf(AttendanceDashboardStatus.NotStarted),
            CountOf(AttendanceDashboardStatus.DayOff),
            CountOf(AttendanceDashboardStatus.NoSchedule),
            CountOf(AttendanceDashboardStatus.Excused),
            CountOf(AttendanceDashboardStatus.Arrived));

        // ── 3. Jadval — qidiruv va holat faqat SHU yerda ─────────────
        var visible = rows.Where(row =>
            (matching is null || matching.Contains(row.EmployeeId)) &&
            (filter.Status is not { } status || row.Verdict.Status == status));

        // Ism bo'yicha — taxtadagi bilan bir xil taqqoslovchi: yangilanishda qatorlar joyida qolsin.
        var ordered = filter.Descending
            ? visible
                .OrderByDescending(row => row.FullName, StringComparer.InvariantCulture)
                .ThenByDescending(row => row.EmployeeId)
                .ToList()
            : visible
                .OrderBy(row => row.FullName, StringComparer.InvariantCulture)
                .ThenBy(row => row.EmployeeId)
                .ToList();

        var pageRows = ordered.Skip(filter.Skip).Take(filter.PageSize).ToList();

        var names = await CatalogNamesAsync(
                pageRows.Select(row => row.DepartmentId),
                pageRows.Select(row => row.PositionId),
                [],
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<AttendanceDashboardItemDto> items =
        [
            .. pageRows.Select(row => new AttendanceDashboardItemDto(
                row.EmployeeId,
                row.FullName,
                row.PhotoKey,
                names.Position(row.PositionId),
                names.Department(row.DepartmentId),
                row.ArrivedAt,
                row.LeftAt,
                row.Verdict.Status,
                row.Verdict.LateMinutes,
                row.Verdict.Status == AttendanceDashboardStatus.Arrived
                    && AttendanceRules.IsNoScheduleAutoExit(date, row.LeftAt))),
        ];

        return new DashboardPage(date, date == today, now, rows.Count, counts, items, ordered.Count);
    }

    /// <summary>
    /// Kunning «kelgan» lahzasi: birinchi kirish, kirish bo'lmasa — kunga tushgan birinchi o'tish.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="AttendanceDayBuilder"/> faktlarda birinchi KIRISHNI beradi. Noma'lum
    /// yo'nalish navbat bilan kirishga aylanadi, ya'ni ikkinchi tarmoqqa faqat
    /// kunning hamma o'tishi aniq «chiqish» bo'lganda tushiladi (masalan, kirish
    /// turniketi ishlamagan kun) — shunda ham odam kelgan va panel «kelmagan»
    /// demasligi kerak.
    /// </para>
    /// <para>
    /// ⚠️ Birinchi o'tish hisoblovchining O'ZIDAN
    /// (<see cref="ComputedAttendanceDay.FirstPassAt"/>): o'tishning qaysi kunga
    /// tushishi (kechagi smenaning kech chiqishi istisnosi bilan) shu yerda qayta
    /// yig'ilsa, panel kechaga o'tgan 00:20 dagi chiqishni bugungi «kelish» deb
    /// ko'rsatishi mumkin edi.
    /// </para>
    /// </remarks>
    private static DateTimeOffset? ArrivedAt(ComputedAttendanceDay day)
        => day.Facts.PassCount > 0 ? day.Facts.FirstInAt ?? day.FirstPassAt : null;

    /// <summary>
    /// Panel holati uchun «keldi» lahzasi: o'tish YOKI «ish vaqti» so'rovi boshlangani,
    /// qaysi biri erta (SPEC-so'rovlar §B3 AMENDED). Ro'yxatda ko'rsatiladigan vaqt
    /// esa faqat o'tishniki (<see cref="ArrivedAt"/>) — terminal yozmagan vaqt «keldi
    /// 08:30» bo'lib chiqmasin.
    /// </summary>
    private static DateTimeOffset? EffectiveArrival(ComputedAttendanceDay? day)
    {
        if (day is null)
        {
            return null;
        }

        var arrived = ArrivedAt(day);

        return day.CreditedFromAt is { } credited && (arrived is null || credited < arrived)
            ? credited
            : arrived;
    }

    // ════════════════════════════════════════════════════════════
    //  2b. Davr grafigi
    // ════════════════════════════════════════════════════════════

    /// <summary>Grafik uchun o'tishlar necha kunlik bo'laklarda o'qiladi.</summary>
    /// <remarks>
    /// <para>
    /// Ikki chekka o'rtasi. Kun bo'yicha aylanish (bo'lak = 1) 92 kunga 92 ta
    /// so'rov degani — o'lchangan ~150 ms dan ~14 soniya chiqadi va
    /// shartnomadagi byudjet buziladi. Butun oraliqni bir marta o'qish esa
    /// 92 × 540 × ~10 ≈ yarim million qatorni bir vaqtda xotirada ushlab
    /// turardi — serverda jami 2 GB.
    /// </para>
    /// <para>
    /// 14 kun: 92 kunga 7 ta so'rov, xotirada bir vaqtda ~75 ming tor qator
    /// (bir oylik jurnal sahifasidan og'ir emas). Qo'shni bo'laklar o'tishlarni
    /// ikki kun bo'yicha qayta o'qiydi — tungi smenaning chiqishi uchun
    /// (<see cref="AttendanceDayBuilder.PassDatesNeeded"/>), va bu narx
    /// ataylab to'lanadi: chegaradagi kun ikkala bo'lakda ham to'liq
    /// ma'lumot bilan hisoblanadi.
    /// </para>
    /// </remarks>
    private const int TrendPassChunkDays = 14;

    /// <summary>To'plam oynasi — org-filtrdan o'tgan BITTA munosabat.</summary>
    /// <remarks>
    /// Kunning to'plami panelnikidek aniqlanadi: shu sanani qamraydigan,
    /// filtrga mos munosabati bor odam. Oraliqning har bir kuni uchun
    /// so'rov takrorlanmaydi — oynalar bir marta o'qilib, sana ustida
    /// XOTIRADA tekshiriladi.
    /// </remarks>
    private sealed record TrendSpan(Guid EmployeeId, DateOnly HireDate, DateOnly? TerminationDate)
    {
        public bool Covers(DateOnly date)
            => HireDate <= date && (TerminationDate is null || TerminationDate >= date);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// ⭐ <b>Panelning o'zi, faqat ko'p kunga.</b> To'plam, asosiy munosabat,
    /// kelgan lahza va holat — <see cref="GetDashboardAsync"/> dagi AYNAN
    /// o'sha to'rt qadam (<see cref="PrimaryEmployment"/>,
    /// <see cref="AttendanceDayBuilder"/>, <see cref="ArrivedAt"/>,
    /// <see cref="DashboardStatusRule"/>). Bir kunlik oraliqda natija
    /// panelning <c>Counts</c> i bilan teng bo'lishi shu tenglikdan kelib
    /// chiqadi, testda esa tekshiriladi.
    /// </para>
    /// <para>
    /// <b>So'rovlar</b> — oraliq uzunligiga bog'liq EMAS: to'plam oynalari
    /// (bo'lim filtrida daraxt uchun yana bittasi), to'plamning barcha
    /// munosabatlari, jadval hal qiluvchisi (uchtagacha) va har bo'lak
    /// uchun bittadan o'tishlar so'rovi. 31 kunga 3 ta bo'lak, 92 kunga 7 ta.
    /// </para>
    /// <para>
    /// Natija SANOQQA yig'iladi (kun × oltita son), ya'ni xotira odamlar
    /// soniga ko'paytirilmaydi — 92 kunlik javob ham 552 ta butun son.
    /// </para>
    /// </remarks>
    public async Task<AttendanceTrendDto> GetDashboardTrendAsync(
        DashboardTrendFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var now = _clock.UtcNow.ToUniversalTime();
        var today = _clock.Today;
        var from = filter.From;
        var to = filter.To;

        // Chegaralar ikki tomondan yopiq. Teskari oraliq bu yerga yetib
        // kelmaydi (`AttendanceQueryParsing.Period` 400 qaytaradi), lekin
        // manfiy uzunlik massiv o'lchamiga aylanmasligi kerak.
        var dayCount = Math.Max(0, to.DayNumber - from.DayNumber + 1);
        if (dayCount == 0)
        {
            return new AttendanceTrendDto(from, to, now, []);
        }

        // Har bir sana uchun oltita sanoq. Holat qiymatlari 1 dan boshlanadi.
        var tallies = new int[dayCount][];
        for (var index = 0; index < dayCount; index++)
        {
            tallies[index] = new int[8];
        }

        // ── 1. To'plam oynalari: org-filtr paneldagi bilan bir xil ──────
        var covering = _context.Employments
            .AsNoTracking()
            .Where(employment =>
                employment.HireDate <= to &&
                (employment.TerminationDate == null || employment.TerminationDate >= from));

        covering = await ApplyEmploymentOrgFilterAsync(
                covering,
                filter.LegalEntityId,
                filter.DepartmentId,
                filter.PositionId,
                cancellationToken)
            .ConfigureAwait(false);

        var spans = await covering
            .Select(employment => new TrendSpan(
                employment.EmployeeId,
                employment.HireDate,
                employment.TerminationDate))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (spans.Count == 0)
        {
            // Bo'sh oraliq ham TIRQISHSIZ: grafik o'z o'qini to'qimaydi.
            return new AttendanceTrendDto(from, to, now, Days(from, tallies));
        }

        var spansByEmployee = spans
            .GroupBy(span => span.EmployeeId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var populationIds = spansByEmployee.Keys.ToList();

        var employments = await EmploymentsAsync(populationIds, cancellationToken).ConfigureAwait(false);

        // Biriktirmalar butun oraliqni bir marta qamraydi — jadval jadvali
        // kichik va uni bo'lak boshiga qayta o'qish foydasiz so'rov bo'lardi.
        var resolver = await LoadScheduleResolverAsync(from, to, cancellationToken).ConfigureAwait(false);
        var builder = new AttendanceDayBuilder(resolver, _clock.Today, now);

        // So'rovlar ham BUTUN oraliq uchun bir marta: ular kam va bo'laklarga
        // bo'lib o'qish faqat qo'shimcha so'rov bo'lardi.
        var excusedDates = await AbsenceQuery
            .ExcusedDatesAsync(_context, populationIds, from, to, cancellationToken)
            .ConfigureAwait(false);

        var (creditFrom, creditTo) = WorkCreditRule.RequestDatesNeeded(from, to);
        var credits = await AbsenceQuery
            .WorkCreditsAsync(_context, populationIds, creditFrom, creditTo, cancellationToken)
            .ConfigureAwait(false);

        // ── 2. Bo'laklab: o'tishlar → holat → sanoq ─────────────────────
        for (var chunkStart = from; chunkStart <= to; chunkStart = chunkStart.AddDays(TrendPassChunkDays))
        {
            var chunkLast = chunkStart.AddDays(TrendPassChunkDays - 1);
            var chunkEnd = chunkLast > to ? to : chunkLast;

            List<DateOnly> chunkDates =
            [
                .. Enumerable
                    .Range(0, chunkEnd.DayNumber - chunkStart.DayNumber + 1)
                    .Select(chunkStart.AddDays),
            ];

            var (passesFrom, passesTo) = AttendanceDayBuilder.PassDatesNeeded(chunkStart, chunkEnd);

            var passes = (await _context.AttendancePasses
                    .AsNoTracking()
                    .Where(pass =>
                        pass.LocalDate >= passesFrom &&
                        pass.LocalDate <= passesTo &&
                        pass.ResolvedEmployeeId != null &&
                        populationIds.Contains(pass.ResolvedEmployeeId.Value))
                    .Select(pass => new PassReading(pass.Id, pass.ResolvedEmployeeId, pass.OccurredAt, pass.Direction))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false))
                .GroupBy(pass => pass.EmployeeId!.Value)
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (var employeeId in populationIds)
            {
                var personEmployments = employments.GetValueOrDefault(employeeId) ?? [];
                var personSpans = spansByEmployee[employeeId];

                // Hisoblash faqat o'tishi BOR odam uchun yurgiziladi —
                // paneldagi bilan bir xil: o'tishsiz kunda kelgan lahza
                // baribir `null`, holatni esa reja va soat hal qiladi.
                Dictionary<DateOnly, DateTimeOffset?>? arrivals = null;

                // Shaxsiy vaqt — kelgan lahza yonida (SPEC-sorov-tahriri §5.6): panel bilan bir xil qaror.
                Dictionary<DateOnly, PersonalTime?>? personalTimes = null;
                var personCredits = credits.GetValueOrDefault(employeeId);

                if (passes.TryGetValue(employeeId, out var readings) || personCredits is not null)
                {
                    arrivals = [];
                    personalTimes = [];

                    foreach (var day in builder.Build(employeeId, personEmployments, readings ?? [], chunkDates, workCredits: personCredits))
                    {
                        arrivals[day.LocalDate] = EffectiveArrival(day);
                        personalTimes[day.LocalDate] = day.Personal;
                    }
                }

                foreach (var date in chunkDates)
                {
                    if (!personSpans.Exists(span => span.Covers(date)))
                    {
                        continue;
                    }

                    // Panel shartnomasi: munosabatsiz odam holat olmaydi.
                    if (PrimaryEmployment.Select(personEmployments, date) is not { } primary)
                    {
                        continue;
                    }

                    var arrivedAt = arrivals is null ? null : arrivals.GetValueOrDefault(date);
                    var verdict = DashboardStatusRule.Classify(
                        resolver.Resolve(primary, date)?.Day,
                        arrivedAt,
                        today,
                        now,
                        excusedDates.GetValueOrDefault(employeeId)?.Contains(date) ?? false,
                        personalTimes?.GetValueOrDefault(date));

                    tallies[date.DayNumber - from.DayNumber][(int)verdict.Status - 1]++;
                }
            }
        }

        return new AttendanceTrendDto(from, to, now, Days(from, tallies));
    }

    /// <summary>Sanoq massivini kunlar qatoriga o'giradi — tirqishsiz, o'sish tartibida.</summary>
    /// <remarks>
    /// <c>Total</c> alohida sanalmaydi, oltitasining yig'indisidan olinadi:
    /// shunda «yig'indi jamiga teng» degan shartnoma (§8.4) buzilishining
    /// yo'li yo'q.
    /// </remarks>
    private static IReadOnlyList<AttendanceTrendDayDto> Days(DateOnly from, int[][] tallies)
    =>
    [
        .. tallies.Select((counts, index) => new AttendanceTrendDayDto(
            from.AddDays(index),
            counts.Sum(),
            new AttendanceDashboardCountsDto(
                counts[(int)AttendanceDashboardStatus.OnTime - 1],
                counts[(int)AttendanceDashboardStatus.Late - 1],
                counts[(int)AttendanceDashboardStatus.Absent - 1],
                counts[(int)AttendanceDashboardStatus.NotStarted - 1],
                counts[(int)AttendanceDashboardStatus.DayOff - 1],
                counts[(int)AttendanceDashboardStatus.NoSchedule - 1],
                counts[(int)AttendanceDashboardStatus.Excused - 1],
                counts[(int)AttendanceDashboardStatus.Arrived - 1]))),
    ];

    // ════════════════════════════════════════════════════════════
    //  2c. Tug'ilgan kunlar paneli
    // ════════════════════════════════════════════════════════════

    /// <summary>Ishonchli deb qabul qilinadigan yoshning yuqori chegarasi.</summary>
    /// <remarks>
    /// Qiymat bosh sahifadagi ro'yxat bilan AYNAN bir xil
    /// (<c>GetBirthdaysTodayQueryHandler.MaxTrustedAge</c>) va bu ataylab:
    /// bitta xodim ikki ekranda bir xil yoshda ko'rinishi kerak. Chegaradan
    /// oshgan qiymat — Excel'dan ko'chirilgan yozuvdagi xato, tug'ilgan kun
    /// emas; shunday holatda yosh umuman chizilmaydi.
    /// </remarks>
    private const int MaxTrustedAge = 120;

    /// <summary>Tartiblashdan oldingi qator — nomi yig'ilgan, keyingi sanasi hisoblangan.</summary>
    private sealed record BirthdayRow(
        Guid EmployeeId,
        string FullName,
        string? PhotoKey,
        DateOnly BirthDate,
        DateOnly NextBirthday,
        int DaysUntil);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>So'rovlar</b> — ro'yxat uzunligidan qat'i nazar: to'plam (bugunni
    /// qamraydigan, org-filtrga mos munosabati bor xodimlarning tug'ilgan
    /// sanalari; bo'lim filtrida daraxt uchun yana bittasi), CHIQQAN o'nta
    /// odamning munosabatlari va ularning bo'lim/lavozim nomlari (ikkitagacha).
    /// </para>
    /// <para>
    /// ⚠️ Keyingi sana SQL da emas, XOTIRADA hisoblanadi
    /// (<see cref="NextBirthday"/>): yil chegarasi va 29-fevral qoidasi
    /// bazaga ko'chirilsa, u yerda ikkinchi nusxa bo'lib qolardi va
    /// PostgreSQL'ning kalendar funksiyalari bilan qayta yozilardi. ~950
    /// tor qatorni tartiblash bundan arzon.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<AttendanceBirthdayDto>> GetUpcomingBirthdaysAsync(
        BirthdayFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.Limit <= 0)
        {
            return [];
        }

        var today = filter.Today;

        // To'plam — paneldagi BUGUNGI to'plamning o'zi (§E4.1).
        var covering = _context.Employments
            .AsNoTracking()
            .Where(employment =>
                employment.HireDate <= today &&
                (employment.TerminationDate == null || employment.TerminationDate >= today));

        covering = await ApplyEmploymentOrgFilterAsync(
                covering,
                filter.LegalEntityId,
                filter.DepartmentId,
                filter.PositionId,
                cancellationToken)
            .ConfigureAwait(false);

        var coveringEmployeeIds = covering.Select(employment => employment.EmployeeId);

        // Sanasi yo'q xodim ro'yxatda umuman qatnashmaydi — «noma'lum» qatori
        // yo'q: tabriklab bo'lmaydigan yozuv panelda joy egallamasligi kerak.
        var people = await _context.Employees
            .AsNoTracking()
            .Where(employee =>
                employee.BirthDate != null &&
                coveringEmployeeIds.Contains(employee.Id))
            .Select(employee => new
            {
                employee.Id,
                employee.FullName.LastName,
                employee.FullName.FirstName,
                employee.FullName.MiddleName,
                employee.PhotoKey,
                employee.BirthDate,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = new List<BirthdayRow>(people.Count);

        foreach (var person in people)
        {
            var birthDate = person.BirthDate!.Value;

            if (NextBirthday(today, birthDate) is not { } next)
            {
                continue;
            }

            rows.Add(new BirthdayRow(
                person.Id,
                FullName.FromTrusted(person.LastName, person.FirstName, person.MiddleName).Full,
                person.PhotoKey,
                birthDate,
                next,
                next.DayNumber - today.DayNumber));
        }

        // Ism va id — teng sanalar uchun BARQAROR tartib: har yangilashda
        // bir xil kunda tug'ilgan ikki odam joyini almashtirmasligi kerak.
        var nearest = rows
            .OrderBy(row => row.DaysUntil)
            .ThenBy(row => row.FullName, StringComparer.InvariantCulture)
            .ThenBy(row => row.EmployeeId)
            .Take(filter.Limit)
            .ToList();

        if (nearest.Count == 0)
        {
            return [];
        }

        var employments = await EmploymentsAsync(
                [.. nearest.Select(row => row.EmployeeId)],
                cancellationToken)
            .ConfigureAwait(false);

        // Bo'lim va lavozim — BUGUNGI asosiy munosabatdan (§5.1), paneldagi
        // jadval bilan bitta qoidadan.
        var primary = nearest.ToDictionary(
            row => row.EmployeeId,
            row => PrimaryEmployment.Select(employments.GetValueOrDefault(row.EmployeeId) ?? [], today));

        var names = await CatalogNamesAsync(
                primary.Values.Select(employment => employment?.DepartmentId),
                primary.Values.Select(employment => employment?.PositionId),
                [],
                cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. nearest.Select(row => new AttendanceBirthdayDto(
                row.EmployeeId,
                row.FullName,
                row.PhotoKey,
                names.Position(primary[row.EmployeeId]?.PositionId),
                names.Department(primary[row.EmployeeId]?.DepartmentId),
                row.BirthDate,
                row.NextBirthday,
                row.DaysUntil,
                Age(row.NextBirthday, row.BirthDate))),
        ];
    }

    /// <summary>Bugundan boshlab keyingi nishonlanadigan sana — bugun ham kiradi.</summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>29-fevralda tug'ilganlar kabisa bo'lmagan yilda O'TKAZIB
    /// YUBORILADI</b> va keyingi kabisa yiliga tushadi. Bu <c>BirthdayQuery</c>
    /// dagi qoidaning o'zi: sun'iy sana tanlash (28-fevral yoki 1-mart)
    /// tizimning qarori bo'lib qolardi, HR ning emas. Amalda bu odam
    /// «eng yaqin o'ntalik» dan chiqib ketadi.
    /// </para>
    /// <para>
    /// Aylanish beshta yil bo'yicha: [yil, yil + 4] oralig'ida kabisa yili
    /// DOIM bor, ya'ni javob topilmasligi mumkin emas. <c>null</c> — faqat
    /// kutilmagan kalendar holati uchun.
    /// </para>
    /// </remarks>
    private static DateOnly? NextBirthday(DateOnly today, DateOnly birthDate)
    {
        for (var year = today.Year; year <= today.Year + 4; year++)
        {
            if (birthDate.Day > DateTime.DaysInMonth(year, birthDate.Month))
            {
                continue;
            }

            var candidate = new DateOnly(year, birthDate.Month, birthDate.Day);

            if (candidate >= today)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Keyingi tug'ilgan kunda to'ladigan yosh — sana ishonchsiz bo'lsa <c>null</c>.</summary>
    private static int? Age(DateOnly nextBirthday, DateOnly birthDate)
    {
        var age = nextBirthday.Year - birthDate.Year;

        return age is > 0 and < MaxTrustedAge ? age : null;
    }

    // ════════════════════════════════════════════════════════════
    //  2d. Yo'qlik so'rovlari
    // ════════════════════════════════════════════════════════════

    /// <summary>So'rov qatorining bazadan olinadigan qismi — nomlarsiz.</summary>
    private sealed record RequestRow(
        Guid Id,
        Guid EmployeeId,
        Guid AbsenceTypeId,
        DateOnly FromDate,
        DateOnly ToDate,
        TimeOnly? FromTime,
        TimeOnly? ToTime,
        string? Note,
        AbsenceRequestStatus Status,
        string? DecisionNote,
        Guid? DecidedBy,
        DateTimeOffset? DecidedAt,
        DateTimeOffset CreatedAt,
        bool SubmittedByEmployee,
        bool CancelledByEmployee,
        int Version,
        DateTimeOffset? EditedAt,
        bool EditedByEmployee);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>So'rovlar</b> — sahifa hajmidan qat'i nazar: sanoq, sahifa, qidiruv
    /// (berilsa), sahifadagi xodimlar, ularning bugungi munosabatlari,
    /// yo'qlik turlari va qaror qabul qilganlarning ismlari.
    /// </para>
    /// <para>
    /// Lavozim va bo'lim BUGUNGI asosiy munosabatdan olinadi, so'rov
    /// davridagidan emas: ro'yxat «hozir kim so'rov yubordi» degan ekran va
    /// undagi odam bugungi bo'limi bilan tanilishi kerak.
    /// </para>
    /// </remarks>
    public async Task<AttendancePage<AbsenceRequestDto>> SearchAbsenceRequestsAsync(
        AbsenceRequestFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = _context.AbsenceRequests.AsNoTracking();

        if (filter.EmployeeId is { } employeeId)
        {
            query = query.Where(request => request.EmployeeId == employeeId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(request => request.Status == status);
        }

        if (filter.Decided is { } decided)
        {
            query = decided
                ? query.Where(request => request.Status != AbsenceRequestStatus.Pending)
                : query.Where(request => request.Status == AbsenceRequestStatus.Pending);
        }

        if (filter.AbsenceTypeId is { } absenceTypeId)
        {
            query = query.Where(request => request.AbsenceTypeId == absenceTypeId);
        }

        // Shakl so'rovning o'zidan: vaqt bor — qism kun; bir sana — butun kun; aks holda — bir necha kun.
        query = filter.Mode switch
        {
            AbsenceRequestMode.PartDay => query.Where(request => request.FromTime != null),
            AbsenceRequestMode.WholeDay => query.Where(request => request.FromTime == null && request.FromDate == request.ToDate),
            AbsenceRequestMode.MultiDay => query.Where(request => request.FromDate != request.ToDate),
            _ => query,
        };

        // Davr QAMROV bo'yicha: oraliqqa qisman tegadigan so'rov ham kiradi.
        if (filter.From is { } from)
        {
            query = query.Where(request => request.ToDate >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(request => request.FromDate <= to);
        }

        if (filter.Search is { } search)
        {
            var matching = EmployeeSearchQuery.Matching(_context, search).Select(employee => employee.Id);
            query = query.Where(request => matching.Contains(request.EmployeeId));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await query
            .OrderByDescending(request => request.CreatedAt)
            .ThenByDescending(request => request.Id)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .Select(request => new RequestRow(
                request.Id,
                request.EmployeeId,
                request.AbsenceTypeId,
                request.FromDate,
                request.ToDate,
                request.FromTime,
                request.ToTime,
                request.Note,
                request.Status,
                request.DecisionNote,
                request.DecidedBy,
                request.DecidedAt,
                request.CreatedAt,
                request.SubmittedByAccountId != null,
                request.CancelledByAccountId != null,
                request.Version,
                request.EditedAt,
                request.EditedByAccountId != null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AttendancePage<AbsenceRequestDto>(
            await ToRequestDtosAsync(rows, cancellationToken).ConfigureAwait(false),
            total);
    }

    /// <inheritdoc />
    public async Task<AbsenceRequestDto?> FindAbsenceRequestAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.AbsenceRequests
            .AsNoTracking()
            .Where(request => request.Id == id)
            .Select(request => new RequestRow(
                request.Id,
                request.EmployeeId,
                request.AbsenceTypeId,
                request.FromDate,
                request.ToDate,
                request.FromTime,
                request.ToTime,
                request.Note,
                request.Status,
                request.DecisionNote,
                request.DecidedBy,
                request.DecidedAt,
                request.CreatedAt,
                request.SubmittedByAccountId != null,
                request.CancelledByAccountId != null,
                request.Version,
                request.EditedAt,
                request.EditedByAccountId != null))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var items = await ToRequestDtosAsync([row], cancellationToken).ConfigureAwait(false);

        return items[0];
    }

    /// <inheritdoc />
    /// <remarks>
    /// Navigatsiya nishoni BITTA son — yo'qlik VA jadval so'rovlari birga
    /// (SPEC-verifikatsiya-jadval-sorovi §C3): HR uchun ikkalasi ham «qaror kutayotgan so'rov».
    /// </remarks>
    public async Task<int> CountPendingAbsenceRequestsAsync(CancellationToken cancellationToken = default)
    {
        var absences = await _context.AbsenceRequests
            .AsNoTracking()
            .CountAsync(request => request.Status == AbsenceRequestStatus.Pending, cancellationToken)
            .ConfigureAwait(false);

        var schedules = await _context.ScheduleChangeRequests
            .AsNoTracking()
            .CountAsync(request => request.Status == AbsenceRequestStatus.Pending, cancellationToken)
            .ConfigureAwait(false);

        return absences + schedules;
    }

    /// <summary>Qatorlarga ism, tur nomi va qaror egasini qo'shadi.</summary>
    private async Task<IReadOnlyList<AbsenceRequestDto>> ToRequestDtosAsync(
        List<RequestRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var employeeIds = rows.Select(row => row.EmployeeId).Distinct().ToList();

        var cards = await EmployeeCardsAsync(employeeIds, cancellationToken).ConfigureAwait(false);
        var employments = await EmploymentsAsync(employeeIds, cancellationToken).ConfigureAwait(false);

        var today = _clock.Today;

        var primaries = employeeIds.ToDictionary(
            id => id,
            id => PrimaryEmployment.Select(employments.GetValueOrDefault(id) ?? [], today));

        var names = await CatalogNamesAsync(
                primaries.Values.Select(employment => employment?.DepartmentId),
                primaries.Values.Select(employment => employment?.PositionId),
                [],
                cancellationToken)
            .ConfigureAwait(false);

        var typeIds = rows.Select(row => row.AbsenceTypeId).Distinct().ToList();

        var types = await _context.AbsenceTypes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(type => typeIds.Contains(type.Id))
            .Select(type => new { type.Id, type.Name })
            .ToDictionaryAsync(type => type.Id, type => type.Name, cancellationToken)
            .ConfigureAwait(false);

        var deciderIds = rows
            .Where(row => row.DecidedBy is not null)
            .Select(row => row.DecidedBy!.Value)
            .Distinct()
            .ToList();

        var deciders = deciderIds.Count == 0
            ? []
            : await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user => deciderIds.Contains(user.Id))
                .Select(user => new { user.Id, Name = user.FullName.Full })
                .ToDictionaryAsync(user => user.Id, user => user.Name, cancellationToken)
                .ConfigureAwait(false);

        return
        [
            .. rows.Select(row =>
            {
                var employment = primaries.GetValueOrDefault(row.EmployeeId);
                var card = cards.GetValueOrDefault(row.EmployeeId);

                return new AbsenceRequestDto(
                    row.Id,
                    row.EmployeeId,
                    card?.FullName ?? string.Empty,
                    card?.PhotoKey,
                    names.Position(employment?.PositionId),
                    names.Department(employment?.DepartmentId),
                    row.AbsenceTypeId,
                    types.GetValueOrDefault(row.AbsenceTypeId) ?? string.Empty,
                    row.FromDate,
                    row.ToDate,
                    row.FromTime,
                    row.ToTime,
                    row.Note,
                    row.Status,
                    row.DecisionNote,
                    row.DecidedBy is { } decider ? deciders.GetValueOrDefault(decider) : null,
                    row.DecidedAt,
                    row.CreatedAt,
                    row.SubmittedByEmployee,
                    row.CancelledByEmployee,
                    row.Version,
                    row.EditedAt,
                    row.EditedByEmployee);
            }),
        ];
    }

    // ════════════════════════════════════════════════════════════
    //  3. Tabel
    // ════════════════════════════════════════════════════════════

    /// <summary>Tabel uchun kun yozuvining kerakli ustunlari.</summary>
    private sealed record DayRow(
        Guid EmployeeId,
        DateOnly LocalDate,
        AttendanceDayStatus Status,
        int WorkedMinutes,
        int PlannedMinutes,
        int LateMinutes,
        int EarlyLeaveMinutes,
        DateTimeOffset? FirstInAt,
        DateTimeOffset? LastOutAt,
        int BreakTakenMinutes,
        int BreakDeductionMinutes,
        DateTimeOffset? PlanStartAt,
        DateTimeOffset? PlanEndAt,
        Guid? DepartmentId,
        Guid? PositionId,
        DateTimeOffset ComputedAt,
        int CreditedMinutes,
        bool IsFlexible,
        int ShortfallMinutes,
        int PersonalMinutes);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>Ikki bosqich.</b> (1) Bazada: filtrga mos xodimlar sanaladi,
    /// saralanadi va sahifa olinadi. (2) Faqat sahifadagi xodimlar uchun
    /// to'rtta so'rov — kun yozuvlari, munosabatlar, kalendar (biriktirma +
    /// jadval + bo'limlar), nomlar — va har bir kun uchun katak XOTIRADA
    /// yig'iladi. Eksportda sahifa butun ro'yxat bo'ladi, so'rovlar soni
    /// esa o'zgarmaydi.
    /// </para>
    /// <para>
    /// Qatordagi bo'lim/lavozim — oyning oxirgi ishlagan kunidagi asosiy
    /// munosabatdan; umuman munosabati bo'lmagan (faqat <c>NotEmployed</c>
    /// yozuvi bor) odamda — kun yozuvi suratidan, bo'lmasa bo'sh.
    /// </para>
    /// </remarks>
    public async Task<TimesheetPage> GetTimesheetAsync(
        TimesheetFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var monthStart = filter.MonthStart;
        var monthEnd = filter.MonthEnd;

        IReadOnlyList<DateOnly> monthDays =
        [
            .. Enumerable.Range(0, monthEnd.DayNumber - monthStart.DayNumber + 1)
                .Select(monthStart.AddDays),
        ];

        // ── 1. Kim ro'yxatda ──────────────────────────────────────
        var employments = _context.Employments
            .AsNoTracking()
            .Where(employment =>
                employment.HireDate <= monthEnd &&
                (employment.TerminationDate == null || employment.TerminationDate >= monthStart));

        var days = _context.AttendanceDays
            .AsNoTracking()
            .Where(day => day.LocalDate >= monthStart && day.LocalDate <= monthEnd);

        employments = await ApplyEmploymentOrgFilterAsync(
                employments,
                filter.LegalEntityId,
                filter.DepartmentId,
                filter.PositionId,
                cancellationToken)
            .ConfigureAwait(false);

        days = await ApplyDayOrgFilterAsync(
                days,
                filter.LegalEntityId,
                filter.DepartmentId,
                filter.PositionId,
                cancellationToken)
            .ConfigureAwait(false);

        var candidateIds = employments
            .Select(employment => employment.EmployeeId)
            .Union(days.Select(day => day.EmployeeId));

        var employees = EmployeeSearchQuery
            .Matching(_context, filter.Search)
            .Where(employee => candidateIds.Contains(employee.Id));

        if (filter.EmployeeId is { } employeeId)
        {
            employees = employees.Where(employee => employee.Id == employeeId);
        }

        var total = await employees.CountAsync(cancellationToken).ConfigureAwait(false);

        var page = await employees
            .OrderBy(employee => employee.FullName.LastName)
            .ThenBy(employee => employee.FullName.FirstName)
            .ThenBy(employee => employee.Id)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .Select(employee => new
            {
                employee.Id,
                employee.FullName.LastName,
                employee.FullName.FirstName,
                employee.FullName.MiddleName,
                employee.PhotoKey,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (page.Count == 0)
        {
            return new TimesheetPage(monthDays, [], total, null);
        }

        // ── 2. Sahifa uchun ma'lumot — xodimlar sonidan qat'i nazar ──
        var pageIds = page.Select(employee => employee.Id).ToList();

        var dayRows = await _context.AttendanceDays
            .AsNoTracking()
            .Where(day =>
                pageIds.Contains(day.EmployeeId) &&
                day.LocalDate >= monthStart &&
                day.LocalDate <= monthEnd)
            .Select(day => new DayRow(
                day.EmployeeId,
                day.LocalDate,
                day.Status,
                day.WorkedMinutes,
                day.PlannedMinutes,
                day.LateMinutes,
                day.EarlyLeaveMinutes,
                day.FirstInAt,
                day.LastOutAt,
                day.BreakTakenMinutes,
                day.BreakDeductionMinutes,
                day.PlanStartAt,
                day.PlanEndAt,
                day.DepartmentId,
                day.PositionId,
                day.ComputedAt,
                day.CreditedMinutes,
                day.IsFlexible,
                day.ShortfallMinutes,
                day.PersonalMinutes))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var storedDays = dayRows.ToDictionary(day => (day.EmployeeId, day.LocalDate));

        var mobileMarks = await MobileMarkCountsAsync(pageIds, monthStart, monthEnd, dayRows, cancellationToken)
            .ConfigureAwait(false);

        var employmentsByEmployee = await EmploymentsAsync(pageIds, cancellationToken).ConfigureAwait(false);
        var builder = new AttendanceDayBuilder(
            await LoadScheduleResolverAsync(monthStart, monthEnd, cancellationToken).ConfigureAwait(false),
            _clock.Today,
            _clock.UtcNow);

        // Hosil qilingan katak ham tasdiqlangan so'rovni bilishi kerak: aks
        // holda hali hisoblanmagan kun «Kelmadi», qo'shnisi esa «Sababli»
        // bo'lib chiqardi (shart `AbsenceQuery` da — bitta nusxa).
        var excusedDates = await AbsenceQuery
            .ExcusedDatesAsync(_context, pageIds, monthStart, monthEnd, cancellationToken)
            .ConfigureAwait(false);

        var (creditFrom, creditTo) = WorkCreditRule.RequestDatesNeeded(monthStart, monthEnd);
        var credits = await AbsenceQuery
            .WorkCreditsAsync(_context, pageIds, creditFrom, creditTo, cancellationToken)
            .ConfigureAwait(false);

        var displayEmployments = pageIds.ToDictionary(
            id => id,
            id => DisplayEmployment(employmentsByEmployee.GetValueOrDefault(id) ?? [], monthDays));

        var latestSnapshots = dayRows
            .GroupBy(day => day.EmployeeId)
            .ToDictionary(group => group.Key, group => group.MaxBy(day => day.LocalDate)!);

        var names = await CatalogNamesAsync(
                pageIds.Select(id => displayEmployments[id]?.DepartmentId
                    ?? latestSnapshots.GetValueOrDefault(id)?.DepartmentId),
                pageIds.Select(id => displayEmployments[id]?.PositionId
                    ?? latestSnapshots.GetValueOrDefault(id)?.PositionId),
                [],
                cancellationToken)
            .ConfigureAwait(false);

        // ── 3. Kataklar — xotirada, bir marta ─────────────────────
        IReadOnlyList<TimesheetRowDto> items =
        [
            .. page.Select(employee =>
            {
                var synthesized = SynthesizeStatuses(
                    builder,
                    employee.Id,
                    employmentsByEmployee.GetValueOrDefault(employee.Id) ?? [],
                    [.. monthDays.Where(date => !storedDays.ContainsKey((employee.Id, date)))],
                    excusedDates.GetValueOrDefault(employee.Id),
                    credits.GetValueOrDefault(employee.Id));

                IReadOnlyList<TimesheetCellDto> cells =
                [
                    .. monthDays.Select(date => storedDays.TryGetValue((employee.Id, date), out var stored)
                        ? new TimesheetCellDto(
                            date,
                            stored.Status,
                            stored.WorkedMinutes,
                            stored.LateMinutes,
                            stored.EarlyLeaveMinutes,
                            stored.FirstInAt,
                            stored.LastOutAt,
                            stored.BreakTakenMinutes,
                            stored.BreakDeductionMinutes,
                            stored.PlanStartAt,
                            stored.PlanEndAt,
                            mobileMarks.GetValueOrDefault((employee.Id, date)),
                            stored.Status == AttendanceDayStatus.NoSchedule
                                && AttendanceRules.IsNoScheduleAutoExit(date, stored.LastOutAt),
                            stored.CreditedMinutes,
                            stored.IsFlexible,
                            stored.PlannedMinutes,
                            stored.ShortfallMinutes,
                            stored.PersonalMinutes)
                        // Hosil qilingan katakda reja ham YO'Q: u hali
                        // hisoblanmagan kun (§5.7.1) va rejasini shu yerda
                        // qayta hisoblash tungi hisoblashning ikkinchi
                        // nusxasi bo'lardi.
                        : new TimesheetCellDto(
                            date,
                            synthesized[date],
                            WorkedMinutes: 0,
                            LateMinutes: 0,
                            EarlyLeaveMinutes: 0,
                            FirstInAt: null,
                            LastOutAt: null,
                            BreakTakenMinutes: 0,
                            BreakDeductionMinutes: 0,
                            PlanStartAt: null,
                            PlanEndAt: null,
                            MobileMarks: mobileMarks.GetValueOrDefault((employee.Id, date)))),
                ];

                var plannedMinutes = monthDays.Sum(date =>
                    storedDays.TryGetValue((employee.Id, date), out var stored) ? stored.PlannedMinutes : 0);

                var totals = new TimesheetTotalsDto(
                    cells.Sum(cell => cell.WorkedMinutes),
                    plannedMinutes,
                    cells.Count(cell => cell.Status == AttendanceDayStatus.Present),
                    cells.Count(cell => cell.Status == AttendanceDayStatus.Absent),
                    cells.Sum(cell => cell.LateMinutes),
                    cells.Sum(cell => cell.EarlyLeaveMinutes),
                    cells.Sum(cell => cell.ShortfallMinutes),
                    cells.Sum(cell => cell.PersonalMinutes));

                var display = displayEmployments[employee.Id];
                var snapshot = latestSnapshots.GetValueOrDefault(employee.Id);

                return new TimesheetRowDto(
                    employee.Id,
                    FullName.FromTrusted(employee.LastName, employee.FirstName, employee.MiddleName).Full,
                    employee.PhotoKey,
                    names.Department(display?.DepartmentId ?? snapshot?.DepartmentId),
                    names.Position(display?.PositionId ?? snapshot?.PositionId),
                    cells,
                    totals);
            }),
        ];

        DateTimeOffset? lastComputedAt = dayRows.Count == 0 ? null : dayRows.Max(day => day.ComputedAt);

        return new TimesheetPage(monthDays, items, total, lastComputedAt);
    }

    /// <summary>
    /// Sahifadagi xodimlarning TELEFON belgilari — (xodim, katak) → soni.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Katak tabel qoidasi bilan aniqlanadi (<see cref="PassAttribution"/>) —
    /// kun yozuvidagi reja SURATI bo'yicha, xuddi xodim ilovasidagi kabi: tungi
    /// smenaning ertalabki telefon chiqishi kechagi katakda sanaladi. Oyning
    /// chetidagi (± bir kun) o'tishlar ham o'qiladi, chunki ular shu oyning
    /// katagiga tegishli bo'lishi mumkin.
    /// </para>
    /// <para>
    /// Bitta tor so'rov: faqat <c>method = Mobile</c> — telefon belgilari
    /// jami o'tishlarning kichik qismi.
    /// </para>
    /// </remarks>
    private async Task<Dictionary<(Guid EmployeeId, DateOnly Date), int>> MobileMarkCountsAsync(
        List<Guid> employeeIds,
        DateOnly monthStart,
        DateOnly monthEnd,
        List<DayRow> dayRows,
        CancellationToken cancellationToken)
    {
        var from = monthStart.AddDays(-1);
        var to = monthEnd.AddDays(1);

        var passes = await _context.AttendancePasses
            .AsNoTracking()
            .Where(pass =>
                pass.Method == PassMethod.Mobile &&
                pass.ResolvedEmployeeId != null &&
                employeeIds.Contains(pass.ResolvedEmployeeId.Value) &&
                pass.LocalDate >= from &&
                pass.LocalDate <= to)
            .Select(pass => new { EmployeeId = pass.ResolvedEmployeeId!.Value, pass.LocalDate, pass.OccurredAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (passes.Count == 0)
        {
            return [];
        }

        var windows = dayRows.ToDictionary(
            day => (day.EmployeeId, day.LocalDate),
            day => PassAttribution.WindowOf(day.PlanStartAt, day.PlanEndAt));

        return passes
            .Select(pass => (
                pass.EmployeeId,
                Date: PassAttribution.DateFor(
                    pass.LocalDate,
                    pass.OccurredAt,
                    date => windows.GetValueOrDefault((pass.EmployeeId, date)))))
            .Where(mark => mark.Date >= monthStart && mark.Date <= monthEnd)
            .GroupBy(mark => mark)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    /// <summary>
    /// Yozuvi yo'q kunlarning holati — SPEC §5.7.1, tungi hisoblash kodi bilan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <see cref="AttendanceDayBuilder"/> NOL o'tish bilan yurgiziladi va
    /// uning <c>Facts.Status</c> i olinadi. O'tishsiz kun uchun §5.6 aynan
    /// §5.7.1 ning to'rt tarmog'iga qisqaradi: munosabat yo'q →
    /// <c>NotEmployed</c>, jadval yo'q → <c>NoSchedule</c>, dam olish kuni →
    /// <c>DayOff</c>, qolgani → <c>Absent</c>. Ya'ni alohida qoida yozilmaydi
    /// va saqlangan katak bilan hosil qilingan katak bir-biriga zid bo'la
    /// olmaydi.
    /// </para>
    /// <para>
    /// Faqat HOLAT olinadi. Qolgan sonlar §5.7.1 bo'yicha 0 — hisoblash
    /// rejadagi daqiqalarni ham berardi, lekin qatori yo'q kun uchun «reja»
    /// yig'indiga qo'shilsa, tabel saqlanmagan narsani jamlagan bo'lardi.
    /// </para>
    /// </remarks>
    private static Dictionary<DateOnly, AttendanceDayStatus> SynthesizeStatuses(
        AttendanceDayBuilder builder,
        Guid employeeId,
        List<Employment> employments,
        List<DateOnly> dates,
        IReadOnlySet<DateOnly>? excusedDates = null,
        IReadOnlyCollection<WorkCredit>? workCredits = null)
        => dates.Count == 0
            ? []
            : builder
                .Build(employeeId, employments, Array.Empty<PassReading>(), dates, excusedDates, workCredits)
                .ToDictionary(day => day.LocalDate, day => day.Facts.Status);

    /// <summary>Oyning oxiridan boshlab birinchi ishlagan kunidagi asosiy munosabat.</summary>
    private static Employment? DisplayEmployment(List<Employment> employments, IReadOnlyList<DateOnly> monthDays)
    {
        if (employments.Count == 0)
        {
            return null;
        }

        for (var i = monthDays.Count - 1; i >= 0; i--)
        {
            if (PrimaryEmployment.Select(employments, monthDays[i]) is { } employment)
            {
                return employment;
            }
        }

        return null;
    }

    // ════════════════════════════════════════════════════════════
    //  4. Hisobotlar
    // ════════════════════════════════════════════════════════════

    /// <inheritdoc />
    /// <remarks>
    /// Faqat SAQLANGAN kun yozuvlari — hisobot kun yozuvi SURATIDAGI firma,
    /// bo'lim va lavozim bo'yicha filtrlanadi, xodimning bugungi holati
    /// bo'yicha emas: ko'chirishdan keyin o'tgan oyning hisoboti o'zgarmaydi
    /// (§2.6). <c>ix_attendance_days_date_status</c> va bo'lim/firma
    /// indekslari aynan shu shartlar uchun.
    /// </remarks>
    public async Task<AttendancePage<AttendanceExceptionDto>> GetExceptionsAsync(
        ExceptionFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var days = _context.AttendanceDays
            .AsNoTracking()
            .Where(day => day.LocalDate >= filter.From && day.LocalDate <= filter.To);

        days = await ApplyDayOrgFilterAsync(
                days,
                filter.LegalEntityId,
                filter.DepartmentId,
                filter.PositionId,
                cancellationToken)
            .ConfigureAwait(false);

        if (filter.EmployeeId is { } employeeId)
        {
            days = days.Where(day => day.EmployeeId == employeeId);
        }

        var minMinutes = filter.MinMinutes ?? 1;

        days = filter.Kind switch
        {
            ExceptionKind.Late => days.Where(day => day.LateMinutes >= minMinutes),
            ExceptionKind.EarlyLeave => days.Where(day => day.EarlyLeaveMinutes >= minMinutes),
            ExceptionKind.Absent => days.Where(day => day.Status == AttendanceDayStatus.Absent),
            ExceptionKind.Missing => days.Where(day =>
                day.Status == AttendanceDayStatus.MissingExit ||
                day.Status == AttendanceDayStatus.MissingEntry ||
                day.Status == AttendanceDayStatus.NoSchedule),
            ExceptionKind.Shortfall => days.Where(day => day.ShortfallMinutes >= minMinutes),
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter.Kind, "Noma'lum chetlanish turi."),
        };

        var shaped =
            from day in days
            join employee in _context.Employees on day.EmployeeId equals employee.Id
            select new
            {
                Day = day,
                employee.FullName.LastName,
                employee.FullName.FirstName,
                employee.FullName.MiddleName,
                employee.PhotoKey,
            };

        var total = await shaped.CountAsync(cancellationToken).ConfigureAwait(false);

        var descending = filter.Descending;

        var sortByMinutes = filter.Sort == ExceptionSortField.Minutes
            && filter.Kind is ExceptionKind.Late or ExceptionKind.EarlyLeave or ExceptionKind.Shortfall;

        var ordered = filter.Sort switch
        {
            ExceptionSortField.Employee => Then(
                By(shaped, row => row.LastName, descending),
                row => row.FirstName,
                descending)
                .ThenByDescending(row => row.Day.LocalDate),
            _ when sortByMinutes && filter.Kind == ExceptionKind.Late =>
                By(shaped, row => row.Day.LateMinutes, descending).ThenByDescending(row => row.Day.LocalDate),
            _ when sortByMinutes && filter.Kind == ExceptionKind.Shortfall =>
                By(shaped, row => row.Day.ShortfallMinutes, descending).ThenByDescending(row => row.Day.LocalDate),
            _ when sortByMinutes =>
                By(shaped, row => row.Day.EarlyLeaveMinutes, descending).ThenByDescending(row => row.Day.LocalDate),
            _ => Then(By(shaped, row => row.Day.LocalDate, descending), row => row.LastName, descending: false),
        };

        var rows = await ordered
            // Oxirgi mezon doim o'sish tartibida — teng qiymatlarda sahifalar
            // orasida qator takrorlanmasin yoki tushib qolmasin.
            .ThenBy(row => row.Day.EmployeeId)
            .ThenBy(row => row.Day.LocalDate)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .Select(row => new
            {
                row.Day.LocalDate,
                row.Day.EmployeeId,
                row.LastName,
                row.FirstName,
                row.MiddleName,
                row.PhotoKey,
                row.Day.DepartmentId,
                row.Day.PositionId,
                row.Day.PlanStartAt,
                row.Day.PlanEndAt,
                row.Day.FirstInAt,
                row.Day.LastOutAt,
                row.Day.LateMinutes,
                row.Day.EarlyLeaveMinutes,
                row.Day.WorkedMinutes,
                row.Day.Status,
                row.Day.ShortfallMinutes,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var names = await CatalogNamesAsync(
                rows.Select(row => row.DepartmentId),
                rows.Select(row => row.PositionId),
                [],
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<AttendanceExceptionDto> items =
        [
            .. rows.Select(row => new AttendanceExceptionDto(
                row.LocalDate,
                row.EmployeeId,
                FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                row.PhotoKey,
                names.Department(row.DepartmentId),
                names.Position(row.PositionId),
                row.PlanStartAt,
                row.PlanEndAt,
                row.FirstInAt,
                row.LastOutAt,
                row.LateMinutes,
                row.EarlyLeaveMinutes,
                row.WorkedMinutes,
                row.Status,
                row.ShortfallMinutes)),
        ];

        return new AttendancePage<AttendanceExceptionDto>(items, total);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Yig'ish BAZADA (<c>GROUP BY employee_id</c>), sahifa ham — 540 xodim ×
    /// 92 kun = ~50 ming qatorni xotiraga olib kelib sanash kerak emas.
    /// </para>
    /// <para>
    /// Bo'lim va lavozim — sahifadagi xodimlarning davrdagi ENG SO'NGGI kun
    /// yozuvi suratidan (bitta tor so'rov).
    /// </para>
    /// </remarks>
    public async Task<AttendancePage<AttendanceSummaryDto>> GetSummaryAsync(
        SummaryFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var days = _context.AttendanceDays
            .AsNoTracking()
            .Where(day => day.LocalDate >= filter.From && day.LocalDate <= filter.To);

        days = await ApplyDayOrgFilterAsync(
                days,
                filter.LegalEntityId,
                filter.DepartmentId,
                filter.PositionId,
                cancellationToken)
            .ConfigureAwait(false);

        var grouped = days
            .GroupBy(day => day.EmployeeId)
            .Select(group => new
            {
                EmployeeId = group.Key,
                PlannedDays = group.Count(day => day.PlannedMinutes > 0),
                PresentDays = group.Count(day => day.Status == AttendanceDayStatus.Present),
                AbsentDays = group.Count(day => day.Status == AttendanceDayStatus.Absent),
                DayOffWorkedDays = group.Count(day => day.Status == AttendanceDayStatus.DayOffWorked),
                ProblemDays = group.Count(day =>
                    day.Status == AttendanceDayStatus.MissingExit ||
                    day.Status == AttendanceDayStatus.MissingEntry ||
                    day.Status == AttendanceDayStatus.NoSchedule),
                WorkedMinutes = group.Sum(day => day.WorkedMinutes),
                PlannedMinutes = group.Sum(day => day.PlannedMinutes),
                LateMinutes = group.Sum(day => day.LateMinutes),
                EarlyLeaveMinutes = group.Sum(day => day.EarlyLeaveMinutes),
                OvertimeMinutes = group.Sum(day => day.OvertimeMinutes),
                ShortfallMinutes = group.Sum(day => day.ShortfallMinutes),
                PersonalMinutes = group.Sum(day => day.PersonalMinutes),
            });

        var shaped =
            from summary in grouped
            join employee in _context.Employees on summary.EmployeeId equals employee.Id
            select new
            {
                Summary = summary,
                employee.FullName.LastName,
                employee.FullName.FirstName,
                employee.FullName.MiddleName,
                employee.PhotoKey,
            };

        var total = await shaped.CountAsync(cancellationToken).ConfigureAwait(false);

        var descending = filter.Descending;

        var ordered = filter.Sort switch
        {
            SummarySortField.WorkedMinutes => By(shaped, row => row.Summary.WorkedMinutes, descending),
            SummarySortField.LateMinutes => By(shaped, row => row.Summary.LateMinutes, descending),
            SummarySortField.AbsentDays => By(shaped, row => row.Summary.AbsentDays, descending),
            SummarySortField.ProblemDays => By(shaped, row => row.Summary.ProblemDays, descending),
            _ => By(shaped, row => row.LastName, descending),
        };

        var rows = await ordered
            .ThenBy(row => row.LastName)
            .ThenBy(row => row.FirstName)
            .ThenBy(row => row.Summary.EmployeeId)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return new AttendancePage<AttendanceSummaryDto>([], total);
        }

        var pageIds = rows.Select(row => row.Summary.EmployeeId).ToList();

        var snapshots = await days
            .Where(day => pageIds.Contains(day.EmployeeId) && (day.DepartmentId != null || day.PositionId != null))
            .Select(day => new { day.EmployeeId, day.LocalDate, day.DepartmentId, day.PositionId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var latest = snapshots
            .GroupBy(day => day.EmployeeId)
            .ToDictionary(group => group.Key, group => group.MaxBy(day => day.LocalDate)!);

        var names = await CatalogNamesAsync(
                latest.Values.Select(day => day.DepartmentId),
                latest.Values.Select(day => day.PositionId),
                [],
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<AttendanceSummaryDto> items =
        [
            .. rows.Select(row =>
            {
                var snapshot = latest.GetValueOrDefault(row.Summary.EmployeeId);

                return new AttendanceSummaryDto(
                    row.Summary.EmployeeId,
                    FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                    row.PhotoKey,
                    names.Department(snapshot?.DepartmentId),
                    names.Position(snapshot?.PositionId),
                    row.Summary.PlannedDays,
                    row.Summary.PresentDays,
                    row.Summary.AbsentDays,
                    row.Summary.DayOffWorkedDays,
                    row.Summary.WorkedMinutes,
                    row.Summary.PlannedMinutes,
                    row.Summary.LateMinutes,
                    row.Summary.EarlyLeaveMinutes,
                    row.Summary.OvertimeMinutes,
                    row.Summary.ProblemDays,
                    row.Summary.ShortfallMinutes,
                    row.Summary.PersonalMinutes);
            }),
        ];

        return new AttendancePage<AttendanceSummaryDto>(items, total);
    }

    // ════════════════════════════════════════════════════════════
    //  5. Qurilmalar
    // ════════════════════════════════════════════════════════════

    /// <inheritdoc />
    /// <remarks>
    /// Bitta <c>GROUP BY device_id</c>. <c>ix_attendance_passes_device_time</c>
    /// <c>occurred_at</c> ustida, bu esa <c>received_at</c> — lekin oyna 24
    /// soat va qurilmalar o'nlab, ya'ni kuniga ~5 400 qatorlik bo'lak.
    /// </remarks>
    public async Task<IReadOnlyDictionary<Guid, int>> CountRecentPassesAsync(
        IReadOnlyCollection<Guid> deviceIds,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceIds);

        if (deviceIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var ids = deviceIds.Distinct().ToList();

        return await _context.AttendancePasses
            .AsNoTracking()
            .Where(pass => ids.Contains(pass.DeviceId) && pass.ReceivedAt >= since)
            .GroupBy(pass => pass.DeviceId)
            .Select(group => new { DeviceId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.DeviceId, row => row.Count, cancellationToken)
            .ConfigureAwait(false);
    }

    // ════════════════════════════════════════════════════════════
    //  6. Mosliklar va import
    // ════════════════════════════════════════════════════════════

    /// <summary>Moslik qatorining bazadan olinadigan qismi — xodim kartasisiz.</summary>
    private sealed record IdentityRow(
        Guid Id,
        AttendanceIdentityProvider Provider,
        string ExternalPersonId,
        Guid EmployeeId,
        string? DisplayNameInDevice,
        IdentitySource Source,
        string? Note,
        DateTimeOffset CreatedAt);

    /// <summary>Moslik proyeksiyasi — ro'yxat va bitta yozuv bitta ifodadan.</summary>
    private static readonly Expression<Func<AttendanceIdentity, IdentityRow>> IdentityRowProjection =
        identity => new IdentityRow(
            identity.Id,
            identity.Provider,
            identity.ExternalPersonId,
            identity.EmployeeId,
            identity.DisplayNameInDevice,
            identity.Source,
            identity.Note,
            identity.CreatedAt);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Qidiruv — pass jurnalidagi bilan bir xil qoida: xodim sharti
    /// <c>EmployeeSearchQuery</c> dan, qurilma raqami esa ichida qidiriladi.
    /// </para>
    /// <para>
    /// Tartib xodim familiyasi, ismi va raqam bo'yicha: HR ro'yxatdan odamni
    /// topadi, raqamni emas. Buning uchun xodimlar jadvali bilan birlashtiriladi
    /// — o'chirilganlari bilan (<c>IgnoreQueryFilters</c>), aks holda o'chirilgan
    /// xodimning moslik yozuvi ro'yxatdan jimgina yo'qolardi.
    /// </para>
    /// </remarks>
    public async Task<AttendancePage<AttendanceIdentityDto>> SearchIdentitiesAsync(
        IdentitySearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var identities = _context.AttendanceIdentities.AsNoTracking();

        if (filter.EmployeeId is { } employeeId)
        {
            identities = identities.Where(identity => identity.EmployeeId == employeeId);
        }

        if (filter.Provider is { } provider)
        {
            identities = identities.Where(identity => identity.Provider == provider);
        }

        if (filter.Search is { } search)
        {
            var matching = EmployeeSearchQuery.MatchingIds(_context, search);
            var pattern = SearchPattern.For(search);

            identities = identities.Where(identity =>
                matching.Contains(identity.EmployeeId) ||
                EF.Functions.ILike(identity.ExternalPersonId, pattern, SearchPattern.Escape));
        }

        var total = await identities.CountAsync(cancellationToken).ConfigureAwait(false);

        /*
          ⚠️ `DeletedAt == null` shu yerda QO'LDA takrorlanadi, garchi global
          filtr allaqachon bo'lsa ham. Sabab: quyidagi birlashmada
          `IgnoreQueryFilters()` bor va EF uni BUTUN so'rovga qo'llaydi —
          ya'ni u xodimning filtri bilan birga MOSLIKNING filtrini ham
          o'chirardi. Natijada uzilgan moslik ro'yxatda ko'rinib turardi,
          `total` esa uni sanamasdi (sanoq alohida so'rov): bitta ekranda
          ikkita javob.

          Xodimning filtri esa ataylab o'chirilgan: o'chirilgan xodimning
          moslik yozuvi TARIX va u ro'yxatdan yo'qolmasligi kerak.
        */
        var ordered =
            from identity in identities.Where(identity => identity.DeletedAt == null)
            join employee in _context.Employees.IgnoreQueryFilters() on identity.EmployeeId equals employee.Id
            orderby employee.FullName.LastName, employee.FullName.FirstName, identity.ExternalPersonId, identity.Id
            select identity;

        var rows = await ordered
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .Select(IdentityRowProjection)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = await ToIdentityDtosAsync(rows, cancellationToken).ConfigureAwait(false);

        return new AttendancePage<AttendanceIdentityDto>(items, total);
    }

    /// <inheritdoc />
    public async Task<AttendanceIdentityDto?> FindIdentityAsync(
        Guid identityId,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.AttendanceIdentities
            .AsNoTracking()
            .Where(identity => identity.Id == identityId)
            .Select(IdentityRowProjection)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var items = await ToIdentityDtosAsync([row], cancellationToken).ConfigureAwait(false);
        return items[0];
    }

    /// <summary>Moslik qatorlarini xodim kartasi bilan to'ldiradi — bitta so'rov.</summary>
    private async Task<IReadOnlyList<AttendanceIdentityDto>> ToIdentityDtosAsync(
        List<IdentityRow> rows,
        CancellationToken cancellationToken)
    {
        var cards = await EmployeeCardsAsync(
                [.. rows.Select(row => row.EmployeeId).Distinct()],
                cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row =>
            {
                cards.TryGetValue(row.EmployeeId, out var card);

                return new AttendanceIdentityDto(
                    row.Id,
                    row.Provider,
                    row.ExternalPersonId,
                    row.EmployeeId,
                    card?.FullName,
                    card?.PhotoKey,
                    row.DisplayNameInDevice,
                    row.Source,
                    row.Note,
                    row.CreatedAt);
            }),
        ];
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Besh so'rov, sahifa hajmidan qat'i nazar: (1) guruhlar soni, (2) sahifa
    /// guruhlari — <c>ix_attendance_passes_unmatched</c> qisman indeksi aynan
    /// shu <c>GROUP BY</c> uchun; (3) har guruhning so'nggi o'tishi, (4) surati
    /// bor so'nggi o'tishi — ikkalasi ham guruh ichida bazada tanlanadi;
    /// (5) qurilma nomlari.
    /// </para>
    /// <para>
    /// «Birinchi/oxirgi ko'rilgan» — <c>occurred_at</c> (odam qachon o'tgan),
    /// <c>received_at</c> emas: HR «u odam qachondan beri kelyapti» deb so'raydi.
    /// </para>
    /// </remarks>
    public async Task<AttendancePage<UnmatchedGroupDto>> ListUnmatchedAsync(
        UnmatchedFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var groups = _context.AttendancePasses
            .AsNoTracking()
            .Where(pass => pass.ResolvedEmployeeId == null)
            .GroupBy(pass => new { pass.IdentityProvider, pass.RawPersonId })
            .Select(group => new
            {
                group.Key.IdentityProvider,
                group.Key.RawPersonId,
                PassCount = group.Count(),
                FirstSeenAt = group.Min(pass => pass.OccurredAt),
                LastSeenAt = group.Max(pass => pass.OccurredAt),
            });

        var total = await groups.CountAsync(cancellationToken).ConfigureAwait(false);

        var page = await groups
            .OrderByDescending(group => group.PassCount)
            .ThenByDescending(group => group.LastSeenAt)
            .ThenBy(group => group.RawPersonId)
            .ThenBy(group => group.IdentityProvider)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (page.Count == 0)
        {
            return new AttendancePage<UnmatchedGroupDto>([], total);
        }

        var rawIds = page.Select(group => group.RawPersonId).Distinct().ToList();
        var unmatched = _context.AttendancePasses
            .AsNoTracking()
            .Where(pass => pass.ResolvedEmployeeId == null && rawIds.Contains(pass.RawPersonId));

        // Har guruhdan BITTA qator bazada tanlanadi (ROW_NUMBER): oylab moslanmagan
        // odamning minglab o'tishi xotiraga olib kelinmaydi.
        var lastPasses = await unmatched
            .GroupBy(pass => new { pass.IdentityProvider, pass.RawPersonId })
            .Select(group => group
                .OrderByDescending(pass => pass.OccurredAt)
                .ThenByDescending(pass => pass.Id)
                .Select(pass => new { pass.IdentityProvider, pass.RawPersonId, pass.DeviceId })
                .First())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var samplePasses = await unmatched
            .Where(pass => pass.PhotoKey != null)
            .GroupBy(pass => new { pass.IdentityProvider, pass.RawPersonId })
            .Select(group => group
                .OrderByDescending(pass => pass.OccurredAt)
                .ThenByDescending(pass => pass.Id)
                .Select(pass => new { pass.IdentityProvider, pass.RawPersonId, pass.Id })
                .First())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var lastDevice = lastPasses.ToDictionary(pass => (pass.IdentityProvider, pass.RawPersonId), pass => pass.DeviceId);
        var sample = samplePasses.ToDictionary(pass => (pass.IdentityProvider, pass.RawPersonId), pass => pass.Id);

        var devices = await DeviceLabelsAsync(lastDevice.Values, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<UnmatchedGroupDto> items =
        [
            .. page.Select(group =>
            {
                var key = (group.IdentityProvider, group.RawPersonId);
                var deviceId = lastDevice[key];

                return new UnmatchedGroupDto(
                    group.IdentityProvider,
                    group.RawPersonId,
                    group.PassCount,
                    group.FirstSeenAt,
                    group.LastSeenAt,
                    deviceId,
                    devices.GetValueOrDefault(deviceId)?.Name,
                    sample.TryGetValue(key, out var passId) ? passId : null);
            }),
        ];

        return new AttendancePage<UnmatchedGroupDto>(items, total);
    }

    /// <summary>Import moslashida xodimning kerakli qismi.</summary>
    private sealed record ImportEmployee(
        Guid Id,
        string FullName,
        string? Pinfl,
        string? PhotoKey,
        string FoldedFullName,
        string FoldedLastFirst);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// ⭐ <b>Xodimlar BIR MARTA, xotiraga o'qiladi</b> (~2 400 qator, arxiv bilan
    /// — faqat identifikator, ism, JSHSHIR va rasm kaliti), keyin har bir qator
    /// lug'at bo'yicha moslanadi. Qatorma-qator so'rov 5 000 ta bo'lardi; «IN»
    /// ro'yxati bilan bitta so'rov esa yig'ilgan ism ustida indekssiz ifoda
    /// talab qilardi. Bu so'rovlar soni qatorlarga bog'liq emas: xodimlar,
    /// nomzodlarning munosabatlari va ularning bo'lim/lavozim nomlari.
    /// </para>
    /// <para>
    /// <b>Ism kaliti.</b> <c>SearchFolding.ForName</c> (қ/ғ/ҳ/ў/ё yig'iladi) va
    /// bo'shliqlar bittaga keltiriladi. Fayldagi ism ikki so'zdan iborat bo'lsa
    /// (otasining ismisiz — Verifix ro'yxatlarida odatiy), «familiya ism» bo'yicha
    /// solishtiriladi; uch va undan ortiq so'z bo'lsa — to'liq F.I.Sh. bo'yicha.
    /// Ikkala holatda ham moslik ANIQ, «ichida qidirish» emas: import qarorini
    /// operator tasdiqlaydi, lekin taklif noaniq bo'lmasligi kerak.
    /// </para>
    /// <para>
    /// O'chirilgan xodim nomzod bo'lmaydi (global filtr). Ishdan ketgan xodim
    /// esa BO'LADI: moslik shaxsga tegishli, va uning o'tishi aynan «ishdan
    /// ketgan odam kirdi» ogohlantirishini beradi (§6.5).
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<ImportCandidateMatch>> FindEmployeeCandidatesAsync(
        IReadOnlyList<ImportCandidateLookup> lookups,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lookups);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        if (lookups.Count == 0)
        {
            return [];
        }

        var rows = await _context.Employees
            .AsNoTracking()
            .Select(employee => new
            {
                employee.Id,
                employee.FullName.LastName,
                employee.FullName.FirstName,
                employee.FullName.MiddleName,
                employee.Pinfl,
                employee.PhotoKey,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var employees = rows.ConvertAll(row => new ImportEmployee(
            row.Id,
            FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
            row.Pinfl?.Value,
            row.PhotoKey,
            FoldName($"{row.LastName} {row.FirstName} {row.MiddleName}"),
            FoldName($"{row.LastName} {row.FirstName}")));

        var byId = employees.ToDictionary(employee => employee.Id);
        var byPinfl = employees
            .Where(employee => employee.Pinfl is not null)
            .ToLookup(employee => employee.Pinfl!, StringComparer.Ordinal);
        var byFullName = employees.ToLookup(employee => employee.FoldedFullName, StringComparer.Ordinal);
        var byLastFirst = employees.ToLookup(employee => employee.FoldedLastFirst, StringComparer.Ordinal);

        var found = lookups
            .Select(lookup =>
            {
                var pinfl = DigitsOnly(lookup.Pinfl);
                var name = FoldName(lookup.FullName);

                IReadOnlyList<ImportEmployee> nameMatches = name.Length == 0
                    ? []
                    : [.. (name.Count(symbol => symbol == ' ') == 1 ? byLastFirst[name] : byFullName[name])
                        .OrderBy(employee => employee.FullName, StringComparer.Ordinal)
                        .ThenBy(employee => employee.Id)
                        .Take(take)];

                IReadOnlyList<ImportEmployee> pinflMatches = pinfl.Length == 0
                    ? []
                    : [.. byPinfl[pinfl].Take(take)];

                var mapped = lookup.MappedEmployeeId is { } mappedId ? byId.GetValueOrDefault(mappedId) : null;

                return (Mapped: mapped, Pinfl: pinflMatches, Name: nameMatches);
            })
            .ToList();

        // Bo'lim va lavozim faqat NOMZOD bo'lgan xodimlar uchun — butun ro'yxat uchun emas.
        var candidateIds = found
            .SelectMany(match => match.Pinfl.Concat(match.Name).Append(match.Mapped))
            .OfType<ImportEmployee>()
            .Select(employee => employee.Id)
            .Distinct()
            .ToList();

        var employments = await EmploymentsAsync(candidateIds, cancellationToken).ConfigureAwait(false);
        var today = _clock.Today;

        var placements = candidateIds.ToDictionary(
            id => id,
            id =>
            {
                var list = employments.GetValueOrDefault(id) ?? [];

                // Bugungi asosiy munosabat; ishdan ketgan bo'lsa — eng so'nggisi.
                return PrimaryEmployment.Select(list, today)
                    ?? list.OrderByDescending(employment => employment.HireDate).FirstOrDefault();
            });

        var names = await CatalogNamesAsync(
                placements.Values.Select(employment => employment?.DepartmentId),
                placements.Values.Select(employment => employment?.PositionId),
                [],
                cancellationToken)
            .ConfigureAwait(false);

        ImportCandidateDto ToCandidate(ImportEmployee employee)
        {
            var employment = placements[employee.Id];

            return new ImportCandidateDto(
                employee.Id,
                employee.FullName,
                employee.Pinfl,
                names.Department(employment?.DepartmentId),
                names.Position(employment?.PositionId),
                employee.PhotoKey);
        }

        return
        [
            .. found.Select(match => new ImportCandidateMatch(
                match.Mapped is null ? null : ToCandidate(match.Mapped),
                [.. match.Pinfl.Select(ToCandidate)],
                [.. match.Name.Select(ToCandidate)])),
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> ExistingEmployeeIdsAsync(
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employeeIds);

        if (employeeIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids = employeeIds.Distinct().ToList();

        var existing = await _context.Employees
            .AsNoTracking()
            .Where(employee => ids.Contains(employee.Id))
            .Select(employee => employee.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return existing.ToHashSet();
    }

    /// <summary>Ismni solishtirish kaliti: yig'ilgan, kichik harfli, bo'shliqlari bittadan.</summary>
    /// <remarks>
    /// Yig'ish qoidasi qidiruvdagi bilan BIR XIL (<see cref="SearchFolding.ForName"/>):
    /// «Абдукодиров» deb yozilgan fayl «Абдуқодиров» xodimga mos kelishi kerak —
    /// HR rus klaviaturasida aynan shunday yozadi.
    /// </remarks>
    private static string FoldName(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : SearchFolding.ForName(string.Join(
                ' ',
                value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));

    /// <summary>Faqat raqamlar — Excel JSHSHIRni son qilib (bo'shliq yoki apostrof bilan) saqlashi mumkin.</summary>
    private static string DigitsOnly(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : SearchFolding.ForPhone(value);

    // ════════════════════════════════════════════════════════════
    //  7. Jadval biriktirmalari
    // ════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScheduleAssignmentDto>> ListAssignmentsAsync(
        AssignmentFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var assignments = _context.ScheduleAssignments.AsNoTracking();

        if (filter.Scope is { } scope)
        {
            assignments = assignments.Where(assignment => assignment.Scope == scope);
        }

        if (filter.EmployeeId is { } employeeId)
        {
            assignments = assignments.Where(assignment => assignment.EmployeeId == employeeId);
        }

        if (filter.DepartmentId is { } departmentId)
        {
            assignments = assignments.Where(assignment => assignment.DepartmentId == departmentId);
        }

        if (filter.PositionId is { } positionId)
        {
            assignments = assignments.Where(assignment => assignment.PositionId == positionId);
        }

        if (filter.OnDate is { } onDate)
        {
            assignments = assignments.Where(assignment =>
                assignment.EffectiveFrom <= onDate &&
                (assignment.EffectiveTo == null || assignment.EffectiveTo >= onDate));
        }

        var rows = await assignments
            .OrderBy(assignment => assignment.Scope)
            .ThenByDescending(assignment => assignment.EffectiveFrom)
            .ThenByDescending(assignment => assignment.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return await ToAssignmentDtosAsync(rows, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ScheduleAssignmentDto?> FindAssignmentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.ScheduleAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(assignment => assignment.Id == assignmentId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var items = await ToAssignmentDtosAsync([row], cancellationToken).ConfigureAwait(false);
        return items[0];
    }

    /// <summary>Biriktirmalarni nomlar bilan to'ldiradi — qatorlar sonidan qat'i nazar to'rtta so'rov.</summary>
    /// <remarks>
    /// Jadval nomi o'chirilganlari bilan: tirik biriktirma o'chirilgan jadvalga
    /// ishora qilmasligi kerak (§6.4), lekin qo'lda tahrirlangan bazada shunday
    /// bo'lsa, ekran bo'sh nom emas, haqiqiy nomni ko'rsatsin.
    /// </remarks>
    private async Task<IReadOnlyList<ScheduleAssignmentDto>> ToAssignmentDtosAsync(
        List<ScheduleAssignment> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var scheduleIds = rows.Select(row => row.WorkScheduleId).Distinct().ToList();

        var schedules = await _context.WorkSchedules
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(schedule => scheduleIds.Contains(schedule.Id))
            .Select(schedule => new { schedule.Id, schedule.Name })
            .ToDictionaryAsync(schedule => schedule.Id, schedule => schedule.Name, cancellationToken)
            .ConfigureAwait(false);

        var cards = await EmployeeCardsAsync(
                [.. rows.Where(row => row.EmployeeId is not null).Select(row => row.EmployeeId!.Value).Distinct()],
                cancellationToken)
            .ConfigureAwait(false);

        var names = await CatalogNamesAsync(
                rows.Select(row => row.DepartmentId),
                rows.Select(row => row.PositionId),
                [],
                cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new ScheduleAssignmentDto(
                row.Id,
                row.WorkScheduleId,
                schedules.GetValueOrDefault(row.WorkScheduleId),
                row.Scope,
                row.EmployeeId,
                row.EmployeeId is { } employeeId ? cards.GetValueOrDefault(employeeId)?.FullName : null,
                row.EmployeeId is { } photoOwnerId ? cards.GetValueOrDefault(photoOwnerId)?.PhotoKey : null,
                row.PositionId,
                names.Position(row.PositionId),
                row.DepartmentId,
                names.Department(row.DepartmentId),
                row.EffectiveFrom,
                row.EffectiveTo)),
        ];
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    /// <summary>Xodimning ro'yxatlarda ko'rinadigan qismi.</summary>
    private sealed record EmployeeCard(string FullName, string? PhotoKey);

    /// <summary>Ma'lumotnoma nomlari — sahifadagi identifikatorlar uchun.</summary>
    private sealed record CatalogNames(
        Dictionary<Guid, string> Departments,
        Dictionary<Guid, string> Positions,
        Dictionary<Guid, string> LegalEntities)
    {
        public string? Department(Guid? id) => id is { } key ? Departments.GetValueOrDefault(key) : null;

        public string? Position(Guid? id) => id is { } key ? Positions.GetValueOrDefault(key) : null;

        public string? LegalEntity(Guid? id) => id is { } key ? LegalEntities.GetValueOrDefault(key) : null;
    }

    /// <summary>Xodimlarning BARCHA munosabatlari (tugaganlari ham), xodim bo'yicha guruhlangan.</summary>
    /// <remarks>
    /// Hisoblash shartnomasi (<see cref="AttendanceDayBuilder.Build"/>): asosiy
    /// munosabat qamrov bo'yicha tanlanadi, shuning uchun chaqiruvchi sana
    /// bo'yicha oldindan kesmaydi — bir xodimda odatda bir-uchta yozuv.
    /// Mehnat yo'li hodisalari BILAN: sanadagi bo'lim/lavozim ulardan o'qiladi
    /// (<see cref="EmploymentPlacement"/>) — qayta hisoblash yuklovchisi bilan bir xil.
    /// </remarks>
    private async Task<Dictionary<Guid, List<Employment>>> EmploymentsAsync(
        List<Guid> employeeIds,
        CancellationToken cancellationToken)
    {
        if (employeeIds.Count == 0)
        {
            return [];
        }

        var employments = await _context.Employments
            .AsNoTracking()
            .Include(employment => employment.Events)
            .AsSplitQuery()
            .Where(employment => employeeIds.Contains(employment.EmployeeId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return employments
            .GroupBy(employment => employment.EmployeeId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    /// <summary>
    /// Jadval hal qiluvchisi — tungi hisoblash kutgan yuklash shartnomasi bilan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uchta so'rov: biriktirmalar ([from − 2, to + 2] ni qamraganlari —
    /// qo'shni sanadagi o'tishning kuni qo'shnilarning rejasiga qarab
    /// aniqlanadi), ularning jadvallari KUNLARI bilan, va bo'limlar (faqat
    /// bo'lim pog'onasida biriktirma bo'lsa).
    /// </para>
    /// <para>
    /// <b>Jadvallar va bo'limlar o'chirilganlari bilan</b>
    /// (<c>IgnoreQueryFilters</c>). Bo'limlar — daraxt yumshoq o'chirilgan
    /// ORALIQ bo'limda uzilib, ichki bo'lim xodimi ota bo'lim jadvalini
    /// jimgina yo'qotmasligi uchun. Jadvallar — tirik biriktirma ko'rsatgan
    /// jadval o'chirilmaydi (§6.4), lekin qo'lda tahrirlangan bazada shunday
    /// bo'lsa, <see cref="ScheduleResolver"/> istisno otadi va o'qish ekrani
    /// (tabel, taxta) hamma uchun yiqilardi; biriktirma amalda ekan, uning
    /// jadvali hisobga olinadi.
    /// </para>
    /// </remarks>
    private async Task<ScheduleResolver> LoadScheduleResolverAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var coverFrom = from.AddDays(-2);
        var coverTo = to.AddDays(2);

        var assignments = await _context.ScheduleAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.EffectiveFrom <= coverTo &&
                (assignment.EffectiveTo == null || assignment.EffectiveTo >= coverFrom))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var scheduleIds = assignments.Select(assignment => assignment.WorkScheduleId).Distinct().ToList();

        var schedules = scheduleIds.Count == 0
            ? []
            : await _context.WorkSchedules
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(schedule => schedule.Days)
                .Where(schedule => scheduleIds.Contains(schedule.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var departments = assignments.Exists(assignment => assignment.Scope == ScheduleScope.Department)
            ? await _context.Departments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)
            : [];

        /*
          Yillik to'rning kataklari — faqat shu oraliq uchun
          (SPEC-jadval-to'ri §B2). Ular naqshdan ustun turadi va ularsiz
          tabel bayram kunini oddiy ish kuni deb ko'rsatardi.
        */
        var dates = await _context.WorkScheduleDates
            .AsNoTracking()
            .Where(date => date.Date >= coverFrom && date.Date <= coverTo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Tasdiqlangan jadval so'rovlari — hal qiluvchining eng yuqori qatlami (SPEC-jadval-so'rovi §C2).
        var overrides = await ScheduleOverrideQuery
            .ApprovedAsync(_context, coverFrom, coverTo, cancellationToken)
            .ConfigureAwait(false);

        return new ScheduleResolver(assignments, schedules, new DepartmentTree(departments), dates, overrides);
    }

    /// <summary>Munosabatlarga firma / bo'lim (ichki bo'limlari bilan) / lavozim filtri.</summary>
    private async Task<IQueryable<Employment>> ApplyEmploymentOrgFilterAsync(
        IQueryable<Employment> employments,
        Guid? legalEntityId,
        Guid? departmentId,
        Guid? positionId,
        CancellationToken cancellationToken)
    {
        if (legalEntityId is { } legalEntity)
        {
            employments = employments.Where(employment => employment.LegalEntityId == legalEntity);
        }

        if (departmentId is { } department)
        {
            var branch = await BranchIdsAsync(department, cancellationToken).ConfigureAwait(false);

            employments = employments.Where(employment =>
                employment.DepartmentId != null && branch.Contains(employment.DepartmentId.Value));
        }

        if (positionId is { } position)
        {
            employments = employments.Where(employment => employment.PositionId == position);
        }

        return employments;
    }

    /// <summary>Kun yozuvlariga SURAT bo'yicha firma / bo'lim / lavozim filtri.</summary>
    private async Task<IQueryable<AttendanceDay>> ApplyDayOrgFilterAsync(
        IQueryable<AttendanceDay> days,
        Guid? legalEntityId,
        Guid? departmentId,
        Guid? positionId,
        CancellationToken cancellationToken)
    {
        if (legalEntityId is { } legalEntity)
        {
            days = days.Where(day => day.LegalEntityId == legalEntity);
        }

        if (departmentId is { } department)
        {
            var branch = await BranchIdsAsync(department, cancellationToken).ConfigureAwait(false);

            days = days.Where(day => day.DepartmentId != null && branch.Contains(day.DepartmentId.Value));
        }

        if (positionId is { } position)
        {
            days = days.Where(day => day.PositionId == position);
        }

        return days;
    }

    /// <summary>Bo'lim va uning BARCHA ichki bo'limlari.</summary>
    /// <remarks>
    /// Daraxt bo'yicha aylanish <c>DepartmentRepository</c> dagi rekursiv CTE
    /// bilan — <c>EmployeeReadModel.BranchIdsAsync</c> bilan aynan bir xil
    /// yo'l, «avlod» ning ikkinchi ta'rifi yozilmaydi.
    /// </remarks>
    private async Task<List<Guid>> BranchIdsAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var descendants = await _departments
            .GetDescendantIdsAsync(departmentId, cancellationToken)
            .ConfigureAwait(false);

        return [departmentId, .. descendants];
    }

    /// <summary>Xodimlar ismi va rasmi — o'chirilganlari bilan birga.</summary>
    private async Task<Dictionary<Guid, EmployeeCard>> EmployeeCardsAsync(
        List<Guid> employeeIds,
        CancellationToken cancellationToken)
    {
        if (employeeIds.Count == 0)
        {
            return [];
        }

        var rows = await _context.Employees
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(employee => employeeIds.Contains(employee.Id))
            .Select(employee => new
            {
                employee.Id,
                employee.FullName.LastName,
                employee.FullName.FirstName,
                employee.FullName.MiddleName,
                employee.PhotoKey,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(
            row => row.Id,
            row => new EmployeeCard(
                FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                row.PhotoKey));
    }

    /// <summary>Qurilma nomlari — o'chirilganlari bilan birga (jurnal o'tishning manbasini yo'qotmasin).</summary>
    /// <summary>Qurilmaning ekranga chiqadigan qismi.</summary>
    /// <remarks>
    /// <para>
    /// <b>Joy (<paramref name="Location"/>) — QURILMANING erkin matnli
    /// izohi</b>, ma'lumotnoma emas: tizimda «lokatsiya» degan yozuv turi
    /// yo'q (SPEC-panel §A, «out of scope»). Shu sababli u shunchaki
    /// ko'rsatiladi va hech qayerda filtr sifatida ishlatilmaydi.
    /// </para>
    /// </remarks>
    private sealed record DeviceLabel(string Name, string? Location, AttendanceProvider Provider);

    /// <summary>Qurilmalarning nomi, joyi va turi — barcha jurnal ekranlari uchun bitta so'rov.</summary>
    private async Task<Dictionary<Guid, DeviceLabel>> DeviceLabelsAsync(
        IEnumerable<Guid> deviceIds,
        CancellationToken cancellationToken)
    {
        var ids = deviceIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await _context.AttendanceDevices
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(device => ids.Contains(device.Id))
            .Select(device => new
            {
                device.Id,
                Label = new DeviceLabel(device.Name, device.LocationNote, device.Provider),
            })
            .ToDictionaryAsync(device => device.Id, device => device.Label, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Bo'lim, lavozim va firma nomlari — faqat berilgan identifikatorlar, o'chirilganlari bilan.</summary>
    private async Task<CatalogNames> CatalogNamesAsync(
        IEnumerable<Guid?> departmentIds,
        IEnumerable<Guid?> positionIds,
        IEnumerable<Guid?> legalEntityIds,
        CancellationToken cancellationToken)
    {
        var departments = Distinct(departmentIds);
        var positions = Distinct(positionIds);
        var legalEntities = Distinct(legalEntityIds);

        return new CatalogNames(
            departments.Count == 0
                ? []
                : await _context.Departments
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(item => departments.Contains(item.Id))
                    .Select(item => new { item.Id, item.Name })
                    .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken)
                    .ConfigureAwait(false),
            positions.Count == 0
                ? []
                : await _context.Positions
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(item => positions.Contains(item.Id))
                    .Select(item => new { item.Id, item.Name })
                    .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken)
                    .ConfigureAwait(false),
            legalEntities.Count == 0
                ? []
                : await _context.LegalEntities
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(item => legalEntities.Contains(item.Id))
                    .Select(item => new { item.Id, item.Name })
                    .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken)
                    .ConfigureAwait(false));

        static List<Guid> Distinct(IEnumerable<Guid?> ids) => [.. ids.OfType<Guid>().Distinct()];
    }

    private static IOrderedQueryable<TRow> By<TRow, TKey>(
        IQueryable<TRow> source,
        Expression<Func<TRow, TKey>> key,
        bool descending)
        => descending ? source.OrderByDescending(key) : source.OrderBy(key);

    private static IOrderedQueryable<TRow> Then<TRow, TKey>(
        IOrderedQueryable<TRow> source,
        Expression<Func<TRow, TKey>> key,
        bool descending)
        => descending ? source.ThenByDescending(key) : source.ThenBy(key);
}
