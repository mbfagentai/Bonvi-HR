namespace Bonvi.Api.Common;

/// <summary>
/// Endpoint belgisi: bu yerga ochiq onlayn anketa (HR-F02) murojaat qiladi — login ham, kalit ham yo'q.
/// </summary>
/// <remarks>
/// <para>
/// Faqat anketa yo'llariga qo'yiladi (<c>KioskEndpoints</c>, SPEC-planshet-anketa §2).
/// <see cref="HttpAuditActorSource"/> shu belgiga qaraydi: bajaruvchi <c>Anonymous</c>, nomi
/// «Onlayn anketa» (usiz jurnalda «Tizimga kirmagan» bo'lib, qaysi amal ekani ko'rinmasdi).
/// Jurnaldagi IP — <see cref="ClientIp"/> dan, 2026-10-27 dan boshqa endpointlarda ham
/// (<see cref="HttpRequestContext"/>).
/// </para>
/// <para>
/// Yo'l prefiksi bo'yicha emas, belgi bo'yicha (<see cref="DeviceEndpointMetadata"/> bilan bir xil
/// sabab): marshrut ko'chirilsa, qoida jimgina sinmasin.
/// </para>
/// </remarks>
public sealed class OnlineFormEndpointMetadata
{
    /// <summary>Jurnaldagi bajaruvchi nomi.</summary>
    public const string ActorName = "Onlayn anketa";

    private OnlineFormEndpointMetadata()
    {
    }

    /// <summary>Yagona nusxa.</summary>
    public static OnlineFormEndpointMetadata Instance { get; } = new();
}
