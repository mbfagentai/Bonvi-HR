using Bonvi.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Incentive"/> uchun jadval konfiguratsiyasi (<c>incentives</c>).
/// </summary>
/// <remarks>
/// <para>
/// <c>TypeId</c> uchun tashqi kalit ATAYLAB yo'q: u <see cref="IncentiveKind"/>ga
/// qarab yoki <c>award_types</c>, yoki <c>penalty_types</c> yozuviga ishora
/// qiladi — bitta ustun ikki jadvalning birortasiga qat'iy bog'lanolmaydi.
/// Mavjudligi va mos kelishi Application qatlamida (buyruq ishlovchisida)
/// tekshiriladi.
/// </para>
/// </remarks>
public sealed class IncentiveConfiguration : IEntityTypeConfiguration<Incentive>
{
    public void Configure(EntityTypeBuilder<Incentive> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        /*
          CHEKLOVLAR — domen (`Incentive.Grant` / `Update`) qoidalarining
          bazadagi nusxasi: qo'lda yozilgan SQL yoki eski klient ham ularni
          chetlab o'tolmasin.

          `kind` raqamlari — `IncentiveKind`: Award = 1, Penalty = 2.
        */
        builder.ToTable("incentives", table =>
        {
            // Jarima — summa YOKI foiz, ikkalasi birga emas.
            table.HasCheckConstraint(
                "ck_incentives_amount_or_percent",
                "amount IS NULL OR percent IS NULL");

            // Foiz faqat jarimada.
            table.HasCheckConstraint(
                "ck_incentives_percent_penalty_only",
                $"percent IS NULL OR kind = {(int)IncentiveKind.Penalty}");

            table.HasCheckConstraint(
                "ck_incentives_percent_range",
                $"percent IS NULL OR (percent > 0 AND percent <= {Incentive.MaxPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        });

        builder.HasKey(incentive => incentive.Id);

        builder.Ignore(incentive => incentive.DomainEvents);

        builder.Property(incentive => incentive.Kind)
            .IsRequired();

        builder.Property(incentive => incentive.TypeId)
            .IsRequired();

        builder.Property(incentive => incentive.Date)
            .IsRequired();

        builder.Property(incentive => incentive.GiftDescription)
            .HasMaxLength(300);

        builder.Property(incentive => incentive.Reason)
            .HasMaxLength(1000);

        builder.MapMoney(incentive => incentive.Amount, "amount", "amount_currency");

        // `numeric(5,2)`: 100.00 gacha, ikki kasr xona — domendagi
        // `Incentive.PercentScale` bilan bir xil.
        builder.Property(incentive => incentive.Percent)
            .HasPrecision(5, Incentive.PercentScale);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(incentive => incentive.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // «Xodim kartasi — Rag'bat/Jarima» tabi shu bo'yicha so'raladi,
        // har ochilishda.
        builder.HasIndex(incentive => incentive.EmployeeId);

        // «Barcha jarimalar» / «Barcha rag'batlar» hisoboti — tur bo'yicha
        // filtrlangan ro'yxat sana bo'yicha kamayish tartibida.
        builder.HasIndex(incentive => new { incentive.Kind, incentive.Date });
    }
}
