using Bonvi.Domain.Common;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Employees;

/// <summary>
/// Xodim — <b>shaxs</b>. Uning tizimdagi asosiy kartasi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Xodim va mehnat munosabati ajratilgan.</b> Excel'da ular bitta qatorda
/// edi, lekin bu noto'g'ri: Bonvi — holding va bitta shaxs bir vaqtda ikki
/// firmada ishlashi mumkin («VIKOR + Вело унион 0,5 ставка ўриндош»).
/// Shuningdek odam ishdan ketib, keyin qaytib kelishi mumkin — o'shanda
/// yangi <see cref="Employment"/> ochiladi, lekin shaxs o'sha-o'sha.
/// </para>
/// <para>
/// <b>JSHSHIR — asosiy identifikator, lekin majburiy emas.</b> Excel'dagi
/// 540 yozuvdan 3 tasida u umuman yo'q, 9 tasida nazorat raqami xato.
/// Majburiy qilinsa, o'sha xodimlarni ko'chirib bo'lmaydi. Shu sababli
/// maydon bo'sh bo'lishi mumkin va bunday yozuvlar tuzatish ro'yxatiga tushadi.
/// </para>
/// </remarks>
public sealed class Employee : AggregateRoot, IAuditable, ISoftDeletable
{
    private readonly List<Guid> _directionIds = [];

    private Employee(Guid id, FullName fullName, DateOnly? birthDate)
        : base(id)
    {
        FullName = fullName;
        BirthDate = birthDate;
    }

    /// <summary>EF Core uchun.</summary>
    private Employee() => FullName = null!;

    public FullName FullName { get; private set; }

    public DateOnly? BirthDate { get; private set; }

    /// <summary>Jins. JSHSHIR mavjud bo'lsa undan olinadi.</summary>
    public Gender? Gender { get; private set; }

    /// <summary>
    /// JSHSHIR. Unikal, lekin bo'sh bo'lishi mumkin — Excel'dagi
    /// yaroqsiz yozuvlar ko'chirishni to'xtatmasligi uchun.
    /// </summary>
    public Pinfl? Pinfl { get; private set; }

    public PassportSeries? Passport { get; private set; }

    /// <summary>Asosiy telefon raqami.</summary>
    public PhoneNumber? Phone { get; private set; }

    /// <summary>Qo'shimcha raqamlar — Excel'da bitta katakda ikkitasi bor edi.</summary>
    public PhoneNumber? SecondPhone { get; private set; }

    public string? Address { get; private set; }

    /// <summary>Mutaxassisligi — ta'lim va diplom haqidagi erkin matn.</summary>
    public string? Speciality { get; private set; }

    public string? TelegramUrl { get; private set; }

    /// <summary>Rasm fayli (<c>IFileStorage</c> kaliti).</summary>
    public string? PhotoKey { get; private set; }

    /// <summary>
    /// Sudlanganmi — uch holat: <c>true</c>, <c>false</c>, <c>null</c>
    /// («noma'lum»).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega alohida maydon, faqat matn emas.</b>
    /// <see cref="CriminalRecordNote"/> Excel'dan kelgan erkin matn
    /// («судланмаган», «2019 й. 168-модда») va undan javobni ishonchli
    /// o'qib bo'lmaydi: bo'sh matn «sudlanmagan» degani emas, «hech kim
    /// yozmagan» degani. HR uchun esa bu ikkisi butunlay boshqa javob —
    /// birinchisida odam tekshirilgan, ikkinchisida yo'q.
    /// </para>
    /// <para>
    /// Shu sababli JAVOB tanlanadi, matn esa uning TAFSILOTI bo'lib
    /// qoladi: «sudlangan» deyilganda qaysi modda ekani o'sha yerda
    /// yoziladi.
    /// </para>
    /// </remarks>
    public bool? HasCriminalRecord { get; private set; }

    /// <summary>
    /// Sudlanganlik tafsiloti (Excel: «Судланганлик»).
    /// </summary>
    /// <remarks>
    /// Erkin matn — Excel'da bu ustunda modda raqami, sana va izoh
    /// aralash yozilgan. <see cref="HasCriminalRecord"/> javobni beradi,
    /// bu maydon esa tafsilotni.
    /// </remarks>
    public string? CriminalRecordNote { get; private set; }

    /// <summary>mehnat.uz dagi profilga havola. Bo'sh bo'lishi mumkin.</summary>
    public string? MehnatUzUrl { get; private set; }

    /// <summary>Yo'nalishlar — ko'p-ga-ko'p («Вело, метан, шина, логистика»).</summary>
    public IReadOnlyList<Guid> DirectionIds => _directionIds.AsReadOnly();

    /// <summary>
    /// Ma'lumot Excel'dan ko'chirilganda to'liq tekshiruvdan o'tmagan bo'lishi
    /// mumkin. Bu bayroq shunday yozuvlarni ajratib turadi.
    /// </summary>
    public bool NeedsReview { get; private set; }

    /// <summary>Nima tekshirilishi kerakligi (ETL hisobotidan).</summary>
    public string? ReviewNote { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public static Result<Employee> Create(FullName fullName, DateOnly? birthDate = null)
    {
        ArgumentNullException.ThrowIfNull(fullName);

        if (birthDate is { } date)
        {
            var valid = ValidateBirthDate(date);
            if (valid.IsFailure)
            {
                return valid.Error;
            }
        }

        return new Employee(NewId(), fullName, birthDate);
    }

    public Result Rename(FullName fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        FullName = fullName;
        return Result.Success();
    }

    /// <summary>
    /// JSHSHIR o'rnatadi va tug'ilgan sana bilan solishtiradi.
    /// </summary>
    /// <remarks>
    /// Excel'da 527 yozuvdan 32 tasida JSHSHIR ichidagi sana kiritilgan
    /// tug'ilgan sana bilan mos kelmagan. Bu kiritish xatosining aniq belgisi.
    /// Ko'chirishni to'xtatmaydi, lekin tekshirish uchun belgilanadi.
    /// </remarks>
    public Result SetPinfl(Pinfl? pinfl)
    {
        Pinfl = pinfl;

        if (pinfl is null)
        {
            return Result.Success();
        }

        Gender ??= pinfl.Gender;

        if (BirthDate is { } birth && !pinfl.MatchesBirthDate(birth))
        {
            return Error.Validation(
                "xodim.jshshir_sana_mos_emas",
                $"JSHSHIR ichidagi sana ({pinfl.BirthDate:dd.MM.yyyy}) tug'ilgan sana bilan mos kelmadi.");
        }

        BirthDate ??= pinfl.BirthDate;
        return Result.Success();
    }

    /// <summary>
    /// Tug'ilgan sanani tuzatadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega sana tahrirlanadigan bo'lishi SHART.</b> Ko'chirishda
    /// «tekshirish kerak» deb belgilangan yozuvlarning katta qismida
    /// nomuvofiqlik aynan shu juftlikda: JSHSHIR ichidagi sana kartadagi
    /// sana bilan mos kelmaydi. Sana qulflansa, HR to'g'ri JSHSHIRni
    /// kiritganda <c>xodim.jshshir_sana_mos_emas</c> bilan rad javob olardi,
    /// sanani esa tuzata olmasdi — ya'ni yozuvni to'g'rilash yo'li butunlay
    /// berk bo'lardi.
    /// </para>
    /// <para>
    /// Mavjud JSHSHIR bilan moslik shu yerda ham tekshiriladi: karta o'zi
    /// bilan o'zi ziddiyatga tushmasligi kerak. Ikkalasi BIRGA
    /// o'zgarayotgan bo'lsa — <see cref="SetBirthDateAndPinfl"/>.
    /// </para>
    /// </remarks>
    public Result SetBirthDate(DateOnly birthDate) => SetBirthDateAndPinfl(birthDate, Pinfl);

    /// <summary>
    /// Tug'ilgan sana va JSHSHIRni BIRGA o'rnatadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega bitta metod.</b> Ikkala maydon bir-birini tekshiradi. Ular
    /// alohida, ketma-ket o'rnatilsa ORALIQ juftlik hosil bo'lardi —
    /// «yangi JSHSHIR + eski sana» yoki «yangi sana + eski JSHSHIR» — va
    /// tekshiruv aynan shu, hech qachon saqlanmaydigan juftlikni rad
    /// etardi. Natijada ikkala yangi qiymat bir-biriga to'liq mos bo'lsa
    /// ham tahrir o'tmasdi va nomuvofiqlikni bartaraf etib bo'lmasdi.
    /// </para>
    /// <para>
    /// <paramref name="birthDate"/> <c>null</c> bo'lsa sana O'ZGARMAYDI:
    /// qism-qism yangilashda bu «maydon yuborilmadi» degani, «sanani
    /// o'chir» degani emas — tug'ilgan sanani bo'shatish HR uchun
    /// ma'noli amal emas.
    /// </para>
    /// </remarks>
    public Result SetBirthDateAndPinfl(DateOnly? birthDate, Pinfl? pinfl)
    {
        if (birthDate is { } date)
        {
            var valid = ValidateBirthDate(date);
            if (valid.IsFailure)
            {
                return valid.Error;
            }

            BirthDate = date;
        }

        // Tartib MUHIM: sana JSHSHIRdan OLDIN qo'yiladi, shu sababli
        // quyidagi tekshiruv YANGI juftlikni ko'radi, eskisini emas.
        return SetPinfl(pinfl);
    }

    public void SetPassport(PassportSeries? passport) => Passport = passport;

    public void SetPhones(PhoneNumber? primary, PhoneNumber? second = null)
    {
        Phone = primary;
        SecondPhone = second;
    }

    public void SetAddress(string? address) => Address = Trim(address);

    public void SetSpeciality(string? speciality) => Speciality = Trim(speciality);

    public void SetGender(Gender? gender) => Gender = gender;

    public void SetPhoto(string? photoKey) => PhotoKey = photoKey;

    public void SetCriminalRecordNote(string? note) => CriminalRecordNote = Trim(note);

    public void SetHasCriminalRecord(bool? value) => HasCriminalRecord = value;

    /// <summary>
    /// mehnat.uz profiliga havolani o'rnatadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega Telegramdagi kabi «@nom» qabul qilinmaydi.</b> Telegramda
    /// foydalanuvchi nomi havolani bir qiymatli tiklaydi
    /// (<c>t.me/&lt;nom&gt;</c>). mehnat.uz da esa bunday qoida yo'q:
    /// profil manzili qidiruv parametrlari bilan keladi va uni qismlardan
    /// yig'ib bo'lmaydi. Shu sababli bu yerda faqat TO'LIQ manzil qabul
    /// qilinadi.
    /// </para>
    /// <para>
    /// Sxemasiz kiritilgan matnga (<c>mehnat.uz/...</c>) <c>https://</c>
    /// qo'shiladi: HR manzilni brauzerdan nusxalaganda sxema ba'zan
    /// tushib qoladi, uni xato deb rad etish esa foydalanuvchini
    /// «nima yetishmayapti» degan savol bilan qoldirardi. <c>http://</c>
    /// ataylab RAD ETILADI — shifrlanmagan havolani saqlash va uni keyin
    /// yangi oynada ochish shaxsiy ma'lumot uchun ma'noli xavf, mehnat.uz
    /// esa allaqachon HTTPS da ishlaydi.
    /// </para>
    /// </remarks>
    public Result SetMehnatUzUrl(string? url)
    {
        var value = Trim(url);
        if (value is null)
        {
            MehnatUzUrl = null;
            return Result.Success();
        }

        if (!value.Contains("://", StringComparison.Ordinal))
        {
            value = "https://" + value.TrimStart('/');
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
            !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            return Error.Validation(
                "xodim.mehnat_havola",
                "mehnat.uz havolasi «https://» bilan boshlanishi kerak.");
        }

        MehnatUzUrl = value;
        return Result.Success();
    }

    public Result SetTelegram(string? url)
    {
        var value = Trim(url);
        if (value is null)
        {
            TelegramUrl = null;
            return Result.Success();
        }

        // Excel'da to'liq havola ham, faqat foydalanuvchi nomi ham uchraydi
        if (value.StartsWith('@'))
        {
            value = "https://t.me/" + value[1..];
        }
        else if (!value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            value = "https://t.me/" + value.TrimStart('/');
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            return Error.Validation("xodim.telegram", "Telegram havolasi noto'g'ri.");
        }

        TelegramUrl = value;
        return Result.Success();
    }

    public void AssignDirection(Guid directionId)
    {
        if (!_directionIds.Contains(directionId))
        {
            _directionIds.Add(directionId);
        }
    }

    public void RemoveDirection(Guid directionId) => _directionIds.Remove(directionId);

    public void ClearDirections() => _directionIds.Clear();

    /// <summary>Ma'lumot tekshirishga muhtoj deb belgilaydi.</summary>
    public void FlagForReview(string reason)
    {
        NeedsReview = true;
        ReviewNote = string.IsNullOrWhiteSpace(ReviewNote)
            ? reason.Trim()
            : $"{ReviewNote}; {reason.Trim()}";
    }

    /// <summary>Tekshirildi — belgi olib tashlanadi.</summary>
    public void MarkReviewed()
    {
        NeedsReview = false;
        ReviewNote = null;
    }

    /// <summary>Tug'ilgan sananing haqiqiyligini tekshiradi.</summary>
    /// <remarks>
    /// Chegaralar Excel'dagi HAQIQIY xatolardan olingan: kelajakdagi sana
    /// (oddiy kirish xatosi) va 100 yildan katta yosh — u odatda JSHSHIR
    /// asrini noto'g'ri o'qishdan chiqadi («1876-yilda tug'ilgan»).
    /// Yaratish va tahrirlash bir xil chegaraga tayanishi kerak, aks holda
    /// yaratishda rad etilgan sana tahrirlash orqali kirib kelardi.
    /// </remarks>
    private static Result ValidateBirthDate(DateOnly birthDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (birthDate > today)
        {
            return Error.Validation("xodim.tugilgan_sana", "Tug'ilgan sana kelajakda bo'lishi mumkin emas.");
        }

        return today.Year - birthDate.Year > 100
            ? Error.Validation("xodim.tugilgan_sana", "Tug'ilgan sana haqiqiy emas.")
            : Result.Success();
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
