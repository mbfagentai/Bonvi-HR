using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Parsing;

namespace Bonvi.Api.Extensions;

/// <summary>
/// Xato jurnali (fayl) sozlamalari — <c>ErrorLog</c> bo'limi.
/// </summary>
/// <remarks>
/// <para>
/// Qiymatlar <c>docker-compose*.yml</c> orqali <c>.env</c> dan keladi
/// (<c>ErrorLog__Path</c>, <c>ErrorLog__RetentionDays</c>, ...). Bu ataylab:
/// jurnalning YO'LI va SAQLASH MUDDATI — ekspluatatsiya qarori, kod qarori
/// emas. Zaxira nusxalarda ham xuddi shunday (<c>BACKUP_RETENTION_DAYS</c>,
/// <c>infra/backup/backup.sh</c>).
/// </para>
/// </remarks>
public sealed class ErrorLogOptions
{
    /// <summary>Konfiguratsiyadagi bo'lim nomi.</summary>
    public const string SectionName = "ErrorLog";

    /// <summary>
    /// Jurnal fayli yo'li. <b>Bo'sh bo'lsa — jurnal umuman yoqilmaydi.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Standart qiymat ataylab BO'SH, ya'ni «o'chiq». Alohida <c>Enabled</c>
    /// bayrog'i YO'Q: ikkita bir xil ma'noli tugma ertami-kechmi bir-biriga
    /// zid qo'yiladi («yo'l bor, lekin Enabled=false — nega yozmayapti?»).
    /// Bitta sozlama — bitta haqiqat.
    /// </para>
    /// <para>
    /// NEGA STANDART O'CHIQ: <c>Bonvi.Api</c> yig'masi faqat serverda emas,
    /// integratsiya testlarida (<c>WebApplicationFactory</c>) va
    /// <c>dotnet ef</c> buyruqlarida ham ko'tariladi. Standart yo'l qo'yilsa,
    /// bu qisqa umrli jarayonlar ham fayl ochib, tasodifiy joyga yozib
    /// ketardi — jurnal shovqinga to'lardi va xato tafsilotlari kutilmagan
    /// papkalarda paydo bo'lardi. Jurnal FAQAT compose uni ataylab yoqqan
    /// joyda ishlaydi.
    /// </para>
    /// <para>
    /// Yo'l oxiridagi tire (<c>/logs/bonvi-xato-.log</c>) tasodifiy emas:
    /// Serilog aylanma sanani aynan shu joyga qo'yadi va fayl
    /// <c>bonvi-xato-20260828.log</c> bo'lib chiqadi.
    /// </para>
    /// </remarks>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Qaysi darajadan boshlab faylga yoziladi.
    /// </summary>
    /// <remarks>
    /// Standart <c>Warning</c>. <c>Information</c> qo'yilsa har bir so'rov
    /// faylga tushadi: fayl bir kunda to'ladi va — muhimrog'i — shaxsiy
    /// ma'lumot sizib chiqishi mumkin bo'lgan yuza keskin kengayadi.
    /// Jurnalning maqsadi «nosozlikni keyin o'qib tushunish», «hamma narsani
    /// yozib olish» EMAS.
    /// </remarks>
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Warning;

    /// <summary>Necha kunlik jurnal saqlanadi (eskilari avtomatik o'chadi).</summary>
    /// <remarks>
    /// Bu <b>maxfiylik</b> chegarasi, disk chegarasi emas: jurnalda xato
    /// tafsilotlari bor va ular abadiy yotmasligi kerak. Disk chegarasi
    /// alohida — <see cref="MaxFileCount"/> ga qarang.
    /// </remarks>
    public int RetentionDays { get; set; } = 30;

    /// <summary>Bitta fayl shu hajmdan oshsa, yangisiga o'tiladi.</summary>
    public int MaxFileMegabytes { get; set; } = 10;

    /// <summary>
    /// Saqlanadigan fayllarning eng ko'p soni — <b>disk kafolati</b>.
    /// </summary>
    /// <remarks>
    /// Faqat kun bo'yicha chegara yetarli emas: takrorlanuvchi xato (masalan
    /// har soniyada yiqiladigan so'rov) bir kunning o'zida diskni to'ldirib,
    /// butun serverni to'xtatib qo'yishi mumkin. Eng yomon holatda jurnal
    /// egallaydigan joy = <see cref="MaxFileMegabytes"/> ×
    /// <see cref="MaxFileCount"/> (standart: 10 MB × 50 = 500 MB).
    /// </remarks>
    public int MaxFileCount { get; set; } = 50;
}

/// <summary>
/// Xato jurnalini faylga yozishni Serilog konveyeriga ulaydi.
/// </summary>
/// <remarks>
/// <para>
/// <b>NEGA SENTRY EMAS:</b> Sentry tashqi xizmat — hisob, DSN va internetga
/// chiquvchi trafik talab qiladi. Bazada esa haqiqiy odamlarning JSHSHIR,
/// pasport, telefon va oyligi bor; istisno tafsilotini uchinchi tomon
/// serveriga yuborish bu ma'lumotni konteynerdan tashqariga chiqaradi. Bu
/// hajmdagi tizimda (3 HR + rahbar) nosozlikni topish uchun oddiy fayl
/// yetarli.
/// </para>
/// <para>
/// <b>NEGA O'Z FAYL PROVAYDERIM EMAS:</b> loyihada Serilog ALLAQACHON yagona
/// log provayderi (<c>Program.cs</c>, <c>UseSerilogRequestLogging</c>,
/// <c>appsettings.json</c> dagi <c>Serilog</c> bo'limi). O'z provayderim
/// ikkinchi, parallel log yo'lini yaratardi va aylanish, eski faylni
/// o'chirish, fayl bloklanishi, oqim xavfsizligi, flush — hammasini
/// qaytadan yozishga majbur qilardi. <c>Serilog.Sinks.File</c> esa
/// <b>Apache-2.0</b> (ImageSharp'dagi litsenziya xavfi yo'q) va u
/// <c>Serilog.AspNetCore</c> ning bog'liqligi sifatida ALLAQACHON yuklab
/// olingan — ya'ni bu yangi bog'liqlik emas, mavjudini ataylab ishlatish.
/// </para>
/// <para>
/// <b>NEGA <c>GlobalExceptionHandler</c> ICHIDA EMAS, PROVAYDER DARAJASIDA:</b>
/// tutilmagan HTTP istisnolari — nosozliklarning faqat bir qismi. Baza
/// migratsiyasi yiqilsa (<c>DatabaseInitializer</c>), Redis ulanmasa,
/// Kestrel so'rovni umuman qabul qila olmasa yoki ishga tushish paytida
/// sozlama noto'g'ri bo'lsa — bu istisnolar <c>GlobalExceptionHandler</c>
/// dan UMUMAN o'tmaydi, chunki u konveyerning ichida. Aynan shunday
/// xatolarni keyin o'qish eng kerak bo'ladi (konteyner qayta ko'tarilgan
/// va `docker logs` tozalangan holat). Shu sababli jurnal <c>ILogger</c>
/// konveyeriga ulanadi va <c>Warning</c> dan yuqori HAMMA narsani ushlaydi.
/// </para>
/// <para>
/// <b>OQIM XAVFSIZLIGI:</b> Serilog fayl sink'i har bir yozuvni o'z qulfi
/// ostida bajaradi, ya'ni bir vaqtda kelgan yuzlab so'rov bir-birining
/// satrini buzmaydi. <c>shared: false</c> — fayl BITTA jarayon tomonidan
/// ochiladi (API bitta nusxada ishlaydi, replika yo'q);
/// <c>shared: true</c> rejimi har bir yozuvda operatsion tizim qulfini
/// olib-qo'yadi va sezilarli sekin.
/// </para>
/// <para>
/// <b>BUFER YO'Q</b> (<c>buffered: false</c>): buferlangan yozuvda dastur
/// yiqilishidan oldingi OXIRGI yozuv — ya'ni aynan yiqilish sababi —
/// diskka umuman tushmay qolishi mumkin. Xato jurnali uchun bu eng yomon
/// nuqson; tezlik esa bu yerda ahamiyatsiz (soniyasiga bir necha yozuv).
/// </para>
/// </remarks>
public static class ErrorLogExtensions
{
    /// <summary>
    /// Sozlamada yo'l ko'rsatilgan bo'lsa, xato jurnalini faylga ulaydi.
    /// </summary>
    public static LoggerConfiguration WriteToBonviErrorFile(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(loggerConfiguration);
        ArgumentNullException.ThrowIfNull(configuration);

        var sozlama = new ErrorLogOptions();
        configuration.GetSection(ErrorLogOptions.SectionName).Bind(sozlama);

        if (string.IsNullOrWhiteSpace(sozlama.Path))
        {
            return loggerConfiguration;
        }

        // Faylni ochib bo'lmasa (papka yo'q, huquq yetmaydi — production'da
        // API `uid 2000` ostida ishlaydi, `./logs` esa xostda boshqa egaga
        // tegishli bo'lishi mumkin) Serilog istisno TASHLAMAYDI: u sink'ni
        // jimgina «bo'sh» sink'ka almashtiradi. Ya'ni tizim ishlaydi, lekin
        // «xatolar yozilyapti» degan YOLG'ON ishonch paydo bo'ladi — bu
        // jurnalning umuman bo'lmasligidan ham yomon. SelfLog shu nosozlikni
        // stderr'ga chiqaradi va u `docker logs api` da darrov ko'rinadi.
        SelfLog.Enable(xabar => Console.Error.WriteLine(xabar));

        return loggerConfiguration.WriteTo.File(
            // Formatter — jurnalning maxfiylik qopqog'i. Faylga aynan shu
            // sinf yozgan baytlar tushadi, boshqa hech narsa.
            // Tafsilot: ErrorLogFormatter izohiga qarang.
            formatter: new ErrorLogFormatter(),
            path: sozlama.Path,
            restrictedToMinimumLevel: sozlama.MinimumLevel,

            // Kunlik aylanish — fayl nomida sana bo'ladi va «o'sha kuni nima
            // bo'ldi?» degan savolga to'g'ridan-to'g'ri javob beradi.
            rollingInterval: RollingInterval.Day,

            // Hajm bo'yicha ham aylanadi: bitta kun ichidagi takrorlanuvchi
            // xato bitta ulkan faylga aylanib, uni ochib bo'lmay qolmasin.
            rollOnFileSizeLimit: true,
            fileSizeLimitBytes: Math.Max(1, sozlama.MaxFileMegabytes) * 1024L * 1024L,

            // Ikkita mustaqil chegara, ikkita boshqa sabab:
            //   • vaqt — maxfiylik: xato tafsiloti abadiy yotmasligi kerak;
            //   • son  — disk: bir kunlik xato bo'roni serverni to'ldirmasin.
            // Faqat bittasi qo'yilsa, ikkinchi xavf ochiq qoladi.
            retainedFileTimeLimit: TimeSpan.FromDays(Math.Max(1, sozlama.RetentionDays)),
            retainedFileCountLimit: Math.Max(1, sozlama.MaxFileCount),

            // BOM'siz UTF-8: BOM aylangan fayllarni `cat`/`grep` bilan
            // birlashtirganda matn o'rtasida ko'rinmas belgi bo'lib chiqadi.
            // O'zbek lotin/kirill matni uchun UTF-8 ning o'zi shart.
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            shared: false,
            buffered: false);
    }
}

/// <summary>
/// Xato jurnali faylining YAGONA yozuvchisi — maxfiylik qopqog'i.
/// </summary>
/// <remarks>
/// <para>
/// <b>ENG MUHIM QOIDA:</b> bazada haqiqiy odamlarning JSHSHIR, pasport
/// raqami, telefoni va oyligi bor. Xato jurnali — bu ma'lumot sizib
/// chiqishi mumkin bo'lgan joy: istisno matnida so'rov tanasi, SQL
/// parametrlari yoki URL parametrlari bo'lishi mumkin, log chaqiruvining
/// o'zida esa <c>{Login}</c>, <c>{FullName}</c> kabi maydonlar bo'lishi
/// mumkin.
/// </para>
/// <para>
/// <b>OQ RO'YXAT, QORA RO'YXAT EMAS.</b> Bu sinfning markaziy qarori.
/// Qora ro'yxat («bu maydonlarni yozma») faqat BUGUNGI maydonlarni biladi:
/// ertaga kimdir <c>_logger.LogWarning("Xodim {Pinfl} topilmadi", pinfl)</c>
/// deb yozsa, u jimgina faylga tushadi va hech kim sezmaydi. Oq ro'yxatda
/// esa yangi maydon standart holda <b>YOZILMAYDI</b> — u
/// <c>[yashirildi]</c> bo'lib chiqadi. Ya'ni xato «ma'lumot sizib chiqdi»
/// emas, «logda maydon ko'rinmadi» bo'lib namoyon bo'ladi. Ikkinchisini
/// tuzatish oson, birinchisini — orqaga qaytarib bo'lmaydi.
/// </para>
/// <para>
/// Shu sababli faylga TUSHMAYDI: so'rov tanasi (u hech qayerda log
/// qilinmaydi va oq ro'yxatda ham yo'q), SQL matni va parametrlari
/// (EF Core «Failed executing DbCommand» yozuvidagi <c>commandText</c> va
/// <c>parameters</c> maydonlari oq ro'yxatda YO'Q; ustiga EF'da
/// <c>EnableSensitiveDataLogging</c> ham yoqilmagan — buni alohida test
/// qo'riqlaydi), F.I.Sh., telefon, login.
/// </para>
/// <para>
/// Faylga TUSHADI: so'rov usuli va yo'li (qidiruv parametrlarisiz), holat
/// kodi, bajarilish vaqti, foydalanuvchi identifikatori (Guid — u o'zi
/// hech kimni tanitmaydi, lekin «kim ko'rgan» savoliga javob beradi),
/// manba IP (parol tanlash urinishini manbasiz aniqlab bo'lmaydi) va
/// istisnoning to'liq matni — tozalangan holda.
/// </para>
/// <para>
/// <b>OQIM XAVFSIZLIGI:</b> sinf holatsiz (barcha maydonlar
/// <c>static readonly</c>, o'zgaruvchi holat yo'q), shu sababli bir nechta
/// so'rov uni bir vaqtda chaqirsa ham xavfsiz. Faylga yozish ketma-ketligi
/// esa sink'ning o'z qulfi bilan ta'minlanadi.
/// </para>
/// </remarks>
public sealed partial class ErrorLogFormatter : ITextFormatter
{
    /// <summary>Oq ro'yxatga kirmagan maydon shu bilan almashtiriladi.</summary>
    public const string YashirinBelgi = "[yashirildi]";

    /// <summary>Uzun raqamlar ketma-ketligi shu bilan almashtiriladi.</summary>
    public const string RaqamBelgisi = "[raqam]";

    /// <summary>URL dagi qidiruv parametrlari shu bilan almashtiriladi.</summary>
    public const string ParametrBelgisi = "?[parametrlar]";

    /// <summary>
    /// Faylga chiqishga ruxsat etilgan maydonlar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ro'yxatga qo'shishdan oldin bitta savolga javob bering: <b>bu maydon
    /// haqiqiy odamni tanitadimi?</b> Tanitsa — qo'shilmaydi, xato topish
    /// qanchalik qiyinlashsa ham.
    /// </para>
    /// <para>
    /// <c>Manba</c> (IP) ataylab ichkarida: ketma-ket 429 lar parol tanlash
    /// urinishining asosiy belgisi va manbasiz bu yozuv hech narsa bermaydi.
    /// <c>UserId</c> ham ichkarida, lekin <c>UserName</c>/<c>Login</c> emas —
    /// bu tizimda login TELEFON RAQAMI.
    /// </para>
    /// <para>
    /// <c>commandText</c> (EF Core SQL matni) ataylab TASHQARIDA: EF odatda
    /// parametr o'rniga <c>@p0</c> qo'yadi, lekin doim emas — literal
    /// qiymatlar SQL matniga tushib qolishi mumkin. Ish uchun istisnoning
    /// o'zi (masalan <c>23505: duplicate key ... ix_employees_pinfl</c>)
    /// yetarli: u qaysi cheklov buzilganini aytadi, kimning ma'lumotini —
    /// yo'q.
    /// </para>
    /// </remarks>
    private static readonly FrozenSet<string> RuxsatEtilganMaydonlar = new[]
    {
        "SourceContext",  // qaysi sinf yozdi
        "RequestMethod",  // GET / POST — UseSerilogRequestLogging shu nomda beradi
        "RequestPath",    // yo'l — qidiruv parametrlari tozalangan holda
        "StatusCode",     // 500, 409, ...
        "Elapsed",        // millisekund
        "Method",         // GlobalExceptionHandler va TezlikLoglari shu nomni ishlatadi
        "Path",           // GlobalExceptionHandler va TezlikLoglari shu nomni ishlatadi
        "UserId",         // Guid — F.I.Sh. emas
        "TraceId",        // javobdagi traceId bilan bog'lash uchun
        "Manba",          // so'rov kelgan IP (TezlikLoglari)
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Bitta log yozuvini faylga chiqaradi.</summary>
    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(output);

        // Sarlavha satri bitta qatorda: `grep '^2026-08-28'` bilan kun
        // bo'yicha, `grep ' \[ERR\] '` bilan daraja bo'yicha izlash mumkin.
        // Yozuvning qolgan qatorlari ikki bo'sh joy bilan chekinadi — ya'ni
        // qaysi qator YANGI yozuv boshlanishi ekani bir qarashda ko'rinadi
        // (stek izi o'nlab qatordan iborat bo'ladi).
        output.Write(logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
        output.Write(" [");
        output.Write(Daraja(logEvent.Level));
        output.Write("] ");
        output.WriteLine(Yozuvchi(logEvent));

        output.Write("  ");
        output.WriteLine(Xabar(logEvent));

        if (logEvent.Exception is not null)
        {
            // Istisno matni — jurnalning eng qimmatli va ayni paytda eng
            // xavfli qismi: u erkin matn, uning ichida nima borligini hech
            // kim nazorat qilmaydi. Shu sababli unga IKKALA tozalagich ham
            // qo'llanadi (URL parametrlari + uzun raqamlar).
            var matn = TozalaIstisno(logEvent.Exception.ToString());

            foreach (var qator in matn.Split('\n'))
            {
                output.Write("  ");
                output.WriteLine(qator.TrimEnd('\r'));
            }
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Xabarni oq ro'yxat bo'yicha yig'ish
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Xabar shablonini maydonlarni oq ro'yxatdan o'tkazib chizadi.
    /// </summary>
    /// <remarks>
    /// NEGA <c>logEvent.RenderMessage()</c> EMAS: standart chizuvchi
    /// shablondagi HAR QANDAY maydonni qiymati bilan qo'yadi. Ya'ni
    /// <c>"{Login} kirdi"</c> deb yozilgan bitta log chaqiruvi telefon
    /// raqamini faylga tushirardi. Bu yerda shablon token-token o'tiladi
    /// va ruxsat etilmagan maydon o'rniga <see cref="YashirinBelgi"/>
    /// qo'yiladi — shablonning doimiy matni saqlanadi, ya'ni yozuv baribir
    /// o'qiladi va «qaysi joyda nima yashirilgani» ko'rinib turadi.
    /// </remarks>
    private static string Xabar(LogEvent logEvent)
    {
        using var yozuvchi = new StringWriter(CultureInfo.InvariantCulture);

        foreach (var token in logEvent.MessageTemplate.Tokens)
        {
            // Shablonning doimiy matni — uni dasturchi yozgan, xavfsiz.
            if (token is TextToken matn)
            {
                yozuvchi.Write(matn.Text);
                continue;
            }

            if (token is not PropertyToken xossa)
            {
                continue;
            }

            if (!RuxsatEtilganMaydonlar.Contains(xossa.PropertyName)
                || !logEvent.Properties.TryGetValue(xossa.PropertyName, out var qiymat))
            {
                yozuvchi.Write(YashirinBelgi);
                continue;
            }

            yozuvchi.Write(TozalaYol(Chiqar(qiymat, xossa.Format)));
        }

        return yozuvchi.ToString();
    }

    /// <summary>Bitta maydon qiymatini matnga aylantiradi.</summary>
    /// <remarks>
    /// Satr qiymatlari ATAYLAB tirnoqsiz («literal» rejim). Serilog standart
    /// holatda satrni <c>"POST"</c> deb chizadi — konsolda buni ko'rmaymiz,
    /// chunki u yerda <c>{Message:lj}</c> shabloni ishlatiladi. Fayl ham
    /// xuddi shunday o'qilishi kerak: aks holda har bir yo'l va usul
    /// tirnoqqa o'ralib, <c>grep '/api/employees'</c> kabi oddiy izlash
    /// naqshlari ham kutilmagan natija berardi.
    /// Format ko'rsatilgan bo'lsa (masalan <c>{Elapsed:0.0000}</c>) — u
    /// Serilog qoidasi bo'yicha qo'llanadi.
    /// </remarks>
    private static string Chiqar(LogEventPropertyValue qiymat, string? format)
    {
        if (format is null && qiymat is ScalarValue { Value: string satr })
        {
            return satr;
        }

        using var yozuvchi = new StringWriter(CultureInfo.InvariantCulture);
        qiymat.Render(yozuvchi, format, CultureInfo.InvariantCulture);
        return yozuvchi.ToString();
    }

    /// <summary>Log yozuvini kim yozgani (<c>ILogger&lt;T&gt;</c> ning T'si).</summary>
    private static string Yozuvchi(LogEvent logEvent)
        => logEvent.Properties.TryGetValue("SourceContext", out var qiymat)
            && qiymat is ScalarValue { Value: string nom }
            ? nom
            : "Bonvi";

    private static string Daraja(LogEventLevel daraja) => daraja switch
    {
        LogEventLevel.Verbose => "VRB",
        LogEventLevel.Debug => "DBG",
        LogEventLevel.Information => "INF",
        LogEventLevel.Warning => "WRN",
        LogEventLevel.Error => "ERR",
        LogEventLevel.Fatal => "FTL",
        _ => "???",
    };

    // ────────────────────────────────────────────────────────────
    //  Tozalagichlar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// URL dagi qidiruv parametrlarini olib tashlaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>/api/employees?search=Karimov&amp;pinfl=...</c> — yo'lning o'zi
    /// xavfsiz, undan keyingi hamma narsa esa foydalanuvchi kiritgan matn:
    /// familiya, telefon, JSHSHIR. Yo'l saqlanadi (qaysi endpoint yiqilgani
    /// bilinadi), parametrlar butunlay tashlanadi.
    /// </para>
    /// <para>
    /// Bu ikkinchi himoya qatlami: birinchisi — <c>Program.cs</c> dagi
    /// <c>IncludeQueryInRequestPath = false</c>, u Serilog'ning so'rov
    /// logiga parametr QO'SHMASLIGINI aytadi. Ikkalasi ham kerak, chunki
    /// URL faqat o'sha yerdan emas, istisno matnidan ham kelishi mumkin.
    /// </para>
    /// </remarks>
    private static string TozalaYol(string qiymat)
        => SorovParametri().Replace(qiymat, "$1" + ParametrBelgisi);

    /// <summary>
    /// Istisno matnini tozalaydi: URL parametrlari va uzun raqamlar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Istisno matni erkin: <c>ArgumentException("Noto'g'ri JSHSHIR:
    /// 12345678901234")</c> kabi yozuv butun raqamni faylga tushirardi.
    /// Butun matnni yashirish esa jurnalni ma'nosiz qilardi, shu sababli
    /// oraliq yechim: <b>6 va undan ortiq ketma-ket raqam</b> maskalanadi.
    /// </para>
    /// <para>
    /// NEGA AYNAN 6: JSHSHIR — 14 raqam, telefon — 9, pasport raqami — 7,
    /// oylik — 6 xonadan yuqori. Bularning hammasi chegaradan o'tadi. Stek
    /// izidagi qator raqamlari (<c>:line 142</c>), holat kodlari va IP
    /// oktetlari esa 1–4 raqam — ular tegilmasdan qoladi, ya'ni xato joyi
    /// baribir ko'rinadi.
    /// </para>
    /// <para>
    /// NEGA BU TOZALAGICH OQ RO'YXAT QIYMATLARIGA QO'LLANMAYDI: u yerda
    /// <c>traceId</c> (<c>0HN7QK9OD1B2C:00000001</c>) bor va uni maskalash
    /// jurnalni foydalanuvchi ko'rgan xato bilan bog'lash imkonini yo'q
    /// qilardi. Oq ro'yxat maydonlari tuzilmaviy — ularda erkin matn
    /// bo'lmaydi, shu sababli u yerda faqat yo'l tozalanadi.
    /// </para>
    /// </remarks>
    private static string TozalaIstisno(string matn)
        => UzunRaqam().Replace(TozalaYol(matn), RaqamBelgisi);

    /// <summary>Yo'ldan keyingi <c>?...</c> qismi.</summary>
    [GeneratedRegex(
        @"(/[^\s?'""]*)\?[^\s'""]*",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 200)]
    private static partial Regex SorovParametri();

    /// <summary>Olti va undan ortiq ketma-ket raqam.</summary>
    [GeneratedRegex(
        @"[0-9]{6,}",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 200)]
    private static partial Regex UzunRaqam();
}
