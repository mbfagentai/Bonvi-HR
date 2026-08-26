using Bonvi.Domain.Catalog;
using Bonvi.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="EmployeeDocument"/> uchun jadval konfiguratsiyasi (<c>employee_documents</c>).
/// </summary>
public sealed class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("employee_documents");

        builder.HasKey(document => document.Id);

        builder.Ignore(document => document.DomainEvents);

        builder.Property(document => document.DocumentTypeId)
            .IsRequired();

        builder.Property(document => document.FileKey)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(document => document.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(document => document.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(document => document.SizeBytes)
            .IsRequired();

        builder.Property(document => document.Note)
            .HasMaxLength(1000);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(document => document.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ma'lumotnomaga — `Restrict`: `EmploymentConfiguration` dagi bilan
        // bir xil qoida, tur o'chirilganda hujjat tarixi yo'qolib ketmasin
        // (amalda ma'lumotnomalar yumshoq o'chiriladi, bu faqat qattiq
        // o'chirishdan himoya).
        builder.HasOne<DocumentType>()
            .WithMany()
            .HasForeignKey(document => document.DocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // «Xodim kartasi — Hujjatlar» tabi shu bo'yicha so'raladi (butun
        // ro'yxat yoki tur bo'yicha filtrlangan — ikkalasi ham shu
        // indeksning chap qismidan foydalanadi).
        builder.HasIndex(document => new { document.EmployeeId, document.DocumentTypeId });
    }
}
