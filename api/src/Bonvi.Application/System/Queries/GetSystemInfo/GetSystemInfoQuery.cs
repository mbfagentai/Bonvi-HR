using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.System.Queries.GetSystemInfo;

/// <summary>
/// Tizim holatini so'rash.
/// </summary>
/// <remarks>
/// Bu birinchi vertikal kesim — qolgan barcha use-case'lar shu andozada yoziladi:
/// <c>Query/Command</c> (kirish) → <c>Handler</c> (mantiq) → <c>Dto</c> (chiqish).
/// Mediator kutubxonasi ishlatilmaydi: handler DI orqali to'g'ridan-to'g'ri olinadi.
/// </remarks>
/// <param name="Environment">Joriy muhit nomi.</param>
public sealed record GetSystemInfoQuery(string Environment);

/// <summary>Ma'lumotlar bazasi holatini tekshiruvchi port.</summary>
/// <remarks>
/// Application EF Core'ni bilmaydi — faqat shu interfeysni biladi.
/// Amalga oshirish Infrastructure qatlamida.
/// </remarks>
public interface IDatabaseProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="GetSystemInfoQuery"/> ni bajaradi.
/// </summary>
public sealed class GetSystemInfoQueryHandler
{
    private const string ServiceName = "Bonvi HR API";
    private const string ServiceVersion = "0.1.0";

    private readonly IDatabaseProbe _database;
    private readonly IClock _clock;

    public GetSystemInfoQueryHandler(IDatabaseProbe database, IClock clock)
    {
        _database = database;
        _clock = clock;
    }

    public async Task<Result<SystemInfoDto>> HandleAsync(
        GetSystemInfoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var connected = await _database.CanConnectAsync(cancellationToken).ConfigureAwait(false);

        return new SystemInfoDto(
            ServiceName,
            ServiceVersion,
            query.Environment,
            connected,
            _clock.UtcNow.ToTashkent());
    }
}
