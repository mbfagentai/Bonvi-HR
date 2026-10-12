using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.EmployeeAccounts;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Identity.EmployeeAuth;

/// <summary>Xodim ilovasiga kirish so'rovi.</summary>
/// <param name="Login">
/// Telefon raqami: 9 xona yoki +998 bilan — xodim profilidagi ASOSIY telefon.
/// </param>
/// <param name="Password">Ochiq parol. Hech qayerga yozilmaydi va loglanmaydi.</param>
/// <param name="IpAddress">So'rov kelgan manzil — token zanjiri uchun.</param>
public sealed record EmployeeLoginCommand(string? Login, string? Password, string? IpAddress);

/// <summary>
/// Xodimni ilovaga kiritadi.
/// </summary>
/// <remarks>
/// <para><b>Tekshiruv tartibi va uning sabablari:</b></para>
/// <list type="number">
///   <item>
///     <b>Login — profil telefoni.</b> Hisob <c>employees.phone</c> orqali
///     topiladi (<see cref="IEmployeeAccountRepository.FindByLoginAsync"/>):
///     HR profilda raqamni tuzatsa, yangi raqam SHU ZAHOTI ishlaydi, eskisi
///     esa yo'q. Raqam ikki o'chirilmagan xodimda bo'lsa — hech kim kirmaydi.
///   </item>
///   <item>
///     <b>Bitta rad javobi.</b> «Bunday raqam yo'q», «raqam takror»,
///     «parol noto'g'ri», «xodim o'chirilgan» va «xodim ishdan ketgan» —
///     hammasi <c>xodim.notogri_malumot</c>. Ajratilsa, endpoint «bu raqam
///     Bonvi xodimimi?» degan savolga javob beradigan ochiq xizmatga aylanardi.
///   </item>
///   <item>
///     <b>Vaqt bo'yicha oshkor qilmaslik.</b> Hisob topilmasa ham soxta
///     xesh ustida tekshiruv bajariladi — aks holda javob vaqtidagi farq
///     loginning bor-yo'qligini aytib berardi (<c>LoginCommandHandler</c>
///     dagi bilan bir xil usul).
///   </item>
///   <item>
///     ⭐ <b>INV-1 va INV-2 — PAROL TO'G'RI chiqqandan KEYIN.</b> Xodimning
///     o'chirilgani va ishdan ketgani tashqi kuzatuvchidan yashirin qolishi
///     kerak, ya'ni bu tekshiruvlar noto'g'ri parol bilan ham bir xil
///     javob berishi shart. Ularni oldinga qo'yish javob vaqtini sezilarli
///     farqlantirardi.
///   </item>
///   <item>
///     ⚠️ <b>INV-2 nega umuman kerak.</b> Ishdan ketgan odamning yozuvi
///     o'chirilmaydi — raqami ham, hisobi ham joyida. Qo'lda «hisobni yopish»
///     yo'q, ya'ni ishdan ketish YAGONA o'chirgich va uni hech qanday cheklov
///     (constraint) ifodalay olmaydi; faqat shu yerdagi tekshiruv, yangilash
///     va <c>/api/me/*</c> to'sig'i (<see cref="EmployeeAccessGate"/>).
///   </item>
/// </list>
/// <para>
/// ⭐ <b>Blok — (kiritilgan login, manba IP) juftligida</b>
/// (<see cref="EmployeeLoginThrottle"/>), 5 xatodan keyin 15 daqiqa, hisob
/// qatorida EMAS. Ilgari blok hisobda edi va parol tekshiruvidan OLDIN
/// <c>xodim.bloklangan</c> qaytarardi: bu (a) raqam Bonvi xodiminiki ekanini
/// oshkor qilardi — notanish raqam hech qachon bloklanmasdi — va (b) istalgan
/// hamkasbni hamma joyda 15 daqiqaga qulflab qo'yish imkonini berardi. Endi
/// notanish raqam ham xuddi shunday bloklanadi, bloklangan esa — hujumchining
/// IP'si. Blok davomida parol tekshirilmaydi (sababi throttle izohida).
/// IP bo'yicha umumiy oqimni tezlik cheklovi chegaralaydi.
/// </para>
/// </remarks>
public sealed class EmployeeLoginCommandHandler
{
    /// <summary>Hisob topilmaganda tekshiriladigan soxta xesh.</summary>
    /// <remarks>
    /// Xeshlagichning O'ZI orqali yasaladi (qo'lda yozilgan satr dekodlashda
    /// istisno tashlab, «topilmadi» ni 500 ga aylantirardi) va bir marta
    /// keshlanadi: bizga uning qiymati emas, TEKSHIRUV VAQTI kerak.
    /// </remarks>
    private static readonly Lock DummyHashLock = new();
    private static string? _dummyHash;

    private readonly IEmployeeAccountRepository _accounts;
    private readonly IEmployeeRefreshTokenRepository _refreshTokens;
    private readonly IEmployeeRepository _employees;
    private readonly IEmploymentRepository _employments;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmployeeTokenService _tokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IEmployeeAuthJournal _journal;
    private readonly EmployeeLoginThrottle _throttle;

    public EmployeeLoginCommandHandler(
        IEmployeeAccountRepository accounts,
        IEmployeeRefreshTokenRepository refreshTokens,
        IEmployeeRepository employees,
        IEmploymentRepository employments,
        IPasswordHasher passwordHasher,
        IEmployeeTokenService tokens,
        IUnitOfWork unitOfWork,
        IClock clock,
        IEmployeeAuthJournal journal,
        EmployeeLoginThrottle throttle)
    {
        _accounts = accounts;
        _refreshTokens = refreshTokens;
        _employees = employees;
        _employments = employments;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _journal = journal;
        _throttle = throttle;
    }

    public async Task<Result<EmployeeAuthResultDto>> HandleAsync(
        EmployeeLoginCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = _clock.UtcNow;
        var login = PhoneNumber.Create(command.Login);
        var key = EmployeeLoginThrottle.Key(login.IsSuccess ? login.Value.Value : null, command.Login);

        // Blok HAMMADAN OLDIN va hisobni qidirmasdan: bloklangan juftlikka
        // hisob bor-yo'qligidan qat'i nazar bir xil javob, parol esa umuman
        // tekshirilmaydi — aks holda blok «parol to'g'rimi?» oracle'iga aylanardi.
        if (_throttle.LockedFor(key, command.IpAddress, now) is { } remaining)
        {
            return EmployeeAccountErrors.LockedOut((int)Math.Ceiling(remaining.TotalMinutes));
        }

        if (login.IsFailure)
        {
            // Format xatosi ham umumiy javob bilan qaytadi: «telefon.uzunlik»
            // hujumchiga raqamning tizimda borligi haqida hech narsa
            // aytmasa-da, javoblarni ikki toifaga ajratib qo'yardi. U ham
            // urinish sifatida sanaladi — xuddi shu sababdan.
            _throttle.RecordFailure(key, command.IpAddress, now);
            return EmployeeAccountErrors.InvalidCredentials;
        }

        var account = await _accounts.FindByLoginAsync(login.Value, cancellationToken).ConfigureAwait(false);

        // HR parolini hali bermagan hisob (avtomatik yasalgan, hech kim ko'rmagan) —
        // notanish raqam bilan AYNAN bir xil yo'l: soxta xesh, sanoq, umumiy javob.
        // Shunda «hisob bor, lekin berilmagan» tashqaridan ko'rinmaydi, va hech kim
        // bilmaydigan parolni taxmin qilishning ma'nosi qolmaydi
        // (<see cref="EmployeeAccount.PasswordHandedOutAt"/>). HR esa sababni
        // kartadagi «Ilova» bo'limida ko'radi — jurnal kerak emas.
        if (account is null || !account.CanSignIn)
        {
            _ = _passwordHasher.Verify(GetDummyHash(), command.Password ?? string.Empty);

            // Notanish raqam ham sanaladi va bloklanadi — mavjud hisob bilan
            // aynan bir xil: blokning o'zi hisob borligini aytib qo'ymasligi kerak.
            _throttle.RecordFailure(key, command.IpAddress, now);
            return EmployeeAccountErrors.InvalidCredentials;
        }

        var (succeeded, needsRehash) = _passwordHasher.Verify(account.PasswordHash, command.Password ?? string.Empty);

        if (!succeeded)
        {
            _throttle.RecordFailure(key, command.IpAddress, now);
            return EmployeeAccountErrors.InvalidCredentials;
        }

        // Parol to'g'ri — juftlikning tarixi o'chadi. Quyidagi rad etishlar
        // (o'chirilgan yoki ishdan ketgan xodim) parol taxminlash emas.
        _throttle.RecordSuccess(key, command.IpAddress);

        // ── INV-1: xodim o'chirilganmi (global filtr shu yerda ishlaydi) ──
        var employee = await _employees.GetByIdAsync(account.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            _journal.EmployeeMissing(account.Id);
            return EmployeeAccountErrors.InvalidCredentials;
        }

        // ── INV-2: bugunni qamrab oluvchi mehnat munosabati bormi ──
        //
        // Ishdan ketish SANA bo'yicha kuchga kiradi, HR yozib qo'ygan lahzada
        // emas — terminal ro'yxatidagi bilan aynan bir xil qoida. Qayta ishga
        // olingan odamda esa yangi munosabat paydo bo'lishi bilan hisob o'zi
        // ochiladi (o'sha parol bilan) — egasining qarori: o'chirgich
        // faqat ishdan ketish.
        if (!await _employments.IsEmployedOnAsync(employee.Id, now.TashkentDate(), cancellationToken).ConfigureAwait(false))
        {
            _journal.NotEmployed(employee.Id);
            return EmployeeAccountErrors.InvalidCredentials;
        }

        if (needsRehash)
        {
            account.RehashPassword(_passwordHasher.Hash(command.Password!));
        }

        account.RecordSuccessfulLogin(now);

        var pair = _tokens.CreateTokens(account);

        _refreshTokens.Add(EmployeeRefreshToken.Issue(
            account.Id,
            pair.RefreshTokenHash,
            pair.RefreshTokenExpiresAt,
            now,
            command.IpAddress));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new EmployeeAuthResultDto(
            pair.AccessToken,
            pair.AccessTokenExpiresAt,
            pair.RefreshToken);
    }

    /// <summary>Soxta xeshni bir marta hisoblab, keshlab qo'yadi.</summary>
    private string GetDummyHash()
    {
        if (_dummyHash is not null)
        {
            return _dummyHash;
        }

        lock (DummyHashLock)
        {
            _dummyHash ??= _passwordHasher.Hash(Guid.NewGuid().ToString("N"));
        }

        return _dummyHash;
    }
}
