using Bonvi.Domain.Common;

namespace Bonvi.Domain.Notifications;

/// <summary>
/// Bitta foydalanuvchining bitta bildirishnomaga munosabati: o'qidimi, o'chirdimi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bazada saqlanadigan YAGONA narsa shu.</b> Bildirishnomaning o'zi
/// (sarlavha, sana, havola) hech qayerda yozilmaydi — u so'rov kelganda
/// joriy ma'lumotdan hisoblanadi. Bu yerda esa faqat foydalanuvchining
/// QARORI turadi va u hisoblashdan omon qoladi.
/// </para>
/// <para>
/// <b>Nega holat HAR BIR FOYDALANUVCHIDA ALOHIDA</b> (foydalanuvchining
/// aniq qarori). Bildirishnomaning O'ZI umumiy: «Karimovning sinovi
/// ertaga tugaydi» — bu fakt, u hamma uchun bir xil. Lekin uni bitta HR
/// menejeri o'qigani ikkinchisi uchun hech narsani anglatmaydi: ikkinchisi
/// hamon xabardor bo'lishi kerak. Shu sababli kalit
/// <c>(UserId, Key)</c> juftligi bo'ladi, faqat <c>Key</c> emas.
/// </para>
/// <para>
/// <b>Nega «o'chirish» maydoni <c>DeletedAt</c> emas, <see cref="DismissedAt"/>.</b>
/// Bu loyihada <c>deleted_at</c> ustuni <c>ISoftDeletable</c> bilan
/// bog'langan aniq ma'noga ega: «yozuvning O'ZI yo'q» va u global
/// so'rov filtri bilan hamma joydan yashiriladi
/// (<c>AppDbContext.ApplySoftDeleteFilters</c>). Bu yerda ma'no
/// TESKARI — yozuv aynan bildirishnomani YASHIRISH uchun bor va u
/// so'rovlarda KO'RINISHI shart. Ikkalasi bir xil nom ostida tursa,
/// kimdir bu sinfga <c>ISoftDeletable</c> ni qo'shib qo'yishi kifoya:
/// o'chirilgan bildirishnomalar jimgina qaytib kelardi va buni hech
/// qanday xato ko'rsatmasdi.
/// </para>
/// <para>
/// <b>Nega yozuvlar tozalanmaydi.</b> Vaqt o'tib bu jadval o'sadi (har
/// «hammasini o'qildi» bosilganda o'nlab qator). Yozuv juda kichik
/// (identifikator, kalit, ikki sana) va tizimda atigi bir necha
/// foydalanuvchi bor — yillar davomida ham u o'nlab minglab qatordan
/// oshmaydi. So'rovlar esa har doim <c>(user_id, key)</c> indeksi bo'yicha
/// va FAQAT shu safar hisoblangan kalitlar bo'yicha ketadi, ya'ni
/// jadval hajmi so'rov narxiga ta'sir qilmaydi. Avtomatik tozalash
/// qo'shilsa, u «qaysi kalit hali ham kerak» degan savolga javob berishi
/// kerak bo'lardi — bu esa aynan fon vazifasini qaytarib olib kelardi.
/// </para>
/// </remarks>
public sealed class NotificationState : AggregateRoot, IAuditable
{
    private NotificationState(Guid id, Guid userId, string key)
        : base(id)
    {
        UserId = userId;
        Key = key;
    }

    /// <summary>EF Core uchun.</summary>
    private NotificationState() => Key = string.Empty;

    /// <summary>Kimning qarori.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Qaysi bildirishnoma — <see cref="NotificationKey"/> hosil qilgan barqaror kalit.</summary>
    public string Key { get; private set; }

    /// <summary>Qachon o'qildi. <c>null</c> — hali o'qilmagan.</summary>
    public DateTimeOffset? ReadAt { get; private set; }

    /// <summary>Qachon o'chirildi (ro'yxatdan olib tashlandi). <c>null</c> — o'chirilmagan.</summary>
    public DateTimeOffset? DismissedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>O'qilganmi.</summary>
    public bool IsRead => ReadAt is not null;

    /// <summary>O'chirilganmi — o'chirilgan bildirishnoma boshqa ko'rinmaydi.</summary>
    public bool IsDismissed => DismissedAt is not null;

    /// <summary>Yangi holat yozuvi ochadi.</summary>
    /// <remarks>
    /// Kalit shu yerda tekshiriladi: bu — mijozdan kelgan satr bazaga
    /// tushishidan oldingi OXIRGI to'siq. Noma'lum prefiksli kalit
    /// hech qachon hisoblanmaydi, ya'ni uning yozuvi ham hech qachon
    /// ishlatilmasdi — jadvalda faqat axlat bo'lib qolardi.
    /// </remarks>
    public static Result<NotificationState> For(Guid userId, string? key)
    {
        if (userId == Guid.Empty)
        {
            return Error.Validation(
                "bildirishnoma.foydalanuvchi_yoq",
                "Bildirishnoma holati foydalanuvchisiz saqlanmaydi.");
        }

        var trimmed = key?.Trim();
        if (!NotificationKey.IsValid(trimmed))
        {
            return Error.Validation(
                "bildirishnoma.kalit_notogri",
                "Bildirishnoma kaliti noto'g'ri yoki noma'lum turga tegishli.");
        }

        return new NotificationState(NewId(), userId, trimmed!);
    }

    /// <summary>
    /// «O'qildi» belgisini qo'yadi.
    /// </summary>
    /// <remarks>
    /// Idempotent va BIRINCHI o'qish vaqtini saqlaydi: takroriy chaqiruv
    /// sanani surib yubormaydi. Sabab — «hammasini o'qildi» tugmasi
    /// allaqachon o'qilganlarni ham qamrab oladi va har bosilganda barcha
    /// sanalar bugungiga aylanib, «bu xabar qachon kelgan edi» degan
    /// savolga javob yo'qolardi.
    /// </remarks>
    public void MarkRead(DateTimeOffset at) => ReadAt ??= at;

    /// <summary>
    /// Bildirishnomani ro'yxatdan olib tashlaydi.
    /// </summary>
    /// <remarks>
    /// <b>Sharti hamon rost bo'lsa ham qaytmaydi</b> — foydalanuvchining
    /// aniq talabi. Ya'ni «bu odamning sinovi o'tib ketgan, bilaman,
    /// keyinroq hal qilaman» degan qaror hurmat qilinadi va tizim ertasiga
    /// uni yana ko'rsatib bezovta qilmaydi.
    /// </remarks>
    public void Dismiss(DateTimeOffset at) => DismissedAt ??= at;
}
