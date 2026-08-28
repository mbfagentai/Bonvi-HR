using Bonvi.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="EmploymentEvent"/> uchun jadval konfiguratsiyasi
/// (<c>employment_events</c>).
/// </summary>
/// <remarks>
/// <para>
/// Bu jadval — tizimning tarixi. Undagi yozuv hech qachon o'zgartirilmaydi:
/// xato bo'lsa, tuzatuvchi yangi hodisa qo'shiladi. Shu sababli yumshoq
/// o'chirish ham yo'q — fakt yo bor, yo yo'q.
/// </para>
/// <para>
/// <b>Bo'lim va lavozimga tashqi kalit ATAYLAB qo'yilmagan.</b> Hodisada
/// «qayerdan / qayerga» qiymatlari — o'sha kundagi holatning surati.
/// Ma'lumotnomadagi lavozim keyinchalik o'chirilsa ham, «2019-yilda
/// omborchi bo'lgan» degan fakt kuchini yo'qotmasligi kerak. Tashqi kalit
/// bo'lsa, u yozuvni o'chirishga to'sqinlik qilardi yoki tarixni buzardi.
/// </para>
/// </remarks>
public sealed class EmploymentEventConfiguration : IEntityTypeConfiguration<EmploymentEvent>
{
    public void Configure(EntityTypeBuilder<EmploymentEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("employment_events");

        builder.HasKey(employmentEvent => employmentEvent.Id);

        builder.Ignore(employmentEvent => employmentEvent.DomainEvents);

        builder.Property(employmentEvent => employmentEvent.Type)
            .IsRequired();

        builder.Property(employmentEvent => employmentEvent.EffectiveDate)
            .IsRequired();

        builder.Property(employmentEvent => employmentEvent.RecordedAt)
            .IsRequired();

        builder.Property(employmentEvent => employmentEvent.IsImported)
            .IsRequired();

        builder.MapMoney(employmentEvent => employmentEvent.FromSalary, "from_salary_amount", "from_salary_currency");
        builder.MapMoney(employmentEvent => employmentEvent.ToSalary, "to_salary_amount", "to_salary_currency");

        // ────────────────────────────────────────────────────────────
        //  Indekslar
        // ────────────────────────────────────────────────────────────

        // Hodisalar DOIM munosabat bo'yicha va sana tartibida o'qiladi
        // («mehnat yo'li» ekrani). Kompozit indeks saralashni ham qoplaydi,
        // shu sababli ~5000 qatorlik jadvalda ORDER BY bepul bo'ladi.
        // Bu indeks tashqi kalit indeksini ham almashtiradi — `employment_id`
        // uning birinchi ustuni.
        builder.HasIndex(employmentEvent => new
        {
            employmentEvent.EmploymentId,
            employmentEvent.EffectiveDate,
        });

        // «Buyruq raqami bo'yicha topish» — HR aynan shuni so'radi: qo'lida
        // qog'oz buyruq bor, tizimdan uni topishi kerak. Qisman indeks:
        // Excel'dan ko'chirilgan hodisalarning ko'pida buyruq raqami yo'q.
        builder.HasIndex(employmentEvent => employmentEvent.OrderNo)
            .HasFilter("order_no IS NOT NULL");

        // «Falon davrda ishga kelganlar / ketganlar / lavozimi o'zgarganlar» —
        // Excel'dagi 91.2, 91.3, 91.6 varaqlarining o'rnini bosadigan so'rov.
        builder.HasIndex(employmentEvent => new
        {
            employmentEvent.Type,
            employmentEvent.EffectiveDate,
        });

        // «Bu nomzod qaysi xodimga aylandi» va taklifni qabul qilishning
        // takrorga qarshi tekshiruvi — ikkalasi ham SHU ustun bo'yicha
        // qidiradi. Qisman indeks juda kichik: hodisalarning katta qismi
        // rekrutingdan kelmaydi (Excel'dan ko'chirilgan 5000 yozuv va HR
        // qo'lda kiritgan qabullar).
        //
        // ⚠️ Tashqi kalit ATAYLAB YO'Q — yuqoridagi bo'lim/lavozim qarori
        // bilan bir xil sabab: bu jadval TARIX va unga qo'yilgan har bir
        // cheklov bir kun faktni o'chirishga yoki buzishga majbur qiladi.
        // Nomzod yozuvi hech qachon jismonan o'chirilmaydi (yumshoq
        // o'chirish), ya'ni havola baribir uzilmaydi.
        builder.HasIndex(employmentEvent => employmentEvent.CandidateId)
            .HasFilter("candidate_id IS NOT NULL");
    }
}
