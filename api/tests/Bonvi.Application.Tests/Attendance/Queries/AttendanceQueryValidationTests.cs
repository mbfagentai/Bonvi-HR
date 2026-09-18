using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Queries;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;
using NSubstitute;

namespace Bonvi.Application.Tests.Attendance.Queries;

/// <summary>
/// Jurnal, tabel, hisobotlar va eksportlarning handler darajasidagi tekshiruvlari — SPEC §6.5–§6.7, §6.9 (B5).
/// </summary>
/// <remarks>
/// <para>
/// O'qish modeli — NSubstitute: bu yerda savol «so'rov bazaga yetib boradimi», natija emas.
/// Har bir rad etishda IKKI narsa tekshiriladi: kod va o'qish modeliga umuman murojaat
/// qilinmagani. Faqat kod tekshirilsa, handler xato qaytarib, shu bilan birga 93 kunlik
/// so'rovni bazaga yuborib qo'yishi ham mumkin edi.
/// </para>
/// <para>
/// ⭐ Eksportlar ekrandagi so'rovning AYNAN o'sha <c>BuildFilter</c> ini chaqiradi —
/// shu sababli davr qoidasi har bir eksportda ham alohida qadab qo'yiladi: kimdir
/// eksportga o'z nusxasini yozsa, u shu yerda yiqiladi.
/// </para>
/// <para>
/// Soat QOTIRILGAN (2026-09-16): «davr berilmasa — bugun» qoidasi test ishga tushgan kunga bog'liq bo'lmasin.
/// </para>
/// </remarks>
public sealed class AttendanceQueryValidationTests
{
    private static readonly DateOnly Bugun = new(2026, 9, 16);

    private readonly IAttendanceReadModel _readModel = Substitute.For<IAttendanceReadModel>();
    private readonly IAttendanceExcelWriter _writer = Substitute.For<IAttendanceExcelWriter>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public AttendanceQueryValidationTests()
    {
        _clock.Today.Returns(Bugun);
        _clock.UtcNow.Returns(new DateTimeOffset(2026, 9, 16, 7, 0, 0, TimeSpan.Zero));

        _readModel.SearchPassesAsync(Arg.Any<PassSearchFilter>(), Arg.Any<CancellationToken>())
            .Returns(new AttendancePage<AttendancePassDto>([], 0));
        _readModel.GetExceptionsAsync(Arg.Any<ExceptionFilter>(), Arg.Any<CancellationToken>())
            .Returns(new AttendancePage<AttendanceExceptionDto>([], 0));
        _readModel.GetSummaryAsync(Arg.Any<SummaryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new AttendancePage<AttendanceSummaryDto>([], 0));
        _readModel.GetTimesheetAsync(Arg.Any<TimesheetFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TimesheetPage([], [], 0, null));

        _writer.WritePasses(Arg.Any<IReadOnlyList<AttendancePassDto>>()).Returns([1]);
        _writer.WriteExceptions(Arg.Any<ExceptionKind>(), Arg.Any<IReadOnlyList<AttendanceExceptionDto>>()).Returns([1]);
        _writer.WriteSummary(Arg.Any<IReadOnlyList<AttendanceSummaryDto>>()).Returns([1]);
        _writer.WriteTimesheet(Arg.Any<AttendanceTimesheetDto>()).Returns([1]);
    }

    /// <summary>Davrli so'rovlarning hammasi — ekrandagi uchtasi va ularning eksportlari.</summary>
    public static TheoryData<string> DavrliSorovlar =>
    [
        "passes",
        "exceptions",
        "summary",
        "export-passes",
        "export-exceptions",
        "export-summary",
    ];

    // ────────────────────── Davr: 92 kun, teskari ──────────────────────

    /// <summary>
    /// Ikkala chegara ham kiradi: 1-iyun…31-avgust — AYNAN 92 kun, qabul; bir kun oldin
    /// boshlansa — 93 kun, <c>davomat.davr_uzun</c> va bazaga so'rov ketmaydi.
    /// </summary>
    [Theory]
    [MemberData(nameof(DavrliSorovlar))]
    public async Task Davr_92_kungacha_qabul_qilinadi_93_kun_davr_uzun(string sorov)
    {
        var chegarada = await YurgizAsync(sorov, new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 31));
        chegarada.IsSuccess.ShouldBeTrue(chegarada.IsFailure ? chegarada.Error.Code : null);
        OqishlarSoni().ShouldBe(1, "92 kunlik davr bazaga yetib borishi kerak");

        _readModel.ClearReceivedCalls();

        var uzun = await YurgizAsync(sorov, new DateOnly(2026, 5, 31), new DateOnly(2026, 8, 31));
        uzun.IsFailure.ShouldBeTrue();
        uzun.Error.Code.ShouldBe("davomat.davr_uzun");
        uzun.Error.Type.ShouldBe(ErrorType.Validation);
        OqishlarSoni().ShouldBe(0, "rad etilgan davr bazaga so'rov yubormasligi kerak");
    }

    /// <summary>Oxiri boshidan oldin — <c>davomat.davr_notogri</c>; bir kunlik davr (boshi = oxiri) — qabul.</summary>
    [Theory]
    [MemberData(nameof(DavrliSorovlar))]
    public async Task Teskari_davr_davr_notogri_bir_kunlik_davr_qabul(string sorov)
    {
        var teskari = await YurgizAsync(sorov, new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 1));
        teskari.IsFailure.ShouldBeTrue();
        teskari.Error.Code.ShouldBe("davomat.davr_notogri");
        teskari.Error.Type.ShouldBe(ErrorType.Validation);
        OqishlarSoni().ShouldBe(0);

        var birKun = await YurgizAsync(sorov, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1));
        birKun.IsSuccess.ShouldBeTrue(birKun.IsFailure ? birKun.Error.Code : null);
    }

    /// <summary>
    /// Davr berilmasa: jurnal — BUGUN (bitta kun), hisobotlar — shu oyning 1-sanasidan bugungacha.
    /// Faqat «from» berilib, u bugundan keyin bo'lsa — teskari davr.
    /// </summary>
    [Fact]
    public async Task Davr_berilmasa_jurnal_bugunni_hisobot_oy_boshidan_bugungacha_oladi()
    {
        (await new SearchPassesQueryHandler(_readModel, _clock).HandleAsync(new SearchPassesQuery(), Ct)).IsSuccess.ShouldBeTrue();
        await _readModel.Received(1).SearchPassesAsync(
            Arg.Is<PassSearchFilter>(filter => filter.From == Bugun && filter.To == Bugun),
            Arg.Any<CancellationToken>());

        (await new GetAttendanceSummaryQueryHandler(_readModel, _clock).HandleAsync(new GetAttendanceSummaryQuery(), Ct)).IsSuccess.ShouldBeTrue();
        await _readModel.Received(1).GetSummaryAsync(
            Arg.Is<SummaryFilter>(filter => filter.From == new DateOnly(2026, 9, 1) && filter.To == Bugun),
            Arg.Any<CancellationToken>());

        var kelajak = await new SearchPassesQueryHandler(_readModel, _clock)
            .HandleAsync(new SearchPassesQuery(From: Bugun.AddDays(1)), Ct);
        kelajak.Error.Code.ShouldBe("davomat.davr_notogri");
    }

    // ────────────────────── Tabel oyi ──────────────────────

    /// <summary>13-oy, 0-oy va 2000…2100 dan tashqari yil — <c>davomat.oy_notogri</c> (ekranda ham, eksportda ham).</summary>
    [Theory]
    [InlineData(2026, 13)]
    [InlineData(2026, 0)]
    [InlineData(2026, -1)]
    [InlineData(1999, 12)]
    [InlineData(2101, 1)]
    public async Task Notogri_oy_yoki_yil_oy_notogri(int yil, int oy)
    {
        var ekran = await new GetTimesheetQueryHandler(_readModel).HandleAsync(new GetTimesheetQuery(yil, oy), Ct);
        ekran.IsFailure.ShouldBeTrue();
        ekran.Error.Code.ShouldBe("davomat.oy_notogri");
        ekran.Error.Type.ShouldBe(ErrorType.Validation);

        var eksport = await new ExportTimesheetQueryHandler(_readModel, _writer).HandleAsync(new GetTimesheetQuery(yil, oy), Ct);
        eksport.IsFailure.ShouldBeTrue();
        eksport.Error.Code.ShouldBe("davomat.oy_notogri");

        OqishlarSoni().ShouldBe(0);
        _writer.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(2000, 1)]
    [InlineData(2100, 12)]
    [InlineData(2026, 2)]
    public async Task Chegaradagi_oy_va_yil_qabul_qilinadi(int yil, int oy)
    {
        var natija = await new GetTimesheetQueryHandler(_readModel).HandleAsync(new GetTimesheetQuery(yil, oy), Ct);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        natija.Value.Year.ShouldBe(yil);
        natija.Value.Month.ShouldBe(oy);
        await _readModel.Received(1).GetTimesheetAsync(
            Arg.Is<TimesheetFilter>(filter => filter.Year == yil && filter.Month == oy),
            Arg.Any<CancellationToken>());
    }

    // ────────────────────── Chetlanish turi ──────────────────────

    /// <summary>
    /// Tur MAJBURIY: yo'q, bo'sh, noma'lum va sanoqda yo'q RAQAM («99», «0») —
    /// <c>davomat.hisobot_turi_notogri</c>. Raqam holati <c>Enum.TryParse</c> ning tuzog'i:
    /// «99» o'qiladi, lekin sanoqda yo'q va jimgina bo'sh ro'yxat qaytarardi.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Sick")]
    [InlineData("99")]
    [InlineData("0")]
    public async Task Chetlanish_turi_yoq_yoki_nomalum_bolsa_hisobot_turi_notogri(string? tur)
    {
        var ekran = await new GetAttendanceExceptionsQueryHandler(_readModel, _clock)
            .HandleAsync(new GetAttendanceExceptionsQuery(Kind: tur), Ct);
        ekran.IsFailure.ShouldBeTrue();
        ekran.Error.Code.ShouldBe("davomat.hisobot_turi_notogri");
        ekran.Error.Type.ShouldBe(ErrorType.Validation);

        var eksport = await new ExportAttendanceExceptionsQueryHandler(_readModel, _writer, _clock)
            .HandleAsync(new GetAttendanceExceptionsQuery(Kind: tur), Ct);
        eksport.IsFailure.ShouldBeTrue();
        eksport.Error.Code.ShouldBe("davomat.hisobot_turi_notogri");

        OqishlarSoni().ShouldBe(0);
    }

    /// <summary>Registr ahamiyatsiz va chetdagi bo'shliq kechiriladi — o'qilgan tur filtrga aynan tushadi.</summary>
    [Theory]
    [InlineData("late", ExceptionKind.Late)]
    [InlineData(" EarlyLeave ", ExceptionKind.EarlyLeave)]
    [InlineData("ABSENT", ExceptionKind.Absent)]
    [InlineData("Missing", ExceptionKind.Missing)]
    public async Task Chetlanish_turi_registrsiz_oqiladi(string tur, ExceptionKind kutilgan)
    {
        var natija = await new GetAttendanceExceptionsQueryHandler(_readModel, _clock)
            .HandleAsync(new GetAttendanceExceptionsQuery(Kind: tur), Ct);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
        await _readModel.Received(1).GetExceptionsAsync(
            Arg.Is<ExceptionFilter>(filter => filter.Kind == kutilgan),
            Arg.Any<CancellationToken>());
    }

    // ────────────────────── Eksport chegarasi ──────────────────────

    /// <summary>
    /// Qator eksportlari: chegaraning o'zi (20 000) — fayl yoziladi; bitta ortiq — fayl QIRQILMAYDI,
    /// <c>davomat.eksport_juda_kop</c> qaytadi va Excel umuman yig'ilmaydi. Xabarda ikkala son bor.
    /// </summary>
    [Theory]
    [InlineData("export-passes")]
    [InlineData("export-exceptions")]
    [InlineData("export-summary")]
    public async Task Qator_eksporti_chegaradan_oshsa_eksport_juda_kop(string sorov)
    {
        JamiQaytar(sorov, AttendanceExport.MaxRows);

        var chegarada = await YurgizAsync(sorov, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));
        chegarada.IsSuccess.ShouldBeTrue(chegarada.IsFailure ? chegarada.Error.Code : null);
        _writer.ReceivedCalls().Count().ShouldBe(1, "chegaraning o'zi hali eksport qilinadi");

        _writer.ClearReceivedCalls();
        JamiQaytar(sorov, AttendanceExport.MaxRows + 1);

        var oshgan = await YurgizAsync(sorov, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));
        oshgan.IsFailure.ShouldBeTrue();
        oshgan.Error.Code.ShouldBe("davomat.eksport_juda_kop");
        oshgan.Error.Type.ShouldBe(ErrorType.Validation);
        oshgan.Error.Message.ShouldContain("20001");
        oshgan.Error.Message.ShouldContain("20000");
        _writer.ReceivedCalls().ShouldBeEmpty("chegaradan oshgan eksport QIRQIB yozilmasligi kerak");
    }

    /// <summary>Tabel eksporti — chegara XODIMLAR soni bo'yicha (2 000), qatorlar emas.</summary>
    [Fact]
    public async Task Tabel_eksporti_xodimlar_chegarasidan_oshsa_eksport_juda_kop()
    {
        var handler = new ExportTimesheetQueryHandler(_readModel, _writer);

        _readModel.GetTimesheetAsync(Arg.Any<TimesheetFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TimesheetPage([], [], AttendanceExport.MaxTimesheetEmployees, null));

        var chegarada = await handler.HandleAsync(new GetTimesheetQuery(2026, 8), Ct);
        chegarada.IsSuccess.ShouldBeTrue(chegarada.IsFailure ? chegarada.Error.Code : null);
        chegarada.Value.FileName.ShouldBe("Bonvi_davomat_tabel_2026-08.xlsx");
        _writer.Received(1).WriteTimesheet(Arg.Any<AttendanceTimesheetDto>());

        _writer.ClearReceivedCalls();
        _readModel.GetTimesheetAsync(Arg.Any<TimesheetFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TimesheetPage([], [], AttendanceExport.MaxTimesheetEmployees + 1, null));

        var oshgan = await handler.HandleAsync(new GetTimesheetQuery(2026, 8), Ct);
        oshgan.IsFailure.ShouldBeTrue();
        oshgan.Error.Code.ShouldBe("davomat.eksport_juda_kop");
        oshgan.Error.Message.ShouldContain("2001");
        _writer.ReceivedCalls().ShouldBeEmpty();
    }

    // ────────────────────── Ishdan ketganlar nishoni ──────────────────────

    /// <summary>
    /// #74a: <c>count</c> — ODAMLAR soni, o'tishlar emas. Uch marta o'tgan bitta odam — count 1,
    /// va <c>items</c> o'qish modeli bergan ro'yxatning o'zi (taxtadagi bilan bitta metoddan).
    /// </summary>
    [Fact]
    public async Task Ishdan_ketganlar_sanogi_odamlar_boyicha_otishlar_boyicha_emas()
    {
        FormerEmployeeAlertDto[] ogohlantirishlar =
        [
            new(
                Guid.CreateVersion7(), Guid.CreateVersion7(), "Ketgan Xodim", null,
                new DateTimeOffset(2026, 9, 16, 4, 15, 0, TimeSpan.Zero), Guid.CreateVersion7(), "Kirish-1",
                PassDirection.In, new DateOnly(2026, 8, 31), "Bonvi MChJ", PassCount: 3),
        ];
        _readModel.GetFormerEmployeeAlertsAsync(Arg.Any<CancellationToken>()).Returns(ogohlantirishlar);

        var natija = await new GetFormerEmployeeAlertsQueryHandler(_readModel)
            .HandleAsync(new GetFormerEmployeeAlertsQuery(), Ct);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Count.ShouldBe(1);
        natija.Value.Items.ShouldBe(ogohlantirishlar);
        natija.Value.Items[0].PassCount.ShouldBe(3);
    }

    // ────────────────────── Yordamchilar ──────────────────────

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>O'qish modelining ma'lumot o'qiydigan metodlariga nechta murojaat bo'lgani.</summary>
    private int OqishlarSoni()
        => _readModel.ReceivedCalls().Count(call => call.GetMethodInfo().Name is
            nameof(IAttendanceReadModel.SearchPassesAsync)
            or nameof(IAttendanceReadModel.GetExceptionsAsync)
            or nameof(IAttendanceReadModel.GetSummaryAsync)
            or nameof(IAttendanceReadModel.GetTimesheetAsync));

    private void JamiQaytar(string sorov, int jami)
    {
        switch (sorov)
        {
            case "export-passes":
                _readModel.SearchPassesAsync(Arg.Any<PassSearchFilter>(), Arg.Any<CancellationToken>())
                    .Returns(new AttendancePage<AttendancePassDto>([], jami));
                break;
            case "export-exceptions":
                _readModel.GetExceptionsAsync(Arg.Any<ExceptionFilter>(), Arg.Any<CancellationToken>())
                    .Returns(new AttendancePage<AttendanceExceptionDto>([], jami));
                break;
            case "export-summary":
                _readModel.GetSummaryAsync(Arg.Any<SummaryFilter>(), Arg.Any<CancellationToken>())
                    .Returns(new AttendancePage<AttendanceSummaryDto>([], jami));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(sorov), sorov, "noma'lum eksport");
        }
    }

    /// <summary>So'rovni nomi bo'yicha, berilgan davr bilan yurgizadi.</summary>
    private async Task<Result> YurgizAsync(string sorov, DateOnly from, DateOnly to)
        => sorov switch
        {
            "passes" => await new SearchPassesQueryHandler(_readModel, _clock)
                .HandleAsync(new SearchPassesQuery(From: from, To: to), Ct),
            "exceptions" => await new GetAttendanceExceptionsQueryHandler(_readModel, _clock)
                .HandleAsync(new GetAttendanceExceptionsQuery(Kind: "Late", From: from, To: to), Ct),
            "summary" => await new GetAttendanceSummaryQueryHandler(_readModel, _clock)
                .HandleAsync(new GetAttendanceSummaryQuery(From: from, To: to), Ct),
            "export-passes" => await new ExportPassesQueryHandler(_readModel, _writer, _clock)
                .HandleAsync(new SearchPassesQuery(From: from, To: to), Ct),
            "export-exceptions" => await new ExportAttendanceExceptionsQueryHandler(_readModel, _writer, _clock)
                .HandleAsync(new GetAttendanceExceptionsQuery(Kind: "Late", From: from, To: to), Ct),
            "export-summary" => await new ExportAttendanceSummaryQueryHandler(_readModel, _writer, _clock)
                .HandleAsync(new GetAttendanceSummaryQuery(From: from, To: to), Ct),
            _ => throw new ArgumentOutOfRangeException(nameof(sorov), sorov, "noma'lum so'rov"),
        };
}
