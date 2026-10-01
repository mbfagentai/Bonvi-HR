using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Api.Mcp.Tools;

/// <summary>
/// Vositalar uchun umumiy lug'atlar va formatlash — bitta joyda, har vosita
/// o'z nusxasini yozmasin.
/// </summary>
/// <remarks>
/// <para>
/// Hamma metod O'QISH kontekstida (<see cref="McpToolContext.Db"/>) va faqat
/// PROYEKSIYA bilan ishlaydi: ruxsat ro'yxatida yo'q ustunga tegsa, PostgreSQL
/// rad etadi (<c>docs/SPEC-mcp.md</c> §4).
/// </para>
/// <para>
/// Lug'atlar o'chirilgan (yumshoq) yozuvlarni ham o'z ichiga oladi
/// (<c>IgnoreQueryFilters</c>): tarixdagi buyruq o'chirilgan bo'limga
/// ishora qilsa, nom baribir ko'rinsin — «?» emas.
/// </para>
/// </remarks>
public static class McpLookups
{
    public static Task<Dictionary<Guid, string>> LegalEntitiesAsync(this McpToolContext t, CancellationToken cancellationToken) =>
        Check(t).Db.LegalEntities.IgnoreQueryFilters()
            .Select(item => new { item.Id, item.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

    public static Task<Dictionary<Guid, string>> DepartmentsAsync(this McpToolContext t, CancellationToken cancellationToken) =>
        Check(t).Db.Departments.IgnoreQueryFilters()
            .Select(item => new { item.Id, item.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

    public static Task<Dictionary<Guid, string>> PositionsAsync(this McpToolContext t, CancellationToken cancellationToken) =>
        Check(t).Db.Positions.IgnoreQueryFilters()
            .Select(item => new { item.Id, item.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

    public static Task<Dictionary<Guid, string>> DirectionsAsync(this McpToolContext t, CancellationToken cancellationToken) =>
        Check(t).Db.Directions.IgnoreQueryFilters()
            .Select(item => new { item.Id, item.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

    public static Task<Dictionary<Guid, string>> TerminationReasonsAsync(this McpToolContext t, CancellationToken cancellationToken) =>
        Check(t).Db.TerminationReasons.IgnoreQueryFilters()
            .Select(item => new { item.Id, item.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

    /// <summary>
    /// Xodimlarning to'liq ismlari (faqat so'ralgan identifikatorlar bo'yicha,
    /// o'chirilganlari ham). Faqat ism ustunlari o'qiladi.
    /// </summary>
    public static async Task<Dictionary<Guid, string>> EmployeeNamesAsync(
        this McpToolContext t,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();

        if (wanted.Count == 0)
        {
            return [];
        }

        var rows = await Check(t).Db.Employees.IgnoreQueryFilters()
            .Where(employee => wanted.Contains(employee.Id))
            .Select(employee => new { employee.Id, employee.FullName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(row => row.Id, row => row.FullName.Full);
    }

    /// <summary>
    /// Tizim foydalanuvchilarining ismlari (kim yozgan / kim qaror qilgan).
    /// Faqat ism ustunlari — login, parol va boshqa hech narsa o'qilmaydi.
    /// </summary>
    public static async Task<Dictionary<Guid, string>> UserNamesAsync(
        this McpToolContext t,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();

        if (wanted.Count == 0)
        {
            return [];
        }

        var rows = await Check(t).Db.Users.IgnoreQueryFilters()
            .Where(user => wanted.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(row => row.Id, row => row.FullName.Full);
    }

    /// <summary>Lug'atdan nom; topilmasa <c>null</c> (JSON'da maydon tushib qoladi).</summary>
    public static string? Name(this Dictionary<Guid, string> map, Guid? id) =>
        id is { } key && map.TryGetValue(key, out var name) ? name : null;

    /// <summary>Pul: <c>{ amount, currency }</c>; <c>null</c> — yo'q yoki ruxsat yo'q.</summary>
    public static object? Money(Money? money, bool visible) =>
        visible && money is not null ? new { amount = money.Amount, currency = money.Currency } : null;

    /// <summary>Telefon: <c>+998 90 123 45 67</c> ko'rinishida.</summary>
    public static string? Phone(PhoneNumber? phone) => phone?.Formatted;

    /// <summary>Tug'ilgan sanadan bugungi yosh.</summary>
    public static int? Age(DateOnly? birthDate)
    {
        if (birthDate is not { } birth)
        {
            return null;
        }

        var today = McpToolContext.Today;
        var age = today.Year - birth.Year;

        if (birth > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    private static McpToolContext Check(McpToolContext t)
    {
        ArgumentNullException.ThrowIfNull(t);
        return t;
    }
}
