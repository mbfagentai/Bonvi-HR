using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Bonvi.DeviceAgent.Hikvision;

/// <summary>
/// Terminalning ISAPI si bilan gaplashadigan yupqa mijoz.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Bu yerda faqat OLTITA chaqiruv bor va bu ro'yxat ataylab
/// yopiq:</b> hodisalarni o'qish, odamlar ro'yxatini o'qish, odam yozish,
/// odamni o'zgartirish, odamni o'chirish va yuz yozish. Terminalning
/// sozlamalariga tegadigan birorta chaqiruv YO'Q — sababi loyiha
/// faylidagi izohda (Verifix bilan bo'lishilgan HTTP push).
/// </para>
/// <para>
/// Tasdiqlash — <c>Digest</c>, <c>HttpClientHandler.Credentials</c>
/// orqali. Qo'lda yozilgan <c>nonce</c> hisobi YO'Q: platformaning o'zi
/// buni qiladi va tanani (JSON yoki multipart) 401 dan keyin qayta
/// yuboradi — shu sababli tana OQIM emas, BAYT bo'lishi shart.
/// </para>
/// </remarks>
internal sealed partial class HikvisionClient
{
    /// <summary>Bitta so'rovda so'raladigan yozuvlar soni.</summary>
    /// <remarks>
    /// Firmware ko'pincha 30 dan ortig'ini bermaydi — bu normal: sahifa
    /// oxirini son emas, terminal javobi aytadi (<see cref="HasMore"/>).
    /// </remarks>
    private const int PageSize = 50;

    /// <summary>Yuz kutubxonasining raqami — MinMoe'da doimo <c>1</c>.</summary>
    private const string FaceLibraryId = "1";

    /// <summary>
    /// ISAPI uchun JSON sozlamalari.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b><c>PropertyNamingPolicy</c> — <c>null</c>, ya'ni nom AYNAN
    /// kodda yozilganidek ketadi.</b> <c>JsonSerializerDefaults.Web</c>
    /// birinchi harfni kichraytiradi va <c>UserInfo</c> tanada
    /// <c>userInfo</c> bo'lib chiqadi; firmware esa bunday so'rovni
    /// <c>MessageParametersLack</c> bilan rad etadi — go'yo maydon
    /// yetishmayotgandek (2026-09-21 da tirik qurilmada, tarmoq oqimini
    /// o'qib aniqlangan). ISAPI da o'ram nomlari PascalCase
    /// (<c>UserInfo</c>, <c>Valid</c>, <c>AcsEventCond</c>), ichidagilari
    /// esa camelCase — ikkalasi ham shu sababli qo'lda yoziladi.
    /// </para>
    /// <para>
    /// ⚠️ <b>Qochirish yumshatilgan</b> (<c>UnsafeRelaxedJsonEscaping</c>):
    /// sukut sozlama <c>+</c> ni <c>\u002B</c>, kirill va o'zbek
    /// harflarini esa <c>\uXXXX</c> qilib yuboradi. Vaqt siljishi va
    /// xodim ismi terminalga AYNAN o'sha holida yetib borishi kerak.
    /// Nomi «unsafe» — HTML ichiga qo'yiladigan matn uchun; bu yerda
    /// esa tana to'g'ridan-to'g'ri qurilmaga ketadi.
    /// </para>
    /// <para>
    /// O'qishda harf katta-kichikligi e'tiborga OLINMAYDI: qurilma
    /// javobida <c>AcsEvent</c> ham, <c>serialNo</c> ham bor.
    /// </para>
    /// </remarks>
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly AgentOptions _options;

    public HikvisionClient(HttpClient http, IOptions<AgentOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    /// <summary>
    /// <paramref name="since"/> — <paramref name="until"/> oralig'idagi hodisalar, sahifama-sahifa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>major = 5</c> — kirish nazorati hodisalari. Eshik holati,
    /// tamper va tanilmagan yuz ham shu turkumga tushadi, shu sababli
    /// chaqiruvchi <c>EmployeeNoString</c> bo'sh qatorni tashlaydi.
    /// </para>
    /// <para>
    /// Vaqt TOSHKENT siljishi bilan yuboriladi: terminalning soati
    /// mahalliy vaqtda yuradi va <c>Z</c> bilan so'rov bo'sh natija
    /// qaytarardi. Oraliq TERMINAL soatida o'lchanadi — chaqiruvchi uni
    /// shunga moslab beradi.
    /// </para>
    /// <para>
    /// ⭐ <b>Sahifa ro'yxat emas, oqim bo'lib qaytadi.</b> Uzilishdan keyin
    /// terminalda minglab hodisa turgan bo'lishi mumkin: hammasini bitta
    /// ro'yxatga yig'ish ham, bitta umumiy muddat ichida o'qish ham
    /// (ilgari 8 soniya edi) agentni abadiy aylanishga solardi — o'qish
    /// hech qachon tugamasdi va kursor hech qachon surilmasdi. Endi
    /// muddat HAR SO'ROVGA alohida (<paramref name="pageTimeout"/>) va
    /// chaqiruvchi yetarlicha yig'gach o'qishni to'xtatib, qolganini
    /// keyingi bo'lakda oladi.
    /// </para>
    /// </remarks>
    public async IAsyncEnumerable<IReadOnlyList<AcsEvent>> SearchEventsAsync(
        DateTimeOffset since,
        DateTimeOffset until,
        TimeSpan pageTimeout,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var position = 0;
        var seen = new HashSet<long>();

        while (true)
        {
            var body = new
            {
                AcsEventCond = new
                {
                    searchID = Guid.NewGuid().ToString("N"),
                    searchResultPosition = position,
                    maxResults = PageSize,
                    major = 5,
                    minor = 0,
                    startTime = Local(since),
                    endTime = Local(until),
                },
            };

            AcsEventResponse? page;

            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(pageTimeout);

                page = await PostJsonAsync<AcsEventResponse>(
                        "/ISAPI/AccessControl/AcsEvent?format=json",
                        body,
                        deadline.Token)
                    .ConfigureAwait(false);
            }

            var items = page?.AcsEvent?.InfoList ?? [];
            var added = items.Count(item => seen.Add(item.SerialNo));

            if (items.Count > 0)
            {
                yield return items;
            }

            position += items.Count;

            if (added == 0 || !HasMore(page?.AcsEvent?.ResponseStatusStrg, page?.AcsEvent?.TotalMatches, position))
            {
                yield break;
            }
        }
    }

    /// <summary>Terminaldagi BARCHA odamlar — raqami, ismi va yuzlari soni.</summary>
    /// <remarks>
    /// Sahifa oxiri <see cref="HasMore"/> bilan aniqlanadi. Ilgari «so'ralgan
    /// 50 tadan kam keldi ⇒ tamom» edi: firmware sahifaga 30 tadan bersa,
    /// agent faqat birinchi 30 kishini ko'rardi va qolgan Bonvi xodimlarini
    /// «terminalda yo'q» deb har daqiqada qayta yozishga urinardi.
    /// </remarks>
    public async Task<IReadOnlyList<DevicePerson>> ListPersonsAsync(CancellationToken cancellationToken)
    {
        var people = new List<DevicePerson>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var position = 0;

        while (true)
        {
            var body = new
            {
                UserInfoSearchCond = new
                {
                    searchID = Guid.NewGuid().ToString("N"),
                    searchResultPosition = position,
                    maxResults = PageSize,
                },
            };

            var page = await PostJsonAsync<UserInfoSearchResponse>(
                    "/ISAPI/AccessControl/UserInfo/Search?format=json",
                    body,
                    cancellationToken)
                .ConfigureAwait(false);

            var items = page?.UserInfoSearch?.UserInfo ?? [];
            var added = 0;

            foreach (var item in items)
            {
                var number = item.EmployeeNo ?? string.Empty;

                // Bir kishi ikki sahifada kelsa (firmware pozitsiyani
                // e'tiborsiz qoldirsa) — bir marta.
                if (seen.Add(number))
                {
                    people.Add(new DevicePerson(number, item.Name, item.NumOfFace));
                    added++;
                }
            }

            position += items.Count;

            if (added == 0 || !HasMore(page?.UserInfoSearch?.ResponseStatusStrg, page?.UserInfoSearch?.TotalMatches, position))
            {
                return people;
            }
        }
    }

    /// <summary>
    /// Qidiruvning yana sahifasi bormi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Birinchi navbatda terminalning O'Z javobi: <c>MORE</c> — bor,
    /// <c>OK</c> / <c>NO MATCH</c> — tamom. Bu maydon bo'lmasa (eski
    /// firmware) — <c>totalMatches</c>. Ikkalasi ham bo'lmasa, bo'sh
    /// sahifa kelguncha o'qiladi: bitta ortiqcha so'rov yo'qolgan
    /// o'tishdan arzon.
    /// </para>
    /// <para>
    /// ⚠️ Sahifadagi yozuvlar soni bilan solishtirish YO'Q: firmware
    /// <c>maxResults</c> dan kam berishi normal va bu «oxirgi sahifa»
    /// degani emas. Aynan shu xato uzilishdan keyin 30 tadan keyingi
    /// hamma o'tishni tashlab ketardi.
    /// </para>
    /// <para>
    /// Chaqiruvchilar yana bir to'siq qo'yadi: sahifada BIRORTA yangi
    /// yozuv bo'lmasa, o'qish to'xtaydi — pozitsiyani e'tiborsiz
    /// qoldiradigan firmware bir sahifani cheksiz qaytarmasin.
    /// </para>
    /// </remarks>
    internal static bool HasMore(string? status, int? totalMatches, int read)
    {
        if (string.Equals(status, "MORE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "NO MATCH", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return totalMatches is not { } total || read < total;
    }

    /// <summary>Odamni yozadi yoki mavjudini JOYIDA yangilaydi.</summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>O'chirib qayta yozish YO'Q</b> (SPEC-agent §E, egasining
    /// qarori). Mavjud odam <c>UserInfo/Modify</c> bilan o'sha raqam
    /// ostida yangilanadi: raqam saqlanadi, ya'ni undan oldingi va
    /// keyingi o'tishlar bitta xodimda qoladi va odam bir lahzaga ham
    /// «qurilmada yo'q» bo'lib qolmaydi.
    /// </para>
    /// <para>
    /// Yozish urinishi <c>employeeNoAlreadyExist</c> bilan qaytsa, bu
    /// xato emas — poyga: ro'yxat o'qilgandan keyin odam boshqa yo'l
    /// bilan qo'shilgan. Bunda chaqiruv o'zgartirishga o'tadi.
    /// </para>
    /// </remarks>
    public async Task WritePersonAsync(
        string personNo,
        string name,
        bool exists,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            UserInfo = new
            {
                employeeNo = personNo,
                name = Trim(name),
                userType = "normal",
                Valid = new
                {
                    enable = true,
                    beginTime = "2026-01-01T00:00:00",
                    endTime = "2037-12-31T23:59:59",
                    timeType = "local",
                },
                doorRight = _options.DoorNo.ToString(CultureInfo.InvariantCulture),
                RightPlan = new[] { new { doorNo = _options.DoorNo, planTemplateNo = "1" } },
            },
        };

        if (exists)
        {
            await SendAsync(HttpMethod.Put, "/ISAPI/AccessControl/UserInfo/Modify?format=json", JsonBody(payload), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        try
        {
            await SendAsync(HttpMethod.Post, "/ISAPI/AccessControl/UserInfo/Record?format=json", JsonBody(payload), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HikvisionException failure) when (failure.Body.Contains("employeeNoAlreadyExist", StringComparison.OrdinalIgnoreCase))
        {
            await SendAsync(HttpMethod.Put, "/ISAPI/AccessControl/UserInfo/Modify?format=json", JsonBody(payload), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Odamni terminaldan o'chiradi.</summary>
    /// <remarks>
    /// ⚠️ Chaqiruvchi BU METODNI faqat Bonvi fazasidagi raqam uchun
    /// chaqirishi shart: terminal Verifix bilan bo'lishiladi va uning
    /// odamlari bizga tegishli emas.
    /// </remarks>
    public Task DeletePersonAsync(string personNo, CancellationToken cancellationToken)
    {
        var payload = new
        {
            UserInfoDelCond = new
            {
                EmployeeNoList = new[] { new { employeeNo = personNo } },
            },
        };

        return SendAsync(HttpMethod.Put, "/ISAPI/AccessControl/UserInfo/Delete?format=json", JsonBody(payload), cancellationToken);
    }

    /// <summary>Yuz rasmini yozadi yoki mavjudini almashtiradi.</summary>
    /// <remarks>
    /// Ikkala yo'l ham BIR XIL raqam (<c>FPID</c>) ostida: almashtirish
    /// <c>FDSetUp</c> bilan, yangisi <c>FaceDataRecord</c> bilan. Odamning
    /// o'zi bu paytda terminalda turadi va o'chib-yonmaydi.
    /// </remarks>
    public Task WriteFaceAsync(
        string personNo,
        byte[] jpeg,
        bool replace,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jpeg);

        var meta = JsonSerializer.Serialize(
            new { faceLibType = "blackFD", FDID = FaceLibraryId, FPID = personNo },
            Json);

        var boundary = $"bonvi-{Guid.NewGuid():N}";
        var content = new MultipartFormDataContent(boundary);

        /*
          ⚠️ Chegara (`boundary`) QO'ShTIRNOQSIZ. .NET uni har doim
          tirnoqqa oladi (`boundary="bonvi-…"`), firmware esa bunday
          sarlavhadagi qismlarni topa olmaydi. `curl` va Python tirnoqsiz
          yuboradi — farq faqat shunda edi.
        */
        content.Headers.ContentType!.Parameters.Clear();
        content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("boundary", boundary));

        /*
          ⚠️ Qismlarning sarlavhasi QO'LDA yoziladi va nomlar QO'ShTIRNOQDA.
          `content.Add(part, "FaceDataRecord")` ni chaqirsak, .NET
          `name=FaceDataRecord` (tirnoqsiz) va rasm uchun qo'shimcha
          `filename*=utf-8''face.jpg` yozadi — firmware bunday qismni
          umuman ko'rmaydi va so'rovni `MessageParametersLack` bilan rad
          etadi (2026-09-21, tarmoq oqimidan). `curl` va Python esa
          tirnoq bilan yuboradi — shuning uchun ular o'tadi.
        */
        var metaPart = new StringContent(meta, Encoding.UTF8);
        metaPart.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        metaPart.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"FaceDataRecord\"",
        };
        content.Add(metaPart);

        var imagePart = new ByteArrayContent(jpeg);
        imagePart.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        imagePart.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"img\"",
            FileName = "\"face.jpg\"",
        };
        content.Add(imagePart);

        return replace
            ? SendAsync(
                HttpMethod.Put,
                $"/ISAPI/Intelligent/FDLib/FDSetUp?format=json&FDID={FaceLibraryId}&FPID={Uri.EscapeDataString(personNo)}",
                content,
                cancellationToken)
            : SendAsync(
                HttpMethod.Post,
                "/ISAPI/Intelligent/FDLib/FaceDataRecord?format=json",
                content,
                cancellationToken);
    }

    /// <summary>
    /// Terminalning O'Z joriy vaqti.
    /// </summary>
    /// <remarks>
    /// Javob JSON emas, XML (<c>/ISAPI/System/time</c> faqat shu shaklda
    /// beriladi), shu sababli kerakli bitta qiymat oddiy qidiruv bilan
    /// olinadi: butun XML modelini yozish bitta satr uchun ortiqcha
    /// bo'lardi.
    /// </remarks>
    public async Task<DateTimeOffset?> ReadClockAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, "/ISAPI/System/time", content: null, cancellationToken)
            .ConfigureAwait(false);

        var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var match = LocalTime().Match(xml);

        return match.Success
            && DateTimeOffset.TryParse(
                match.Groups[1].Value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Kameradan JONLI kadr — JPEG baytlari.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Qurilma sozlamasiga TEGMAYDI — oddiy o'qish so'rovi, xuddi
    /// veb-interfeysdagi «surat olish» tugmasi kabi.
    /// </para>
    /// <para>
    /// Baytlar <see cref="SendAsync"/> dan o'tmaydi: u javobni matn qilib
    /// o'qiydi va JPEG buzilib ketardi. Javob JPEG bo'lmasa (masalan XML
    /// xato) — <c>null</c>: kadrsiz o'tish ham to'liq o'tish.
    /// </para>
    /// </remarks>
    public async Task<byte[]?> SnapshotAsync(string channel, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/ISAPI/Streaming/channels/{Uri.EscapeDataString(channel)}/picture");

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        // JPEG boshlanishi — FF D8. Boshqa narsa (XML xato, bo'sh javob)
        // surat sifatida saqlanmasligi kerak.
        return bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8 ? bytes : null;
    }

    [GeneratedRegex("<localTime>([^<]+)</localTime>", RegexOptions.IgnoreCase)]
    private static partial Regex LocalTime();

    /// <summary>Ismni terminal qabul qiladigan uzunlikka qirqadi.</summary>
    /// <remarks>
    /// Firmware ismni 32 BAYTgacha oladi va undan uzunini butun so'rov
    /// bilan birga rad etadi. Qirqish BAYT bo'yicha: kirill yoki
    /// o'zbekcha harf ikki bayt egallaydi, shu sababli belgi bo'yicha
    /// qirqish chegaradan oshib ketardi.
    /// </remarks>
    private static string Trim(string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        if (bytes.Length <= 32)
        {
            return name;
        }

        // Yarim qolgan belgini tashlaydi — `Encoding.UTF8` almashtirish
        // belgisi qo'ymasin.
        return new UTF8Encoding(false, false).GetString(bytes, 0, 32).TrimEnd('�');
    }

    private static string Local(DateTimeOffset instant)
        => instant.ToOffset(TimeSpan.FromHours(5)).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    /// <summary>
    /// JSON tana — <c>charset</c> SIZ.
    /// </summary>
    /// <remarks>
    /// ⚠️ <c>new StringContent(..., Encoding.UTF8, "application/json")</c>
    /// sarlavhaga <c>; charset=utf-8</c> qo'shadi va firmware bunday
    /// so'rovni <c>MessageParametersLack</c> bilan RAD ETADI — go'yo tanada
    /// maydon yetishmayotgandek (2026-09-21 da tirik qurilmada aniqlangan:
    /// aynan bir xil tana <c>curl</c> dan o'tadi, .NET dan o'tmaydi).
    /// Shu sababli tur qo'lda, parametrlarsiz qo'yiladi; baytlar baribir
    /// UTF-8.
    /// </remarks>
    private static StringContent JsonBody(object payload)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private async Task<TResponse?> PostJsonAsync<TResponse>(
        string path,
        object payload,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, path, JsonBody(payload), cancellationToken)
            .ConfigureAwait(false);

        return await response.Content
            .ReadFromJsonAsync<TResponse>(Json, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// So'rovni yuboradi; muvaffaqiyatsiz javob <see cref="HikvisionException"/> ga aylanadi.
    /// </summary>
    /// <remarks>
    /// Terminal xatoni HTTP holati bilan ham, tanadagi <c>subStatusCode</c>
    /// bilan ham beradi. Tana AYNAN o'sha holida saqlanadi va hisobot
    /// orqali HR ekraniga chiqadi: «face quality too low» degan xabarni
    /// tarjima qilish yoki qisqartirish uni foydasiz qilardi.
    /// </remarks>
    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        /*
          ⚠️ GET so'rovida tana BO'LMAYDI — hatto bo'shi ham. Bo'sh
          `StringContent` ham `Content-Length: 0` va `Content-Type:
          text/plain` sarlavhalarini qo'shadi, firmware esa bunday GET
          so'rovini umuman qabul qilmaydi.
        */
        using var request = new HttpRequestMessage(method, path);

        if (content is not null)
        {
            request.Content = content;
        }

        var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode || body.Contains("\"statusCode\":4", StringComparison.Ordinal))
        {
            response.Dispose();
            throw new HikvisionException((int)response.StatusCode, body);
        }

        // Tana yuqorida o'qib bo'lingan — chaqiruvchi uni qayta o'qiy
        // olishi uchun yangisiga o'rab qaytariladi.
        response.Content = new StringContent(body, Encoding.UTF8);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return response;
    }
}

/// <summary>Terminal so'rovni rad etdi.</summary>
/// <param name="statusCode">HTTP holati.</param>
/// <param name="body">Terminalning O'Z javobi — hisobotga aynan shu tushadi.</param>
internal sealed class HikvisionException(int statusCode, string body)
    : Exception($"ISAPI {statusCode}: {Shorten(body)}")
{
    public int StatusCode { get; } = statusCode;

    public string Body { get; } = body;

    private static string Shorten(string body)
        => body.Length <= 300 ? body : body[..300];
}
