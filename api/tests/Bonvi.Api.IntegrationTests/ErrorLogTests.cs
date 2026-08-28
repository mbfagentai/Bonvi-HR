using System.Globalization;
using Bonvi.Api.Extensions;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Parsing;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Xato jurnali: shaxsiy ma'lumot faylga TUSHMASLIGI va fayl aylanishi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu testlar bor.</b> Jurnal — bu ma'lumot sizib chiqishi mumkin
/// bo'lgan joy: bazada haqiqiy odamlarning JSHSHIR, pasport raqami,
/// telefoni va oyligi bor. «Jurnalga shaxsiy ma'lumot tushmaydi» degan
/// qoida faqat izohda yozilgan bo'lsa, u birinchi shoshilinch tuzatishda
/// buziladi va hech kim sezmaydi — sizib chiqqan ma'lumotni esa orqaga
/// qaytarib bo'lmaydi. Shu sababli qoida test bilan qadab qo'yilgan.
/// </para>
/// <para>
/// <b>Nega bu yerda, alohida birlik-test loyihasida emas.</b> Formatter
/// <c>Bonvi.Api</c> yig'masida yotadi va uni ko'radigan yagona test
/// loyihasi shu. Bu testlarning o'ziga PostgreSQL kerak emas
/// (<see cref="BonviApiFactory"/> ishlatilmaydi) — ular vaqtinchalik
/// papkaga yozib, o'sha zahoti o'chiradi.
/// </para>
/// </remarks>
public sealed class ErrorLogFormatterTests
{
    /// <summary>Bir xil bo'lgan shablon tahlilchisi (Serilog o'zi ham shundan foydalanadi).</summary>
    private static readonly MessageTemplateParser Tahlilchi = new();

    // ────────────────────────────────────────────────────────────
    //  Oq ro'yxat
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ruxsat etilgan tuzilmaviy maydonlar o'z qiymati bilan yoziladi —
    /// aks holda jurnal umuman foydasiz bo'lardi.
    /// </summary>
    [Fact]
    public void Oq_royxatdagi_maydonlar_yoziladi()
    {
        var matn = Chizish(
            "Tutilmagan istisno: {Method} {Path}",
            ("Method", "POST"),
            ("Path", "/api/employees"));

        matn.ShouldContain("POST");
        matn.ShouldContain("/api/employees");
        matn.ShouldNotContain(ErrorLogFormatter.YashirinBelgi);

        // Tirnoqsiz: Serilog satrlarni standart holda `"POST"` deb chizadi,
        // formatter esa «literal» rejimda yozadi. Bu jurnalni `grep` bilan
        // izlash uchun muhim — naqshga tirnoq qo'shish kerak emas.
        matn.ShouldNotContain("\"POST\"");
        matn.ShouldNotContain("\"/api/employees\"");
    }

    /// <summary>
    /// Oq ro'yxatda yo'q maydon — hatto dasturchi uni ataylab log qilgan
    /// bo'lsa ham — faylga TUSHMAYDI.
    /// </summary>
    /// <remarks>
    /// Bu qora ro'yxat emas, oq ro'yxat ekanining isboti: bu yerdagi
    /// maydon nomlarining hech biri kodda alohida taqiqlanmagan, ular
    /// shunchaki ro'yxatda YO'Q.
    /// </remarks>
    [Theory]
    [InlineData("Login", "998905088888")]         // login = telefon raqami
    [InlineData("Pinfl", "31234567890123")]       // JSHSHIR
    [InlineData("Passport", "AA1234567")]         // pasport
    [InlineData("FullName", "Karimov Aziz Alisherovich")]
    [InlineData("Phone", "901234567")]
    [InlineData("Salary", "18500000")]
    public void Oq_royxatda_yoq_maydon_yashiriladi(string maydon, string qiymat)
    {
        var matn = Chizish($"Xodim {{{maydon}}} bilan muammo", (maydon, qiymat));

        matn.ShouldNotContain(qiymat);
        matn.ShouldContain(ErrorLogFormatter.YashirinBelgi);

        // Shablonning doimiy matni saqlanadi — yozuv baribir o'qiladi.
        matn.ShouldContain("Xodim");
        matn.ShouldContain("bilan muammo");
    }

    /// <summary>
    /// EF Core ning «Failed executing DbCommand» yozuvidan SQL matni ham,
    /// parametrlari ham faylga tushmaydi.
    /// </summary>
    /// <remarks>
    /// Bu eng xavfli haqiqiy holat: baza so'rovi yiqilganda EF o'z
    /// shablonini <c>Error</c> darajasida yozadi, ya'ni u aynan xato
    /// jurnaliga tushadigan yozuv. Shablon shu yerda EF'dagi bilan bir xil
    /// maydon nomlari bilan takrorlangan.
    /// </remarks>
    [Fact]
    public void EF_sql_matni_va_parametrlari_yashiriladi()
    {
        var matn = Chizish(
            "Failed executing DbCommand ({elapsed}ms) [Parameters=[{parameters}], "
            + "CommandType='{commandType}', CommandTimeout='{commandTimeout}']{newLine}{commandText}",
            ("elapsed", "12"),
            ("parameters", "@p0='31234567890123', @p1='Karimov Aziz'"),
            ("commandType", "Text"),
            ("commandTimeout", "30"),
            ("newLine", "\n"),
            ("commandText", "SELECT e.pinfl FROM employees AS e WHERE e.phone = '901234567'"));

        matn.ShouldNotContain("31234567890123");
        matn.ShouldNotContain("Karimov");
        matn.ShouldNotContain("901234567");
        matn.ShouldNotContain("SELECT");

        // «Failed executing DbCommand» qismi qoladi — nima yiqilgani bilinadi.
        matn.ShouldContain("Failed executing DbCommand");
    }

    // ────────────────────────────────────────────────────────────
    //  URL parametrlari
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Yo'l saqlanadi, qidiruv parametrlari — yo'q. Qidiruv satrida
    /// familiya, telefon yoki JSHSHIR bo'lishi mumkin.
    /// </summary>
    [Fact]
    public void Url_parametrlari_yoldan_olib_tashlanadi()
    {
        var matn = Chizish(
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode}",
            ("RequestMethod", "GET"),
            ("RequestPath", "/api/employees?search=Karimov&pinfl=31234567890123"),
            ("StatusCode", "500"));

        matn.ShouldNotContain("Karimov");
        matn.ShouldNotContain("31234567890123");
        matn.ShouldContain("/api/employees" + ErrorLogFormatter.ParametrBelgisi);

        // Holat kodi 3 xonali — uzun raqam tozalagichiga tushmaydi.
        matn.ShouldContain("500");
    }

    // ────────────────────────────────────────────────────────────
    //  Istisno matni
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Istisno matni erkin yoziladi, shu sababli undagi uzun raqamlar
    /// (JSHSHIR, telefon, pasport, oylik) maskalanadi.
    /// </summary>
    [Fact]
    public void Istisno_matnidagi_uzun_raqamlar_maskalanadi()
    {
        var istisno = new InvalidOperationException(
            "JSHSHIR 31234567890123 va telefon 901234567 uchun yozuv topilmadi");

        var matn = Chizish("Tutilmagan istisno: {Method} {Path}", istisno, ("Method", "GET"), ("Path", "/api/employees"));

        matn.ShouldNotContain("31234567890123");
        matn.ShouldNotContain("901234567");
        matn.ShouldContain(ErrorLogFormatter.RaqamBelgisi);

        // Istisno turi va matnining qolgan qismi qoladi — xatoni topish uchun
        // aynan shu kerak.
        matn.ShouldContain("InvalidOperationException");
        matn.ShouldContain("yozuv topilmadi");
    }

    /// <summary>
    /// Istisno matnidagi URL ham tozalanadi — u faqat so'rov logidan emas,
    /// istisnoning o'zidan ham kelishi mumkin.
    /// </summary>
    [Fact]
    public void Istisno_matnidagi_url_parametrlari_tozalanadi()
    {
        var istisno = new HttpRequestException(
            "So'rov yiqildi: /api/employees?search=Karimov%20Aziz&page=1");

        var matn = Chizish("Tutilmagan istisno: {Method} {Path}", istisno, ("Method", "GET"), ("Path", "/api/employees"));

        matn.ShouldNotContain("Karimov");
        matn.ShouldContain("/api/employees" + ErrorLogFormatter.ParametrBelgisi);
    }

    /// <summary>
    /// Qisqa raqamlar tegilmaydi: stek izidagi qator raqami va IP oktetlari
    /// maskalansa, jurnal xatoni topish uchun yaroqsiz bo'lib qolardi.
    /// </summary>
    [Fact]
    public void Qisqa_raqamlar_saqlanadi()
    {
        var istisno = new InvalidOperationException(
            "EmployeeService.cs:line 142 — manba 192.168.1.10");

        var matn = Chizish("Tutilmagan istisno: {Method} {Path}", istisno, ("Method", "GET"), ("Path", "/api/employees"));

        matn.ShouldContain("line 142");
        matn.ShouldContain("192.168.1.10");
    }

    /// <summary>
    /// <c>traceId</c> buzilmaydi — u foydalanuvchi ko'rgan xatoni jurnaldagi
    /// yozuv bilan bog'laydigan yagona ip.
    /// </summary>
    /// <remarks>
    /// Uzun raqam tozalagichi ATAYLAB faqat istisno matniga qo'llanadi:
    /// <c>0HN7QK9OD1B2C:00000001</c> dagi sakkiz nol maskalansa, bog'lanish
    /// yo'qolardi.
    /// </remarks>
    [Fact]
    public void TraceId_buzilmaydi()
    {
        var matn = Chizish("So'rov: {Method} {Path} {TraceId}",
            ("Method", "GET"),
            ("Path", "/api/employees"),
            ("TraceId", "0HN7QK9OD1B2C:00000001"));

        matn.ShouldContain("0HN7QK9OD1B2C:00000001");
    }

    /// <summary>Daraja va yozuvchi sinf nomi sarlavha satrida bo'ladi.</summary>
    /// <remarks>
    /// Sarlavha bitta qatorda: `grep ' \[ERR\] '` bilan faqat xatolarni
    /// ajratib olish mumkin bo'lsin.
    /// </remarks>
    [Fact]
    public void Sarlavha_satrida_daraja_va_manba_boladi()
    {
        var matn = Chizish(
            "Tutilmagan istisno: {Method} {Path}",
            LogEventLevel.Error,
            exception: null,
            ("SourceContext", "Bonvi.Api.Common.GlobalExceptionHandler"),
            ("Method", "GET"),
            ("Path", "/api/employees"));

        matn.ShouldContain("[ERR]");
        matn.ShouldContain("Bonvi.Api.Common.GlobalExceptionHandler");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private static string Chizish(string shablon, params (string Nom, string Qiymat)[] maydonlar)
        => Chizish(shablon, LogEventLevel.Error, null, maydonlar);

    private static string Chizish(string shablon, Exception istisno, params (string Nom, string Qiymat)[] maydonlar)
        => Chizish(shablon, LogEventLevel.Error, istisno, maydonlar);

    private static string Chizish(
        string shablon,
        LogEventLevel daraja,
        Exception? exception,
        params (string Nom, string Qiymat)[] maydonlar)
    {
        var yozuv = new LogEvent(
            DateTimeOffset.UnixEpoch,
            daraja,
            exception,
            Tahlilchi.Parse(shablon),
            maydonlar.Select(m => new LogEventProperty(m.Nom, new ScalarValue(m.Qiymat))));

        using var chiqish = new StringWriter(CultureInfo.InvariantCulture);
        new ErrorLogFormatter().Format(yozuv, chiqish);
        return chiqish.ToString();
    }
}

/// <summary>
/// Xato jurnalining haqiqiy faylga yozilishi: yoqish/o'chirish, aylanish,
/// saqlash chegarasi va bir vaqtda yozish.
/// </summary>
/// <remarks>
/// Formatter birlik-test bilan qoplangan, bu yerda esa Serilog konveyeri
/// bilan BIRGA — ya'ni «sozlama noto'g'ri ulangan» xatosi ham tutiladi.
/// Har bir test o'z vaqtinchalik papkasida ishlaydi va tugagach o'chiradi.
/// </remarks>
public sealed class ErrorLogFileTests : IDisposable
{
    private readonly string _papka =
        Path.Combine(
            Path.GetTempPath(),
            "bonvi-log-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12]);

    public void Dispose()
    {
        if (Directory.Exists(_papka))
        {
            Directory.Delete(_papka, recursive: true);
        }
    }

    /// <summary>
    /// Yo'l berilmasa jurnal UMUMAN yoqilmaydi.
    /// </summary>
    /// <remarks>
    /// Bu shunchaki qulaylik emas: <c>Bonvi.Api</c> integratsiya testlarida
    /// va <c>dotnet ef</c> buyruqlarida ham ko'tariladi. Standart yo'l
    /// bo'lganda bu qisqa umrli jarayonlar ham fayl ochib, tasodifiy joyga
    /// yozib ketardi.
    /// </remarks>
    [Fact]
    public void Yol_berilmasa_fayl_yaratilmaydi()
    {
        using (var logger = Jurnal(yol: null))
        {
            logger.Error("Tutilmagan istisno: {Method} {Path}", "GET", "/api/employees");
        }

        Directory.Exists(_papka).ShouldBeFalse();
    }

    /// <summary>
    /// Uchi-uchiga: haqiqiy fayl yoziladi va unda shaxsiy ma'lumot YO'Q.
    /// </summary>
    /// <remarks>
    /// Bu testning ma'nosi — «faylga aynan nima tushdi?» degan savolga
    /// bevosita javob berish. Formatter alohida sinalgan bo'lsa ham, uni
    /// sink'ga ulashda xato qilish mumkin (masalan <c>formatter:</c> o'rniga
    /// <c>outputTemplate:</c> berib qo'yish) — o'shanda hamma himoya
    /// jimgina chetlab o'tilardi.
    /// </remarks>
    [Fact]
    public void Faylga_shaxsiy_malumot_tushmaydi()
    {
        var istisno = new InvalidOperationException(
            "JSHSHIR 31234567890123 uchun /api/employees?search=Karimov so'rovi yiqildi");

        using (var logger = Jurnal())
        {
            logger.Error(
                istisno,
                "Xodim {Login} uchun tutilmagan istisno: {Method} {Path}",
                "998905088888",
                "GET",
                "/api/employees");
        }

        var matn = ButunMatn();

        // Sizib chiqishi mumkin bo'lgan hamma narsa
        matn.ShouldNotContain("31234567890123");   // JSHSHIR (istisno matnida)
        matn.ShouldNotContain("998905088888");     // login = telefon (log maydonida)
        matn.ShouldNotContain("Karimov");          // familiya (URL parametrida)
        matn.ShouldNotContain("search=");

        // Ish uchun kerak bo'lgan hamma narsa
        matn.ShouldContain("/api/employees");
        matn.ShouldContain("InvalidOperationException");
        matn.ShouldContain(ErrorLogFormatter.YashirinBelgi);
        matn.ShouldContain(ErrorLogFormatter.RaqamBelgisi);
    }

    /// <summary>
    /// Chegaradan past darajadagi yozuvlar faylga tushmaydi.
    /// </summary>
    /// <remarks>
    /// Standart chegara <c>Warning</c>: <c>Information</c> yoqilsa har bir
    /// so'rov faylga tushadi va shaxsiy ma'lumot sizib chiqishi mumkin
    /// bo'lgan yuza keskin kengayadi.
    /// </remarks>
    [Fact]
    public void Chegaradan_past_yozuvlar_faylga_tushmaydi()
    {
        using (var logger = Jurnal())
        {
            logger.Information("Oddiy so'rov: {Method} {Path}", "GET", "/api/employees");
            logger.Warning("Tezlik cheklovi ishga tushdi: {Method} {Path}", "POST", "/api/auth/login");
        }

        var matn = ButunMatn();

        matn.ShouldNotContain("Oddiy so'rov");
        matn.ShouldContain("Tezlik cheklovi");
    }

    /// <summary>Fayl nomida sana bo'ladi — kunlik aylanish.</summary>
    /// <remarks>
    /// Sana fayl nomida bo'lgani uchun «o'sha kuni nima bo'ldi?» degan
    /// savolga jurnalni ochmasdan javob berish mumkin.
    /// </remarks>
    [Fact]
    public void Fayl_nomida_sana_boladi()
    {
        using (var logger = Jurnal())
        {
            logger.Error("Tutilmagan istisno: {Method} {Path}", "GET", "/api/employees");
        }

        var nom = Path.GetFileName(Fayllar().Single());

        nom.ShouldStartWith("bonvi-xato-");
        nom.ShouldEndWith(".log");
        nom.ShouldContain(DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Hajm chegarasi va fayllar soni chegarasi HAQIQATAN ishlaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bu disk kafolati: bir kunning o'zida takrorlanuvchi xato
    /// (har so'rovda yiqiladigan endpoint) diskni to'ldirib, butun serverni
    /// to'xtatib qo'yishi mumkin. Faqat kunlik aylanish bunga qarshi hech
    /// narsa qilmaydi — shu sababli hajm bo'yicha aylanish va fayllar soni
    /// chegarasi ham bor.
    /// </para>
    /// <para>
    /// Testda chegara 1 MB × 2 fayl qilib qo'yiladi va ~4 MB yoziladi:
    /// chegara ishlamasa papkada 4+ fayl qolardi.
    /// </para>
    /// </remarks>
    [Fact]
    public void Hajm_oshsa_fayl_aylanadi_va_eskisi_ochiriladi()
    {
        var uzunMatn = new string('x', 2000);

        using (var logger = Jurnal(maxFileMegabytes: 1, maxFileCount: 2))
        {
            for (var i = 0; i < 2500; i++)
            {
                // Matn shablonning DOIMIY qismida — ya'ni u yashirilmaydi
                // va fayl haqiqatan to'ladi.
                logger.Error("To'ldirish " + uzunMatn + ": {Method} {Path}", "GET", "/api/employees");
            }
        }

        Fayllar().Length.ShouldBeLessThanOrEqualTo(2);
    }

    /// <summary>
    /// Bir vaqtda kelgan yuzlab so'rov bir-birining satrini buzmaydi.
    /// </summary>
    /// <remarks>
    /// Bu «o'z fayl provayderim» o'rniga Serilog tanlanganining asosiy
    /// sabablaridan biri: qulflash, fayl bloklanishi va flush — hammasi
    /// sink ichida hal qilingan. Test shuni tasdiqlaydi: har bir yozuv
    /// ikki qator (sarlavha + xabar) va ularning soni aniq.
    /// </remarks>
    [Fact]
    public void Bir_vaqtda_yozish_satrlarni_buzmaydi()
    {
        const int Soni = 400;

        using (var logger = Jurnal())
        {
            Parallel.For(0, Soni, i =>
                logger.Error("Tutilmagan istisno: {Method} {Path}", "GET", "/api/employees"));
        }

        var qatorlar = File.ReadAllLines(Fayllar().Single());

        // Har bir yozuv: sarlavha (sana bilan boshlanadi) + chekingan xabar.
        qatorlar.Length.ShouldBe(Soni * 2);
        qatorlar.Count(q => q.StartsWith("19", StringComparison.Ordinal)
            || q.StartsWith("20", StringComparison.Ordinal)).ShouldBe(Soni);
        qatorlar.Count(q => q.StartsWith("  ", StringComparison.Ordinal)).ShouldBe(Soni);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private Serilog.Core.Logger Jurnal(
        string? yol = "bonvi-xato-.log",
        int maxFileMegabytes = 10,
        int maxFileCount = 50)
    {
        var sozlama = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ErrorLog:Path"] = yol is null ? string.Empty : Path.Combine(_papka, yol),
                ["ErrorLog:MinimumLevel"] = "Warning",
                ["ErrorLog:RetentionDays"] = "30",
                ["ErrorLog:MaxFileMegabytes"] = maxFileMegabytes.ToString(CultureInfo.InvariantCulture),
                ["ErrorLog:MaxFileCount"] = maxFileCount.ToString(CultureInfo.InvariantCulture),
            })
            .Build();

        // Konveyer chegarasi ataylab eng past: shu holda faylga nima
        // tushishini FAQAT `ErrorLog:MinimumLevel` hal qiladi va test
        // aynan o'sha sozlamani tekshiradi.
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteToBonviErrorFile(sozlama)
            .CreateLogger();
    }

    private string[] Fayllar()
        => Directory.Exists(_papka)
            ? [.. Directory.GetFiles(_papka, "*.log").Order(StringComparer.Ordinal)]
            : [];

    private string ButunMatn()
        => string.Concat(Fayllar().Select(File.ReadAllText));
}

/// <summary>
/// EF Core maxfiy ma'lumot logi YOQILMAGANLIGINI qo'riqlaydi.
/// </summary>
/// <remarks>
/// <para>
/// <c>EnableSensitiveDataLogging()</c> yoqilsa, EF har bir so'rov
/// parametrini (JSHSHIR, telefon, oylik) log yozuviga QIYMATI bilan
/// qo'shadi. Xato jurnalidagi oq ro'yxat bu maydonlarni baribir yashiradi,
/// lekin ular KONSOL logiga (`docker logs`) va boshqa har qanday
/// provayderga to'liq chiqib ketardi.
/// </para>
/// <para>
/// Ya'ni bu ikkinchi mudofaa chizig'i emas — BIRINCHISI. Formatterning
/// oq ro'yxati esa uning orqasidagi to'r.
/// </para>
/// </remarks>
public sealed class EfSensitiveLoggingTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public EfSensitiveLoggingTests(BonviApiFactory factory) => _factory = factory;

    [Fact]
    public void Maxfiy_malumot_logi_ochiq_bolishi_shart()
    {
        using var scope = _factory.Services.CreateScope();

        var sozlamalar = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
        var yadro = sozlamalar.FindExtension<CoreOptionsExtension>();

        yadro.ShouldNotBeNull();
        yadro.IsSensitiveDataLoggingEnabled.ShouldBeFalse();
        yadro.DetailedErrorsEnabled.ShouldBeFalse();
    }
}
