using System.Globalization;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Etl.Excel;
using Bonvi.Etl.Mapping;
using Bonvi.Etl.Reporting;

namespace Bonvi.Etl.Runtime;

/// <summary>Hisobotda qaysi qatordan gap ketayotganini ko'rsatadi.</summary>
/// <param name="Step">Qadam nomi.</param>
/// <param name="Sheet">Varaq nomi.</param>
/// <param name="Row">Qator raqami.</param>
/// <param name="Employee">Xodim (F.I.Sh yoki bo'sh satr).</param>
public readonly record struct RowContext(string Step, string Sheet, int Row, string Employee)
{
    /// <summary>Xodim nomi ma'lum bo'lgach yangilanadi.</summary>
    public RowContext For(string employee) => this with { Employee = employee };
}

/// <summary>Qatordagi xodimni qidirish natijasi.</summary>
/// <param name="Employee">Topilgan xodim yoki <c>null</c>.</param>
/// <param name="Name">O'qilgan F.I.Sh — hisobotda va kontekstda ishlatiladi.</param>
public readonly record struct EmployeeLookup(Employee? Employee, FullName? Name)
{
    /// <summary>Hech narsa topilmadi.</summary>
    public static readonly EmployeeLookup None = new(null, null);

    /// <summary>Xodim aniq topildi.</summary>
    public bool Found => Employee is not null;

    /// <summary>Hisobot uchun ism (topilmagan bo'lsa ham bo'sh qolmaydi).</summary>
    public string Label => Name?.Full ?? string.Empty;
}

/// <summary>Mehnat munosabati uchun tanlangan yuridik shaxs.</summary>
/// <param name="Id">Yuridik shaxs identifikatori yoki <c>null</c>.</param>
/// <param name="Assumed">
/// Excel'da ko'rsatilmagan, standart qiymat olindimi. Bunday munosabat
/// <c>NeedsReview</c> bilan belgilanadi — HR uni albatta ko'radi.
/// </param>
public readonly record struct LegalEntityChoice(Guid? Id, bool Assumed);

/// <summary>
/// Ko'chirishning umumiy ish maydoni: yuklangan holat, ma'lumotnomalar,
/// hisobot va qiymat aylantirish qoidalari.
/// </summary>
/// <remarks>
/// <para>
/// Butun mavjud holat (xodimlar, mehnat munosabatlari, sinov muddatlari,
/// belgilar) boshida XOTIRAGA o'qiladi. Hajmi kichik (~2500 xodim,
/// ~5000 hodisa), lekin foydasi katta: har bir Excel qatori uchun alohida
/// <c>SELECT</c> yuborilmaydi va idempotentlik tekshiruvi bir xil
/// mantiq bo'yicha — bazadan kelgan yozuv uchun ham, shu yugurishda
/// yaratilgan yozuv uchun ham — ishlaydi.
/// </para>
/// <para>
/// Qiymat aylantirish metodlari <b>doim hisobotga yozadi</b>: qaysi katak
/// tushunilmagani keyinchalik HR ko'radigan yagona hujjat.
/// </para>
/// </remarks>
public sealed class EtlWorkspace
{
    private readonly Dictionary<Guid, List<Employment>> _employmentsByEmployee = [];
    private readonly Dictionary<Guid, List<Probation>> _probationsByEmployment = [];
    private readonly HashSet<string> _eventKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _flagKeys = new(StringComparer.Ordinal);

    /// <summary>Xodim → uning OCHIQ belgisi (tugash sanasi yo'q).</summary>
    /// <remarks>
    /// «Bir vaqtda atigi bitta kuchdagi belgi» qoidasi shu reyestrga
    /// tayanadi. Bazadan yuklangan belgilar ham, shu yugurishda
    /// yaratilganlar ham bir xil <see cref="Register(EmployeeFlag)"/>
    /// dan o'tadi — ya'ni ikki manba orasidagi to'qnashuv ham ko'rinadi,
    /// faqat Excel varaqlari orasidagisi emas.
    /// </remarks>
    private readonly Dictionary<Guid, EmployeeFlag> _openFlags = [];
    private readonly Dictionary<Guid, EmployeeCompetency> _competencies = [];
    private readonly HashSet<string> _incentiveKeys = new(StringComparer.Ordinal);

    /// <summary>Voronka bosqichlari nomi bo'yicha («Кенгаш»).</summary>
    private readonly Dictionary<string, PipelineStage> _stages = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Vakansiya nomlari — arxiv vakansiyasi uchun.</summary>
    private readonly Dictionary<string, VacancyTitle> _vacancyTitles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Vakansiyalar nomi identifikatori bo'yicha.</summary>
    private readonly Dictionary<Guid, Vacancy> _vacanciesByTitle = [];

    /// <summary>
    /// Nomzodlar: «vakansiya + ism + tug'ilgan sana» kaliti bo'yicha.
    /// </summary>
    /// <remarks>
    /// Kalitda vakansiya ham bor: bitta odam ikki xil vakansiyaga ariza
    /// bergan bo'lishi mumkin va ular AYRIM yozuv. Tug'ilgan sana esa
    /// bir xil ismli ikki nomzodni ajratadi — kengash varag'ida bunday
    /// juftliklar uchraydi.
    /// </remarks>
    private readonly Dictionary<string, Candidate> _candidates = new(StringComparer.Ordinal);

    public EtlWorkspace(
        EtlOptions options,
        ExcelWorkbook workbook,
        AliasTable aliases,
        CatalogIndex catalogs,
        EtlDatabase database,
        EtlReport report)
    {
        Options = options;
        Workbook = workbook;
        Aliases = aliases;
        Catalogs = catalogs;
        Database = database;
        Report = report;
        Employees = new EmployeeIndex();
    }

    public EtlOptions Options { get; }

    public ExcelWorkbook Workbook { get; }

    public AliasTable Aliases { get; }

    public CatalogIndex Catalogs { get; }

    public EtlDatabase Database { get; }

    public EtlReport Report { get; }

    /// <summary>Xodimlar indeksi — takrorlanmaslik uchun.</summary>
    public EmployeeIndex Employees { get; }

    /// <summary>Yuridik shaxs ko'rsatilmaganda ishlatiladigan identifikator.</summary>
    public Guid? DefaultLegalEntityId
        => Catalogs.Find(CatalogKind.LegalEntity, Options.DefaultLegalEntityCode);

    // ────────────────────────────────────────────────────────────
    //  Holat ro'yxati
    // ────────────────────────────────────────────────────────────

    /// <summary>Bazadan yuklangan yoki yangi yaratilgan xodimni ro'yxatga oladi.</summary>
    public void Register(Employee employee) => Employees.Add(employee);

    /// <summary>Mehnat munosabatini ro'yxatga oladi (hodisalari bilan).</summary>
    public void Register(Employment employment)
    {
        ArgumentNullException.ThrowIfNull(employment);

        if (!_employmentsByEmployee.TryGetValue(employment.EmployeeId, out var list))
        {
            list = [];
            _employmentsByEmployee[employment.EmployeeId] = list;
        }

        list.Add(employment);

        foreach (var employmentEvent in employment.Events)
        {
            _ = _eventKeys.Add(EventKey(
                employment.Id,
                employmentEvent.Type,
                employmentEvent.EffectiveDate,
                employmentEvent.OrderNo));
        }
    }

    /// <summary>Sinov muddatini ro'yxatga oladi.</summary>
    public void Register(Probation probation)
    {
        ArgumentNullException.ThrowIfNull(probation);

        if (!_probationsByEmployment.TryGetValue(probation.EmploymentId, out var list))
        {
            list = [];
            _probationsByEmployment[probation.EmploymentId] = list;
        }

        list.Add(probation);
    }

    /// <summary>Belgini ro'yxatga oladi.</summary>
    public void Register(EmployeeFlag flag)
    {
        ArgumentNullException.ThrowIfNull(flag);
        _ = _flagKeys.Add(FlagKey(flag.EmployeeId, flag.Type, flag.StartDate));

        // Yopiq belgi hech kimga xalaqit bermaydi — u tarix.
        if (flag.EndDate is null)
        {
            _openFlags[flag.EmployeeId] = flag;
        }
        else
        {
            // Belgi shu yugurishda YOPILGAN bo'lishi mumkin (to'qnashuv
            // yechimi). O'shanda reyestrdan ham chiqarilishi kerak, aks
            // holda keyingi varaq uni hamon «ochiq» deb ko'rardi.
            if (_openFlags.TryGetValue(flag.EmployeeId, out var open) && open.Id == flag.Id)
            {
                _ = _openFlags.Remove(flag.EmployeeId);
            }
        }
    }

    /// <summary>Xodimning ochiq belgisi — bo'lmasa <c>null</c>.</summary>
    public EmployeeFlag? OpenFlagOf(Guid employeeId) => _openFlags.GetValueOrDefault(employeeId);

    /// <summary>Qobiliyat profilini ro'yxatga oladi.</summary>
    /// <remarks>
    /// Boshqa yozuvlardan farqli o'laroq bu yerda kalitlar to'plami emas,
    /// obyektning O'ZI saqlanadi. Sabab: xodimda profil bitta va u ikkinchi
    /// marta yaratilmaydi — mavjudining bo'sh maydonlari to'ldiriladi, ya'ni
    /// «bormi?» degan javob yetarli emas, obyektni topish kerak.
    /// </remarks>
    public void Register(EmployeeCompetency competency)
    {
        ArgumentNullException.ThrowIfNull(competency);

        // `TryAdd`: bazada bir xodimga ikkita profil paydo bo'lib qolsa
        // (unikal indeks bo'lsa ham, ETL bunga tayanmaydi) birinchisi kuchda
        // qoladi. Ikkinchisini «to'g'rirog'i» deb tanlashning asosi yo'q, va
        // uni jimgina to'ldirish ikkalasini ham chalkashtirardi.
        _ = _competencies.TryAdd(competency.EmployeeId, competency);
    }

    /// <summary>Rag'bat yoki jarimani ro'yxatga oladi.</summary>
    public void Register(Incentive incentive)
    {
        ArgumentNullException.ThrowIfNull(incentive);
        _ = _incentiveKeys.Add(IncentiveKey(incentive.EmployeeId, incentive.Kind, incentive.TypeId, incentive.Date));
    }

    // ────────────────────────────────────────────────────────────
    //  Rekruting — kengash qadami uchun
    // ────────────────────────────────────────────────────────────

    /// <summary>Voronka bosqichini ro'yxatga oladi.</summary>
    public void Register(PipelineStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        _stages[stage.Name] = stage;
    }

    /// <summary>Bosqichni NOMI bo'yicha topadi («Кенгаш»).</summary>
    public PipelineStage? StageNamed(string name) => _stages.GetValueOrDefault(name);

    /// <summary>Vakansiya nomini ro'yxatga oladi.</summary>
    public void Register(VacancyTitle title)
    {
        ArgumentNullException.ThrowIfNull(title);
        _vacancyTitles[title.Name] = title;
    }

    /// <summary>Vakansiya nomini NOMI bo'yicha topadi.</summary>
    public VacancyTitle? VacancyTitleNamed(string name) => _vacancyTitles.GetValueOrDefault(name);

    /// <summary>
    /// Vakansiyani ro'yxatga oladi.
    /// </summary>
    /// <remarks>
    /// Kalit — vakansiya NOMINING identifikatori. Kengash qadami arxiv
    /// uchun BITTA vakansiya ochadi va ikkinchi yugurishda aynan
    /// o'shanisini qayta topishi kerak, aks holda har safar yangisi
    /// ochilib, nomzodlar bir necha vakansiyaga tarqab ketardi.
    /// </remarks>
    public void Register(Vacancy vacancy)
    {
        ArgumentNullException.ThrowIfNull(vacancy);
        _ = _vacanciesByTitle.TryAdd(vacancy.VacancyTitleId, vacancy);
    }

    /// <summary>Shu nomdagi vakansiya (bo'lmasa <c>null</c>).</summary>
    public Vacancy? VacancyOfTitle(Guid vacancyTitleId) => _vacanciesByTitle.GetValueOrDefault(vacancyTitleId);

    /// <summary>Nomzodni ro'yxatga oladi.</summary>
    public void Register(Candidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        _candidates[CandidateKey(candidate.VacancyId, candidate.FullName, candidate.BirthDate)] = candidate;
    }

    /// <summary>Shu vakansiyadagi shu odam (bo'lmasa <c>null</c>).</summary>
    public Candidate? CandidateOf(Guid vacancyId, FullName fullName, DateOnly? birthDate)
        => _candidates.GetValueOrDefault(CandidateKey(vacancyId, fullName, birthDate));

    /// <summary>
    /// Nomzod kaliti: vakansiya + ism + tug'ilgan sana.
    /// </summary>
    /// <remarks>
    /// Ism <see cref="EmployeeIndex.NameKey"/> orqali normallashtiriladi —
    /// ya'ni xodim qidiruvi bilan AYNAN bir xil qoida (kirill gomogliflari,
    /// «ўғли»/«угли» farqi). Tug'ilgan sana kalitning ikkinchi qismi:
    /// kengash varag'ida bir xil ismli, boshqa odamlar uchraydi va ularni
    /// bitta nomzodga qo'shib yuborish tarixni buzardi.
    /// </remarks>
    private static string CandidateKey(Guid vacancyId, FullName fullName, DateOnly? birthDate)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{vacancyId:N}|{EmployeeIndex.NameKey(fullName)}|{birthDate?.DayNumber ?? 0}");

    /// <summary>Xodimning barcha mehnat munosabatlari.</summary>
    public IReadOnlyList<Employment> EmploymentsOf(Guid employeeId)
        => _employmentsByEmployee.TryGetValue(employeeId, out var list) ? list : [];

    /// <summary>
    /// Berilgan sanada kuchda bo'lgan mehnat munosabatini tanlaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bitta xodimda bir nechta munosabat bo'lishi mumkin: o'rindoshlik yoki
    /// ketib-qaytish. Hodisani qaysi biriga yozishni sana hal qiladi.
    /// </para>
    /// <para>
    /// Sana hech qaysisiga tushmasa — eng yaqin boshlangani olinadi.
    /// Bu <b>taxmin</b>, shu sababli chaqiruvchi buni hisobotga yozadi.
    /// </para>
    /// </remarks>
    public Employment? EmploymentOn(Guid employeeId, DateOnly date, out bool exact)
    {
        exact = false;

        var all = EmploymentsOf(employeeId);
        if (all.Count == 0)
        {
            return null;
        }

        Employment? best = null;

        foreach (var employment in all)
        {
            var covers = employment.HireDate <= date
                && (employment.TerminationDate is null || employment.TerminationDate >= date);

            if (covers && (best is null || employment.HireDate > best.HireDate))
            {
                best = employment;
                exact = true;
            }
        }

        if (best is not null)
        {
            return best;
        }

        // Qamrab olmasa — sanadan oldin boshlangan eng oxirgisi, u ham
        // bo'lmasa umuman eng birinchisi.
        foreach (var employment in all)
        {
            if (employment.HireDate <= date && (best is null || employment.HireDate > best.HireDate))
            {
                best = employment;
            }
        }

        if (best is not null)
        {
            return best;
        }

        best = all[0];
        foreach (var employment in all)
        {
            if (employment.HireDate < best.HireDate)
            {
                best = employment;
            }
        }

        return best;
    }

    /// <summary>Xodimning bir yuridik shaxs bilan aynan shu sanadagi munosabati.</summary>
    public Employment? EmploymentFor(Guid employeeId, Guid legalEntityId, DateOnly hireDate)
        => EmploymentsOf(employeeId)
            .FirstOrDefault(employment =>
                employment.LegalEntityId == legalEntityId && employment.HireDate == hireDate);

    /// <summary>Shu hodisa allaqachon yozilganmi.</summary>
    public bool HasEvent(Guid employmentId, EmploymentEventType type, DateOnly effectiveDate, string? orderNo)
        => _eventKeys.Contains(EventKey(employmentId, type, effectiveDate, orderNo));

    /// <summary>Hodisani mehnat munosabatiga qo'shadi va idempotentlik kalitini eslab qoladi.</summary>
    public void AddEvent(Employment employment, EmploymentEvent employmentEvent)
    {
        ArgumentNullException.ThrowIfNull(employment);
        ArgumentNullException.ThrowIfNull(employmentEvent);

        employment.ImportEvent(employmentEvent);

        _ = _eventKeys.Add(EventKey(
            employment.Id,
            employmentEvent.Type,
            employmentEvent.EffectiveDate,
            employmentEvent.OrderNo));
    }

    /// <summary>Shu sanada boshlangan mehnat munosabati (yuridik shaxsdan qat'i nazar).</summary>
    /// <remarks>
    /// «91.2 Янги ишга келганлар» varag'ida yuridik shaxs ustuni yo'q, shu
    /// sababli takrorlanish faqat xodim va ishga kirish sanasi bo'yicha
    /// aniqlanadi. Bir odam bir kunda ikki korxonaga rasmiylashtirilgan
    /// hol Excel'da uchramaydi.
    /// </remarks>
    public Employment? EmploymentStartedOn(Guid employeeId, DateOnly hireDate)
        => EmploymentsOf(employeeId).FirstOrDefault(employment => employment.HireDate == hireDate);

    /// <summary>
    /// Ishdan bo'shatishni yozadi va idempotentlik kalitini eslab qoladi.
    /// </summary>
    /// <remarks>
    /// <c>Employment.ImportTermination</c> ustidan yupqa qatlam: hodisa
    /// domen ichida yaratilgani uchun uni <see cref="AddEvent"/> orqali
    /// qo'shib bo'lmaydi, lekin kalit baribir ro'yxatga tushishi kerak —
    /// aks holda «91.8» varag'i o'sha bo'shatishni ikkinchi marta yozardi.
    /// </remarks>
    public Result Terminate(
        Employment employment,
        DateOnly terminationDate,
        Guid? reasonId,
        string? orderNo = null,
        DateOnly? orderDate = null,
        string? note = null)
    {
        ArgumentNullException.ThrowIfNull(employment);

        var result = employment.ImportTermination(terminationDate, reasonId, orderNo, orderDate, note);

        if (result.IsSuccess)
        {
            _ = _eventKeys.Add(EventKey(
                employment.Id,
                EmploymentEventType.Termination,
                terminationDate,
                orderNo));
        }

        return result;
    }

    /// <summary>Bu munosabat bo'yicha ishdan bo'shatish allaqachon yozilganmi.</summary>
    /// <remarks>
    /// <see cref="HasEvent"/> dan farqi: buyruq raqami hisobga OLINMAYDI.
    /// «91.8 Стажировкадан кетганлар» — «91.6» ning bir qismi, lekin unda
    /// buyruq raqami ustuni yo'q. Kalit bo'yicha solishtirilsa, bir bo'shatish
    /// ikki marta yozilardi.
    /// </remarks>
    public static bool IsTerminated(Employment employment)
    {
        ArgumentNullException.ThrowIfNull(employment);
        return employment.Status == EmploymentStatus.Terminated;
    }

    /// <summary>Mehnat munosabatining sinov muddati bormi.</summary>
    public bool HasProbation(Guid employmentId)
        => _probationsByEmployment.TryGetValue(employmentId, out var list) && list.Count > 0;

    /// <summary>Shu belgi allaqachon qo'yilganmi.</summary>
    public bool HasFlag(Guid employeeId, EmployeeFlagType type, DateOnly startDate)
        => _flagKeys.Contains(FlagKey(employeeId, type, startDate));

    /// <summary>Xodimning qobiliyat profili (bo'lmasa <c>null</c>).</summary>
    public EmployeeCompetency? CompetencyOf(Guid employeeId)
        => _competencies.GetValueOrDefault(employeeId);

    /// <summary>
    /// Shu rag'bat/jarima allaqachon yozilganmi.
    /// </summary>
    /// <remarks>
    /// Kalitda buyruq raqami YO'Q — «91.14» varag'ida bunday ustun umuman
    /// yo'q, ya'ni uni kalitga qo'shish har bir qatorni har yugurishda «yangi»
    /// qilib ko'rsatardi va idempotentlik yo'qolardi. Bir odamga bir kunda
    /// bir xil turdagi ikkita rag'bat berilgan hol varaqda uchramaydi —
    /// tekshirib chiqilgan.
    /// </remarks>
    public bool HasIncentive(Guid employeeId, IncentiveKind kind, Guid typeId, DateOnly date)
        => _incentiveKeys.Contains(IncentiveKey(employeeId, kind, typeId, date));

    // ────────────────────────────────────────────────────────────
    //  Katakni qiymatga aylantirish
    // ────────────────────────────────────────────────────────────

    /// <summary>Sanani o'qiydi. Qiymat bor, lekin sana bo'lmasa — hisobotga yoziladi.</summary>
    public DateOnly? Date(RowContext context, ExcelCell cell, string field)
    {
        if (cell.IsEmpty)
        {
            return null;
        }

        var date = ExcelDate.Read(cell);

        if (date is null)
        {
            Report.Add(context.Step, context.Sheet, context.Row, context.Employee,
                $"{field}: sana o'qilmadi", cell.Text, EtlAction.LeftEmpty);
        }

        return date;
    }

    /// <summary>Bo'limni topadi: 6-ustun, u bo'lmasa varaqdagi blok sarlavhasi.</summary>
    /// <remarks>
    /// <para>
    /// Asosiy varaq 49 ta blokka bo'lingan va 80 ta Rispetto xodimi uchun
    /// 6-ustunda shunchaki <c>"Rispetto" МЧЖ</c> yozilgan — haqiqiy bo'lim
    /// faqat blok sarlavhasida bor. Shu sababli zavod darajasidagi qiymat
    /// kelganda blok ustun turadi.
    /// </para>
    /// </remarks>
    public Guid? Department(RowContext context, ExcelCell cell, string? blockCode)
    {
        var match = Aliases.Department(cell);
        var code = match.First;

        // Zavod — eng umumiy daraja. Blok aniqroq bo'lsa, u tanlanadi.
        if (blockCode is { Length: > 0 } && (code is null || IsFactoryRoot(code)))
        {
            code = blockCode;
        }

        if (code is null)
        {
            if (!match.Known && cell.HasValue)
            {
                Report.Add(context.Step, context.Sheet, context.Row, context.Employee,
                    "bo'lim ma'lumotnomada yo'q", cell.Text, EtlAction.LeftEmpty);
            }

            return null;
        }

        var id = Catalogs.Find(CatalogKind.Department, code);

        if (id is null)
        {
            Report.Add(context.Step, context.Sheet, context.Row, context.Employee,
                "bo'lim kodi bazada yo'q", code, EtlAction.LeftEmpty);
        }

        return id;
    }

    /// <summary>Lavozimni topadi.</summary>
    public Guid? Position(RowContext context, ExcelCell cell)
    {
        var match = Aliases.Position(cell);

        if (match.First is not { } code)
        {
            if (!match.Known && cell.HasValue)
            {
                Report.Add(context.Step, context.Sheet, context.Row, context.Employee,
                    "lavozim ma'lumotnomada yo'q", cell.Text, EtlAction.LeftEmpty);
            }

            return null;
        }

        var id = Catalogs.Find(CatalogKind.Position, code);

        if (id is null)
        {
            Report.Add(context.Step, context.Sheet, context.Row, context.Employee,
                "lavozim kodi bazada yo'q", code, EtlAction.LeftEmpty);
        }

        return id;
    }

    /// <summary>Yo'nalishlarni topadi — bitta katakda bir nechtasi bo'lishi mumkin.</summary>
    public IReadOnlyList<Guid> Directions(RowContext context, ExcelCell cell)
    {
        var match = Aliases.Direction(cell);

        if (!match.Known && cell.HasValue)
        {
            Report.Add(context.Step, context.Sheet, context.Row, context.Employee,
                "yo'nalish ma'lumotnomada yo'q", cell.Text, EtlAction.LeftEmpty);
            return [];
        }

        return Catalogs.FindAll(CatalogKind.Direction, match.Codes);
    }

    /// <summary>Yuridik shaxs(lar)ni topadi.</summary>
    public IReadOnlyList<Guid> LegalEntities(RowContext context, ExcelCell cell)
    {
        var match = Aliases.LegalEntity(cell);

        if (!match.Known && cell.HasValue)
        {
            Report.Add(context.Step, context.Sheet, context.Row, context.Employee,
                "yuridik shaxs ma'lumotnomada yo'q", cell.Text, EtlAction.LeftEmpty);
            return [];
        }

        return Catalogs.FindAll(CatalogKind.LegalEntity, match.Codes);
    }

    /// <summary>Ishdan ketish sababini topadi.</summary>
    public Guid? TerminationReason(RowContext context, ExcelCell cell)
    {
        var match = Aliases.TerminationReason(cell);

        if (match.First is not { } code)
        {
            if (!match.Known && cell.HasValue)
            {
                Report.Add(context.Step, context.Sheet, context.Row, context.Employee,
                    "ishdan ketish sababi ma'lumotnomada yo'q", cell.Text, EtlAction.LeftEmpty);
            }

            return null;
        }

        return Catalogs.Find(CatalogKind.TerminationReason, code);
    }

    /// <summary>
    /// Rag'bat turini topadi — identifikatori va «pullimi» belgisi bilan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// «91.14» varag'ining <c>«Вид благодарности»</c> ustunida ma'lumotnoma
    /// nomi emas, <b>jumla</b> turibdi: <c>«Йил якунида ташаккурнома
    /// берилди»</c>. Shu sababli qiymat avval <c>aliases.json</c> dan
    /// o'tkaziladi va kanonik nomga (<c>«Ташаккурнома»</c>) aylantiriladi —
    /// xuddi bo'lim va lavozim kabi.
    /// </para>
    /// <para>
    /// Javob <see cref="Guid"/> emas, <see cref="CatalogEntry"/>:
    /// <c>Incentive.Grant</c> turning identifikatori bilan birga «summa
    /// kerakmi» degan javobni ham talab qiladi va ularni ikki alohida
    /// qidiruvdan yig'ish bir turning identifikatorini boshqasining qoidasi
    /// bilan qo'shib yuborish xavfini tug'dirardi.
    /// </para>
    /// </remarks>
    public CatalogEntry? AwardType(RowContext context, ExcelCell cell)
    {
        var match = Aliases.AwardType(cell);

        if (match.First is not { } name)
        {
            if (!match.Known && cell.HasValue)
            {
                Report.Add(context.Step, context.Sheet, context.Row, context.Employee,
                    "rag'bat turi ma'lumotnomada yo'q", cell.Text, EtlAction.LeftEmpty);
            }

            return null;
        }

        var entry = Catalogs.Entry(CatalogKind.AwardType, name);

        if (entry is null)
        {
            Report.Add(context.Step, context.Sheet, context.Row, context.Employee,
                "rag'bat turi bazada yo'q", name, EtlAction.LeftEmpty);
        }

        return entry;
    }

    /// <summary>
    /// Buyruq raqamini o'qiydi.
    /// </summary>
    /// <remarks>
    /// Excel'da u ham son (<c>185</c>), ham matn (<c>«25 П»</c>) bo'lishi
    /// mumkin. Son bo'lsa kasr qism olib tashlanadi — <c>«185.0»</c> buyruq
    /// raqami emas.
    /// </remarks>
    public static string? OrderNo(ExcelCell cell)
    {
        if (cell.IsEmpty)
        {
            return null;
        }

        if (cell.IsWholeNumber && cell.Number is { } number)
        {
            return ((long)Math.Round(number)).ToString(CultureInfo.InvariantCulture);
        }

        return cell.Text;
    }

    /// <summary>
    /// Oklad katagini o'qiydi. Tushunarsiz qiymat hisobotga tushadi.
    /// </summary>
    /// <remarks>
    /// <c>«Ишбай»</c> va <c>«Ўзгаришсиз»</c> hisobotga YOZILMAYDI: ular xato
    /// emas, HR ataylab yozgan holat. Hisobotga faqat o'qilmagan katak tushadi.
    /// </remarks>
    public SalaryCellResult Salary(RowContext context, ExcelCell cell, string field)
    {
        var result = SalaryCell.Parse(cell);

        if (result.Kind == SalaryCellKind.Unknown)
        {
            Report.Add(context.Step, context.Sheet, context.Row, context.Employee,
                $"{field}: oklad o'qilmadi", result.Raw, EtlAction.LeftEmpty);
        }

        return result;
    }

    /// <summary>
    /// Qatordagi xodimni topadi — <b>yangi xodim yaratmaydi</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hodisa varaqlari (91.3, 91.4, 91.6…) faqat mavjud odam haqida gapiradi.
    /// Agar u topilmasa — bu xodimlar qadami uni o'tkazib yuborganini bildiradi
    /// va hodisani «kimgadir» yozib qo'yish emas, hisobotga chiqarish to'g'ri.
    /// </para>
    /// <para>
    /// Bir xil ismli bir nechta xodim bo'lsa ham TANLANMAYDI: noto'g'ri odamga
    /// yozilgan ishdan bo'shatish buyrug'i — yo'qolgan yozuvdan yomonroq.
    /// </para>
    /// </remarks>
    public EmployeeLookup FindEmployee(
        RowContext context,
        ExcelCell nameCell,
        ExcelCell birthCell,
        ExcelCell pinflCell,
        ExcelCell passportCell)
    {
        // Sarlavha tekshiruvi ajratishdan OLDIN: «Ходимнинг Ф.И.Ш» uch
        // so'zdan iborat va to'liq ism sifatida muvaffaqiyatli ajraladi.
        // Faqat ajratish xatosiga tayansak, u «xodim topilmadi» bo'lib
        // hisobotga tushardi.
        if (IsHeaderRow(nameCell))
        {
            return EmployeeLookup.None;
        }

        var parsed = FullName.Parse(nameCell.Text);

        if (parsed.IsFailure)
        {
            Report.Add(context.Step, context.Sheet, context.Row, nameCell.Text,
                "ism ajratilmadi", parsed.Error.Message, EtlAction.RowSkipped);

            return EmployeeLookup.None;
        }

        var fullName = parsed.Value;

        var pinfl = Accept(Pinfl.Create, pinflCell);
        var passport = Accept(PassportSeries.Create, passportCell);
        var birthDate = birthCell.IsEmpty ? null : ExcelDate.Read(birthCell);

        var match = Employees.Find(pinfl, passport, fullName, birthDate);

        if (match.Found)
        {
            return new EmployeeLookup(match.Employee, fullName);
        }

        // Tug'ilgan sanasi bor, lekin mos kelmadi — faqat ism bo'yicha yana
        // bir imkoniyat beriladi. Sana Excel'da tez-tez xato kiritilgan, ism esa
        // deyarli hech qachon.
        if (match.Kind == EmployeeMatchKind.Ambiguous && birthDate is not null)
        {
            var byName = Employees.FindByName(fullName);

            if (byName.Found)
            {
                Report.Add(context.Step, context.Sheet, context.Row, fullName.Full,
                    "tug'ilgan sana bazadagidan farq qiladi — ism bo'yicha bog'landi",
                    birthCell.Text, EtlAction.Flagged);

                return new EmployeeLookup(byName.Employee, fullName);
            }
        }

        if (match.Kind == EmployeeMatchKind.Ambiguous)
        {
            Report.Add(context.Step, context.Sheet, context.Row, fullName.Full,
                $"bir xil ismli {match.Candidates} ta xodim bor — tanlanmadi",
                nameCell.Text, EtlAction.RowSkipped);

            return new EmployeeLookup(null, fullName);
        }

        // Otasining ismi tushib qolgan bo'lishi mumkin. Bunday nomzod
        // TANLANMAYDI — faqat hisobotda ko'rsatiladi, chunki bir xil
        // familiya va ismli aka-uka odatiy hol. Qarorni HR qabul qiladi.
        var suggestion = Employees.SuggestByShortName(fullName);

        Report.Add(
            context.Step,
            context.Sheet,
            context.Row,
            fullName.Full,
            suggestion is null
                ? "xodim topilmadi"
                : $"xodim topilmadi — ehtimoliy moslik: «{suggestion.FullName.Full}»",
            nameCell.Text,
            EtlAction.RowSkipped);

        return new EmployeeLookup(null, fullName);
    }

    /// <summary>
    /// Qiymat qabul qilinsa qaytaradi, aks holda <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Qidiruvda ishlatiladi, shu sababli xato hisobotga YOZILMAYDI: buzuq
    /// JSHSHIR haqida xodimlar qadami allaqachon xabar bergan, ikkinchi marta
    /// takrorlash hisobotni shovqinga to'ldirardi.
    /// </remarks>
    private static T? Accept<T>(Func<string, Result<T>> create, ExcelCell cell)
        where T : class
    {
        if (cell.IsEmpty)
        {
            return null;
        }

        var result = create(cell.Text);
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>Xodimni ism va tug'ilgan sana bo'yicha topadi.</summary>
    public EmployeeLookup FindEmployee(RowContext context, ExcelCell nameCell, ExcelCell birthCell)
        => FindEmployee(context, nameCell, birthCell, ExcelCell.Empty, ExcelCell.Empty);

    /// <summary>Xodimni faqat ism bo'yicha topadi.</summary>
    public EmployeeLookup FindEmployee(RowContext context, ExcelCell nameCell)
        => FindEmployee(context, nameCell, ExcelCell.Empty, ExcelCell.Empty, ExcelCell.Empty);

    /// <summary>
    /// Mehnat munosabati uchun yuridik shaxsni tanlaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Excel'da bu ustun 540 xodimdan 287 tasida bo'sh, hodisa varaqlarida esa
    /// umuman yo'q. Bo'sh bo'lsa standart kod olinadi — bu bloknot sarlavhasiga
    /// asoslangan dalil, lekin baribir TAXMIN, shu sababli
    /// <see cref="LegalEntityChoice.Assumed"/> bilan qaytariladi.
    /// </para>
    /// <para>
    /// Bir katakda bir nechta korxona bo'lsa (o'rindoshlik) — birinchisi
    /// asosiy hisoblanadi, qolganini chaqiruvchi o'zi hal qiladi.
    /// </para>
    /// </remarks>
    public LegalEntityChoice EmploymentLegalEntity(RowContext context, ExcelCell cell)
    {
        var found = LegalEntities(context, cell);

        if (found.Count > 0)
        {
            return new LegalEntityChoice(found[0], false);
        }

        return new LegalEntityChoice(DefaultLegalEntityId, true);
    }

    /// <summary>
    /// Katakda F.I.Sh emas, <b>takroriy ustun sarlavhasi</b> turibdimi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bloknot varaqlari yillar bo'yicha bloklarga bo'lingan va HR har bir
    /// blok boshida ustun sarlavhalarini qaytadan yozgan. Natijada varaq
    /// o'rtasida <c>«Ф.И.О»</c> yozilgan 79 ta qator uchraydi.
    /// </para>
    /// <para>
    /// Bu <b>xato emas, varaqning tuzilishi</b> — xuddi yil va oy
    /// sarlavhalari kabi. Shu sababli bunday qator jimgina tashlab
    /// yuboriladi: uni hisobotga yozish HR ning ish ro'yxatiga 79 ta
    /// bajarib bo'lmaydigan topshiriq qo'shardi.
    /// </para>
    /// </remarks>
    public static bool IsHeaderRow(ExcelCell nameCell)
    {
        if (nameCell.IsEmpty)
        {
            return false;
        }

        var key = AliasTable.Key(nameCell.Text).Replace(".", string.Empty, StringComparison.Ordinal);

        return key is "фио" or "фиш" or "ходимнинг фио" or "ходимнинг фиш"
            or "ходимнинг фамилияси исми шарифи";
    }

    /// <summary>Zavod darajasidagi bo'lim — blok sarlavhasi undan aniqroq.</summary>
    /// <remarks>
    /// Ro'yxat ma'lumotnomaning O'ZIDAN olinadi
    /// (<see cref="CatalogIndex.FactoryRootDepartments"/>): u yerda nima
    /// uchun qattiq ro'yxat yaramasligi batafsil yozilgan.
    /// </remarks>
    private static bool IsFactoryRoot(string name)
        => CatalogIndex.FactoryRootDepartments.Contains(name);

    private static string EventKey(Guid employmentId, EmploymentEventType type, DateOnly date, string? orderNo)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{employmentId:N}|{(int)type}|{date:yyyy-MM-dd}|{orderNo ?? string.Empty}");

    private static string FlagKey(Guid employeeId, EmployeeFlagType type, DateOnly startDate)
        => string.Create(CultureInfo.InvariantCulture, $"{employeeId:N}|{(int)type}|{startDate:yyyy-MM-dd}");

    private static string IncentiveKey(Guid employeeId, IncentiveKind kind, Guid typeId, DateOnly date)
        => string.Create(CultureInfo.InvariantCulture, $"{employeeId:N}|{(int)kind}|{typeId:N}|{date:yyyy-MM-dd}");
}
