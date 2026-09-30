using System.Globalization;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Attendance.Retention;

/// <summary>Amaldagi saqlash muddatlari — ikkala tozalash vazifasi AYNAN shu qiymatlar bilan ishlaydi.</summary>
/// <param name="PhotoRetentionDays">Surat va xom hodisa necha kun saqlanadi.</param>
/// <param name="PassRetentionDays">O'tish qatori necha kun saqlanadi; <c>0</c> — hech qachon o'chirilmaydi.</param>
public sealed record AttendanceRetentionPolicy(int PhotoRetentionDays, int PassRetentionDays)
{
    /// <summary>O'tishlar umuman o'chirilmaydimi (<c>0</c> — «hech qachon»).</summary>
    public bool KeepsPassesForever => PassRetentionDays == 0;

    /// <summary>O'tish tozalashi ishlatadigan muddat: <c>max(pass, photo)</c> (SPEC §9.2.1).</summary>
    /// <remarks>
    /// Sozlamalar ekrani <c>pass ≥ photo</c> ni allaqachon talab qiladi; bu esa
    /// bazani qo'lda tahrirlagan holat uchun ikkinchi himoya — o'tish suratidan
    /// oldin ketsa ham fayl yetim qolmaydi, lekin surat muddati baribir
    /// o'tishlarni erta o'chirishga sabab bo'lmasligi kerak.
    /// </remarks>
    public int EffectivePassRetentionDays => Math.Max(PassRetentionDays, PhotoRetentionDays);
}

/// <summary>
/// Saqlash muddatlarini o'qiydi va «qayta hisoblanmaydigan kunlar» chegarasini yuritadi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Bitta joyda, chunki uch sinf bir xil javobga tayanadi:</b> surat
/// tozalash, o'tish tozalash va qayta hisoblash
/// (<c>AttendanceDayRecomputer</c>). Muddatni ikki joyda ikki xil o'qish —
/// masalan biri «0» ni «hech qachon», ikkinchisi «bugun» deb tushunishi —
/// aynan qaytarib bo'lmaydigan o'chirishga olib boradi.
/// </para>
/// <para>
/// Sozlama yozuvi bo'lmasa <see cref="SystemSettingsReader"/> sukut qiymatni
/// beradi, ya'ni <see cref="AttendanceRules.DefaultPhotoRetentionDays"/> va
/// <see cref="AttendanceRules.DefaultPassRetentionDays"/>.
/// </para>
/// </remarks>
public sealed class AttendanceRetention
{
    private readonly SystemSettingsReader _settings;
    private readonly ISystemSettingRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AttendanceRetention(
        SystemSettingsReader settings,
        ISystemSettingRepository repository,
        IUnitOfWork unitOfWork)
    {
        _settings = settings;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Amaldagi muddatlar.</summary>
    /// <remarks>
    /// <para>
    /// Yozish yo'li chegaralarni allaqachon tekshiradi. Bu yerdagi ikki qoida
    /// faqat bazani QO'LDA tahrirlagan holat uchun va ikkalasi ham
    /// o'chirmaslik tomoniga og'adi: surat muddati quyi chegaradan past bo'lsa
    /// (masalan qo'lda yozilgan <c>0</c> — bugungi suratlar ketardi) quyi
    /// chegara olinadi; manfiy o'tish muddati «hech qachon» deb o'qiladi —
    /// buzuq qiymat o'chirishga buyruq emas.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<AttendanceRetentionPolicy> ReadAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);

        var photoDays = settings.Number(SystemSettingKeys.PhotoRetentionDays);
        var passDays = settings.Number(SystemSettingKeys.PassRetentionDays);

        var photoFloor = SystemSettingKeys.Find(SystemSettingKeys.PhotoRetentionDays)?.Min
            ?? AttendanceRules.DefaultPhotoRetentionDays;

        return new AttendanceRetentionPolicy(
            PhotoRetentionDays: Math.Max(photoDays, photoFloor),
            PassRetentionDays: Math.Max(passDays, 0));
    }

    /// <summary>Shu sanagacha (o'zi ham) kunlar qayta hisoblanmaydi; hali hech narsa o'chirilmagan bo'lsa <c>null</c>.</summary>
    /// <remarks>
    /// ⚠️ O'qib bo'lmaydigan qiymat — ISTISNO, «chegara yo'q» emas. Qiymatni faqat
    /// server yozadi, ya'ni buzilgani qo'lda aralashuv belgisi; uni jimgina
    /// e'tiborsiz qoldirish o'chirilgan kunlarni qayta hisoblashga ochib,
    /// tabelni nolga aylantirardi. Yiqilgan qayta hisoblash esa jurnalda ko'rinadi.
    /// </remarks>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<DateOnly?> FrozenThroughAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);

        return Parse(settings.Hidden(SystemSettingKeys.AttendanceFrozenThrough));
    }

    /// <summary>
    /// Chegarani <paramref name="date"/> gacha KO'TARADI (hech qachon tushirmaydi) va darhol saqlaydi.
    /// </summary>
    /// <remarks>
    /// O'tish tozalashi buni birinchi qatorni o'chirishdan OLDIN chaqiradi: aks
    /// holda o'chirish bilan yozish orasidagi yiqilish o'chirilgan kunni
    /// himoyasiz qoldirardi.
    /// </remarks>
    /// <param name="date">Yangi chegara — shu sana va undan oldingilar muzlaydi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task FreezeThroughAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var key = SystemSettingKeys.AttendanceFrozenThrough;
        var value = date.ToString(SystemSettingKeys.FrozenThroughFormat, CultureInfo.InvariantCulture);

        var row = await _repository.FindAsync(key, cancellationToken).ConfigureAwait(false);

        if (row is null)
        {
            var created = SystemSetting.CreateServerValue(key, value);
            if (created.IsFailure)
            {
                throw new InvalidOperationException($"«{key}» yozilmadi: {created.Error.Code}");
            }

            _repository.Add(created.Value);
        }
        else
        {
            if (Parse(row.Value) is { } current && current >= date)
            {
                return;
            }

            var applied = row.SetServerValue(value);
            if (applied.IsFailure)
            {
                throw new InvalidOperationException($"«{key}» yozilmadi: {applied.Error.Code}");
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Shu qamrovda keyin o'qilsa — yangi qiymat ko'rinsin.
        _settings.Forget();
    }

    private static DateOnly? Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParseExact(
                value,
                SystemSettingKeys.FrozenThroughFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date)
            ? date
            : throw new InvalidOperationException(
                $"«{SystemSettingKeys.AttendanceFrozenThrough}» qiymati sana emas — qayta hisoblash to'xtatildi.");
    }
}
