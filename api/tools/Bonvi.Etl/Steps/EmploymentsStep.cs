using Bonvi.Domain.Employees;
using Bonvi.Etl.Excel;
using Bonvi.Etl.Mapping;
using Bonvi.Etl.Reporting;
using Bonvi.Etl.Runtime;

namespace Bonvi.Etl.Steps;

/// <summary>
/// 3-qadam: mehnat munosabatlarini — ya'ni ishga qabullarni — ko'chiradi.
/// </summary>
/// <remarks>
/// <para>
/// Asosiy manba — <b>«91.2 Янги ишга келганлар»</b>: 2972 qatorda 1960 ta
/// qabul, har birida buyruq raqami, buyruq sanasi va oklad bor. Bu bloknotdagi
/// yagona joy, u yerda ishga qabul <b>hodisa</b> sifatida yozilgan.
/// </para>
/// <para>
/// Lekin «91.2» ning ikkita kamchiligi bor va qadamning uch bosqichga
/// bo'linishi ham shundan:
/// </para>
/// <list type="number">
///   <item>unda <b>yuridik shaxs ustuni yo'q</b>, mehnat munosabati esa
///         usiz umuman yaratilmaydi — dalil xodim kartasidagi
///         «Мехнат дафтарчаси» ustunidan olinadi (1-bosqich);</item>
///   <item>u <b>to'liq emas</b>: asosiy ro'yxatdagi 540 xodimning ba'zisining
///         qabul buyrug'i varaqqa umuman tushmagan. Bunday odam bazada
///         «hech qachon ishlamagan» bo'lib qolardi, shu sababli qolgan
///         qabullar karta qatorining o'zidan yasaladi (3-bosqich).</item>
/// </list>
/// </remarks>
public sealed class EmploymentsStep
{
    /// <summary>Qadam nomi (<c>--steps</c> uchun).</summary>
    public const string Name = "employments";

    /// <summary>«91.2» dagi buyruq raqami — xodim kartasida bunday ustun yo'q.</summary>
    private const int OrderNoColumn = 6;

    /// <summary>«91.2» dagi buyruq sanasi.</summary>
    private const int OrderDateColumn = 7;

    /// <summary>«91.2» dagi «ОКЛАД» ustuni.</summary>
    private const int SalaryColumn = 11;

    private readonly EtlWorkspace _workspace;

    /// <summary>Xodim → yuridik shaxs. 1-bosqichda kartalardan yig'iladi.</summary>
    private readonly Dictionary<Guid, Guid> _legalEntities = [];

    /// <summary>Xodimi topilgan karta qatorlari — 3-bosqich uchun saqlanadi.</summary>
    private readonly List<CardRow> _cards = [];

    public EmploymentsStep(EtlWorkspace workspace) => _workspace = workspace;

    /// <summary>Uch bosqichni ketma-ket bajaradi.</summary>
    /// <remarks>
    /// <para>
    /// Bosqichlar tartibi majburiy: 1-bosqichsiz mehnat munosabatining
    /// yuridik shaxsi bo'lmaydi, 3-bosqich esa 2-bosqich nimani yozganini
    /// bilishi kerak — aks holda har bir qabulni ikki marta yozardi.
    /// </para>
    /// <para>
    /// <c>Read</c> faqat «91.2» qatorlarini sanaydi. Karta varaqlari bu yerda
    /// <b>qo'shimcha dalil</b> sifatida o'qiladi va ularning qatorlari xodimlar
    /// qadamida allaqachon sanalgan; ikkinchi marta qo'shilsa, hisobot
    /// bloknotda 1960 emas, 4200 ta qabul bordek ko'rsatardi.
    /// </para>
    /// </remarks>
    public void Run()
    {
        var summary = _workspace.Report.Step(Name);

        ReadCards(EmployeeSheet.Main);
        ReadCards(EmployeeSheet.Archive);

        ReadHires(summary);
        FillGaps(summary);
    }

    // ────────────────────────────────────────────────────────────
    //  1-bosqich — yuridik shaxs indeksi
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Xodim kartasi varag'ini o'qiydi: yuridik shaxs indeksini to'ldiradi va
    /// qatorlarni 3-bosqich uchun eslab qoladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// «Мехнат дафтарчаси» ustuni asosiy ro'yxatda 13-, arxivda 14-ustun.
    /// U mehnat daftarchasi qaysi korxonada saqlanishini bildiradi — bu
    /// bloknotdagi yagona yuridik shaxs dalili.
    /// </para>
    /// <para>
    /// Xodim <b>bir marta</b> qidiriladi va natija saqlanadi. Sabab: 3-bosqich
    /// aynan shu qatorlar bo'ylab yana yuradi, va ikkinchi qidiruv «xodim
    /// topilmadi» xabarini hisobotga ikki marta yozardi.
    /// </para>
    /// </remarks>
    private void ReadCards(EmployeeSheet sheet)
    {
        var rows = _workspace.Workbook.Rows(sheet.Name);

        if (rows.Count == 0)
        {
            _workspace.Report.Add(Name, sheet.Name, 0, string.Empty,
                "varaq topilmadi", sheet.Name, EtlAction.RowSkipped);
            return;
        }

        var processed = 0;
        string? blockCode = null;

        foreach (var row in rows)
        {
            if (row.Number < sheet.FirstDataRow)
            {
                continue;
            }

            var nameCell = row[sheet.FullName];
            if (nameCell.IsEmpty)
            {
                continue;
            }

            if (sheet.HasBlockHeaders && !row.Has(sheet.Position))
            {
                // Blok sarlavhasi. 80 ta Rispetto xodimida 6-ustunda
                // shunchaki «"Rispetto" МЧЖ» — zavod nomi turibdi, haqiqiy
                // bo'lim esa faqat shu sarlavhada yozilgan.
                //
                // Noma'lum sarlavha kelsa kod NULL ga tushadi, oldingisi
                // saqlanmaydi: tanish bo'lmagan blok ostidagi xodimlarni
                // avvalgi bo'limga yozib qo'yish jim yotgan xato bo'lardi.
                blockCode = _workspace.Aliases.DepartmentBlock(nameCell).First;
                continue;
            }

            if (_workspace.Options.Limit is { } limit && processed >= limit)
            {
                break;
            }

            processed++;

            var context = new RowContext(Name, sheet.Name, row.Number, string.Empty);

            var lookup = _workspace.FindEmployee(
                context, nameCell, row[sheet.BirthDate], row[sheet.Pinfl], row[sheet.Passport]);

            if (lookup.Employee is not { } employee)
            {
                continue;
            }

            _cards.Add(new CardRow(sheet, row, employee, sheet.HasBlockHeaders ? blockCode : null));

            // Birinchi topilgan qiymat kuchda qoladi. Asosiy ro'yxat arxivdan
            // oldin o'qiladi va u yangiroq: odam korxonadan korxonaga
            // o'tgan bo'lsa, joriy holat asosiy ro'yxatda turibdi.
            if (_legalEntities.ContainsKey(employee.Id))
            {
                continue;
            }

            var legalEntityCell = row[sheet.LegalEntity];
            if (legalEntityCell.IsEmpty)
            {
                continue;
            }

            var found = _workspace.LegalEntities(context.For(lookup.Label), legalEntityCell);

            if (found.Count > 0)
            {
                // Bir katakda bir nechta korxona bo'lsa (o'rindoshlik) —
                // birinchisi asosiy ish joyi deb olinadi.
                _legalEntities[employee.Id] = found[0];
            }
        }
    }

    // ────────────────────────────────────────────────────────────
    //  2-bosqich — «91.2» dan ishga qabullar
    // ────────────────────────────────────────────────────────────

    /// <summary>«91.2 Янги ишга келганлар» varag'ini o'qiydi.</summary>
    private void ReadHires(StepSummary summary)
    {
        var sheet = EmployeeSheet.NewHires;
        var rows = _workspace.Workbook.Rows(sheet.Name);

        if (rows.Count == 0)
        {
            _workspace.Report.Add(Name, sheet.Name, 0, string.Empty,
                "varaq topilmadi", sheet.Name, EtlAction.RowSkipped);
            return;
        }

        var processed = 0;

        foreach (var row in rows)
        {
            if (row.Number < sheet.FirstDataRow)
            {
                continue;
            }

            if (row[sheet.FullName].IsEmpty)
            {
                // Yil/oy bloki sarlavhasi — u 1- yoki 3-ustunda yozilgan.
                // 2972 qatordan 1960 tasidagina F.I.Sh bor, qolgani shunday
                // ajratgich va ular o'qilgan qatorlar qatoriga kirmaydi.
                continue;
            }

            if (_workspace.Options.Limit is { } limit && processed >= limit)
            {
                break;
            }

            processed++;
            summary.Read++;

            ReadHire(sheet, row, summary);
        }
    }

    /// <summary>«91.2» ning bitta qatoridan mehnat munosabati yasaydi.</summary>
    private void ReadHire(EmployeeSheet sheet, ExcelRow row, StepSummary summary)
    {
        var context = new RowContext(Name, sheet.Name, row.Number, string.Empty);

        // Bu varaqda pasport ham, JSHSHIR ham bor — xodim ishonchli topiladi.
        // Yangi xodim ATAYLAB yaratilmaydi: agar odam bazada yo'q bo'lsa,
        // demak xodimlar qadami uni o'tkazib yuborgan va buni hisobotdan
        // ko'rish kerak, «kimgadir» qabul buyrug'i yozib qo'yishdan emas.
        var lookup = _workspace.FindEmployee(
            context, row[sheet.FullName], row[sheet.BirthDate], row[sheet.Pinfl], row[sheet.Passport]);

        if (lookup.Employee is not { } employee)
        {
            summary.Skipped++;
            return;
        }

        context = context.For(lookup.Label);

        var hireCell = row[sheet.HireDate];

        if (_workspace.Date(context, hireCell, "ishga kirgan sana") is not { } hireDate)
        {
            // Katak o'qilmagan bo'lsa Date(...) o'zi yozdi; bo'sh bo'lsa esa
            // hech kim yozmaydi. Ishga kirish sanasi — munosabatning o'zagi,
            // usiz na takrorlanishni aniqlab bo'ladi, na hodisa sanasini.
            _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                "ishga kirish sanasi yo'q", hireCell.Text, EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        if (_workspace.EmploymentStartedOn(employee.Id, hireDate) is not null)
        {
            // Qayta yurgizish yoki bir qabulning varaqda ikki marta yozilishi.
            // Yangi yozuv ochilmaydi — vosita idempotent bo'lishi shart.
            _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                "bu sanadagi ishga qabul allaqachon yozilgan", hireCell.Text, EtlAction.AlreadyExists);
            summary.Updated++;
            return;
        }

        var legalEntity = LegalEntityFor(context, employee);

        var orderNo = EtlWorkspace.OrderNo(row[OrderNoColumn]);
        var orderDate = _workspace.Date(context, row[OrderDateColumn], "buyruq sanasi");

        // 8-ustun («йўналиш») bu yerda umuman O'QILMAYDI. Vasvasa bor:
        // 55 qatorda «"Vicor" метан МЧЖ», yana 45 tasida «"Viкor" метан МЧЖ»
        // — ya'ni yuridik shaxs nomi turibdi va uni o'rindoshlik dalili deb
        // olish mumkindek. Lekin ustunning asosiy ma'nosi — yo'nalish, va
        // yo'nalish odamning o'ziga tegishli: uni xodimlar qadami allaqachon
        // yozib qo'ygan. Ikki ma'noli ustunni ikkinchi marta talqin qilish
        // 1900 ta to'g'ri qatorni 100 ta istisno uchun xavf ostiga qo'yardi.
        var created = Employment.Hire(
            employee.Id,
            legalEntity.Id ?? Guid.Empty,
            hireDate,
            _workspace.Department(context, row[sheet.Department], blockCode: null),
            _workspace.Position(context, row[sheet.Position]),
            orderNo,
            orderDate);

        if (created.IsFailure)
        {
            // Amalda bitta sabab: yuridik shaxs topilmadi (ma'lumotnomada
            // standart kod yo'q yoki `--default-legal-entity ""` berilgan).
            _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                "mehnat munosabati yaratilmadi", created.Error.Message, EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var employment = created.Value;

        if (legalEntity.Assumed)
        {
            employment.FlagForReview("yuridik shaxs ko'rsatilmagan — standart qiymat olindi");
        }

        // Oklad faqat ANIQ summa bo'lganda yoziladi. «Ишбай» va «Ўзгаришсиз»
        // — xato emas, HR ataylab yozgan holat: bunday xodimda belgilangan
        // oklad yo'q va bo'sh maydon to'g'ri javob. Tushunarsiz katak haqida
        // Salary(...) o'zi hisobotga yozadi.
        var salary = _workspace.Salary(context, row[SalaryColumn], "oklad");

        if (salary.Kind == SalaryCellKind.Amount)
        {
            // Oklad hodisasining sanasi — ishga kirish sanasi: bu boshlang'ich
            // oklad, keyingi o'zgarishlar «91.3» varag'idan keladi.
            //
            // Natija tekshirilmaydi: SetSalary faqat ishdan ketgan xodimda
            // yiqiladi, endigina yaratilgan munosabat esa sinov holatida.
            _ = employment.SetSalary(salary.Salary, hireDate, orderNo, orderDate);
        }

        Add(employee, employment, summary);
    }

    // ────────────────────────────────────────────────────────────
    //  3-bosqich — kartada bor, «91.2» da yo'q qabullar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Karta varaqlarida qolgan, buyrug'i topilmagan qabullarni yaratadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// «91.2» da 1960 ta qabul bor, asosiy ro'yxatda esa 540 joriy xodim va
    /// arxivda 1715 ta ketgan odam. Sonlar to'g'ri kelmaydi: ba'zi xodimning
    /// qabul buyrug'i varaqqa umuman tushmay qolgan. Ularsiz odam bazada
    /// «hech qachon ishlamagan» bo'lib qolardi — kartasi bor, mehnat
    /// munosabati yo'q.
    /// </para>
    /// <para>
    /// Bunday munosabatda buyruq raqami ham, buyruq sanasi ham yo'q, shu
    /// sababli u <c>NeedsReview</c> bilan belgilanadi: HR buyruqni arxivdan
    /// topib kiritishi kerak.
    /// </para>
    /// </remarks>
    private void FillGaps(StepSummary summary)
    {
        foreach (var card in _cards)
        {
            var sheet = card.Sheet;
            var row = card.Row;

            var context = new RowContext(Name, sheet.Name, row.Number, card.Employee.FullName.Full);
            var hireCell = row[sheet.HireDate];

            if (_workspace.Date(context, hireCell, "ishga kirgan sana") is not { } hireDate)
            {
                if (hireCell.IsEmpty)
                {
                    _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                        "ishga kirish sanasi yo'q — mehnat munosabati yaratilmadi",
                        string.Empty, EtlAction.LeftEmpty);
                }

                continue;
            }

            if (_workspace.EmploymentStartedOn(card.Employee.Id, hireDate) is not null)
            {
                // Odatiy hol: qabul «91.2» dan yoki oldingi karta varag'idan
                // allaqachon yozilgan. Hisobotga tushmaydi — bunday qator
                // ikki mingdan ortiq va ular shovqindan boshqa narsa emas.
                continue;
            }

            var legalEntity = LegalEntityFor(context, card.Employee);

            var created = Employment.Hire(
                card.Employee.Id,
                legalEntity.Id ?? Guid.Empty,
                hireDate,
                _workspace.Department(context, row[sheet.Department], card.BlockCode),
                _workspace.Position(context, row[sheet.Position]));

            if (created.IsFailure)
            {
                _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                    "mehnat munosabati yaratilmadi", created.Error.Message, EtlAction.RowSkipped);
                summary.Skipped++;
                continue;
            }

            var employment = created.Value;

            employment.FlagForReview("ishga qabul buyrug'i Excel'da topilmadi");

            if (legalEntity.Assumed)
            {
                employment.FlagForReview("yuridik shaxs ko'rsatilmagan — standart qiymat olindi");
            }

            _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                "«91.2» da ishga qabul buyrug'i topilmadi — munosabat karta qatoridan yaratildi",
                hireCell.Text, EtlAction.Flagged);

            Add(card.Employee, employment, summary);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Umumiy yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Munosabat uchun yuridik shaxsni tanlaydi.
    /// </summary>
    /// <remarks>
    /// Avval 1-bosqichda kartalardan yig'ilgan dalil, u bo'lmasa — bloknot
    /// sarlavhasiga asoslangan standart kod. Ikkinchi hol <c>Assumed</c>
    /// bilan qaytadi va chaqiruvchi uni tekshirishga belgilaydi.
    /// </remarks>
    private LegalEntityChoice LegalEntityFor(RowContext context, Employee employee)
        => _legalEntities.TryGetValue(employee.Id, out var known)
            ? new LegalEntityChoice(known, false)
            : _workspace.EmploymentLegalEntity(context, ExcelCell.Empty);

    /// <summary>Yangi munosabatni ro'yxatga oladi va saqlashga qo'yadi.</summary>
    private void Add(Employee employee, Employment employment, StepSummary summary)
    {
        // Asosiy ish joyi — xodimning birinchi yozilgan munosabati.
        // Konstruktor har bir munosabatni «asosiy» qilib yaratadi, shu sababli
        // belgi ochiq qo'yiladi: o'rindosh yoki qaytib kelgan xodimda ikkita
        // asosiy ish joyi qolib ketmasin. Ro'yxatga olishdan OLDIN
        // tekshiriladi — Register(...) sanoqni o'zgartiradi.
        employment.SetPrimary(_workspace.EmploymentsOf(employee.Id).Count == 0);

        if (employment.NeedsReview)
        {
            summary.NeedsReview++;
        }

        _workspace.Register(employment);
        _workspace.Database.Add(employment);

        summary.Created++;
    }

    /// <summary>
    /// Xodim kartasidagi bir qator — xodimi allaqachon topilgan holda.
    /// </summary>
    /// <param name="Sheet">Qaysi varaq (ustun raqamlari shundan olinadi).</param>
    /// <param name="Row">Excel qatori.</param>
    /// <param name="Employee">1-bosqichda topilgan xodim.</param>
    /// <param name="BlockCode">Qator tegishli bo'lgan blok sarlavhasi kodi.</param>
    private readonly record struct CardRow(
        EmployeeSheet Sheet,
        ExcelRow Row,
        Employee Employee,
        string? BlockCode);
}
