using System.Net;
using System.Net.Sockets;

namespace Bonvi.Api.Common;

/// <summary>
/// Mijozning HAQIQIY IP manzili — odamga tegishli hamma joyda (SPEC-planshet-anketa §2, §2.3.3).
/// </summary>
/// <remarks>
/// <para>
/// Prod'da yo'l: Cloudflare → Caddy → API. API ko'radigan <c>RemoteIpAddress</c> — Cloudflare
/// chekkasi (2026-10-12 dan audit IP'lari shunday), ya'ni IP bo'yicha cheklov hamma nomzodni
/// bitta-ikkita chekka manzilga yig'ardi. Caddy Cloudflare diapazonlariga ishonadi, mijozni
/// <c>Cf-Connecting-Ip</c> dan oladi va API'ga <see cref="HeaderName"/> qilib beradi — mijoz
/// yuborgan qiymatni USTIDAN yozadi (<c>web/Caddyfile</c>).
/// </para>
/// <para>
/// <b>Qayerda o'qiladi</b> (2026-10-27 dan): ochiq anketa (cheklov, audit, bot tekshiruvi), HR va
/// xodim kirishi cheklovlari, sessiya yangilash cheklovi, umumiy cheklovning anonim qismi,
/// <c>audit_logs.ip_address</c>, yangilash tokenlari va xodim sessiyalarida saqlanadigan IP, AI
/// ulanish (MCP) cheklovlari va jurnali. <b>Qayerda O'QILMAYDI</b>: qurilma qabuli, davomat agenti,
/// ISUP — u yerda avvalgidek <c>RemoteIpAddress</c> (terminallarning xulqi va qurilma kartasidagi
/// IP ro'yxati o'zgarmasin); tashqi API'ning kalitsiz cheklovi va kalitning «oxirgi IP»si ham
/// (uni odam emas, boshqa loyihalarning serverlari chaqiradi). <c>X-Forwarded-For</c> qoidasining
/// o'zi ATAYLAB o'zgarmagan.
/// </para>
/// <para>
/// Sarlavhaga ishonish sharti — API porti Caddy'dan tashqariga chiqarilmagan (prod compose'da
/// <c>api</c> xizmatida <c>ports:</c> yo'q) va Caddy uni har API proksisida o'zi o'rnatib, mijoz
/// yuborganini o'chiradi. Ichki tarmoqdan (bot, ISUP shlyuzi) va dev'da sarlavha bo'lmaydi —
/// <c>RemoteIpAddress</c> olinadi.
/// </para>
/// <para>
/// Qiymat IP sifatida o'qiladi: buzuq yoki uzun matn kalitga aylanmaydi (cheklov bo'linmalari
/// soni o'smaydi, jurnalga axlat tushmaydi) — u holda <c>RemoteIpAddress</c>.
/// </para>
/// </remarks>
public static class ClientIp
{
    /// <summary>Caddy o'rnatadigan sarlavha.</summary>
    public const string HeaderName = "X-Bonvi-Client-Ip";

    /// <summary>Mijoz manzili: sarlavha (yaroqli IP bo'lsa), aks holda ulanish manzili.</summary>
    public static IPAddress? Resolve(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var header = context.Request.Headers[HeaderName];

        var address = header.Count == 1
            && header[0] is { Length: > 0 and <= 64 } value
            && IPAddress.TryParse(value.Trim(), out var parsed)
                ? parsed
                : context.Connection.RemoteIpAddress;

        // «::ffff:1.2.3.4» — aslida IPv4: /64 qoidasiga tushsa, barcha IPv4 mijozlar bitta kalitda qolardi
        return address is { IsIPv4MappedToIPv6: true } ? address.MapToIPv4() : address;
    }

    /// <summary>Tezlik cheklovi kaliti: IPv4 — o'zi, IPv6 — /64 tarmog'i.</summary>
    /// <remarks>
    /// IPv6'da bitta abonentga butun /64 beriladi va u manzilni istalgancha almashtira oladi —
    /// to'liq manzil bo'yicha bo'linsa, «bitta IP'ga soatiga 20 ta» chegarasi ma'nosiz bo'lardi.
    /// </remarks>
    public static string PartitionKey(HttpContext context)
    {
        if (Resolve(context) is not { } address)
        {
            return "nomalum";
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);

        return new IPAddress(bytes) + "/64";
    }
}
