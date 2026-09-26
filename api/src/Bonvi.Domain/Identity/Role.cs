using Bonvi.Domain.Common;

namespace Bonvi.Domain.Identity;

/// <summary>
/// Rol — ruxsatlar to'plami. Foydalanuvchiga rol beriladi, ruxsat emas.
/// </summary>
public sealed class Role : AggregateRoot, IAuditable
{
    /// <summary>Rol nomining eng katta uzunligi.</summary>
    /// <remarks>
    /// Nomni endi egasi o'zi yozadi («Kadr bo'limi boshlig'i»). Chegara
    /// ro'yxat va tanlash maydonida sig'adigan nom uchun — undan uzuni
    /// nom emas, tavsif.
    /// </remarks>
    public const int MaxNameLength = 100;

    private readonly List<string> _permissions = [];

    private Role(Guid id, string code, string name, bool isSystem)
        : base(id)
    {
        Code = code;
        Name = name;
        IsSystem = isSystem;
    }

    /// <summary>EF Core uchun.</summary>
    private Role()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    /// <summary>Mashina uchun kod: <c>hr_manager</c>. O'zgarmaydi.</summary>
    public string Code { get; private set; }

    /// <summary>Foydalanuvchiga ko'rinadigan nom.</summary>
    public string Name { get; private set; }

    /// <summary>Tizim roli — seeder yaratgan (bazadagi bayroq).</summary>
    public bool IsSystem { get; private set; }

    /// <summary>
    /// Himoyalangan rol — rollar muharriri uni o'zgartirmaydi va o'chirmaydi.
    /// </summary>
    /// <remarks>
    /// Bazada saqlanmaydi, KODDAN hisoblanadi (<see cref="SystemRoles.Protected"/>):
    /// jadvaldagi bayroqni qo'lda almashtirib himoyani olib tashlab bo'lmasligi kerak.
    /// </remarks>
    public bool IsProtected => SystemRoles.Protected.Contains(Code);

    public IReadOnlyList<string> Permissions => _permissions.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<Role> Create(string? code, string? name, bool isSystem = false)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Error.Validation("rol.kod_bosh", "Rol kodi kiritilishi shart.");
        }

        var checkedName = ValidateName(name);
        if (checkedName.IsFailure)
        {
            return checkedName.Error;
        }

        return new Role(NewId(), code.Trim().ToLowerInvariant(), checkedName.Value, isSystem);
    }

    /// <summary>
    /// Rol nomini tekshiradi va probellardan tozalangan ko'rinishini qaytaradi.
    /// </summary>
    /// <remarks>
    /// Ochiq va statik: rollar muharriri nomni rolga TEGMASDAN oldin
    /// tekshiradi — keyingi qadamlardan biri (masalan «nom band») rad etsa,
    /// agregat yarim o'zgargan holda qolmasligi kerak.
    /// </remarks>
    public static Result<string> ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("rol.nom_bosh", "Rol nomi kiritilishi shart.");
        }

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            return Error.Validation(
                "rol.nom_uzun",
                $"Rol nomi {MaxNameLength} belgidan oshmasligi kerak.");
        }

        return trimmed;
    }

    /// <summary>
    /// Ruxsatlar ro'yxatini tekshiradi: har bir kod mavjud bo'lishi kerak,
    /// takrorlar olib tashlanadi, tartib saqlanadi.
    /// </summary>
    /// <remarks>
    /// <see cref="ValidateName"/> bilan bir xil sabab bilan ochiq va statik:
    /// chaqiruvchi ro'yxatni rolga yozishdan OLDIN boshqa qoidalarga ham
    /// solishtiradi (masalan «o'zingizda yo'q ruxsat»).
    /// </remarks>
    public static Result<IReadOnlyList<string>> ValidatePermissions(IEnumerable<string?>? permissions)
    {
        var result = new List<string>();

        foreach (var permission in permissions ?? [])
        {
            if (permission is null || !Domain.Identity.Permissions.Exists(permission))
            {
                return Error.Validation("rol.notanish_ruxsat", $"«{permission}» — bunday ruxsat mavjud emas.");
            }

            if (!result.Contains(permission, StringComparer.Ordinal))
            {
                result.Add(permission);
            }
        }

        return Result.Success<IReadOnlyList<string>>(result);
    }

    /// <summary>
    /// Ruxsatlar ro'yxatini TO'LIQ almashtiradi — rollar muharriri va seeder uchun.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bitta-bitta <see cref="Grant"/>/<see cref="Revoke"/> emas, butun
    /// ro'yxat: muharrir rolning yakuniy holatini yuboradi va u yo to'liq
    /// qabul qilinadi, yo umuman qabul qilinmaydi. Yarmida to'xtagan
    /// almashtirish rolni hech kim so'ramagan holatda qoldirardi.
    /// </para>
    /// <para>
    /// <b>Super administrator bu yerda ham himoyalangan:</b> uning bironta
    /// AMALDAGI ruxsati tushib qolmasligi kerak (<see cref="Revoke"/> bilan
    /// bir xil qoida). Koddan olib tashlangan eski kod esa tushib qolaveradi
    /// — aks holda <see cref="Domain.Identity.Permissions.LegacyEmploymentManage"/>
    /// kabi kodlar undan hech qachon ketmasdi.
    /// </para>
    /// </remarks>
    public Result ReplacePermissions(IEnumerable<string?>? permissions)
    {
        var checkedPermissions = ValidatePermissions(permissions);
        if (checkedPermissions.IsFailure)
        {
            return checkedPermissions.Error;
        }

        var next = checkedPermissions.Value;

        if (IsLocked && _permissions.Exists(current =>
                Domain.Identity.Permissions.Exists(current) && !next.Contains(current, StringComparer.Ordinal)))
        {
            return LockedError;
        }

        _permissions.Clear();
        _permissions.AddRange(next);

        return Result.Success();
    }

    public Result Grant(string permission)
    {
        if (!Domain.Identity.Permissions.Exists(permission))
        {
            return Error.Validation("rol.notanish_ruxsat", $"«{permission}» — bunday ruxsat mavjud emas.");
        }

        if (!_permissions.Contains(permission, StringComparer.Ordinal))
        {
            _permissions.Add(permission);
        }

        return Result.Success();
    }

    /// <summary>
    /// Ruxsatni olib tashlaydi — super administrator rolidan tashqari.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ilgari cheklov faqat <c>admin</c> roliga tegishli edi, keyin
    /// <see cref="SystemRoles.FullAccess"/> ning ikkala roliga (super
    /// administrator va bosh rahbar) kengaydi. Rollar muharriri paydo
    /// bo'lgach bosh rahbar ODDIY rolga aylandi — uning ruxsatlarini egasi
    /// o'zi belgilaydi — va cheklov yana bitta rolga qaytdi.
    /// </para>
    /// <para>
    /// Sabab o'zgarmagan: tizim egasining rolini ruxsatsiz qoldirish tizimni
    /// boshqarib bo'lmaydigan holatga tushiradi — bunday holatdan chiqish
    /// yo'li esa faqat bazaga qo'lda aralashish bo'lardi.
    /// </para>
    /// </remarks>
    public Result Revoke(string permission)
    {
        if (IsLocked)
        {
            return LockedError;
        }

        _permissions.Remove(permission);
        return Result.Success();
    }

    public bool Has(string permission) => _permissions.Contains(permission, StringComparer.Ordinal);

    public Result Rename(string? name)
    {
        var checkedName = ValidateName(name);
        if (checkedName.IsFailure)
        {
            return checkedName.Error;
        }

        Name = checkedName.Value;
        return Result.Success();
    }

    /// <summary>
    /// Rolning KODINI va nomini almashtiradi — faqat bir martalik ko'chirish uchun.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega bunday metod bor.</b> <see cref="Code"/> — rolning haqiqiy
    /// identifikatori va odatda O'ZGARMAYDI (shu sababli qolgan hamma joyda
    /// <c>private set</c>). Yagona istisno — model o'zgarishi: <c>admin</c>
    /// roli uch rolli modelda <c>super_admin</c> ga aylanadi. Bu ishni
    /// <c>IdentitySeeder</c> bajaradi.
    /// </para>
    /// <para>
    /// <b>Nega yangi rol yaratib, foydalanuvchilarni ko'chirish emas.</b>
    /// Rolning <see cref="Entity.Id"/> si o'zgarmasa, foydalanuvchilardagi
    /// <c>role_ids</c> massivi ham tegilmaydi — hech kim bir lahzaga ham
    /// rolsiz qolmaydi. Yangi rol yaratilsa, har bir foydalanuvchi qatorini
    /// qayta yozish kerak bo'lardi va yarim yo'lda uzilish (masalan ishga
    /// tushish paytida) bir qism hisoblarni ruxsatsiz qoldirardi.
    /// </para>
    /// </remarks>
    public Result Recode(string? code, string? name)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Error.Validation("rol.kod_bosh", "Rol kodi kiritilishi shart.");
        }

        var renamed = Rename(name);
        if (renamed.IsFailure)
        {
            return renamed;
        }

        Code = code.Trim().ToLowerInvariant();
        return Result.Success();
    }

    /// <summary>
    /// Ruxsatlari kamaytirilmaydigan rol — seeder yaratgan super administrator.
    /// </summary>
    /// <remarks>
    /// <see cref="IsProtected"/> dan TORROQ: bot roli ham himoyalangan
    /// (muharrir unga tegmaydi), lekin uning ro'yxatini seeder o'zi
    /// moslashtiradi va undan ortiqcha ruxsat olib tashlanishi kerak.
    /// </remarks>
    private bool IsLocked => IsSystem && string.Equals(Code, SystemRoles.SuperAdmin, StringComparison.Ordinal);

    private static Error LockedError => Error.Conflict(
        "rol.admin_ozgartirilmaydi",
        "Super administrator rolidan ruxsat olib tashlab bo'lmaydi.");
}
