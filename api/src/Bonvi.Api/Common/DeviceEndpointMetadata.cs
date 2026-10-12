namespace Bonvi.Api.Common;

/// <summary>
/// Endpoint belgisi: bu yerga davomat QURILMASI (terminal yoki uning agenti) murojaat qiladi.
/// </summary>
/// <remarks>
/// Qabul va agent guruhlariga qo'yiladi (<c>AttendanceIngestEndpoints</c>,
/// <c>AttendanceAgentEndpoints</c>). <see cref="HttpAuditActorSource"/> shu belgi
/// bo'yicha audit bajaruvchisini <c>Device</c> deb yozadi — usiz qurilma yozgan
/// qator «noma'lum odam» (<c>Anonymous</c>) bo'lib ko'rinardi. Yo'l prefiksi bo'yicha
/// emas, belgi bo'yicha: marshrut ko'chirilsa yoki yangisi qo'shilsa, qoida jimgina
/// sinmasin.
/// </remarks>
public sealed class DeviceEndpointMetadata
{
    private DeviceEndpointMetadata()
    {
    }

    /// <summary>Yagona nusxa.</summary>
    public static DeviceEndpointMetadata Instance { get; } = new();
}
