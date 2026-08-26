using Bonvi.Domain.Employees;
using Bonvi.Domain.Feedback;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="ExitInterview"/> uchun jadval konfiguratsiyasi (<c>exit_interviews</c>).
/// </summary>
/// <remarks>
/// Bitta mehnat munosabatiga bitta suhbat — <c>ProbationConfiguration</c>
/// dagi bilan bir xil qoida, bir xil sabab: bu yerda ham ushlab turish
/// faqat kodda yetarli emas, unikal indeks kerak.
/// </remarks>
public sealed class ExitInterviewConfiguration : IEntityTypeConfiguration<ExitInterview>
{
    public void Configure(EntityTypeBuilder<ExitInterview> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("exit_interviews");

        builder.HasKey(interview => interview.Id);

        builder.Ignore(interview => interview.DomainEvents);

        builder.Property(interview => interview.InterviewDate)
            .IsRequired();

        builder.Property(interview => interview.Reason)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(interview => interview.WouldRecommend);

        builder.Property(interview => interview.Impressions)
            .HasMaxLength(2000);

        builder.Property(interview => interview.ConditionsToReturn)
            .HasMaxLength(2000);

        builder.HasOne<Employment>()
            .WithMany()
            .HasForeignKey(interview => interview.EmploymentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Bitta munosabatga bitta suhbat — qoida bazada ushlanadi.
        // Filtr MAJBURIY: `ExitInterview` yumshoq o'chiriladi
        // (`ISoftDeletable`), filtrsiz unikal indeks o'chirilgan yozuv
        // turgan joyda yangisini yozishga yo'l qo'ymay qolardi —
        // `EmployeeConfiguration`dagi PINFL indeksi bilan bir xil qoida.
        builder.HasIndex(interview => interview.EmploymentId)
            .IsUnique()
            .HasFilter("deleted_at IS NULL");
    }
}
