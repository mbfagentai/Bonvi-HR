using Bonvi.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="EmployeeFlag"/> uchun jadval konfiguratsiyasi
/// (<c>employee_flags</c>).
/// </summary>
/// <remarks>
/// Excel'dagi to'rt xil varaq (rezerv, qora ro'yxat, nazorat, «vazifa
/// bersa bo'ladigan») bitta jadvalga yig'ilgan — mohiyati bir xil.
/// Turlar orasidagi farq faqat <see cref="EmployeeFlagType"/> da, shu
/// sababli yangi yorliq turi jadval o'zgartirmasdan qo'shiladi.
/// </remarks>
public sealed class EmployeeFlagConfiguration : IEntityTypeConfiguration<EmployeeFlag>
{
    public void Configure(EntityTypeBuilder<EmployeeFlag> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("employee_flags");

        builder.HasKey(flag => flag.Id);

        builder.Ignore(flag => flag.DomainEvents);

        builder.Property(flag => flag.Type)
            .IsRequired();

        builder.Property(flag => flag.StartDate)
            .IsRequired();

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(flag => flag.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // ⚠️ HR aniq so'ragan qoida: ishdan bo'shatish va qayta ishga olishda
        // tizim qora ro'yxat va nazoratni tekshirib ogohlantirishi kerak.
        // Bu tekshiruv «shu xodimda shu turdagi belgi bormi» so'rovi —
        // aynan shu indeksga tushadi.
        builder.HasIndex(flag => new { flag.EmployeeId, flag.Type });

        // «Qora ro'yxatdagilar (bugungi kunga)» — alohida ekran.
        // Sanalar indeksga kiritilgan, shunda faollik tekshiruvi
        // (`start_date <= :kun AND (end_date IS NULL OR end_date >= :kun)`)
        // jadvalga tushmasdan indeksdan hal bo'ladi.
        builder.HasIndex(flag => new { flag.Type, flag.StartDate, flag.EndDate });
    }
}
