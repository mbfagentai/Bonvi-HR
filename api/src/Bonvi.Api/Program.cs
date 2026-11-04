using System.Text.Json.Serialization;
using System.Globalization;
using Bonvi.Api.Common;
using Bonvi.Api.Endpoints;
using Bonvi.Api.Extensions;
using Bonvi.Api.Mcp;
using Bonvi.Api.Startup;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Ingest;
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
//
// Qurilma tokeni yo'lda keladi (`/api/attendance/ingest/{token}`) va yo'l
// logning eng oddiy maydoni. `IngestTokenScrubber` uni HAR IKKALA chiqishdan
// oldin yashiradi — shu sababli u `FromLogContext` dan KEYIN turadi.
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.With<IngestTokenScrubber>()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .WriteToBonviErrorFile(context.Configuration));

// ---------- Qatlamlar ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---------- Veb ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Xodim ilovasining so'rovchisi — `ICurrentUser` dan ALOHIDA port.
// Ikkalasi bir-birini ko'rmaydi: xodim tokenida `NameIdentifier` da'vosi
// yo'q, ya'ni audit jurnali mavjud bo'lmagan foydalanuvchiga ishora
// qilmaydi (sababi `EmployeeJwtTokenService` izohida).
builder.Services.AddScoped<ICurrentEmployee, CurrentEmployee>();

// Audit jurnalining «qaysi amal, qaysi guruh, qayerdan» va «kim» portlari —
// HTTP so'rovidan (SPEC-rollar-va-audit §7.4). Bitta nusxa ikkala portga:
// ishga tushishdagi `IAuditScope.Use(...)` bergan nom `IRequestContext` da
// ko'rinishi kerak.
builder.Services.AddScoped<HttpRequestContext>();
builder.Services.AddScoped<IRequestContext>(provider => provider.GetRequiredService<HttpRequestContext>());
builder.Services.AddScoped<IAuditScope>(provider => provider.GetRequiredService<HttpRequestContext>());
builder.Services.AddScoped<IAuditActorSource, HttpAuditActorSource>();

// Qurilma qabuli jurnali — Application porti, lekin yozuvga so'rov usuli
// va yo'li kerak, ya'ni amalga oshirish shu qatlamda (`ICurrentUser` kabi).
// Singleton: holatsiz, `IHttpContextAccessor` ning o'zi ham singleton, va
// uni singleton Hikvision tahlilchisi ishlatadi.
builder.Services.AddSingleton<IAttendanceIngestJournal, AttendanceIngestJournal>();

// ---------- Xavfsizlik va yuklamaga chidamlilik ----------
builder.AddBonviSecurity();
builder.AddBonviPerformance();
builder.Services.AddBonviRateLimiting(builder.Configuration, builder.Environment);

builder.Services.Configure<BootstrapOptions>(
    builder.Configuration.GetSection(BootstrapOptions.SectionName));
builder.Services.AddHostedService<DatabaseInitializer>();

// Onlayn anketaning bot tekshiruvi: faqat bitta kalit berilgan bo'lsa (tekshiruv o'chiq qoladi) —
// ishga tushishda ogohlantirish (SPEC-planshet-anketa §2.3.2).
builder.Services.AddHostedService<TurnstileStartupCheck>();

// ---------- Fon vazifalari ----------
// Hangfire serveri + navbat porti; `Testing` da — sinxron navbat, server yo'q.
// Bazani tayyorlagichdan KEYIN: xost xizmatlari ro'yxat tartibida ishga
// tushadi va vazifalar migratsiya qo'llanmagan bazaga yugurmasin.
builder.AddBonviBackgroundJobs();

builder.Services.AddBonviAuthentication(builder.Configuration);
builder.Services.AddBonviAuthorization();

// Tashqi API — IKKINCHI autentifikatsiya sxemasi va uning O'Z siyosati.
// Ular ichki oqimga tegmaydi: standart sxema `Bearer` bo'lib qoladi,
// ommaviy siyosat esa faqat `X-API-Key` ni sanaydi. Ikkala yo'nalishning
// yopiqligi test bilan qo'riqlanadi (sabab `ApiKeyAuthenticationHandler`
// izohida).
builder.Services.AddBonviApiKeyScheme();
builder.Services.AddBonviPublicApiAuthorization();

// AI ulanish (MCP) — UCHINCHI autentifikatsiya sxemasi (shaffof OAuth tokeni),
// o'z siyosati, o'z tezlik cheklovlari va o'qish bazasi (`docs/SPEC-mcp.md`).
// Ichki oqimga tegmaydi: MCP tokeni boshqa hech bir endpointda qabul qilinmaydi.
builder.AddBonviMcp();

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

/*
  ═══════════════════════════════════════════════════════════
   IKKITA OpenAPI HUJJATI, IKKI XIL O'QUVCHI UCHUN.

   • `v1`     — ichki API. `./bonvi gen` (Orval) shundan TypeScript
                klient yasaydi;
   • `public` — tashqi API. Uni holdingning boshqa loyihasi o'qiydi va
                u FAQAT o'zi chaqira oladigan endpoint'larni ko'rishi
                kerak (SPEC §8).

   ⚠️ `ShouldInclude` IKKALA hujjatda ham ATAYLAB qo'lda yozilgan.
   Standart holatda guruh nomi ko'rsatilmagan endpoint HAR BIR hujjatga
   tushadi — ya'ni ommaviy hujjatga butun ichki API ham kirib kelardi va
   iste'molchi o'zi chaqira olmaydigan 150 ta marshrutni ko'rardi.
  ═══════════════════════════════════════════════════════════
*/
builder.Services.AddOpenApi(
    "v1",
    options => options.ShouldInclude = api =>
        !string.Equals(api.GroupName, PublicApiEndpoints.DocumentName, StringComparison.Ordinal));

builder.Services.AddOpenApi(
    PublicApiEndpoints.DocumentName,
    options => options.ShouldInclude = api =>
        string.Equals(api.GroupName, PublicApiEndpoints.DocumentName, StringComparison.Ordinal));

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

// Tokensiz kirish yo'llarida tana 16 KB dan oshmasin (sababi kengaytma izohida).
app.UseBonviAnonymousBodyLimit();

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
    // `/openapi/{documentName}.json` — ikkala hujjat ham shu marshrutdan
    // beriladi: `/openapi/v1.json` va `/openapi/public.json`.
    app.MapOpenApi().AllowAnonymous();

    // Scalar hujjatlarni O'ZI topmaydi — ular ochiq sanab beriladi.
    // Ro'yxatga qo'shilmagan hujjat `/scalar` da umuman ko'rinmaydi.
    app.MapScalarApiReference(options => options
        .WithTitle("Bonvi HR API")
        .WithTheme(ScalarTheme.BluePlanet)
        .AddDocument("v1", "Ichki API")
        .AddDocument(PublicApiEndpoints.DocumentName, "Tashqi API — faqat o'qish"))
        .AllowAnonymous();
}

app.UseCors("web");

// Tartib muhim: avval «kimsan» (authentication),
// keyin «nimaga ruxsating bor» (authorization).
app.UseAuthentication();

// Ommaviy yuzada kalit AVTORIZATSIYADAN OLDIN aniqlanadi: tezlik cheklovi
// kalit prefiksi bo'yicha bo'linadi va u claim'da yotadi. `UseAuthentication`
// faqat STANDART sxemani (JWT) bajaradi — batafsil sabab
// `UseBonviApiKeyAuthentication` izohida. Natija keshlanadi, ya'ni
// avtorizatsiya bosqichida kalit qayta tekshirilmaydi.
app.UseBonviApiKeyAuthentication(PublicApiEndpoints.Prefix);

// Xodim ilovasi ham xuddi shunday: cheklov XODIM bo'yicha bo'linsin, zavodning
// umumiy IP'si bo'yicha emas (sababi `UseBonviEmployeeAuthentication` izohida).
app.UseBonviEmployeeAuthentication("/api/me");

// AI ulanish ham shunday: `/mcp` so'rovining egasi cheklovdan OLDIN aniqlanadi —
// cheklov ulanish (grant) bo'yicha bo'linsin. Shu yerda MCP yopiq bo'lsa 503.
app.UseBonviMcpAuthentication();

/*
  Tezlik cheklovi autentifikatsiyadan KEYIN: cheklov foydalanuvchi bo'yicha
  hisoblanadi, u aniqlanmagan bo'lsa — IP bo'yicha.

  ⚠️ U AVTORIZATSIYADAN OLDIN turadi va bu ataylab. Aks holda 401/403
  bilan rad etilgan so'rovlar cheklovga umuman SANALMASDI — ya'ni kalit
  yoki parol tanlab ko'rish urinishlari (aynan cheklov to'sishi kerak
  bo'lgan narsa) chegarani yemasdi. Foydalanuvchi bo'yicha bo'linish
  buzilmaydi: JWT `UseAuthentication` da allaqachon o'qilgan.
*/
app.UseRateLimiter();

app.UseAuthorization();

app.MapBonviHealthChecks();

// ---------- Endpoint guruhlari ----------
app.MapAuthEndpoints();
app.MapUserEndpoints();

// Rollar muharriri — egasi rollarni o'zi yaratadi va tahrirlaydi
// («roles.manage»). Foydalanuvchilar yonida: ekranda ham qo'shni.
app.MapRoleEndpoints();

// Audit jurnali — faqat O'QISH («audit.view» + qator bo'limining ruxsati).
// Xodim kartasidagi «O'zgarishlar tarixi» marshruti ham shu yerda (o'z guruhida).
app.MapAuditEndpoints();

app.MapCatalogEndpoints();
app.MapEmployeeEndpoints();
app.MapEmploymentEndpoints();
app.MapIncentiveEndpoints();
app.MapEmployeeDocumentEndpoints();
app.MapExitInterviewEndpoints();
app.MapIncidentEndpoints();

// Davomat (3-bosqich) — turniket/yuz terminallarining hodisa qabuli.
// ⚠️ Tizimdagi YAGONA anonim yozish endpointi: himoya — qurilma tokeni
// (sababi `AttendanceIngestEndpoints` izohida).
app.MapAttendanceIngestEndpoints();

// Davomat — LAN ichidagi agent: ro'yxat, rasm va hisobot. Ochiqligi va
// himoyasi qabul yo'lidagidek — qurilma tokeni (SPEC-agent §D4).
app.MapAttendanceAgentEndpoints();

// Davomat — qurilmalar, mosliklar, jadvallar, jurnal, taxta, tabel, hisobotlar,
// eksport va qayta hisoblash. Hammasi `attendance.view` / `attendance.manage`
// ostida (SPEC §7.3, ruxsat jadvali `AttendanceEndpoints` izohida).
app.MapAttendanceEndpoints();

// Davomat — OBYEKTLAR ma'lumotnomasi (telefondan belgilashning geo-to'sig'i).
// Alohida fayl, chunki ruxsati BO'LINGAN: o'qish `catalog.view`, yozish
// `attendance.manage` — sababi `WorkSiteEndpoints` izohida.
app.MapWorkSiteEndpoints();

// Xodim ilovasi — HR tomoni: hisob ochish, parol tiklash, yopish va
// «telefondan belgilash» o'chirgichi. Hammasi `attendance.manage` ostida.
app.MapEmployeeAccountEndpoints();

// ⭐ Xodim ilovasi — XODIM tomoni. `bonvi-employee` auditoriyasidagi
// token bilan, ruxsat da'volarisiz: HR tokeni bu endpointlarga KIRA
// OLMAYDI va xodim tokeni HR endpointlariga o'ta olmaydi (ikki
// autentifikatsiya sxemasi, `AuthenticationExtensions`).
//
// ⚠️ Ochiq/yopiq — `EmployeeApp:Enabled`. Prod'da sukut bo'yicha YOPIQ
// (`docker-compose.prod.yml`, egasining qarori 2026-09-30): ilova xodimlarga
// berilishidan oldin PIN himoyasi kuchaytiriladi. Yopiq bo'lsa marshrutlar
// UMUMAN yo'q (404) — internetdan kirish ham, parol taxmin qilish ham, xeshlash
// yuklamasi ham yo'q. HR tomoni («Ilova» bo'limi, hisoblar) ishlayveradi.
if (app.Configuration.GetValue(EmployeeAppEnabledKey, defaultValue: true))
{
    app.MapEmployeeAuthEndpoints();
    app.MapMeEndpoints();
}

// Rekruting (2-bosqich): voronka bosqichlari → vakansiya → nomzod →
// ishga olish. Oxirgi qadam 1-bosqichning ishga qabul oqimiga ulanadi.
app.MapPipelineStageEndpoints();
app.MapVacancyEndpoints();
app.MapVacancyRequestEndpoints();
app.MapCandidateEndpoints();

// Nomzod anketasi HR-F02: ochiq onlayn forma (login ham, kalit ham yo'q — ofis
// plansheti va nomzodning o'z telefoni) va nomzod kartasidagi anketa PDF'i
// (SPEC-planshet-anketa).
app.MapKioskEndpoints();

// «Blankalar» — HR qog'oz blankalari (HR-F01…F19) onlayn: shablonlar, rollar,
// to'ldirish, yuborish, imzo qadamlari va fayllar (docs/SPEC-blankalar.md §7).
app.MapBlankEndpoints();

// Bildirishnomalar — yuqori paneldagi qo'ng'iroq. Bironta bo'limga
// tegishli emas: sinov, xodim va nomzod ma'lumotlarini BIRGA
// o'qiydi va har bir foydalanuvchiga o'z ro'yxatini beradi.
app.MapNotificationEndpoints();

app.MapSystemEndpoints();
app.MapDashboardEndpoints();

// Global qidiruv — yuqori paneldagi maydon. Bironta bo'limga tegishli
// emas: xodimlar, nomzodlar va vakansiyalarni birga qidiradi.
app.MapSearchEndpoints();
app.MapReportsEndpoints();

// Tizim sozlamalari — bot matni va bildirishnoma chegaralari.
// «settings.manage» ostida va u faqat super administratorda
// (sababi `Permissions.SettingsManage` izohida). Ichida bitta istisno
// bor: `/api/settings/bot` botning ruxsati bilan ochiladi.
app.MapSettingsEndpoints();

// HRMentor blankalari (SPEC-blankalar-hrmentor §6) — ko'rish «employees.view», «Hozir
// yangilash» esa «settings.manage».
app.MapHrMentorBlankEndpoints();

// Tashqi API kalitlari — ICHKI boshqaruv (JWT + «settings.manage»).
// Sozlamalar bilan yonma-yon, chunki ekranda ular bitta bo'limda.
app.MapApiKeyEndpoints();

/*
  ═══════════════════════════════════════════════════════════
   AI ULANISH (MCP) — FAQAT O'QISH (`docs/SPEC-mcp.md`).

   `/mcp` — vositalar, `/oauth/*` va `/.well-known/oauth-*` — kirish,
   `/api/mcp/*` — «Tizim sozlamalari» dagi boshqaruv (`settings.manage`).
   Vositalar alohida PostgreSQL roli bilan o'qiydi: pasport va fayl
   ustunlari unga baza darajasida berilmagan.
  ═══════════════════════════════════════════════════════════
*/
app.MapBonviMcp();

/*
  ═══════════════════════════════════════════════════════════
   TASHQI API — holdingning boshqa loyihalari uchun, FAQAT `GET`.

   Bu guruh boshqa hammasidan ALOHIDA yuza: o'z autentifikatsiya
   sxemasi (`X-API-Key`), o'z tezlik cheklovi (kalit prefiksi bo'yicha),
   o'z OpenAPI hujjati va ataylab tor maydon to'plami bor.
   Chegara — `PublicEmployeeDto` izohida; unga maydon qo'shish
   xodimlarning ma'lumotini BonviHR nazorat qilmaydigan bazalarga
   ko'chiradi.
  ═══════════════════════════════════════════════════════════
*/
app.MapPublicApiEndpoints();
app.MapPublicTeamEndpoints();

// Takroriy vazifalar jadvali (SPEC §9.2). Dashboard marshruti ATAYLAB yo'q.
app.UseBonviRecurringJobs();

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
    /// <summary>Xodim ilovasi marshrutlarining o'chirgichi (env: <c>EmployeeApp__Enabled</c>).</summary>
    public const string EmployeeAppEnabledKey = "EmployeeApp:Enabled";

    protected Program()
    {
    }
}
