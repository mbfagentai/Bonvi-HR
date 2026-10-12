using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.EmployeeAuth;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.EmployeeAccounts;

/// <summary>
/// Xodimga ilova hisobini AVTOMATIK ochadi — yagona joy.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega avtomatik</b> (egasining qarori, 2026-09-27). ~540 xodimga
/// birma-bir «Hisob ochish» bosish HR ning ishi emas va har bir unutilgan
/// xodim «men kira olmayapman» degan qo'ng'iroqqa aylanardi. Hisob ikki
/// yo'l bilan paydo bo'ladi, ikkalasi ham SHU sinf orqali:
/// </para>
/// <list type="number">
///   <item>fon vazifasi — ishga tushishda va har 15 daqiqada
///   (<see cref="EmployeeAccountProvisioningJob"/>);</item>
///   <item>HR xodim kartasining «Ilova» bo'limini ochgan lahza — vazifani
///   kutmasdan (yangi qabul qilingan odam).</item>
/// </list>
/// <para>
/// Xodim/mehnat/rekruting modullariga TEGILMAYDI: hisob o'sha yerdagi
/// buyruqlarga ulanmaydi, ya'ni ular bu imkoniyatni bilmaydi ham.
/// </para>
/// <para>
/// ⚠️ <b>Poyga.</b> Vazifa va HR ekrani bitta xodimga bir vaqtda hisob
/// ochishi mumkin — ikkinchisini <c>ux_employee_accounts_employee</c>
/// tutadi. Bu nosozlik emas: yutqazgan tomon o'z (saqlanmagan) hisobini
/// kuzatuvdan chiqaradi va g'olibnikini qayta o'qiydi.
/// </para>
/// <para>
/// Audit yozuvi YO'Q: hisob ochish odamning qarori emas, tizim qoidasi.
/// Qachon ochilgani — <c>created_at</c> da.
/// </para>
/// </remarks>
public sealed class EmployeeAccountProvisioner
{
    private readonly IEmployeeAccountRepository _accounts;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISecretProtector _protector;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDatabaseFault _fault;
    private readonly IClock _clock;

    public EmployeeAccountProvisioner(
        IEmployeeAccountRepository accounts,
        IPasswordHasher passwordHasher,
        ISecretProtector protector,
        IUnitOfWork unitOfWork,
        IDatabaseFault fault,
        IClock clock)
    {
        _accounts = accounts;
        _passwordHasher = passwordHasher;
        _protector = protector;
        _unitOfWork = unitOfWork;
        _fault = fault;
        _clock = clock;
    }

    /// <summary>Xodimning hisobi — yo'q bo'lsa shu yerda ochiladi va saqlanadi.</summary>
    /// <param name="employeeId">Xodim (mavjudligini chaqiruvchi tekshirgan).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<EmployeeAccount> EnsureAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var existing = await _accounts.FindByEmployeeAsync(employeeId, cancellationToken).ConfigureAwait(false);

        return existing
            ?? (await CreateAsync(employeeId, cancellationToken).ConfigureAwait(false)).Account;
    }

    /// <summary>Hisob ochadi; kimdir oldinroq ochib ulgurgan bo'lsa — o'shani qaytaradi.</summary>
    /// <param name="employeeId">Xodim.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>Hisob va uni AYNAN SHU chaqiruv ochganmi.</returns>
    public async Task<(EmployeeAccount Account, bool Created)> CreateAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var password = EmployeePassword.Generate();

        var issued = EmployeeAccount.Issue(
            employeeId,
            _passwordHasher.Hash(password),
            _protector.Protect(password),
            _clock.UtcNow);

        if (issued.IsFailure)
        {
            // Bo'sh identifikator yoki bo'sh xesh — dastur xatosi, foydalanuvchi
            // kiritgan qiymat emas.
            throw new InvalidOperationException($"Xodim hisobi ochilmadi: {issued.Error.Code}.");
        }

        var account = issued.Value;
        _accounts.Add(account);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return (account, true);
        }
        catch (Exception exception) when (_fault.IsDuplicateEmployeeAccount(exception))
        {
            // Qo'shilgan (saqlanmagan) obyekt kuzatuvdan chiqadi — aks holda
            // shu so'rovning keyingi saqlashi uni yana yozmoqchi bo'lardi.
            _accounts.Remove(account);

            var winner = await _accounts.FindByEmployeeAsync(employeeId, cancellationToken).ConfigureAwait(false);

            return (winner ?? throw new InvalidOperationException(
                "Xodim hisobi takror deb rad etildi, lekin qayta o'qishda topilmadi."), false);
        }
    }
}

/// <summary>
/// <c>employee-app:provision-accounts</c> — hisobi yo'q HAR BIR o'chirilmagan xodimga hisob ochadi.
/// </summary>
/// <remarks>
/// <para>
/// Ishga tushishda bir marta va har 15 daqiqada. Idempotent: hisobi borlar
/// so'rovga umuman tushmaydi, ya'ni ikkinchi yurish hech narsa qilmaydi.
/// Ishdan ketganlar ham OLINADI — qayta ishga olinganda o'sha hisob va
/// o'sha parol bilan darhol ishlashi uchun (kirishni ishdan ketish
/// to'sadi, hisobning yo'qligi emas).
/// </para>
/// <para>
/// ⚠️ <b>Bo'laklab.</b> Xeshlash ataylab sekin (~0,1 s), ya'ni ~540 xodimli
/// birinchi yurish bir necha o'n soniya davom etadi — shu sababli u fonda,
/// ishga tushishni to'sib qo'ymaydi. Har bir hisob ALOHIDA saqlanadi:
/// yurish o'rtada to'xtasa, ochilganlari yo'qolmaydi va keyingi yurish
/// qolganidan davom etadi.
/// </para>
/// </remarks>
public sealed class EmployeeAccountProvisioningJob
{
    /// <summary>Bir so'rovda nechta xodim olinadi.</summary>
    public const int BatchSize = 50;

    /// <summary>
    /// Bir vaqtda faqat BITTA yurish. Ishga tushishdagi navbat, 15 daqiqalik
    /// jadval va Hangfire'ning qayta urinishi bir lahzaga tushsa, ikki yurish
    /// bir xil xodimlarga parallel hisob ochmoqchi bo'lardi: natija baribir
    /// to'g'ri (noyob indeks + qayta o'qish), lekin har to'qnashuv EF logiga
    /// ERR bo'lib tushardi. Ikkinchi yurish NAVBAT kutadi: birinchisi tugagach
    /// u hisobi yo'qlarni qayta so'raydi, odatda hech kim qolmagan bo'ladi.
    /// (Chiqib ketish emas — testlar va HR «Ilova» dan keyingi yurish
    /// «bajarildi» deb o'ylab, aslida bajarilmagan ishni kutib qolmasin.)
    /// </summary>
    /// <remarks>
    /// Jarayon ichidagi qulf — bitta API nusxasi sharti bilan (xuddi kirish
    /// cheklagichidagi kabi, <c>docs/PENDING_WIRING.md</c>). Ikkinchi nusxa
    /// qo'shilsa ham xato bo'lmaydi: faqat ERR shovqini qaytadi.
    /// </remarks>
    private static readonly SemaphoreSlim SingleRun = new(1, 1);

    /// <summary>Audit jurnalidagi amal nomi — vazifa identifikatori bilan bir xil.</summary>
    public const string AuditOperation = "job:employee-app:provision-accounts";

    private readonly IEmployeeAccountRepository _accounts;
    private readonly EmployeeAccountProvisioner _provisioner;
    private readonly IEmployeeAuthJournal _journal;
    private readonly IAuditScope _auditScope;

    public EmployeeAccountProvisioningJob(
        IEmployeeAccountRepository accounts,
        EmployeeAccountProvisioner provisioner,
        IEmployeeAuthJournal journal,
        IAuditScope auditScope)
    {
        _accounts = accounts;
        _provisioner = provisioner;
        _journal = journal;
        _auditScope = auditScope;
    }

    /// <summary>Hisobi yo'q xodimlarning hammasiga hisob ochadi.</summary>
    /// <param name="cancellationToken">Bekor qilish belgisi (Hangfire beradi).</param>
    /// <returns>Shu yurishda ochilgan hisoblar soni.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        await SingleRun.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Ochilgan hisoblarning audit qatorlari «tizim, shu vazifa» bo'lib
            // yoziladi. Bo'sh yurish (hisobi yo'q xodim qolmagan) hech narsa
            // saqlamaydi, ya'ni jurnalga ham hech narsa tushmaydi.
            using var scope = _auditScope.Use(AuditOperation);

            return await ProvisionAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SingleRun.Release();
        }
    }

    private async Task<int> ProvisionAsync(CancellationToken cancellationToken)
    {
        var created = 0;

        // Himoya: bir xil xodim ikkinchi marta kelsa (hisob ochildi, lekin
        // qandaydir sabab bilan ko'rinmayapti), yurish aylanib qolmasin.
        var seen = new HashSet<Guid>();

        while (true)
        {
            var batch = await _accounts
                .EmployeesWithoutAccountAsync(BatchSize, cancellationToken)
                .ConfigureAwait(false);

            var fresh = batch.Where(seen.Add).ToList();
            if (fresh.Count == 0)
            {
                break;
            }

            foreach (var employeeId in fresh)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (_, isNew) = await _provisioner.CreateAsync(employeeId, cancellationToken).ConfigureAwait(false);
                if (isNew)
                {
                    created++;
                }
            }
        }

        _journal.AccountsProvisioned(created);

        return created;
    }
}
