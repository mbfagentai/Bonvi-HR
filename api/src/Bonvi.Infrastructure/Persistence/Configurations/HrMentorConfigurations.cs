using Bonvi.Domain.Blanks;
using Bonvi.Domain.Recruiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary><c>hr_blanks</c> — HRMentor blankalarining nusxalari (SPEC-blankalar-hrmentor §7).</summary>
public sealed class HrBlankConfiguration : IEntityTypeConfiguration<HrBlank>
{
    public void Configure(EntityTypeBuilder<HrBlank> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("hr_blanks");

        builder.HasKey(blank => blank.Id);

        builder.Ignore(blank => blank.DomainEvents);

        // Bitta hujjat — bitta qator: versiya almashsa qator yangilanadi.
        builder.HasIndex(blank => blank.DocumentId)
            .IsUnique();

        builder.Property(blank => blank.Code)
            .HasMaxLength(HrBlank.MaxCodeLength)
            .IsRequired();

        builder.Property(blank => blank.Title)
            .HasMaxLength(HrBlank.MaxTitleLength)
            .IsRequired();

        builder.Property(blank => blank.FileName)
            .HasMaxLength(HrBlank.MaxTitleLength)
            .IsRequired();

        builder.Property(blank => blank.ContentType)
            .HasMaxLength(HrBlank.MaxContentTypeLength)
            .IsRequired();

        builder.Property(blank => blank.Sha256)
            .HasMaxLength(HrBlank.Sha256Length)
            .IsFixedLength()
            .IsRequired();

        builder.Property(blank => blank.FileKey)
            .HasMaxLength(HrBlank.MaxFileKeyLength)
            .IsRequired();
    }
}

/// <summary><c>form_text_snapshots</c> — HR matnlari reviziyalari (SPEC-blankalar-hrmentor §7).</summary>
public sealed class FormTextSnapshotConfiguration : IEntityTypeConfiguration<FormTextSnapshot>
{
    public void Configure(EntityTypeBuilder<FormTextSnapshot> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("form_text_snapshots");

        builder.HasKey(snapshot => snapshot.Id);

        builder.Ignore(snapshot => snapshot.DomainEvents);

        builder.Property(snapshot => snapshot.FormCode)
            .HasMaxLength(FormTextSnapshot.MaxFormCodeLength)
            .IsRequired();

        builder.Property(snapshot => snapshot.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        // Bir reviziya — bir qator; eng yangisini topish ham shu indeks bo'yicha.
        builder.HasIndex(snapshot => new { snapshot.FormCode, snapshot.Revision })
            .IsUnique();
    }
}

/// <summary><c>form_definitions</c> — HRMentor konstruktoridagi anketa versiyalari (SPEC-anketa-konstruktori §5.1).</summary>
public sealed class FormDefinitionSnapshotConfiguration : IEntityTypeConfiguration<FormDefinitionSnapshot>
{
    public void Configure(EntityTypeBuilder<FormDefinitionSnapshot> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("form_definitions");

        builder.HasKey(snapshot => snapshot.Id);

        builder.Ignore(snapshot => snapshot.DomainEvents);

        builder.Property(snapshot => snapshot.FormCode)
            .HasMaxLength(FormDefinitionSnapshot.MaxFormCodeLength)
            .IsRequired();

        builder.Property(snapshot => snapshot.Label)
            .HasMaxLength(FormDefinitionSnapshot.MaxLabelLength)
            .IsRequired();

        builder.Property(snapshot => snapshot.Definition)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(snapshot => snapshot.Sha256)
            .HasMaxLength(FormDefinitionSnapshot.Sha256Length)
            .IsFixedLength()
            .IsRequired();

        builder.Property(snapshot => snapshot.ReceivedAt)
            .IsRequired();

        // Bir versiya — bir qator. Anketa (code, version) bo'yicha unga bog'lanadi (FK).
        builder.HasAlternateKey(snapshot => new { snapshot.FormCode, snapshot.Version });
    }
}
