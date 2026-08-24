using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IEmployeeRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
/// <remarks>
/// ⚠️ Qiymat obyekti bo'yicha qidirishda obyektning O'ZI solishtiriladi,
/// uning <c>.Value</c> xossasi EMAS. Sabab: <c>Pinfl</c>, <c>PassportSeries</c>
/// va <c>PhoneNumber</c> konvertor orqali <c>string</c> ga map qilingan, ya'ni
/// EF uchun ular skalyar. <c>.Value</c> ga murojaat SQL'ga tarjima bo'lmaydi
/// (butun jadval xotiraga o'qilib ketardi), butun obyekt esa
/// <c>employees.pinfl = @p0</c> ga aylanadi. Bu xato loyihada bir marta
/// yuz bergan — izohi <c>UserRepository</c> da ham turibdi.
/// </remarks>
public sealed class EmployeeRepository : Repository<Employee>, IEmployeeRepository
{
    public EmployeeRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public Task<Employee?> FindByPinflAsync(Pinfl pinfl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pinfl);

        return Set.FirstOrDefaultAsync(employee => employee.Pinfl == pinfl, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Employee?> FindByPassportAsync(
        PassportSeries passport,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(passport);

        // Pasport unikal emas (gomoglif tufayli dublikatlar bo'lishi mumkin),
        // shu sababli birinchisi qaytariladi — bu «bor-yo'qligini» aniqlash
        // uchun yetarli, dublikatlar esa tekshiruv ro'yxatida ko'rinadi.
        return Set.FirstOrDefaultAsync(employee => employee.Passport == passport, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Taqqoslash ATAYLAB aniq (registrga sezgir): so'rov shundagina
    /// <c>(last_name, first_name)</c> indeksiga tushadi. Bu metod dublikat
    /// izlashning oxirgi zaxira yo'li — u «ehtimoliy mos» ro'yxatini
    /// qaytaradi, yakuniy qarorni odam qabul qiladi. Ismning bir qismi
    /// bo'yicha erkin qidiruv uchun trigramma indeksi bor (migratsiyaga qarang).
    /// </remarks>
    public async Task<IReadOnlyList<Employee>> FindByNameAndBirthDateAsync(
        FullName fullName,
        DateOnly? birthDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fullName);

        var lastName = fullName.LastName;
        var firstName = fullName.FirstName;

        var query = Set
            .AsNoTracking()
            .Where(employee =>
                employee.FullName.LastName == lastName &&
                employee.FullName.FirstName == firstName);

        // Tug'ilgan sana berilmagan bo'lsa u bo'yicha toraytirilmaydi —
        // Excel'dagi 3 ta xodimda JSHSHIR ham, pasport ham, sana ham yo'q.
        if (birthDate is { } date)
        {
            query = query.Where(employee => employee.BirthDate == date);
        }

        return await query
            .OrderBy(employee => employee.FullName.LastName)
            .ThenBy(employee => employee.FullName.FirstName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <paramref name="exceptId"/> tahrirlash uchun: xodimning O'Z JSHSHIR'i
    /// «band» deb hisoblanmasligi kerak, aks holda kartani saqlab bo'lmasdi.
    /// </remarks>
    public Task<bool> PinflExistsAsync(
        Pinfl pinfl,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pinfl);

        return Set.AnyAsync(
            employee => employee.Pinfl == pinfl && (exceptId == null || employee.Id != exceptId),
            cancellationToken);
    }
}
