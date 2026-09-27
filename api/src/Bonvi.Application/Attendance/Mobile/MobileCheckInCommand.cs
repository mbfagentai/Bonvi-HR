using System.Globalization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Mobile;

/// <summary>Telefondan belgilash so'rovi.</summary>
/// <param name="Direction">Kirish yoki chiqish — odamning O'ZI aytadi.</param>
/// <param name="Latitude">Telefon bergan kenglik.</param>
/// <param name="Longitude">Telefon bergan uzunlik.</param>
/// <param name="AccuracyMeters">Telefon bergan aniqlik, metr.</param>
/// <remarks>
/// ⚠️ Uchala son ham NULLABLE va bu ataylab: yuborilmagan maydon endi
/// jimgina 0 bo'lib bog'lanmaydi. Ilgari aniqligi yo'q so'rov «±0 m —
/// mukammal o'lchov» bo'lib 100 metrlik chegaradan o'tib ketardi, yo'q
/// koordinata esa ekvatordagi (0; 0) nuqtaga aylanardi. Yo'q qiymat —
/// <c>davomat.joylashuv_notogri</c>.
/// </remarks>
public sealed record MobileCheckInCommand(
    PassDirection Direction,
    decimal? Latitude,
    decimal? Longitude,
    decimal? AccuracyMeters);

/// <summary>
/// Telefondan kelgan belgini qabul qiladi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Natija — oddiy o'tish</b> (<see cref="AttendancePass"/>), ya'ni
/// tabel, jonli taxta, hisobotlar va eksport uchun hech qanday yangi qoida
/// kerak emas. Telefon belgisi terminal o'tishidan faqat uchta ustun bilan
/// farq qiladi: <c>method = Mobile</c>, <c>direction_source = Employee</c>
/// va qurilma sifatida «Mobil ilova» qatori.
/// </para>
/// <para>
/// ⭐ <b>Tekshiruv TARTIBI SPEC §E dagidek va u ahamiyatli:</b> avval
/// «senga umuman ruxsat bormi» (403), keyin «o'lchoving yaroqlimi»,
/// keyin «qayerdasan», eng oxirida «yaqinda belgilaganmisan». Teskari
/// tartibda ruxsati yo'q odam avval joylashuvi haqida xabar olardi — ya'ni
/// mavjud obyektlar haqida ma'lumot sizib chiqardi.
/// </para>
/// <para>
/// ⭐ <b>Qulf takror tekshiruvidan OLDIN va BITTA tranzaksiya ichida</b>
/// (SPEC §K6). Takror qoidasi «o'qib, keyin yozadi»: ikki bosish 1,2 soniya
/// farq bilan kelsa, ular unikal indeksdan o'tib ketadi (kalitdagi vaqt
/// soniyagacha yaxlitlangan) va ikkala so'rov ham «yaqinda belgi yo'q»
/// degan javobni o'qiy oladi. Hisob qatoriga qo'yilgan
/// <c>SELECT … FOR UPDATE</c> bitta xodimning belgilarini faqat
/// bir-biriga nisbatan navbatga qo'yadi — lekin faqat qulf <c>COMMIT</c>
/// gacha tursa. Shu sababli qulfdan saqlashgacha bo'lgan hamma ish
/// <see cref="IUnitOfWork.ExecuteInTransactionAsync{TResult}"/> ichida:
/// ilgari qulf tranzaksiyasiz olinib, so'rov tugashi bilan bo'shardi va
/// ikkinchi bosish ikkinchi o'tish yozardi.
/// </para>
/// <para>
/// Unikal indeks (<c>ux_attendance_passes_device_dedupe</c>) to'qnashuvi
/// shunga qaramay yuz bersa (masalan, saqlash bazada bajarilib, javobi
/// yo'qolgan va ish qayta urinilgan), u 500 emas —
/// <c>davomat.takroriy_belgi</c> (409): birinchi belgi baribir saqlangan.
/// </para>
/// <para>
/// ⚠️ <b>Ishdan ketgan xodim ham belgilay olmaydi</b> — mehnat munosabati
/// BUGUNGI sana bo'yicha tekshiriladi (kirish va yangilashdagi INV-2 bilan
/// bir xil qoida). Usiz access token amal qiladigan 15 daqiqa davomida
/// bo'shatilgan odam hali ham «ishga keldim» deb belgilay olardi.
/// </para>
/// <para>
/// ⚠️ <b>Vaqt Toshkent siljishida uzatiladi</b> (<c>BonviTime</c>), UTC da
/// emas. <see cref="AttendancePass.Record"/> bitta argumentdan beshta
/// ustun yasaydi: UTC bo'lsa jurnal har bir telefon belgisini besh soat
/// oldin ko'rsatardi va soat 19:00 dan keyingi belgi tabelda BOSHQA KUNGA
/// tushardi.
/// </para>
/// <para>
/// Rad etilgan urinish saqlanmaydi: har bir bosish qator yasaydigan jadval
/// ochiq suiiste'mol kanali bo'lardi. Sabab logga yoziladi — endpointda
/// (<c>MeEndpoints</c>), chunki Application qatlami loglash kutubxonasiga
/// bog'lanmagan; kod va qiymatlar esa xatoning o'zida
/// (<see cref="Error.Details"/>).
/// </para>
/// </remarks>
public sealed class MobileCheckInCommandHandler
{
    private readonly ICurrentEmployee _currentEmployee;
    private readonly IEmployeeAccountRepository _accounts;
    private readonly IEmployeeRepository _employees;
    private readonly IEmploymentRepository _employments;
    private readonly IReferenceRepository<WorkSite> _sites;
    private readonly IAttendancePassRepository _passes;
    private readonly IMobileCheckInRepository _checkIns;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDatabaseFault _databaseFault;
    private readonly IClock _clock;

    public MobileCheckInCommandHandler(
        ICurrentEmployee currentEmployee,
        IEmployeeAccountRepository accounts,
        IEmployeeRepository employees,
        IEmploymentRepository employments,
        IReferenceRepository<WorkSite> sites,
        IAttendancePassRepository passes,
        IMobileCheckInRepository checkIns,
        IUnitOfWork unitOfWork,
        IDatabaseFault databaseFault,
        IClock clock)
    {
        _currentEmployee = currentEmployee;
        _accounts = accounts;
        _employees = employees;
        _employments = employments;
        _sites = sites;
        _passes = passes;
        _checkIns = checkIns;
        _unitOfWork = unitOfWork;
        _databaseFault = databaseFault;
        _clock = clock;
    }

    public async Task<Result<MobileCheckInResultDto>> HandleAsync(
        MobileCheckInCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Direction is not (PassDirection.In or PassDirection.Out))
        {
            // «Noma'lum» yo'nalish faqat QURILMA aytmaganda tug'iladi. Odam
            // esa ikkita tugmadan birini bosadi — uchinchi holat yo'q.
            return Error.Validation("davomat.yonalish_notogri", "Yo'nalish «Kirish» yoki «Chiqish» bo'lishi kerak.");
        }

        if (_currentEmployee.AccountId is not { } accountId)
        {
            return AttendanceErrors.MobileCheckInNotAllowed;
        }

        // Qulf, o'qishlar va saqlash — BITTA tranzaksiyada (sinf izohi).
        return await _unitOfWork
            .ExecuteInTransactionAsync(token => CheckInAsync(accountId, command, token), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Qulfdan saqlashgacha — tranzaksiya ichida bajariladigan qism.</summary>
    /// <remarks>
    /// Ish qayta urinilishi mumkin (vaqtinchalik baza nosozligi), shu sababli
    /// hamma narsa shu yerda boshidan o'qiladi — tashqaridan yuklangan
    /// obyektga tayanilmaydi.
    /// </remarks>
    private async Task<Result<MobileCheckInResultDto>> CheckInAsync(
        Guid accountId,
        MobileCheckInCommand command,
        CancellationToken cancellationToken)
    {
        // ── 1-qadam: hisob va ruxsat (SPEC §E1) ──────────────────────
        //
        // Qulf AYNAN shu yerda olinadi, keyinroq emas: u tranzaksiya oxirigacha
        // turadi va 4-qadamdagi «o'qib, keyin yoz» ni qo'riqlaydi. Qatorning
        // o'zi baribir kerak edi, ya'ni qo'shimcha so'rov qo'shilmayapti.
        var account = await _accounts.LockAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (account is null || !account.IsActive)
        {
            return AttendanceErrors.MobileCheckInNotAllowed;
        }

        var employee = await _employees.GetByIdAsync(account.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (employee is null || !employee.MobileCheckInAllowed)
        {
            return AttendanceErrors.MobileCheckInNotAllowed;
        }

        var now = _clock.UtcNow;

        // Ishdan ketgan (yoki hali ishga kirmagan) odam — sana bo'yicha,
        // kirish yo'lidagi INV-2 bilan AYNAN bir xil qoida (sinf izohi).
        if (!await _employments
                .IsEmployedOnAsync(employee.Id, now.TashkentDate(), cancellationToken)
                .ConfigureAwait(false))
        {
            return AttendanceErrors.MobileCheckInNotAllowed;
        }

        // ── 2-qadam: o'lchovning o'zi va aniqligi (SPEC §E2) ─────────
        //
        // Yo'q, nol yoki manfiy aniqlik — «mukammal o'lchov» emas, YAROQSIZ
        // o'lchov. Chegaradan tashqari koordinata ham shu kod bilan.
        if (command.Latitude is not { } rawLatitude
            || command.Longitude is not { } rawLongitude
            || command.AccuracyMeters is not { } rawAccuracy)
        {
            return AttendanceErrors.LocationInvalid;
        }

        var latitude = Round(rawLatitude, MobileCheckInRules.CoordinateScale);
        var longitude = Round(rawLongitude, MobileCheckInRules.CoordinateScale);
        var accuracy = Round(rawAccuracy, MobileCheckInRules.MeterScale);

        // Yaxlitlangandan KEYIN: 0,04 m kabi son 0 bo'ladi — u ham «mukammal» emas.
        if (accuracy <= 0 || latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return AttendanceErrors.LocationInvalid;
        }

        if (accuracy > MobileCheckInRules.MaxAccuracyMeters)
        {
            return AttendanceErrors.LocationTooCoarse(accuracy);
        }

        // ── 3-qadam: qabul qiladigan FAOL obyekt (SPEC §E3, SPEC-joylashuvlar §B) ──
        //
        // Yigirmagacha qator o'qilib, xotirada solishtiriladi: bu hajmda
        // hech qanday indeks «yigirma qatorni olib, siklda yurish» dan
        // tez bo'la olmaydi (sababi ma'lumot modeli §A.4 da). Yumshoq
        // o'chirilganlar global filtr bilan, faolsizlari `onlyActive` bilan
        // chiqib ketadi — ikkalasi ikki xil gap.
        //
        // Doira va ko'pburchakni birga tartiblash qoidasi domenda
        // (`Geofence.Locate`) — bu yerda faqat natija o'qiladi.
        var sites = await _sites.ListAsync(onlyActive: true, cancellationToken).ConfigureAwait(false);

        var reading = Geofence.Locate(sites, latitude, longitude);

        if (reading is null)
        {
            return AttendanceErrors.OutsideWorkSite(null, null);
        }

        if (!reading.Accepted)
        {
            // CHEGARAGACHA masofa, markazgacha emas: «Zavoddan 120 m
            // tashqaridasiz» — odamga kerak bo'lgan son shu.
            return AttendanceErrors.OutsideWorkSite(reading.Site.Name, reading.OutsideMeters);
        }

        // ── 4-qadam: takroriy belgi (SPEC §E4) ───────────────────────
        var bounceFrom = now.AddSeconds(-AttendanceRules.BounceSeconds);

        var lastPassAt = await _passes
            .LastPassAtAsync(employee.Id, bounceFrom, cancellationToken)
            .ConfigureAwait(false);

        if (lastPassAt is { } recent)
        {
            return AttendanceErrors.CheckInTooSoon(recent);
        }

        // ── Yozish ───────────────────────────────────────────────────
        var occurredAt = now.ToTashkent();

        var recorded = AttendancePass.Record(
            AttendanceDevice.MobileAppId,
            AttendanceIdentityProvider.Mobile,

            // `raw_person_id` — xodimning O'Z identifikatori: u har doim bor,
            // barqaror va boshqa raqamlar fazosi bilan chalkashmaydi
            // (sababi `AttendanceIdentityProvider.Mobile` izohida).
            employee.Id.ToString(),
            occurredAt,
            now,
            DedupeKey(employee.Id, occurredAt, command.Direction),
            command.Direction,
            DirectionSource.Employee,
            PassMethod.Mobile,

            // Siljish TAXMIN qilinmadi — server uni O'ZI tanladi.
            offsetAssumed: false);

        if (recorded.IsFailure)
        {
            return recorded.Error;
        }

        var pass = recorded.Value;

        // Moslik qabul lahzasida ma'lum: odam tizimga kirgan. Shu sababli
        // telefon belgisi HECH QACHON «bog'lanmagan o'tishlar» ekraniga
        // tushmaydi — bu qurilish bo'yicha shunday.
        pass.Resolve(employee.Id, now);

        _passes.Add(pass);

        // Dalil qatoriga QAROR juftligi emas, DALIL juftligi yoziladi —
        // farqi `GeofenceReading` izohida.
        _checkIns.Add(MobileCheckIn.Record(
            pass.Id,
            employee.Id,
            reading.Site.Id,
            latitude,
            longitude,
            accuracy,
            Round((decimal)reading.FrozenDistanceMeters, MobileCheckInRules.MeterScale),
            reading.FrozenAllowanceMeters,
            reading.Site.Shape,
            now));

        // `processed_at` NULL bo'lib qoladi — kun shu bilan qayta hisoblash
        // navbatiga tushadi, xuddi terminal o'tishi kabi. Alohida navbatga
        // qo'yish chaqiruvi KERAK EMAS va qo'shilmaydi ham: ikkinchi yo'l
        // birinchisidan ajralib ketishi mumkin bo'lgan joy bo'lardi.
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (_databaseFault.IsDuplicatePass(exception))
        {
            // Shu soniyadagi belgi allaqachon saqlangan. Qo'shilgan (bazaga
            // yetmagan) o'tish kuzatuvdan chiqariladi — ingest yo'lidagi bilan
            // bir xil. Tranzaksiya esa savepoint'ga qaytgan, ya'ni buzilmagan
            // va keyingi o'qish ishlaydi (`AppDbContext` izohi).
            _passes.Remove(pass);

            var saved = await _passes
                .LastPassAtAsync(employee.Id, bounceFrom, cancellationToken)
                .ConfigureAwait(false);

            return AttendanceErrors.CheckInTooSoon(saved ?? occurredAt);
        }

        return new MobileCheckInResultDto(
            pass.OccurredAt,
            pass.Direction,
            reading.Site.Name);
    }

    /// <summary>
    /// Takrorni ushlash kaliti — qabul yo'lidagi <c>t:</c> formatining AYNAN o'zi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Yangi format o'ylab topilmadi: <c>(device_id, dedupe_key)</c> unikal
    /// indeksi bitta bo'lgani uchun ikki xil format bir jadvalda yashashi
    /// kerak bo'lardi va ularning to'qnashmasligini hech kim tekshirmasdi.
    /// Barcha telefon belgilari bitta <c>device_id</c> ni bo'lishadi, ya'ni
    /// kalitdagi xodim identifikatori ikki kishining bir lahzadagi
    /// belgisini ajratib turadi.
    /// </para>
    /// <para>
    /// ⚠️ Mijoz bergan kalit (Hikvision uchun mavjud <c>s:</c> shakli) RAD
    /// ETILDI: bu yerda mijoz — xodimning telefoni, ya'ni dushmanlik
    /// qilishi mumkin bo'lgan kirish. Yangi kalit bilan qayta yuborish
    /// takror indeksidan bemalol o'tib ketardi.
    /// </para>
    /// </remarks>
    private static string DedupeKey(Guid employeeId, DateTimeOffset occurredAt, PassDirection direction)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"t:{employeeId}|{occurredAt.ToUniversalTime():yyyyMMdd'T'HHmmss}|{(int)direction}");

    /// <summary>
    /// Ustun sig'imiga yaxlitlaydi.
    /// </summary>
    /// <remarks>
    /// APPLICATION da, bazada emas: <c>numeric(9,6)</c> ga sig'maydigan son
    /// bilan borish tushunarsiz 500 berardi. Yaxlitlash o'zi hech narsa
    /// yo'qotmaydi — oltita kasr xonasi ~8 sm, GPS esa eng yaxshi holatda
    /// 5 m beradi.
    /// </remarks>
    private static decimal Round(decimal value, int scale)
        => Math.Round(value, scale, MidpointRounding.AwayFromZero);
}
