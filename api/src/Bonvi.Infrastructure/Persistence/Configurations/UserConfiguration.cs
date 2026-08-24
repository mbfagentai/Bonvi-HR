using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="User"/> uchun jadval konfiguratsiyasi (<c>users</c>).
/// </summary>
/// <remarks>
/// <para>
/// Ustun nomlari qo'lda yozilmaydi: <c>UseSnakeCaseNamingConvention()</c>
/// <c>PasswordHash</c> ni <c>password_hash</c> ga o'zi aylantiradi.
/// Istisno — <see cref="FullName"/> ustunlari (pastda sababi tushuntirilgan).
/// </para>
/// </remarks>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>
    /// <see cref="PhoneNumber"/> uchun taqqoslagich.
    /// </summary>
    /// <remarks>
    /// Nega kerak: konvertordan keyin EF o'zgarishlarni aniqlash uchun qiymatni
    /// nusxalab saqlaydi. Havola bo'yicha taqqoslashda bir xil raqamdan
    /// yasalgan ikki obyekt "har xil" deb hisoblanib, keraksiz UPDATE
    /// yuborilardi. Shu sababli taqqoslash satr qiymati bo'yicha aniq beriladi.
    /// </remarks>
    private static readonly ValueComparer<PhoneNumber> LoginComparer = new(
        (left, right) => left!.Value == right!.Value,
        phone => StringComparer.Ordinal.GetHashCode(phone.Value),
        phone => PhoneNumber.FromTrusted(phone.Value));

    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(user => user.Id);

        // Domen hodisalari xotirada yashaydi — bazaga tegishli emas
        builder.Ignore(user => user.DomainEvents);

        // Login — qiymat obyekti. Bazada oddiy satr, o'qishda esa tekshiruvsiz
        // qayta yig'iladi: bazadagi qiymat yozilayotganda allaqachon tekshirilgan,
        // uni ikkinchi marta tekshirish faqat migratsiyalarni sindiradi.
        builder.Property(user => user.Login)
            .HasConversion(
                login => login.Value,
                value => PhoneNumber.FromTrusted(value),
                LoginComparer)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .IsRequired();

        // FullName — uchta ustunga yoyiladi. Complex type tanlandi (OwnsOne emas):
        // u alohida identifikatorga ega bo'lmagan sof qiymat, EF uni entity
        // sifatida kuzatmaydi va ortiqcha JOIN/tracking yuki bo'lmaydi.
        //
        // Ustun nomlari ataylab qo'lda: standart nom "full_name_last_name"
        // bo'lardi — prefiks bu yerda hech narsa qo'shmaydi, faqat so'rovlarni
        // uzaytiradi.
        builder.ComplexProperty(user => user.FullName, fullName =>
        {
            fullName.Property(name => name.LastName)
                .HasColumnName("last_name")
                .IsRequired();

            fullName.Property(name => name.FirstName)
                .HasColumnName("first_name")
                .IsRequired();

            fullName.Property(name => name.MiddleName)
                .HasColumnName("middle_name");
        });

        builder.Property(user => user.IsActive)
            .IsRequired();

        // Hisob rasmining saqlash kaliti — ixtiyoriy satr (`photo_key`).
        // Uzunlik cheklanmaydi: kalitni saqlash amalga oshirilishi yasaydi
        // va uning shakli (sana/GUID.jpg) kelajakda S3 ga o'tishda o'zgarishi
        // mumkin — chegara qo'yilsa, migratsiya kerak bo'lardi.
        builder.Property(user => user.PhotoKey);

        builder.Property(user => user.FailedLoginAttempts)
            .IsRequired();

        // Rollar — PostgreSQL massivi (uuid[]), join jadval emas.
        //
        // Nega: User'da Role'ga navigatsiya xossasi yo'q va bo'lmasligi ham kerak
        // (Domain EF Core'ni bilmaydi, agregatlar bir-birini identifikator orqali
        // biladi). Navigatsiyasiz ko'p-ko'pga bog'lanishda EF join jadvalini
        // o'qiy olmaydi — `RoleIds` bo'sh qolib, autentifikatsiya jimgina buzilardi.
        // Massivda esa qiymat foydalanuvchi bilan bitta SELECT'da yuklanadi.
        //
        // Ro'yxat `_roleIds` maydonida yashiringan, ochiq xossa faqat o'qish uchun —
        // shu sababli EF yozishda to'g'ridan-to'g'ri maydon bilan ishlaydi.
        //
        // Ataylab maydon nomi bilan emas, XOSSA bilan bog'landi: shundagina
        // `Users.Where(u => u.RoleIds.Contains(id))` SQL'ga tarjima qilinadi
        // (`role_ids @> ARRAY[...]`). Maydonga bog'lansa LINQ tarjimasi buzilib,
        // repozitoriy `EF.Property<...>` yozishga majbur bo'lardi.
        builder.PrimitiveCollection(user => user.RoleIds)
            .HasField("_roleIds")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();

        // GIN — massiv ichidan qidirish uchun ("shu rolga ega foydalanuvchilar").
        // Oddiy B-tree indeks `@>` operatorida ishlamaydi.
        builder.HasIndex(user => user.RoleIds)
            .HasMethod("gin");

        // Login unikal, LEKIN faqat "tirik" yozuvlar orasida: yumshoq o'chirilgan
        // xodimning raqamini yangi foydalanuvchiga berish mumkin bo'lishi kerak.
        builder.HasIndex(user => user.Login)
            .IsUnique()
            .HasFilter("deleted_at IS NULL");

        // "Bu xodimning tizim hisobi bormi?" — profil sahifasida har safar so'raladi
        builder.HasIndex(user => user.EmployeeId);
    }
}
