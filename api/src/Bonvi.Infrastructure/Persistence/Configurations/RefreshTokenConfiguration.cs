using Bonvi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="RefreshToken"/> uchun jadval konfiguratsiyasi (<c>refresh_tokens</c>).
/// </summary>
/// <remarks>
/// Bu yozuv <c>IAuditable</c> emas: tokenni "kim o'zgartirdi" degan savol yo'q,
/// uning butun tarixi <c>created_at</c> / <c>revoked_at</c> ustunlarida.
/// </remarks>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(token => token.Id);

        builder.Ignore(token => token.DomainEvents);

        builder.Property(token => token.TokenHash)
            .IsRequired();

        builder.Property(token => token.ExpiresAt)
            .IsRequired();

        builder.Property(token => token.CreatedAt)
            .IsRequired();

        // Navigatsiyasiz bog'lanish: token agregat sifatida foydalanuvchini
        // obyekt bo'lib ushlab turmaydi, faqat uning identifikatorini biladi.
        // Foydalanuvchi butunlay o'chirilsa (yumshoq emas, haqiqiy DELETE),
        // uning tokenlari ham ketishi kerak — osilib qolgan token xavfsizlik teshigi.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Kiruvchi tokenni topish — har bir yangilash so'rovidagi birinchi amal.
        // Unikal: bir xil xesh ikki marta berilishi mumkin emas.
        builder.HasIndex(token => token.TokenHash)
            .IsUnique();

        // "Shu foydalanuvchining amaldagi tokenlari" va muddati o'tganlarini
        // tozalash — ikkalasi ham shu kompozit indeksdan foydalanadi.
        builder.HasIndex(token => new { token.UserId, token.ExpiresAt });
    }
}
