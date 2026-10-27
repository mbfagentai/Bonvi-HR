using System.Collections.Concurrent;
using System.Text.Json;
using Bonvi.Application.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;

namespace Bonvi.Application.Me;

/// <summary>«Jamoam» ko'rish auditining jurnali — port.</summary>
/// <remarks>
/// Application qatlami loglash kutubxonasiga bog'lanmagan (arxitektura testi) —
/// <c>IEmployeeAuthJournal</c> bilan bir xil sabab.
/// </remarks>
public interface ITeamViewJournal
{
    /// <summary>Ko'rish audit jurnaliga yozilmadi — javob baribir qaytdi.</summary>
    /// <param name="action">Qaysi ko'rish: <c>team_member_viewed</c> yoki <c>team_board_viewed</c>.</param>
    /// <param name="error">Sabab.</param>
    void AuditNotRecorded(string action, Exception error);
}

/// <summary>
/// Bir xil ko'rish (bitta boshliq — bitta xodim yoki panel) oynasi ichida faqat BIR MARTA yoziladi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega.</b> Audit jurnali faqat qo'shiladi va uni tozalab bo'lmaydi. Panel ochiq
/// turganda har 60 soniyada yangilanadi (SPEC-jamoam §F), oy ekrani esa har varaqlashda
/// so'raladi — har biri qator bo'lsa, bitta boshliq kuniga yuzlab bir xil qator yozardi.
/// Birinchisi «kim, kimni, qachon ko'rdi» ga javob beradi. Takror yozilmaslik g'oyasi
/// ruxsatsiz urinishlardagi 10 daqiqalik oyna bilan bir xil (<c>AuthenticationExtensions</c>).
/// </para>
/// <para>
/// Jarayon xotirasida (API bitta nusxada ishlaydi): qayta ishga tushganda yana bittadan
/// yoziladi — zararsiz. Soat <see cref="IClock"/> dan, ya'ni oyna testda boshqariladi
/// (<c>AttendanceIdentityCache</c> precedenti). SINGLETON bo'lishi shart.
/// </para>
/// </remarks>
public sealed class TeamViewWindow
{
    /// <summary>Bitta a'zoning oyi — boshliq va a'zo jufti bo'yicha.</summary>
    public static readonly TimeSpan MemberWindow = TimeSpan.FromMinutes(10);

    /// <summary>Panel — boshliq bo'yicha (sana va bo'limdan qat'i nazar).</summary>
    public static readonly TimeSpan BoardWindow = TimeSpan.FromMinutes(30);

    /// <summary>Lug'at shu hajmdan oshsa, muddati o'tganlar tozalanadi.</summary>
    /// <remarks>Boshliqlar yuztacha; chegara faqat xotira o'sib ketmasligi uchun.</remarks>
    private const int SweepThreshold = 10_000;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _until = new(StringComparer.Ordinal);
    private readonly IClock _clock;

    public TeamViewWindow(IClock clock) => _clock = clock;

    /// <summary>
    /// Kalit oynada birinchi marta ko'rinyaptimi — ha bo'lsa, oynani shu so'rov EGALLAYDI.
    /// </summary>
    /// <remarks>
    /// Bir vaqtda kelgan ikki so'rovdan faqat bittasi <c>true</c> oladi (<c>TryAdd</c> /
    /// <c>TryUpdate</c> atomar), ya'ni yangilanish ikki qator yozmaydi.
    /// </remarks>
    /// <param name="key">Boshliq va ko'rilgan narsa.</param>
    /// <param name="window">Oyna uzunligi.</param>
    public bool TryClaim(string key, TimeSpan window)
    {
        var now = _clock.UtcNow;

        if (_until.Count > SweepThreshold)
        {
            Sweep(now);
        }

        while (true)
        {
            if (_until.TryGetValue(key, out var until))
            {
                if (until > now)
                {
                    return false;
                }

                if (_until.TryUpdate(key, now + window, until))
                {
                    return true;
                }

                continue;
            }

            if (_until.TryAdd(key, now + window))
            {
                return true;
            }
        }
    }

    /// <summary>Egallangan oynani qaytaradi — yozuv tushmagan bo'lsa, keyingi so'rov qayta urinsin.</summary>
    /// <param name="key">Kalit.</param>
    public void Release(string key) => _until.TryRemove(key, out _);

    private void Sweep(DateTimeOffset now)
    {
        foreach (var entry in _until)
        {
            if (entry.Value <= now)
            {
                _until.TryRemove(entry);
            }
        }
    }
}

/// <summary>
/// «Jamoam» ko'rish auditi — <c>password_viewed</c> kabi O'QISH izi (SPEC-jamoam §C6).
/// </summary>
/// <remarks>
/// <para>
/// Bajaruvchi — boshliqning ilova hisobi (<c>HttpAuditActorSource</c> uni tokendan qo'yadi).
/// Amal turi <see cref="AuditAction.Export"/> — parolni ko'rishdagi kabi: ma'lumot tizimdan
/// HR bo'lmagan odamga chiqdi. Tafsilotda faqat amal nomi va oy — ism, telefon yo'q.
/// </para>
/// <para>
/// ⚠️ <b>Yozib bo'lmasa — log, va javob O'ZGARMAYDI</b> (§C6). Saqlash so'rov bekor qilinishiga
/// bog'lanmagan: o'qish allaqachon bo'ldi, mijoz uzilsa ham izi qolsin
/// (<c>RecordPermissionDeniedAsync</c> precedenti).
/// </para>
/// </remarks>
public sealed class TeamViewAudit
{
    private const string MemberViewed = "team_member_viewed";
    private const string BoardViewed = "team_board_viewed";

    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TeamViewWindow _window;
    private readonly ITeamViewJournal _journal;

    public TeamViewAudit(
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        TeamViewWindow window,
        ITeamViewJournal journal)
    {
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _window = window;
        _journal = journal;
    }

    /// <summary>Boshliq a'zoning oyini ko'rdi — yozuvning obyekti a'zo.</summary>
    /// <param name="head">Boshliq.</param>
    /// <param name="memberId">Ko'rilgan xodim.</param>
    /// <param name="year">Yil.</param>
    /// <param name="month">Oy.</param>
    public Task MemberViewedAsync(CurrentEmployeeIds head, Guid memberId, int year, int month)
    {
        ArgumentNullException.ThrowIfNull(head);

        return RecordAsync(
            $"member:{head.EmployeeId:N}:{memberId:N}",
            TeamViewWindow.MemberWindow,
            MemberViewed,
            "Employee",
            memberId.ToString(),
            JsonSerializer.Serialize(new { action = MemberViewed, year, month }));
    }

    /// <summary>Boshliq panelni ko'rdi — yozuvning obyekti boshliqning o'z ilova hisobi.</summary>
    /// <remarks>
    /// Panel bir nechta bo'limni qamrashi mumkin, ya'ni «bitta obyekt» yo'q; hisob —
    /// <c>password_viewed</c> dagi bilan bir xil yozuv turi.
    /// </remarks>
    /// <param name="head">Boshliq.</param>
    public Task BoardViewedAsync(CurrentEmployeeIds head)
    {
        ArgumentNullException.ThrowIfNull(head);

        return RecordAsync(
            $"board:{head.EmployeeId:N}",
            TeamViewWindow.BoardWindow,
            BoardViewed,
            "EmployeeAccount",
            head.AccountId.ToString(),
            JsonSerializer.Serialize(new { action = BoardViewed }));
    }

    private async Task RecordAsync(
        string key,
        TimeSpan window,
        string action,
        string entityName,
        string entityId,
        string detail)
    {
        if (!_window.TryClaim(key, window))
        {
            return;
        }

        try
        {
            _auditLogger.Record(AuditAction.Export, entityName, entityId, newValues: detail);

            await _unitOfWork.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // O'qish allaqachon bo'ldi — audit xatosi javobni buzmaydi (sinf izohi).
        catch (Exception error) when (error is not OperationCanceledException)
#pragma warning restore CA1031
        {
            _window.Release(key);
            _journal.AuditNotRecorded(action, error);
        }
    }
}
