using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Common.Photos;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Queries;

/// <summary>O'tishning turniket surati (<c>GET /passes/{id}/photo</c>).</summary>
/// <param name="PassId">O'tish.</param>
/// <param name="KnownVersion">Mijozdagi nusxa versiyasi (<c>If-None-Match</c>) — mos kelsa, baytlar qayta yuborilmaydi.</param>
public sealed record GetPassPhotoQuery(Guid PassId, string? KnownVersion);

/// <summary><see cref="GetPassPhotoQuery"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// ⭐ <b>Surat faqat shu yo'l bilan olinadi</b>, kalitdan yasalgan saqlash
/// yo'li bilan emas (SPEC §6.5): kalit — ichki tafsilot, ruxsat
/// (<c>attendance.view</c>) esa shu endpointda tekshiriladi.
/// </para>
/// <para>
/// Ikki xil 404: o'tishning o'zi yo'q (havola eskirgan, saqlash muddati
/// tugagan) — <c>davomat.otish_topilmadi</c>; o'tish bor, surati yo'q yoki
/// fayl tozalangan — <c>davomat.surat_yoq</c>.
/// </para>
/// <para>
/// Versiya va kesh sarlavhalari xodim rasmi bilan bir xil qoida
/// (<see cref="PhotoLimits.VersionOf"/>, <c>PhotoHttp</c>): kalit o'zgarmaydi,
/// ya'ni versiya ham o'zgarmaydi.
/// </para>
/// </remarks>
public sealed class GetPassPhotoQueryHandler
{
    private readonly IAttendanceReadModel _readModel;
    private readonly IFileStorage _storage;

    public GetPassPhotoQueryHandler(IAttendanceReadModel readModel, IFileStorage storage)
    {
        _readModel = readModel;
        _storage = storage;
    }

    public async Task<Result<PhotoContent>> HandleAsync(
        GetPassPhotoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pass = await _readModel.FindPassAsync(query.PassId, cancellationToken).ConfigureAwait(false);
        if (pass is null)
        {
            return AttendanceErrors.PassNotFound;
        }

        if (pass.PhotoKey is not { } key)
        {
            return AttendanceErrors.PassPhotoMissing;
        }

        var version = PhotoLimits.VersionOf(key);
        var contentType = ContentTypeOf(key);

        if (string.Equals(query.KnownVersion, version, StringComparison.Ordinal))
        {
            return new PhotoContent(version, contentType, Content: null);
        }

        var content = await _storage.OpenAsync(key, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            return AttendanceErrors.PassPhotoMissing;
        }

        return new PhotoContent(version, contentType, content);
    }

    /// <summary>Tur kalit kengaytmasidan.</summary>
    /// <remarks>
    /// Qabul yo'li suratni MAZMUNIDAN aniqlab, mos kengaytma bilan saqlaydi
    /// (<c>PassPhotoStore</c>: JPEG yoki PNG), ya'ni kengaytma ishonchli.
    /// </remarks>
    private static string ContentTypeOf(string key)
        => key.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
}
