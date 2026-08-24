using System.Linq.Expressions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Employment"/> uchun jadval konfiguratsiyasi (<c>employments</c>).
/// </summary>
/// <remarks>
/// <para>
/// Bo'lim, lavozim va oklad aynan SHU jadvalda: bir xodim bir firmada
/// omborchi, boshqasida haydovchi bo'lishi mumkin, shu sababli ular
/// xodimga emas, munosabatga tegishli.
/// </para>
/// <para>
/// Tashqi kalitlar hamma joyda <c>Restrict</c>: ma'lumotnomadagi yozuv
/// o'chirilganda uning ortidan mehnat tarixi yo'q bo'lib ketishi mumkin
/// emas. Ma'lumotnomalar yumshoq o'chiriladi, ya'ni amalda bu cheklov
/// faqat noto'g'ri qattiq o'chirishdan himoya qiladi.
/// </para>
/// </remarks>
public sealed class EmploymentConfiguration : IEntityTypeConfiguration<Employment>
{
    public void Configure(EntityTypeBuilder<Employment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("employments");

        builder.HasKey(employment => employment.Id);

        builder.Ignore(employment => employment.DomainEvents);

        // Stavka: 1.0 — to'liq, 0.5 — o'rindoshlik. Aniqlik ataylab beriladi,
        // aks holda PostgreSQL cheksiz aniqlikdagi `numeric` yaratadi va
        // 0.3333… kabi qiymat jimgina saqlanib qolishi mumkin edi.
        builder.Property(employment => employment.Rate)
            .HasPrecision(4, 2)
            .IsRequired();

        builder.Property(employment => employment.Status)
            .IsRequired();

        builder.Property(employment => employment.IsPrimary)
            .IsRequired();

        builder.Property(employment => employment.NeedsReview)
            .IsRequired();

        builder.MapMoney(employment => employment.Salary, "salary_amount", "salary_currency");

        // ────────────────────────────────────────────────────────────
        //  Hodisalar — munosabat agregatining ICHIDA
        // ────────────────────────────────────────────────────────────

        // Hodisalar alohida repozitoriyga ega emas va munosabatsiz ma'noga
        // ega emas, shu sababli kaskad o'chirish.
        //
        // MAVJUD munosabatga yangi hodisa qo'shilganda EF uni to'g'ri INSERT
        // qilishini `AppDbContext` dagi `ChangeTracker.Tracked` ishlovchisi
        // ta'minlaydi — izohi o'sha yerda.
        builder.HasMany(employment => employment.Events)
            .WithOne()
            .HasForeignKey(employmentEvent => employmentEvent.EmploymentId)
            .OnDelete(DeleteBehavior.Cascade);

        // `Events` — faqat o'qish uchun xossa (`_events.AsReadOnly()`), unga
        // yozib bo'lmaydi. Shu sababli EF to'g'ridan-to'g'ri maydon bilan
        // ishlaydi: aks holda materializatsiyada «xossaga qiymat o'rnatib
        // bo'lmadi» xatosi chiqardi.
        builder.Navigation(employment => employment.Events)
            .HasField("_events")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ────────────────────────────────────────────────────────────
        //  Bog'lanishlar
        // ────────────────────────────────────────────────────────────

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(employment => employment.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LegalEntity>()
            .WithMany()
            .HasForeignKey(employment => employment.LegalEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(employment => employment.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Position>()
            .WithMany()
            .HasForeignKey(employment => employment.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TerminationReason>()
            .WithMany()
            .HasForeignKey(employment => employment.TerminationReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        // Mas'ul xodim (mentor) — o'sha `employees` jadvaliga ikkinchi havola
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(employment => employment.ResponsibleEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // ────────────────────────────────────────────────────────────
        //  Indekslar
        // ────────────────────────────────────────────────────────────

        // «Shu xodimning mehnat tarixi» — xodim kartasining har ochilishi
        builder.HasIndex(employment => employment.EmployeeId);

        // «Firma bo'yicha shtat» va «bo'lim/lavozim bo'yicha shtat» hisobotlari
        builder.HasIndex(employment => employment.LegalEntityId);
        builder.HasIndex(employment => employment.DepartmentId);
        builder.HasIndex(employment => employment.PositionId);

        // «Faol xodimlar, ishga kirish sanasi bo'yicha» — asosiy ro'yxat ekrani
        // va «yangi kelganlar» hisoboti. Kompozit indeks saralashni ham qoplaydi.
        builder.HasIndex(employment => new { employment.Status, employment.HireDate });

        // «Falon oyda ishdan ketganlar» hisoboti. Qisman indeks: ishlab
        // turganlarda bu ustun NULL va ular indeksda o'rin egallashi shart emas
        // (~2000 munosabatning katta qismi aynan shunday).
        builder.HasIndex(employment => employment.TerminationDate)
            .HasFilter("termination_date IS NOT NULL");

        // «Xodimning ASOSIY ish joyi» — xodim ro'yxatida har bir qator uchun
        // so'raladi. Qisman indeks juda kichik bo'ladi: xodimda asosiy joy
        // bitta, qolgan o'rindoshliklar indeksga tushmaydi.
        builder.HasIndex(employment => new { employment.EmployeeId, employment.IsPrimary })
            .HasFilter("is_primary AND deleted_at IS NULL");
    }
}

/// <summary>
/// <see cref="Money"/> ni ikkita ustunga map qiladigan umumiy yordamchi.
/// </summary>
/// <remarks>
/// <para>
/// Nega alohida: pul uch joyda uchraydi (oklad, hodisadagi eski va yangi
/// qiymat) va ularning ustun turi bir xil bo'lishi SHART. Har birida qo'lda
/// yozilsa, birontasida aniqlik tushib qolishi va tiyinlar yo'qolishi mumkin.
/// </para>
/// <para>
/// <b>Nega ikkita ustun, bitta emas.</b> Valyuta kodisiz miqdor ma'nosiz:
/// bugun hammasi so'mda, lekin dollar hisobidagi oklad allaqachon
/// uchraydi. Bitta satr ustunga («4500000 UZS») yozilsa, summa bo'yicha
/// saralash va agregat qilish imkonsiz bo'lardi.
/// </para>
/// <para>
/// <b>Nega complex type.</b> <see cref="Money"/> — identifikatorsiz sof
/// qiymat; <c>OwnsOne</c> uni entity sifatida kuzatib, har saqlashda
/// ortiqcha ish qilardi. O'qishda EF qiymatni maxfiy konstruktor orqali
/// tekshiruvsiz yig'adi — bu <c>Money.FromTrusted</c> bilan bir xil ma'noni
/// beradi: bazadagi qiymat yozilayotganda allaqachon tekshirilgan.
/// </para>
/// </remarks>
internal static class MoneyMappingExtensions
{
    /// <summary>Pul maydonini <c>numeric(14,2)</c> + valyuta kodi ustunlariga yoyadi.</summary>
    public static void MapMoney<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, Money?>> selector,
        string amountColumn,
        string currencyColumn)
        where TEntity : class
    {
        builder.ComplexProperty(selector, money =>
        {
            // Oklad ko'rsatilmagan bo'lishi mumkin: Excel'dagi bir qism
            // yozuvda summa umuman yo'q, ishga qabul hodisasida ham u
            // ko'pincha bo'sh. Shu sababli butun qiymat NULL bo'la oladi.
            money.IsRequired(false);

            money.Property(value => value.Amount)
                .HasColumnName(amountColumn)
                .HasPrecision(14, 2);

            money.Property(value => value.Currency)
                .HasColumnName(currencyColumn)
                .HasMaxLength(3);
        });
    }
}
