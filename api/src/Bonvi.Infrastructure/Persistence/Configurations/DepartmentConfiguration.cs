using Bonvi.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Department"/> uchun jadval konfiguratsiyasi (<c>departments</c>).
/// </summary>
/// <remarks>
/// <para>
/// Bo'lim — o'ziga o'zi murojaat qiladigan daraxt. <c>Level</c> va <c>Path</c>
/// hisoblangan qiymatlar: ular har so'rovda rekursiv CTE ishlatmaslik uchun
/// saqlanadi va <c>DepartmentRepository.RebuildHierarchyAsync</c> bilan
/// yangilanadi.
/// </para>
/// </remarks>
public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureReference("departments");

        builder.Property(department => department.Level)
            .IsRequired();

        // Navigatsiyasiz o'ziga murojaat: Domain EF Core'ni bilmaydi va
        // `Department` ichida `Parent`/`Children` xossalari yo'q — agregatlar
        // bir-birini faqat identifikator orqali biladi.
        //
        // Restrict, Cascade emas: ota bo'lim o'chirilganda butun tarmoq
        // jimgina yo'q bo'lib ketishi mumkin emas. Avval bolalar ko'chiriladi.
        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(department => department.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LegalEntity>()
            .WithMany()
            .HasForeignKey(department => department.LegalEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        // «Shu bo'limning bolalari» — daraxt ko'rinishidagi har bir ochish amali.
        // Rekursiv CTE ham aynan shu indeksdan foydalanadi (JOIN parent_id bo'yicha).
        builder.HasIndex(department => department.ParentId);

        // Yo'l bo'yicha qidiruv: «7-Департамент / …» — butun tarmoqni bitta
        // `LIKE 'prefiks%'` bilan olish. Standart B-tree indeks kirill matnli
        // collation'da LIKE'ga yaramaydi, shu sababli `text_pattern_ops`:
        // u qiymatni bayt bo'yicha taqqoslaydi va prefiks qidiruvini tezlashtiradi.
        builder.HasIndex(department => department.Path)
            .HasOperators("text_pattern_ops")
            .HasFilter("path IS NOT NULL");

        // Bo'lim boshlig'i — kelajakdagi `Employee` agregatiga havola.
        // Tashqi kalit ATAYLAB yo'q: `employees` jadvali hali mavjud emas va
        // bo'limlar undan oldin urug'lantiriladi.
        builder.HasIndex(department => department.HeadEmployeeId)
            .HasFilter("head_employee_id IS NOT NULL");
    }
}
