using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Incident"/> uchun jadval konfiguratsiyasi (<c>incidents</c>).
/// </summary>
public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("incidents");

        builder.HasKey(incident => incident.Id);

        builder.Ignore(incident => incident.DomainEvents);

        builder.Property(incident => incident.IncidentDate)
            .IsRequired();

        builder.Property(incident => incident.Shift)
            .HasMaxLength(200);

        builder.Property(incident => incident.Location)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(incident => incident.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(incident => incident.Cause)
            .HasMaxLength(2000);

        builder.Property(incident => incident.CorrectiveAction)
            .HasMaxLength(2000);

        builder.Property(incident => incident.Severity)
            .IsRequired();

        builder.Property(incident => incident.MedicalAidProvided)
            .IsRequired();

        builder.Property(incident => incident.MedicalAidNote)
            .HasMaxLength(1000);

        builder.Property(incident => incident.CommissionConclusion)
            .HasMaxLength(2000);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(incident => incident.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Voqea vaqtidagi lavozim — SNAPSHOT (entity izohiga qarang).
        // `Restrict`: lavozim ma'lumotnomadan o'chirilmaydi, faqat
        // deaktivatsiya qilinadi (`ReferenceEntity`), shu sababli bu yerda
        // amalda ishlamaydi, lekin niyatni aniq ko'rsatadi.
        builder.HasOne<Position>()
            .WithMany()
            .HasForeignKey(incident => incident.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Mas'ul xodim — «imzo» o'rnini bosadi. `Restrict`: mas'ul xodim
        // keyinroq ishdan ketsa ham, kim javobgar bo'lgani ma'lum qolishi
        // kerak (xuddi `ProbationConfiguration`dagi baholovchi kabi).
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(incident => incident.ResponsibleEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Jurnal ro'yxati sana bo'yicha kamayish tartibida so'raladi.
        builder.HasIndex(incident => incident.IncidentDate);

        // «Xodim kartasi — baxtsiz hodisalar» tabi shu bo'yicha so'raladi.
        builder.HasIndex(incident => incident.EmployeeId);
    }
}
