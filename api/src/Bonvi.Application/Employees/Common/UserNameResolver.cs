using Bonvi.Application.Abstractions;

namespace Bonvi.Application.Employees.Common;

/// <summary>
/// Buyruqni KIM kiritgani — foydalanuvchi ismi, so'rov davomida eslab qolinadi.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CatalogNameResolver"/> bilan bir xil naqsh (Scoped kesh), farqi —
/// <see cref="PreloadAsync"/>: mehnat yo'li o'nlab hodisani bir vaqtda chizadi va
/// mualliflar 3–4 kishi. Oldindan bitta so'rov bilan yuklanmasa, har bir yangi muallif
/// uchun alohida so'rov ketardi.
/// </para>
/// <para>
/// Excel'dan ko'chirilgan va tizim yozgan hodisalarda muallif yo'q (<c>RecordedBy</c> bo'sh)
/// — ism ham <c>null</c>: «kiritgan noma'lum» degani emas, «import qilingan» degani.
/// </para>
/// </remarks>
public sealed class UserNameResolver
{
    private readonly IUserNameLookup _lookup;
    private readonly Dictionary<Guid, string?> _names = [];

    public UserNameResolver(IUserNameLookup lookup) => _lookup = lookup;

    /// <summary>Bir nechta muallifni bitta so'rov bilan oldindan yuklaydi.</summary>
    public async Task PreloadAsync(IEnumerable<Guid?> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        List<Guid> missing =
        [
            .. userIds
                .OfType<Guid>()
                .Where(id => !_names.ContainsKey(id))
                .Distinct(),
        ];

        if (missing.Count == 0)
        {
            return;
        }

        var found = await _lookup.GetNamesAsync(missing, cancellationToken).ConfigureAwait(false);

        foreach (var id in missing)
        {
            _names[id] = found.TryGetValue(id, out var name) ? name : null;
        }
    }

    /// <summary>Muallifning ismi; muallif yo'q yoki topilmasa — <c>null</c>.</summary>
    public async Task<string?> NameAsync(Guid? userId, CancellationToken cancellationToken = default)
    {
        if (userId is not { } id)
        {
            return null;
        }

        if (!_names.ContainsKey(id))
        {
            await PreloadAsync([id], cancellationToken).ConfigureAwait(false);
        }

        return _names.GetValueOrDefault(id);
    }
}
