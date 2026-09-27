using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IEmployeeAccountRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class EmployeeAccountRepository : Repository<EmployeeAccount>, IEmployeeAccountRepository
{
    public EmployeeAccountRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Ikki so'rov: avval raqam egalari (ko'pi bilan IKKITA o'qiladi — bittadan
    /// ko'pligini bilish uchun shu yetadi), keyin yagona egasining hisobi
    /// (<c>ux_employee_accounts_employee</c>). Xodimlar jadvalining yumshoq
    /// o'chirish filtri BIRINCHI so'rovda ishlaydi — o'chirilgan xodimning
    /// raqami hech kimga kirish bermaydi va boshqaga «takror» ham sanalmaydi.
    /// </para>
    /// <para>
    /// ⚠️ <c>AsNoTracking</c> hisob so'rovida ATAYLAB yo'q: topilgan hisob
    /// darhol o'zgaradi — muvaffaqiyatli kirishda <c>last_login_at</c>
    /// yoziladi. Kuzatilmagan obyektdagi o'zgarish jimgina yo'qolardi.
    /// </para>
    /// </remarks>
    public async Task<EmployeeAccount?> FindByLoginAsync(
        PhoneNumber login,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(login);

        // Qiymat obyektining O'ZI solishtiriladi, `.Value` emas: `Phone`
        // konvertor orqali `text` ga tushgan, ya'ni EF uchun u skalyar va
        // butun obyekt `employees.phone = @p0` ga aylanadi
        // (`UserRepository.GetByLoginAsync` dagi bilan bir xil to'siq).
        var holders = await Context.Employees
            .AsNoTracking()
            .Where(employee => employee.Phone == login)
            .Select(employee => employee.Id)
            .Take(2)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (holders.Count != 1)
        {
            return null;
        }

        var employeeId = holders[0];

        return await Set
            .FirstOrDefaultAsync(account => account.EmployeeId == employeeId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks><c>ux_employee_accounts_employee</c> bo'yicha aniq zarba.</remarks>
    public Task<EmployeeAccount?> FindByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(account => account.EmployeeId == employeeId, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Identifikator bo'yicha tartiblanadi — ekranda ro'yxat har ochilganda
    /// bir xil ketma-ketlikda chiqsin.
    /// </remarks>
    public async Task<IReadOnlyList<Employee>> PhoneHoldersAsync(
        PhoneNumber phone,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(phone);

        return await Context.Employees
            .AsNoTracking()
            .Where(employee => employee.Phone == phone)
            .OrderBy(employee => employee.Id)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>NOT EXISTS</c> — <c>ux_employee_accounts_employee</c> indeksi
    /// bo'yicha. Tartib — birlamchi kalit: bo'laklar har yurishda bir xil
    /// ketma-ketlikda olinadi.
    /// </remarks>
    public async Task<IReadOnlyList<Guid>> EmployeesWithoutAccountAsync(
        int take,
        CancellationToken cancellationToken = default)
        => await Context.Employees
            .AsNoTracking()
            .Where(employee => !Set.Any(account => account.EmployeeId == employee.Id))
            .OrderBy(employee => employee.Id)
            .Select(employee => employee.Id)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    /// <remarks>
    /// Xodimlar jadvaliga qo'shilish (join) — yumshoq o'chirish filtri shu
    /// yerda ishlaydi, ya'ni o'chirilgan xodimning hisobi <c>null</c> beradi.
    /// </remarks>
    public async Task<Guid?> LiveEmployeeIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var ids = await Set
            .AsNoTracking()
            .Where(account => account.Id == accountId)
            .Join(
                Context.Employees,
                account => account.EmployeeId,
                employee => employee.Id,
                (account, employee) => employee.Id)
            .Take(1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ids.Count == 0 ? null : ids[0];
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// ⭐ Loyihadagi YAGONA aniq qator qulfi va u ATAYLAB xom SQL bilan
    /// yozilgan: LINQ da <c>FOR UPDATE</c> ni ifodalaydigan operator yo'q,
    /// borlari esa (ifoda daraxtiga yashiringan kengaytmalar) «bu yerda
    /// nima uchun qulf bor?» degan savolni o'qiyotgan odamdan
    /// yashirardi. Bu yerda qulf so'rovning o'zida ko'rinib turadi.
    /// </para>
    /// <para>
    /// ⚠️ Qulf JORIY tranzaksiya oxirigacha turadi — ya'ni u FAQAT
    /// <c>IUnitOfWork.ExecuteInTransactionAsync</c> ichida ma'noga ega.
    /// Tranzaksiyasiz chaqirilsa, PostgreSQL so'rovni o'zining yashirin
    /// tranzaksiyasida bajaradi va qulf so'rov tugashi bilan bo'shaydi:
    /// keyingi <c>SaveChangesAsync</c> esa BOSHQA tranzaksiya ochadi va
    /// hech narsa navbatga qo'yilmaydi (ilgari aynan shunday edi — ikki
    /// bosish 1,2 soniya farq bilan ikkita o'tish yozardi). Chaqiruvchi
    /// (<c>MobileCheckInCommandHandler</c>) qulfdan saqlashgacha bo'lgan
    /// hamma ishni bitta tranzaksiyaga o'raydi.
    /// </para>
    /// <para>
    /// ⚠️ <c>AsNoTracking</c> BO'LMASLIGI shart: qulflangan qator
    /// chaqiruvchi tomonidan o'qiladi va o'sha saqlashda boshqa yozuvlar
    /// bilan birga yoziladi.
    /// </para>
    /// <para>
    /// Natija LINQ bilan KENGAYTIRILMAYDI (<c>ToListAsync</c> darhol
    /// bajaradi): ustiga operator qo'yilsa, EF so'rovni ichki so'rovga
    /// o'rab qo'yardi va qulfning qayerga tushishi noaniq bo'lardi.
    /// </para>
    /// </remarks>
    public async Task<EmployeeAccount?> LockAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var locked = await Set
            .FromSql($"SELECT * FROM employee_accounts WHERE id = {accountId} FOR UPDATE")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return locked.Count == 0 ? null : locked[0];
    }
}

/// <summary><see cref="IEmployeeRefreshTokenRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
/// <remarks>
/// <c>RefreshTokenRepository</c> ning aynan nusxasi, boshqa jadval ustida —
/// sababi <see cref="EmployeeRefreshToken"/> izohida.
/// </remarks>
public sealed class EmployeeRefreshTokenRepository
    : Repository<EmployeeRefreshToken>, IEmployeeRefreshTokenRepository
{
    public EmployeeRefreshTokenRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public Task<EmployeeRefreshToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return Set.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Qatorlar XOTIRAGA yuklanadi (ommaviy <c>UPDATE</c> emas): bitta
    /// hisobda bir vaqtda bir nechtadan ortiq tirik token bo'lmaydi va
    /// domen qoidasi (<see cref="EmployeeRefreshToken.Revoke"/> ikkinchi
    /// chaqiruvda hech narsa qilmaydi) shu yerda ishlashi kerak.
    /// </remarks>
    public async Task<int> RevokeAllForAccountAsync(
        Guid employeeAccountId,
        DateTimeOffset at,
        string? byIp,
        CancellationToken cancellationToken = default)
    {
        var active = await Set
            .Where(token => token.EmployeeAccountId == employeeAccountId && token.RevokedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var token in active)
        {
            token.Revoke(at, byIp);
        }

        return active.Count;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>ExecuteDeleteAsync</c> — qatorlarni xotiraga yuklamasdan
    /// o'chiradi: 950 ta hisobda muddati o'tgan tokenlar minglab bo'lishi
    /// mumkin va bu fon vazifasi.
    /// </remarks>
    public Task<int> DeleteExpiredAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
        => Set
            .Where(token => token.ExpiresAt < before)
            .ExecuteDeleteAsync(cancellationToken);
}

/// <summary><see cref="IMobileCheckInRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
/// <remarks>
/// Faqat yozish: qator agregat ildizi emas va unga o'z o'tishidan boshqa
/// yo'l bilan murojaat qilinmaydi. <c>Repository&lt;&gt;</c> dan meros
/// olmaydi — u <c>AggregateRoot</c> talab qiladi.
/// </remarks>
public sealed class MobileCheckInRepository : IMobileCheckInRepository
{
    private readonly AppDbContext _context;

    public MobileCheckInRepository(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public void Add(MobileCheckIn checkIn) => _context.MobileCheckIns.Add(checkIn);
}
