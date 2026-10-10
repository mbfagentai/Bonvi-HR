using System.Text.RegularExpressions;
using Bonvi.IsupGateway;
using Bonvi.IsupGateway.Agents;
using Bonvi.IsupGateway.Cms;
using Bonvi.IsupGateway.Faces;
using Bonvi.IsupGateway.Tunnel;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

/*
  Jurnal: bitta qator, terminal doirasi (scope) bilan — `[Terminal: zavod-1]`.
  ⚠️ `HttpClient` jurnallari ishlatilmaydi (klientlar qo'lda yasaladi):
  so'rov sarlavhasida qurilma tokeni bor.
*/
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(console =>
{
    console.IncludeScopes = true;
    console.SingleLine = true;
    console.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

builder.Services
    .AddOptions<IsupOptions>()
    .Bind(builder.Configuration.GetSection(IsupOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<IsupOptions>, IsupOptionsValidator>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DeviceRegistry>();
builder.Services.AddSingleton<FacePictureStore>();
builder.Services.AddSingleton<IsupCmsServer>();
builder.Services.AddSingleton<IIsapiTunnel>(provider => provider.GetRequiredService<IsupCmsServer>());

// Tartib MUHIM: avval CMS (tinglash), keyin agentlar.
builder.Services.AddHostedService<IsupCmsHostedService>();
builder.Services.AddHostedService<DeviceAgentsHostedService>();

var app = builder.Build();

/*
  Terminal yuz rasmini shu yerdan yuklab oladi (Caddy: /isup-face/* → shu
  port). Faqat aniq shakldagi token — boshqa har qanday yo'l 404.
*/
app.MapGet("/isup-face/{name}", (string name, FacePictureStore faces) =>
{
    var match = FaceName().Match(name);

    return match.Success && faces.TryGet(match.Groups[1].Value, out var jpeg)
        ? Results.Bytes(jpeg, "image/jpeg")
        : Results.NotFound();
});

/*
  Ichki holat: konteyner tekshiruvi va «qaysi terminal ulangan» savoli.
  Tashqariga chiqarilmaydi (Caddy faqat /isup-face/ ni uzatadi). Sir yo'q:
  kalit va token bu yerda ko'rinmaydi.
*/
app.MapGet("/health", (DeviceRegistry registry, IOptions<IsupOptions> options) => Results.Ok(new
{
    status = "ok",
    configured = options.Value.Devices.Select(d => d.DeviceId),
    online = registry.Snapshot().Select(s => new { s.DeviceId, s.Serial, s.Firmware, s.RemoteAddress, s.OnlineSince }),
}));

await app.RunAsync().ConfigureAwait(false);

/// <summary>Kirish nuqtasi — <see cref="FaceName"/> uchun partial.</summary>
internal partial class Program
{
    [GeneratedRegex("^([0-9a-f]{32})\\.jpg$")]
    private static partial Regex FaceName();
}
