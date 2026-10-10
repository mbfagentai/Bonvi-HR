namespace Bonvi.IsupGateway.Tunnel;

/// <summary>Tunnel so'rovining natijasi.</summary>
internal enum TunnelOutcome
{
    /// <summary>Terminal javob berdi (javob ichidagi holat alohida tekshiriladi).</summary>
    Ok,

    /// <summary>Terminal hozir ulanmagan.</summary>
    Offline,

    /// <summary>SDK chaqiruvi muvaffaqiyatsiz (muddat, uzilish, juda katta javob).</summary>
    Failed,
}

/// <param name="Outcome">Natija turi.</param>
/// <param name="Body">Terminalning javob tanasi (XML yoki JSON); <see cref="TunnelOutcome.Ok"/> bo'lmasa bo'sh.</param>
/// <param name="Error">Muvaffaqiyatsizlik sababi — jurnal va agent hisoboti uchun.</param>
internal sealed record TunnelResponse(TunnelOutcome Outcome, string Body, string? Error = null)
{
    public static TunnelResponse Offline(string deviceId) => new(TunnelOutcome.Offline, string.Empty, $"ISUP: '{deviceId}' terminali hozir ulanmagan");

    public static TunnelResponse Failed(string error) => new(TunnelOutcome.Failed, string.Empty, error);
}

/// <summary>
/// ISAPI so'rovini terminalning ISUP ulanishi ichidan yuboradi.
/// </summary>
/// <remarks>
/// Faqat MATN o'tadi: usul, yo'l (so'rov parametrlari bilan) va JSON/XML
/// tana. <c>Content-Type</c> sarlavhasi yo'q — ya'ni multipart ham yo'q
/// (yuz rasmi URL orqali, <see cref="IsupPassthroughHandler"/>).
/// </remarks>
internal interface IIsapiTunnel
{
    Task<TunnelResponse> SendAsync(
        string deviceId,
        string method,
        string pathAndQuery,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken);
}
