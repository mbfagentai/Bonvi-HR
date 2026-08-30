using Bonvi.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="SystemSetting"/> uchun jadval konfiguratsiyasi (<c>system_settings</c>).
/// </summary>
public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("system_settings");

        builder.HasKey(setting => setting.Id);

        builder.Ignore(setting => setting.DomainEvents);

        builder.Property(setting => setting.Key)
            .IsRequired()
            .HasMaxLength(SystemSetting.MaxKeyLength);

        // ⚠️ ENG MUHIM CHEKLOV. Bitta kalitda ATIGI BITTA qator.
        //
        // Ikkinchi qator paydo bo'lsa, «qaysi qiymat haqiqiy» degan savol
        // so'rov tartibiga qolardi: bugun bittasi o'qilardi, ertaga
        // ikkinchisi — va yozish esa uchinchi qator qo'shib qo'yardi.
        // Bunday xato hech qanday belgisiz kechadi: sozlama «saqlandi»
        // deb ko'rinadi, lekin tizim eski qiymat bilan ishlaydi.
        //
        // Bu indeks bir vaqtda IZLASH indeksi ham: barcha so'rovlar
        // (`FindAsync`) aynan kalit bo'yicha ketadi.
        builder.HasIndex(setting => setting.Key)
            .IsUnique();

        // Qiymat BO'SH bo'lishi mumkin (matnli sozlamani o'chirish —
        // bo'sh satr), lekin `null` emas: «yozilmagan sozlama» ni bo'sh
        // qator bilan emas, qatorning UMUMAN yo'qligi bilan ifodalaymiz
        // (`SystemSetting` izohi). Ikkala holat ham `null` bo'lsa,
        // «o'chirilgan matn» bilan «hech qachon tegilmagan» ni ajratib
        // bo'lmasdi.
        builder.Property(setting => setting.Value)
            .IsRequired()
            .HasMaxLength(SystemSettingKeys.MaxTextLength);
    }
}
