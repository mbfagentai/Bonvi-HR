using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
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
    /// ⚠️ <c>AsNoTracking</c> ATAYLAB yo'q: topilgan hisob darhol o'zgaradi —
    /// muvaffaqiyatli kirishda <c>last_login_at</c>, xatoda esa
    /// urinishlar hisoblagichi yoziladi. Kuzatilmagan obyektdagi
    /// o'zgarish jimgina yo'qolardi va blok umuman ishlamas edi.
    /// </remarks>
    public Task<EmployeeAccount?> FindByLoginAsync(
        PhoneNumber login,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(login);

        // Qiymat obyektining O'ZI solishtiriladi, `.Value` emas: `Login`
        // konvertor orqali `text` ga tushgan, ya'ni EF uchun u skalyar va
        // butun obyekt `employee_accounts.login = @p0` ga aylanadi.
        // `.Value` ga murojaat esa SQL'ga tarjima bo'lmasdi
        // (`UserRepository.GetByLoginAsync` dagi bilan bir xil to'siq).
        return Set.FirstOrDefaultAsync(account => account.Login == login, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks><c>ux_employee_accounts_employee</c> bo'yicha aniq zarba.</remarks>
    public Task<EmployeeAccount?> FindByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(account => account.EmployeeId == employeeId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> LoginExistsAsync(
        PhoneNumber login,
        Guid? exceptAccountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(login);

        var query = Set.AsNoTracking().Where(account => account.Login == login);

        if (exceptAccountId is { } id)
        {
            query = query.Where(account => account.Id != id);
        }

        return query.AnyAsync(cancellationToken);
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
    /// ⚠️ Qulf JORIY tranzaksiya oxirigacha turadi. Bu yo'lda alohida
    /// tranzaksiya ochilmaydi: <c>SaveChangesAsync</c> o'zining ichki
    /// tranzaksiyasini ochadi, qulf esa u yopilgan lahzada bo'shaydi.
    /// Shu sababli qulf so'rovi bilan saqlash ORASIDA uzoq ish
    /// bajarilmasligi kerak — chaqiruvchida ular orasida faqat bitta
    /// indeks bo'yicha o'qish bor.
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
