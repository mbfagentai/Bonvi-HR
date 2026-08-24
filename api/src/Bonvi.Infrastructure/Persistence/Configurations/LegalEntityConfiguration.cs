using Bonvi.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="LegalEntity"/> uchun jadval konfiguratsiyasi (<c>legal_entities</c>).
/// </summary>
public sealed class LegalEntityConfiguration : IEntityTypeConfiguration<LegalEntity>
{
    public void Configure(EntityTypeBuilder<LegalEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureReference("legal_entities");

        // STIR — 9 raqam, domenda tekshiriladi. Bu yerda faqat qidirish uchun indeks:
        // shartnoma va hisobotlarda firma aynan STIR bo'yicha izlanadi.
        // Unikal EMAS: bir xil STIR bilan ikkita yozuv xato, lekin uni migratsiya
        // paytida tuzatib bo'lmaydi — eski Excel'da STIR umuman yo'q edi.
        builder.HasIndex(entity => entity.TaxId)
            .HasFilter("tax_id IS NOT NULL");
    }
}
