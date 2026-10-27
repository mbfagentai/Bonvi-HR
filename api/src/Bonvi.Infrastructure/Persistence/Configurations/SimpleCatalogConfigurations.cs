using Bonvi.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="TerminationReason"/> uchun jadval konfiguratsiyasi (<c>termination_reasons</c>).
/// </summary>
/// <remarks>
/// Kadrlar oqimi hisoboti «ixtiyoriy / majburiy» kesimida quriladi, shu sababli
/// <c>is_voluntary</c> bo'yicha alohida indeks bor. Boshqa ma'lumotnomalarda
/// bunday indeks yo'q — u yerda shunday so'rov yo'q.
/// </remarks>
public sealed class TerminationReasonConfiguration : IEntityTypeConfiguration<TerminationReason>
{
    public void Configure(EntityTypeBuilder<TerminationReason> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureReference("termination_reasons");

        builder.Property(reason => reason.IsVoluntary)
            .IsRequired();

        builder.Property(reason => reason.IsRehireable)
            .IsRequired();

        builder.HasIndex(reason => reason.IsVoluntary);
    }
}

/// <summary>
/// <see cref="AbsenceType"/> uchun jadval konfiguratsiyasi (<c>absence_types</c>).
/// </summary>
/// <remarks>
/// <c>counts_as_work_time</c> — 2026-10-17 da qo'shildi (SPEC-so'rovlar §B3
/// AMENDED); sukut <c>false</c>, ya'ni mavjud turlar avvalgidek ishlaydi.
/// </remarks>
public sealed class AbsenceTypeConfiguration : IEntityTypeConfiguration<AbsenceType>
{
    public void Configure(EntityTypeBuilder<AbsenceType> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureReference("absence_types");

        // Sukut faqat migratsiyada (bazada) — eski image rollback'da ustunni
        // bilmay qator qo'sha olsin. Modelda `HasDefaultValue` yo'q: loyiha
        // qoidasi, sababi `AttendanceConfigurations` boshidagi izohda.
        builder.Property(type => type.CountsAsWorkTime)
            .IsRequired();

        // Bazadagi sukut `true` faqat migratsiyada (`JadvalSorovi`) — xuddi shu sabab.
        builder.Property(type => type.EmployeeCanRequest)
            .IsRequired();
    }
}

/// <summary>
/// <see cref="ReferralChannel"/> uchun jadval konfiguratsiyasi (<c>referral_channels</c>).
/// </summary>
/// <remarks>
/// Sakkizta boshlang'ich kanal migratsiyada QAT'IY identifikatorlar bilan yoziladi
/// (<c>QayerdanBilgan</c>): bot anketalaridagi eski javoblar aynan shu identifikatorlarga
/// ko'chiriladi, ya'ni ko'chirish har bazada bir xil natija beradi. Shu sababli bu
/// ma'lumotnoma urug' faylidan (<c>CatalogSeeder</c>) to'ldirilmaydi.
/// </remarks>
public sealed class ReferralChannelConfiguration : IEntityTypeConfiguration<ReferralChannel>
{
    public void Configure(EntityTypeBuilder<ReferralChannel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureReference("referral_channels");

        // Bazadagi sukut `false` faqat migratsiyada — `AbsenceTypeConfiguration` dagi sabab.
        builder.Property(channel => channel.AsksDetail)
            .IsRequired();
    }
}

/// <summary>
/// <see cref="AwardType"/> uchun jadval konfiguratsiyasi (<c>award_types</c>).
/// </summary>
public sealed class AwardTypeConfiguration : IEntityTypeConfiguration<AwardType>
{
    public void Configure(EntityTypeBuilder<AwardType> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureReference("award_types");

        builder.Property(award => award.HasAmount)
            .IsRequired();
    }
}

/// <summary>
/// <see cref="PenaltyType"/> uchun jadval konfiguratsiyasi (<c>penalty_types</c>).
/// </summary>
public sealed class PenaltyTypeConfiguration : IEntityTypeConfiguration<PenaltyType>
{
    public void Configure(EntityTypeBuilder<PenaltyType> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureReference("penalty_types");

        builder.Property(penalty => penalty.HasAmount)
            .IsRequired();

        builder.Property(penalty => penalty.Severity)
            .IsRequired();
    }
}

/// <summary>
/// <see cref="DocumentType"/> uchun jadval konfiguratsiyasi (<c>document_types</c>).
/// </summary>
/// <remarks>
/// «Yetishmayotgan hujjatlar» ogohlantirishi majburiy turlar ro'yxatidan
/// boshlanadi, shu sababli <c>is_required</c> bo'yicha qisman indeks.
/// </remarks>
public sealed class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureReference("document_types");

        builder.Property(document => document.IsRequired)
            .IsRequired();

        builder.HasIndex(document => document.IsRequired)
            .HasFilter("is_required");
    }
}
