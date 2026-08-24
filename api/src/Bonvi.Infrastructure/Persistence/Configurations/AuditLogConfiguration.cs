using Bonvi.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="AuditLog"/> uchun jadval konfiguratsiyasi (<c>audit_logs</c>).
/// </summary>
/// <remarks>
/// <para>
/// Jurnal o'zgarmas: na <c>IAuditable</c>, na yumshoq o'chirish.
/// "Kim audit yozuvini o'zgartirdi" degan savol bo'lmasligi kerak —
/// yozuv faqat qo'shiladi.
/// </para>
/// <para>
/// Bu jadval eng tez o'sadigan jadval bo'ladi, shu sababli indekslar
/// haqiqiy so'rovlarga qarab tanlangan, "har ehtimolga qarshi" emas.
/// </para>
/// </remarks>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(log => log.Id);

        builder.Ignore(log => log.DomainEvents);

        builder.Property(log => log.OccurredAt)
            .IsRequired();

        // Enum int sifatida saqlanadi: nomi o'zgarsa ham bazadagi tarix buzilmaydi
        builder.Property(log => log.Action)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(log => log.EntityName)
            .IsRequired();

        // "Oxirgi hodisalar" — jurnalning asosiy ko'rinishi, doim teskari tartibda.
        // Indeks ham teskari: shunda PostgreSQL saralashni butunlay o'tkazib yuboradi.
        builder.HasIndex(log => log.OccurredAt)
            .IsDescending();

        // "Shu xodim kartochkasi bilan nima bo'lgan?" — obyekt tarixi
        builder.HasIndex(log => new { log.EntityName, log.EntityId });

        // "Shu foydalanuvchi nima qildi?" — xavfsizlik tekshiruvi
        builder.HasIndex(log => log.UserId);
    }
}
