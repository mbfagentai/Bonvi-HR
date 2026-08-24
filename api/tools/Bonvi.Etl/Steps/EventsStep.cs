using System.Globalization;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Etl.Excel;
using Bonvi.Etl.Mapping;
using Bonvi.Etl.Reporting;
using Bonvi.Etl.Runtime;

namespace Bonvi.Etl.Steps;

/// <summary>
/// 3-qadam: mehnat yo'lidagi hodisalarni ko'chiradi — lavozim va bo'lim
/// o'zgarishi, qo'shimcha vazifa, ishdan bo'shatish, sinov ichida ketish.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hodisa holatni o'zgartirmaydi.</b> Bu qadam <c>Transfer</c>,
/// <c>SetSalary</c> kabi domen metodlarini ATAYLAB chaqirmaydi va
/// hodisalarni <see cref="EmploymentEvent.Import"/> bilan yasab,
/// <c>EtlWorkspace.AddEvent</c> orqali qo'yadi. Sabab Excel'da: «91.3»
/// varag'ida tarix tartibi buzilgan — odam ishdan ketganidan keyin
/// yozilgan lavozim o'zgarishi ham bor. Domen metodlari bunday qatorni
/// (to'g'ri qiladi) rad etardi va tarixning bir qismi yo'qolardi.
/// <c>ImportEvent</c> esa munosabat holatiga tegmaydi: fakt saqlanadi,
/// joriy holat esa «91 Умумий» va «91.7 Архив» dan kelgan holicha qoladi.
/// </para>
/// <para>
/// <b>Yagona istisno — ishdan bo'shatish.</b> U holatni ham o'zgartiradi
/// (<c>EtlWorkspace.Terminate</c>), chunki «xodim ishlayaptimi» degan
/// savolga javob beradigan yagona ustun shu. Bu yerda domen tekshiruvlari
/// saqlanib qoladi va ular ishlaganda — qator hisobotga tushadi.
/// </para>
/// <para>
/// Ustunlar nom bo'yicha emas, RAQAM bo'yicha o'qiladi: to'rtala varaqda
/// ham sarlavha ikki qatorga bo'lingan, birlashtirilgan va takrorlanadi.
/// </para>
/// </remarks>
public sealed class EventsStep
{
    /// <summary>Qadam nomi (<c>--steps</c> uchun).</summary>
    public const string Name = "events";

    /// <summary>F.I.Sh — to'rtala varaqda ham ikkinchi ustun.</summary>
    private const int FullNameColumn = 2;

    private readonly EtlWorkspace _workspace;

    /// <summary>Shu qadamda belgilangan munosabatlar — jamlanmada ikki marta sanalmasin.</summary>
    private readonly HashSet<Guid> _flagged = [];

    public EventsStep(EtlWorkspace workspace) => _workspace = workspace;

    /// <summary>
    /// To'rtta varaqni <b>qat'iy tartibda</b> o'qiydi.
    /// </summary>
    /// <remarks>
    /// «91.8» eng oxirida turishi shart. U «91.6» ning bir qismi (231
    /// yozuvdan 143 tasi o'sha odam, o'sha ketish sanasi bilan «91.6» da
    /// ham bor), lekin unda buyruq raqami ustuni YO'Q — ya'ni
    /// <c>HasEvent</c> kaliti «91.6» dagidan farq qiladi va takrorni topa
    /// olmaydi. Yagona ishonchli dalil — munosabatning yakunlangani, u esa
    /// faqat «91.6» o'qilgandan keyin rost bo'ladi.
    /// </remarks>
    public void Run()
    {
        var summary = _workspace.Report.Step(Name);

        ReadPositionChanges(summary);
        ReadAdditionalDuties(summary);
        ReadTerminations(summary);
        ReadProbationExits(summary);
    }

    // ────────────────────────────────────────────────────────────
    //  91.3 — lavozim, bo'lim va oklad o'zgarishi
    // ────────────────────────────────────────────────────────────

    /// <summary>«91.3 Лавозими ўзгарган ходимлар» — 3028 qator.</summary>
    /// <remarks>
    /// Varaqda eski va yangi holat yonma-yon turadi. Bu yerda ular bitta
    /// hodisaning <c>From…</c> va <c>To…</c> maydonlariga tushadi — ro'yxat
    /// esa hodisalardan hosil qilinadi.
    /// </remarks>
    private void ReadPositionChanges(StepSummary summary)
    {
        foreach (var row in DataRows(PositionChanges.Sheet, PositionChanges.FirstDataRow, summary))
        {
            var context = new RowContext(Name, PositionChanges.Sheet, row.Number, string.Empty);

            // Bu varaqda tug'ilgan sana ustuni yo'q — qidiruv faqat ism bo'yicha.
            var lookup = _workspace.FindEmployee(context, row[FullNameColumn]);

            if (!lookup.Found)
            {
                summary.Skipped++;
                continue;
            }

            context = context.For(lookup.Label);

            var orderNo = EtlWorkspace.OrderNo(row[PositionChanges.OrderNo]);
            var orderDate = _workspace.Date(context, row[PositionChanges.OrderDate], "buyruq sanasi");

            // Hodisa sanasi — kuchga kirish sanasi (9-ustun). U bo'sh bo'lsa
            // buyruq sanasi olinadi: varaqda buyruq o'sha kuni kuchga kirgan
            // qatorlar ko'p, sanasiz hodisani esa umuman yozib bo'lmaydi.
            var effective = _workspace.Date(context, row[PositionChanges.EffectiveDate], "kuchga kirish sanasi")
                ?? orderDate;

            if (effective is not { } date)
            {
                _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                    "hodisa sanasi yo'q (na kuchga kirish, na buyruq sanasi)",
                    row.Text(PositionChanges.OrderNo), EtlAction.RowSkipped);
                summary.Skipped++;
                continue;
            }

            // 5- va 10-ustunlardagi yo'nalish ATAYLAB o'qilmaydi: yo'nalish
            // xodimga tegishli (u xodimlar qadamida qo'yilgan), mehnat
            // munosabatiga emas.
            var fromDepartment = _workspace.Department(context, row[PositionChanges.FromDepartment], blockCode: null);
            var fromPosition = _workspace.Position(context, row[PositionChanges.FromPosition]);
            var toDepartment = _workspace.Department(context, row[PositionChanges.ToDepartment], blockCode: null);

            // 12-ustunda 54 marta «Ойлик оширилди» yozilgan — bu lavozim emas,
            // oklad oshgani haqidagi izoh. Alias jadvali uni tanimaydi va
            // hisobotga «lavozim ma'lumotnomada yo'q» bo'lib tushadi; qator esa
            // oklad o'zgarishiga aylanadi. Bu to'g'ri natija, chunki hodisaning
            // haqiqiy mazmuni ham shu.
            var toPosition = _workspace.Position(context, row[PositionChanges.ToPosition]);

            var fromSalaryCell = _workspace.Salary(context, row[PositionChanges.FromSalary], "eski oklad");
            var toSalaryCell = _workspace.Salary(context, row[PositionChanges.ToSalary], "yangi oklad");

            var fromSalary = fromSalaryCell.Kind == SalaryCellKind.Amount ? fromSalaryCell.Salary : null;

            var toSalary = toSalaryCell.Kind switch
            {
                SalaryCellKind.Amount => toSalaryCell.Salary,

                // «Ўзгаришсиз» — HR ataylab yozgan javob: oklad tegilmadi,
                // eskisi kuchda qoladi. Bo'sh qoldirilsa, hodisa «oklad olib
                // tashlandi» degan yolg'on ma'no berardi.
                SalaryCellKind.Unchanged => fromSalary,

                // «Ишбай» va bo'sh katak — belgilangan oklad yo'q.
                _ => null,
            };

            if (EventType(fromDepartment, toDepartment, fromPosition, toPosition, fromSalary, toSalary)
                is not { } type)
            {
                _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                    "qatorda o'zgarish ko'rinmadi — yangi lavozim, bo'lim va oklad bo'sh",
                    row.Text(PositionChanges.ToPosition), EtlAction.RowSkipped);
                summary.Skipped++;
                continue;
            }

            if (ResolveEmployment(context, lookup.Employee!.Id, date) is not { } employment)
            {
                summary.Skipped++;
                continue;
            }

            if (_workspace.HasEvent(employment.Id, type, date, orderNo))
            {
                _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                    "bu hodisa allaqachon yozilgan", orderNo ?? Format(date), EtlAction.AlreadyExists);
                summary.Updated++;
                continue;
            }

            _workspace.AddEvent(employment, EmploymentEvent.Import(
                employment.Id,
                type,
                date,
                orderNo: orderNo,
                orderDate: orderDate,
                fromDepartmentId: fromDepartment,
                toDepartmentId: toDepartment,
                fromPositionId: fromPosition,
                toPositionId: toPosition,
                fromSalary: fromSalary,
                toSalary: toSalary,
                note: Note(row, PositionChanges.Note)));

            summary.Created++;
        }
    }

    /// <summary>
    /// Qator qaysi hodisa haqida ekanini aniqlaydi.
    /// </summary>
    /// <remarks>
    /// Bitta qatorda uchala qiymat ham o'zgargan bo'lishi mumkin, hodisa esa
    /// bitta. Shu sababli tartib qat'iy: lavozim → bo'lim → oklad. HR uchun
    /// lavozim o'zgarishi eng muhim fakt, oklad esa ko'pincha uning natijasi.
    /// Uchalasi ham o'zgarmagan bo'lsa <c>null</c> qaytadi — bunday qator
    /// (odatda vaqtinchalik ko'chirish izohi) yozilmaydi.
    /// </remarks>
    private static EmploymentEventType? EventType(
        Guid? fromDepartment,
        Guid? toDepartment,
        Guid? fromPosition,
        Guid? toPosition,
        Money? fromSalary,
        Money? toSalary)
    {
        if (toPosition is not null && toPosition != fromPosition)
        {
            return EmploymentEventType.Promotion;
        }

        if (toDepartment is not null && toDepartment != fromDepartment)
        {
            return EmploymentEventType.Transfer;
        }

        return toSalary is not null && toSalary != fromSalary
            ? EmploymentEventType.SalaryChange
            : null;
    }

    // ────────────────────────────────────────────────────────────
    //  91.4 — qo'shimcha vazifalar
    // ────────────────────────────────────────────────────────────

    /// <summary>«91.4 Қушимча вазифалар берилган» — 1464 qator.</summary>
    /// <remarks>
    /// 9-ustundagi «yangi bo'lim» ATAYLAB o'qilmaydi: bu odam ko'chgan bo'lim
    /// emas, qo'shimcha vazifa bajariladigan joy. Uni <c>ToDepartmentId</c>
    /// ga yozish hodisani bo'lim ko'chishiga o'xshatib qo'yardi — vazifaning
    /// matni esa bu haqda allaqachon aytadi.
    /// </remarks>
    private void ReadAdditionalDuties(StepSummary summary)
    {
        foreach (var row in DataRows(AdditionalDuties.Sheet, AdditionalDuties.FirstDataRow, summary))
        {
            var context = new RowContext(Name, AdditionalDuties.Sheet, row.Number, string.Empty);

            var lookup = _workspace.FindEmployee(context, row[FullNameColumn]);

            if (!lookup.Found)
            {
                summary.Skipped++;
                continue;
            }

            context = context.For(lookup.Label);

            var orderNo = EtlWorkspace.OrderNo(row[AdditionalDuties.OrderNo]);
            var orderDate = _workspace.Date(context, row[AdditionalDuties.OrderDate], "buyruq sanasi");

            var effective = _workspace.Date(context, row[AdditionalDuties.EffectiveDate], "kuchga kirish sanasi")
                ?? orderDate;

            if (effective is not { } date)
            {
                _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                    "hodisa sanasi yo'q (na kuchga kirish, na buyruq sanasi)",
                    row.Text(AdditionalDuties.OrderNo), EtlAction.RowSkipped);
                summary.Skipped++;
                continue;
            }

            // Vazifa matni — hodisaning YAGONA mazmuni: «kimgadir nimadir
            // topshirildi» degan yozuv na ro'yxatda, na hisobotda foydali.
            // Domen ham buni rad etadi («mehnat.vazifa_bosh»).
            var duty = row.Text(AdditionalDuties.Duty);

            if (duty.Length == 0)
            {
                _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                    "qo'shimcha vazifa matni yo'q", orderNo, EtlAction.RowSkipped);
                summary.Skipped++;
                continue;
            }

            if (ResolveEmployment(context, lookup.Employee!.Id, date) is not { } employment)
            {
                summary.Skipped++;
                continue;
            }

            if (_workspace.HasEvent(employment.Id, EmploymentEventType.AdditionalDuty, date, orderNo))
            {
                _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                    "bu qo'shimcha vazifa allaqachon yozilgan", orderNo ?? Format(date), EtlAction.AlreadyExists);
                summary.Updated++;
                continue;
            }

            // 11-ustun — vazifa uchun belgilangan qo'shimcha to'lov.
            // «Ўзгаришсиз» bu yerda «qo'shimcha to'lov yo'q» degani, shu
            // sababli faqat aniq summa olinadi.
            var salary = _workspace.Salary(context, row[AdditionalDuties.Salary], "qo'shimcha vazifa okladi");

            _workspace.AddEvent(employment, EmploymentEvent.Import(
                employment.Id,
                EmploymentEventType.AdditionalDuty,
                date,
                orderNo: orderNo,
                orderDate: orderDate,
                toSalary: salary.Kind == SalaryCellKind.Amount ? salary.Salary : null,
                note: duty));

            summary.Created++;
        }
    }

    // ────────────────────────────────────────────────────────────
    //  91.6 — ishdan bo'shatish
    // ────────────────────────────────────────────────────────────

    /// <summary>«91.6 Ишдан кетган ходимлар» — 2614 qator.</summary>
    /// <remarks>
    /// Bu varaqda ishdan ketish sabablari qamrovi to'liq: 1260 ta to'ldirilgan
    /// katakning hammasi alias jadvalida bor. Ya'ni bu yerdan chiqqan
    /// «ma'lumotnomada yo'q» yozuvi — haqiqiy yangilik, e'tibor talab qiladi.
    /// </remarks>
    private void ReadTerminations(StepSummary summary)
    {
        foreach (var row in DataRows(Terminations.Sheet, Terminations.FirstDataRow, summary))
        {
            var context = new RowContext(Name, Terminations.Sheet, row.Number, string.Empty);

            var lookup = _workspace.FindEmployee(context, row[FullNameColumn], row[Terminations.BirthDate]);

            if (!lookup.Found)
            {
                summary.Skipped++;
                continue;
            }

            context = context.For(lookup.Label);

            var terminationDate = _workspace.Date(context, row[Terminations.TerminationDate], "ishdan ketgan sana");

            if (terminationDate is not { } date)
            {
                _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                    "ishdan ketgan sana yo'q", row.Text(Terminations.OrderNo), EtlAction.RowSkipped);
                summary.Skipped++;
                continue;
            }

            var hireDate = _workspace.Date(context, row[Terminations.HireDate], "ishga kirgan sana");

            if (ResolveEnding(context, lookup.Employee!.Id, hireDate, date) is not { } employment)
            {
                summary.Skipped++;
                continue;
            }

            // 9-ustun (ishlagan kun) o'qilmaydi: u ikki sanadan hisoblanadi
            // (`Employment.WorkedDays`) va Excel'dagi qo'lda yozilgan qiymat
            // ular bilan har doim ham mos kelmaydi. 12-ustun («обходной») va
            // 13-ustun (telefon) esa mehnat munosabatiga tegishli emas.
            var reasonId = _workspace.TerminationReason(context, row[Terminations.Reason]);
            var orderNo = EtlWorkspace.OrderNo(row[Terminations.OrderNo]);

            var note = Note(row, Terminations.Analysis);

            switch (WriteTermination(context, employment, date, reasonId, orderNo, note, summary))
            {
                case TerminationOutcome.Written:
                    summary.Created++;
                    break;

                case TerminationOutcome.AlreadyDone:
                    summary.Updated++;
                    break;

                default:
                    summary.Skipped++;
                    break;
            }
        }
    }

    // ────────────────────────────────────────────────────────────
    //  91.8 — sinov muddati ichida ketganlar
    // ────────────────────────────────────────────────────────────

    /// <summary>«91.8 Стажировкадан кетганлар» — 1180 qator.</summary>
    /// <remarks>
    /// <para>
    /// Bu varaq «91.6» dan mustaqil ro'yxat EMAS: 231 yozuvdan 143 tasi
    /// o'sha odam va o'sha ketish sanasi bilan «91.6» da ham bor. Qolgan 88
    /// tasi esa faqat shu yerda — ya'ni varaqni tashlab bo'lmaydi.
    /// </para>
    /// <para>
    /// Buyruq raqami ustuni yo'qligi sababli takrorni <c>HasEvent</c> topa
    /// olmaydi (kalitga buyruq raqami kiradi). Shu sababli tekshiruv
    /// munosabatning holatiga tayanadi: allaqachon yakunlangan bo'lsa,
    /// bo'shatish qayta yozilmaydi.
    /// </para>
    /// <para>
    /// Varaqning «91.6» ga qo'shadigan haqiqiy fakti bitta: odam <b>sinov
    /// muddati ichida</b> ketgan. Shu sababli bo'shatishdan alohida
    /// <c>ProbationResult</c> hodisasi yoziladi — takroriy qatorlarda ham
    /// bu ma'lumot yo'qolmasligi kerak.
    /// </para>
    /// </remarks>
    private void ReadProbationExits(StepSummary summary)
    {
        foreach (var row in DataRows(ProbationExits.Sheet, ProbationExits.FirstDataRow, summary))
        {
            var context = new RowContext(Name, ProbationExits.Sheet, row.Number, string.Empty);

            var lookup = _workspace.FindEmployee(context, row[FullNameColumn], row[ProbationExits.BirthDate]);

            if (!lookup.Found)
            {
                summary.Skipped++;
                continue;
            }

            context = context.For(lookup.Label);

            var terminationDate = _workspace.Date(context, row[ProbationExits.TerminationDate], "ishdan ketgan sana");

            if (terminationDate is not { } date)
            {
                _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                    "ishdan ketgan sana yo'q", row.Text(ProbationExits.Reason), EtlAction.RowSkipped);
                summary.Skipped++;
                continue;
            }

            var hireDate = _workspace.Date(context, row[ProbationExits.HireDate], "ishga kirgan sana");

            if (ResolveEnding(context, lookup.Employee!.Id, hireDate, date) is not { } employment)
            {
                summary.Skipped++;
                continue;
            }

            var wrote = false;
            var reason = row[ProbationExits.Reason];

            // Takroriy qatorda sabab alias jadvalidan qidirilmaydi: «91.6» uni
            // allaqachon qidirgan va topilmagani hisobotga tushgan. Ikkinchi
            // marta yozish hisobotni takrorga to'ldirardi.
            if (!EtlWorkspace.IsTerminated(employment))
            {
                var reasonId = _workspace.TerminationReason(context, reason);

                wrote = WriteTermination(context, employment, date, reasonId, orderNo: null, note: null, summary)
                    == TerminationOutcome.Written;
            }

            // Buyruq raqami bu varaqda yo'q — kalit ataylab `null` bilan
            // tuziladi, ya'ni qayta yurgizishda ham bitta hodisa qoladi.
            if (!_workspace.HasEvent(employment.Id, EmploymentEventType.ProbationResult, date, orderNo: null))
            {
                _workspace.AddEvent(employment, EmploymentEvent.Import(
                    employment.Id,
                    EmploymentEventType.ProbationResult,
                    date,
                    note: Note(row, ProbationExits.Reason)));

                wrote = true;
            }

            if (wrote)
            {
                summary.Created++;
            }
            else
            {
                summary.Updated++;
            }
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Umumiy yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Bo'shatish yozuvining natijasi — jamlanma hisobi shunga qarab yuritiladi.</summary>
    private enum TerminationOutcome
    {
        /// <summary>Yangi bo'shatish yozildi.</summary>
        Written = 0,

        /// <summary>Munosabat allaqachon yakunlangan — tegilmadi.</summary>
        AlreadyDone = 1,

        /// <summary>Sanalar bir-biriga zid — yozilmadi, yozuv tekshirishga belgilandi.</summary>
        Rejected = 2,
    }

    /// <summary>
    /// Varaqning ma'lumot qatorlarini beradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Blok sarlavhalari («2026 ЙИЛ», «2026 ЙИЛ АВГУСТ ОЙИДА…») va bo'sh
    /// tartib raqamli qatorlar F.I.Sh ustuni bo'yicha ajratiladi: to'rtala
    /// varaqda ham sarlavhada ism yo'q. Ular <c>Read</c> hisobiga
    /// KIRMAYDI — aks holda jamlanma «3028 qator o'qildi» deb yolg'on
    /// aytardi.
    /// </para>
    /// <para>
    /// Chegara (<c>--limit</c>) har bir varaq uchun alohida sanaladi: sinovda
    /// «har bir varaqdan 20 qator» kutiladi, «umuman 20 qator» emas.
    /// </para>
    /// </remarks>
    private IEnumerable<ExcelRow> DataRows(string sheet, int firstDataRow, StepSummary summary)
    {
        var rows = _workspace.Workbook.Rows(sheet);

        if (rows.Count == 0)
        {
            _workspace.Report.Add(Name, sheet, 0, string.Empty, "varaq topilmadi", sheet, EtlAction.RowSkipped);
            yield break;
        }

        var processed = 0;

        foreach (var row in rows)
        {
            if (row.Number < firstDataRow || !row.Has(FullNameColumn))
            {
                continue;
            }

            if (_workspace.Options.Limit is { } limit && processed >= limit)
            {
                yield break;
            }

            processed++;
            summary.Read++;

            yield return row;
        }
    }

    /// <summary>
    /// Hodisa yoziladigan mehnat munosabatini sana bo'yicha tanlaydi.
    /// </summary>
    /// <remarks>
    /// Sana hech bir munosabatga tushmasa — bu Excel'dagi tartib buzilishi
    /// (ishdan ketganidan keyin yozilgan o'zgarish, yoki noto'g'ri yil).
    /// Hodisa eng yaqin munosabatga yoziladi, lekin bu <b>taxmin</b>, shu
    /// sababli hisobotga tushadi: HR qaysi qator shubhali ekanini ko'radi.
    /// </remarks>
    private Employment? ResolveEmployment(RowContext context, Guid employeeId, DateOnly date)
    {
        var employment = _workspace.EmploymentOn(employeeId, date, out var exact);

        if (employment is null)
        {
            _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                "xodimning mehnat munosabati yo'q — hodisa yoziladigan joy topilmadi",
                Format(date), EtlAction.RowSkipped);
            return null;
        }

        if (!exact)
        {
            _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                "hodisa sanasi hech bir mehnat munosabatiga tushmadi — eng yaqini tanlandi",
                Format(date), EtlAction.Flagged);
        }

        return employment;
    }

    /// <summary>
    /// Yakunlanadigan mehnat munosabatini tanlaydi.
    /// </summary>
    /// <remarks>
    /// Avval <b>ishga kirish sanasi</b> bo'yicha aniq mos keladigani izlanadi:
    /// odam bir necha marta kelib-ketgan bo'lsa, «qaysi kelishi tugadi»
    /// degan savolga faqat shu ustun javob beradi. Topilmasa — ketish
    /// sanasida kuchda bo'lgan munosabat olinadi.
    /// </remarks>
    private Employment? ResolveEnding(RowContext context, Guid employeeId, DateOnly? hireDate, DateOnly terminationDate)
    {
        if (hireDate is { } started && _workspace.EmploymentStartedOn(employeeId, started) is { } exact)
        {
            return exact;
        }

        return ResolveEmployment(context, employeeId, terminationDate);
    }

    /// <summary>Ishdan bo'shatishni yozadi va domen rad etgan holatlarni hisobotga chiqaradi.</summary>
    private TerminationOutcome WriteTermination(
        RowContext context,
        Employment employment,
        DateOnly terminationDate,
        Guid? reasonId,
        string? orderNo,
        string? note,
        StepSummary summary)
    {
        // Buyruq sanasi ataylab `null`: «91.6» da faqat buyruq RAQAMI ustuni
        // bor, sanasi esa yo'q. Ketish sanasini buyruq sanasi deb yozish
        // o'ylab topilgan ma'lumot bo'lardi.
        var result = _workspace.Terminate(employment, terminationDate, reasonId, orderNo, orderDate: null, note: note);

        if (result.IsSuccess)
        {
            return TerminationOutcome.Written;
        }

        if (result.Error.Type == ErrorType.Conflict)
        {
            // Munosabat allaqachon yakunlangan: qator takrorlangan yoki vosita
            // qayta yurgizilgan. Bu xato emas — birinchi yozuv kuchda qoladi.
            _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                "mehnat munosabati allaqachon yakunlangan", Format(terminationDate), EtlAction.AlreadyExists);
            return TerminationOutcome.AlreadyDone;
        }

        // Ketish sanasi ishga kirish sanasidan oldin. Qaysi sana xato ekanini
        // Excel aytmaydi, shu sababli hech biri TUZATILMAYDI: munosabat
        // ochiqligicha qoladi va HR uni tekshiradi.
        _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
            result.Error.Message, Format(terminationDate), EtlAction.Flagged);

        Flag(employment, "ishdan ketish sanasi ishga kirish sanasidan oldin", summary);

        return TerminationOutcome.Rejected;
    }

    /// <summary>Munosabatni tekshirishga belgilaydi — sabab ham, hisob ham takrorlanmaydi.</summary>
    private void Flag(Employment employment, string reason, StepSummary summary)
    {
        if (employment.ReviewNote?.Contains(reason, StringComparison.Ordinal) != true)
        {
            employment.FlagForReview(reason);
        }

        if (_flagged.Add(employment.Id))
        {
            summary.NeedsReview++;
        }
    }

    /// <summary>Izoh ustunini o'qiydi: bo'sh katak <c>null</c> bo'ladi, bo'sh satr emas.</summary>
    private static string? Note(ExcelRow row, int column)
        => row.Text(column) is { Length: > 0 } text ? text : null;

    /// <summary>Hisobotda sanani ko'rsatish ko'rinishi.</summary>
    private static string Format(DateOnly date)
        => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    // ────────────────────────────────────────────────────────────
    //  Varaqlarning ustun tartibi
    // ────────────────────────────────────────────────────────────

    /// <summary>«91.3 Лавозими ўзгарган ходимлар» ustunlari.</summary>
    private static class PositionChanges
    {
        public const string Sheet = "91.3 Лавозими ўзгарган ходимлар";

        /// <summary>Sarlavha uch qatorli, 4- va 5-qator esa blok nomi.</summary>
        public const int FirstDataRow = 6;

        public const int OrderNo = 3;
        public const int OrderDate = 4;
        public const int FromDepartment = 6;
        public const int FromPosition = 7;
        public const int FromSalary = 8;
        public const int EffectiveDate = 9;
        public const int ToDepartment = 11;
        public const int ToPosition = 12;
        public const int ToSalary = 13;
        public const int Note = 14;
    }

    /// <summary>«91.4 Қушимча вазифалар берилган» ustunlari.</summary>
    private static class AdditionalDuties
    {
        public const string Sheet = "91.4 Қушимча вазифалар берилган";

        public const int FirstDataRow = 6;

        public const int OrderNo = 3;
        public const int OrderDate = 4;
        public const int EffectiveDate = 8;
        public const int Duty = 10;
        public const int Salary = 11;
    }

    /// <summary>«91.6 Ишдан кетган ходимлар» ustunlari.</summary>
    private static class Terminations
    {
        public const string Sheet = "91.6 Ишдан кетган ходимлар";

        /// <summary>Sarlavha ikki qatorli, 3- va 4-qator esa blok nomi.</summary>
        public const int FirstDataRow = 5;

        public const int BirthDate = 3;
        public const int OrderNo = 6;
        public const int TerminationDate = 7;
        public const int HireDate = 8;
        public const int Reason = 10;

        /// <summary>«Бириктирилган ходим анализи» — bo'shatish izohi.</summary>
        public const int Analysis = 11;
    }

    /// <summary>«91.8 Стажировкадан кетганлар» ustunlari.</summary>
    private static class ProbationExits
    {
        public const string Sheet = "91.8 Стажировкадан кетганлар";

        public const int FirstDataRow = 5;

        public const int BirthDate = 3;
        public const int TerminationDate = 7;
        public const int HireDate = 8;
        public const int Reason = 9;
    }
}
