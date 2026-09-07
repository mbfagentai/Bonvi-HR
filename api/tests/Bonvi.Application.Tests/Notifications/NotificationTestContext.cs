using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Notifications;
using Bonvi.Application.Notifications.Abstractions;
using Bonvi.Application.Settings;
using Bonvi.Application.Tests.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Notifications;
using Bonvi.Domain.Recruiting;
using NSubstitute;

namespace Bonvi.Application.Tests.Notifications;

/// <summary>
/// Bildirishnomalar use-case'lari uchun umumiy to'plam.
/// </summary>
/// <remarks>
/// <para>
/// Ikkita bog'liqlik mock EMAS, haqiqiy soxta sinf: manba
/// (<see cref="FakeNotificationSource"/>) va holat jadvali
/// (<see cref="FakeNotificationStates"/>). Sabab: bu bo'limning eng
/// muhim qoidalari («o'qilgani o'qilgan qoladi», «o'chirilgani
/// qaytmaydi») YOZISH va keyingi O'QISH orasidagi bog'liqlikni
/// tekshiradi. Mock bilan bunday testni yozib bo'lmaydi — u faqat
/// «qaysi metod chaqirildi» ni ko'rsatadi, natijani emas.
/// </para>
/// <para>
/// Sukut bo'yicha holat: foydalanuvchida BARCHA ruxsatlar bor va
/// manbalarda hech narsa yo'q. Har bir test faqat o'ziga kerakli yozuvni
/// qo'shadi — va shu yozuv testning mazmuni bo'lib ko'rinadi.
/// </para>
/// </remarks>
internal sealed class NotificationTestContext
{
    /// <summary>Testlardagi «bugun» — qat'iy sana, chunki hamma shart kunlarda hisoblanadi.</summary>
    public static readonly DateOnly Today = new(2026, 8, 29);

    /// <summary>Testlardagi «hozir».</summary>
    public static readonly DateTimeOffset Now =
        new(Today.ToDateTime(new TimeOnly(9, 0)), TimeSpan.FromHours(5));

    public NotificationTestContext()
    {
        Clock.Today.Returns(Today);
        Clock.UtcNow.Returns(Now);

        // Sukut bo'yicha hamma ruxsat bor: ruxsat testlari ularni ATAYLAB
        // olib tashlaydi va shu farq testning o'zi bo'ladi.
        CurrentUser.UserId.Returns(UserId);
        CurrentUser.IsAuthenticated.Returns(true);
        CurrentUser.HasPermission(Arg.Any<string>()).Returns(true);

        Employees
            .ListMissingProbationsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<MissingProbationRow>)[.. MissingProbations]);
    }

    /// <summary>Testdagi foydalanuvchi.</summary>
    public Guid UserId { get; } = Guid.CreateVersion7();

    public FakeNotificationSource Source { get; } = new();

    public IEmployeeReadModel Employees { get; } = Substitute.For<IEmployeeReadModel>();

    public ICurrentUser CurrentUser { get; } = Substitute.For<ICurrentUser>();

    public IClock Clock { get; } = Substitute.For<IClock>();

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public FakeNotificationStates States { get; } = new();

    /// <summary>
    /// Tizim sozlamalari — bildirishnoma CHEGARALARI shu yerdan olinadi.
    /// </summary>
    /// <remarks>
    /// Sukut bo'yicha jadval BO'SH va bu ataylab: shunda yig'uvchi
    /// <c>NotificationRules</c> dagi sukut qiymatlar bilan ishlaydi va
    /// mavjud testlarning hammasi o'zgarmagan shartlarda qoladi.
    /// Chegarani o'zgartiradigan test uni o'zi yozib qo'yadi.
    /// </remarks>
    public FakeSystemSettings Settings { get; } = new();

    /// <summary>Sinov yozuvisiz qolgan munosabatlar — <c>IEmployeeReadModel</c> orqali keladi.</summary>
    public List<MissingProbationRow> MissingProbations { get; } = [];

    public NotificationCollector Collector =>
        new(Source, Employees, new SystemSettingsReader(Settings, Settings.Protector), CurrentUser, Clock);

    /// <summary>Bitta ruxsatni olib tashlaydi — qolganlari joyida qoladi.</summary>
    public void Deny(string permission) => CurrentUser.HasPermission(permission).Returns(false);

    // ────────────────────── Manbalarni to'ldirish ──────────────────────

    /// <summary>Yakunlanmagan sinov muddatini qo'shadi.</summary>
    /// <param name="daysLeft">Tugashiga qolgan kunlar. Manfiy — muddat o'tib ketgan.</param>
    /// <param name="name">Xodim nomi.</param>
    public ProbationDeadlineRow Probation(int daysLeft, string name = "Karimov Aziz")
    {
        var row = new ProbationDeadlineRow(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            name,
            "Metan zavod",
            "Operator",
            Today.AddDays(daysLeft));

        Source.Probations.Add(row);
        return row;
    }

    /// <summary>Tug'ilgan kunni qo'shadi.</summary>
    public BirthdayRow Birthday(DateOnly on, string name, int age = 30)
    {
        var row = new BirthdayRow(
            Guid.CreateVersion7(),
            name,
            new DateOnly(on.Year - age, on.Month, on.Day));

        Source.Birthdays.Add(row);
        return row;
    }

    /// <summary>Yangi nomzodni qo'shadi.</summary>
    public NewCandidateRow Candidate(string name = "Rahimov Bobur")
    {
        var row = new NewCandidateRow(
            Guid.CreateVersion7(),
            name,
            "Sotuvchi-konsultant",
            CandidateSource.Manual,
            Today.AddDays(-1));

        Source.Candidates.Add(row);
        return row;
    }

    /// <summary>Sinov yozuvisiz qolgan munosabatni qo'shadi.</summary>
    public MissingProbationRow MissingProbation(string name = "Toshmatov Olim")
    {
        var row = new MissingProbationRow(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            name,
            null,
            "Bonvi",
            "Metan zavod",
            "Operator",
            Today.AddDays(-400),
            400);

        MissingProbations.Add(row);
        return row;
    }
}

/// <summary>
/// Bildirishnoma shartlarining xotiradagi manbai.
/// </summary>
/// <remarks>
/// Ro'yxatlarga to'g'ridan-to'g'ri yoziladi va chegaralar (sana, holat)
/// ATAYLAB qo'llanmaydi: ular bazadagi so'rovning vazifasi va integratsiya
/// testlarida haqiqiy PostgreSQL'da tekshiriladi. Bu yerda tekshirilayotgan
/// narsa boshqa — qator qanday bildirishnomaga aylanishi.
/// </remarks>
internal sealed class FakeNotificationSource : INotificationReadModel
{
    public List<ProbationDeadlineRow> Probations { get; } = [];

    public List<BirthdayRow> Birthdays { get; } = [];

    public List<NewCandidateRow> Candidates { get; } = [];

    /// <summary>Manba so'rovlari nechta marta chaqirilgani — ruxsat testlari uchun.</summary>
    public int ProbationCalls { get; private set; }

    /// <summary>Nomzodlar so'ralgani — ruxsatsiz odam uchun so'rov umuman ketmasligi kerak.</summary>
    public int CandidateCalls { get; private set; }

    /// <summary>
    /// Manbaga uzatilgan CHEGARALAR — sozlama testlari uchun.
    /// </summary>
    /// <remarks>
    /// Chegaralar bu yerda QO'LLANMAYDI (ro'yxatlar o'z holicha
    /// qaytadi), lekin ESLAB QOLINADI: sozlama chegarani haqiqatan
    /// o'zgartirganini boshqa yo'l bilan ko'rsatib bo'lmaydi — filtr
    /// bazada bajariladi va u integratsiya testlarida tekshiriladi.
    /// </remarks>
    public DateOnly? LastProbationUntil { get; private set; }

    /// <summary>Yangi nomzodlar uchun so'ralgan quyi sana.</summary>
    public DateOnly? LastCandidateSince { get; private set; }

    public Task<IReadOnlyList<ProbationDeadlineRow>> ListProbationDeadlinesAsync(
        DateOnly until,
        CancellationToken cancellationToken = default)
    {
        ProbationCalls++;
        LastProbationUntil = until;
        return Task.FromResult<IReadOnlyList<ProbationDeadlineRow>>([.. Probations]);
    }

    public Task<IReadOnlyList<BirthdayRow>> ListBirthdaysAsync(
        DateOnly today,
        DateOnly tomorrow,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BirthdayRow>>([.. Birthdays]);

    public Task<IReadOnlyList<NewCandidateRow>> ListNewCandidatesAsync(
        DateOnly since,
        CancellationToken cancellationToken = default)
    {
        CandidateCalls++;
        LastCandidateSince = since;
        return Task.FromResult<IReadOnlyList<NewCandidateRow>>([.. Candidates]);
    }
}

/// <summary>
/// Bildirishnoma holatlarining xotiradagi jadvali.
/// </summary>
/// <remarks>
/// Haqiqiy repozitoriy kabi ishlaydi: <c>Add</c> qilingan yozuv keyingi
/// <c>GetAsync</c> da qaytadi. Aynan shu bog'liqlik bo'limning asosiy
/// qoidalarini tekshirish imkonini beradi.
/// </remarks>
internal sealed class FakeNotificationStates : INotificationStateRepository
{
    private readonly List<NotificationState> _rows = [];

    /// <summary>Saqlangan barcha yozuvlar — tekshiruv uchun.</summary>
    public IReadOnlyList<NotificationState> Rows => _rows;

    public Task<IReadOnlyList<NotificationState>> GetAsync(
        Guid userId,
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        IReadOnlyList<NotificationState> found =
        [
            .. _rows.Where(row => row.UserId == userId && keys.Contains(row.Key, StringComparer.Ordinal)),
        ];

        return Task.FromResult(found);
    }

    public Task<NotificationState?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_rows.Find(row => row.Id == id));

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_rows.Exists(row => row.Id == id));

    public void Add(NotificationState aggregate) => _rows.Add(aggregate);

    public void Remove(NotificationState aggregate) => _rows.Remove(aggregate);
}
