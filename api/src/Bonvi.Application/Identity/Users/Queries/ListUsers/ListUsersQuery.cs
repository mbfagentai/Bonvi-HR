using Bonvi.Application.Identity.Users.Common;
using Bonvi.Application.Identity.Users.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.Users.Queries.ListUsers;

/// <summary>Foydalanuvchilar ro'yxatini so'rash.</summary>
/// <param name="Search">
/// F.I.Sh. yoki LOGIN (telefon raqami) bo'yicha qidiruv. Bo'sh — hammasi.
/// <para>
/// ⚠️ Xodimlar qidiruvidan ATAYLAB tor: foydalanuvchida JSHSHIR ham,
/// pasport ham yo'q (hisob — bu tizimga kirish huquqi, shaxs kartasi
/// emas). Bu farq ekranda ham ko'rinadi: qidiruv maydonining matni
/// boshqacha.
/// </para>
/// </param>
public sealed record ListUsersQuery(string? Search = null);

/// <summary>
/// Tizimdagi barcha hisoblarni qaytaradi — faol va faolsizlarini ham.
/// </summary>
/// <remarks>
/// <para>
/// Faolsizlar ATAYLAB yashirilmaydi: administrator xato bloklangan hisobni
/// shu ro'yxatning o'zidan qayta faollashtiradi. Sahifalash yo'q — tizimda
/// bor-yo'g'i bir nechta hisob, ular hammasi bitta ekranga sig'adi.
/// </para>
/// <para>
/// Qidiruv esa BOR va u serverda bajariladi. Ro'yxat kichik bo'lsa ham
/// klientda kesish tanlanmadi: ism yig'ish (қ/ғ/ҳ/ў/ё) va telefon
/// ajratgichlarini tashlash qoidalari bazada yashaydi va ularning
/// ikkinchi nusxasi vaqt o'tib birinchisidan ajralib ketardi — bu
/// loyihada aynan shunday xato allaqachon bo'lgan.
/// </para>
/// <para>
/// Rollar bitta qo'shimcha so'rov bilan olinadi va xotirada moslashtiriladi:
/// foydalanuvchilar soni ham, rollar soni ham juda kichik (2 ta rol), shu
/// sababli har bir foydalanuvchi uchun alohida so'rov ortiqcha bo'lardi.
/// </para>
/// </remarks>
public sealed class ListUsersQueryHandler
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;

    public ListUsersQueryHandler(IUserRepository users, IRoleRepository roles)
    {
        _users = users;
        _roles = roles;
    }

    public async Task<Result<IReadOnlyList<UserSummaryDto>>> HandleAsync(
        ListUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Bo'sh matn `null` ga aylantiriladi: «?search=» — «qidiruvsiz»,
        // «hech narsa topilmasin» emas (barcha ro'yxatlarda bir xil qoida).
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        var users = await _users.ListAsync(search, cancellationToken).ConfigureAwait(false);
        var roles = await _roles.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var dtos = users
            .Select(user => UserSummaryDtoFactory.Create(user, roles))
            .OrderBy(dto => dto.FullName, StringComparer.CurrentCulture)
            .ToList();

        return Result.Success<IReadOnlyList<UserSummaryDto>>(dtos);
    }
}
