using Bonvi.Domain.Identity;
using Bonvi.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="NotificationState"/> uchun jadval konfiguratsiyasi
/// (<c>notification_states</c>).
/// </summary>
public sealed class NotificationStateConfiguration : IEntityTypeConfiguration<NotificationState>
{
    public void Configure(EntityTypeBuilder<NotificationState> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("notification_states");

        builder.HasKey(state => state.Id);

        builder.Ignore(state => state.DomainEvents);

        builder.Property(state => state.UserId)
            .IsRequired();

        builder.Property(state => state.Key)
            .IsRequired()
            .HasMaxLength(NotificationKey.MaxLength);

        // ⚠️ ENG MUHIM CHEKLOV. Bitta foydalanuvchida bitta kalit uchun
        // ATIGI BITTA yozuv bo'lishi kerak. Ikkinchi qator paydo bo'lsa,
        // «o'qildi» va «o'chirildi» ikki xil qatorga tushib qolishi
        // mumkin — natijada o'chirilgan bildirishnoma qaytib kelardi va
        // buni hech qanday xato ko'rsatmasdi.
        //
        // Bu indeks bir vaqtda IZLASH indeksi ham: barcha so'rovlar
        // «shu foydalanuvchining shu kalitlari» ko'rinishida bo'ladi.
        builder.HasIndex(state => new { state.UserId, state.Key })
            .IsUnique();

        // Foydalanuvchi o'chirilsa, uning shaxsiy bildirishnoma holatlari
        // ham ma'nosini yo'qotadi — `Cascade`. Bu qoida boshqa
        // jadvallardan (`RefreshToken`) farq qilmaydi: hisobga bog'langan,
        // tarixiy qiymati yo'q ma'lumot hisob bilan birga ketadi.
        //
        // Amalda hisoblar o'chirilmaydi, faolsizlantiriladi — lekin
        // cheklov niyatni aniq ko'rsatadi va «egasiz» qatorlar
        // paydo bo'lishiga yo'l qo'ymaydi.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(state => state.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
