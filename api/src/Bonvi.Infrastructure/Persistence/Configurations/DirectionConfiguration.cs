using Bonvi.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Direction"/> uchun jadval konfiguratsiyasi (<c>directions</c>).
/// </summary>
/// <remarks>
/// Yo'nalishlar soni ~8 ta — qo'shimcha indeks keraksiz.
/// Rang faqat interfeys uchun, u bo'yicha hech qachon qidirilmaydi.
/// </remarks>
public sealed class DirectionConfiguration : IEntityTypeConfiguration<Direction>
{
    public void Configure(EntityTypeBuilder<Direction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureReference("directions");
    }
}
