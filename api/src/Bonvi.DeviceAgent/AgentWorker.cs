using System.Globalization;
using Bonvi.DeviceAgent.Bonvi;
using Bonvi.DeviceAgent.Hikvision;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bonvi.DeviceAgent;

/// <summary>
/// Agentning ikki halqasi: hodisalar va ro'yxat.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Ikkalasi ham TORTIB oladi (pull).</b> Ishlab chiqarish serveri
/// ofis tarmog'iga kira olmaydi va terminal ham tashqariga chiqmaydi —
/// ya'ni hech qayerda kiruvchi port ochilmaydi (SPEC-agent §C).
/// </para>
/// <para>
/// ⭐ <b>Ro'yxat halqasi — SOLIShTIRISH, navbat emas.</b> Agent «ishga
/// olindi», «rasm almashdi», «ishdan ketdi» xabarlarini qayta o'ynatmaydi:
/// u KERAKLI ro'yxatni (Bonvi) va HAQIQIY ro'yxatni (terminal) olib,
/// farqini tuzatadi. Shu sababli agent bir hafta o'chib tursa ham, yonishi
/// bilan holat o'z-o'zidan tiklanadi.
/// </para>
/// <para>
/// ⚠️ <b>Agent O'ZI yaratmagan odamga TEGMAYDI.</b> Terminal Verifix bilan
/// bo'lishiladi; o'chirish faqat Bonvi fazasidagi raqamga (9 bilan
/// boshlanadigan 9 belgi) qo'llanadi. Bu tekshiruvsiz birinchi
/// solishtirishning o'ziyoq Verifix ning barcha xodimlarini o'chirib
/// tashlagan bo'lardi.
/// </para>
/// </remarks>
internal sealed partial class AgentWorker : BackgroundService
{
    /// <summary>Yuz rasmining eng katta hajmi — terminal shundan kattasini rad etadi.</summary>
    /// <remarks>
    /// Bonvi rasmni 512×512 JPEG qilib saqlaydi (~40–120 KB), ya'ni odatda
    /// bu chegaraga umuman yaqinlashmaydi. Oshib ketsa — bu xato holat va
    /// u HR ekranida ko'rinishi kerak, agentning ichida jimgina
    /// tuzatilmasligi (loyiha fayli izohi).
    /// </remarks>
    private const int MaxFaceBytes = 200 * 1024;

    /// <summary>
    /// Hodisa va kadr so'rovlarining muddati.
    /// </summary>
    /// <remarks>
    /// Terminal ikkalasiga 0.1 soniyada javob beradi (2026-09-21 da
    /// o'lchangan). Sakkiz soniya — sekin tarmoq uchun zaxira bilan, lekin
    /// umumiy 30 soniyadan ancha qisqa. Yuz yozish esa umumiy muddatda
    /// qoladi: u haqiqatan sekin.
    /// </remarks>
    private static readonly TimeSpan QuickCallTimeout = TimeSpan.FromSeconds(8);

    /// <summary>Bitta qabul so'rovidagi eng ko'p hodisa — suratli hodisa ~110 KB.</summary>
    private const int IngestChunkSize = 10;

    /// <summary>Kadrning eng katta hajmi — qabul yo'li bitta suratga 1 MB beradi.</summary>
    private const int MaxSnapshotBytes = 900 * 1024;

    /// <summary>
    /// Qurilma soati biroz oldinda yursa, yangi hodisa «kelajakda» ko'rinadi —
    /// shuncha soniyagacha bu normal.
    /// </summary>
    private const int FutureToleranceSeconds = 2;

    /// <summary>Kursor yo'q bo'lganda qancha orqaga qaraladi.</summary>
    private static readonly TimeSpan ColdStart = TimeSpan.FromHours(1);

    /// <summary>
    /// Qidiruv oxirgi uzatilgan hodisadan shuncha OLDINDAN boshlanadi.
    /// </summary>
    /// <remarks>
    /// Bir soniyada bir nechta o'tish yoki terminal soatining ozgina
    /// orqaga surilishi uchun zaxira. Qayta o'qilganlar tartib raqami
    /// bo'yicha tashlanadi, serverga yetsa ham — takror sifatida.
    /// </remarks>
    private static readonly TimeSpan Overlap = TimeSpan.FromMinutes(2);

    /// <summary>Qidiruv oxiri terminalning «hozir»idan shuncha keyin.</summary>
    /// <remarks>
    /// Terminal soatining siljishi hali o'lchanmagan birinchi daqiqada
    /// ham oldinda yurgan soatning yangi o'tishi oraliqqa tushsin.
    /// </remarks>
    private static readonly TimeSpan FutureAllowance = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Bitta bo'lakda uzatiladigan eng ko'p YANGI hodisa.
    /// </summary>
    /// <remarks>
    /// Uzilishdan keyingi qoldiq shunday bo'laklarda ketadi va har
    /// bo'lakdan keyin kursor saqlanadi: o'rtada yana uzilish bo'lsa,
    /// keyingi urinish boshidan emas, to'xtagan joyidan davom etadi.
    /// </remarks>
    internal const int MaxEventsPerBatch = 300;

    /// <summary>
    /// Vaqti hozirdan shuncha kundan uzoq hodisa YUBORILMAYDI.
    /// </summary>
    /// <remarks>
    /// Server 30 kundan uzoq og'ishni rad etadi
    /// (<c>AttendanceRules.MaxClockSkewDays</c>) va bitta shunday hodisa
    /// BUTUN so'rovni yiqitadi — agent esa o'sha bo'lakni cheksiz qayta
    /// yuborib, undan keyingi hamma o'tishni ham tiqib qo'yardi. Bunday
    /// vaqt — buzilgan terminal soati (1970, 2000 yil), va u baribir
    /// qabul qilinmaydi: tashlanadi va jurnalga yoziladi. Bir kun zaxira —
    /// agent va server soatlari orasidagi farq uchun.
    /// </remarks>
    private const int MaxEventAgeDays = 29;

    private readonly HikvisionClient _device;
    private readonly BonviAgentClient _bonvi;
    private readonly AgentOptions _options;
    private readonly ILogger<AgentWorker> _logger;
    private readonly TimeProvider _time;

    private readonly AgentState _state;

    /// <summary>Terminal soati minus agent soati — oxirgi o'lchov (tik).</summary>
    /// <remarks>
    /// Ikki halqa ikki xil oqimda: biri yozadi (ro'yxat halqasi soatni
    /// o'lchaydi), ikkinchisi o'qiydi (hodisalar). Shu sababli
    /// <see cref="Volatile"/> orqali.
    /// </remarks>
    private long _deviceOffsetTicks;

    public AgentWorker(
        HikvisionClient device,
        BonviAgentClient bonvi,
        IOptions<AgentOptions> options,
        ILogger<AgentWorker> logger,
        TimeProvider time)
    {
        _device = device;
        _bonvi = bonvi;
        _options = options.Value;
        _logger = logger;
        _time = time;
        _state = AgentState.Load(_options.StatePath);
    }

    private TimeSpan DeviceOffset => TimeSpan.FromTicks(Volatile.Read(ref _deviceOffsetTicks));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Started(_options.DeviceHost, _options.EventsIntervalSeconds, _options.RosterIntervalSeconds);

        // Ikki halqa MUSTAQIL: ro'yxat tekshiruvi sekin (terminaldan
        // butun ro'yxat o'qiladi) va u hodisalarni kechiktirmasligi kerak.
        var events = LoopAsync(
            TimeSpan.FromSeconds(_options.EventsIntervalSeconds),
            ForwardEventsAsync,
            "hodisalar",
            stoppingToken);

        var roster = LoopAsync(
            TimeSpan.FromSeconds(_options.RosterIntervalSeconds),
            ReconcileAsync,
            "ro'yxat",
            stoppingToken);

        await Task.WhenAll(events, roster).ConfigureAwait(false);
    }

    /// <summary>Bitta halqa — xato butun jarayonni to'xtatmaydi.</summary>
    private async Task LoopAsync(
        TimeSpan interval,
        Func<CancellationToken, Task> step,
        string name,
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await step(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception failure)
            {
                // Tarmoq uzilishi, terminalning qayta yuklanishi, serverning
                // qayta chiqarilishi — hammasi shu yerga tushadi va halqa
                // keyingi urinishda o'zi tiklanadi.
                LoopFailed(name, failure);
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Terminal jurnalidagi yangi o'tishlarni Bonvi ga uzatadi.</summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Internet uzilsa o'tish yo'qolmaydi.</b> Terminal hodisani o'z
    /// jurnalida saqlaydi va o'qilgani uchun o'chirmaydi. Kursor faqat
    /// server QABUL QILGANDAN keyin suriladi, ya'ni aloqa qaytishi bilan
    /// qolgan hammasi yuboriladi. Qoldiq katta bo'lsa — bo'lakma-bo'lak
    /// (<see cref="MaxEventsPerBatch"/>) va TO'XTAMASDAN, 2 soniyalik
    /// kutishsiz.
    /// </para>
    /// <para>
    /// Kursor ikkita: tartib raqami va oxirgi hodisaning TERMINAL
    /// vaqti (<see cref="AgentState.LastEventTime"/>). Qidiruv oralig'i
    /// ikkinchisidan boshlanadi — agent soatidan emas.
    /// </para>
    /// </remarks>
    internal async Task ForwardEventsAsync(CancellationToken cancellationToken)
    {
        while (await ForwardBatchAsync(cancellationToken).ConfigureAwait(false))
        {
            // Terminalda hali o'qilmagan qoldiq bor — keyingi bo'lak darhol.
        }
    }

    /// <summary>Bitta bo'lak: o'qish → uzatish → kursorni saqlash.</summary>
    /// <returns>Terminalda hali o'qilmagan qoldiq qolganmi.</returns>
    private async Task<bool> ForwardBatchAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var deviceNow = now + DeviceOffset;

        var fresh = new List<AcsEvent>();
        var complete = true;

        var pages = _device.SearchEventsAsync(
            SearchStart(deviceNow) - Overlap,
            deviceNow + FutureAllowance,
            QuickCallTimeout,
            cancellationToken);

        await foreach (var page in pages.ConfigureAwait(false))
        {
            fresh.AddRange(page.Where(IsNew));

            if (fresh.Count >= MaxEventsPerBatch)
            {
                complete = false;
                break;
            }
        }

        if (fresh.Count == 0)
        {
            // Yangi narsa yo'q — kursorga TEGILMAYDI: qidiruvning boshi
            // oxirgi uzatilgan hodisaga bog'langan, «hozir»ga emas.
            return false;
        }

        fresh = fresh
            .DistinctBy(item => (item.SerialNo, item.Time))
            .OrderBy(item => EventTime(item) ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.SerialNo)
            .ToList();

        // Bo'sh shaxs raqami — eshik holati yoki tanilmagan yuz, o'tish
        // EMAS (`AcsEvent.EmployeeNoString` izohi).
        var passes = new List<(AcsEvent Item, DateTimeOffset Time)>();
        var implausible = 0;

        foreach (var item in fresh)
        {
            if (string.IsNullOrWhiteSpace(item.EmployeeNoString) || EventTime(item) is not { } time)
            {
                continue;
            }

            if (!Plausible(time, now))
            {
                implausible++;
                continue;
            }

            passes.Add((item, time));
        }

        if (implausible > 0)
        {
            ImplausibleSkipped(implausible, MaxEventAgeDays);
        }

        /*
          Jonli kadr — BITTA, faqat yangi o'tish bo'lsa. Kadr O'QISH
          lahzasida olinadi, shu sababli u faqat bir necha soniya oldingi
          o'tishga biriktiriladi (`SnapshotMaxAgeSeconds` izohi): agent
          uzilib qaytganda yuboriladigan eski o'tishlar suratsiz qoladi,
          noto'g'ri surat bilan emas.
        */
        var photo = passes.Any(pass => IsFresh(pass.Time, deviceNow))
            ? await SnapshotAsync(cancellationToken).ConfigureAwait(false)
            : null;

        var events = passes
            .Select(pass => new IngestEvent(
                pass.Item.EmployeeNoString!,
                pass.Item.Time!,
                "Unknown",
                Method(pass.Item.CurrentVerifyMode),
                EventId(pass.Item.SerialNo, pass.Time),
                photo is not null && IsFresh(pass.Time, deviceNow) ? photo : null))
            .ToList();

        /*
          Bo'laklab: suratli hodisa ~110 KB, qabul yo'lining tana chegarasi
          esa 2 MB. O'nta hodisa — chegaradan ancha past, ertalabki
          navbatda ham. Bo'laklardan biri yiqilsa kursor surilmaydi va
          hammasi qayta yuboriladi — server takrorni o'zi tashlaydi.
        */
        foreach (var chunk in events.Chunk(IngestChunkSize))
        {
            var result = await _bonvi.IngestAsync(chunk, cancellationToken).ConfigureAwait(false);

            Forwarded(chunk.Length, result.Stored, result.Duplicates);
        }

        /*
          Kursor FAQAT muvaffaqiyatli uzatishdan keyin suriladi: server
          javob bermasa, o'sha hodisalar keyingi urinishda qayta
          yuboriladi. Takror zararsiz (server o'zi tashlaydi), yo'qolgan
          o'tish esa tabelda tuzatib bo'lmaydigan teshik qoldirardi.

          Ikkala qiymat ham faqat O'SADI: terminal soati orqaga surilgan
          bo'lsa, kursor u bilan birga orqaga qaytmaydi (aks holda eski va
          yangi hodisa navbat bilan «yangi» bo'lib, cheksiz qayta
          yuborilardi). Vaqti aql bovar qilmaydiganlari kursorni
          siljitmaydi — 2106-yilli bitta hodisa qidiruvni kelajakka
          surib, keyingi hammasini yo'qotardi.
        */
        _state.LastSerialNo = Math.Max(_state.LastSerialNo, fresh.Max(item => item.SerialNo));

        var newest = fresh
            .Select(EventTime)
            .Where(time => time is { } value && Plausible(value, now))
            .Max();

        if (newest is { } latest && (_state.LastEventTime is not { } last || latest > last))
        {
            _state.LastEventTime = latest;
        }

        _state.Save(_options.StatePath);

        return !complete;
    }

    /// <summary>
    /// Qidiruvning boshi — terminal soatida.
    /// </summary>
    /// <remarks>
    /// Oxirgi uzatilgan hodisa, lekin terminalning «hozir»idan KEYIN emas:
    /// terminal soati orqaga to'g'rilansa (masalan, bir soatga), yangi
    /// o'tishlar eski kursordan oldin yoziladi va qidiruv ularni o'tkazib
    /// yuborardi. Siljish har daqiqada o'lchanadi (<see cref="ReportClockAsync"/>).
    /// </remarks>
    private DateTimeOffset SearchStart(DateTimeOffset deviceNow)
    {
        var anchor = _state.LastEventTime
            ?? (_state.LastReadAt is { } read ? read - ColdStart : deviceNow - ColdStart);

        return anchor < deviceNow ? anchor : deviceNow;
    }

    /// <summary>Hodisa hali uzatilmaganmi.</summary>
    /// <remarks>
    /// Tartib raqami kursordan katta — yangi. Vaqti oxirgi uzatilgan
    /// hodisadan KECH — ham yangi, raqamidan qat'i nazar: jurnal
    /// tozalanib raqamlar 1 dan qayta boshlansa, aks holda terminal
    /// hech qachon eski raqamga yetmasdi va hamma o'tish jimgina
    /// tashlanardi.
    /// </remarks>
    private bool IsNew(AcsEvent item)
        => item.SerialNo > _state.LastSerialNo
           || (_state.LastEventTime is { } last && EventTime(item) is { } time && time > last);

    /// <summary>
    /// Qabul yo'liga ketadigan hodisa kaliti — raqam VA vaqt.
    /// </summary>
    /// <remarks>
    /// Server takrorni shu kalit bo'yicha tashlaydi. Faqat raqam bo'lsa,
    /// jurnal tozalangandan keyingi yangi «5-hodisa» oylar oldingi
    /// «5-hodisa» ning takrori deb jimgina tashlanardi.
    /// </remarks>
    internal static string EventId(long serialNo, DateTimeOffset time)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"minmoe-{serialNo}-{time:yyyyMMddHHmmss}");

    private static DateTimeOffset? EventTime(AcsEvent item)
        => DateTimeOffset.TryParse(item.Time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : null;

    private static bool Plausible(DateTimeOffset time, DateTimeOffset now)
        => Math.Abs((now - time).TotalDays) <= MaxEventAgeDays;

    /// <summary>
    /// Kerakli ro'yxat bilan terminaldagi haqiqiy ro'yxatni solishtiradi.
    /// </summary>
    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        /*
          Soat o'lchovi shu halqada: u hodisalarga bog'liq emas va
          daqiqada bir marta yetarli. Xatosi butun tekshiruvni
          to'xtatmasligi kerak — terminal vaqtni bermasa ham odamlarni
          yozish davom etadi.
        */
        await ReportClockAsync(cancellationToken).ConfigureAwait(false);

        var roster = await _bonvi.RosterAsync(cancellationToken).ConfigureAwait(false);
        var people = await _device.ListPersonsAsync(cancellationToken).ConfigureAwait(false);

        var onDevice = people
            .Where(person => !string.IsNullOrEmpty(person.EmployeeNo))
            .ToDictionary(person => person.EmployeeNo, StringComparer.Ordinal);

        var report = new List<ReportItem>();

        foreach (var entry in roster.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var exists = onDevice.TryGetValue(entry.PersonNo, out var person);

            // Yuzi yo'q odam ham yangilanishi kerak: terminal odamni
            // ko'rsatadi, lekin yuzsiz u eshikni ocholmaydi.
            var faceMissing = exists && person!.NumOfFace is 0;

            if (exists && !entry.Force && !faceMissing)
            {
                continue;
            }

            report.Add(await WriteAsync(entry, exists, cancellationToken).ConfigureAwait(false));
        }

        /*
          Ortiqchalar — FAQAT Bonvi fazasidagilar. Verifix ning odamlari
          bu tekshiruvda ko'rinmaydi ham (sinf izohidagi qoida).
        */
        var expected = roster.Entries.Select(entry => entry.PersonNo).ToHashSet(StringComparer.Ordinal);

        foreach (var person in onDevice.Keys.Where(IsManaged).Where(number => !expected.Contains(number)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            report.Add(await RemoveAsync(person, cancellationToken).ConfigureAwait(false));
        }

        await _bonvi.ReportAsync(report, cancellationToken).ConfigureAwait(false);

        if (report.Count > 0)
        {
            Reconciled(roster.Entries.Count, report.Count);
        }
    }

    /// <summary>Terminal soatini o'lchab, serverga yuboradi.</summary>
    /// <remarks>
    /// Serverga SOLIShTIRISH uchun terminalning o'z vaqti yuboriladi,
    /// tayyor farq emas: agentning o'z soati ham adashishi mumkin va
    /// o'shanda xato ikki marta qo'shilardi.
    /// </remarks>
    internal async Task ReportClockAsync(CancellationToken cancellationToken)
    {
        try
        {
            var deviceTime = await _device.ReadClockAsync(cancellationToken).ConfigureAwait(false);

            if (deviceTime is { } time)
            {
                // Hodisalar qidiruvi shu siljish bilan terminal soatiga
                // o'tkaziladi (`SearchStart`). Server bilan aloqa bo'lmasa
                // ham o'lchov saqlanadi — u agentning o'ziga kerak.
                Volatile.Write(ref _deviceOffsetTicks, (time - _time.GetUtcNow()).Ticks);

                await _bonvi.ReportClockAsync(time, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception failure) when (failure is HikvisionException or HttpRequestException)
        {
            ClockFailed(failure.Message);
        }
    }

    /// <summary>Bitta xodimni terminalga yozadi — odam va yuz.</summary>
    private async Task<ReportItem> WriteAsync(RosterEntry entry, bool exists, CancellationToken cancellationToken)
    {
        try
        {
            await _device
                .WritePersonAsync(entry.PersonNo, entry.FullName, exists, cancellationToken)
                .ConfigureAwait(false);

            var photo = await _bonvi.PhotoAsync(entry.EmployeeId, cancellationToken).ConfigureAwait(false);

            if (photo is null)
            {
                // Server rasmsiz xodimni ro'yxatga qo'shmaydi — bu yerga
                // tushish poyga demak: ro'yxat olingandan keyin rasm
                // o'chirilgan.
                return new ReportItem(entry.EmployeeId, entry.PersonNo, AgentEnrollmentAction.Upsert, Ok: false, entry.Version, "Rasm topilmadi.");
            }

            if (photo.Length > MaxFaceBytes)
            {
                return new ReportItem(
                    entry.EmployeeId,
                    entry.PersonNo,
                    AgentEnrollmentAction.Upsert,
                    Ok: false,
                    entry.Version,
                    $"Rasm juda katta: {photo.Length / 1024} KB.");
            }

            await _device
                .WriteFaceAsync(entry.PersonNo, photo, exists, cancellationToken)
                .ConfigureAwait(false);

            return new ReportItem(entry.EmployeeId, entry.PersonNo, AgentEnrollmentAction.Upsert, Ok: true, entry.Version, null);
        }
        catch (HikvisionException failure)
        {
            WriteFailed(entry.PersonNo, failure.Message);
            return new ReportItem(entry.EmployeeId, entry.PersonNo, AgentEnrollmentAction.Upsert, Ok: false, entry.Version, failure.Message);
        }
    }

    /// <summary>Ro'yxatdan chiqqan odamni terminaldan olib tashlaydi.</summary>
    /// <remarks>
    /// Xodim identifikatori bu yerda YO'Q — terminalda faqat raqam bor.
    /// Hisobotga RAQAM yuboriladi va server uni mosliklar jadvali orqali
    /// xodimga qaytaradi; moslik ham topilmasa, qator serverda jimgina
    /// tashlanadi (qurilmadan o'chirish esa baribir bajarilgan).
    /// </remarks>
    private async Task<ReportItem> RemoveAsync(string personNo, CancellationToken cancellationToken)
    {
        try
        {
            await _device.DeletePersonAsync(personNo, cancellationToken).ConfigureAwait(false);
            Removed(personNo);
        }
        catch (HikvisionException failure)
        {
            RemoveFailed(personNo, failure.Message);
        }

        return new ReportItem(Guid.Empty, personNo, AgentEnrollmentAction.Remove, Ok: true, null, null);
    }

    /// <summary>Raqam Bonvi fazasidami — 9 bilan boshlanadigan 9 belgi.</summary>
    /// <remarks>
    /// Qoida serverdagi <c>DevicePersonNo</c> bilan bir xil. Ikki joyda
    /// yozilgani ataylab: agent server loyihalariga havola qilmaydi
    /// (loyiha fayli izohi), va bu qoida o'zgarmaydigan shartnoma —
    /// o'zgarsa, qurilmadagi mavjud odamlar fazadan chiqib ketardi.
    /// </remarks>
    private static bool IsManaged(string personNo)
        => personNo.Length == 9 && personNo[0] == '9';

    /*
      Jurnal — manba generatori orqali (`CA1848`). Bu yerda jarayonning
      butun ko'rinishi: ishga tushdi, o'tish uzatildi, ro'yxat
      tekshirildi, xato. Boshqa hech narsa yozilmaydi — agent mijozning
      serverida ishlaydi va jurnal u yerda hech kim tozalamaydigan
      faylga oqadi.
    */

    [LoggerMessage(EventId = 6100, Level = LogLevel.Information,
        Message = "Qurilma agenti ishga tushdi: {Host}, hodisalar {Events}s, ro'yxat {Roster}s")]
    private partial void Started(string host, int events, int roster);

    [LoggerMessage(EventId = 6101, Level = LogLevel.Information,
        Message = "O'tish uzatildi: {Sent}, yozildi: {Stored}, takror: {Duplicates}")]
    private partial void Forwarded(int sent, int stored, int duplicates);

    [LoggerMessage(EventId = 6102, Level = LogLevel.Information,
        Message = "Ro'yxat tekshirildi: {Total} kishi, o'zgarish {Changed}")]
    private partial void Reconciled(int total, int changed);

    [LoggerMessage(EventId = 6103, Level = LogLevel.Information,
        Message = "Qurilmadan olib tashlandi: {PersonNo}")]
    private partial void Removed(string personNo);

    [LoggerMessage(EventId = 6104, Level = LogLevel.Warning,
        Message = "Xodim yozilmadi ({PersonNo}): {Error}")]
    private partial void WriteFailed(string personNo, string error);

    [LoggerMessage(EventId = 6105, Level = LogLevel.Warning,
        Message = "O'chirilmadi ({PersonNo}): {Error}")]
    private partial void RemoveFailed(string personNo, string error);

    [LoggerMessage(EventId = 6106, Level = LogLevel.Warning,
        Message = "Halqa «{Name}» xato bilan tugadi")]
    private partial void LoopFailed(string name, Exception failure);

    [LoggerMessage(EventId = 6107, Level = LogLevel.Warning,
        Message = "Qurilma soati o'qilmadi: {Error}")]
    private partial void ClockFailed(string error);

    [LoggerMessage(EventId = 6108, Level = LogLevel.Warning,
        Message = "Jonli kadr olinmadi: {Error}")]
    private partial void SnapshotFailed(string error);

    [LoggerMessage(EventId = 6109, Level = LogLevel.Warning,
        Message = "Vaqti hozirdan {Days} kundan uzoq {Count} ta o'tish yuborilmadi — terminal soatini tekshiring")]
    private partial void ImplausibleSkipped(int count, int days);

    /// <summary>
    /// O'tish shunchalik yangimi, jonli kadr unda o'sha odamni ko'rsata oladi.
    /// </summary>
    /// <remarks>
    /// Yosh terminalning O'Z soati bo'yicha o'lchanadi: soati bir necha
    /// soniya orqada yurgan terminalda ham yangi o'tish kadrsiz qolmasin.
    /// Siljish hali o'lchanmagan bo'lsa va soat katta og'gan bo'lsa, kadr
    /// olinmaydi — shubhali holatda surat biriktirilmaydi.
    /// </remarks>
    /// <param name="occurred">O'tish vaqti — terminal soatida.</param>
    /// <param name="deviceNow">Terminalning «hozir»i — agent soati + o'lchangan siljish.</param>
    private bool IsFresh(DateTimeOffset occurred, DateTimeOffset deviceNow)
    {
        var age = (deviceNow - occurred).TotalSeconds;

        return age >= -FutureToleranceSeconds && age <= _options.SnapshotMaxAgeSeconds;
    }

    /// <summary>Jonli kadr — xatosi o'tishni to'xtatmaydi.</summary>
    private async Task<string?> SnapshotAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SnapshotChannel))
        {
            return null;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(QuickCallTimeout);

        try
        {
            var bytes = await _device
                .SnapshotAsync(_options.SnapshotChannel, deadline.Token)
                .ConfigureAwait(false);

            return bytes is { Length: > 0 and <= MaxSnapshotBytes } ? Convert.ToBase64String(bytes) : null;
        }
        catch (HttpRequestException failure)
        {
            SnapshotFailed(failure.Message);
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Muddat tugadi — o'tish kadrsiz ketadi, lekin KECHIKMAYDI.
            SnapshotFailed("muddat tugadi");
            return null;
        }
    }

    private static string Method(string? verifyMode)
    {
        if (string.IsNullOrEmpty(verifyMode))
        {
            return "Unknown";
        }

        if (verifyMode.Contains("face", StringComparison.OrdinalIgnoreCase))
        {
            return "Face";
        }

        if (verifyMode.Contains("card", StringComparison.OrdinalIgnoreCase))
        {
            return "Card";
        }

        return verifyMode.Contains("fp", StringComparison.OrdinalIgnoreCase) ? "Fingerprint" : "Unknown";
    }
}
