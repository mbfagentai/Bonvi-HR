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

    private readonly HikvisionClient _device;
    private readonly BonviAgentClient _bonvi;
    private readonly AgentOptions _options;
    private readonly ILogger<AgentWorker> _logger;

    private readonly AgentState _state;

    public AgentWorker(
        HikvisionClient device,
        BonviAgentClient bonvi,
        IOptions<AgentOptions> options,
        ILogger<AgentWorker> logger)
    {
        _device = device;
        _bonvi = bonvi;
        _options = options.Value;
        _logger = logger;
        _state = AgentState.Load(_options.StatePath);
    }

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
    /// Kursor — <c>serialNo</c>: vaqt bo'yicha kesish bir soniyada ikkita
    /// o'tish bo'lganda bittasini tashlab ketardi. Vaqt esa faqat
    /// SO'ROVNI toraytirish uchun ishlatiladi.
    /// </remarks>
    private async Task ForwardEventsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var since = _state.LastReadAt ?? now - ColdStart;

        /*
          Qisqa muddat — umumiy 30 soniya EMAS. Osilib qolgan bitta so'rov
          halqani 30 soniyaga to'xtatib qo'yardi: o'tishlar kechikardi va
          jonli kadr o'tgan odamni allaqachon ushlay olmasdi. Yiqilgan
          so'rov keyingi aylanishda (2 soniyadan keyin) qaytariladi.
        */
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(QuickCallTimeout);

        var items = await _device
            .ReadEventsAsync(since - TimeSpan.FromMinutes(1), now, deadline.Token)
            .ConfigureAwait(false);

        var fresh = items
            .Where(item => item.SerialNo > _state.LastSerialNo)
            .OrderBy(item => item.SerialNo)
            .ToList();

        if (fresh.Count == 0)
        {
            _state.LastReadAt = now;
            return;
        }

        // Bo'sh shaxs raqami — eshik holati yoki tanilmagan yuz, o'tish
        // EMAS (`AcsEvent.EmployeeNoString` izohi).
        var passes = fresh
            .Where(item => !string.IsNullOrWhiteSpace(item.EmployeeNoString) && item.Time is not null)
            .ToList();

        /*
          Jonli kadr — BITTA, faqat yangi o'tish bo'lsa. Kadr O'QISH
          lahzasida olinadi, shu sababli u faqat bir necha soniya oldingi
          o'tishga biriktiriladi (`SnapshotMaxAgeSeconds` izohi): agent
          uzilib qaytganda yuboriladigan eski o'tishlar suratsiz qoladi,
          noto'g'ri surat bilan emas.
        */
        var photo = passes.Any(item => IsFresh(item, now))
            ? await SnapshotAsync(cancellationToken).ConfigureAwait(false)
            : null;

        var events = passes
            .Select(item => new IngestEvent(
                item.EmployeeNoString!,
                item.Time!,
                "Unknown",
                Method(item.CurrentVerifyMode),
                $"minmoe-{item.SerialNo}",
                photo is not null && IsFresh(item, now) ? photo : null))
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
        */
        _state.LastSerialNo = fresh[^1].SerialNo;
        _state.LastReadAt = now;
        _state.Save(_options.StatePath);
    }

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
    private async Task ReportClockAsync(CancellationToken cancellationToken)
    {
        try
        {
            var deviceTime = await _device.ReadClockAsync(cancellationToken).ConfigureAwait(false);

            if (deviceTime is { } time)
            {
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

    /// <summary>
    /// O'tish shunchalik yangimi, jonli kadr unda o'sha odamni ko'rsata oladi.
    /// </summary>
    /// <remarks>
    /// Vaqt o'qib bo'lmasa — YO'Q: shubhali holatda surat biriktirilmaydi.
    /// Qurilma soati katta og'gan bo'lsa ham shunday: bunda hodisa eski yoki
    /// uzoq kelajakda ko'rinadi va kadr olinmaydi. Og'ishning o'zi
    /// qurilmalar sahifasida ko'rinib turadi.
    /// </remarks>
    private bool IsFresh(AcsEvent item, DateTimeOffset now)
    {
        if (!DateTimeOffset.TryParse(item.Time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var occurred))
        {
            return false;
        }

        var age = (now - occurred).TotalSeconds;

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
