using Bonvi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Role"/> uchun jadval konfiguratsiyasi (<c>roles</c>).
/// </summary>
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(role => role.Id);

        builder.Ignore(role => role.DomainEvents);

        builder.Property(role => role.Code)
            .IsRequired();

        builder.Property(role => role.Name)
            .IsRequired();

        builder.Property(role => role.IsSystem)
            .IsRequired();

        // Ruxsatlar — PostgreSQL'ning o'z massivi (text[]), alohida jadval emas.
        // Sabab: ruxsat ma'lumot emas, kod konstantasi (Permissions sinfi).
        // Rolga tekshiruv doim to'liq rol bilan birga o'qiladi — JOIN keraksiz.
        //
        // Ro'yxat `_permissions` maydonida yashiringan, ochiq xossa esa faqat
        // o'qish uchun; shu sababli EF yozishda to'g'ridan-to'g'ri maydon bilan ishlaydi.
        //
        // Bog'lanish xossa orqali beriladi (maydon nomi orqali emas) — shundagina
        // `Roles.Where(r => r.Permissions.Contains("salary.view"))` SQL'ga
        // tarjima qilinadi: `permissions @> ARRAY['salary.view']`.
        builder.PrimitiveCollection(role => role.Permissions)
            .HasField("_permissions")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();

        // GIN — "shu ruxsatga ega rollar" so'rovi uchun (permissions @> ARRAY[...])
        builder.HasIndex(role => role.Permissions)
            .HasMethod("gin");

        // Kod — rolning haqiqiy identifikatori (kodda `SystemRoles.Admin` bo'yicha
        // izlanadi), takrorlanishi mumkin emas
        builder.HasIndex(role => role.Code)
            .IsUnique();
    }
}
