using Bonvi.Domain.PublicApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="ApiKey"/> uchun jadval konfiguratsiyasi (<c>api_keys</c>).
/// </summary>
/// <remarks>
/// <para>
/// Jadval kichik (holdingdagi loyihalar soni) va deyarli o'zgarmaydi,
/// lekin u HAR BIR ommaviy so'rovda o'qiladi. Shu sababli bitta indeks
/// muhim: <c>key_prefix</c> ustidagi unikal indeks — kalitni tekshirishga
/// ketadigan yagona so'rov aynan shu ustun bo'yicha.
/// </para>
/// <para>
/// <c>ISoftDeletable</c> ATAYLAB amalga oshirilmagan: bekor qilingan
/// kalit tarixdir va u ro'yxatda ko'rinib turishi kerak (sababi
/// <see cref="ApiKey"/> izohida). Ya'ni bu jadvalga global yumshoq
/// o'chirish filtri qo'llanmaydi.
/// </para>
/// </remarks>
public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("api_keys");

        builder.HasKey(key => key.Id);

        builder.Ignore(key => key.DomainEvents);

        builder.Property(key => key.Name)
            .HasMaxLength(ApiKey.MaxNameLength)
            .IsRequired();

        // Prefiks qat'iy uzunlikda: u formatning bir qismi
        // (`ApiKeyFormat.PrefixLength`), o'zgaruvchi matn emas.
        builder.Property(key => key.KeyPrefix)
            .HasMaxLength(ApiKeyFormat.PrefixLength)
            .IsRequired();

        // SHA-256 — har doim aynan 32 bayt. `bytea` bilan `HasMaxLength`
        // PostgreSQL'da cheklov bermaydi, lekin u NIYATNI e'lon qiladi va
        // modelda xeshning uzunligi yozilgan bo'lib qoladi.
        builder.Property(key => key.KeyHash)
            .HasMaxLength(ApiKeyFormat.HashLength)
            .IsRequired();

        builder.Property(key => key.LastUsedIp)
            .HasMaxLength(ApiKey.MaxIpLength);

        // Kalitni tekshirishning YAGONA so'rovi — shu indeks bo'yicha.
        // Unikal: ikkita kalit bir xil prefiks bilan yashasa, tekshiruv
        // qaysi biriga tegishli ekanini bilmasdi.
        builder.HasIndex(key => key.KeyPrefix)
            .IsUnique();

        // Nom BEKOR QILINMAGANLAR orasida unikal. Qisman indeks ataylab:
        // sizib ketgan kalit bekor qilingandan keyin o'sha loyihaga
        // AYNAN o'sha nom bilan yangi kalit berish mumkin bo'lishi kerak
        // (sababi `IApiKeyRepository.ActiveNameExistsAsync` izohida).
        //
        // `lower(name)` ATAYLAB: «BonviCall» va «bonvicall» bir xil
        // loyiha va ikkita yozuv bo'lib turishi chalkashlik berardi.
        // Ifoda kod tomonidagi tekshiruv bilan AYNAN bir xil
        // (`ApiKeyRepository.NameEquals`), aks holda tekshiruvdan o'tgan
        // nom bazada 23505 bilan yiqilib, mijoz 409 o'rniga 500 olardi.
        //
        // Ifoda ustidagi indeksni fluent API ifodalay olmaydi, shu
        // sababli u migratsiyada xom SQL bilan yaratiladi —
        // ma'lumotnomalardagi bilan aynan bir xil naqsh.
    }
}
