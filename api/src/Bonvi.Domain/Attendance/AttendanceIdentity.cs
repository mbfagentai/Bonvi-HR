using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>
/// Qurilmadagi shaxs raqamini xodimga bog'laydigan yozuv.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu ALOHIDA jadval, <c>Employment</c> dagi bitta ustun emas.</b>
/// <c>PLAN.md</c> §8 aynan shunday ustunni («Verifix ID») taklif qilgan
/// edi va u ATAYLAB qurilmadi. Uchta sabab:
/// </para>
/// <list type="number">
///   <item>bitta ustun bitta raqam saqlaydi, egasining talabi esa aniq:
///         bitta xodimda bir NECHTA qurilma raqami bo'lishi mumkin;</item>
///   <item>ustunda tarix yo'q — bog'lash va uzish iz qoldirishi kerak;</item>
///   <item>raqam ODAMGA tegishli, shartnomaga emas: ishdan ketib qaytgan
///         odamda yangi <c>Employment</c> va o'sha yuz bo'ladi, ustun esa
///         moslikni aynan HR ga kerak bo'lgan lahzada yo'qotardi.</item>
/// </list>
/// <para>
/// <b>Tarix qo'shimcha ustunlar bilan emas, YUMSHOQ O'CHIRISH bilan
/// saqlanadi.</b> Uzish <c>deleted_at</c>/<c>deleted_by</c> qo'yadi, qayta
/// bog'lash YANGI qator qo'shadi. Ya'ni <c>CreatedAt</c>/<c>CreatedBy</c> —
/// bu «qachon va kim bog'ladi», <c>DeletedAt</c>/<c>DeletedBy</c> esa
/// «qachon va kim uzdi». Rad etilgan muqobil: alohida <c>linked_at</c>,
/// <c>linked_by</c>, <c>unlinked_at</c>, <c>unlinked_by</c> ustunlari —
/// interceptor allaqachon to'ldiradigan to'rtta ustunning nusxasi, va
/// kimdir birini to'ldirishni unutgan birinchi kuniyoq ular ajralib
/// ketardi.
/// </para>
/// <para>
/// Unikallik SHAXS RAQAMI bo'yicha (<c>provider</c> + <c>external_person_id</c>),
/// xodim bo'yicha emas: bitta odamda bir nechta raqam bo'lishi mumkin,
/// bitta raqam esa bir vaqtda ikki odamga tegishli bo'la olmaydi.
/// </para>
/// </remarks>
public sealed class AttendanceIdentity : AggregateRoot, IAuditable, ISoftDeletable
{
    /// <summary>Qurilmadagi shaxs raqamining eng katta uzunligi.</summary>
    /// <remarks>
    /// Hikvision'da bu <c>employeeNoString</c> yoki karta raqami — ikkalasi
    /// ham qisqa satr. 64 belgi har qanday ishlab chiqaruvchining raqamini
    /// qamrab oladi va indeksni tor saqlaydi.
    /// </remarks>
    public const int MaxExternalPersonIdLength = 64;

    private AttendanceIdentity(
        Guid id,
        AttendanceIdentityProvider provider,
        string externalPersonId,
        Guid employeeId,
        IdentitySource source)
        : base(id)
    {
        Provider = provider;
        ExternalPersonId = externalPersonId;
        EmployeeId = employeeId;
        Source = source;
    }

    /// <summary>EF Core uchun.</summary>
    private AttendanceIdentity() => ExternalPersonId = string.Empty;

    /// <summary>Raqam qaysi fazoda.</summary>
    public AttendanceIdentityProvider Provider { get; private set; }

    /// <summary>Qurilmadagi shaxs raqami — qurilma yuborgan holida, faqat bo'shliqlari olib tashlangan.</summary>
    public string ExternalPersonId { get; private set; }

    /// <summary>Bog'langan xodim.</summary>
    public Guid EmployeeId { get; private set; }

    /// <summary>Qurilma ro'yxatidagi F.I.Sh. — moslikni ko'z bilan tekshirish uchun.</summary>
    /// <remarks>
    /// Bu bizning ma'lumotimiz EMAS va hech qayerda hisobga olinmaydi:
    /// u faqat «bu raqam haqiqatan shu odammi» degan savolga javob
    /// beradigan manba matni. Xodim F.I.Sh. si o'zgarsa, bu yerdagi
    /// matn eskiligicha qoladi — va aynan shunday bo'lishi kerak.
    /// </remarks>
    public string? DisplayNameInDevice { get; private set; }

    /// <summary>Moslik qayerdan kelgan.</summary>
    public IdentitySource Source { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Qurilma raqamini xodimga bog'laydi.</summary>
    public static Result<AttendanceIdentity> Link(
        AttendanceIdentityProvider provider,
        string? externalPersonId,
        Guid employeeId,
        IdentitySource source = IdentitySource.Manual,
        string? displayNameInDevice = null,
        string? note = null)
    {
        var personId = Trim(externalPersonId);
        if (personId is null)
        {
            return Error.Validation("davomat.shaxs_raqami_bosh", "Qurilmadagi shaxs raqami kiritilishi shart.");
        }

        if (employeeId == Guid.Empty)
        {
            return Error.Validation("davomat.xodim_yoq", "Xodim ko'rsatilishi shart.");
        }

        return new AttendanceIdentity(NewId(), provider, personId, employeeId, source)
        {
            DisplayNameInDevice = Trim(displayNameInDevice),
            Note = Trim(note),
        };
    }

    /// <summary>
    /// Yozuvning izohli qismini tuzatadi.
    /// </summary>
    /// <remarks>
    /// Raqam, xodim va faza bu yerdan O'ZGARMAYDI. Sabab
    /// <c>Incentive.Update</c> dagi bilan bir xil: boshqa xodimga
    /// bog'lash — tuzatish emas, BOSHQA fakt. To'g'ri yo'l: eskisini
    /// uzish (yumshoq o'chirish) va yangisini bog'lash — o'shanda
    /// tarixda ikkala qaror ham ko'rinib turadi.
    /// </remarks>
    public void Describe(string? displayNameInDevice, string? note)
    {
        DisplayNameInDevice = Trim(displayNameInDevice);
        Note = Trim(note);
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
