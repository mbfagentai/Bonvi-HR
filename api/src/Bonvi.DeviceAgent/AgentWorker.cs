using System.Globalization;
using System.Net;
using Bonvi.Application.Attendance.Ingest;
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
/// boshlanadigan 9 belgi) qo'llanadi — va faqat SHU server ajratgan
/// raqamga (<see cref="RosterResponse.RemoveNumbers"/>). Bu tekshiruvsiz
/// birinchi solishtirishning o'ziyoq Verifix ning barcha xodimlarini
/// o'chirib tashlagan bo'lardi, bir terminalga ulangan ikki Bonvi serveri
/// (ishchi va sinov) esa bir-birining odamlarini o'chirib turardi.
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

    /// <summary>
    /// ESKI holat fayli (faqat agent soatidagi o'qish lahzasi) bilan birinchi
    /// ishga tushishda shuncha orqaga qaraladi.
    /// </summary>
    /// <remarks>
    /// Holat fayli umuman yo'q bo'lsa — <see cref="AgentOptions.ColdStartHours"/>.
    /// </remarks>
    private static readonly TimeSpan LegacyLookback = TimeSpan.FromHours(1);

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

    /// <summary>Hisobot shuncha qatordan keyin yuboriladi — aylanish oxirini kutmasdan.</summary>
    /// <remarks>
    /// Birinchi o'rnatishda 500 kishini yozish o'n daqiqalab davom etadi.
    /// Hisobot faqat oxirida yuborilsa, o'rtadagi har qanday uzilish
    /// (internet, konteyner qayta ishga tushishi) butun natijani yo'qotardi
    /// va server hamma qatorni «navbatda» deb ko'rsatib turardi.
    /// </remarks>
    internal const int ReportFlushSize = 20;

    /// <summary>
    /// Ketma-ket shuncha VAQTINCHALIK xato — aylanish to'xtatiladi.
    /// </summary>
    /// <remarks>
    /// Bitta xodimning rasmi yuklanmasa, qolganlari davom etadi. Lekin
    /// terminal yoki server umuman javob bermay qolsa, 500 kishining
    /// har biri 30 soniyalik muddatni kutib, aylanish soatlab cho'zilardi —
    /// undan ko'ra to'xtab, halqaning kutish oralig'idan keyin qaytadan
    /// boshlagan yaxshi.
    /// </remarks>
    private const int MaxConsecutiveTransientFailures = 3;

    /// <summary>
    /// Ommaviy o'chirish to'sig'i: terminaldagi Bonvi odamlarining shuncha
    /// ulushidan ko'pi o'chishi kerak bo'lsa, agent o'chirmaydi (SPEC-agent §L).
    /// </summary>
    internal const double MassRemovalShare = 0.2;

    /// <summary>
    /// To'siqning pastki chegarasi — kichik terminalda bitta ishdan ketgan
    /// odam «beshdan biri» bo'lib, to'siqni yoqib yubormasin.
    /// </summary>
    internal const int MassRemovalFloor = 3;

    /// <summary>Muvaffaqiyatsiz aylanishdan keyingi eng uzoq kutish.</summary>
    internal static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Terminal paroli rad etilganda (401/403) yoki token boshqa terminalga
    /// bog'langanda kutish — kamida o'n daqiqa.
    /// </summary>
    /// <remarks>
    /// ⚠️ Noto'g'ri parol bilan har 2 soniyada urinish terminalda hisobni
    /// QULFLAYDI (SPEC-agent §H). Bu xatolar o'z-o'zidan tuzalmaydi — odam
    /// sozlamani to'g'rilashi kerak — ya'ni tez-tez urinishning foydasi yo'q,
    /// zarari bor.
    /// </remarks>
    internal static readonly TimeSpan AuthBackoff = TimeSpan.FromMinutes(10);

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

    private TimeSpan ColdStart => TimeSpan.FromHours(_options.ColdStartHours);

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
    /// <remarks>
    /// <para>
    /// ⭐ <b>Xatodan keyin kutish o'sib boradi</b> (<see cref="RetryDelay"/>):
    /// ilgari halqa har 2 soniyada qayta urinib, har safar to'liq stek izini
    /// jurnalga yozardi (sinov muhitida 10 daqiqada 4281 qator) — terminal
    /// uzilgan kechada disk to'lib ketardi.
    /// </para>
    /// <para>
    /// Stek izi faqat BIRINCHI xatoda (muvaffaqiyatdan keyingi). Keyingilari —
    /// bitta qisqa qator; tiklanganda esa «tiklandi» degan qator.
    /// </para>
    /// </remarks>
    private async Task LoopAsync(
        TimeSpan interval,
        Func<CancellationToken, Task> step,
        string name,
        CancellationToken stoppingToken)
    {
        var failures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = interval;

            try
            {
                await step(stoppingToken).ConfigureAwait(false);

                if (failures > 0)
                {
                    LoopRecovered(name, failures);
                }

                failures = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception failure)
            {
                // Tarmoq uzilishi, terminalning qayta yuklanishi, serverning
                // qayta chiqarilishi — hammasi shu yerga tushadi va halqa
                // kutishdan keyin o'zi tiklanadi.
                failures++;
                wait = RetryDelay(interval, failures, failure);

                if (failures == 1)
                {
                    LoopFailed(name, (int)wait.TotalSeconds, failure);
                }
                else
                {
                    LoopStillFailing(name, failures, (int)wait.TotalSeconds, failure.Message);
                }
            }

            try
            {
                await Task.Delay(wait, _time, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// <paramref name="failures"/>-chi ketma-ket xatodan keyin qancha kutish.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>Tasdiqlash xatosi (terminal paroli — 401/403, Bonvi tokeni —
    ///         401/403, boshqa terminalga bog'langan token —
    ///         <c>davomat.terminal_mos_emas</c>) — <see cref="AuthBackoff"/>:
    ///         ular o'zi tuzalmaydi, tez urinish esa hisobni qulflaydi.</item>
    ///   <item>Qolgani — oddiy oraliqdan boshlab har safar ikki barobar,
    ///         <see cref="MaxBackoff"/> gacha.</item>
    /// </list>
    /// </remarks>
    internal static TimeSpan RetryDelay(TimeSpan interval, int failures, Exception failure)
    {
        if (IsAuthFailure(failure))
        {
            return AuthBackoff;
        }

        var exponent = Math.Min(Math.Max(failures - 1, 0), 16);
        var seconds = interval.TotalSeconds * Math.Pow(2, exponent);

        return TimeSpan.FromSeconds(Math.Min(Math.Max(seconds, interval.TotalSeconds), MaxBackoff.TotalSeconds));
    }

    /// <summary>O'zi tuzalmaydigan tasdiqlash xatosimi.</summary>
    private static bool IsAuthFailure(Exception failure) => failure switch
    {
        HikvisionException { StatusCode: 401 or 403 } => true,
        BonviApiException { Code: "davomat.terminal_mos_emas" } => true,
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } => true,
        _ => false,
    };

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
    /// Kursor uchta: tartib raqami, oxirgi hodisaning TERMINAL vaqti
    /// (<see cref="AgentState.LastEventTime"/>, faqat o'sadi) va qidiruv
    /// boshi (<see cref="AgentState.SearchAnchor"/>, soat orqaga ketsa
    /// pasayadi). Qidiruv oralig'i uchinchisidan boshlanadi — agent soatidan
    /// emas.
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

        /*
          Terminal sahifani ESKIDAN yangiga berishi so'ralgan
          (`timeReverseOrder: false`). Firmware buni e'tiborsiz qoldirib
          teskari bersa, «yetarlicha yig'ildi» deb to'xtash eng YANGI
          hodisalarni olib, eskilarini kursor ortida qoldirardi. Shu sababli
          teskari tartib sezilsa — to'xtamasdan oxirigacha o'qiladi va eng
          eskilari tanlanadi.
        */
        long? previousSerial = null;
        var reversed = false;

        var pages = _device.SearchEventsAsync(
            SearchStart(deviceNow) - Overlap,
            deviceNow + FutureAllowance,
            QuickCallTimeout,
            cancellationToken);

        await foreach (var page in pages.ConfigureAwait(false))
        {
            foreach (var item in page)
            {
                if (previousSerial is { } previous && item.SerialNo < previous)
                {
                    reversed = true;
                }

                previousSerial = item.SerialNo;
            }

            fresh.AddRange(page.Where(IsNew));

            if (!reversed && fresh.Count >= MaxEventsPerBatch)
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

        // Eng ESKI bo'lak — bitta soniya o'rtasidan kesilmaydi: kesilgan
        // soniyaning qolgani kursor bilan bir xil vaqtda bo'lib, «yangi»
        // sanalmay qolishi mumkin edi.
        if (fresh.Count > MaxEventsPerBatch)
        {
            var cut = MaxEventsPerBatch;
            var edge = EventTime(fresh[cut - 1]);

            while (cut < fresh.Count && EventTime(fresh[cut]) == edge)
            {
                cut++;
            }

            if (cut < fresh.Count)
            {
                fresh = fresh[..cut];
                complete = false;
            }
        }

        // Bo'sh shaxs raqami — eshik holati yoki tanilmagan yuz, o'tish
        // EMAS (`AcsEvent.EmployeeNoString` izohi). Rad etilgan urinish
        // (ruxsat yo'q, muddatdan tashqari, yuz tasdiqlanmadi) ham o'tish
        // emas — ro'yxati server bilan UMUMIY (`HikvisionEventTypes`).
        var passes = new List<(AcsEvent Item, DateTimeOffset Time)>();
        var implausible = 0;
        var denied = 0;

        foreach (var item in fresh)
        {
            if (string.IsNullOrWhiteSpace(item.EmployeeNoString) || EventTime(item) is not { } time)
            {
                continue;
            }

            if (HikvisionEventTypes.IsDenied(item.Minor))
            {
                denied++;
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

        if (denied > 0)
        {
            DeniedSkipped(denied);
        }

        /*
          Jonli kadr — BITTA va faqat ENG YANGI o'tishga. Kadr O'QISH
          lahzasida olinadi, ya'ni unda HOZIR terminal oldida turgan odam
          bor — bir necha soniya ichida o'tganlarning oxirgisi. Oldingilariga
          biriktirilsa, ularning kartasida boshqa odam turardi (va o'nta
          o'tishli bo'lakda bir xil kadr o'n marta ketib, qabul yo'lining
          2 MB chegarasini oshirib yuborardi). Agent uzilib qaytganda
          yuboriladigan eski o'tishlar suratsiz qoladi, noto'g'ri surat
          bilan emas (`SnapshotMaxAgeSeconds` izohi).
        */
        var newest = passes
            .Where(pass => IsFresh(pass.Time, deviceNow))
            .OrderByDescending(pass => pass.Time)
            .ThenByDescending(pass => pass.Item.SerialNo)
            .Select(pass => pass.Item)
            .FirstOrDefault();

        var photo = newest is not null
            ? await SnapshotAsync(cancellationToken).ConfigureAwait(false)
            : null;

        var events = passes
            .Select(pass => new IngestEvent(
                pass.Item.EmployeeNoString!,
                pass.Item.Time!,
                "Unknown",
                Method(pass.Item.CurrentVerifyMode),
                EventId(pass.Item.SerialNo, pass.Time),
                ReferenceEquals(pass.Item, newest) ? photo : null,
                pass.Item.Minor))
            .ToList();

        /*
          Bo'laklab: suratli hodisa ~110 KB, qabul yo'lining tana chegarasi
          esa 2 MB. O'nta hodisa — chegaradan ancha past, ertalabki
          navbatda ham (surat esa bo'lakda ko'pi bilan bitta). Bo'laklardan
          biri yiqilsa kursor surilmaydi va hammasi qayta yuboriladi —
          server takrorni o'zi tashlaydi.
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

          Tartib raqami va oxirgi vaqt faqat O'SADI: terminal soati orqaga
          surilgan bo'lsa, «yangimi» degan hukm u bilan birga orqaga
          qaytmaydi (aks holda eski va yangi hodisa navbat bilan «yangi»
          bo'lib, cheksiz qayta yuborilardi). Qidiruv boshi esa oxirgi
          YOZILGAN hodisaga suriladi va pasayishi mumkin
          (`AgentState.SearchAnchor` izohi). Vaqti aql bovar qilmaydiganlari
          ikkalasini ham siljitmaydi — 2106-yilli bitta hodisa qidiruvni
          kelajakka surib, keyingi hammasini yo'qotardi.
        */
        _state.LastSerialNo = Math.Max(_state.LastSerialNo, fresh.Max(item => item.SerialNo));

        var plausible = fresh
            .Select(item => (item.SerialNo, Time: EventTime(item)))
            .Where(entry => entry.Time is { } value && Plausible(value, now))
            .ToList();

        if (plausible.Count > 0)
        {
            var latest = plausible.Max(entry => entry.Time!.Value);

            if (_state.LastEventTime is not { } last || latest > last)
            {
                _state.LastEventTime = latest;
            }

            _state.SearchAnchor = plausible.MaxBy(entry => entry.SerialNo).Time;
        }

        _state.Save(_options.StatePath);

        return !complete;
    }

    /// <summary>
    /// Qidiruvning boshi — terminal soatida.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Oxirgi YOZILGAN hodisa (<see cref="AgentState.SearchAnchor"/>). U
    /// terminalning «hozir»idan KEYIN bo'lsa — terminal soati orqaga
    /// to'g'rilangan: yangi o'tishlar undan oldingi vaqt bilan yoziladi.
    /// Bunda qidiruv boshi o'sha lahzadagi terminal vaqtiga QOTIRILADI va
    /// saqlanadi — keyingi o'tish uzatilguncha o'sha yerda turadi. Ilgari u
    /// har safar «hozir» bilan birga surilardi va agent ikki daqiqadan uzoq
    /// uzilsa (internet), oradagi o'tishlar qidiruvdan tashqarida qolardi.
    /// </para>
    /// <para>
    /// Siljish har daqiqada o'lchanadi (<see cref="ReportClockAsync"/>).
    /// </para>
    /// </remarks>
    private DateTimeOffset SearchStart(DateTimeOffset deviceNow)
    {
        var anchor = _state.SearchAnchor
            ?? _state.LastEventTime
            ?? (_state.LastReadAt is { } read ? read - LegacyLookback : deviceNow - ColdStart);

        if (anchor <= deviceNow)
        {
            return anchor;
        }

        _state.SearchAnchor = deviceNow;
        _state.Save(_options.StatePath);

        return deviceNow;
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
    /// <remarks>
    /// <para>
    /// ⭐ <b>Hamma-yoki-hech narsa EMAS.</b> Har bir xodim alohida: birining
    /// rasmi yuklanmasa yoki terminal uni rad etsa, qolganlari davom etadi
    /// va xato hisobotga tushadi. Ilgari bitta tarmoq xatosi butun aylanishni
    /// to'xtatardi — hisobot yo'qolar, o'chirishlar bajarilmas, birinchi
    /// o'rnatishdagi 500 kishi esa hech qachon oxirigacha yozilmasdi.
    /// </para>
    /// <para>
    /// Tartib: terminal seriyasi → soat → ro'yxat → O'CHIRISH → yozish.
    /// O'chirish birinchi, chunki u xavfsizlik (ishdan ketgan odam eshikni
    /// ochmasin) va yozishlar o'n daqiqalab cho'zilishi mumkin. Hisobot har
    /// <see cref="ReportFlushSize"/> qatorda va oxirida — xato bo'lsa ham.
    /// </para>
    /// </remarks>
    internal async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        /*
          Terminal seriyasi — BIRINCHI qadam. Server tokenni shu seriyaga
          bog'laydi: boshqa terminalga yo'naltirilgan agent ro'yxat ham
          olmaydi, hech kimni o'chirmaydi ham. Terminal javob bermasa, aylanish
          shu yerda to'xtaydi (halqa kutib, qayta uradi).
        */
        _bonvi.TerminalSerial = await _device.ReadSerialAsync(cancellationToken).ConfigureAwait(false);

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

        var plan = RemovalPlan.Of(roster, onDevice.Keys);

        var report = new List<ReportItem>();
        var streak = 0;

        try
        {
            if (plan.Held > 0)
            {
                MassRemovalHeld(plan.Held, plan.Managed);
            }
            else
            {
                foreach (var personNo in plan.Remove)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var removed = await RemoveAsync(personNo, cancellationToken).ConfigureAwait(false);

                    await AddAsync(report, removed.Item, cancellationToken).ConfigureAwait(false);
                    streak = Track(removed, streak);
                }
            }

            if (plan.Foreign > 0)
            {
                ForeignPersonsSeen(plan.Foreign);
            }

            foreach (var entry in roster.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Qurilma aynan shu belgini rad etgan — belgi o'zgarguncha
                // qayta urinilmaydi (har daqiqada o'sha xato bo'lardi).
                if (entry.Hold)
                {
                    continue;
                }

                var exists = onDevice.TryGetValue(entry.PersonNo, out var person);

                // Yuzi yo'q odam ham yangilanishi kerak: terminal odamni
                // ko'rsatadi, lekin yuzsiz u eshikni ocholmaydi.
                var faceMissing = exists && person!.NumOfFace is 0;

                if (exists && !entry.Force && !faceMissing)
                {
                    continue;
                }

                var written = await WriteAsync(entry, exists, cancellationToken).ConfigureAwait(false);

                // Avval hisobotga — aylanish shu qatorda to'xtasa ham, uning
                // xatosi HR ga yetib borsin.
                await AddAsync(report, written.Item, cancellationToken).ConfigureAwait(false);
                streak = Track(written, streak);
            }
        }
        finally
        {
            // Qolgan hisobot — xato yoki to'xtatish bo'lsa ham (`FlushAsync` izohi).
            await FlushAsync(report).ConfigureAwait(false);
        }

        await _bonvi.StatusAsync(plan.Held, plan.Foreign, cancellationToken).ConfigureAwait(false);

        Reconciled(roster.Entries.Count, plan.Remove.Count, plan.Held, plan.Foreign);
    }

    /// <summary>
    /// Ketma-ket vaqtinchalik xatolarni sanaydi — chegaradan oshsa aylanishni to'xtatadi.
    /// </summary>
    private static int Track(Outcome outcome, int streak)
    {
        if (outcome.Transient is not { } failure)
        {
            return 0;
        }

        streak++;

        if (streak >= MaxConsecutiveTransientFailures)
        {
            throw new HttpRequestException(
                $"Ketma-ket {streak} ta vaqtinchalik xato — aylanish to'xtatildi: {failure.Message}",
                failure);
        }

        return streak;
    }

    /// <summary>Qatorni hisobotga qo'shadi va har <see cref="ReportFlushSize"/> tada yuboradi.</summary>
    private async Task AddAsync(List<ReportItem> report, ReportItem item, CancellationToken cancellationToken)
    {
        report.Add(item);

        if (report.Count >= ReportFlushSize)
        {
            await _bonvi.ReportAsync([.. report], cancellationToken).ConfigureAwait(false);
            report.Clear();
        }
    }

    /// <summary>
    /// Yig'ilgan hisobotni yuboradi — o'z muddati bilan, to'xtatish belgisiga bog'lanmasdan.
    /// </summary>
    /// <remarks>
    /// <c>finally</c> dan chaqiriladi: aylanish xato bilan tugasa ham,
    /// konteyner to'xtatilayotgan bo'lsa ham terminalda BAJARILGAN ishning
    /// natijasi serverga yetsin — aks holda HR «navbatda» ni ko'rib turardi,
    /// agent esa keyingi safar hammasini qayta yozardi. Yuborib bo'lmasa —
    /// faqat jurnal: asl xatoni yashirmasligi kerak.
    /// </remarks>
    private async Task FlushAsync(List<ReportItem> report)
    {
        if (report.Count == 0)
        {
            return;
        }

        using var deadline = new CancellationTokenSource(QuickCallTimeout, _time);

        try
        {
            await _bonvi.ReportAsync([.. report], deadline.Token).ConfigureAwait(false);
            report.Clear();
        }
        catch (Exception failure) when (failure is HttpRequestException or OperationCanceledException)
        {
            ReportLost(report.Count, failure.Message);
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
        catch (Exception failure) when (failure is HikvisionException { StatusCode: not (401 or 403) } or HttpRequestException)
        {
            ClockFailed(failure.Message);
        }
    }

    /// <summary>Bitta xodimni terminalga yozadi — odam va yuz.</summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Rasm BIRINCHI.</b> Rasm yo'q yoki juda katta bo'lsa, terminalga
    /// umuman tegilmaydi. Ilgari odam avval yozilar, keyin rasm
    /// tekshirilardi — va terminalda yuzsiz, eshikni ocha olmaydigan «bo'sh»
    /// odam qolardi.
    /// </para>
    /// <para>
    /// Xato ikki xil: QAT'IY (terminal so'rovni rad etdi, rasm yo'q yoki juda
    /// katta) — server bu belgini eslab qoladi va o'zgarguncha qayta bermaydi;
    /// VAQTINCHALIK (tarmoq, muddat) — keyingi aylanishda qayta urinadi.
    /// Terminal paroli rad etilsa (401/403) — bu xodimning xatosi emas:
    /// istisno halqaga ketadi va u uzoq kutadi (hisob qulflanmasin).
    /// </para>
    /// </remarks>
    private async Task<Outcome> WriteAsync(RosterEntry entry, bool exists, CancellationToken cancellationToken)
    {
        try
        {
            var photo = await _bonvi.PhotoAsync(entry.EmployeeId, cancellationToken).ConfigureAwait(false);

            if (photo is null)
            {
                // Server rasmsiz xodimni ro'yxatga yozish uchun bermaydi —
                // bu yerga tushish poyga yoki rasm o'chirilgan-u odam
                // terminalda yo'qolgan.
                return Rejected(entry, "Rasm topilmadi.");
            }

            if (photo.Length > MaxFaceBytes)
            {
                return Rejected(entry, $"Rasm juda katta: {photo.Length / 1024} KB.");
            }

            await _device
                .WritePersonAsync(entry.PersonNo, entry.FullName, exists, cancellationToken)
                .ConfigureAwait(false);

            await _device
                .WriteFaceAsync(entry.PersonNo, photo, exists, cancellationToken)
                .ConfigureAwait(false);

            return new Outcome(
                new ReportItem(entry.EmployeeId, entry.PersonNo, AgentEnrollmentAction.Upsert, Ok: true, entry.Version, null),
                Transient: null);
        }
        catch (HikvisionException failure) when (failure.StatusCode is not (401 or 403))
        {
            WriteFailed(entry.PersonNo, failure.Message);

            return failure.StatusCode is >= 400 and < 500 and not (408 or 429)
                ? Rejected(entry, failure.Message)
                : Failed(entry, failure.Message, failure);
        }
        catch (HttpRequestException failure) when (!IsAuthFailure(failure))
        {
            WriteFailed(entry.PersonNo, failure.Message);
            return Failed(entry, failure.Message, failure);
        }
        catch (OperationCanceledException failure) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient muddati — to'xtatish emas.
            WriteFailed(entry.PersonNo, "muddat tugadi");
            return Failed(entry, "Muddat tugadi.", failure);
        }
    }

    private static Outcome Rejected(RosterEntry entry, string error)
        => new(
            new ReportItem(entry.EmployeeId, entry.PersonNo, AgentEnrollmentAction.Upsert, Ok: false, entry.Version, error, Rejected: true),
            Transient: null);

    private static Outcome Failed(RosterEntry entry, string error, Exception failure)
        => new(
            new ReportItem(entry.EmployeeId, entry.PersonNo, AgentEnrollmentAction.Upsert, Ok: false, entry.Version, error),
            failure);

    /// <summary>Ro'yxatdan chiqqan odamni terminaldan olib tashlaydi.</summary>
    /// <remarks>
    /// <para>
    /// Xodim identifikatori bu yerda YO'Q — terminalda faqat raqam bor.
    /// Hisobotga RAQAM yuboriladi va server uni mosliklar jadvali orqali
    /// xodimga qaytaradi; moslik ham topilmasa, qator serverda jimgina
    /// tashlanadi.
    /// </para>
    /// <para>
    /// ⚠️ Muvaffaqiyatsiz o'chirish — MUVAFFAQIYATSIZ deb yuboriladi. Ilgari
    /// hisobot har doim «bajarildi» derdi, HR «O'chirilgan» ni ko'rardi, ishdan
    /// ketgan odam esa eshikni ochib yuraverardi.
    /// </para>
    /// </remarks>
    private async Task<Outcome> RemoveAsync(string personNo, CancellationToken cancellationToken)
    {
        try
        {
            await _device.DeletePersonAsync(personNo, cancellationToken).ConfigureAwait(false);
            Removed(personNo);

            return new Outcome(new ReportItem(Guid.Empty, personNo, AgentEnrollmentAction.Remove, Ok: true, null, null), null);
        }
        catch (HikvisionException failure) when (failure.StatusCode is not (401 or 403))
        {
            RemoveFailed(personNo, failure.Message);
            return new Outcome(new ReportItem(Guid.Empty, personNo, AgentEnrollmentAction.Remove, Ok: false, null, failure.Message), null);
        }
        catch (HttpRequestException failure)
        {
            RemoveFailed(personNo, failure.Message);
            return new Outcome(new ReportItem(Guid.Empty, personNo, AgentEnrollmentAction.Remove, Ok: false, null, failure.Message), failure);
        }
        catch (OperationCanceledException failure) when (!cancellationToken.IsCancellationRequested)
        {
            RemoveFailed(personNo, "muddat tugadi");
            return new Outcome(new ReportItem(Guid.Empty, personNo, AgentEnrollmentAction.Remove, Ok: false, null, "Muddat tugadi."), failure);
        }
    }

    /// <summary>Raqam Bonvi fazasidami — 9 bilan boshlanadigan 9 belgi.</summary>
    /// <remarks>
    /// Qoida serverdagi <c>DevicePersonNo</c> bilan bir xil. Ikki joyda
    /// yozilgani ataylab: agent server loyihalariga havola qilmaydi
    /// (loyiha fayli izohi), va bu qoida o'zgarmaydigan shartnoma —
    /// o'zgarsa, qurilmadagi mavjud odamlar fazadan chiqib ketardi.
    /// </remarks>
    internal static bool IsManaged(string personNo)
        => personNo.Length == 9 && personNo[0] == '9';

    /// <summary>
    /// Ommaviy o'chirish to'sig'i — shuncha o'chirishni bajarmay USHLAB turish kerakmi.
    /// </summary>
    /// <remarks>
    /// Ro'yxat bo'sh (baza tiklanmoqda, noto'g'ri server, so'rov xatosi) yoki
    /// terminaldagi Bonvi odamlarining beshdan biridan ko'pi bir aylanishda
    /// o'chishi kerak — ikkalasi ham odatda XATO, haqiqiy ishdan bo'shatish
    /// emas. Bir xato ro'yxat butun korxonaning eshigini yopib qo'yardi.
    /// Haqiqatan shunday bo'lsa (filial yopildi), HR kartadagi tugma bilan
    /// tasdiqlaydi (<see cref="RosterResponse.AllowMassRemoval"/>).
    /// </remarks>
    /// <param name="rosterSize">Kerakli ro'yxatdagi odamlar soni.</param>
    /// <param name="removals">O'chirilishi kerak bo'lganlar.</param>
    /// <param name="managedOnDevice">Terminaldagi Bonvi fazasidagi odamlar.</param>
    internal static bool ShouldHoldRemovals(int rosterSize, int removals, int managedOnDevice)
    {
        if (removals == 0)
        {
            return false;
        }

        if (rosterSize == 0)
        {
            return true;
        }

        return removals > Math.Max(MassRemovalFloor, managedOnDevice * MassRemovalShare);
    }

    /// <summary>Bitta yozish yoki o'chirish natijasi.</summary>
    /// <param name="Item">Hisobot qatori.</param>
    /// <param name="Transient">Vaqtinchalik xato bo'lsa — uning istisnosi (ketma-ketlik hisobi uchun).</param>
    private readonly record struct Outcome(ReportItem Item, Exception? Transient);

    /// <summary>
    /// Terminaldagi Bonvi odamlaridan qaysi biri o'chiriladi, qaysi biri begona.
    /// </summary>
    /// <param name="Remove">O'chiriladigan raqamlar (to'siq qo'llanmagan bo'lsa).</param>
    /// <param name="Held">To'siq ushlab qolgan o'chirishlar soni (<c>0</c> — ushlanmadi).</param>
    /// <param name="Foreign">Begona Bonvi-raqamli odamlar — boshqa server yozgan.</param>
    /// <param name="Managed">Terminaldagi Bonvi fazasidagi odamlar soni.</param>
    internal sealed record RemovalPlan(IReadOnlyList<string> Remove, int Held, int Foreign, int Managed)
    {
        /// <summary>Rejani tuzadi.</summary>
        /// <remarks>
        /// Server <see cref="RosterResponse.RemoveNumbers"/> ni bersa — faqat o'sha
        /// raqamlar o'chiriladi, fazadagi qolgan ortiqchasi BEGONA. Bermasa (eski
        /// server) — eski qoida: fazadagi har qanday ortiqcha.
        /// </remarks>
        public static RemovalPlan Of(RosterResponse roster, IEnumerable<string> onDevice)
        {
            var managed = onDevice.Where(IsManaged).ToList();
            var expected = roster.Entries.Select(entry => entry.PersonNo).ToHashSet(StringComparer.Ordinal);
            var extra = managed.Where(number => !expected.Contains(number)).ToList();

            List<string> remove;
            var foreign = 0;

            if (roster.RemoveNumbers is { } listed)
            {
                var ours = listed.ToHashSet(StringComparer.Ordinal);
                remove = [.. extra.Where(ours.Contains)];
                foreign = extra.Count - remove.Count;
            }
            else
            {
                remove = extra;
            }

            var held = !roster.AllowMassRemoval && ShouldHoldRemovals(roster.Entries.Count, remove.Count, managed.Count)
                ? remove.Count
                : 0;

            return new RemovalPlan(held > 0 ? [] : remove, held, foreign, managed.Count);
        }
    }

    /*
      Jurnal — manba generatori orqali (`CA1848`). Bu yerda jarayonning
      butun ko'rinishi: ishga tushdi, o'tish uzatildi, ro'yxat
      tekshirildi, xato. Boshqa hech narsa yozilmaydi — agent mijozning
      serverida ishlaydi va jurnal u yerda hech kim tozalamaydigan
      faylga oqadi (shu sababli docker-compose da hajm chegarasi ham bor).
    */

    [LoggerMessage(EventId = 6100, Level = LogLevel.Information,
        Message = "Qurilma agenti ishga tushdi: {Host}, hodisalar {Events}s, ro'yxat {Roster}s")]
    private partial void Started(string host, int events, int roster);

    [LoggerMessage(EventId = 6101, Level = LogLevel.Information,
        Message = "O'tish uzatildi: {Sent}, yozildi: {Stored}, takror: {Duplicates}")]
    private partial void Forwarded(int sent, int stored, int duplicates);

    [LoggerMessage(EventId = 6102, Level = LogLevel.Information,
        Message = "Ro'yxat tekshirildi: {Total} kishi, o'chirildi {Removed}, ushlab qolindi {Held}, begona {Foreign}")]
    private partial void Reconciled(int total, int removed, int held, int foreign);

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
        Message = "Halqa «{Name}» xato bilan tugadi, {Delay}s dan keyin qayta urinadi")]
    private partial void LoopFailed(string name, int delay, Exception failure);

    [LoggerMessage(EventId = 6107, Level = LogLevel.Warning,
        Message = "Qurilma soati o'qilmadi: {Error}")]
    private partial void ClockFailed(string error);

    [LoggerMessage(EventId = 6108, Level = LogLevel.Warning,
        Message = "Jonli kadr olinmadi: {Error}")]
    private partial void SnapshotFailed(string error);

    [LoggerMessage(EventId = 6109, Level = LogLevel.Warning,
        Message = "Vaqti hozirdan {Days} kundan uzoq {Count} ta o'tish yuborilmadi — terminal soatini tekshiring")]
    private partial void ImplausibleSkipped(int count, int days);

    [LoggerMessage(EventId = 6110, Level = LogLevel.Warning,
        Message = "Halqa «{Name}» hali ham xatoda ({Attempts}-urinish), {Delay}s dan keyin: {Error}")]
    private partial void LoopStillFailing(string name, int attempts, int delay, string error);

    [LoggerMessage(EventId = 6111, Level = LogLevel.Information,
        Message = "Halqa «{Name}» tiklandi — {Attempts} ta xatodan keyin")]
    private partial void LoopRecovered(string name, int attempts);

    [LoggerMessage(EventId = 6112, Level = LogLevel.Warning,
        Message = "{Held} ta o'chirish USHLAB QOLINDI (terminalda {Managed} ta Bonvi odami) — ro'yxat bo'sh yoki juda ko'p o'chirish kerak. HR qurilma kartasida tasdiqlashi kerak")]
    private partial void MassRemovalHeld(int held, int managed);

    [LoggerMessage(EventId = 6113, Level = LogLevel.Warning,
        Message = "Terminalda bu server ajratmagan {Count} ta Bonvi-raqamli odam bor — boshqa Bonvi o'rnatmasi ham shu terminalga yozyapti. Ularga tegilmadi")]
    private partial void ForeignPersonsSeen(int count);

    [LoggerMessage(EventId = 6114, Level = LogLevel.Information,
        Message = "Rad etilgan {Count} ta urinish (ruxsat yo'q, muddatdan tashqari, yuz tasdiqlanmadi) o'tish sifatida yuborilmadi")]
    private partial void DeniedSkipped(int count);

    [LoggerMessage(EventId = 6115, Level = LogLevel.Warning,
        Message = "{Count} qatorli hisobot yuborilmadi: {Error} — keyingi aylanishda holat qayta yuboriladi")]
    private partial void ReportLost(int count, string error);

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
