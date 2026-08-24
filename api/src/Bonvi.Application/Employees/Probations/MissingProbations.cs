using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Employees.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Probations;

/// <summary>
/// Holati «sinovda», lekin sinov yozuvi yo'q munosabatlar — ro'yxat va
/// ularni ommaviy yopish.
/// </summary>
/// <remarks>
/// <para>
/// <b>Muammo qayerdan.</b> <c>Employment.Hire</c> munosabatni doim
/// <c>Probation</c> holatida ochadi va uni <c>Active</c> ga faqat sinov
/// muddatining yopilishi o'tkazadi. Tizim ichida bu ikkalasi birga
/// tug'iladi. Excel'dan ko'chirishda esa sinov ma'lumoti faqat «91.5»
/// varag'ida bor — u yerda bo'lmagan odam holatsiz qolib ketadi.
/// </para>
/// <para>
/// <b>Nega bu eng qimmat jimgina xato.</b> Bunday xodim xodimlar
/// ro'yxatida ham ko'rinmaydi (u yerda doimiylar), sinov muddatlari
/// sahifasida ham (u yerda sinov YOZUVLARI). Ya'ni tizimda bor, ekranda
/// yo'q, xato ham chiqmaydi. Tekshirilgan bazada 59 xodimdan 18 tasi
/// aynan shu holatda edi — ular orasida 2010-yilda ishga kirgani ham bor.
/// </para>
/// <para>
/// <b>Nega avtomatik yopilmaydi.</b> «Muddati o'tgan — demak doimiy»
/// degan qoida bilan tizim o'zi yopsa, haqiqatan sinovda turgan bir necha
/// odam ham qo'shilib ketardi va keyin ularni ajratib bo'lmasdi. HR
/// ro'yxatni ko'radi, kerakligini belgilaydi va tasdiqlaydi.
/// </para>
/// </remarks>
public sealed record ListMissingProbationsQuery;

/// <summary>Ro'yxat qatori — ekranga tayyor ko'rinish.</summary>
/// <param name="EmploymentId">Tasdiqlashda shu identifikator qaytariladi.</param>
/// <param name="EmployeeId">Xodim kartasiga o'tish uchun.</param>
/// <param name="EmployeeName">To'liq ism.</param>
/// <param name="PhotoKey">Rasm kaliti; HR odamni yuzidan taniydi.</param>
/// <param name="LegalEntity">Qaysi firmada — holdingda bu muhim.</param>
/// <param name="Department">Bo'lim nomi.</param>
/// <param name="Position">Lavozim nomi.</param>
/// <param name="HireDate">Ishga kirgan sana.</param>
/// <param name="WorkedDays">
/// Bugungi kunga qancha ishlagani. HR qarorini aynan shu raqamga qarab
/// qabul qiladi — 5000 kun ishlagan odam sinovda tura olmaydi.
/// </param>
public sealed record MissingProbationDto(
    Guid EmploymentId,
    Guid EmployeeId,
    string EmployeeName,
    string? PhotoKey,
    string? LegalEntity,
    string? Department,
    string? Position,
    DateOnly HireDate,
    int WorkedDays);

public sealed class ListMissingProbationsQueryHandler
{
    private readonly IEmployeeReadModel _readModel;

    public ListMissingProbationsQueryHandler(IEmployeeReadModel readModel)
        => _readModel = readModel;

    public async Task<Result<IReadOnlyList<MissingProbationDto>>> HandleAsync(
        ListMissingProbationsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rows = await _readModel
            .ListMissingProbationsAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<MissingProbationDto> items =
        [
            .. rows.Select(row => new MissingProbationDto(
                row.EmploymentId,
                row.EmployeeId,
                row.EmployeeName,
                row.PhotoKey,
                row.LegalEntityName,
                row.DepartmentName,
                row.PositionName,
                row.HireDate,
                row.WorkedDays)),
        ];

        return Result.Success(items);
    }
}

/// <summary>
/// Belgilangan munosabatlarni doimiy holatga o'tkazadi.
/// </summary>
/// <param name="EmploymentIds">HR belgilagan qatorlar.</param>
/// <param name="At">Qaysi sanadan. Bo'sh bo'lsa — bugun.</param>
/// <param name="Note">Umumiy izoh — tarixda hodisa matni bo'lib qoladi.</param>
public sealed record ConfirmMissingProbationsCommand(
    IReadOnlyList<Guid> EmploymentIds,
    DateOnly? At = null,
    string? Note = null);

/// <summary>Ommaviy tasdiqlash natijasi.</summary>
/// <param name="Confirmed">Nechtasi doimiy holatga o'tdi.</param>
/// <param name="Skipped">
/// Nechtasi o'tkazib yuborildi — topilmadi yoki holati allaqachon boshqa.
/// Ular XATO EMAS: HR ro'yxatni ochib turganda boshqa birov o'zgartirgan
/// bo'lishi mumkin va butun amalni shu sababli bekor qilish noto'g'ri
/// bo'lardi.
/// </param>
public sealed record ConfirmMissingProbationsResultDto(int Confirmed, int Skipped);

/// <summary>
/// Ommaviy tasdiqlash.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hammasi bitta tranzaksiyada.</b> <c>SaveChangesAsync</c> oxirida bir
/// marta chaqiriladi: HR «120 tasini tasdiqladim» degan bo'lsa, natija
/// 120 ta yoki 0 ta bo'lishi kerak. Yarim bajarilgan ro'yxat eng yomon
/// natija — qaysi biri o'tganini keyin aniqlab bo'lmaydi.
/// </para>
/// <para>
/// Har bir munosabat uchun <c>CompleteProbation</c> chaqiriladi, ya'ni
/// tarixda «sinov natijasi» hodisasi qoladi. Holatni to'g'ridan-to'g'ri
/// o'zgartirish mumkin edi va bu tezroq bo'lardi — lekin u holda mehnat
/// yo'li lentasida odam «sinovda» dan «ishlayapti» ga sababsiz sakragan
/// bo'lib ko'rinardi.
/// </para>
/// </remarks>
public sealed class ConfirmMissingProbationsCommandHandler
{
    /// <summary>Bitta so'rovda tasdiqlanadigan eng ko'p yozuv.</summary>
    /// <remarks>
    /// Chegara ataylab: ro'yxat ekranda ko'rinadigan hajmda bo'ladi va
    /// mingta identifikator kelgani — mijoz tomonidagi xato yoki suiste'mol
    /// belgisi. Ko'chirishdan keyin kutilgan hajm bir necha yuz qator.
    /// </remarks>
    public const int MaxBatch = 500;

    /// <summary>Izoh berilmaganda tarixga yoziladigan matn.</summary>
    private const string DefaultNote =
        "Ko'chirilgan yozuv: sinov muddati ma'lumoti Excel'da yo'q edi, "
        + "holat HR tasdig'i bilan doimiyga o'tkazildi";

    private readonly IEmployeeReadModel _readModel;
    private readonly IEmploymentRepository _employments;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmMissingProbationsCommandHandler(
        IEmployeeReadModel readModel,
        IEmploymentRepository employments,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _readModel = readModel;
        _employments = employments;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConfirmMissingProbationsResultDto>> HandleAsync(
        ConfirmMissingProbationsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.EmploymentIds.Count == 0)
        {
            return Error.Validation(
                "sinov.tanlanmagan",
                "Tasdiqlash uchun kamida bitta yozuv belgilanishi kerak.");
        }

        if (command.EmploymentIds.Count > MaxBatch)
        {
            return Error.Validation(
                "sinov.juda_kop",
                $"Bir so'rovda eng ko'pi bilan {MaxBatch} ta yozuv tasdiqlanadi.");
        }

        // ⚠️ Ro'yxat QAYTA olinadi va mijozdan kelgan identifikatorlar
        // faqat SHU ro'yxat bilan kesishtiriladi. Busiz «tasdiqlash»
        // istalgan munosabatning holatini o'zgartira oladigan yo'lga
        // aylanardi: mijoz ID yuboradi, server esa uni tekshirmasdan
        // doimiy qilib qo'yardi.
        var candidates = await _readModel
            .ListMissingProbationsAsync(cancellationToken)
            .ConfigureAwait(false);

        var allowed = candidates.Select(row => row.EmploymentId).ToHashSet();

        var at = command.At ?? _clock.Today;
        var note = string.IsNullOrWhiteSpace(command.Note) ? DefaultNote : command.Note.Trim();

        var confirmed = 0;
        var skipped = 0;

        foreach (var employmentId in command.EmploymentIds.Distinct())
        {
            if (!allowed.Contains(employmentId))
            {
                skipped++;
                continue;
            }

            var employment = await _employments
                .GetWithEventsAsync(employmentId, cancellationToken)
                .ConfigureAwait(false);

            if (employment is not { Status: EmploymentStatus.Probation })
            {
                skipped++;
                continue;
            }

            var completed = employment.CompleteProbation(at, note);
            if (completed.IsFailure)
            {
                return completed.Error;
            }

            confirmed++;
        }

        if (confirmed > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new ConfirmMissingProbationsResultDto(confirmed, skipped));
    }
}
