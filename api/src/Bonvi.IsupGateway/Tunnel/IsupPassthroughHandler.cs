using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Bonvi.IsupGateway.Faces;

namespace Bonvi.IsupGateway.Tunnel;

/// <summary>
/// Agentning terminal mijozi (<c>HikvisionClient</c>) ostidagi transport:
/// HTTP so'rovni ISUP tunneliga, javobni yana HTTP'ga aylantiradi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ Agent bu yerda LAN'dagi bilan AYNAN bir xil so'rovlarni yuboradi va
/// farqni sezmaydi (docs/SPEC-verifikatsiya-isup.md, G1). Uchta joyda
/// tarjima bor:
/// </para>
/// <list type="bullet">
///   <item><b>Yuz rasmi.</b> Agent <c>FaceDataRecord</c> / <c>FDSetUp</c> ni
///         multipart (JSON + JPEG) bilan yuboradi. Tunnel faqat matn o'tkazadi,
///         shu sababli JPEG bir martalik havolaga qo'yiladi va JSON'ga
///         <c>faceURL</c> qo'shiladi — terminal rasmni o'zi yuklab oladi
///         (Hikvision ISUP namunasidagi shakl). Havola so'rov tugashi bilan o'chadi.</item>
///   <item><b>Holat.</b> Tunnel HTTP holatini bermaydi — u javob tanasidan
///         tiklanadi (<see cref="IsapiStatusMapper"/>).</item>
///   <item><b>Ikkilik javob</b> (jonli kadr) — tunnel orqali o'tmaydi: 501, agent
///         bunda kadrsiz davom etadi.</item>
/// </list>
/// <para>
/// Tasdiqlash yo'q: ISUP terminalni kalit bilan allaqachon tasdiqlagan.
/// </para>
/// </remarks>
internal sealed class IsupPassthroughHandler(
    string deviceId,
    IIsapiTunnel tunnel,
    FacePictureStore faces,
    IsupOptions options) : HttpMessageHandler
{
    private const string FaceMetaPart = "FaceDataRecord";
    private const string FaceImagePart = "img";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var path = request.RequestUri?.PathAndQuery ?? "/";

        if (path.Contains("/Streaming/", StringComparison.OrdinalIgnoreCase))
        {
            return Respond(request, HttpStatusCode.NotImplemented, "ISUP tunneli ikkilik javobni (kadr) o'tkazmaydi.");
        }

        byte[] body;
        string? faceToken = null;

        if (request.Content is MultipartFormDataContent multipart)
        {
            var face = await TranslateFaceAsync(multipart, cancellationToken).ConfigureAwait(false);

            if (face is null)
            {
                return Respond(request, HttpStatusCode.BadRequest, "ISUP: multipart so'rovda 'FaceDataRecord' yoki 'img' qismi yo'q.");
            }

            (body, faceToken) = face.Value;
        }
        else
        {
            body = request.Content is null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var result = await tunnel
                .SendAsync(deviceId, request.Method.Method, path, body, cancellationToken)
                .ConfigureAwait(false);

            return result.Outcome switch
            {
                TunnelOutcome.Ok => Respond(request, IsapiStatusMapper.Map(result.Body), result.Body),
                TunnelOutcome.Offline => Respond(request, HttpStatusCode.ServiceUnavailable, result.Error),
                _ => Respond(request, HttpStatusCode.GatewayTimeout, result.Error),
            };
        }
        finally
        {
            if (faceToken is not null)
            {
                faces.Remove(faceToken);
            }
        }
    }

    /// <summary>
    /// Multipart yuz so'rovini <c>faceURL</c> li JSON'ga aylantiradi; rasm havolaga qo'yiladi.
    /// </summary>
    private async Task<(byte[] Body, string Token)?> TranslateFaceAsync(MultipartFormDataContent multipart, CancellationToken cancellationToken)
    {
        JsonObject? meta = null;
        byte[]? jpeg = null;

        foreach (var part in multipart)
        {
            // Agent nomlarni qo'shtirnoqda yozadi (`name="FaceDataRecord"`) — firmware talabi.
            var name = part.Headers.ContentDisposition?.Name?.Trim('"');

            if (string.Equals(name, FaceMetaPart, StringComparison.Ordinal))
            {
                meta = JsonNode.Parse(await part.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)) as JsonObject;
            }
            else if (string.Equals(name, FaceImagePart, StringComparison.Ordinal))
            {
                jpeg = await part.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (meta is null || jpeg is null || jpeg.Length == 0)
        {
            return null;
        }

        var token = faces.Add(jpeg, TimeSpan.FromSeconds(options.FaceUrlTtlSeconds));
        meta["faceURL"] = options.FaceUrlBase + token + ".jpg";

        return (Encoding.UTF8.GetBytes(meta.ToJsonString()), token);
    }

    private static HttpResponseMessage Respond(HttpRequestMessage request, HttpStatusCode status, string? body)
    {
        var text = body ?? string.Empty;

        var response = new HttpResponseMessage(status)
        {
            RequestMessage = request,
            Content = new StringContent(text, Encoding.UTF8),
        };

        response.Content.Headers.ContentType = new MediaTypeHeaderValue(
            text.AsSpan().TrimStart().StartsWith("<", StringComparison.Ordinal) ? "application/xml" : "application/json");

        return response;
    }
}
