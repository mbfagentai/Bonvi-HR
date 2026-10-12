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
/// yozuv faqat qo'shiladi. Bazada buni <c>audit_logs_append_only</c>
/// triggeri qo'riqlaydi (<c>AuditJurnaliKengaytirildi</c> migratsiyasi):
/// <c>UPDATE</c>, <c>DELETE</c> va <c>TRUNCATE</c> superuser uchun ham rad etiladi.
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

        /*
          ⭐ Kengaytirilgan ustunlarning HAMMASI bo'sh bo'la oladi yoki sukut
          qiymatli (SPEC-rollar-va-audit §5.7): oldingi reliz ularni bilmaydi va
          kod qaytarilganda yozuv qo'shishda davom etishi kerak. `actor_kind`
          sukuti 1 (User) — kengaytirishdan oldingi barcha qatorlar shunday.

          Qo'riqchi qiymat (sentinel) — CLR sukuti 0, sanoqda yo'q: kod har doim
          haqiqiy turni beradi, ya'ni baza sukuti faqat eski reliz yozganda
          ishlaydi. Oshkora berilmasa EF har ishga tushishda ogohlantirish yozadi.
        */
        builder.Property(log => log.ActorKind)
            .HasConversion<int>()
            .HasDefaultValue(AuditActorKind.User)
            .HasSentinel(default(AuditActorKind))
            .IsRequired();

        // Ro'yxat `_changedFields` maydonida, ochiq xossa faqat o'qish uchun —
        // `User.RoleIds` bilan bir xil naqsh (text[]).
        builder.PrimitiveCollection(log => log.ChangedFields)
            .HasField("_changedFields")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired(false);

        // "Oxirgi hodisalar" — jurnalning asosiy ko'rinishi, doim teskari tartibda.
        // Indeks ham teskari: shunda PostgreSQL saralashni butunlay o'tkazib yuboradi.
        builder.HasIndex(log => log.OccurredAt)
            .IsDescending();

        // "Shu xodim kartochkasi bilan nima bo'lgan?" — obyekt tarixi
        builder.HasIndex(log => new { log.EntityName, log.EntityId });

        // "Shu foydalanuvchi nima qildi?" — xavfsizlik tekshiruvi
        builder.HasIndex(log => log.UserId);

        // Xodim kartasidagi «O'zgarishlar tarixi» — eng yangisi birinchi.
        // QISMAN: xodimga tegmaydigan qatorlar (rol, sozlama, kirish) indeksga
        // tushmaydi va uni bekorga shishirmaydi.
        builder.HasIndex(log => new { log.SubjectEmployeeId, log.OccurredAt })
            .HasDatabaseName("ix_audit_logs_subject_employee_id_occurred_at")
            .IsDescending(false, true)
            .HasFilter("subject_employee_id IS NOT NULL");

        // «Shu harakatda yana nima o'zgardi» — bitta so'rovning qatorlari.
        builder.HasIndex(log => log.CorrelationId)
            .HasDatabaseName("ix_audit_logs_correlation_id")
            .HasFilter("correlation_id IS NOT NULL");
    }
}
