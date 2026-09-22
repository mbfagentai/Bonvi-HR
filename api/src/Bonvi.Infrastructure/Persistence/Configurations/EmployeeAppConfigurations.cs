using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/*
  ════════════════════════════════════════════════════════════════════
   XODIM ILOVASINING JADVALLARI — to'rtta

   Faylning butun bo'ylab amal qiladigan ikkita qoida
   `AttendanceConfigurations.cs` boshidagi bilan AYNAN bir xil va bu
   ataylab:

   1. INDEKS NOMLARI QO'LDA beriladi (`HasDatabaseName`) —
      ma'lumot modeli hujjati ularni nomi bilan sanab chiqqan va joriy
      qilish tekshiruvi aynan o'sha nomlarni qidiradi.

   2. USTUN SUKUT QIYMATLARI (`HasDefaultValue`) QO'YILMAYDI. Qator
      har doim domen bergan qiymat bilan yoziladi; `bool` ustunda EF
      CLR sukutini «qiymat berilmadi» deb o'qiydi va ataylab yozilgan
      `false` jimgina `true` bo'lib tushardi.
  ════════════════════════════════════════════════════════════════════
*/

/// <summary>
/// <see cref="WorkSite"/> uchun jadval konfiguratsiyasi (<c>work_sites</c>).
/// </summary>
/// <remarks>
/// <para>
/// <c>ConfigureReference</c> bir qatorda o'n narsani beradi: kalit, o'zbek
/// alifbosi bo'yicha saralanadigan <c>name</c>, <c>is_active</c>,
/// <c>note</c>, audit va yumshoq o'chirish ustunlari hamda
/// <c>(is_active, name)</c> ro'yxat indeksi. Ularni qo'lda yozish —
/// birortasini unutish imkoniyati.
/// </para>
/// <para>
/// ⚠️ <c>lower(name)</c> bo'yicha unikal indeks BU YERDA yo'q: EF fluent
/// API ifoda ustidagi indeksni ifodalay olmaydi. U migratsiyada xom SQL
/// bilan yaratiladi — qolgan to'qqizta ma'lumotnomadagi bilan bir xil
/// texnika.
/// </para>
/// </remarks>
public sealed class WorkSiteConfiguration : IEntityTypeConfiguration<WorkSite>
{
    public void Configure(EntityTypeBuilder<WorkSite> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureReference("work_sites");

        /*
          KOORDINATA VA RADIUS CHEKLOVLARI — bazada ham.

          Domen `WorkSite.Create` / `Reconfigure` da aynan shu qoidalarni
          tekshiradi, lekin domen tekshiruvi migratsiya yoki qo'lda
          yozilgan SQL orqali kelgan qatorni qo'riqlay olmaydi
          (`work_schedule_days` dagi bilan bir xil sabab).

          ⚠️ O'zbekiston chegaralari bo'yicha TORROQ cheklov ATAYLAB
          qo'yilmadi. U eng ehtimoliy kiritish xatosini — kenglik bilan
          uzunlikning almashib qolishini — ushlagan bo'lardi (global
          tekshiruv ushlamaydi: (69.24, 41.31) Somalida mutlaqo haqiqiy
          nuqta). Lekin xoldingning chet eldagi obyekti paydo bo'lgan
          kuni tuzatish shoshilinch migratsiyaga aylanardi. Chegara
          tekshiruvining o'rni — HR formasidagi OGOHLANTIRISH
          («bu nuqta O'zbekistonda emas — to'g'rimi?»), cheklov emas.
        */
        builder.ToTable("work_sites", table =>
        {
            table.HasCheckConstraint(
                "ck_work_sites_coordinates",
                "latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180");

            table.HasCheckConstraint(
                "ck_work_sites_radius",
                $"radius_meters BETWEEN {WorkSite.MinRadiusMeters} AND {WorkSite.MaxRadiusMeters}");
        });

        // `numeric(9,6)`: uch butun xona (±180 ga yetadi) va olti kasr
        // xona (~11 sm). `double precision` RAD ETILDI — koordinatani odam
        // o'qiydi va qayta kiritadi, JSON javobidagi `41.31108099999999`
        // esa abadiy murojaat manbai bo'lardi.
        builder.Property(site => site.Latitude)
            .HasPrecision(9, 6)
            .IsRequired();

        builder.Property(site => site.Longitude)
            .HasPrecision(9, 6)
            .IsRequired();

        builder.Property(site => site.RadiusMeters)
            .IsRequired();
    }
}

/// <summary>
/// <see cref="MobileCheckIn"/> uchun jadval konfiguratsiyasi (<c>mobile_check_ins</c>).
/// </summary>
/// <remarks>
/// Audit ustunlari ham, yumshoq o'chirish ham yo'q — sababi domenda:
/// bu qatorni hech kim tahrirlamaydi va qo'lda o'chirmaydi.
/// </remarks>
public sealed class MobileCheckInConfiguration : IEntityTypeConfiguration<MobileCheckIn>
{
    public void Configure(EntityTypeBuilder<MobileCheckIn> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("mobile_check_ins", table =>
        {
            table.HasCheckConstraint(
                "ck_mobile_check_ins_coordinates",
                "latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180");

            // Manfiy masofa yoki manfiy aniqlik — dastur xatosi. Bazada
            // to'silgani uchun u 500 bo'lib chiqadi, jimgina saqlanmaydi.
            table.HasCheckConstraint(
                "ck_mobile_check_ins_meters",
                "accuracy_meters >= 0 AND distance_meters >= 0");

            table.HasCheckConstraint(
                "ck_mobile_check_ins_radius",
                $"site_radius_meters BETWEEN {WorkSite.MinRadiusMeters} AND {WorkSite.MaxRadiusMeters}");
        });

        builder.HasKey(checkIn => checkIn.Id);

        builder.Ignore(checkIn => checkIn.DomainEvents);

        builder.Property(checkIn => checkIn.Latitude)
            .HasPrecision(9, 6)
            .IsRequired();

        builder.Property(checkIn => checkIn.Longitude)
            .HasPrecision(9, 6)
            .IsRequired();

        // 0–100 oralig'i tekshiruvda qo'yilgan, ya'ni `numeric(5,1)`
        // zaxira bilan yetadi. Bir kasr xona ATAYLAB: 0,4 ni 0 ga
        // yaxlitlash ekranda «mukammal aniqlik» bo'lib ko'rinardi.
        builder.Property(checkIn => checkIn.AccuracyMeters)
            .HasPrecision(5, 1)
            .IsRequired();

        // 99 999,9 gacha — 5 000 metrlik radius shiftidan ancha keng,
        // lekin qiymat CHEGARALANGANINI ifodalaydi.
        builder.Property(checkIn => checkIn.DistanceMeters)
            .HasPrecision(6, 1)
            .IsRequired();

        builder.Property(checkIn => checkIn.SiteRadiusMeters)
            .IsRequired();

        builder.Property(checkIn => checkIn.CreatedAt)
            .IsRequired();

        /*
          ⭐ CASCADE — butun modulda YAGONA joy, va u ataylab.

          `attendance_passes` saqlash muddati siyosati bilan HAQIQATAN
          o'chiriladi (`ExecuteDeleteAsync`, sukut 365 kun). Modulning
          odatdagi `Restrict` i bu yerda qo'yilsa, tozalash vazifasi
          birinchi telefon belgisidan roppa-rosa bir yil keyin tashqi
          kalit xatosi bilan yiqilib, har kecha yiqilaverardi — disk esa
          o'sishda davom etardi.

          ⚠️ `ExecuteDeleteAsync` EF ning o'zgarish izlagichini chetlab
          o'tadi, ya'ni ishlaydigani AYNAN BAZA darajasidagi kaskad.
          EF tomonidagi `DeleteBehavior.Cascade` esa shu bog'lanish uchun
          MIGRATSIYAGA `ON DELETE CASCADE` yozdiradi — kerak bo'lgani shu.
        */
        builder.HasOne<AttendancePass>()
            .WithMany()
            .HasForeignKey(checkIn => checkIn.PassId)
            .OnDelete(DeleteBehavior.Cascade);

        // Obyekt va xodim — `Restrict`: ikkalasi ham yumshoq o'chiriladi,
        // haqiqiy `DELETE` esa yo tasodif, yo tuzatish skripti — ikkala
        // holatda ham u dalil qatorini o'zi bilan olib ketmasligi kerak.
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(checkIn => checkIn.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WorkSite>()
            .WithMany()
            .HasForeignKey(checkIn => checkIn.WorkSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        /*
          Har bir o'tishga ko'pi bilan BITTA koordinata. Jurnal sahifasi
          shu indeks orqali bitta `WHERE pass_id = ANY(…)` bilan o'qiydi.

          ⚠️ `employee_id` va `work_site_id` ustunlariga indeks ATAYLAB
          qo'yilmadi: PostgreSQL tashqi kalitni o'zi indekslamaydi va
          indekssiz bola ustuni faqat OTA qator o'chirilganda qimmatga
          tushadi — bu ikkalasi esa hech qachon haqiqiy `DELETE`
          ko'rmaydi (ikkalasi ham yumshoq o'chiriladi).
        */
        builder.HasIndex(checkIn => checkIn.PassId)
            .IsUnique()
            .HasDatabaseName("ux_mobile_check_ins_pass");
    }
}

/// <summary>
/// <see cref="EmployeeAccount"/> uchun jadval konfiguratsiyasi (<c>employee_accounts</c>).
/// </summary>
public sealed class EmployeeAccountConfiguration : IEntityTypeConfiguration<EmployeeAccount>
{
    /// <summary>
    /// <see cref="PhoneNumber"/> uchun taqqoslagich.
    /// </summary>
    /// <remarks>
    /// <c>UserConfiguration.LoginComparer</c> ning aynan nusxasi va shunday
    /// bo'lishi kerak: konvertordan keyin EF qiymatni nusxalab saqlaydi va
    /// havola bo'yicha taqqoslashda bir xil raqamdan yasalgan ikki obyekt
    /// «har xil» bo'lib, har saqlashda keraksiz UPDATE yuborilardi.
    /// </remarks>
    private static readonly ValueComparer<PhoneNumber> LoginComparer = new(
        (left, right) => left!.Value == right!.Value,
        phone => StringComparer.Ordinal.GetHashCode(phone.Value),
        phone => PhoneNumber.FromTrusted(phone.Value));

    public void Configure(EntityTypeBuilder<EmployeeAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("employee_accounts");

        builder.HasKey(account => account.Id);

        builder.Ignore(account => account.DomainEvents);

        // Login — `users.login` bilan AYNAN bir xil qiymat obyekti, bir xil
        // konvertor va bir xil taqqoslagich. Bazadan o'qishda tekshiruvsiz
        // qayta yig'iladi: yozilayotganda allaqachon tekshirilgan.
        builder.Property(account => account.Login)
            .HasConversion(
                login => login.Value,
                value => PhoneNumber.FromTrusted(value),
                LoginComparer)
            .IsRequired();

        builder.Property(account => account.PasswordHash)
            .IsRequired();

        builder.Property(account => account.MustChangePassword)
            .IsRequired();

        builder.Property(account => account.PasswordSetAt)
            .IsRequired();

        builder.Property(account => account.IsActive)
            .IsRequired();

        builder.Property(account => account.FailedLoginAttempts)
            .IsRequired();

        // Xodim yumshoq o'chiriladi, ya'ni haqiqiy `DELETE` — yo tasodif,
        // yo tuzatish skripti. Ikkala holatda ham u kirish ma'lumotini
        // jimgina olib ketmasligi, to'xtab so'rashi kerak.
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(account => account.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        /*
          IKKALA UNIKAL INDEKSDA HAM yumshoq o'chirish filtri YO'Q — chunki
          jadvalda `deleted_at` ustunining o'zi yo'q (sabab domenda).
          Ma'lumotnomalardan farqi shu: o'chirilgan yozuvning NOMINI qayta
          ishlatish kerak bo'ladi, band bo'lib qolgan LOGIN esa band
          bo'lib qolishi kerak.
        */
        builder.HasIndex(account => account.EmployeeId)
            .IsUnique()
            .HasDatabaseName("ux_employee_accounts_employee");

        builder.HasIndex(account => account.Login)
            .IsUnique()
            .HasDatabaseName("ux_employee_accounts_login");
    }
}

/// <summary>
/// <see cref="EmployeeRefreshToken"/> uchun jadval konfiguratsiyasi
/// (<c>employee_refresh_tokens</c>).
/// </summary>
/// <remarks>
/// <c>RefreshTokenConfiguration</c> ning nusxasi, faqat boshqa ota jadval
/// bilan. <c>IAuditable</c> emas: tokenni «kim o'zgartirdi» degan savol
/// yo'q, butun tarixi <c>created_at</c> / <c>revoked_at</c> da.
/// </remarks>
public sealed class EmployeeRefreshTokenConfiguration : IEntityTypeConfiguration<EmployeeRefreshToken>
{
    public void Configure(EntityTypeBuilder<EmployeeRefreshToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("employee_refresh_tokens");

        builder.HasKey(token => token.Id);

        builder.Ignore(token => token.DomainEvents);

        builder.Property(token => token.TokenHash)
            .IsRequired();

        builder.Property(token => token.ExpiresAt)
            .IsRequired();

        builder.Property(token => token.CreatedAt)
            .IsRequired();

        // 45 — IPv6 ning eng uzun matn ko'rinishi (IPv4 ko'rinishi bilan).
        builder.Property(token => token.CreatedByIp)
            .HasMaxLength(45);

        builder.Property(token => token.RevokedByIp)
            .HasMaxLength(45);

        // ⭐ CASCADE: hisobdan omon qolgan yangilash tokeni — xavfsizlik
        // teshigi. Bu — hisob qatorini tirik ma'lumot qoldirmasdan
        // haqiqatan o'chirish mumkin bo'lgan yagona yo'l.
        builder.HasOne<EmployeeAccount>()
            .WithMany()
            .HasForeignKey(token => token.EmployeeAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Har bir yangilashdagi birinchi amal — xesh bo'yicha qidirish.
        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_employee_refresh_tokens_hash");

        // «Hisobning tirik tokenlari» va muddati o'tganlarini tozalash —
        // ikkalasi ham shu kompozit indeksdan foydalanadi.
        builder.HasIndex(token => new { token.EmployeeAccountId, token.ExpiresAt })
            .HasDatabaseName("ix_employee_refresh_tokens_account_expires");
    }
}
