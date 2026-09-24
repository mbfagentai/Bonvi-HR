using Bonvi.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="EmployeeCompetency"/> uchun jadval konfiguratsiyasi
/// (<c>employee_competencies</c>).
/// </summary>
/// <remarks>
/// Bitta xodimda bitta profil — qoida bazada unikal indeks bilan
/// mahkamlanadi (xuddi <c>Probation</c> — mehnat munosabati juftligidagi
/// kabi).
/// </remarks>
public sealed class EmployeeCompetencyConfiguration : IEntityTypeConfiguration<EmployeeCompetency>
{
    public void Configure(EntityTypeBuilder<EmployeeCompetency> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("employee_competencies");

        builder.HasKey(competency => competency.Id);

        builder.Ignore(competency => competency.DomainEvents);

        builder.Property(competency => competency.Languages)
            .HasMaxLength(500);

        builder.Property(competency => competency.Specialization)
            .HasMaxLength(300);

        builder.Property(competency => competency.OtherAbilities)
            .HasMaxLength(500);

        builder.Property(competency => competency.Interest)
            .HasMaxLength(500);

        builder.Property(competency => competency.EducationPlace)
            .HasMaxLength(300);

        // Anketa javoblari VA HR izohlari (kartadagi izoh, bosqich
        // xulosalari, izohlar lentasi) bitta matnda — sababi
        // `EmployeeCompetency.CandidateNoteMaxLength` izohida.
        builder.Property(competency => competency.CandidateNote)
            .HasMaxLength(EmployeeCompetency.CandidateNoteMaxLength);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(competency => competency.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tillar — profil agregatining ICHIDA (PositionDuty — Position kabi):
        // alohida repozitoriysi yo'q, profilsiz ma'noga ega emas, kaskad o'chadi.
        builder.HasMany(competency => competency.LanguageSkills)
            .WithOne()
            .HasForeignKey(language => language.EmployeeCompetencyId)
            .OnDelete(DeleteBehavior.Cascade);

        // `LanguageSkills` — faqat o'qish uchun xossa (`_languageSkills.AsReadOnly()`).
        // EF to'g'ridan-to'g'ri maydon bilan ishlaydi, aks holda materializatsiyada
        // «xossaga qiymat o'rnatib bo'lmadi» xatosi chiqardi.
        //
        // `AutoInclude` ATAYLAB: profil BITTA ekran uchun va HAR DOIM tillari
        // bilan birga o'qiladi. `Include` esa repozitoriyning har bir so'rovida
        // takrorlanishi kerak bo'lardi va yangi so'rov yozgan odam uni unutsa,
        // saqlashda butun ro'yxat «bo'sh» deb o'qilib O'CHIB ketardi.
        builder.Navigation(competency => competency.LanguageSkills)
            .HasField("_languageSkills")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();

        // Bitta xodimda bitta profil — qoida bazada ushlanadi.
        builder.HasIndex(competency => competency.EmployeeId)
            .IsUnique();
    }
}

/// <summary>
/// <see cref="EmployeeCompetencyLanguage"/> uchun jadval konfiguratsiyasi
/// (<c>employee_competency_languages</c>).
/// </summary>
/// <remarks>
/// Alohida jadval, matn ustuni emas: har bir til alohida qator bo'lsa,
/// «ingliz tilini biladiganlar» oddiy SQL so'rovi bo'ladi. Sanoqlar
/// bazada RAQAM bilan yotadi (boshqa sanoqlar kabi) — nomlar faqat
/// JSON qatlamida yuradi.
/// </remarks>
public sealed class EmployeeCompetencyLanguageConfiguration
    : IEntityTypeConfiguration<EmployeeCompetencyLanguage>
{
    public void Configure(EntityTypeBuilder<EmployeeCompetencyLanguage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("employee_competency_languages");

        builder.HasKey(language => language.Id);

        builder.Ignore(language => language.DomainEvents);

        builder.Property(language => language.Language)
            .IsRequired();

        builder.Property(language => language.Level)
            .IsRequired();

        // «Bitta til — bir marta» qoidasi domenda tekshiriladi
        // (`SetLanguageSkills`), lekin bazada ham qadaladi: ikki parallel
        // saqlash yoki qo'lda yozilgan SQL domen tekshiruvini chetlab
        // o'tishi mumkin, indeks esa hech kimga yo'l qo'ymaydi.
        builder.HasIndex(language => new { language.EmployeeCompetencyId, language.Language })
            .IsUnique();
    }
}
