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

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(competency => competency.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Bitta xodimda bitta profil — qoida bazada ushlanadi.
        builder.HasIndex(competency => competency.EmployeeId)
            .IsUnique();
    }
}
