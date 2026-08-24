using System.Globalization;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Etl.Excel;
using Bonvi.Etl.Reporting;
using Bonvi.Etl.Runtime;

namespace Bonvi.Etl.Steps;

/// <summary>
/// 5-qadam: xodim belgilarini ko'chiradi.
/// </summary>
/// <remarks>
/// <para>
/// To'rtta varaq — to'rtta belgi turi: rezerv, qora ro'yxat, «vazifa bersa
/// bo'ladigan» va nazorat. Ustunlari har xil, lekin ish tartibi bir xil:
/// sanani top, odamni top, sababni yig'. Shu sababli varaqlarning farqi
/// tavsifga chiqarilgan va mantiq bitta joyda yozilgan.
/// </para>
/// <para>
/// <b>Bugungi sana hech qachon qo'yilmaydi.</b> Belgi boshlanish sanasi
/// Excel'da topilmasa, qator o'tkazib yuboriladi va hisobotga tushadi:
/// «bugundan boshlab rezervda» degan yozuv o'ylab topilgan ma'lumot bo'lardi
/// va keyin uni haqiqiydan ajratib bo'lmasdi.
/// </para>
/// <para>
/// <b>Tugash sanasi hech qayerda qo'yilmaydi.</b> Rezerv varag'ida
/// «Бошқа иш топган, ўзи отказ қилди» kabi izohlar bor — ular belgi
/// yopilganini bildiradi, lekin qachon yopilganini emas. Belgi ochiq
/// qoladi va izoh sabab matnida saqlanadi.
/// </para>
/// </remarks>
public sealed class FlagsStep
{
    /// <summary>Qadam nomi (<c>--steps</c> uchun).</summary>
    public const string Name = "flags";

    /// <summary>F.I.Sh — to'rtala varaqda ham ikkinchi ustun.</summary>
    private const int FullNameColumn = 2;

    /// <summary>Belgi varag'ining tavsifi.</summary>
    private sealed record FlagSheet
    {
        /// <summary>Varaq nomi.</summary>
        public required string Name { get; init; }

        /// <summary>Ma'lumot boshlanadigan qator.</summary>
        public required int FirstDataRow { get; init; }

        /// <summary>Shu varaqdan qo'yiladigan belgi.</summary>
        public required EmployeeFlagType Type { get; init; }

        /// <summary>
        /// Belgi boshlanish sanasi izlanadigan ustunlar — ishonchlilik
        /// tartibida. Birinchisi bo'sh bo'lsa keyingisi sinaladi.
        /// </summary>
        public required IReadOnlyList<int> StartDateColumns { get; init; }

        /// <summary>Sabab matni yig'iladigan ustunlar.</summary>
        public IReadOnlyList<int> ReasonColumns { get; init; } = [];

        /// <summary>
        /// Varaqdagi odamlar hali xodim emasmi.
        /// </summary>
        /// <remarks>
        /// Faqat rezerv varag'i uchun <c>true</c>: u yerdagilar nomzod, ya'ni
        /// ularning bazada bo'lmasligi <b>kutilgan hol</b>, xato emas.
        /// </remarks>
        public bool Candidates { get; init; }
    }

    /// <summary>
    /// To'rtta varaq va ularning ustun tartibi.
    /// </summary>
    /// <remarks>
    /// Ustunlar nom bo'yicha emas, raqam bo'yicha o'qiladi: bloknotda
    /// sarlavhalar birlashtirilgan, takrorlanadi va varaqdan varaqqa
    /// boshqacha yozilgan («Келган сана», «Ишга келган сана»,
    /// «ХОДИМ ИШГА КИРГАН САНА» — bir xil narsa).
    /// </remarks>
    private static readonly FlagSheet[] Sheets =
    [
        new()
        {
            // 1 №, 2 F.I.Sh, 3 tug'ilgan sana, 4 anketa sanasi,
            // 5 kelgan sana, 6 qaysi lavozimga, 7 telefon, 8 status.
            Name = "91.9 Резерв",
            FirstDataRow = 3,
            Type = EmployeeFlagType.Reserve,

            // «Келган сана» 65 qatordan faqat 33 tasida to'ldirilgan, shu
            // sababli zaxira sifatida anketa topshirilgan kun olinadi —
            // odam aynan o'shanda rezervga tushgan.
            StartDateColumns = [5, 4],

            // Ikkala ustun ham alohida to'liq javob bermaydi: 6-ustunda
            // qaysi ishga da'vogar («Метан заводга»), 8-ustunda hozirgi
            // holat («Бўш иш ўрни бўлса олинади»).
            ReasonColumns = [6, 8],

            Candidates = true,
        },
        new()
        {
            // 1 №, 2 F.I.Sh, 3 lavozim, 4 ishdan ketish sababi,
            // 5 obxodnoy, 6 ishga kirish sanasi, 7 ishdan ketish sanasi.
            Name = "91.10 Чёрный список",
            FirstDataRow = 3,
            Type = EmployeeFlagType.Blacklist,

            // Qora ro'yxatga odam ishdan ketgan kuni tushadi. Bu sana
            // yo'q bo'lsa, ishga kirish sanasi — eng yomon holatda ham
            // belgi odam korxonaga kelgandan keyin boshlanadi.
            StartDateColumns = [7, 6],

            ReasonColumns = [4],
        },
        new()
        {
            // 1 №, 2 F.I.Sh, 3 ishga kelgan sana, 4 bo'lim, 5 lavozim, 6 izoh.
            Name = "91.12 Вазифа берса бўладиган хо",
            FirstDataRow = 2,
            Type = EmployeeFlagType.Capable,
            StartDateColumns = [3],
            ReasonColumns = [6],
        },
        new()
        {
            // 1 №, 2 F.I.Sh, 3 ishga kirgan sana, 4 bo'lim, 5 lavozim,
            // 6 sinov muddati, 7 yashash joyi, 8 pasport, 9 telefon,
            // 10 nazoratga olingan sana, 11 mas'ul shaxs.
            Name = "Назоратга олинган ходимлар",
            FirstDataRow = 3,
            Type = EmployeeFlagType.Monitoring,
            StartDateColumns = [10, 3],

            // Sabab ustuni yo'q: varaqda «nima uchun nazoratga olindi»
            // degan savolga javob beradigan katak umuman ko'zda tutilmagan.
            // Mas'ul shaxs (11-ustun) sabab emas, shu sababli u yerga
            // yozilmaydi.
            ReasonColumns = [],
        },
    ];

    private readonly EtlWorkspace _workspace;

    public FlagsStep(EtlWorkspace workspace) => _workspace = workspace;

    /// <summary>To'rtala varaqni ko'chiradi.</summary>
    public void Run()
    {
        var summary = _workspace.Report.Step(Name);

        foreach (var sheet in Sheets)
        {
            ReadSheet(sheet, summary);
        }
    }

    private void ReadSheet(FlagSheet sheet, StepSummary summary)
    {
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
            // «Назоратга олинган ходимлар» varag'i deyarli bo'sh: 13
            // qatordan ikkitasi sarlavha, qolgani bo'sh. Bo'sh qator xato
            // emas — hisobotga yozilmaydi.
            if (row.Number < sheet.FirstDataRow || row[FullNameColumn].IsEmpty)
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

    private void ReadRow(FlagSheet sheet, ExcelRow row, StepSummary summary)
    {
        var context = new RowContext(Name, sheet.Name, row.Number, row.Text(FullNameColumn));

        // Sana ATAYLAB birinchi o'qiladi. Varaqlar ichida yil sarlavhalari
        // yotibdi («2025 ЙИЛ», «2024 йил»), rezerv varag'ining 69-qatoridan
        // keyin esa butunlay boshqa jadval boshlanadi («Лавозим | Сони» —
        // lavozimlar bo'yicha ehtiyoj). Ularning birortasida sana yo'q, ya'ni
        // shu tekshiruv ularni xodim qidirishga umuman qo'ymaydi va hisobotda
        // «xodim topilmadi» degan chalg'ituvchi yozuv paydo bo'lmaydi.
        if (StartDate(sheet, row, context) is not { } startDate)
        {
            _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                "belgi boshlanish sanasi topilmadi",
                row.Text(sheet.StartDateColumns[0]), EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var lookup = Locate(sheet, row, context);

        if (lookup.Employee is not { } employee)
        {
            // Sabab allaqachon hisobotga yozilgan — bu yerda faqat sanoq.
            summary.Skipped++;
            return;
        }

        context = context.For(lookup.Label);

        if (_workspace.HasFlag(employee.Id, sheet.Type, startDate))
        {
            _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                "belgi allaqachon qo'yilgan",
                startDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                EtlAction.AlreadyExists);
            summary.Updated++;
            return;
        }

        var raised = EmployeeFlag.Raise(employee.Id, sheet.Type, startDate, Reason(sheet, row));

        if (raised.IsFailure)
        {
            // Deyarli har doim bitta sabab: qora ro'yxat sababsiz
            // qo'yilmaydi. Excel'da 4-ustuni bo'sh qatorlar bor («Собиров
            // Асрорбек Алишер ўғли») va aynan shunday yozuvlar tufayli
            // domen qoidasi kiritilgan — keyin hech kim odam nima uchun
            // ro'yxatga tushganini bilmasdi. Sababni bu yerda to'qib
            // qo'yish qoidani aylanib o'tish bo'lardi.
            _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                "belgi qo'yilmadi", raised.Error.Message, EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var flag = raised.Value;

        _workspace.Register(flag);
        _workspace.Database.Add(flag);
        summary.Created++;
    }

    /// <summary>Belgi boshlanish sanasini ustunlar bo'yicha navbat bilan izlaydi.</summary>
    private DateOnly? StartDate(FlagSheet sheet, ExcelRow row, RowContext context)
    {
        foreach (var column in sheet.StartDateColumns)
        {
            if (_workspace.Date(context, row[column], "belgi boshlanish sanasi") is { } date)
            {
                return date;
            }
        }

        return null;
    }

    /// <summary>
    /// Qatordagi odamni xodimlar indeksidan topadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rezerv varag'i uchun <see cref="EtlWorkspace"/> ning tayyor qidiruvi
    /// <b>ataylab ishlatilmaydi</b>. U topilmagan qatorni «xodim topilmadi,
    /// qator o'tkazib yuborildi» deb yozadi va bu noto'g'ri xulosa bo'lardi:
    /// rezervdagilar ko'pincha hali xodim emas — nomzod. Ular 2-bosqichda
    /// (rekruting) bazaga tushadi. Ularni xato sifatida ko'rsatish hisobotning
    /// asosiy foydasini — «nimani tekshirish kerak» ro'yxatini — buzardi.
    /// </para>
    /// <para>
    /// Qolgan uchta varaqda esa odam haqiqatan ham xodim bo'lishi kerak, shu
    /// sababli u yerda topilmagani chinakam muammo va standart yo'l ishlaydi.
    /// </para>
    /// </remarks>
    private EmployeeLookup Locate(FlagSheet sheet, ExcelRow row, RowContext context)
    {
        var nameCell = row[FullNameColumn];

        if (!sheet.Candidates)
        {
            return _workspace.FindEmployee(context, nameCell);
        }

        var parsed = FullName.Parse(nameCell.Text);

        if (parsed.IsFailure)
        {
            _workspace.Report.Add(Name, sheet.Name, row.Number, nameCell.Text,
                "ism ajratilmadi", parsed.Error.Message, EtlAction.RowSkipped);
            return EmployeeLookup.None;
        }

        var fullName = parsed.Value;
        var match = _workspace.Employees.FindByName(fullName);

        if (match.Employee is not null)
        {
            return new EmployeeLookup(match.Employee, fullName);
        }

        var problem = match.Kind == EmployeeMatchKind.Ambiguous
            ? $"bir xil ismli {match.Candidates} ta xodim bor — tanlanmadi"
            : "rezerv nomzodi bazada xodim sifatida yo'q — nomzodlar 2-bosqichda (rekruting) qo'shiladi";

        _workspace.Report.Add(Name, sheet.Name, row.Number, fullName.Full,
            problem, nameCell.Text, EtlAction.Ignored);

        return new EmployeeLookup(null, fullName);
    }

    /// <summary>
    /// Sabab matnini yig'adi.
    /// </summary>
    /// <remarks>
    /// Bir nechta ustun birlashtiriladi, chunki alohida-alohida ularning
    /// birortasi to'liq javob bermaydi: rezervda 6-ustun «qaysi ishga»
    /// («Метан заводга»), 8-ustun esa «nima bo'ldi» («Бошқа иш топган,
    /// ўзи отказ қилди») degan savolga javob beradi.
    /// </remarks>
    private static string? Reason(FlagSheet sheet, ExcelRow row)
    {
        var parts = new List<string>(sheet.ReasonColumns.Count);

        foreach (var column in sheet.ReasonColumns)
        {
            var text = row.Text(column);

            if (text.Length > 0)
            {
                parts.Add(text);
            }
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }
}
