using Bonvi.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Probation"/> uchun jadval konfiguratsiyasi (<c>probations</c>).
/// </summary>
/// <remarks>
/// <para>
/// Bitta mehnat munosabatida bitta sinov muddati bo'ladi — bu qoida bazada
/// unikal indeks bilan mahkamlanadi, chunki uni faqat kodda ushlab turish
/// ikki so'rov bir vaqtda kelganda ishlamaydi.
/// </para>
/// <para>
/// Yumshoq o'chirish yo'q: sinov muddati mehnat munosabati bilan birga
/// yashaydi va uni alohida «o'chirish» holati mavjud emas — natija
/// <see cref="ProbationStatus"/> orqali yopiladi.
/// </para>
/// </remarks>
public sealed class ProbationConfiguration : IEntityTypeConfiguration<Probation>
{
    public void Configure(EntityTypeBuilder<Probation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("probations");

        builder.HasKey(probation => probation.Id);

        builder.Ignore(probation => probation.DomainEvents);

        builder.Property(probation => probation.StartDate)
            .IsRequired();

        builder.Property(probation => probation.EndDate)
            .IsRequired();

        builder.Property(probation => probation.Status)
            .IsRequired();

        builder.HasOne<Employment>()
            .WithMany()
            .HasForeignKey(probation => probation.EmploymentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Baholovchi — xodim. `Restrict`: baholagan odam ketsa ham, uning
        // xulosasi kim tomonidan berilgani ma'lum bo'lib qolishi kerak.
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(probation => probation.ReviewerEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Bitta munosabatga bitta sinov muddati — qoida bazada ushlanadi
        builder.HasIndex(probation => probation.EmploymentId)
            .IsUnique();

        // ⚠️ Tizimning eng muhim kundalik so'rovi: «muddati tugashiga
        // 7 kun qolganlar». U aynan shu indeksga tushadi —
        // `WHERE status = 'InProgress' AND end_date <= :chegara`.
        // Excel'da bu qo'lda kuzatilardi va tez-tez o'tkazib yuborilardi.
        builder.HasIndex(probation => new { probation.Status, probation.EndDate });
    }
}
