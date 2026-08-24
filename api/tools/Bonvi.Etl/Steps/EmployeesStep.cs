using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Etl.Excel;
using Bonvi.Etl.Mapping;
using Bonvi.Etl.Reporting;
using Bonvi.Etl.Runtime;

namespace Bonvi.Etl.Steps;

/// <summary>
/// 1–2-qadam: xodim kartalarini ko'chiradi (asosiy ro'yxat, yangi kelganlar, arxiv).
/// </summary>
/// <remarks>
/// <para>
/// Bu qadam faqat <b>shaxs</b> haqidagi ma'lumotni ko'chiradi: ism, tug'ilgan
/// sana, JSHSHIR, pasport, telefon, manzil, yo'nalishlar. Kim qayerda
/// ishlagani — keyingi qadamda (<see cref="EmploymentsStep"/>), chunki
/// modelda shaxs va mehnat munosabati ataylab ajratilgan.
/// </para>
/// <para>
/// <b>Varaqlar tartibi muhim.</b> Bir xil odam uchala varaqda ham bo'lishi
/// mumkin (ketib, qaytib kelgan). Yangiroq manba birinchi o'qiladi, keyingilari
/// esa faqat <b>bo'sh</b> maydonlarni to'ldiradi va hech qachon ustidan
/// yozmaydi — sabab <see cref="Run"/> da batafsil.
/// </para>
/// </remarks>
public sealed class EmployeesStep
{
    /// <summary>Qadam nomi (<c>--steps</c> uchun).</summary>
    public const string Name = "employees";

    /// <summary>«Текширилди» — sudlanganlik ustunidagi «tekshirildi» belgisi.</summary>
    private const string CheckedMark = "текширилди";

    private readonly EtlWorkspace _workspace;

    /// <summary>Shu qadamda belgilangan xodimlar — jamlanmada ikki marta sanalmasin.</summary>
    private readonly HashSet<Guid> _flagged = [];

    /// <summary>JSHSHIR yo'qligi haqida xabar berilgan xodimlar.</summary>
    /// <remarks>
    /// Bir odam uch varaqda uchraydi va hech qaysisida JSHSHIR bo'lmasligi
    /// mumkin. Har bir varaq uchun alohida satr yozilsa, 1540 xodim
    /// hisobotda 2578 satr bo'lib ko'rinardi va HR ro'yxatning uzunligiga
    /// qarab noto'g'ri xulosa chiqarardi.
    /// </remarks>
    private readonly HashSet<Guid> _missingPinflReported = [];

    public EmployeesStep(EtlWorkspace workspace) => _workspace = workspace;

    /// <summary>Uchala varaqni ko'chiradi.</summary>
    /// <remarks>
    /// <para>
    /// Tartib — <b>ma'lumotning yangiligi bo'yicha</b>, chunki
    /// <see cref="Fill"/> hech qachon mavjud qiymat ustidan yozmaydi: kim
    /// birinchi kelsa, o'sha qoladi.
    /// </para>
    /// <list type="number">
    ///   <item><b>«91 Умумий ходимлар»</b> — bugun ishlab turgan 540 xodim.
    ///         HR uni har kuni yangilaydi, ya'ni eng ishonchli manba;</item>
    ///   <item><b>«91.2 Янги ишга келганлар»</b> — qabul paytida to'ldirilgan
    ///         karta. Undagi pasport, JSHSHIR va manzil odam ishga kirgan
    ///         kunidagi holat: asosiy ro'yxatdan eskiroq, lekin arxivdan
    ///         yangiroq va deyarli hech qachon bo'sh emas;</item>
    ///   <item><b>«91.7 Архив»</b> — eng eski qatlam. U faqat birinchi ikkitasi
    ///         to'ldirmagan bo'sh maydonlarni yopadi.</item>
    /// </list>
    /// </remarks>
    public void Run()
    {
        var summary = _workspace.Report.Step(Name);

        ReadSheet(EmployeeSheet.Main, summary);
        ReadSheet(EmployeeSheet.NewHires, summary);
        ReadSheet(EmployeeSheet.Archive, summary);
    }

    /// <summary>
    /// Bitta varaqni o'qiydi va har bir qator uchun xodimni yaratadi
    /// yoki mavjudini to'ldiradi.
    /// </summary>
    public void ReadSheet(EmployeeSheet sheet, StepSummary summary)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(summary);

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

            var nameCell = row[sheet.FullName];
            if (nameCell.IsEmpty)
            {
                continue;
            }

            // Blok sarlavhasi: ismi bor, lekin lavozimi yo'q qator. Bu xodim
            // emas, varaqning bo'linishi — mehnat munosabatlari qadamida
            // bo'limning ikkinchi dalili sifatida o'qiladi.
            if (sheet.HasBlockHeaders && !row.Has(sheet.Position))
            {
                continue;
            }

            if (_workspace.Options.Limit is { } limit && processed >= limit)
            {
                break;
            }

            processed++;
            summary.Read++;

            ReadRow(sheet, row, summary);
        }
    }

    private void ReadRow(EmployeeSheet sheet, ExcelRow row, StepSummary summary)
    {
        var context = new RowContext(Name, sheet.Name, row.Number, string.Empty);

        // Takroriy ustun sarlavhasi («Ф.И.О», «Ходимнинг Ф.И.Ш») xato emas —
        // varaqning tuzilishi. Tekshiruv ajratishdan OLDIN turadi, chunki
        // uch so'zli sarlavha to'liq ism sifatida muvaffaqiyatli ajraladi.
        if (EtlWorkspace.IsHeaderRow(row[sheet.FullName]))
        {
            summary.Skipped++;
            return;
        }

        var nameResult = FullName.Parse(row.Text(sheet.FullName));
        if (nameResult.IsFailure)
        {
            _workspace.Report.Add(Name, sheet.Name, row.Number, row.Text(sheet.FullName),
                "ism ajratilmadi", nameResult.Error.Message, EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var fullName = nameResult.Value;
        context = context.For(fullName.Full);

        var birthDate = _workspace.Date(context, row[sheet.BirthDate], "tug'ilgan sana");
        var pinfl = ReadPinfl(context, row[sheet.Pinfl], out var pinflProblem);
        var passport = ReadPassport(context, row[sheet.Passport]);

        var match = _workspace.Employees.Find(pinfl, passport, fullName, birthDate);

        Employee employee;

        if (match.Found)
        {
            employee = match.Employee!;
            summary.Updated++;
        }
        else
        {
            var created = Employee.Create(fullName, birthDate);

            if (created.IsFailure)
            {
                // Tug'ilgan sana haqiqiy emas (kelajakda yoki 100 yildan eski).
                // Xodimni sanasiz yaratamiz — odam yo'qolmasin.
                _workspace.Report.Add(Name, sheet.Name, row.Number, fullName.Full,
                    "tug'ilgan sana qabul qilinmadi", row.Text(sheet.BirthDate), EtlAction.LeftEmpty);

                created = Employee.Create(fullName, birthDate: null);

                if (created.IsFailure)
                {
                    _workspace.Report.Add(Name, sheet.Name, row.Number, fullName.Full,
                        "xodim yaratilmadi", created.Error.Message, EtlAction.RowSkipped);
                    summary.Skipped++;
                    return;
                }

                Flag(created.Value, "tug'ilgan sana Excel'da noto'g'ri");
            }

            employee = created.Value;
            summary.Created++;

            _workspace.Register(employee);
            _workspace.Database.Add(employee);
        }

        if (match.Kind == EmployeeMatchKind.Ambiguous)
        {
            _workspace.Report.Add(Name, sheet.Name, row.Number, fullName.Full,
                $"bir xil ismli {match.Candidates} ta xodim bor — yangi yozuv yaratildi",
                fullName.Full, EtlAction.Flagged);
            Flag(employee, "bir xil ismli boshqa xodim bor");
        }

        Fill(sheet, row, context, employee, pinfl, pinflProblem, passport);

        if (employee.NeedsReview && _flagged.Add(employee.Id))
        {
            summary.NeedsReview++;
        }
    }

    /// <summary>Bo'sh maydonlarni to'ldiradi. Mavjud qiymat USTIDAN YOZILMAYDI.</summary>
    private void Fill(
        EmployeeSheet sheet,
        ExcelRow row,
        RowContext context,
        Employee employee,
        Pinfl? pinfl,
        string? pinflProblem,
        PassportSeries? passport)
    {
        if (pinfl is not null && employee.Pinfl is null)
        {
            var applied = employee.SetPinfl(pinfl);

            if (applied.IsFailure)
            {
                // JSHSHIR ichidagi sana kiritilgan sana bilan mos kelmadi.
                // JSHSHIR saqlanadi (u davlat raqami), lekin yozuv belgilanadi.
                _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                    "JSHSHIR ichidagi sana tug'ilgan sanaga mos emas", pinfl.Value, EtlAction.Flagged);
                Flag(employee, "JSHSHIR va tug'ilgan sana mos emas");
            }
        }
        else if (pinflProblem is not null && employee.Pinfl is null)
        {
            // Xodimda JSHSHIR hech qaysi varaqdan ham to'planmadi. Ana endi
            // bu HR uchun haqiqiy ish: davlat raqamisiz xodimni tizimda
            // ishonchli aniqlab bo'lmaydi.
            if (_missingPinflReported.Add(employee.Id))
            {
                _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                    pinflProblem, row.Text(sheet.Pinfl), EtlAction.Flagged);
            }

            Flag(employee, "JSHSHIR yo'q yoki noto'g'ri");
        }

        if (passport is not null && employee.Passport is null)
        {
            employee.SetPassport(passport);
        }

        if (employee.Phone is null)
        {
            var phones = PhoneCell.Parse(row[sheet.Phone]);

            if (phones.Primary is not null)
            {
                employee.SetPhones(phones.Primary, phones.Second);
            }

            foreach (var rejected in phones.Rejected)
            {
                _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                    "telefon raqami o'qilmadi", rejected, EtlAction.LeftEmpty);
            }

            if (phones.Extra.Count > 0)
            {
                _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                    "uchinchi va keyingi telefon raqamlari saqlanmadi",
                    string.Join(' ', phones.Extra), EtlAction.LeftEmpty);
            }
        }

        if (employee.Address is null)
        {
            employee.SetAddress(row.Text(sheet.Address));
        }

        if (employee.Speciality is null)
        {
            employee.SetSpeciality(row.Text(sheet.Speciality));
        }

        if (employee.TelegramUrl is null)
        {
            var telegram = row[sheet.Telegram];
            if (telegram.IsEmpty && sheet.TelegramAlternate > 0)
            {
                telegram = row[sheet.TelegramAlternate];
            }

            if (telegram.HasValue)
            {
                var applied = employee.SetTelegram(telegram.Text);
                if (applied.IsFailure)
                {
                    _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                        "telegram havolasi o'qilmadi", telegram.Text, EtlAction.LeftEmpty);
                }
            }
        }

        if (employee.RegisteredOnMehnatUz is null && row.Has(sheet.MehnatUz))
        {
            // Ustunda havola bo'lsa — ro'yxatdan o'tgan. Bo'sh bo'lsa
            // «o'tmagan» degani EMAS: shunchaki ma'lumot yo'q.
            var value = row.Text(sheet.MehnatUz);
            if (value.Contains("mehnat.uz", StringComparison.OrdinalIgnoreCase))
            {
                employee.SetRegisteredOnMehnatUz(true);
            }
        }

        ReadCriminalRecord(sheet, row, context, employee);

        // Bo'lim va lavozim xodimda emas, mehnat munosabatida saqlanadi —
        // ular keyingi qadamda o'qiladi.
        foreach (var directionId in _workspace.Directions(context, row[sheet.Direction]))
        {
            employee.AssignDirection(directionId);
        }
    }

    /// <summary>
    /// «Судланганлик» ustunini o'qiydi.
    /// </summary>
    /// <remarks>
    /// Bu ustunda ikki xil ma'no bor: <c>«Текширилди»</c> — xavfsizlik
    /// tekshiruvi o'tkazilgan (66 + 134 yozuv), qolgani esa sudlanganlik
    /// haqidagi izoh («266.1 Судланган 12.08.2017»). Ularni ajratmasa,
    /// tekshiruvdan o'tgan odam sudlangan bo'lib ko'rinardi.
    /// </remarks>
    private void ReadCriminalRecord(EmployeeSheet sheet, ExcelRow row, RowContext context, Employee employee)
    {
        var cell = row[sheet.CriminalRecord];
        if (cell.IsEmpty)
        {
            return;
        }

        if (AliasTable.Key(cell.Text).Contains(CheckedMark, StringComparison.Ordinal))
        {
            if (employee.SecurityChecked is null)
            {
                employee.SetSecurityChecked(true);
            }

            return;
        }

        // Ustun siljigan: sana yoki telefon raqami tushib qolgan.
        if (cell.Number is not null)
        {
            _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                "sudlanganlik ustunida son turibdi", cell.Text, EtlAction.Ignored);
            return;
        }

        if (employee.CriminalRecordNote is null)
        {
            employee.SetCriminalRecordNote(cell.Text);
        }
    }

    private Pinfl? ReadPinfl(RowContext context, ExcelCell cell, out string? problem)
    {
        problem = null;

        if (cell.IsEmpty)
        {
            // Hisobot satri BU YERDA yozilmaydi. Sabab: bir odam uch varaqda
            // uchraydi va JSHSHIR faqat bittasida bo'lishi mumkin. Katak
            // bo'shligi hali muammo emas — muammo shundaki, xodim OXIRIDA
            // JSHSHIR'siz qolsa. Shu sababli qaror `Fill` ga qoldiriladi.
            problem = "JSHSHIR kiritilmagan";
            return null;
        }

        var result = Pinfl.Create(cell.Text);

        if (result.IsSuccess)
        {
            return result.Value;
        }

        problem = result.Error.Message;
        _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
            "JSHSHIR qabul qilinmadi", cell.Text, EtlAction.Flagged);

        return null;
    }

    private PassportSeries? ReadPassport(RowContext context, ExcelCell cell)
    {
        if (cell.IsEmpty)
        {
            return null;
        }

        var result = PassportSeries.Create(cell.Text);

        if (result.IsSuccess)
        {
            return result.Value;
        }

        _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
            "pasport qabul qilinmadi", cell.Text, EtlAction.LeftEmpty);

        return null;
    }

    /// <summary>Xodimni tekshirishga belgilaydi — sabab takrorlanmaydi.</summary>
    private static void Flag(Employee employee, string reason)
    {
        if (employee.ReviewNote?.Contains(reason, StringComparison.Ordinal) == true)
        {
            return;
        }

        employee.FlagForReview(reason);
    }
}
