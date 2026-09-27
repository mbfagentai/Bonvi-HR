using Bonvi.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>Tekshiruv materiallari — <c>employee_check_files</c>.</summary>
/// <remarks>
/// <c>EmployeeDocumentConfiguration</c> bilan bir xil shakl, faqat turisiz
/// (sababi <see cref="EmployeeCheckFile"/> izohida).
/// </remarks>
public sealed class EmployeeCheckFileConfiguration : IEntityTypeConfiguration<EmployeeCheckFile>
{
    public void Configure(EntityTypeBuilder<EmployeeCheckFile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("employee_check_files");

        builder.HasKey(file => file.Id);

        builder.Ignore(file => file.DomainEvents);

        builder.Property(file => file.FileKey)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(file => file.FileName)
            .HasMaxLength(EmployeeCheckFile.MaxFileNameLength)
            .IsRequired();

        builder.Property(file => file.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(file => file.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Karta shu bo'yicha so'raydi — faqat tirik yozuvlar.
        builder.HasIndex(file => file.EmployeeId)
            .HasFilter("deleted_at IS NULL");
    }
}
