using System.Net;
using Bonvi.DeviceAgent;
using Bonvi.DeviceAgent.Bonvi;
using Bonvi.DeviceAgent.Hikvision;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))

    /*
      Bu yerda manzil, token va parol MAJBURIY (botdan farqi): agent
      ularsiz umuman ish qila olmaydi va «kutib turish» holati yo'q —
      terminal paroli ekrandan kelmaydi, u faqat muhitdan keladi.
      Noto'g'ri sozlangan agent darhol va tushunarli xato bilan
      to'xtashi kerak, jim turib qolmasligi.
    */
    .ValidateDataAnnotations()
    .ValidateOnStart();

/*
  Terminal mijozi.

  ⚠️ Digest tasdiqlash platformaning O'ZIDA: `Credentials` qo'yilsa,
  `HttpClient` 401 javobini o'zi qayta ishlaydi va so'rovni `nonce`
  bilan qayta yuboradi. Qo'lda yozilgan digest hisobi — parolni ochiq
  matnda uzatib yuborish uchun eng oson yo'l.

  ⚠️ `PreAuthenticate` YOQILMAGAN: terminal har so'rovda yangi `nonce`
  kutadi va oldindan yuborilgan sarlavha bilan so'rovni rad etardi.
*/
builder.Services.AddHttpClient<HikvisionClient>((provider, client) =>
{
    var options = provider.GetRequiredService<IOptions<AgentOptions>>().Value;

    var host = options.DeviceHost.Contains("://", StringComparison.Ordinal)
        ? options.DeviceHost
        : "http://" + options.DeviceHost;

    client.BaseAddress = new Uri(host.EndsWith('/') ? host : host + "/");

    // Terminal sekin javob beradi (ayniqsa yuz yozishda), lekin halqa
    // muddatsiz osilib qolmasligi kerak.
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(provider =>
{
    var options = provider.GetRequiredService<IOptions<AgentOptions>>().Value;

    return new HttpClientHandler
    {
        Credentials = new NetworkCredential(options.DeviceUser, options.DevicePassword),
        PreAuthenticate = false,
    };
})

/*
  ⚠️ PAROL JURNALGA TUSHMASLIGI UCHUN.

  `AddHttpClient` sukut bo'yicha so'rov sarlavhalarini ham yozishi
  mumkin, digest esa `Authorization` sarlavhasida ketadi. Bot bilan
  bir xil qaror: bu jurnallar butunlay o'chiriladi va natijani
  `AgentWorker` o'zi yozadi.
*/
.RemoveAllLoggers();

builder.Services.AddHttpClient<BonviAgentClient>((provider, client) =>
{
    var options = provider.GetRequiredService<IOptions<AgentOptions>>().Value;

    var baseUrl = options.ApiBaseUrl.EndsWith('/') ? options.ApiBaseUrl : options.ApiBaseUrl + "/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);

    // Qurilma tokeni HAR so'rovda — agentda boshqa tasdiqlash yo'q.
    client.DefaultRequestHeaders.Add("X-Bonvi-Device-Token", options.DeviceToken);
})
.RemoveAllLoggers();

// Soat — testlar vaqtni boshqara olishi uchun (terminal soati orqada/oldinda).
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();

await host.RunAsync().ConfigureAwait(false);
