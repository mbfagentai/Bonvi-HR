namespace Bonvi.Application.System.Queries.GetSystemInfo;

/// <summary>
/// Tizim holati haqidagi ma'lumot.
/// </summary>
/// <param name="Service">Servis nomi.</param>
/// <param name="Version">Versiya.</param>
/// <param name="Environment">Muhit: Development / Testing / Production.</param>
/// <param name="DatabaseConnected">Ma'lumotlar bazasi ulanishi mavjudmi.</param>
/// <param name="ServerTime">Server vaqti (Toshkent, UTC+5).</param>
public sealed record SystemInfoDto(
    string Service,
    string Version,
    string Environment,
    bool DatabaseConnected,
    DateTimeOffset ServerTime);
