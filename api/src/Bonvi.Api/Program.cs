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
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

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

builder.Services.AddCors(options => options.AddPolicy("web", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:7173"])
    .AllowAnyHeader()
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
app.UseSerilogRequestLogging();

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
app.MapCatalogEndpoints();
app.MapEmployeeEndpoints();
app.MapEmploymentEndpoints();
app.MapSystemEndpoints();
app.MapDashboardEndpoints();

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
