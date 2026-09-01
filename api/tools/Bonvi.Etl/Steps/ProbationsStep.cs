using System.Globalization;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Etl.Excel;
using Bonvi.Etl.Mapping;
using Bonvi.Etl.Reporting;
using Bonvi.Etl.Runtime;

namespace Bonvi.Etl.Steps;

/// <summary>
/// 4-qadam: sinov muddatlarini ko'chiradi.
/// </summary>
/// <remarks>
/// <para>
/// Uchta manba bor va ular <b>ataylab shu tartibda</b> o'qiladi:
/// </para>
/// <list type="number">
///   <item>«91.5 Синов муддатидаги ходимлар» — eng boy manba: har bir
///         qatorda muddat tugash sanasi ham, HR yozgan xulosa ham bor
///         (1225 ta ma'lumot qatori);</item>
///   <item>«91 Умумий ходимлар» ning sinov muddati ustuni — 128 katakda
///         sana, 411 katakda <c>«Тугатилган»</c>;</item>
///   <item>«91.2 Янги ишга келганлар» ning sinov muddati ustuni — eng
///         yangi ishga qabullar, 1043 katakda sana.</item>
/// </list>
/// <para>
/// Tartib muhim: 91.5 xulosa matnini beradi, qolgan ikkitasi esa faqat
/// sanani. Asosiy varaq oldin o'qilsa, o'sha mehnat munosabatida sinov
/// muddati allaqachon paydo bo'lardi va 91.5 dagi xulosa yozilmay qolardi —
/// <see cref="EtlWorkspace.HasProbation"/> ikkinchi yozuvni to'sib qo'yadi.
/// </para>
/// <para>
/// <b>«91.7 Архив» varag'i ataylab o'qilmaydi.</b> Unda shu ustun siljib
/// ketgan: 376 katakda lavozim nomi yotibdi («Юк ташувчи», «Краска ходими»).
/// Bunday ustunga tayanib sinov muddati yaratish — ma'lumot o'ylab topish
/// bo'lardi (<see cref="ProbationCell"/> izohiga qarang).
/// </para>
/// </remarks>
public sealed class ProbationsStep
{
    /// <summary>Qadam nomi (<c>--steps</c> uchun).</summary>
    public const string Name = "probations";

    /// <summary>«91.5 Синов муддатидаги ходимлар» — asosiy manba.</summary>
    private const string SheetName = "91.5 Синов муддатидаги ходимлар";

    /// <summary>
    /// Varaqning ustun tartibi: 1 №, 2 F.I.Sh, 3 ishga kirgan sana,
    /// 4 bo'lim, 5 lavozim, 6 sinov muddati tugash sanasi, 7 xulosa.
    /// Bo'lim va lavozim bu qadamga kerak emas — ular mehnat munosabatida.
    /// </summary>
    private const int FirstDataRow = 3;

    private const int FullNameColumn = 2;
    private const int HireDateColumn = 3;
    private const int EndDateColumn = 6;
    private const int ConclusionColumn = 7;

    /// <summary>
    /// Salbiy natijaning Excel'da uchraydigan yozilishlari.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>«ўтмади»</c> dan tashqari <c>«ўта олмади»</c> ham bor va u kamroq
    /// emas: <c>«Синов муддатидан ўтмади. Ишдан кетди»</c> — 143 qator,
    /// <c>«Синов муддатидан ўта олмади. Ишдан кетди»</c> — 118 qator.
    /// Ikkinchisini ro'yxatga qo'shmasa, 118 odamning sinov muddati
    /// «davom etmoqda» bo'lib qolardi.
    /// </para>
    /// <para>
    /// Ro'yxat <see cref="Fold"/> dan o'tgan ko'rinishda yozilgan.
    /// </para>
    /// </remarks>
    private static readonly string[] FailedForms = ["утмади", "ута олмади"];

    /// <summary>
    /// Ijobiy natijaning yozilishlari.
    /// </summary>
    /// <remarks>
    /// <c>«яхши»</c> alohida turadi, chunki 61 qatorda natija so'zsiz,
    /// faqat baho bilan yozilgan: <c>«Ишга вақтида келиб яхши ишлаяпти»</c>.
    /// Excel'dagi 1172 ta xulosaning birortasida ham salbiy va ijobiy
    /// so'zlar bir vaqtda uchramaydi — tekshirib chiqilgan.
    /// </remarks>
    private static readonly string[] PassedForms = ["утди", "яхши", "тугатилган"];

    private readonly EtlWorkspace _workspace;

    /// <summary>
    /// Shu yugurishda yaratilgan sinov muddatlari: mehnat munosabati → yozuv.
    /// </summary>
    /// <remarks>
    /// <see cref="EtlWorkspace.HasProbation"/> faqat «bormi» degan savolga
    /// javob beradi, yozuvning o'zini qaytarmaydi. Asosiy varaqdagi
    /// <c>«Тугатилган»</c> katagi esa 91.5 dan kelgan yozuvni <b>yakunlashi</b>
    /// kerak — buning uchun obyektning o'zi kerak.
    /// </remarks>
    private readonly Dictionary<Guid, Probation> _created = [];

    public ProbationsStep(EtlWorkspace workspace) => _workspace = workspace;

    /// <summary>Uchala manbani ko'chiradi.</summary>
    public void Run()
    {
        var summary = _workspace.Report.Step(Name);

        ReadProbationSheet(summary);
        ReadEmployeeSheet(EmployeeSheet.Main, summary);
        ReadEmployeeSheet(EmployeeSheet.NewHires, summary);

        // Varaqlar o'qilgandan KEYIN: solishtirish shu yugurishda
        // yaratilgan yozuvlarni ham, bazadan yuklanganlarini ham
        // birga ko'rishi kerak.
        Reconcile(summary);
    }

    // ────────────────────────────────────────────────────────────
    //  Holatni solishtirish
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Munosabat holatini sinov natijasiga moslashtiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Qanday xato tuzatiladi.</b> Bu qadam sinov natijasini domen
    /// obyektiga to'g'ridan-to'g'ri yozadi (<c>Probation.Resolve</c>) va
    /// shu bilan MUNOSABATNI unutardi. Tizimdagi odatiy oqim esa ikkalasini
    /// birga bajaradi: <c>ResolveProbationCommandHandler</c> sinovni
    /// yakunlaydi VA <c>employment.CompleteProbation()</c> ni chaqiradi.
    /// Natijada bazada 379 ta odam «sinovda» bo'lib qolgan edi — sinovi
    /// allaqachon «o'tdi» bo'lsa ham. Bosh sahifa esa «Faol xodimlar: 30»
    /// deb ko'rsatardi.
    /// </para>
    /// <para>
    /// <b>Nega tuzatish ALOHIDA yurish, yozish joyida emas.</b> Xato
    /// allaqachon bazada. Faqat yozish joyini tuzatsak, yangi yugurish
    /// to'g'ri ishlardi-yu, mavjud 379 yozuv abadiy qotib qolardi:
    /// interfeysda ularni tuzatadigan yo'l YO'Q (sinov yakunlangani uchun
    /// «sinov» ekrani ularni ko'rsatmaydi, <c>Resolve</c> esa
    /// «allaqachon yakunlangan» xatosini beradi). Bu yurish esa
    /// idempotent: har safar faqat mos kelmaydiganini tuzatadi.
    /// </para>
    /// <para>
    /// <b>Faqat «o'tdi» avtomatik tuzatiladi.</b> «O'tmadi» yoki «o'zi
    /// ketdi» bo'lsa odam ishdan bo'shatilgan bo'lishi kerak, lekin
    /// bo'shatish yozuvi varaqda yo'q — bu HR qarori, ETL taxmini emas.
    /// Ular hisobotga tushadi.
    /// </para>
    /// </remarks>
    private void Reconcile(StepSummary summary)
    {
        foreach (var employment in _workspace.AllEmployments())
        {
            if (employment.Status != EmploymentStatus.Probation)
            {
                continue;
            }

            var probations = _workspace.ProbationsOf(employment.Id);

            if (probations.Count == 0)
            {
                // Sinov yozuvi umuman yo'q — bu boshqa holat va uning
                // o'z ekrani bor («sinov» sahifasidagi «yozuvsizlar»
                // bloki, `ListMissingProbationsAsync`). HR u yerda
                // bir bosishda tasdiqlaydi, ETL aralashmaydi.
                continue;
            }

            var latest = Latest(probations);

            if (latest.Status == ProbationStatus.InProgress)
            {
                continue;
            }

            if (latest.Status != ProbationStatus.Passed)
            {
                _workspace.Report.Add(Name, SheetName, 0, string.Empty,
                    $"sinov «{latest.Status}» bilan yakunlangan, lekin xodim hali sinovda — HR ko'rib chiqsin",
                    latest.Conclusion, EtlAction.Flagged);
                summary.NeedsReview++;
                continue;
            }

            var completed = employment.CompleteProbation(
                latest.ResolvedAt ?? latest.EndDate,
                latest.Conclusion);

            if (completed.IsFailure)
            {
                _workspace.Report.Add(Name, SheetName, 0, string.Empty,
                    "sinov natijasi munosabatga qo'llanmadi", completed.Error.Message, EtlAction.Flagged);
                summary.NeedsReview++;
                continue;
            }

            summary.Updated++;
        }
    }

    /// <summary>Eng oxirgi sinov muddati — tugash sanasi bo'yicha.</summary>
    /// <remarks>
    /// Bitta munosabatda bir nechta sinov bo'lishi mumkin (muddat
    /// uzaytirilgan yoki qayta ochilgan). Holatni ULARNING ENG
    /// OXIRGISI belgilaydi: eskisi allaqachon o'tgan tarix.
    /// </remarks>
    private static Probation Latest(IReadOnlyList<Probation> probations)
    {
        var latest = probations[0];

        for (var i = 1; i < probations.Count; i++)
        {
            if (probations[i].EndDate > latest.EndDate)
            {
                latest = probations[i];
            }
        }

        return latest;
    }

    // ────────────────────────────────────────────────────────────
    //  «91.5 Синов муддатидаги ходимлар»
    // ────────────────────────────────────────────────────────────

    private void ReadProbationSheet(StepSummary summary)
    {
        var rows = _workspace.Workbook.Rows(SheetName);

        if (rows.Count == 0)
        {
            _workspace.Report.Add(Name, SheetName, 0, string.Empty,
                "varaq topilmadi", SheetName, EtlAction.RowSkipped);
            return;
        }

        var processed = 0;

        foreach (var row in rows)
        {
            if (row.Number < FirstDataRow)
            {
                continue;
            }

            // Oy sarlavhasi («Март ойи», «ИЮЛЬ», «Январ 2026») 1- yoki
            // 2-ustunda yozilgan, lekin 3-ustuni doim bo'sh. Varaqdagi 27 ta
            // shunday qator va 525 ta butunlay bo'sh qator shu bitta
            // tekshiruv bilan ajraladi — hammasi tekshirib chiqilgan.
            if (row[HireDateColumn].IsEmpty)
            {
                continue;
            }

            if (_workspace.Options.Limit is { } limit && processed >= limit)
            {
                break;
            }

            processed++;
            summary.Read++;

            ReadProbationRow(row, summary);
        }
    }

    private void ReadProbationRow(ExcelRow row, StepSummary summary)
    {
        var context = new RowContext(Name, SheetName, row.Number, string.Empty);

        // Sanasi va xulosasi bor, lekin ismi yo'q qator (varaqda ikkitasi:
        // 659 va 1259). Kimga tegishli ekani noma'lum — taxmin qilinmaydi.
        if (row[FullNameColumn].IsEmpty)
        {
            _workspace.Report.Add(Name, SheetName, row.Number, string.Empty,
                "F.I.Sh ustuni bo'sh", row.Text(ConclusionColumn), EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        // Xodim FAQAT ism bo'yicha qidiriladi. 3-ustun tug'ilgan sana emas,
        // ishga kirgan sana — uni qidiruvga berish har bir qatorni
        // «tug'ilgan sana mos emas» holatiga tushirardi.
        var lookup = _workspace.FindEmployee(context, row[FullNameColumn]);

        if (lookup.Employee is not { } employee)
        {
            // Sabab allaqachon FindEmployee tomonidan hisobotga yozilgan.
            summary.Skipped++;
            return;
        }

        context = context.For(lookup.Label);

        if (_workspace.Date(context, row[HireDateColumn], "ishga kirgan sana") is not { } startDate)
        {
            _workspace.Report.Add(Name, SheetName, row.Number, context.Employee,
                "ishga kirgan sana o'qilmadi — mehnat munosabati aniqlanmadi",
                row.Text(HireDateColumn), EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var employment = FindEmployment(context, employee.Id, startDate);

        if (employment is null)
        {
            _workspace.Report.Add(Name, SheetName, row.Number, context.Employee,
                "mehnat munosabati topilmadi", row.Text(HireDateColumn), EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        if (_workspace.HasProbation(employment.Id))
        {
            _workspace.Report.Add(Name, SheetName, row.Number, context.Employee,
                "sinov muddati allaqachon yozilgan", row.Text(EndDateColumn), EtlAction.AlreadyExists);
            summary.Updated++;
            return;
        }

        if (_workspace.Date(context, row[EndDateColumn], "sinov muddati tugash sanasi") is not { } endDate)
        {
            // Tugash sanasisiz sinov muddati ma'nosiz: na 7 kunlik eslatma
            // yuborib bo'ladi, na natija qachon chiqarilganini yozib bo'ladi.
            _workspace.Report.Add(Name, SheetName, row.Number, context.Employee,
                "sinov muddati tugash sanasi yo'q", row.Text(EndDateColumn), EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var imported = Probation.Import(employment.Id, startDate, endDate);

        if (imported.IsFailure)
        {
            _workspace.Report.Add(Name, SheetName, row.Number, context.Employee,
                "sinov muddati sanalari qabul qilinmadi", imported.Error.Message, EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var probation = imported.Value;

        Resolve(context, probation, row.Text(ConclusionColumn), endDate);

        _workspace.Register(probation);
        _workspace.Database.Add(probation);
        _created[employment.Id] = probation;
        summary.Created++;
    }

    // ────────────────────────────────────────────────────────────
    //  Xodim varaqlaridagi sinov muddati ustuni
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Xodim varag'ining sinov muddati ustunini o'qiydi.
    /// </summary>
    /// <remarks>
    /// <see cref="StepSummary.Read"/> bu yerda <b>qatorlarni emas, to'ldirilgan
    /// kataklarni</b> sanaydi. Aks holda jamlanmada «2000 qator o'qildi»
    /// chiqardi, holbuki ularning ko'pchiligida sinov muddati haqida
    /// umuman ma'lumot yo'q.
    /// </remarks>
    private void ReadEmployeeSheet(EmployeeSheet sheet, StepSummary summary)
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
            if (row.Number < sheet.FirstDataRow)
            {
                continue;
            }

            var nameCell = row[sheet.FullName];

            if (nameCell.IsEmpty)
            {
                continue;
            }

            // Ikki xil sarlavha qatori bor: asosiy varaqdagi 49 ta blok
            // sarlavhasi (ismi bor, lavozimi yo'q) va «91.2» ichida 9 marta
            // takrorlanadigan ustun sarlavhasi («Ф.И.О» — bitta so'z).
            // Ikkalasi ham xodim emas. Bu yerda hisobotga yozilmaydi:
            // ism ajratilmagani haqida xodimlar qadami allaqachon xabar
            // beradi, ikkinchi marta takrorlash shovqin bo'lardi.
            if ((sheet.HasBlockHeaders && !row.Has(sheet.Position))
                || FullName.Parse(nameCell.Text).IsFailure)
            {
                continue;
            }

            var parsed = ProbationCell.Parse(row[sheet.Probation]);

            if (parsed.Kind == ProbationCellKind.Empty)
            {
                continue;
            }

            if (_workspace.Options.Limit is { } limit && processed >= limit)
            {
                break;
            }

            processed++;
            summary.Read++;

            ReadEmployeeRow(sheet, row, parsed, summary);
        }
    }

    private void ReadEmployeeRow(
        EmployeeSheet sheet,
        ExcelRow row,
        ProbationCellResult parsed,
        StepSummary summary)
    {
        var context = new RowContext(Name, sheet.Name, row.Number, row.Text(sheet.FullName));

        if (parsed.Kind == ProbationCellKind.Unknown)
        {
            // Katakda sinov muddati emas, boshqa narsa: «Синовсиз» (21 ta),
            // «2 ой» (muddat uzunligi, sana emas), «29.02.2026» (mavjud
            // bo'lmagan kun), «26.02.2026..» (imlo xatosi). Xodimni qidirish
            // ham shart emas — bu katakdan sinov muddati chiqmaydi.
            _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                "sinov muddati katagi tushunilmadi", parsed.Raw, EtlAction.Ignored);
            summary.Skipped++;
            return;
        }

        var lookup = _workspace.FindEmployee(
            context,
            row[sheet.FullName],
            row[sheet.BirthDate],
            row[sheet.Pinfl],
            row[sheet.Passport]);

        if (lookup.Employee is not { } employee)
        {
            summary.Skipped++;
            return;
        }

        context = context.For(lookup.Label);

        if (_workspace.Date(context, row[sheet.HireDate], "ishga kirgan sana") is not { } hireDate)
        {
            _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                "ishga kirgan sana o'qilmadi — mehnat munosabati aniqlanmadi",
                row.Text(sheet.HireDate), EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var employment = FindEmployment(context, employee.Id, hireDate);

        if (employment is null)
        {
            _workspace.Report.Add(Name, sheet.Name, row.Number, context.Employee,
                "mehnat munosabati topilmadi", row.Text(sheet.HireDate), EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        if (parsed.Kind == ProbationCellKind.Completed)
        {
            Complete(context, employment, parsed.Raw, summary);
        }
        else if (parsed.EndDate is { } endDate)
        {
            Create(context, employment, endDate, parsed.Raw, summary);
        }
    }

    /// <summary>
    /// Sana ko'rsatilgan katakdan yangi sinov muddati yaratadi.
    /// </summary>
    /// <remarks>
    /// Boshlanish sanasi sifatida mehnat munosabatining ishga qabul sanasi
    /// olinadi. Bu taxmin emas: bu varaqlarda sinov muddatining boshlanishi
    /// alohida yozilmagan, chunki u ta'rifi bo'yicha ishga qabul kunidan
    /// boshlanadi. Xulosa esa bu yerda yo'q — muddat ochiq qoladi va uni
    /// HR tizimning o'zida yopadi.
    /// </remarks>
    private void Create(
        RowContext context,
        Employment employment,
        DateOnly endDate,
        string raw,
        StepSummary summary)
    {
        if (_workspace.HasProbation(employment.Id))
        {
            _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                "sinov muddati allaqachon yozilgan", raw, EtlAction.AlreadyExists);
            summary.Updated++;
            return;
        }

        var imported = Probation.Import(employment.Id, employment.HireDate, endDate);

        if (imported.IsFailure)
        {
            // Odatda tugash sanasi ishga qabul sanasidan oldin: ustun
            // siljigan yoki sana xato kiritilgan. Taxmin bilan tuzatilmaydi.
            _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                "sinov muddati sanalari qabul qilinmadi", imported.Error.Message, EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var probation = imported.Value;

        _workspace.Register(probation);
        _workspace.Database.Add(probation);
        _created[employment.Id] = probation;
        summary.Created++;
    }

    /// <summary>
    /// <c>«Тугатилган»</c> katagini qayta ishlaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bu so'z bitta narsani bildiradi: sinov muddati tugagan. Qachon
    /// boshlangani ham, qachon tugagani ham katakda yo'q. Shu sababli yozuv
    /// <b>yaratilmaydi</b> — sanasi o'ylab topilgan sinov muddati yolg'on
    /// ma'lumot bo'lardi.
    /// </para>
    /// <para>
    /// Asosiy varaqdagi 411 ta <c>«Тугатилган»</c> katakning 211 tasi 91.5
    /// varag'ida ham bor va o'sha yerdan sanasi bilan ko'chiriladi. Qolgan
    /// 200 tasi hisobotga tushadi: bu yo'qotish emas, HR uchun aniq ish
    /// ro'yxati — «bu odamlarning sinov muddati tugagan, sanasini toping».
    /// </para>
    /// <para>
    /// 91.5 dan kelgan yozuv allaqachon yakunlangan bo'lsa tegilmaydi:
    /// u yerdagi to'liq xulosa matni bu yerdagi bitta so'zdan boy.
    /// </para>
    /// </remarks>
    private void Complete(RowContext context, Employment employment, string raw, StepSummary summary)
    {
        if (_created.TryGetValue(employment.Id, out var probation))
        {
            if (probation.Status != ProbationStatus.InProgress)
            {
                return;
            }

            // Yakunlangan sana sifatida muddatning o'zi tugagan kun olinadi:
            // «Тугатилган» aynan shu kunni nazarda tutadi.
            var resolved = probation.Resolve(ProbationStatus.Passed, at: probation.EndDate, conclusion: raw);

            if (resolved.IsFailure)
            {
                _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                    "sinov muddati yakunlanmadi", resolved.Error.Message, EtlAction.LeftEmpty);
                return;
            }

            summary.Updated++;
            return;
        }

        if (_workspace.HasProbation(employment.Id))
        {
            // Yozuv bazadan kelgan — oldingi yugurishda ko'chirilgan va
            // natijasi ham o'shanda qo'yilgan. Ustidan yozilmaydi.
            _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                "sinov muddati allaqachon yozilgan", raw, EtlAction.AlreadyExists);
            summary.Updated++;
            return;
        }

        _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
            "sinov muddati tugagan, lekin sanalari noma'lum — yozuv yaratilmadi",
            raw, EtlAction.LeftEmpty);
        summary.Skipped++;
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Sinov muddati qaysi mehnat munosabatiga tegishli ekanini aniqlaydi.
    /// </summary>
    /// <remarks>
    /// Avval <b>aynan shu kunda boshlangan</b> munosabat izlanadi: varaqdagi
    /// sana ishga kirish sanasi, ya'ni u munosabatning boshlanishi bilan bir
    /// xil bo'lishi kerak. Topilmasa (sana bir-ikki kunga farq qilishi yoki
    /// odam ketib-qaytib kelgan bo'lishi mumkin) zaxira yo'l ishlaydi, lekin
    /// bu <b>taxmin</b> va hisobotga tushadi — HR uni ko'radi.
    /// </remarks>
    private Employment? FindEmployment(RowContext context, Guid employeeId, DateOnly hireDate)
    {
        if (_workspace.EmploymentStartedOn(employeeId, hireDate) is { } started)
        {
            return started;
        }

        var employment = _workspace.EmploymentOn(employeeId, hireDate, out var covers);

        if (employment is not null)
        {
            _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                covers
                    ? "shu sanada boshlangan mehnat munosabati yo'q — o'sha kunda kuchda bo'lgani olindi"
                    : "shu sanada mehnat munosabati yo'q — eng yaqini olindi",
                hireDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                EtlAction.Flagged);
        }

        return employment;
    }

    /// <summary>
    /// Xulosa matnidan natijani aniqlaydi va sinov muddatini yakunlaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Matn tanilmasa muddat <b>ataylab ochiq</b> qoldiriladi. Domen
    /// yakunlangan muddatning natijasini keyin o'zgartirishga ruxsat
    /// bermaydi (<c>Probation.Resolve</c> ikkinchi marta xato qaytaradi),
    /// shu sababli noto'g'ri natija qo'yish bo'sh qoldirishdan yomonroq.
    /// </para>
    /// <para>
    /// Tanilmagan matn hisobotning «qiymat» ustunida saqlanadi. Boshqa yo'l
    /// yo'q: domenda <c>Conclusion</c> faqat <c>Resolve</c> ichida
    /// to'ldiriladi, ya'ni yakunlanmagan muddatga izoh yozib bo'lmaydi.
    /// Matn HR ko'zidan qochmasligi kerak — natijani u o'zi qo'yadi.
    /// </para>
    /// </remarks>
    private void Resolve(RowContext context, Probation probation, string conclusion, DateOnly at)
    {
        if (conclusion.Length == 0)
        {
            // 53 qatorda xulosa yozilmagan. Bu xato emas — muddat hali davom
            // etayotgan bo'lishi mumkin, shu sababli hisobotga ham tushmaydi.
            return;
        }

        if (Outcome(conclusion) is not { } status)
        {
            _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                "xulosa matni tanilmadi — sinov muddati ochiq qoldirildi",
                conclusion, EtlAction.LeftEmpty);
            return;
        }

        var resolved = probation.Resolve(status, at: at, conclusion: conclusion);

        if (resolved.IsFailure)
        {
            _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                "sinov muddati yakunlanmadi", resolved.Error.Message, EtlAction.LeftEmpty);
        }
    }

    /// <summary>
    /// Xulosa matnidan sinov muddati natijasini aniqlaydi.
    /// </summary>
    /// <remarks>
    /// Salbiy shakl <b>birinchi</b> sinaladi: <c>«Синов муддатидан ўта
    /// олмади. Ойлик маош камлик қилди»</c> kabi matnda ijobiy so'z ham
    /// uchrashi mumkin, natija esa baribir salbiy.
    /// </remarks>
    private static ProbationStatus? Outcome(string conclusion)
    {
        var key = Fold(conclusion);

        foreach (var form in FailedForms)
        {
            if (key.Contains(form, StringComparison.Ordinal))
            {
                return ProbationStatus.Failed;
            }
        }

        foreach (var form in PassedForms)
        {
            if (key.Contains(form, StringComparison.Ordinal))
            {
                return ProbationStatus.Passed;
            }
        }

        return null;
    }

    /// <summary>
    /// Kirill imlo variantlarini bir shaklga keltiradi.
    /// </summary>
    /// <remarks>
    /// <see cref="ProbationCell"/> dagi bilan bir xil qoida, lekin u yerda
    /// yopiq: o'sha sinf bitta katakni o'qiydi, bu yerda esa erkin matn
    /// tahlil qilinadi. Ikki vazifani bitta ochiq yordamchiga birlashtirish
    /// ularni bir-biriga bog'lab qo'yardi.
    /// </remarks>
    private static string Fold(string value)
    {
        var lowered = AliasTable.Key(value);
        return lowered
            .Replace('ў', 'у')
            .Replace('қ', 'к')
            .Replace('ғ', 'г')
            .Replace('ҳ', 'х');
    }
}
