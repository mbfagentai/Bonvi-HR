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

        var items = await _device
            .ReadEventsAsync(since - TimeSpan.FromMinutes(1), now, cancellationToken)
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

        var events = fresh
            // Bo'sh shaxs raqami — eshik holati yoki tanilmagan yuz,
            // o'tish EMAS (`AcsEvent.EmployeeNoString` izohi).
            .Where(item => !string.IsNullOrWhiteSpace(item.EmployeeNoString) && item.Time is not null)
            .Select(item => new IngestEvent(
                item.EmployeeNoString!,
                item.Time!,
                "Unknown",
                Method(item.CurrentVerifyMode),
                $"minmoe-{item.SerialNo}"))
            .ToList();

        if (events.Count > 0)
        {
            var result = await _bonvi.IngestAsync(events, cancellationToken).ConfigureAwait(false);

            Forwarded(events.Count, result.Stored, result.Duplicates);
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
