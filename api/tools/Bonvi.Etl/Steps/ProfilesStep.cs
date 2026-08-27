using System.Globalization;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Etl.Excel;
using Bonvi.Etl.Mapping;
using Bonvi.Etl.Reporting;
using Bonvi.Etl.Runtime;

namespace Bonvi.Etl.Steps;

/// <summary>
/// 6-qadam: xodim profilini boyitadi — qobiliyatlar va rag'batlar.
/// </summary>
/// <remarks>
/// <para>
/// Ikkita varaq, ikki xil yozuv:
/// </para>
/// <list type="bullet">
///   <item><c>«91.15 Қобилятлар жадвали»</c> → <see cref="EmployeeCompetency"/> —
///         xodimga BITTA profil, tarix emas: yangi ma'lumot mavjudini
///         to'ldiradi;</item>
///   <item><c>«91.14 Тақдирланган ходимлар»</c> → <see cref="Incentive"/> —
///         har bir rag'bat alohida fakt, ya'ni tarix.</item>
/// </list>
/// <para>
/// <b>Jarima varag'i YO'Q.</b> Bloknotdagi 20 varaqning birortasi ham jarima
/// ro'yxati emas (<c>«Бахтсиз ходисалар руйхати»</c> — baxtsiz hodisalar,
/// butunlay boshqa narsa). Shu sababli bu qadam
/// <see cref="IncentiveKind.Penalty"/> yozuvini <b>hech qachon
/// yaratmaydi</b>. Mavjud bo'lmagan varaqni «ehtiyot uchun» o'qiydigan kod
/// yozilmadi: u keyingi o'quvchini «demak jarimalar ham ko'chirilgan» degan
/// noto'g'ri xulosaga olib kelardi. Jarimani HR tizimning o'zida kiritadi.
/// </para>
/// <para>
/// <b>Nega bu qadam eng oxirida.</b> Ikkala varaq ham faqat mavjud odam
/// haqida gapiradi va yangi xodim yaratmaydi — ular xodimlar qadami
/// tugagandan keyingina to'g'ri bog'lanadi.
/// </para>
/// </remarks>
public sealed class ProfilesStep
{
    /// <summary>Qadam nomi (<c>--steps</c> uchun).</summary>
    public const string Name = "profiles";

    private readonly EtlWorkspace _workspace;

    public ProfilesStep(EtlWorkspace workspace) => _workspace = workspace;

    /// <summary>Ikkala varaqni ko'chiradi.</summary>
    /// <remarks>
    /// Tartib ahamiyatsiz: qobiliyat profili va rag'bat bir-biriga bog'liq
    /// emas, ikkalasi ham to'g'ridan-to'g'ri xodimga yoziladi.
    /// </remarks>
    public void Run()
    {
        var summary = _workspace.Report.Step(Name);

        ReadCompetencies(summary);
        ReadAwards(summary);
    }

    // ────────────────────────────────────────────────────────────
    //  91.15 — qobiliyatlar jadvali
    // ────────────────────────────────────────────────────────────

    /// <summary>«91.15 Қобилятлар жадвали» varag'ining ustun tartibi.</summary>
    /// <remarks>
    /// Varaq nomidagi <c>«Қобилятлар»</c> — HR ning imlo xatosi
    /// (<c>«Қобилиятлар»</c> bo'lishi kerak edi). U ATAYLAB tuzatilmagan:
    /// varaq Excel'da qanday nomlangan bo'lsa, shundayligicha izlanadi.
    /// </remarks>
    private static class Competencies
    {
        public const string Sheet = "91.15 Қобилятлар жадвали";

        /// <summary>1-qator — jadval sarlavhasi, 2-qator — ustun nomlari.</summary>
        public const int FirstDataRow = 3;

        public const int Number = 1;
        public const int FullName = 2;
        public const int BirthDate = 3;
        public const int Position = 4;
        public const int Languages = 5;
        public const int ComputerLiteracy = 6;
        public const int Specialization = 7;
        public const int OtherAbilities = 8;
        public const int Interest = 9;
        public const int EducationPlace = 10;

        /// <summary>
        /// Profilga tushadigan ustunlar — «ko'chiradigan narsa bormi» tekshiruvi uchun.
        /// </summary>
        /// <remarks>
        /// Bugungi bloknotda ulardan faqat uchtasi to'ldirilgan: til (33
        /// katak), mutaxassislik (35) va ta'lim joyi (36). Kompyuter
        /// savodxonligi, «Бошка қобилиятлар» va «Қизикиши нимада» ustunlari
        /// <b>butunlay bo'sh</b>. Ular baribir o'qiladi: ustun bo'sh bo'lishi
        /// «ma'lumot yo'q» emas, «HR hozircha yozmagan» degani, va u
        /// ko'chirishdan oldin to'ldirilishi mumkin.
        /// </remarks>
        public static readonly int[] Fields =
            [Languages, ComputerLiteracy, Specialization, OtherAbilities, Interest, EducationPlace];
    }

    /// <summary>
    /// Til katagining ishonchli deb qabul qilinadigan eng katta uzunligi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Til bilishi qisqa yoziladi: varaqdagi eng uzun haqiqiy qiymat
    /// <c>«Рус тили ва Инглиз тили»</c> — 23 belgi. 213-qatorda esa shu
    /// ustunda butunlay boshqa narsa turibdi: <c>«Топ кадр хисобланади,
    /// берилган вазифани чин дилдан килади, шунчаки эмас»</c> — bu til emas,
    /// xodimga berilgan tavsif.
    /// </para>
    /// <para>
    /// Qiymat baribir yoziladi (uni tashlash — ma'lumot yo'qotish), lekin
    /// hisobotga tushadi va HR uni <c>«Бошка қобилиятлар»</c> ga o'zi
    /// ko'chiradi. <b>NEGA avtomatik ko'chirilmaydi:</b> «uzun matn — demak
    /// tavsif» taxmin, va ETL taxmin bo'yicha ma'lumotni bir maydondan
    /// ikkinchisiga surib qo'ymaydi.
    /// </para>
    /// <para>
    /// <b>NEGA «ичида „тил“ сўзи борми» deb tekshirilmaydi:</b> HR ertaga
    /// <c>«Английский»</c> yoki <c>«O'zbek/Rus»</c> deb yozishi mumkin va
    /// bunday qoida haqiqiy ma'lumotni jimgina yo'qotardi. Uzunlik esa
    /// qiymatning ma'nosiga emas, shakliga qaraydi.
    /// </para>
    /// </remarks>
    private const int MaxPlausibleLanguageLength = 60;

    private void ReadCompetencies(StepSummary summary)
    {
        var rows = _workspace.Workbook.Rows(Competencies.Sheet);

        if (rows.Count == 0)
        {
            _workspace.Report.Add(Name, Competencies.Sheet, 0, string.Empty,
                "varaq topilmadi", Competencies.Sheet, EtlAction.RowSkipped);
            return;
        }

        var processed = 0;

        foreach (var row in rows)
        {
            if (row.Number < Competencies.FirstDataRow || row[Competencies.FullName].IsEmpty)
            {
                continue;
            }

            // Bo'lim sarlavhasi: «1-ДЕПАРТАМЕНТ БЎЛИМИ ( 6 ходим )». Varaqda
            // ularning 46 tasi bor va ular AYNAN F.I.Sh ustunida yotadi —
            // ya'ni «ismi bormi» tekshiruvi ularni ajratmaydi. Yagona
            // ishonchli farq: sarlavhada na tartib raqami, na lavozim bor,
            // 509 ta xodim qatorining hammasida esa ikkalasi ham bor
            // (tekshirib chiqilgan).
            //
            // Ikkala ustun BIRGA tekshiriladi, bittasi emas: tartib raqami
            // yoki lavozimi tushib qolgan haqiqiy xodim qatori baribir
            // o'qilsin.
            if (row[Competencies.Number].IsEmpty && row[Competencies.Position].IsEmpty)
            {
                continue;
            }

            // Qobiliyat ustunlari ATAYLAB xodim qidirishdan ham, `Read`
            // sanog'idan ham OLDIN tekshiriladi. Varaq — butun korxonaning
            // ro'yxati (509 qator), lekin qobiliyat ustunlaridan birortasi
            // to'ldirilgani atigi 43 qatorda. Qolgan 466 qatorda ko'chiradigan
            // hech narsa yo'q: bo'sh profil — yozuv emas. Ularni «o'qildi»
            // yoki «tashlandi» deb sanash jamlanmani ma'nosiz qilardi, xodim
            // qidirish esa hisobotga 466 ta soxta «xodim topilmadi» satri
            // qo'shardi.
            if (!Array.Exists(Competencies.Fields, column => row[column].HasValue))
            {
                continue;
            }

            if (_workspace.Options.Limit is { } limit && processed >= limit)
            {
                break;
            }

            processed++;
            summary.Read++;

            ReadCompetencyRow(row, summary);
        }
    }

    private void ReadCompetencyRow(ExcelRow row, StepSummary summary)
    {
        var context = new RowContext(Name, Competencies.Sheet, row.Number, row.Text(Competencies.FullName));

        // Tug'ilgan sana qidiruvga BERILADI: bu varaqda 3-ustun haqiqatan
        // tug'ilgan sana (91.14 dan farqli — u yerda ishga kirgan sana).
        // 509 qatordan 10 tasida u seriya emas, matn; ularning to'rttasi
        // buzuq («26.07..1978», «10.14.1996» — 14-oy yo'q). Bunday katak
        // `null` beradi va qidiruv faqat ismga tayanadi — bu to'g'ri, chunki
        // Excel'da xato kiritilgani AYNAN sana, ism emas.
        var lookup = _workspace.FindEmployee(
            context, row[Competencies.FullName], row[Competencies.BirthDate]);

        if (lookup.Employee is not { } employee)
        {
            // Sabab allaqachon `FindEmployee` tomonidan hisobotga yozilgan.
            summary.Skipped++;
            return;
        }

        context = context.For(lookup.Label);

        if (_workspace.CompetencyOf(employee.Id) is { } existing)
        {
            Enrich(existing, row, context, summary);
            return;
        }

        var created = EmployeeCompetency.Create(employee.Id);

        if (created.IsFailure)
        {
            _workspace.Report.Add(Name, Competencies.Sheet, row.Number, context.Employee,
                "qobiliyat profili yaratilmadi", created.Error.Message, EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var competency = created.Value;

        competency.SetLanguages(Language(row, context));
        competency.SetSpecialization(Text(row, Competencies.Specialization));
        competency.SetOtherAbilities(Text(row, Competencies.OtherAbilities));
        competency.SetInterest(Text(row, Competencies.Interest));
        competency.SetEducationPlace(Text(row, Competencies.EducationPlace));
        SetComputerLiteracy(competency, row, context);

        _workspace.Register(competency);
        _workspace.Database.Add(competency);
        summary.Created++;
    }

    /// <summary>
    /// Mavjud profilning FAQAT bo'sh maydonlarini to'ldiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Qadam nomi aynan shuni anglatadi — <b>profil boyitish</b>. To'ldirilgan
    /// maydon hech qachon qayta yozilmaydi: ETL ikkinchi marta ishga
    /// tushirilganda HR tizimda qo'lda kiritgan aniqroq ma'lumot Excel'dagi
    /// eski qiymat bilan almashtirilib ketardi. Excel — bir martalik manba,
    /// tizim esa doimiy; ziddiyatda tizimdagi qiymat ustun turadi.
    /// </para>
    /// <para>
    /// <b>NEGA hisobotga yozilmaydi:</b> bu muammo emas, kutilgan hol.
    /// Jamlanmada u <c>Updated</c> bo'lib ko'rinadi.
    /// </para>
    /// </remarks>
    private void Enrich(EmployeeCompetency competency, ExcelRow row, RowContext context, StepSummary summary)
    {
        if (competency.Languages is null)
        {
            competency.SetLanguages(Language(row, context));
        }

        if (competency.Specialization is null)
        {
            competency.SetSpecialization(Text(row, Competencies.Specialization));
        }

        if (competency.OtherAbilities is null)
        {
            competency.SetOtherAbilities(Text(row, Competencies.OtherAbilities));
        }

        if (competency.Interest is null)
        {
            competency.SetInterest(Text(row, Competencies.Interest));
        }

        if (competency.EducationPlace is null)
        {
            competency.SetEducationPlace(Text(row, Competencies.EducationPlace));
        }

        if (competency.ComputerLiteracyPercent is null)
        {
            SetComputerLiteracy(competency, row, context);
        }

        summary.Updated++;
    }

    /// <summary>Til katagini o'qiydi va shubhali qiymatni hisobotga chiqaradi.</summary>
    private string? Language(ExcelRow row, RowContext context)
    {
        var text = Text(row, Competencies.Languages);

        if (text is { Length: > MaxPlausibleLanguageLength })
        {
            _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                "til ustunida til nomi emas, tavsif — qiymat o'zgartirilmasdan yozildi",
                text, EtlAction.Flagged);
        }

        return text;
    }

    /// <summary>
    /// Kompyuter savodxonligi foizini o'qiydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Excel'da bu ustun (<c>«Компьютер саводхонлиги»</c>) <b>butunlay
    /// bo'sh</b> — 509 qatorning birortasida ham qiymat yo'q. Kod baribir
    /// yozilgan: HR ko'chirishdan oldin varaqni to'ldirib qo'yishi mumkin va
    /// bugungi bo'sh ustun «ma'lumot yo'q» emas, «hozircha yozilmagan»
    /// degani.
    /// </para>
    /// <para>
    /// <b>0 dan 1 gacha qiymat foizga aylantiriladi.</b> Excel'ning «%»
    /// formati 80 % ni katakda <c>0.8</c> qilib saqlaydi va uni butunlashtirib
    /// 1 % deb yozish ma'lumotni buzardi. Bu taxmin, lekin xavfsiz tomonga:
    /// «kompyuter savodxonligi 0,8 %» degan qiymat amalda ma'nosiz.
    /// </para>
    /// </remarks>
    private void SetComputerLiteracy(EmployeeCompetency competency, ExcelRow row, RowContext context)
    {
        var cell = row[Competencies.ComputerLiteracy];

        if (cell.IsEmpty)
        {
            return;
        }

        if (Percent(cell) is not { } percent)
        {
            _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                "kompyuter savodxonligi 0–100 oralig'idagi son emas", cell.Text, EtlAction.LeftEmpty);
            return;
        }

        // Domen tekshiruvi baribir chaqiriladi, garchi `Percent` chegarani
        // allaqachon tekshirgan bo'lsa ham: chegara qoidasi domenniki va u
        // o'zgarsa, bu yerdagi nusxa emas, aynan u ishlashi kerak.
        if (competency.SetComputerLiteracy(percent).IsFailure)
        {
            _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                "kompyuter savodxonligi qabul qilinmadi", cell.Text, EtlAction.LeftEmpty);
        }
    }

    /// <summary>Katakdan foizni ajratadi: <c>80</c>, <c>«80%»</c> yoki <c>0.8</c>.</summary>
    private static int? Percent(ExcelCell cell)
    {
        var number = cell.Number;

        if (number is null)
        {
            var text = cell.Text.TrimEnd('%').Trim();

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return null;
            }

            number = parsed;
        }

        var value = number.Value;

        // Excel'ning foiz formati: 0 < x < 1 → aslida x·100 foiz.
        if (value is > 0 and < 1)
        {
            value *= 100;
        }

        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);

        return rounded is < 0 or > EmployeeCompetency.MaxComputerLiteracyPercent ? null : rounded;
    }

    /// <summary>Ustundagi matn (bo'sh bo'lsa <c>null</c>).</summary>
    private static string? Text(ExcelRow row, int column)
    {
        var text = row.Text(column);
        return text.Length == 0 ? null : text;
    }

    // ────────────────────────────────────────────────────────────
    //  91.14 — taqdirlangan xodimlar
    // ────────────────────────────────────────────────────────────

    /// <summary>«91.14 Тақдирланган ходимлар» varag'ining ustun tartibi.</summary>
    /// <remarks>
    /// 3-ustun — <b>ishga kelgan sana</b>, tug'ilgan sana emas. U qidiruvga
    /// berilmaydi (<see cref="ReadAwardRow"/> izohiga qarang) va yozuvga ham
    /// tushmaydi: mehnat munosabati allaqachon 2-qadamda yaratilgan.
    /// 4-ustun (bo'lim) va 5-ustun (lavozim) ham shu sababli o'qilmaydi.
    /// </remarks>
    private static class Awards
    {
        public const string Sheet = "91.14 Тақдирланган ходимлар";

        /// <summary>1-qator — jadval sarlavhasi, 2-qator — ustun nomlari.</summary>
        public const int FirstDataRow = 3;

        public const int FullName = 2;
        public const int Type = 6;
        public const int Expense = 7;
        public const int Gift = 8;
        public const int Date = 9;
    }

    private void ReadAwards(StepSummary summary)
    {
        var rows = _workspace.Workbook.Rows(Awards.Sheet);

        if (rows.Count == 0)
        {
            _workspace.Report.Add(Name, Awards.Sheet, 0, string.Empty,
                "varaq topilmadi", Awards.Sheet, EtlAction.RowSkipped);
            return;
        }

        var processed = 0;

        foreach (var row in rows)
        {
            // Yil sarlavhasi («2026 йил», 4-ustunda) va faqat tartib raqami
            // yozilgan bo'sh qatorlar (varaqda 5 tasi — kelasi yil uchun
            // oldindan tayyorlab qo'yilgan) shu bitta tekshiruv bilan
            // ajraladi: ikkalasida ham F.I.Sh yo'q. Bu xato emas, varaqning
            // tuzilishi — hisobotga yozilmaydi.
            if (row.Number < Awards.FirstDataRow || row[Awards.FullName].IsEmpty)
            {
                continue;
            }

            if (_workspace.Options.Limit is { } limit && processed >= limit)
            {
                break;
            }

            processed++;
            summary.Read++;

            ReadAwardRow(row, summary);
        }
    }

    private void ReadAwardRow(ExcelRow row, StepSummary summary)
    {
        var context = new RowContext(Name, Awards.Sheet, row.Number, row.Text(Awards.FullName));

        // Xodim FAQAT ism bo'yicha qidiriladi — xuddi belgilar qadamidagidek.
        // 3-ustun bu yerda ishga kelgan sana, uni tug'ilgan sana o'rniga
        // berish har bir qatorni «tug'ilgan sana mos emas» holatiga
        // tushirardi va hisobot 33 ta yolg'on ogohlantirishga to'lardi.
        var lookup = _workspace.FindEmployee(context, row[Awards.FullName]);

        if (lookup.Employee is not { } employee)
        {
            summary.Skipped++;
            return;
        }

        context = context.For(lookup.Label);

        // Sana rag'batning yagona ajratuvchi belgisi (buyruq ustuni varaqda
        // umuman yo'q), shu sababli usiz yozuv yaratilmaydi: «qachonligi
        // noma'lum rag'bat» idempotentlik kalitiga tushmasdi va har bir
        // yugurishda takrorlanardi.
        if (_workspace.Date(context, row[Awards.Date], "rag'bat sanasi") is not { } date)
        {
            _workspace.Report.Add(Name, Awards.Sheet, row.Number, context.Employee,
                "rag'bat sanasi topilmadi", row.Text(Awards.Date), EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        if (_workspace.AwardType(context, row[Awards.Type]) is not { } type)
        {
            // Varaqda bunday bitta qator bor (44-qator): xodim, bo'lim,
            // lavozim va sana yozilgan, lekin nima uchun taqdirlangani
            // yozilmagan. Turni taxmin qilib bo'lmaydi — u summa qoidasini
            // ham belgilaydi.
            _workspace.Report.Add(Name, Awards.Sheet, row.Number, context.Employee,
                "rag'bat turi ko'rsatilmagan", row.Text(Awards.Type), EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var amount = Amount(row, context, type);

        if (type.HasAmount && amount is null)
        {
            // Pul mukofoti summasiz yozilmaydi — domen shuni talab qiladi va
            // u haq: «qancha berilgani» noma'lum bo'lsa, yozuvning butun
            // ma'nosi yo'qoladi. Excel'da hozircha bunday qator yo'q (ikkala
            // tur ham pulsiz), lekin HR varaqni to'ldirgach paydo bo'lishi
            // mumkin — o'shanda dastur yiqilmasligi, qatorni hisobotga
            // chiqarishi kerak.
            _workspace.Report.Add(Name, Awards.Sheet, row.Number, context.Employee,
                "bu tur pul bilan bog'liq, lekin summa o'qilmadi", row.Text(Awards.Expense),
                EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        if (_workspace.HasIncentive(employee.Id, IncentiveKind.Award, type.Id, date))
        {
            _workspace.Report.Add(Name, Awards.Sheet, row.Number, context.Employee,
                "rag'bat allaqachon yozilgan",
                date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                EtlAction.AlreadyExists);
            summary.Updated++;
            return;
        }

        var granted = Incentive.Grant(
            employee.Id,
            IncentiveKind.Award,
            type.Id,
            date,
            type.HasAmount,
            amount,
            giftDescription: Text(row, Awards.Gift),

            // Sabab sifatida 6-ustunning XOM matni saqlanadi. Kanonik tur
            // («Ташаккурнома») HR yozgan nozik ma'noni yo'qotadi: «Йил
            // якунида ташаккурнома берилди» — nima uchun berilganini aynan
            // shu jumla aytadi, ma'lumotnoma esa faqat nima berilganini.
            reason: Text(row, Awards.Type));

        if (granted.IsFailure)
        {
            // Bu yerga domen qoidasining har qanday buzilishi tushadi —
            // pulsiz turga tushib qolgan summa, jarimada sovg'a, sababsiz
            // jarima. Qator tashlanadi va hisobotga chiqadi; dastur
            // to'xtamaydi, chunki bitta buzuq qator qolgan 33 tasini
            // ko'chirishga to'sqinlik qilmasligi kerak.
            _workspace.Report.Add(Name, Awards.Sheet, row.Number, context.Employee,
                "rag'bat yozilmadi", granted.Error.Message, EtlAction.RowSkipped);
            summary.Skipped++;
            return;
        }

        var incentive = granted.Value;

        _workspace.Register(incentive);
        _workspace.Database.Add(incentive);
        summary.Created++;
    }

    /// <summary>
    /// «Чиқим» ustunini o'qiydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Bu ustun rag'batning summasi emas, sovg'aning tannarxi.</b> 33
    /// qatorning 30 tasida bir xil <c>186 000</c> so'm turibdi va yonidagi
    /// <c>«Подарок»</c> ustunida bir xil to'plam: <c>«Ташаккурнома, Термос,
    /// Блокнот, ручка»</c>. Ya'ni bu HR ning xarajat hisobi, xodimga berilgan
    /// pul emas.
    /// </para>
    /// <para>
    /// Shu sababli pulsiz turda (ikkala tur ham — <c>«Ташаккурнома»</c> va
    /// <c>«Ота-онасига ташаккурнома»</c>) summa <b>yozilmaydi</b>: domen buni
    /// to'g'ri taqiqlaydi.
    /// </para>
    /// <para>
    /// <b>NEGA qator tashlanmaydi:</b> agar tannarx tufayli qator
    /// tashlansa, 33 ta haqiqiy rag'batning HAMMASI yo'qolardi — varaqdagi
    /// har bir qatorda bu ustun to'ldirilgan. Rag'batning o'zi haqiqiy fakt,
    /// tannarx esa boshqa hisobning ma'lumoti. Shu sababli maydon bo'sh
    /// qoldiriladi (tamoyil №1) va xarajat hisobotga chiqadi — qayerga
    /// yozishni HR o'zi hal qiladi.
    /// </para>
    /// <para>
    /// <see cref="SalaryCell"/> qayta ishlatiladi: u <c>«3 000 000»</c> kabi
    /// bo'shliqli yozuvni ham tushunadi va manfiy/haddan tashqari katta
    /// qiymatni rad etadi. Uning <c>«Ишбай»</c> va <c>«Ўзгаришсиз»</c>
    /// holatlari bu ustunda ma'nosiz — ular ham «o'qilmadi» deb qaraladi,
    /// chunki xarajat ustunidagi bunday katak chinakam xato bo'lardi.
    /// </para>
    /// </remarks>
    private Money? Amount(ExcelRow row, RowContext context, CatalogEntry type)
    {
        var cell = row[Awards.Expense];

        if (cell.IsEmpty)
        {
            return null;
        }

        var parsed = SalaryCell.Parse(cell);

        if (parsed.Kind != SalaryCellKind.Amount || parsed.Salary is not { } money)
        {
            _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                "rag'bat summasi o'qilmadi", parsed.Raw, EtlAction.LeftEmpty);
            return null;
        }

        if (!type.HasAmount)
        {
            _workspace.Report.Add(Name, context.Sheet, context.Row, context.Employee,
                "tur pulsiz — «Чиқим» ustunidagi summa (sovg'a tannarxi) yozilmadi",
                money.Formatted, EtlAction.LeftEmpty);
            return null;
        }

        return money;
    }
}
