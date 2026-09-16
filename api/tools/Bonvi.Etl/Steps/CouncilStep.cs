using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Etl.Excel;
using Bonvi.Etl.Reporting;
using Bonvi.Etl.Runtime;

namespace Bonvi.Etl.Steps;

/// <summary>
/// «91.1 Кенгаш хулосаси» → nomzod yozuvi va kengash bosqichi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu qadam kerak.</b> Varaqda 3185 qator bor va ularning atigi
/// uchdan biri bazadagi xodimga to'g'ri keladi — qolgani ishga
/// OLINMAGAN odamlar. Ya'ni bu varaq xodimlar tarixi emas, korxonaning
/// <b>tanlov tarixi</b>: kim ko'rildi, nima deyildi, nega olinmadi.
/// Foydalanuvchining talabi aynan shu edi — «barcha Excel'dagi tarix».
/// </para>
/// <para>
/// <b>Nega nomzod yozuvi orqali.</b> Xodim kartasidagi «Kengash
/// xulosasi» bo'limi <c>Nomzod → Bosqich yozuvi</c> zanjirini o'qiydi
/// (<c>GetEmployeeStagesQuery</c>). Boshqa manba qo'shish tizimda
/// ikkita «kengash xulosasi» yaratardi va qaysi biri to'g'ri ekanini
/// hech kim ayta olmasdi. Shu sababli tarix MAVJUD tuzilmaga
/// joylashtiriladi, yangisi o'ylab topilmaydi.
/// </para>
/// <para>
/// <b>Bitta arxiv vakansiyasi.</b> <see cref="Candidate"/> vakansiyasiz
/// yozilmaydi, kengash varag'ida esa vakansiya ustuni YO'Q. Har bir
/// lavozim uchun alohida vakansiya to'qib chiqarish — 490 ta soxta
/// yozuv degani. Buning o'rniga bitta yopiq vakansiya ochiladi va uning
/// nomi HR uchun ochiq aytadi: bu arxiv, reja emas.
/// </para>
/// <para>
/// <b>Nega eng oxirida yurgiziladi.</b> Qadam xodimlarni ham, ularning
/// ishga qabul hodisalarini ham izlaydi — ikkalasi ham oldingi
/// qadamlarda yaratiladi. Undan oldin yurgizilsa, hali ko'chirilmagan
/// odamlar «topilmadi» bo'lib hisobotni to'ldirardi.
/// </para>
/// </remarks>
public sealed class CouncilStep
{
    private const string Name = "council";
    private const string SheetName = "91.1 Кенгаш хулосаси";

    /// <summary>Kengash bosqichining ma'lumotnomadagi nomi.</summary>
    /// <remarks>
    /// Bosqich urug'lantirishda YARATILGAN («Анкета → Телефон суҳбат →
    /// Суҳбат 1 → Кенгаш → Раҳбар суҳбати»), ya'ni bu qadam uni
    /// yaratmaydi — topadi. Topilmasa qadam butunlay o'tkazib
    /// yuboriladi: bosqichni o'zi yaratib qo'yish HR sozlagan voronkaga
    /// aralashish bo'lardi.
    /// </remarks>
    private const string StageName = "Кенгаш";

    /// <summary>Arxiv vakansiyasining nomi.</summary>
    private const string ArchiveVacancy = "Кенгаш архиви (Excel)";

    /// <summary>
    /// Sarlavha qatorlari: 1–3 va 3313.
    /// </summary>
    /// <remarks>
    /// Varaq ikki marta sarlavhalangan — 3313-qatordan ustun nomlari
    /// QAYTA yozilgan («Иш жойи» o'rniga «Статус»). Ustunlarning
    /// tartibi esa bir xil qolgan, shu sababli faqat sarlavhaning o'zini
    /// tashlab ketish yetarli.
    /// </remarks>
    private const int FirstDataRow = 4;
    private const int SecondHeaderRow = 3313;

    private const int NumberColumn = 1;
    private const int FullNameColumn = 2;
    private const int BirthDateColumn = 3;
    private const int PositionColumn = 4;
    private const int PhoneColumn = 5;
    private const int ResponsibleColumn = 11;
    private const int StartDateColumn = 12;
    private const int NoteColumn = 13;

    /// <summary>Kengash a'zolarining ustunlari (Мамажонов Б … Абдурахимов А).</summary>
    /// <remarks>
    /// To'rt a'zo, to'rt ustun. Yozuv esa BITTA bo'ladi: domen qarori
    /// bo'yicha kengash ovoz bermaydi, bitta xulosa yoziladi
    /// (<see cref="CandidateStageEntry"/> izohi). Ustunlar shu sababli
    /// natijani aniqlash uchun O'QILADI, lekin alohida saqlanmaydi.
    /// </remarks>
    private static readonly int[] MemberColumns = [6, 7, 8, 9];

    /// <summary>
    /// Kengash yig'ilishi sanasi yozilgan qatormi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Varaqda yig'ilishlar sana bilan ajratilgan va sana AYNAN F.I.Sh
    /// ustuniga yozilgan — o'sha qatorda boshqa hech narsa yo'q. Undan
    /// keyin tartib raqami qaytadan 1 dan boshlanadi.
    /// </para>
    /// <para>
    /// Bu qatorlar oldin «ism ajratilmadi» bo'lib hisobotga tushardi
    /// (18 ta soxta xato). Endi ular XATO emas, eng ishonchli
    /// SANA MANBAI: bir blokdagi odamlarning hammasi shu kuni ko'rilgan.
    /// </para>
    /// </remarks>
    private static bool IsSessionHeader(ExcelRow row)
        => row[NumberColumn].IsEmpty
            && row[FullNameColumn].HasValue
            && ExcelDate.Read(row[FullNameColumn]) is not null;

    /// <summary>Salbiy xulosa shakllari — birinchi bo'lib sinaladi.</summary>
    /// <remarks>
    /// Tartib <c>ProbationsStep.Outcome</c> dagi bilan bir xil sababdan:
    /// «Тавсия этилмади» ichida «тавсия» ham bor va ijobiy shakl avval
    /// sinalsa, rad etilgan nomzod «o'tdi» bo'lib yozilardi.
    /// </remarks>
    private static readonly string[] FailedForms = ["тавсия этилмади", "отказ", "рад этилди", "утмади"];

    private static readonly string[] PassedForms = ["тавсия", "маъкул", "макул", "олинди", "утди"];

    /// <summary>Belgi bilan yozilgan xulosalar.</summary>
    private const string PassedMark = "✔";
    private const string PassedMarkAlt = "✓";
    private const string FailedMark = "❌";
    private const string FailedMarkAlt = "✖";

    private readonly EtlWorkspace _workspace;

    public CouncilStep(EtlWorkspace workspace) => _workspace = workspace;

    public void Run()
    {
        var summary = _workspace.Report.Step(Name);

        var stage = _workspace.StageNamed(StageName);

        if (stage is null)
        {
            _workspace.Report.Add(Name, SheetName, 0, string.Empty,
                "voronka bosqichi topilmadi — kengash xulosalari ko'chirilmadi",
                StageName, EtlAction.RowSkipped);

            return;
        }

        var rows = _workspace.Workbook.Rows(SheetName);

        if (rows.Count == 0)
        {
            _workspace.Report.Add(Name, SheetName, 0, string.Empty,
                "varaq topilmadi", SheetName, EtlAction.RowSkipped);

            return;
        }

        if (ArchiveVacancyOf() is not { } vacancy)
        {
            _workspace.Report.Add(Name, SheetName, 0, string.Empty,
                "arxiv vakansiyasi yaratilmadi", ArchiveVacancy, EtlAction.RowSkipped);

            return;
        }

        // Blok sanalari BUTUN varaq o'qilgandan keyin kerak bo'ladi
        // (izohi `BlockDates` da), shu sababli ikki yurish.
        var blocks = BlockDates(rows);

        var processed = 0;

        foreach (var row in rows)
        {
            if (row.Number < FirstDataRow
                || row.Number == SecondHeaderRow
                || row[FullNameColumn].IsEmpty
                || IsSessionHeader(row))
            {
                continue;
            }

            if (_workspace.Options.Limit is { } limit && processed >= limit)
            {
                break;
            }

            processed++;
            summary.Read++;

            ReadRow(row, vacancy, stage.Id, blocks, summary);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Arxiv vakansiyasi
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Arxiv vakansiyasini topadi yoki ochadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vakansiya darhol <b>YOPILADI</b>: u reja emas, tarixning idishi.
    /// Ochiq qolsa bosh sahifadagi «ochiq vakansiyalar» hisoblagichi
    /// yolg'on gapirardi va HR har kuni yopishga urinardi.
    /// </para>
    /// <para>
    /// Ikkinchi yugurishda YANGISI ochilmaydi: nomi bo'yicha topiladi.
    /// Shu sababli qadam idempotent — <c>--steps council</c> ni qayta
    /// yurgizish xavfsiz.
    /// </para>
    /// </remarks>
    private Vacancy? ArchiveVacancyOf()
    {
        var title = _workspace.VacancyTitleNamed(ArchiveVacancy);

        if (title is null)
        {
            var created = VacancyTitle.Create(ArchiveVacancy);

            if (created.IsFailure)
            {
                return null;
            }

            title = created.Value;
            _workspace.Register(title);
            _workspace.Database.Add(title);
        }

        if (_workspace.VacancyOfTitle(title.Id) is { } existing)
        {
            return existing;
        }

        var opened = Domain.Recruiting.Vacancy.Open(
            title.Id,
            openedAt: DateOnly.FromDateTime(DateTime.UtcNow),
            openings: 1,
            note: "Excel «91.1 Кенгаш хулосаси» varag'idan ko'chirilgan tarix. "
                + "Bu haqiqiy vakansiya EMAS — kengash yozuvlarining idishi.");

        if (opened.IsFailure)
        {
            return null;
        }

        var vacancy = opened.Value;
        _ = vacancy.Close(DateOnly.FromDateTime(DateTime.UtcNow));

        _workspace.Register(vacancy);
        _workspace.Database.Add(vacancy);

        return vacancy;
    }

    // ────────────────────────────────────────────────────────────
    //  Qator
    // ────────────────────────────────────────────────────────────

    private void ReadRow(
        ExcelRow row,
        Vacancy vacancy,
        Guid stageId,
        IReadOnlyDictionary<int, DateOnly> blocks,
        StepSummary summary)
    {
        var context = new RowContext(Name, SheetName, row.Number, row.Text(FullNameColumn));

        var parsed = FullName.Parse(row.Text(FullNameColumn));

        if (parsed.IsFailure)
        {
            _workspace.Report.Add(Name, SheetName, row.Number, row.Text(FullNameColumn),
                "ism ajratilmadi", parsed.Error.Message, EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var fullName = parsed.Value;
        var birthDate = row[BirthDateColumn].IsEmpty ? null : ExcelDate.Read(row[BirthDateColumn]);

        if (_workspace.CandidateOf(vacancy.Id, fullName, birthDate) is not null)
        {
            _workspace.Report.Add(Name, SheetName, row.Number, fullName.Full,
                "kengash yozuvi allaqachon ko'chirilgan", null, EtlAction.AlreadyExists);
            summary.Updated++;
            return;
        }

        // Xodim ISM bo'yicha izlanadi: varaqda na JSHSHIR, na pasport
        // bor. Topilishi majburiy EMAS — aynan topilmaganlar
        // («ishga olinmagan nomzod») bu varaqning asosiy qismi.
        var lookup = _workspace.FindEmployee(context, row[FullNameColumn], row[BirthDateColumn]);
        var employee = lookup.Employee;

        var note = row.Text(NoteColumn).Trim();
        var verdict = Verdict(row);

        var date = ResolveDate(row, employee, blocks, out var assumed);

        var registered = Register(fullName, vacancy.Id, birthDate, row);

        if (registered.IsFailure && birthDate is not null)
        {
            // Yagona rad etilish sababi — tug'ilgan sana chegaradan
            // tashqarida (varaqda «1899», «2015» kabi qiymatlar bor).
            // Sana tashlanadi, YOZUV esa qoladi: kengash xulosasi
            // tug'ilgan sanadan ko'ra qimmatliroq.
            _workspace.Report.Add(Name, SheetName, row.Number, fullName.Full,
                "tug'ilgan sana qabul qilinmadi — sanasiz yozildi",
                row.Text(BirthDateColumn), EtlAction.LeftEmpty);

            registered = Register(fullName, vacancy.Id, birthDate: null, row);
            birthDate = null;
        }

        if (registered.IsFailure)
        {
            _workspace.Report.Add(Name, SheetName, row.Number, fullName.Full,
                "nomzod yozuvi qabul qilinmadi", registered.Error.Message, EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var candidate = registered.Value;

        if (date is { } councilDate)
        {
            /*
              ⚠️ Kengash NATIJASI («o'tdi/o'tmadi») endi yozilmaydi va
              maydonning o'zi ham yo'q (2026-09-16). U varaqdagi matndan
              TAXMIN qilinardi va ko'p joyda yolg'on chiqardi: yorlig'i
              «o'tdi» bo'lgan yozuvning xulosasi aslida rad etilgani
              haqida gapirardi. Xulosa MATNI esa o'z o'rnida qoladi va
              u yagona ishonchli manba.
            */
            var recorded = candidate.RecordStage(
                stageId,
                councilDate,
                conclusion: Conclusion(note, verdict));

            if (recorded.IsFailure)
            {
                _workspace.Report.Add(Name, SheetName, row.Number, fullName.Full,
                    "kengash bosqichi yozilmadi", recorded.Error.Message, EtlAction.LeftEmpty);
            }
            else if (assumed)
            {
                _workspace.Report.Add(Name, SheetName, row.Number, fullName.Full,
                    "kengash sanasi qatorda yo'q — taxminiy sana qo'yildi",
                    councilDate.ToString("yyyy-MM-dd", null), EtlAction.Adjusted);
            }
        }
        else
        {
            // Sanasiz bosqich yozuvi yaratilmaydi: sana yozuvning
            // MA'NOSI («kengash qachon bo'ldi»), uni o'ylab topish esa
            // yolg'on tarix bo'lardi. Xulosa matni yo'qolmaydi — u
            // nomzodning izohida turadi.
            _workspace.Report.Add(Name, SheetName, row.Number, fullName.Full,
                "kengash sanasi topilmadi — xulosa nomzod izohiga yozildi",
                note.Length > 0 ? note : verdict, EtlAction.LeftEmpty);
        }

        Close(candidate, employee, date, note, summary);

        _workspace.Register(candidate);
        _workspace.Database.Add(candidate);
        summary.Created++;

        if (employee is not null)
        {
            Link(candidate, employee, summary);
        }
    }

    private static Result<Candidate> Register(FullName fullName, Guid vacancyId, DateOnly? birthDate, ExcelRow row)
        => Candidate.Register(
            fullName,
            vacancyId,
            CandidateSource.Excel,
            birthDate,
            Phone(row),
            address: null,
            speciality: null,
            note: Description(row));

    /// <summary>
    /// Nomzodni yopadi: xodim topilgan bo'lsa «ishga olindi», aks holda «rad etildi».
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega hammasi yopiladi.</b> Ochiq qolgan nomzod voronkada FAOL
    /// bo'lib ko'rinadi — HR ertaga 3000 ta «jarayondagi» nomzodni
    /// ko'rib, tizimga ishonmay qolardi. Bu yozuvlar esa tarix: ularning
    /// hammasi allaqachon hal bo'lgan.
    /// </para>
    /// <para>
    /// <b>Xodim bor = ishga olingan.</b> Boshqa dalil kerak emas: odam
    /// xodimlar jadvalida turgan ekan, kengashdan keyin ishga olingan.
    /// Rad etish sababi esa xulosa matnining o'zi — HR aynan shuni
    /// o'qiydi.
    /// </para>
    /// </remarks>
    private void Close(
        Candidate candidate,
        Employee? employee,
        DateOnly? date,
        string note,
        StepSummary summary)
    {
        // Yopish sanasi bosqich sanasidan olinadi. U ham bo'lmasa —
        // xodimning ishga kirish sanasi, u ham bo'lmasa bugun: bu
        // maydon ARXIV belgisi (`ClosedAt`), tarixiy da'vo emas.
        var at = date
            ?? HireDate(employee)
            ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var closed = employee is not null
            ? candidate.MarkHired(at)
            : candidate.Reject(at, note.Length > 0 ? note : null);

        if (closed.IsFailure)
        {
            _workspace.Report.Add(Name, SheetName, 0, candidate.FullName.Full,
                "nomzod yopilmadi", closed.Error.Message, EtlAction.LeftEmpty);
            summary.NeedsReview++;
        }
    }

    /// <summary>
    /// Nomzodni xodimning ishga qabul hodisasiga bog'laydi.
    /// </summary>
    /// <remarks>
    /// Xodim kartasidagi «Kengash xulosasi» bo'limi AYNAN shu
    /// bog'lanishni o'qiydi: <c>xodim → munosabat → hodisa.CandidateId →
    /// nomzod → bosqich yozuvlari</c>. Bog'lanish yozilmasa, nomzod
    /// yozuvi bazada bo'ladi-yu, xodim kartasida ko'rinmasdi.
    /// </remarks>
    private void Link(Candidate candidate, Employee employee, StepSummary summary)
    {
        foreach (var employment in _workspace.EmploymentsOf(employee.Id))
        {
            foreach (var employmentEvent in employment.Events)
            {
                if (employmentEvent.Type == EmploymentEventType.Hire
                    && employmentEvent.LinkCandidate(candidate.Id))
                {
                    return;
                }
            }
        }

        // Ishga qabul hodisasi topilmadi yoki allaqachon boshqa
        // nomzodga bog'langan. Nomzod yozuvi baribir saqlanadi —
        // u voronka tarixida ko'rinadi, faqat xodim kartasida emas.
        _workspace.Report.Add(Name, SheetName, 0, employee.FullName.Full,
            "ishga qabul hodisasi topilmadi — xulosa xodim kartasida ko'rinmaydi",
            null, EtlAction.Flagged);
        summary.NeedsReview++;
    }

    // ────────────────────────────────────────────────────────────
    //  Sana
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Har bir blokning sanasi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Varaq bloklarga bo'lingan: har bir kengash yig'ilishi o'z tartib
    /// raqamidan (1, 2, 3…) boshlanadi. Bir blokdagi odamlar BIR kunda
    /// ko'rilgan, sana esa ularning faqat bir qismida yozilgan.
    /// </para>
    /// <para>
    /// Shu sababli blokning eng erta sanasi butun blokka tarqatiladi.
    /// Bu <b>taxmin</b> va u hisobotga <c>Adjusted</c> bo'lib tushadi —
    /// HR qaysi yozuvda sana o'ylab topilganini ko'radi.
    /// </para>
    /// </remarks>
    private static Dictionary<int, DateOnly> BlockDates(IReadOnlyList<ExcelRow> rows)
    {
        var blockOfRow = new Dictionary<int, int>();
        var earliest = new Dictionary<int, DateOnly>();

        var block = 0;
        var previous = 0;

        // Yig'ilish sarlavhasidan olingan sana — eng ishonchlisi.
        // U blokning O'ZI haqida gapiradi, qo'shni qatorlardan
        // yig'ilgan taxmin emas.
        var declared = new Dictionary<int, DateOnly>();

        foreach (var row in rows)
        {
            if (row.Number < FirstDataRow || row.Number == SecondHeaderRow || row[FullNameColumn].IsEmpty)
            {
                continue;
            }

            if (IsSessionHeader(row))
            {
                // Sarlavha KEYINGI blokni ochadi: undan so'ng tartib
                // raqami 1 dan boshlanadi. Blok raqami shu yerda
                // oshiriladi, aks holda sana oldingi blokka yozilardi.
                block++;
                previous = 0;
                declared[block] = ExcelDate.Read(row[FullNameColumn])!.Value;
                continue;
            }

            var number = row[NumberColumn].IsWholeNumber ? (int)row[NumberColumn].Number!.Value : 0;

            if (number == 1 && previous != 1 && !declared.ContainsKey(block))
            {
                block++;
            }

            previous = number;
            blockOfRow[row.Number] = block;

            if (row[StartDateColumn].IsEmpty || ExcelDate.Read(row[StartDateColumn]) is not { } date)
            {
                continue;
            }

            if (!earliest.TryGetValue(block, out var known) || date < known)
            {
                earliest[block] = date;
            }
        }

        // Natija QATOR raqami bo'yicha beriladi: chaqiruvchi blok
        // raqamini bilmaydi va bilishi ham shart emas.
        var byRow = new Dictionary<int, DateOnly>(blockOfRow.Count);

        foreach (var (rowNumber, rowBlock) in blockOfRow)
        {
            if (declared.TryGetValue(rowBlock, out var session))
            {
                byRow[rowNumber] = session;
            }
            else if (earliest.TryGetValue(rowBlock, out var date))
            {
                byRow[rowNumber] = date;
            }
        }

        return byRow;
    }

    /// <summary>
    /// Kengash sanasi: o'z qatoridan → xodimning ishga kirish sanasidan → blokdan.
    /// </summary>
    /// <remarks>
    /// Xodimning ishga kirish sanasi blokdan OLDIN turadi va bu ataylab:
    /// u aniq ma'lum fakt, blok sanasi esa qo'shni qatorlardan olingan
    /// taxmin. Ikkalasi ham <c>assumed</c> deb belgilanadi — ishga
    /// kirish sanasi kengash kuni EMAS, unga yaqin kun.
    /// </remarks>
    private DateOnly? ResolveDate(
        ExcelRow row,
        Employee? employee,
        IReadOnlyDictionary<int, DateOnly> blocks,
        out bool assumed)
    {
        assumed = false;

        if (!row[StartDateColumn].IsEmpty && ExcelDate.Read(row[StartDateColumn]) is { } own)
        {
            return own;
        }

        assumed = true;

        if (HireDate(employee) is { } hired)
        {
            return hired;
        }

        return blocks.Count > 0 && BlockOf(row, blocks) is { } fromBlock ? fromBlock : null;
    }

    private static DateOnly? BlockOf(ExcelRow row, IReadOnlyDictionary<int, DateOnly> blocks)
        => blocks.TryGetValue(row.Number, out var date) ? date : null;

    private DateOnly? HireDate(Employee? employee)
    {
        if (employee is null)
        {
            return null;
        }

        DateOnly? earliest = null;

        foreach (var employment in _workspace.EmploymentsOf(employee.Id))
        {
            if (earliest is null || employment.HireDate < earliest)
            {
                earliest = employment.HireDate;
            }
        }

        return earliest;
    }

    // ────────────────────────────────────────────────────────────
    //  Xulosa
    // ────────────────────────────────────────────────────────────

    /// <summary>Kengash a'zolari ustunlaridagi birinchi ma'noli qiymat.</summary>
    /// <remarks>
    /// Ustunlar HAR DOIM ham xulosa saqlamaydi: varaqning ayrim
    /// qismlarida ularga korxona nomi yoki lavozim yozib ketilgan.
    /// Shu sababli tanilmagan matn xulosa deb qabul qilinmaydi —
    /// u shunchaki e'tiborsiz qoldiriladi.
    /// </remarks>
    private static string Verdict(ExcelRow row)
    {
        foreach (var column in MemberColumns)
        {
            var text = row.Text(column).Trim();

            if (text.Length > 0 && Recognized(text))
            {
                return text;
            }
        }

        return string.Empty;
    }

    private static bool Recognized(string text) => Mark(text) || Form(text);

    /// <summary>Katakda kengash belgisi turibdimi («✔», «❌»).</summary>
    private static bool Mark(string text)
        => text is PassedMark or PassedMarkAlt or FailedMark or FailedMarkAlt;

    /// <summary>Katakda kengash xulosasining so'zi turibdimi.</summary>
    private static bool Form(string text)
    {
        var key = Fold(text);

        foreach (var form in FailedForms)
        {
            if (key.Contains(form, StringComparison.Ordinal))
            {
                return true;
            }
        }

        foreach (var form in PassedForms)
        {
            if (key.Contains(form, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Bosqich yozuvining xulosa matni.</summary>
    private static string? Conclusion(string note, string verdict)
    {
        if (note.Length > 0)
        {
            return note;
        }

        // Izoh yo'q bo'lsa xulosaning O'ZI yoziladi («Тавсия этилмади»).
        // Bo'sh xulosa HR uchun «hech narsa yozilmagan» degani va u
        // natija nishonidan ko'proq ma'lumot bermasdi.
        return verdict.Length > 0 ? verdict : null;
    }

    /// <summary>
    /// Nomzod izohi — varaqning bosqich yozuviga sig'magan qismi.
    /// </summary>
    /// <remarks>
    /// Lavozim, ish joyi va mas'ul shaxs bosqich yozuvida saqlanadigan
    /// maydonga ega emas, lekin ular kengash qarorining KONTEKSTI:
    /// «qaysi ishga ko'rildi va kim biriktirildi». Izohga yozilishi ham
    /// shundan — ular yo'qolmasin, lekin xulosa matnini ham
    /// to'ldirmasin.
    /// </remarks>
    private static string? Description(ExcelRow row)
    {
        var parts = new List<string>(3);

        var position = row.Text(PositionColumn).Trim();
        if (position.Length > 0)
        {
            parts.Add($"Лавозим: {position}");
        }

        var responsible = row.Text(ResponsibleColumn).Trim();
        if (responsible.Length > 0)
        {
            parts.Add($"Масъул: {responsible}");
        }

        return parts.Count > 0 ? string.Join(" · ", parts) : null;
    }

    /// <summary>
    /// Taqqoslash uchun matnni soddalashtiradi.
    /// </summary>
    /// <remarks>
    /// <c>ProbationsStep.Fold</c> bilan bir xil qoida va u ATAYLAB
    /// nusxalangan: ikkala varaqdagi xulosa so'zlari mustaqil ravishda
    /// o'zgarishi mumkin va umumiy yordamchi ikkalasini bir-biriga
    /// bog'lab qo'yardi. Qoida esa kichik — kirill gomogliflari va
    /// registr.
    /// </remarks>
    private static string Fold(string value)
    {
        var folded = value.Trim().ToLowerInvariant();

        return folded
            .Replace('ў', 'у')
            .Replace('қ', 'к')
            .Replace('ғ', 'г')
            .Replace('ҳ', 'х')
            .Replace('ё', 'е')
            .Replace("ъ", string.Empty, StringComparison.Ordinal)
            .Replace("ь", string.Empty, StringComparison.Ordinal);
    }

    private static PhoneNumber? Phone(ExcelRow row)
    {
        var cell = row[PhoneColumn];

        if (cell.IsEmpty)
        {
            return null;
        }

        var text = cell.IsWholeNumber
            ? cell.Number!.Value.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)
            : cell.Text;

        var created = PhoneNumber.Create(text);

        // Telefon MAJBURIY emas: varaqdagi raqamlarning bir qismi
        // buzuq va ular sabab butun kengash yozuvini tashlab yuborish
        // mutlaqo noo'rin bo'lardi.
        return created.IsSuccess ? created.Value : null;
    }
}
