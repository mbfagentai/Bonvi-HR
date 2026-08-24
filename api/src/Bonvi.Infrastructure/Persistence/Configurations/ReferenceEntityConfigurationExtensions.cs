using Bonvi.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="ReferenceEntity"/> dan meros olgan barcha ma'lumotnomalar uchun
/// umumiy jadval sozlamalari.
/// </summary>
/// <remarks>
/// <para>
/// Nega alohida yordamchi: sakkizta ma'lumotnomaning ustunlari va indekslari
/// bir xil. Har birida takrorlansa, keyinchalik qoida o'zgarganda (masalan
/// yangi indeks qo'shilganda) bittasi unutiladi va faqat ishlab chiqarishda
/// ma'lum bo'ladi. Bu yerda esa o'zgarish bitta joyda.
/// </para>
/// <para>
/// Ustun nomlari qo'lda yozilmaydi — <c>UseSnakeCaseNamingConvention()</c>
/// <c>SortOrder</c> ni <c>sort_order</c> ga o'zi aylantiradi.
/// </para>
/// </remarks>
internal static class ReferenceEntityConfigurationExtensions
{
    /// <summary>
    /// Ma'lumotnomaning kaliti, majburiy ustunlari va umumiy indekslarini o'rnatadi.
    /// </summary>
    /// <param name="builder">Sozlanayotgan entity.</param>
    /// <param name="tableName">
    /// Jadval nomi ATAYLAB aniq beriladi, <c>DbSet</c> nomidan olinmaydi.
    /// Sabab: <c>lower(name)</c> bo'yicha unikal indekslar migratsiyada xom SQL
    /// bilan yaratiladi va u yerda jadval nomi matn sifatida yoziladi.
    /// <c>DbSet</c> xossasining nomi o'zgarsa, xom SQL jimgina noto'g'ri
    /// jadvalga murojaat qilardi — bu yerda esa bog'lanish ko'rinib turadi.
    /// </param>
    public static void ConfigureReference<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        string tableName)
        where TEntity : ReferenceEntity
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(tableName);

        builder.HasKey(entity => entity.Id);

        // Domen hodisalari xotirada yashaydi — bazaga tegishli emas
        builder.Ignore(entity => entity.DomainEvents);

        // Nom bo'yicha saralash o'zbek alifbosi tartibida bo'lishi kerak —
        // sababi `DatabaseCollations` da tushuntirilgan. Bu yerda turgani
        // muhim: sakkizta ma'lumotnomaning hammasi shu yordamchidan o'tadi,
        // ya'ni qoida bitta joyda va unutilishi mumkin emas.
        builder.Property(entity => entity.Name)
            .UseCollation(DatabaseCollations.UzbekNames)
            .IsRequired();

        builder.Property(entity => entity.IsActive)
            .IsRequired();

        builder.Property(entity => entity.SortOrder)
            .IsRequired();

        // Uzunlik cheklovi ataylab QO'YILMAGAN (text). PostgreSQL'da varchar(n)
        // va text bir xil tezlikda ishlaydi, lekin cheklov domenda emas, faqat
        // bazada bo'lsa — import paytida tushunarsiz «value too long» xatosi
        // chiqadi. Uzunlik qoidasi kerak bo'lsa, u Domain'da yoziladi.

        // Kod — integratsiya va import kaliti. Unikal, LEKIN:
        //   • NULL kodlar ko'p bo'lishi mumkin (kod ixtiyoriy),
        //   • yumshoq o'chirilgan yozuvning kodini qayta ishlatish mumkin bo'lishi kerak.
        // Shu sababli qisman (partial) indeks.
        builder.HasIndex(entity => entity.Code)
            .IsUnique()
            .HasFilter("code IS NOT NULL AND deleted_at IS NULL");

        // Ma'lumotnomaning ASOSIY so'rovi bitta: «faol yozuvlarni tartib bo'yicha ber».
        // Bu kompozit indeks unga to'liq javob beradi — PostgreSQL saralashni
        // butunlay o'tkazib yuboradi.
        builder.HasIndex(entity => new { entity.IsActive, entity.SortOrder });
    }
}
