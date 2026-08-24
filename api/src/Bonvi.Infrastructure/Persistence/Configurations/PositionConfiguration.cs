using Bonvi.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Position"/> uchun jadval konfiguratsiyasi (<c>positions</c>).
/// </summary>
public sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureReference("positions");

        builder.Property(position => position.Grade)
            .IsRequired();

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(position => position.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Vazifalar — lavozim agregatining ICHIDA. Ular alohida repozitoriyga
        // ega emas va lavozimsiz ma'noga ega emas, shu sababli kaskad o'chirish.
        //
        // Yangi vazifa qo'shilganda EF uni to'g'ri INSERT qilishini
        // `AppDbContext` dagi `ChangeTracker.Tracked` ishlovchisi ta'minlaydi —
        // izohi o'sha yerda.
        builder.HasMany(position => position.Duties)
            .WithOne()
            .HasForeignKey(duty => duty.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        // `Duties` — faqat o'qish uchun xossa (`_duties.AsReadOnly()`), unga
        // yozib bo'lmaydi. Shu sababli EF to'g'ridan-to'g'ri maydon bilan ishlaydi:
        // aks holda materializatsiyada «xossaga qiymat o'rnatib bo'lmadi» xatosi chiqardi.
        builder.Navigation(position => position.Duties)
            .HasField("_duties")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // «Shu bo'limning lavozimlari» — shtat jadvali ekranining asosiy so'rovi
        builder.HasIndex(position => position.DepartmentId);
    }
}

/// <summary>
/// <see cref="PositionDuty"/> uchun jadval konfiguratsiyasi (<c>position_duties</c>).
/// </summary>
/// <remarks>
/// Alohida jadval, chunki vazifalar soni oldindan noma'lum va ular
/// tartiblanadi. Massiv ustuni bo'lsa, bitta vazifani ko'chirish uchun
/// butun massivni qayta yozish kerak bo'lardi.
/// Yumshoq o'chirish yo'q: vazifa lavozim bilan birga yashaydi.
/// </remarks>
public sealed class PositionDutyConfiguration : IEntityTypeConfiguration<PositionDuty>
{
    public void Configure(EntityTypeBuilder<PositionDuty> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("position_duties");

        builder.HasKey(duty => duty.Id);

        builder.Ignore(duty => duty.DomainEvents);

        builder.Property(duty => duty.Text)
            .IsRequired();

        builder.Property(duty => duty.SortOrder)
            .IsRequired();

        // Vazifalar DOIM lavozim bo'yicha va tartibda o'qiladi — indeks aynan shunday.
        // Natijada EF'ning `ORDER BY sort_order` so'rovi saralashsiz bajariladi.
        builder.HasIndex(duty => new { duty.PositionId, duty.SortOrder });
    }
}
