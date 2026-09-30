using System.Globalization;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Identities;
using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Tests.Attendance.Identities;

/// <summary>
/// Verifix ro'yxatining sarlavhasini topish va qatorlarini ajratish — SPEC §4.4.
/// </summary>
public sealed class IdentityImportSheetTests
{
    private static ImportSheetRow Row(int number, params string[] cells) => new(number, cells);

    [Fact]
    public void Sarlavha_birinchi_10_qatorda_topiladi_registr_va_bosh_joylar_farq_qilmaydi()
    {
        var rows = new[]
        {
            Row(1, "Verifix eksport"),
            Row(2, "2026-09-17"),
            Row(3, "  табельный   НОМЕР ", "ф.и.о.", "pinfl"),
            Row(4, "10427", "Каримов Азиз", "31505880000001"),
        };

        var lines = IdentityImportSheet.Parse(rows).Value;

        lines.ShouldHaveSingleItem().ShouldBe(new IdentityImportLine(4, "10427", "Каримов Азиз", "31505880000001"));
    }

    [Fact]
    public void Bir_nechta_mos_ustunda_aniqrog_i_yutadi()
    {
        var rows = new[]
        {
            Row(1, "ID", "Employee No", "Name"),
            Row(2, "1", "E-77", "Ali"),
        };

        IdentityImportSheet.Parse(rows).Value.ShouldHaveSingleItem().RawPersonId.ShouldBe("E-77");
    }

    [Fact]
    public void Shaxs_raqamisiz_qatorlar_tashlanadi_ism_va_jshshir_ustunsiz_ham_ishlaydi()
    {
        var rows = new[]
        {
            Row(1, "Kod"),
            Row(2, " 5 "),
            Row(3, ""),
            Row(5, "6"),
        };

        var lines = IdentityImportSheet.Parse(rows).Value;

        lines.Select(line => line.RawPersonId).ShouldBe(["5", "6"]);
        lines.ShouldAllBe(line => line.FullName == null && line.Pinfl == null);
    }

    [Fact]
    public void Sarlavha_10_qatordan_keyin_bolsa_ustun_topilmadi()
    {
        var rows = Enumerable.Range(1, 10).Select(i => Row(i, "matn")).Append(Row(11, "Employee No")).ToList();

        IdentityImportSheet.Parse(rows).Error.Code.ShouldBe("davomat.import_ustun_topilmadi");
    }

    [Fact]
    public void Chegaradan_bitta_ortiq_qator_rad_etiladi_chegaraning_ozi_qabul_qilinadi()
    {
        var atLimit = Enumerable.Range(1, AttendanceRules.MaxImportRows)
            .Select(i => Row(i + 1, i.ToString(CultureInfo.InvariantCulture)))
            .Prepend(Row(1, "employeeNo"))
            .ToList();

        IdentityImportSheet.Parse(atLimit).Value.Count.ShouldBe(AttendanceRules.MaxImportRows);

        var overLimit = atLimit.Append(Row(AttendanceRules.MaxImportRows + 2, "ortiqcha")).ToList();

        IdentityImportSheet.Parse(overLimit).Error.Code.ShouldBe("davomat.import_qator_kop");
    }
}
