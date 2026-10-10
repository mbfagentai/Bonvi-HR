using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Xml;

namespace Bonvi.IsupGateway.Tunnel;

/// <summary>
/// Terminal javob tanasidan HTTP holatini chiqaradi.
/// </summary>
/// <remarks>
/// <para>
/// ISUP tunneli HTTP holatini bermaydi — faqat tanani. LAN'da esa terminal
/// xatoni HTTP holati bilan ham beradi va <c>AgentWorker</c> qaror shundan
/// qiladi (SPEC-agent §L1.5: 4xx — qat'iy rad, qolgani — vaqtinchalik). Shu
/// sababli holat Hikvision'ning <c>ResponseStatus</c> tanasidan tiklanadi
/// (jadval: docs/SPEC-verifikatsiya-isup.md, §C).
/// </para>
/// <para>
/// Faqat ILDIZ elementi tekshiriladi: ma'lumot javoblari
/// (<c>AcsEvent</c>, <c>UserInfoSearch</c>, <c>DeviceInfo</c>) ichida
/// <c>statusCode</c> bo'lmaydi, lekin bo'lsa ham ularni xato deb o'qish
/// mumkin emas. 401/403 HECH QACHON qaytmaydi — ISUP terminalni allaqachon
/// tasdiqlagan, 401 esa agentni o'n daqiqaga kutishga solardi.
/// </para>
/// </remarks>
internal static class IsapiStatusMapper
{
    public static HttpStatusCode Map(string body)
    {
        var code = ReadStatusCode(body);

        return code switch
        {
            null => HttpStatusCode.OK,
            1 or 7 => HttpStatusCode.OK,
            2 => HttpStatusCode.ServiceUnavailable,
            3 => HttpStatusCode.InternalServerError,
            4 or 5 or 6 => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.InternalServerError,
        };
    }

    internal static int? ReadStatusCode(string body)
    {
        var text = body.AsSpan().TrimStart();

        if (text.IsEmpty)
        {
            return null;
        }

        return text[0] switch
        {
            '{' => FromJson(body),
            '<' => FromXml(body),
            _ => null,
        };
    }

    private static int? FromJson(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "statusCode", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.Number
                    && property.Value.TryGetInt32(out var code))
                {
                    return code;
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? FromXml(string body)
    {
        try
        {
            using var reader = XmlReader.Create(
                new StringReader(body),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });

            reader.MoveToContent();

            if (!string.Equals(reader.LocalName, "ResponseStatus", StringComparison.Ordinal))
            {
                return null;
            }

            using var subtree = reader.ReadSubtree();

            while (subtree.Read())
            {
                if (subtree.NodeType == XmlNodeType.Element
                    && subtree.Depth == 1
                    && string.Equals(subtree.LocalName, "statusCode", StringComparison.Ordinal))
                {
                    return int.TryParse(
                        subtree.ReadElementContentAsString(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var code) ? code : null;
                }
            }

            return null;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
