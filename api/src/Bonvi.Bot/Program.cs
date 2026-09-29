using Bonvi.Bot;
using Bonvi.Bot.Bonvi;
using Bonvi.Bot.Conversation;
using Bonvi.Bot.Telegram;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<BotOptions>()
    .Bind(builder.Configuration.GetSection(BotOptions.SectionName))

    // Faqat DIAPAZONLAR tekshiriladi (masalan `PollTimeoutSeconds`):
    // noto'g'ri son bilan ishga tushish keyinroq tushunarsiz xatoga
    // aylanardi.
    //
    // Token va parolning BO'SHLIGI bu yerda tekshirilmaydi: bu xato emas,
    // «bot hali sozlanmagan» degan HOLAT va tizim bundan mustaqil ishlaydi
    // (izohi `BotOptions.Token` da).
    .ValidateDataAnnotations()
    .ValidateOnStart();

/*
  Botning JORIY tokeni — bitta umumiy yacheyka.

  Uni `BotWorker` to'ldiradi (tizim sozlamalaridan, bo'lmasa `.env` dan),
  `TelegramClient` esa har chaqiruvda o'qiydi. Sabab `TelegramToken`
  izohida: token endi ish paytida almashishi mumkin.
*/
builder.Services.AddSingleton<TelegramToken>();

/*
  Telegram mijozi.

  ⚠️ Manzil bazasida TOKEN YO'Q — faqat host. Ilgari u shu yerda
  `bot{token}/` bo'lib qotib qolardi va shuning uchun tokenni
  almashtirish konteynerni qayta ishga tushirishni talab qilardi. Endi
  to'liq yo'l har chaqiruvda yig'iladi (`TelegramClient.CallAsync`).
*/
builder.Services.AddHttpClient<TelegramClient>((provider, client) =>
{
    var options = provider.GetRequiredService<IOptions<BotOptions>>().Value;

    client.BaseAddress = new Uri("https://api.telegram.org/");

    // Long polling so'rovi `PollTimeoutSeconds` gacha ochiq turadi —
    // `HttpClient` ning muddati undan UZUNROQ bo'lishi shart, aks holda
    // har so'rov o'z vaqti tugashi bilan uzilardi.
    client.Timeout = TimeSpan.FromSeconds(options.PollTimeoutSeconds + 15);
})

/*
  ⚠️ TOKEN JURNALGA TUSHMASLIGI UCHUN.

  `AddHttpClient` sukut bo'yicha har bir so'rovni jurnalga yozadi va
  MANZILNI to'liq ko'rsatadi. Telegram tokeni esa aynan manzil ichida
  turadi (API shunday qurilgan), ya'ni sukut sozlama bilan bot tokeni
  `docker logs bot` da ochiq matn bo'lib yotardi — uni ko'rgan har
  kim bot nomidan xabar yuborishi mumkin.

  Bu jurnallar butunlay o'chiriladi. Yo'qotilgan narsa yo'q: chaqiruv
  natijasini `TelegramClient` o'zi yozadi (metod nomi bilan, tokensiz).
*/
.RemoveAllLoggers();

builder.Services.AddHttpClient<BonviClient>((provider, client) =>
{
    var options = provider.GetRequiredService<IOptions<BotOptions>>().Value;

    var baseUrl = options.ApiBaseUrl.EndsWith('/') ? options.ApiBaseUrl : options.ApiBaseUrl + "/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

/*
  Suhbat holatining Redis nusxasi — bot qayta ishga tushganda (deploy)
  yarim to'ldirilgan anketa yo'qolmasligi uchun (`ConversationStore`
  izohi). Ulanish satri API dagi bilan bir xil kalitda:
  `ConnectionStrings__Redis`.

  Satr bo'lmasa (masalan lokal `dotnet run`) nusxa umuman ulanmaydi va
  bot avvalgidek faqat xotirada ishlaydi.

  `abortConnect=false` va qisqa kutish: Redis ko'tarilmagan bo'lsa bot
  baribir ishga tushadi. O'chiq Redis birinchi xatoda yarim soniyagacha
  ushlaydi, keyin nusxa 30 soniya chetlab o'tiladi
  (`RedisConversationBackup` izohi).
*/
var redis = builder.Configuration.GetConnectionString("Redis");

if (!string.IsNullOrWhiteSpace(redis))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redis + ",abortConnect=false,connectTimeout=1000,syncTimeout=500";
        options.InstanceName = "bonvi-bot:";
    });

    builder.Services.AddSingleton<IConversationBackup, RedisConversationBackup>();
}

builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IOptions<BotOptions>>().Value;

    return new ConversationStore(
        TimeSpan.FromMinutes(options.ConversationTimeoutMinutes),
        provider.GetService<IConversationBackup>());
});

builder.Services.AddHostedService<BotWorker>();

var host = builder.Build();

/*
  `HttpClient` orqali olinadigan mijozlar SCOPED bo'ladi, `BotWorker` esa
  singleton — shu sababli u ularni to'g'ridan-to'g'ri ololmaydi. Bu yerda
  ikkalasi ham bir marta yasalib, butun umr bo'yi ishlatiladi: bot bitta
  jarayon va bitta halqadan iborat, ya'ni qamrov (scope) tushunchasining
  o'zi bu yerda ma'nosiz.
*/
await host.RunAsync().ConfigureAwait(false);
