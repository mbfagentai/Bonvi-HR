using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Employees.Queries;
using Bonvi.Application.Reports.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Reports.Queries;

/// <summary>Excel faylga aylantirilgan xodimlar ro'yxati.</summary>
/// <param name="Content"><c>.xlsx</c> faylining baytlari.</param>
/// <param name="FileName">Yuklab olinadigan fayl nomi.</param>
public sealed record EmployeeExportFile(byte[] Content, string FileName);

/// <summary>
/// Xodimlar ro'yxatini, EKRANDAGI bilan bir xil filtr va saralash bilan,
/// Excel faylga eksport qiladi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Kirish — AYNAN <see cref="SearchEmployeesQuery"/>, yangi tur emas.</b>
/// Ro'yxat ekrani va eksport bir xil so'rov shaklini olishi — «bir xil
/// filtr, bir xil saralash» talabining eng kuchli kafolati: agar eksport
/// o'zining alohida so'rov turini olsa, ikkalasi vaqt o'tishi bilan
/// bittasi yangilanib, ikkinchisi eskirib qolishi tabiiy bo'lardi (yangi
/// filtr faqat ro'yxatga qo'shiladi, eksport haqida unutiladi). Bitta
/// tur bo'lsa, bu buzilish KOMPILYATSIYADA sezilardi.
/// </para>
/// <para>
/// <b>Filtr qurish — <see cref="SearchEmployeesQueryHandler.BuildFilter"/>
/// orqali, parallel mantiqsiz.</b> Faqat <c>Page</c>/<c>PageSize</c>
/// boshqacha: ekran 200 bilan cheklaydi, bu yerda esa
/// <see cref="MaxExportRows"/> — HR filtrni toraytirmasdan BUTUN mos
/// ro'yxatni bitta faylda olishi kerak, sahifalab emas.
/// </para>
/// <para>
/// <b>Har bir eksport jurnalga yoziladi</b> (<c>Export</c>, SPEC-rollar-va-audit
/// §7.6): kim, qachon, qaysi filtr bilan va nechta qator — shaxsiy ma'lumot
/// BIRORTA HAM. Qidiruv matnining o'zi yozilmaydi, faqat «qidiruv bor edi»:
/// unda ism yoki JSHSHIR bo'ladi. So'rov GET bo'lsa ham saqlash shu sababli.
/// </para>
/// </remarks>
public sealed class ExportEmployeesQueryHandler
{
    /// <summary>
    /// Bitta eksportda ruxsat etilgan eng ko'p qator.
    /// </summary>
    /// <remarks>
    /// Bugungi haqiqiy xodimlar soni — bir necha ming (2347, o'sish bilan).
    /// Bu chegara ularning hech biriga TEGMAYDI: u faqat noaniq keng filtr
    /// (masalan hammasi, arxiv bilan birga) yillar davomida o'nlab ming
    /// yozuvga yetib borgan taqdirda serverni bitta so'rovda butun jadvalni
    /// xotiraga olib, javobni daqiqalab kutdirishdan asraydi. Chegaraga
    /// urilganda fayl JIMGINA qirqilmaydi — aniq xato qaytadi va HR
    /// filtrni toraytirishga taklif qilinadi: «540 o'rniga 480 qator»
    /// jimgina eksport qilinsa, HR buni sezmay qolardi — bu esa Excel'ga
    /// ishonchni yo'qotadigan aynan o'sha xato.
    /// </remarks>
    public const int MaxExportRows = 5000;

    private readonly IEmployeeReadModel _readModel;
    private readonly IEmployeeExcelWriter _writer;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExportEmployeesQueryHandler(
        IEmployeeReadModel readModel,
        IEmployeeExcelWriter writer,
        ICurrentUser currentUser,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _readModel = readModel;
        _writer = writer;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<EmployeeExportFile>> HandleAsync(
        SearchEmployeesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // `MaxExportRows + 1`: chegaradan oshganini bilish uchun bittasi
        // ortiqcha yetarli — butun ortiqcha yozuvni tortib kelish shart emas.
        var filterResult = SearchEmployeesQueryHandler.BuildFilter(query, page: 1, pageSize: MaxExportRows + 1);
        if (filterResult.IsFailure)
        {
            return filterResult.Error;
        }

        var found = await _readModel.SearchEmployeesAsync(filterResult.Value, cancellationToken).ConfigureAwait(false);

        if (found.Total > MaxExportRows)
        {
            return Error.Validation(
                "eksport.juda_kop",
                $"Tanlangan filtrga {found.Total} yozuv mos keladi — eksport chegarasi " +
                $"{MaxExportRows}. Filtrni toraytirib (masalan bo'lim yoki holat bo'yicha) qayta urinib ko'ring.");
        }

        var canSeeSalary = _currentUser.CanSeeSalary();
        IReadOnlyList<EmployeeListItemDto> rows =
            [.. found.Items.Select(row => SearchEmployeesQueryHandler.ToDto(row, canSeeSalary))];

        var content = _writer.Write(rows);
        var fileName = $"Bonvi_xodimlar_{_clock.Today:yyyy-MM-dd}.xlsx";

        _auditLogger.Record(
            AuditAction.Export,
            "Employee",
            entityId: null,
            newValues: JsonSerializer.Serialize(new ExportAuditDetail(ExportAuditFilters.From(query), rows.Count), ExportAuditJson));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new EmployeeExportFile(content, fileName);
    }

    /// <summary>Bo'sh filtrlar yozilmaydi — tafsilot qisqa va o'qilarli bo'lsin.</summary>
    private static readonly JsonSerializerOptions ExportAuditJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Eksport hodisasining tafsiloti.</summary>
    /// <param name="Filters">Qo'llangan filtrlar (qidiruv matnisiz).</param>
    /// <param name="Rows">Fayldagi qatorlar soni.</param>
    private sealed record ExportAuditDetail(ExportAuditFilters Filters, int Rows);

    /// <summary>Filtrlar — shaxsiy ma'lumotsiz.</summary>
    private sealed record ExportAuditFilters(
        bool? HasSearch,
        Guid? DepartmentId,
        Guid? PositionId,
        Guid? DirectionId,
        Guid? LegalEntityId,
        string? Status,
        bool? NeedsReview,
        string? Sort)
    {
        public static ExportAuditFilters From(SearchEmployeesQuery query) => new(
            string.IsNullOrWhiteSpace(query.Search) ? null : true,
            query.DepartmentId,
            query.PositionId,
            query.DirectionId,
            query.LegalEntityId,
            query.Status,
            query.NeedsReview,
            query.Sort);
    }
}
