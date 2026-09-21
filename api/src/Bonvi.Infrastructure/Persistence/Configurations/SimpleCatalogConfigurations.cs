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
/// Qo'shimcha ustun yo'q: yo'qlik turida nomdan boshqa hech narsa yo'q
/// (SPEC-so'rovlar §B1 — «ishlangan vaqtga kiradimi» bayrog'i ataylab
/// qo'shilmagan).
/// </remarks>
public sealed class AbsenceTypeConfiguration : IEntityTypeConfiguration<AbsenceType>
{
    public void Configure(EntityTypeBuilder<AbsenceType> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureReference("absence_types");
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
