using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Bitta <c>tools/call</c> natijasi.</summary>
/// <param name="Status">HTTP holati.</param>
/// <param name="IsError">Vosita xato qaytardi (yoki JSON-RPC xatosi).</param>
/// <param name="Text">Vosita matni (yoki JSON-RPC xato xabari).</param>
/// <param name="Raw">Javobning XOM tanasi — sizib chiqishni aynan shundan qidiramiz.</param>
public sealed record McpCallResult(HttpStatusCode Status, bool IsError, string Text, string Raw);

/// <summary>
/// AI mijozining o'rnini bosadi: OAuth 2.1 oqimi (DCR → kirish sahifasi →
/// rozilik → token) va <c>/mcp</c> ga JSON-RPC chaqiruvlar — haqiqiy MCP
/// mijozi yuradigan yo'ldan.
/// </summary>
/// <remarks>
/// Har bir oqim o'z soxta IP'sini oladi (<see cref="Ip"/>): IP bo'yicha
/// cheklovlar (ro'yxatdan o'tish 10/soat, kirish formasi 10/daqiqa) testlarni
/// bir-biriga to'siq qilmasin. Jurnaldagi IP ham shu.
/// </remarks>
public sealed partial class McpFlow
{
    /// <summary>Loopback qaytish manzili — mahalliy MCP mijozlari shunday ro'yxatdan o'tadi.</summary>
    public const string RedirectUri = "http://127.0.0.1:53123/callback";

    public const string State = "holat-1";

    /// <summary>Kanonik resurs — TestServer manzili <c>http://localhost</c>.</summary>
    public const string Resource = "http://localhost/mcp";

    private static readonly string[] GrantTypes = ["authorization_code", "refresh_token"];
    private static readonly string[] ResponseTypes = ["code"];

    private readonly McpHost _host;
    private int _id;

    public McpFlow(McpHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    public string Ip { get; } = RandomIp();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string RandomIp() => string.Create(
        CultureInfo.InvariantCulture,
        $"10.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}");

    public HttpClient Client() => _host.Client(Ip);

    // ─────────────────────────────────────────────────────────────
    //  OAuth
    // ─────────────────────────────────────────────────────────────

    public static (string Verifier, string Challenge) Pkce()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (verifier, Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
    }

    public static string Base64Url(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Xom ro'yxatdan o'tish so'rovi.</summary>
    public async Task<HttpResponseMessage> RegisterRawAsync(object body)
    {
        using var client = Client();
        return await client.PostAsJsonAsync(new Uri("/oauth/register", UriKind.Relative), body, Ct).ConfigureAwait(false);
    }

    /// <summary>Ochiq mijozni ro'yxatdan o'tkazadi va uning <c>client_id</c> sini qaytaradi.</summary>
    public async Task<string> RegisterAsync(string name = "Sinov AI", string redirect = RedirectUri)
    {
        using var javob = await RegisterRawAsync(new
        {
            client_name = name,
            redirect_uris = new[] { redirect },
            grant_types = GrantTypes,
            response_types = ResponseTypes,
            token_endpoint_auth_method = "none",
        }).ConfigureAwait(false);

        var matn = await javob.Content.ReadAsStringAsync(Ct).ConfigureAwait(false);
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, matn);

        using var hujjat = JsonDocument.Parse(matn);
        return hujjat.RootElement.GetProperty("client_id").GetString()!;
    }

    public static Uri AuthorizeUri(
        string clientId,
        string? challenge,
        string redirect = RedirectUri,
        string? method = "S256",
        string state = State)
    {
        var parametrlar = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirect,
            ["state"] = state,
            ["scope"] = "hr.read",
            ["resource"] = Resource,
        };

        if (challenge is not null)
        {
            parametrlar["code_challenge"] = challenge;
        }

        if (method is not null)
        {
            parametrlar["code_challenge_method"] = method;
        }

        return new Uri(QueryHelpers.AddQueryString("/oauth/authorize", parametrlar), UriKind.Relative);
    }

    [GeneratedRegex("name=\"req\" value=\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex ReqField();

    /// <summary>Sahifadagi yashirin <c>req</c> tutqichi.</summary>
    public static string ReqOf(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var moslik = ReqField().Match(html);
        moslik.Success.ShouldBeTrue("sahifada yashirin «req» maydoni yo'q: " + html[..Math.Min(500, html.Length)]);
        return WebUtility.HtmlDecode(moslik.Groups[1].Value);
    }

    /// <summary>HTML sahifaning matni — apostroflar (<c>&amp;#39;</c>) ochilgan holda.</summary>
    public static async Task<string> HtmlAsync(HttpResponseMessage javob)
    {
        ArgumentNullException.ThrowIfNull(javob);
        return WebUtility.HtmlDecode(await javob.Content.ReadAsStringAsync(Ct).ConfigureAwait(false));
    }

    public async Task<HttpResponseMessage> PostFormAsync(string path, Dictionary<string, string> form)
    {
        using var client = Client();
        using var tana = new FormUrlEncodedContent(form);
        return await client.PostAsync(new Uri(path, UriKind.Relative), tana, Ct).ConfigureAwait(false);
    }

    public async Task<HttpResponseMessage> GetAsync(Uri uri)
    {
        using var client = Client();
        return await client.GetAsync(uri, Ct).ConfigureAwait(false);
    }

    /// <summary>Kirish sahifasini ochadi va telefon + parolni yuboradi. Kirish POST'ining javobi qaytadi.</summary>
    public async Task<HttpResponseMessage> LoginAsync(string clientId, string challenge, string login, string password)
    {
        using var sahifa = await GetAsync(AuthorizeUri(clientId, challenge)).ConfigureAwait(false);
        var html = await sahifa.Content.ReadAsStringAsync(Ct).ConfigureAwait(false);
        sahifa.StatusCode.ShouldBe(HttpStatusCode.OK, html);

        return await PostFormAsync("/oauth/authorize", new Dictionary<string, string>
        {
            ["step"] = "login",
            ["req"] = ReqOf(html),
            ["login"] = login,
            ["password"] = password,
        }).ConfigureAwait(false);
    }

    /// <summary>Brauzer qismi to'liq: kirish + rozilik. Yo'naltirish manzilining so'rov parametrlari qaytadi.</summary>
    public async Task<Dictionary<string, string>> ConsentAsync(
        string clientId,
        string challenge,
        string login,
        string password,
        string decision = "allow")
    {
        using var kirish = await LoginAsync(clientId, challenge, login, password).ConfigureAwait(false);
        var html = await kirish.Content.ReadAsStringAsync(Ct).ConfigureAwait(false);
        kirish.StatusCode.ShouldBe(HttpStatusCode.OK, html);

        using var rozilik = await PostFormAsync("/oauth/authorize", new Dictionary<string, string>
        {
            ["step"] = "consent",
            ["req"] = ReqOf(html),
            ["decision"] = decision,
        }).ConfigureAwait(false);

        rozilik.StatusCode.ShouldBe(HttpStatusCode.Redirect, await rozilik.Content.ReadAsStringAsync(Ct).ConfigureAwait(false));
        return RedirectQuery(rozilik);
    }

    /// <summary>Yo'naltirish manzili qaytish manzilimizga boradi — uning parametrlari.</summary>
    public static Dictionary<string, string> RedirectQuery(HttpResponseMessage javob, string redirect = RedirectUri)
    {
        ArgumentNullException.ThrowIfNull(javob);

        var manzil = javob.Headers.Location;
        manzil.ShouldNotBeNull();
        manzil.IsAbsoluteUri.ShouldBeTrue();
        manzil.GetLeftPart(UriPartial.Path).ShouldBe(redirect);

        return QueryHelpers.ParseQuery(manzil.Query)
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);
    }

    /// <summary>Avtorizatsiya kodi.</summary>
    public async Task<string> CodeAsync(string clientId, string challenge, string login, string password)
    {
        var parametrlar = await ConsentAsync(clientId, challenge, login, password).ConfigureAwait(false);
        parametrlar["state"].ShouldBe(State);
        parametrlar["iss"].ShouldBe("http://localhost");
        return parametrlar["code"];
    }

    public Task<HttpResponseMessage> TokenAsync(Dictionary<string, string> form) => PostFormAsync("/oauth/token", form);

    public static Dictionary<string, string> CodeForm(string code, string clientId, string verifier) => new()
    {
        ["grant_type"] = "authorization_code",
        ["code"] = code,
        ["redirect_uri"] = RedirectUri,
        ["client_id"] = clientId,
        ["code_verifier"] = verifier,
    };

    public static Dictionary<string, string> RefreshForm(string refresh, string clientId) => new()
    {
        ["grant_type"] = "refresh_token",
        ["refresh_token"] = refresh,
        ["client_id"] = clientId,
    };

    /// <summary>Token javobidan juftlik.</summary>
    public static async Task<McpTokens> TokensOfAsync(HttpResponseMessage javob, string clientId)
    {
        ArgumentNullException.ThrowIfNull(javob);

        var matn = await javob.Content.ReadAsStringAsync(Ct).ConfigureAwait(false);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, matn);

        using var hujjat = JsonDocument.Parse(matn);
        return new McpTokens(
            hujjat.RootElement.GetProperty("access_token").GetString()!,
            hujjat.RootElement.GetProperty("refresh_token").GetString()!,
            clientId);
    }

    /// <summary>To'liq oqim: ro'yxatdan o'tish → kirish → rozilik → token.</summary>
    public async Task<McpTokens> ConnectAsync(string login, string password, string clientName = "Sinov AI")
    {
        var clientId = await RegisterAsync(clientName).ConfigureAwait(false);
        var (verifier, challenge) = Pkce();
        var code = await CodeAsync(clientId, challenge, login, password).ConfigureAwait(false);

        using var javob = await TokenAsync(CodeForm(code, clientId, verifier)).ConfigureAwait(false);
        return await TokensOfAsync(javob, clientId).ConfigureAwait(false);
    }

    /// <summary>Boshlang'ich super administrator nomidan ulanish (barcha ruxsatlar).</summary>
    public Task<McpTokens> ConnectAdminAsync(string clientName = "Sinov AI")
        => ConnectAsync(BonviApiFactory.TestAdminLogin, BonviApiFactory.TestAdminPassword, clientName);

    /// <summary>Faqat shu ruxsatlarga ega yangi hisob va uning ulanishi.</summary>
    public async Task<(TestAccount Account, McpTokens Tokens)> ConnectNewAsync(params string[] permissions)
    {
        var hisob = await TestAccounts.CreateAsync(_host.Factory, permissions).ConfigureAwait(false);
        return (hisob, await ConnectAsync(hisob.Login, hisob.Password).ConfigureAwait(false));
    }

    // ─────────────────────────────────────────────────────────────
    //  JSON-RPC (Streamable HTTP)
    // ─────────────────────────────────────────────────────────────

    /// <summary>Bitta JSON-RPC xabari <c>/mcp</c> ga.</summary>
    public async Task<HttpResponseMessage> PostMcpAsync(string? accessToken, object message)
    {
        using var client = Client();
        using var sorov = new HttpRequestMessage(HttpMethod.Post, new Uri("/mcp", UriKind.Relative))
        {
            Content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json"),
        };

        sorov.Headers.Accept.ParseAdd("application/json");
        sorov.Headers.Accept.ParseAdd("text/event-stream");

        if (accessToken is not null)
        {
            sorov.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(sorov, Ct).ConfigureAwait(false);
    }

    /// <summary>Faqat holat kodi kerak bo'lganda — <c>tools/list</c>.</summary>
    public async Task<HttpStatusCode> ProbeAsync(string? accessToken)
    {
        using var javob = await PostMcpAsync(accessToken, new { jsonrpc = "2.0", id = ++_id, method = "tools/list", @params = new { } })
            .ConfigureAwait(false);
        return javob.StatusCode;
    }

    /// <summary>JSON-RPC chaqiruv; butun javob tugunini (<c>result</c> yoki <c>error</c>) va xom matnni qaytaradi.</summary>
    public async Task<(HttpStatusCode Status, JsonNode? Node, string Raw)> RpcRawAsync(string accessToken, string method, object? parameters = null)
    {
        using var javob = await PostMcpAsync(accessToken, new { jsonrpc = "2.0", id = ++_id, method, @params = parameters ?? new { } })
            .ConfigureAwait(false);

        var matn = await javob.Content.ReadAsStringAsync(Ct).ConfigureAwait(false);

        if (javob.StatusCode != HttpStatusCode.OK)
        {
            return (javob.StatusCode, null, matn);
        }

        return (javob.StatusCode, JsonNode.Parse(ExtractJson(matn)), matn);
    }

    /// <summary>Muvaffaqiyatli bo'lishi SHART bo'lgan chaqiruv — <c>result</c> tuguni.</summary>
    public async Task<JsonNode> RpcAsync(string accessToken, string method, object? parameters = null)
    {
        var (status, node, raw) = await RpcRawAsync(accessToken, method, parameters).ConfigureAwait(false);

        status.ShouldBe(HttpStatusCode.OK, $"{method}: {raw}");
        node.ShouldNotBeNull();
        node["error"].ShouldBeNull($"{method}: JSON-RPC xatosi — {raw}");

        var result = node["result"];
        result.ShouldNotBeNull($"{method}: natija yo'q — {raw}");
        return result;
    }

    public Task<JsonNode> InitializeAsync(string accessToken) => RpcAsync(accessToken, "initialize", new
    {
        protocolVersion = "2025-06-18",
        capabilities = new { },
        clientInfo = new { name = "bonvi-mcp-tests", version = "1.0" },
    });

    /// <summary>Vositalar ro'yxati (to'liq tugunlar).</summary>
    public async Task<JsonArray> ToolsAsync(string accessToken)
    {
        var natija = await RpcAsync(accessToken, "tools/list").ConfigureAwait(false);
        var tools = natija["tools"]?.AsArray();
        tools.ShouldNotBeNull();
        return tools;
    }

    /// <summary><c>tools/call</c>. JSON-RPC xatosi ham «xato» deb qaytadi (HTTP 200 bo'lsa).</summary>
    public async Task<McpCallResult> CallAsync(string accessToken, string tool, object? arguments = null)
    {
        var (status, node, raw) = await RpcRawAsync(accessToken, "tools/call", new { name = tool, arguments = arguments ?? new { } })
            .ConfigureAwait(false);

        if (node is null)
        {
            return new McpCallResult(status, true, raw, raw);
        }

        if (node["error"] is { } xato)
        {
            return new McpCallResult(status, true, xato["message"]?.GetValue<string>() ?? xato.ToJsonString(), raw);
        }

        var natija = node["result"];
        natija.ShouldNotBeNull(raw);

        var isError = natija["isError"]?.GetValue<bool>() ?? false;
        var matn = string.Concat((natija["content"]?.AsArray() ?? new JsonArray())
            .Select(qism => qism?["text"]?.GetValue<string>() ?? string.Empty));

        return new McpCallResult(status, isError, matn, raw);
    }

    /// <summary>Javob oddiy JSON yoki SSE oqimi (<c>data: {...}</c>) bo'lishi mumkin.</summary>
    public static string ExtractJson(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var trimmed = body.TrimStart();

        if (trimmed.StartsWith('{'))
        {
            return trimmed;
        }

        var data = body.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith("data:", StringComparison.Ordinal))
            .Select(line => line[5..].Trim())
            .LastOrDefault(line => line.StartsWith('{'));

        data.ShouldNotBeNull("javobda JSON yo'q: " + body);
        return data;
    }

    /// <summary>JSON daraxtidagi barcha xossa nomlari (ichma-ich).</summary>
    public static IEnumerable<string> PropertyNames(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (kalit, qiymat) in obj)
                {
                    yield return kalit;

                    foreach (var ichki in PropertyNames(qiymat))
                    {
                        yield return ichki;
                    }
                }

                break;

            case JsonArray array:
                foreach (var element in array)
                {
                    foreach (var ichki in PropertyNames(element))
                    {
                        yield return ichki;
                    }
                }

                break;
        }
    }

    /// <summary>Vosita matni JSON bo'lsa — tugun, aks holda <c>null</c>.</summary>
    public static JsonNode? TryParse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var trimmed = text.TrimStart();

        if (!trimmed.StartsWith('{') && !trimmed.StartsWith('['))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(trimmed);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Vosita sxemasidan argumentlar
    // ─────────────────────────────────────────────────────────────

    /// <summary>Vosita nomi (<c>tools/list</c> tugunidan).</summary>
    public static string NameOf(JsonNode tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return tool["name"]!.GetValue<string>();
    }

    /// <summary>
    /// Vositaning <c>inputSchema</c> si bo'yicha argumentlar yig'adi.
    /// </summary>
    /// <param name="tool"><c>tools/list</c> dagi vosita tuguni.</param>
    /// <param name="tanla">
    /// Xossa nomi va sxemasi → qiymat (<c>null</c> — tanlanmadi). Tanlangan
    /// xossa (majburiy yoki ixtiyoriy) argumentga tushadi.
    /// </param>
    /// <returns>
    /// Argumentlar va TANLANMAGAN majburiy xossalar soni — ular umumiy
    /// to'ldiruvchi bilan to'ldirilgan (sxema turiga mos, lekin ma'nosiz qiymat).
    /// </returns>
    public static (JsonObject Args, int Guessed) BuildArgs(JsonNode tool, Func<string, JsonNode, JsonNode?> tanla)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(tanla);

        var schema = tool["inputSchema"];
        var required = (schema?["required"]?.AsArray() ?? new JsonArray())
            .Select(item => item!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

        var args = new JsonObject();
        var guessed = 0;

        foreach (var (nom, xossa) in schema?["properties"]?.AsObject() ?? new JsonObject())
        {
            var qiymat = xossa is null ? null : tanla(nom, xossa);

            if (qiymat is null && required.Contains(nom))
            {
                qiymat = Default(nom, xossa);
                guessed++;
            }

            if (qiymat is not null)
            {
                args[nom] = qiymat;
            }
        }

        return (args, guessed);
    }

    /// <summary>Sxema turiga mos umumiy qiymat — majburiy xossa bo'sh qolmasin.</summary>
    public static JsonNode Default(string nom, JsonNode? xossa)
    {
        ArgumentNullException.ThrowIfNull(nom);

        if (xossa?["enum"]?.AsArray() is { Count: > 0 } variantlar)
        {
            return variantlar[0]!.DeepClone();
        }

        var tur = TypeOf(xossa);
        var kichik = nom.ToLowerInvariant();

        return tur switch
        {
            "integer" or "number" => JsonValue.Create(1),
            "boolean" => JsonValue.Create(false),
            "array" => new JsonArray(),
            "object" => new JsonObject(),
            _ when string.Equals(xossa?["format"]?.GetValue<string>(), "uuid", StringComparison.Ordinal) || kichik.EndsWith("id", StringComparison.Ordinal)
                => JsonValue.Create(Guid.NewGuid().ToString()),
            _ when kichik.Contains("date", StringComparison.Ordinal) || kichik is "from" or "to" or "day"
                => JsonValue.Create(DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            _ => JsonValue.Create("x"),
        };
    }

    /// <summary>Sxemadagi tur (<c>["string","null"]</c> ko'rinishida bo'lsa — birinchi null bo'lmagani).</summary>
    public static string? TypeOf(JsonNode? xossa)
    {
        var tur = xossa?["type"];

        return tur switch
        {
            JsonValue value => value.GetValue<string>(),
            JsonArray array => array.Select(item => item?.GetValue<string>()).FirstOrDefault(item => item is not null and not "null"),
            _ => null,
        };
    }
}

/// <summary>Ulanishning token juftligi.</summary>
public sealed record McpTokens(string Access, string Refresh, string ClientId);
