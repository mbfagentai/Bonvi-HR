using System.Text;
using Bonvi.Etl.Reporting;

namespace Bonvi.Etl.Tests;

/// <summary>
/// Hisobot CSV formatining qoidalari.
/// </summary>
/// <remarks>
/// Hisobot — bu vositaning asosiy mahsuloti, bazadagi yozuvlar emas.
/// Uni HR Excel'da ochadi va bevosita ish ro'yxati sifatida ishlatadi,
/// shu sababli fayl formati testda qadalgan.
/// </remarks>
public sealed class EtlReportTests
{
    [Fact]
    public async Task Csv_BOM_bilan_yoziladi()
    {
        // BOM'siz Excel kirill matnini krakozyabra qilib ochadi va
        // hisobot o'qib bo'lmas holga keladi.
        var path = Path.Combine(Path.GetTempPath(), $"etl-{Guid.NewGuid():N}.csv");

        try
        {
            var report = new EtlReport();
            report.Add("employees", "91 Умумий ходимлар", 12, "Каримов Азиз", "JSHSHIR yo'q", "", EtlAction.Flagged);

            await report.WriteCsvAsync(path, TestContext.Current.CancellationToken);

            var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

            bytes.Length.ShouldBeGreaterThan(3);
            bytes[0].ShouldBe((byte)0xEF);
            bytes[1].ShouldBe((byte)0xBB);
            bytes[2].ShouldBe((byte)0xBF);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Vergul_va_qoshtirnoq_qalqonlanadi()
    {
        var path = Path.Combine(Path.GetTempPath(), $"etl-{Guid.NewGuid():N}.csv");

        try
        {
            var report = new EtlReport();
            report.Add("events", "91.3", 7, "Каримов, Азиз", "izoh", "«25 П», \"asosiy\"", EtlAction.Flagged);

            await report.WriteCsvAsync(path, TestContext.Current.CancellationToken);

            var text = await File.ReadAllTextAsync(path, Encoding.UTF8, TestContext.Current.CancellationToken);

            text.ShouldContain("\"Каримов, Азиз\"");
            text.ShouldContain("\"\"asosiy\"\"");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Maydon_ichidagi_yangi_qator_boshliqqa_almashtiriladi()
    {
        // Yangi qator maydon ichida qolsa, CSV'ni o'qiydigan har bir dastur
        // uni boshqacha talqin qiladi va jadval siljib ketadi.
        var path = Path.Combine(Path.GetTempPath(), $"etl-{Guid.NewGuid():N}.csv");

        try
        {
            var report = new EtlReport();
            report.Add("flags", "91.10", 5, "Каримов", "sabab", "birinchi\nikkinchi", EtlAction.Flagged);

            await report.WriteCsvAsync(path, TestContext.Current.CancellationToken);

            var lines = await File.ReadAllLinesAsync(path, TestContext.Current.CancellationToken);

            lines.Length.ShouldBe(2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Mavjud_yozuv_haqidagi_satr_hisobotga_tushmaydi()
    {
        // Hisobot — HR uchun ish ro'yxati. «Bu yozuv allaqachon bor edi»
        // hech qanday ish talab qilmaydi, lekin ikkinchi yugurishda bunday
        // satrlar 7000 dan oshadi va haqiqiy muammolarni ko'mib yuborardi.
        var report = new EtlReport();

        report.Add("employments", "91.2", 5, "Каримов", "qabul allaqachon bor", "", EtlAction.AlreadyExists);
        report.Add("employments", "91.2", 6, "Тошматов", "JSHSHIR yo'q", "", EtlAction.Flagged);

        report.Issues.Count.ShouldBe(1);
        report.AlreadyExisting.ShouldBe(1);
    }

    [Fact]
    public void Qadam_jamlanmasi_bir_marta_yaratiladi()
    {
        var report = new EtlReport();

        var first = report.Step("employees");
        first.Read = 10;

        var second = report.Step("employees");

        second.ShouldBeSameAs(first);
        report.Steps.Count.ShouldBe(1);
    }

    [Fact]
    public void Qadamlar_bajarilgan_tartibda_saqlanadi()
    {
        var report = new EtlReport();

        _ = report.Step("employees");
        _ = report.Step("employments");
        _ = report.Step("events");

        report.Steps.Select(step => step.Name).ShouldBe(["employees", "employments", "events"]);
    }
}
