using System.Text;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Etl;
using Bonvi.Etl.Excel;
using Bonvi.Etl.Mapping;
using Bonvi.Etl.Reporting;
using Bonvi.Etl.Runtime;
using Bonvi.Etl.Steps;
using ClosedXML.Excel;

namespace Bonvi.Etl.Tests;

/// <summary>
/// 6-qadam — profil boyitish: «91.15 Қобилятлар жадвали» va
/// «91.14 Тақдирланган ходимлар».
/// </summary>
/// <remarks>
/// <para>
/// Testlar haqiqiy <c>.xlsx</c> yasaydi (vaqtinchalik faylda), lekin
/// <b>bloknotning o'zini talab qilmaydi</b>: har bir test faqat o'ziga
/// kerakli bir-ikki qatorni yozadi. Baza ham kerak emas —
/// <see cref="EtlDatabase.Offline"/> va urug' fayllaridan qurilgan
/// ma'lumotnoma yetarli.
/// </para>
/// <para>
/// Bu qadamda eng nozik joy — <b>summa qoidasi</b>. «Чиқим» ustuni
/// rag'batning summasi emas, sovg'aning tannarxi, va uni ko'r-ko'rona
/// <c>Incentive.Amount</c> ga yozish 33 ta yozuvning hammasini domen
/// tekshiruvida yiqitardi. Shu sababli qoida hujjatda emas, aynan shu
/// yerda qadalgan.
/// </para>
/// </remarks>
public sealed class ProfilesStepTests : IDisposable
{
    /// <summary>Varaq nomlari ATAYLAB matn sifatida takrorlangan.</summary>
    /// <remarks>
    /// Qadamdagi doimiylarga havola qilinsa, varaq nomi noto'g'ri
    /// o'zgartirilganda test ham u bilan birga «to'g'rilanib» ketardi va
    /// ko'chirish jimgina ishlamay qolardi. Nom Excel'da qanday bo'lsa —
    /// imlo xatosi bilan («Қобилятлар») — shundayligicha yozilgan.
    /// </remarks>
    private const string CompetencySheet = "91.15 Қобилятлар жадвали";

    private const string AwardSheet = "91.14 Тақдирланган ходимлар";

    /// <summary>
    /// Sinov uchun alias jadvali.
    /// </summary>
    /// <remarks>
    /// Haqiqiy <c>aliases.json</c> dagi ikkita yozuvning nusxasi, ustiga
    /// pulli tur qo'shilgan: bloknotda pulli rag'bat hozircha yo'q, lekin
    /// qoidani sinash uchun kerak. Kanonik nomlar <c>award-types.json</c>
    /// dagi nomlar bilan AYNAN bir xil bo'lishi shart — ma'lumotnomaga
    /// aynan shu nom bo'yicha kiriladi.
    /// </remarks>
    private const string Aliases = """
        {
          "awardTypes": {
            "Йил якунида ташаккурнома берилди": "Ташаккурнома",
            "Пул мукофоти берилди": "Пул мукофоти",
            "Кимга берилгани ёзилмаган": null
          }
        }
        """;

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"etl-profiles-{Guid.NewGuid():N}.xlsx");
    private readonly EtlReport _report = new();
    private readonly List<(string Sheet, int Row, int Column, XLCellValue Value)> _cells = [];
    private readonly List<Employee> _people = [];
    private readonly List<EmployeeCompetency> _profiles = [];
    private readonly List<Incentive> _incentives = [];

    private ExcelWorkbook? _workbook;
    private EtlWorkspace? _workspace;

    // ────────────────────────────────────────────────────────────
    //  91.15 — qobiliyatlar
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Qobiliyat_ustunlari_profilga_kochiriladi()
    {
        var employee = AddEmployee();
        CompetencyRow(3, "Каримов Азиз Валиевич", position: "Бухгалтер");
        Put(CompetencySheet, 3, 5, "Рус тили ва Инглиз тили");
        Put(CompetencySheet, 3, 6, 80);
        Put(CompetencySheet, 3, 7, "Ихтисодиёт");
        Put(CompetencySheet, 3, 8, "Пайвандлаш");
        Put(CompetencySheet, 3, 9, "Шахмат");
        Put(CompetencySheet, 3, 10, "ТДТУ");

        var summary = Run();

        summary.Created.ShouldBe(1);

        var profile = Workspace.CompetencyOf(employee.Id).ShouldNotBeNull();
        profile.Languages.ShouldBe("Рус тили ва Инглиз тили");
        profile.ComputerLiteracyPercent.ShouldBe(80);
        profile.Specialization.ShouldBe("Ихтисодиёт");
        profile.OtherAbilities.ShouldBe("Пайвандлаш");
        profile.Interest.ShouldBe("Шахмат");
        profile.EducationPlace.ShouldBe("ТДТУ");
    }

    [Fact]
    public void Bolim_sarlavhasi_qator_deb_oqilmaydi()
    {
        // «1-ДЕПАРТАМЕНТ БЎЛИМИ ( 6 ходим )» F.I.Sh ustunida yotadi, ya'ni
        // «ismi bormi» tekshiruvi uni ajratmaydi. Varaqda shunday 46 ta
        // qator bor va ularning har biri hisobotga «xodim topilmadi» bo'lib
        // tushishi mumkin edi.
        Put(CompetencySheet, 3, 2, "1-ДЕПАРТАМЕНТ БЎЛИМИ ( 6 ходим )");
        Put(CompetencySheet, 3, 3, 6);
        Put(CompetencySheet, 3, 5, "Рус тили");

        var summary = Run();

        summary.Read.ShouldBe(0);
        _report.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Qobiliyati_yozilmagan_qator_umuman_sanalmaydi()
    {
        // Varaq butun korxonaning ro'yxati: 509 qatordan atigi 43 tasida
        // qobiliyat ustuni to'ldirilgan. Qolganida ko'chiradigan narsa yo'q
        // — bo'sh profil yozuv emas.
        _ = AddEmployee();
        CompetencyRow(3, "Каримов Азиз Валиевич", position: "Бухгалтер");

        var summary = Run();

        summary.Read.ShouldBe(0);
        summary.Created.ShouldBe(0);
        _report.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Topilmagan_xodimning_qobiliyati_kochirilmaydi()
    {
        CompetencyRow(3, "Каримов Азиз Валиевич", position: "Бухгалтер");
        Put(CompetencySheet, 3, 7, "Ихтисодиёт");

        var summary = Run();

        summary.Read.ShouldBe(1);
        summary.Created.ShouldBe(0);
        summary.Skipped.ShouldBe(1);
        _report.Issues.ShouldContain(issue => issue.Problem.StartsWith("xodim topilmadi", StringComparison.Ordinal));
    }

    [Fact]
    public void Til_ustunidagi_tavsif_yoziladi_lekin_belgilanadi()
    {
        // 213-qatorda til o'rniga xodimga berilgan tavsif yozilgan. Qiymat
        // saqlanadi (tashlash — ma'lumot yo'qotish), lekin HR ko'radi.
        const string Description = "Топ кадр хисобланади, берилган вазифани чин дилдан килади, шунчаки эмас.";

        var employee = AddEmployee();
        CompetencyRow(3, "Каримов Азиз Валиевич", position: "Бухгалтер");
        Put(CompetencySheet, 3, 5, Description);

        _ = Run();

        Workspace.CompetencyOf(employee.Id).ShouldNotBeNull().Languages.ShouldBe(Description);
        _report.Issues.ShouldContain(issue => issue.Action == EtlAction.Flagged);
    }

    [Fact]
    public void Qisqa_til_nomi_belgilanmaydi()
    {
        _ = AddEmployee();
        CompetencyRow(3, "Каримов Азиз Валиевич", position: "Бухгалтер");
        Put(CompetencySheet, 3, 5, "Рус тили ва Инглиз тили");

        _ = Run();

        _report.Issues.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(80, 80)]      // oddiy foiz
    [InlineData(0.8, 80)]     // Excel'ning «%» formati 80 % ni 0.8 qilib saqlaydi
    [InlineData(100, 100)]    // yuqori chegara
    public void Kompyuter_savodxonligi_foizga_keltiriladi(double raw, int expected)
    {
        var employee = AddEmployee();
        CompetencyRow(3, "Каримов Азиз Валиевич", position: "Бухгалтер");
        Put(CompetencySheet, 3, 6, raw);

        _ = Run();

        Workspace.CompetencyOf(employee.Id).ShouldNotBeNull().ComputerLiteracyPercent.ShouldBe(expected);
    }

    [Theory]
    [InlineData(150.0)]
    [InlineData(-5.0)]
    public void Oraliqdan_chiqqan_foiz_bosh_qoldiriladi(double raw)
    {
        // Domen 0–100 ni talab qiladi. Qator tashlanmaydi: qolgan ustunlar
        // haqiqiy va ular tufayli yo'qolmasligi kerak.
        var employee = AddEmployee();
        CompetencyRow(3, "Каримов Азиз Валиевич", position: "Бухгалтер");
        Put(CompetencySheet, 3, 6, raw);
        Put(CompetencySheet, 3, 7, "Ихтисодиёт");

        var summary = Run();

        summary.Created.ShouldBe(1);

        var profile = Workspace.CompetencyOf(employee.Id).ShouldNotBeNull();
        profile.ComputerLiteracyPercent.ShouldBeNull();
        profile.Specialization.ShouldBe("Ихтисодиёт");
        _report.Issues.ShouldContain(issue => issue.Action == EtlAction.LeftEmpty);
    }

    [Fact]
    public void Mavjud_profilning_faqat_bosh_maydoni_toldiriladi()
    {
        // Ikkinchi yugurishda HR tizimda kiritgan aniqroq qiymat Excel'dagi
        // eski qiymat bilan almashtirilmasligi kerak: Excel bir martalik
        // manba, tizim esa doimiy.
        var employee = AddEmployee();

        var existing = EmployeeCompetency.Create(employee.Id).Value;
        existing.SetSpecialization("Молия");
        _profiles.Add(existing);

        CompetencyRow(3, "Каримов Азиз Валиевич", position: "Бухгалтер");
        Put(CompetencySheet, 3, 7, "Ихтисодиёт");
        Put(CompetencySheet, 3, 10, "ТДТУ");

        var summary = Run();

        summary.Created.ShouldBe(0);
        summary.Updated.ShouldBe(1);

        existing.Specialization.ShouldBe("Молия");
        existing.EducationPlace.ShouldBe("ТДТУ");
        _report.Issues.ShouldBeEmpty();
    }

    // ────────────────────────────────────────────────────────────
    //  91.14 — rag'batlar
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Ragbat_turi_alias_orqali_topiladi_va_yoziladi()
    {
        var employee = AddEmployee();
        AwardRow(3, "Каримов Азиз Валиевич", type: "Йил якунида ташаккурнома берилди", date: 45651);
        Put(AwardSheet, 3, 8, "Ташаккурнома, Термос, Блокнот, ручка");

        var summary = Run();

        summary.Read.ShouldBe(1);
        summary.Created.ShouldBe(1);

        var typeId = Workspace.Catalogs.Find(CatalogKind.AwardType, "Ташаккурнома").ShouldNotBeNull();
        Workspace.HasIncentive(employee.Id, IncentiveKind.Award, typeId, new DateOnly(2024, 12, 25))
            .ShouldBeTrue();
    }

    [Fact]
    public void Pulsiz_turda_chiqim_yozilmaydi_lekin_ragbat_saqlanadi()
    {
        // «Чиқим» — sovg'aning tannarxi, xodimga berilgan pul emas. 33
        // qatorning HAMMASIDA u to'ldirilgan: qator tashlansa, butun varaq
        // ko'chmay qolardi.
        var employee = AddEmployee();
        AwardRow(3, "Каримов Азиз Валиевич", type: "Йил якунида ташаккурнома берилди", date: 45651);
        Put(AwardSheet, 3, 7, 186000);

        var summary = Run();

        summary.Created.ShouldBe(1);
        summary.Skipped.ShouldBe(0);

        var typeId = Workspace.Catalogs.Find(CatalogKind.AwardType, "Ташаккурнома").ShouldNotBeNull();
        Workspace.HasIncentive(employee.Id, IncentiveKind.Award, typeId, new DateOnly(2024, 12, 25))
            .ShouldBeTrue();

        _report.Issues.ShouldContain(issue =>
            issue.Problem.Contains("tur pulsiz", StringComparison.Ordinal)
            && issue.Action == EtlAction.LeftEmpty);
    }

    [Fact]
    public void Pulli_turda_summa_bolsa_ragbat_yoziladi()
    {
        var employee = AddEmployee();
        AwardRow(3, "Каримов Азиз Валиевич", type: "Пул мукофоти берилди", date: 45651);
        Put(AwardSheet, 3, 7, "3 000 000");

        var summary = Run();

        summary.Created.ShouldBe(1);

        var typeId = Workspace.Catalogs.Find(CatalogKind.AwardType, "Пул мукофоти").ShouldNotBeNull();
        Workspace.HasIncentive(employee.Id, IncentiveKind.Award, typeId, new DateOnly(2024, 12, 25))
            .ShouldBeTrue();
    }

    [Fact]
    public void Pulli_turda_summasiz_qator_tashlanadi()
    {
        // Domen qoidasi: pul bilan bog'liq turda summa MAJBURIY. «Qancha
        // berilgani» noma'lum bo'lsa, yozuvning butun ma'nosi yo'qoladi.
        // Bloknotda bunday qator hozircha yo'q, lekin HR varaqni to'ldirgach
        // paydo bo'lishi mumkin — dastur yiqilmasligi kerak.
        _ = AddEmployee();
        AwardRow(3, "Каримов Азиз Валиевич", type: "Пул мукофоти берилди", date: 45651);

        var summary = Run();

        summary.Created.ShouldBe(0);
        summary.Skipped.ShouldBe(1);
        _report.Issues.ShouldContain(issue =>
            issue.Problem.Contains("pul bilan bog'liq", StringComparison.Ordinal)
            && issue.Action == EtlAction.RowSkipped);
    }

    [Fact]
    public void Pulli_turda_oqilmagan_chiqim_qatorni_tashlaydi()
    {
        _ = AddEmployee();
        AwardRow(3, "Каримов Азиз Валиевич", type: "Пул мукофоти берилди", date: 45651);
        Put(AwardSheet, 3, 7, "Ишбай");

        var summary = Run();

        summary.Created.ShouldBe(0);
        summary.Skipped.ShouldBe(1);
    }

    [Fact]
    public void Turi_yozilmagan_qator_tashlanadi()
    {
        // Varaqning 44-qatori aynan shunday: xodim, bo'lim, lavozim va sana
        // bor, «Вид благодарности» esa bo'sh. Turni taxmin qilib bo'lmaydi —
        // u summa qoidasini ham belgilaydi.
        _ = AddEmployee();
        AwardRow(3, "Каримов Азиз Валиевич", type: null, date: 45651);

        var summary = Run();

        summary.Read.ShouldBe(1);
        summary.Skipped.ShouldBe(1);
        _report.Issues.ShouldContain(issue => issue.Problem == "rag'bat turi ko'rsatilmagan");
    }

    [Fact]
    public void Sanasiz_ragbat_yozilmaydi()
    {
        // Sana — rag'batning yagona ajratuvchi belgisi (buyruq ustuni
        // varaqda yo'q). Usiz yozuv idempotentlik kalitiga tushmasdi va har
        // yugurishda takrorlanardi.
        _ = AddEmployee();
        AwardRow(3, "Каримов Азиз Валиевич", type: "Йил якунида ташаккурнома берилди", date: null);

        var summary = Run();

        summary.Skipped.ShouldBe(1);
        _report.Issues.ShouldContain(issue => issue.Problem == "rag'bat sanasi topilmadi");
    }

    [Fact]
    public void Yil_sarlavhasi_va_bosh_qator_hisobotga_tushmaydi()
    {
        // 3-qatorda «2026 йил», 4–7-qatorlarda faqat tartib raqami: HR
        // kelasi yil uchun oldindan joy tayyorlab qo'ygan. Bu xato emas.
        Put(AwardSheet, 3, 4, "2026 йил");
        Put(AwardSheet, 4, 1, 1);
        Put(AwardSheet, 5, 1, 2);

        var summary = Run();

        summary.Read.ShouldBe(0);
        _report.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Topilmagan_xodimning_ragbati_yozilmaydi()
    {
        AwardRow(3, "Каримов Азиз Валиевич", type: "Йил якунида ташаккурнома берилди", date: 45651);

        var summary = Run();

        summary.Created.ShouldBe(0);
        summary.Skipped.ShouldBe(1);
        _report.Issues.ShouldContain(issue => issue.Problem.StartsWith("xodim topilmadi", StringComparison.Ordinal));
    }

    [Fact]
    public void Allaqachon_yozilgan_ragbat_takrorlanmaydi()
    {
        var employee = AddEmployee();
        var typeId = CatalogIndex.FromSeedFiles().Find(CatalogKind.AwardType, "Ташаккурнома")!.Value;

        _incentives.Add(Incentive.Grant(
            employee.Id,
            IncentiveKind.Award,
            typeId,
            new DateOnly(2024, 12, 25),
            typeHasAmount: false).Value);

        AwardRow(3, "Каримов Азиз Валиевич", type: "Йил якунида ташаккурнома берилди", date: 45651);

        var summary = Run();

        summary.Created.ShouldBe(0);
        summary.Updated.ShouldBe(1);
    }

    // ────────────────────────────────────────────────────────────
    //  Umumiy xatti-harakat
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Yoq_varaqlar_hisobotga_tushadi_va_qadam_yiqilmaydi()
    {
        // Bloknotning eski nusxasida bu varaqlar bo'lmasligi mumkin. Qadam
        // to'xtamaydi: qolgan qadamlar allaqachon bajarilgan va ularning
        // natijasi bitta yo'q varaq tufayli bekor qilinmasligi kerak.
        var summary = Run(withSheets: false);

        summary.Read.ShouldBe(0);
        _report.Issues.Count(issue => issue.Problem == "varaq topilmadi").ShouldBe(2);
    }

    [Fact]
    public void Cheklov_har_varaqqa_alohida_qollanadi()
    {
        _ = AddEmployee("Каримов", "Азиз", "Валиевич");
        _ = AddEmployee("Раҳимов", "Ботир", "Аҳмадович");

        CompetencyRow(3, "Каримов Азиз Валиевич", position: "Бухгалтер");
        Put(CompetencySheet, 3, 7, "Ихтисодиёт");
        CompetencyRow(4, "Раҳимов Ботир Аҳмадович", position: "Бухгалтер");
        Put(CompetencySheet, 4, 7, "Молия");

        AwardRow(3, "Каримов Азиз Валиевич", type: "Йил якунида ташаккурнома берилди", date: 45651);
        AwardRow(4, "Раҳимов Ботир Аҳмадович", type: "Йил якунида ташаккурнома берилди", date: 45651);

        var summary = Run(limit: 1);

        // Har varaqdan bittadan: bitta profil va bitta rag'bat.
        summary.Read.ShouldBe(2);
        summary.Created.ShouldBe(2);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchi
    // ────────────────────────────────────────────────────────────

    private EtlWorkspace Workspace
        => _workspace ?? throw new InvalidOperationException("Avval Run() chaqirilishi kerak.");

    private Employee AddEmployee(string last = "Каримов", string first = "Азиз", string? middle = "Валиевич")
    {
        var employee = Employee.Create(FullName.FromTrusted(last, first, middle), new DateOnly(1990, 5, 17)).Value;
        _people.Add(employee);
        return employee;
    }

    /// <summary>Qobiliyatlar varag'ining «skeleti»: tartib raqami, ism, lavozim.</summary>
    /// <remarks>
    /// Tug'ilgan sana ATAYLAB bo'sh: xodim ism bo'yicha yagona topilsin va
    /// test aynan qobiliyat ustunlarini tekshirsin, ism-sana mosligini emas
    /// (uning o'z testlari <c>EtlWorkspaceTests</c> da).
    /// </remarks>
    private void CompetencyRow(int row, string name, string position)
    {
        Put(CompetencySheet, row, 1, row - 2);
        Put(CompetencySheet, row, 2, name);
        Put(CompetencySheet, row, 4, position);
    }

    private void AwardRow(int row, string name, string? type, double? date)
    {
        Put(AwardSheet, row, 1, row - 2);
        Put(AwardSheet, row, 2, name);

        if (type is not null)
        {
            Put(AwardSheet, row, 6, type);
        }

        if (date is { } serial)
        {
            Put(AwardSheet, row, 9, serial);
        }
    }

    private void Put(string sheet, int row, int column, XLCellValue value)
        => _cells.Add((sheet, row, column, value));

    /// <summary>Ikkala varaqning 1- va 2-qatorini — sarlavhalarini — yozadi.</summary>
    /// <remarks>
    /// <para>
    /// Sarlavhalar ATAYLAB yoziladi, garchi hech bir test ularni
    /// tekshirmasa ham. Ikki sabab bor:
    /// </para>
    /// <list type="number">
    ///   <item>butunlay bo'sh varaq <c>RowsUsed()</c> da ko'rinmaydi va
    ///         «varaq topilmadi» bo'lib hisobotga tushardi — ya'ni «varaq bor,
    ///         lekin ma'lumot yo'q» holati sinalmay qolardi;</item>
    ///   <item>har bir test <c>FirstDataRow</c> tekshiruvidan ham o'tadi:
    ///         sarlavha qatori xodim qatori deb o'qilib qolmasligi kerak.</item>
    /// </list>
    /// </remarks>
    private void Headers()
    {
        Put(CompetencySheet, 1, 1, "\"MBF COMPANY\" ДА ... ҚЎШИМЧА ҚОБИЛИЯТИ ВА БИЛИМИ БОР ХОДИМЛАР");
        Put(CompetencySheet, 2, 1, "№");
        Put(CompetencySheet, 2, 2, "ХОДИМНИНГ ФАМИЛИЯСИ, ИСМИ, ОТАСИНИНГ ИСМИ");
        Put(CompetencySheet, 2, 3, "ХОДИМ ТУҒИЛГАН САНА");
        Put(CompetencySheet, 2, 4, "ЛАВОЗИМИ");
        Put(CompetencySheet, 2, 5, "Тил билиш");
        Put(CompetencySheet, 2, 6, "Компьютер саводхонлиги");
        Put(CompetencySheet, 2, 7, "Мутахассислиги");
        Put(CompetencySheet, 2, 8, "Бошка қобилиятлар");
        Put(CompetencySheet, 2, 9, "Қизикиши нимада");
        Put(CompetencySheet, 2, 10, "Талим олган жойи");

        Put(AwardSheet, 1, 2, "\"MBF COMPANY\" ДА БЎЙИЧА ТАҚДИРЛАНГАН ХОДИМЛАР РЎЙХАТИ.");
        Put(AwardSheet, 2, 1, "№");
        Put(AwardSheet, 2, 2, "ФИО");
        Put(AwardSheet, 2, 3, "Ишга келган сана");
        Put(AwardSheet, 2, 4, "Бўлим");
        Put(AwardSheet, 2, 5, "Лавозим");
        Put(AwardSheet, 2, 6, "Вид благодарности");
        Put(AwardSheet, 2, 7, "Чиқим");
        Put(AwardSheet, 2, 8, "Подарок");
        Put(AwardSheet, 2, 9, "Сана");
    }

    /// <summary>Varaqlarni yozadi, ish maydonini yig'adi va qadamni yurgizadi.</summary>
    /// <param name="limit">«--limit» — har varaqdan nechta qator o'qilsin.</param>
    /// <param name="withSheets">
    /// Ikkala varaq ham yaratilsinmi.
    /// </param>
    /// <remarks>
    /// <paramref name="withSheets"/> standart holatda <c>true</c>: aks holda
    /// faqat bitta varaqqa tegadigan har bir test hisobotda begona «varaq
    /// topilmadi» satrini olardi va «hisobot toza» degan tekshiruvlar
    /// yolg'ondan yiqilardi. Yo'q varaq holati o'z testida ataylab
    /// yaratiladi.
    /// </remarks>
    private StepSummary Run(int? limit = null, bool withSheets = true)
    {
        using (var created = new XLWorkbook())
        {
            // ClosedXML varaqsiz kitobni saqlamaydi, shu sababli kamida
            // bittasi doim bo'ladi.
            _ = created.AddWorksheet("bo'sh");

            if (withSheets)
            {
                _ = created.AddWorksheet(CompetencySheet);
                _ = created.AddWorksheet(AwardSheet);
                Headers();
            }

            foreach (var (sheet, row, column, value) in _cells)
            {
                created.Worksheet(sheet).Cell(row, column).Value = value;
            }

            created.SaveAs(_path);
        }

        _workbook = new ExcelWorkbook(_path);

        _workspace = new EtlWorkspace(
            new EtlOptions { Source = _path, Limit = limit },
            _workbook,
            AliasTable.Parse(new MemoryStream(Encoding.UTF8.GetBytes(Aliases))),
            CatalogIndex.FromSeedFiles(),
            EtlDatabase.Offline(),
            _report);

        foreach (var employee in _people)
        {
            _workspace.Register(employee);
        }

        foreach (var profile in _profiles)
        {
            _workspace.Register(profile);
        }

        foreach (var incentive in _incentives)
        {
            _workspace.Register(incentive);
        }

        new ProfilesStep(_workspace).Run();

        return _report.Step(ProfilesStep.Name);
    }

    public void Dispose()
    {
        _workbook?.Dispose();
        File.Delete(_path);
    }
}
