using System.Text.Json.Serialization;
using System.Globalization;
using Bonvi.Api.Common;
using Bonvi.Api.Endpoints;
using Bonvi.Api.Extensions;
using Bonvi.Api.Startup;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Common;
using Bonvi.Infrastructure;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

// ============================================================
//  BONVI HR — API kirish nuqtasi (kompozitsiya ildizi)
//  Bu yerda qatlamlar bir-biriga ulanadi. Biznes mantiq bu yerda YO'Q.
// ============================================================

var builder = WebApplication.CreateBuilder(args);

// ---------- Log ----------
// Ikkita chiqish, ikkita maqsad:
//   • Konsol — «hozir nima bo'lyapti» (docker logs api). Konteyner qayta
//     ishga tushsa yo'qoladi, shu sababli nosozlikni KEYIN tekshirish uchun
//     yaramaydi;
//   • Fayl — «o'tgan payshanba kechqurun nima yiqilgan edi». Faqat
//     `ErrorLog__Path` berilgan joyda (compose) yoqiladi va faylga faqat
//     ErrorLogFormatter ruxsat bergan maydonlar tushadi.
//     Batafsil: Extensions/ErrorLogExtensions.cs.
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .WriteToBonviErrorFile(context.Configuration));

// ---------- Qatlamlar ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---------- Veb ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// ---------- Xavfsizlik va yuklamaga chidamlilik ----------
builder.AddBonviSecurity();
builder.AddBonviPerformance();
builder.Services.AddBonviRateLimiting(builder.Configuration, builder.Environment);

builder.Services.Configure<BootstrapOptions>(
    builder.Configuration.GetSection(BootstrapOptions.SectionName));
builder.Services.AddHostedService<DatabaseInitializer>();

builder.Services.AddBonviAuthentication(builder.Configuration);
builder.Services.AddBonviAuthorization();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Sanoq qiymatlari JSON'da SON emas, NOM bo'lib yuradi.
//
// Standart holatda `EmploymentStatus.Terminated` javobda `4` bo'lib chiqadi
// va so'rovda ham faqat `4` qabul qilinadi. Ikkita jiddiy oqibati bor:
//
//   • mijoz `{"type":"Blacklist"}` yuborsa 400 oladi — nomni JSON tanasida
//     ishlatib bo'lmasdi, holbuki so'rov satrida (`?type=Blacklist`) ishlardi.
//     Bitta API ikki xil qoidada yurardi;
//   • sanoqqa yangi qiymat O'RTAGA qo'shilsa, barcha eski raqamlar siljib
//     ketardi va bu hech qanday xatosiz, jimgina noto'g'ri ma'lumot berardi.
//
// Nom bilan ikkalasi ham yo'qoladi va javob o'zini o'zi tushuntiradi.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Parametrni o'qib bo'lmaganda minimal API istisno tashlasin — BARCHA muhitda.
//
// Standart holatda bu faqat Development'da yoqiladi, qolgan joyda esa mijozga
// TANASIZ 400 qaytadi: na sabab, na `traceId`. Ya'ni xato aynan ishlab
// chiqarishda tushunarsiz bo'lardi. Istisno tashlansa, u
// `GlobalExceptionHandler` ga tushadi va hamma joyda bir xil ProblemDetails
// beriladi.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("postgres");

// Bo'sh va probelli yozuvlar OLIB TASHLANADI. Sabab amaliy: production
// compose'da manba `.env` dagi o'zgaruvchi va u berilmasa massivga bo'sh
// satr tushadi — `WithOrigins("")` esa ArgumentException bilan butun
// dasturni ishga tushirmasdi. Bo'sh ro'yxat ham to'g'ri holat: prod'da
// web va API BIR XIL domenda (Caddy `/api/*` ni proksi qiladi), ya'ni
// CORS umuman ishlamaydi va hech qanday tashqi manbaga ruxsat kerak emas.
var corsOrigins = (builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim().TrimEnd('/'))
    .ToArray();

if (corsOrigins.Length == 0 && builder.Environment.IsDevelopment())
{
    corsOrigins = ["http://localhost:7173"];
}

builder.Services.AddCors(options => options.AddPolicy("web", policy => policy
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    // `AllowAnyHeader()` faqat SO'ROV sarlavhalariga tegishli — u klient
    // nima yubora olishini belgilaydi. Javob sarlavhalari esa boshqa
    // ro'yxat: CORS ostida brauzer JS ga faqat oltita «xavfsiz» sarlavhani
    // ko'rsatadi, qolganini `Access-Control-Expose-Headers` da ataylab
    // ochish kerak. Aks holda Excel eksporti javobidagi
    // `Content-Disposition` klientga umuman yetib bormaydi va u serverning
    // bergan fayl nomini (`Bonvi_xodimlar_2026-08-26.xlsx`) O'QIY OLMAYDI.
    // Xodim hujjatlari bu muammoni chetlab o'tgan — u yerda nom DTO da
    // keladi; eksportda esa DTO yo'q, javob to'g'ridan-to'g'ri fayl baytlari.
    .WithExposedHeaders("Content-Disposition")
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

// ---------- Konveyer (pipeline) ----------
// Konveyer tartibi ataylab shunday:
//   1. Xavfsizlik sarlavhalari va forwarded headers — eng birinchi,
//      chunki xato javoblarida ham sarlavhalar bo'lishi kerak
//   2. Istisnolarni tutish
//   3. Log — haqiqiy IP allaqachon aniqlangan bo'lishi uchun
app.UseBonviSecurity();

app.UseExceptionHandler();

// So'rov logi. `IncludeQueryInRequestPath` ATAYLAB va OSHKORA `false`:
// bu Serilog'ning standart qiymati, lekin u shu tizimda maxfiylik talabi —
// `?search=Karimov&pinfl=...` dagi hamma narsa foydalanuvchi kiritgan
// shaxsiy ma'lumot. Standart qiymatga ishonib qo'yib yuborilsa, kimdir uni
// «loglar tushunarsiz» deb yoqib qo'yishi va shaxsiy ma'lumot jimgina
// faylga oqib tushishi mumkin. Ikkinchi qatlam — ErrorLogFormatter'dagi
// yo'l tozalagichi.
app.UseSerilogRequestLogging(options => options.IncludeQueryInRequestPath = false);

if (app.Environment.IsDevelopment())
{
    // Fallback siyosati barcha endpoint'ni himoyalaydi, shu sababli OpenAPI
    // hujjati ham 401 qaytarardi va `./bonvi gen` (Orval) uni o'qiy olmasdi.
    // Bu blok butunlay `IsDevelopment()` ichida — production'da hujjat yo'q.
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options => options
        .WithTitle("Bonvi HR API")
        .WithTheme(ScalarTheme.BluePlanet))
        .AllowAnonymous();
}

app.UseCors("web");

// Tartib muhim: avval «kimsan» (authentication),
// keyin «nimaga ruxsating bor» (authorization).
app.UseAuthentication();
app.UseAuthorization();

// Tezlik cheklovi autentifikatsiyadan KEYIN: cheklov foydalanuvchi
// bo'yicha hisoblanadi, u aniqlanmagan bo'lsa — IP bo'yicha.
app.UseRateLimiter();

app.MapBonviHealthChecks();

// ---------- Endpoint guruhlari ----------
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapCatalogEndpoints();
app.MapEmployeeEndpoints();
app.MapEmploymentEndpoints();
app.MapIncentiveEndpoints();
app.MapEmployeeDocumentEndpoints();
app.MapExitInterviewEndpoints();
app.MapIncidentEndpoints();

// Rekruting (2-bosqich): voronka bosqichlari → vakansiya → nomzod →
// ishga olish. Oxirgi qadam 1-bosqichning ishga qabul oqimiga ulanadi.
app.MapPipelineStageEndpoints();
app.MapVacancyEndpoints();
app.MapCandidateEndpoints();

// Bildirishnomalar — yuqori paneldagi qo'ng'iroq. Bironta bo'limga
// tegishli emas: sinov, xodim va nomzod ma'lumotlarini BIRGA
// o'qiydi va har bir foydalanuvchiga o'z ro'yxatini beradi.
app.MapNotificationEndpoints();

app.MapSystemEndpoints();
app.MapDashboardEndpoints();
app.MapReportsEndpoints();

// Tizim sozlamalari — bot matni va bildirishnoma chegaralari.
// «settings.manage» ostida va u faqat super administratorda
// (sababi `Permissions.SettingsManage` izohida). Ichida bitta istisno
// bor: `/api/settings/bot` botning ruxsati bilan ochiladi.
app.MapSettingsEndpoints();

app.MapGet("/", () => Results.Ok(new
{
    servis = "Bonvi HR API",
    versiya = "0.1.0",
    muhit = app.Environment.EnvironmentName,
}))
.AllowAnonymous()
.WithSummary("Servis holati")
.ExcludeFromDescription();

await app.RunAsync().ConfigureAwait(false);

/// <summary>Integratsiya testlari uchun (<c>WebApplicationFactory&lt;Program&gt;</c>).</summary>
public partial class Program
{
    protected Program()
    {
    }
}
