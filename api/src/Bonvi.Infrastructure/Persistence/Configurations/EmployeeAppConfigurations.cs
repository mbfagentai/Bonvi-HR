using System.Text.Json;
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

            /*
              SHAKL CHEKLOVLARI (SPEC-joylashuvlar §A) — yana o'sha sabab:
              domen `WorkSite.CreatePolygon` / `ReconfigurePolygon` da
              tekshiradi, baza esa qo'lda yozilgan SQL'ni ushlaydi.

              `ck_work_sites_polygon` — ikki tomonlama: ko'pburchakda uchlar
              BOR, doirada YO'Q. Bir tomoni yetmaydi: uchsiz «ko'pburchak»
              qabul qilish yo'lida `InvalidOperationException` beradi,
              uchli «doira» esa HR xaritada ko'rgan shakl bilan qaror
              qabul qilingan shaklni ajratib yuborardi. Uchlarning soni,
              oralig'i va kesishmasligi bu yerda TEKSHIRILMAYDI — JSON
              ichini SQL'da tahlil qilish domen qoidasining ikkinchi,
              ajralib ketadigan nusxasi bo'lardi.
            */
            table.HasCheckConstraint(
                "ck_work_sites_shape",
                $"shape IN ('{WorkSiteShapeColumn.Circle}', '{WorkSiteShapeColumn.Polygon}')");

            table.HasCheckConstraint(
                "ck_work_sites_polygon",
                $"(shape = '{WorkSiteShapeColumn.Polygon}') = (polygon IS NOT NULL)");

            table.HasCheckConstraint(
                "ck_work_sites_edge_buffer",
                $"edge_buffer_meters BETWEEN {WorkSite.MinEdgeBufferMeters} AND {WorkSite.MaxEdgeBufferMeters}");
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

        // Uzunlik chegarasi (300) domenda; ustun `text` — SPEC §A.
        builder.Property(site => site.Address);

        builder.Property(site => site.Shape)
            .HasConversion(
                shape => WorkSiteShapeColumn.ToText(shape),
                text => WorkSiteShapeColumn.FromText(text))
            .IsRequired();

        // `jsonb`: `[{ "lat": 41.311081, "lng": 69.279737 }, …]`. Ustun
        // qidirilmaydi va indekslanmaydi — faqat obyekt bilan birga o'qiladi.
        builder.Property(site => site.Polygon)
            .HasConversion(
                vertices => WorkSitePolygonColumn.ToJson(vertices!),
                json => WorkSitePolygonColumn.FromJson(json),
                WorkSitePolygonColumn.Comparer)
            .HasColumnType("jsonb");

        builder.Property(site => site.EdgeBufferMeters)
            .IsRequired();
    }
}

/// <summary>
/// <see cref="WorkSiteShape"/> ning bazadagi matni — <c>'circle'</c> / <c>'polygon'</c>.
/// </summary>
/// <remarks>
/// <para>
/// Enum SONI emas, MATN (SPEC-joylashuvlar §A): <c>work_sites.shape</c> va
/// <c>mobile_check_ins.site_shape</c> ni qo'lda o'qigan odam <c>2</c> emas,
/// <c>polygon</c> ni ko'radi. CHECK cheklovlari ham shu sinfdagi
/// konstantalardan yasaladi — ikki joyda ikki xil yozilish bo'lmasin.
/// </para>
/// <para>
/// ⚠️ Noma'lum qiymat ikkala tomonda ham ISTISNO beradi: jimgina «doira»
/// deb o'qish bazadagi buzuq qatorni noto'g'ri to'siq bilan ishlatardi.
/// </para>
/// </remarks>
internal static class WorkSiteShapeColumn
{
    public const string Circle = "circle";

    public const string Polygon = "polygon";

    public static string ToText(WorkSiteShape shape) => shape switch
    {
        WorkSiteShape.Circle => Circle,
        WorkSiteShape.Polygon => Polygon,
        _ => throw new InvalidOperationException($"Obyekt shakli bazaga yozilmaydi: {shape}."),
    };

    public static WorkSiteShape FromText(string text) => text switch
    {
        Circle => WorkSiteShape.Circle,
        Polygon => WorkSiteShape.Polygon,
        _ => throw new InvalidOperationException($"Bazada noma'lum obyekt shakli: «{text}»."),
    };
}

/// <summary>
/// Ko'pburchak uchlarining <c>jsonb</c> ko'rinishi va taqqoslagichi.
/// </summary>
/// <remarks>
/// <para>
/// JSON nomlari (<c>lat</c>, <c>lng</c>) SPEC §A dan va ular shu yerda,
/// domenda emas: domen nuqtasi <see cref="GeoPoint"/> <c>Latitude</c> /
/// <c>Longitude</c> deb ataladi va saqlash shaklini bilmaydi.
/// </para>
/// <para>
/// Taqqoslagich KETMA-KETLIK bo'yicha: usiz EF ro'yxatni havola bo'yicha
/// solishtirib, bir xil uchlardan yasalgan yangi massivni «o'zgargan» deb
/// har saqlashda keraksiz UPDATE yuborardi (<c>PhoneNumber</c>
/// taqqoslagichlaridagi bilan bir xil sabab).
/// </para>
/// </remarks>
internal static class WorkSitePolygonColumn
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ValueComparer<IReadOnlyList<GeoPoint>?> Comparer { get; } = new(
        (left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
        vertices => vertices == null
            ? 0
            : vertices.Aggregate(0, (hash, vertex) => HashCode.Combine(hash, vertex.GetHashCode())),
        vertices => vertices == null ? null : vertices.ToArray());

    public static string ToJson(IReadOnlyList<GeoPoint> vertices)
        => JsonSerializer.Serialize(
            vertices.Select(vertex => new StoredVertex(vertex.Latitude, vertex.Longitude)),
            Json);

    public static IReadOnlyList<GeoPoint> FromJson(string json)
        => (JsonSerializer.Deserialize<StoredVertex[]>(json, Json)
                ?? throw new InvalidOperationException("work_sites.polygon: JSON massiv emas."))
            .Select(vertex => new GeoPoint(vertex.Lat, vertex.Lng))
            .ToArray();

    /// <summary>Bazadagi bitta uch — <c>{ "lat": …, "lng": … }</c>.</summary>
    private sealed record StoredVertex(decimal Lat, decimal Lng);
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

            /*
              RUXSAT — shaklga qarab (SPEC-joylashuvlar §B): doirada bu
              radius (50–5000), ko'pburchakda chet zaxirasi (0–500). Ustun
              nomi tarixiy, ma'nosi `site_shape` bilan birga o'qiladi.
            */
            table.HasCheckConstraint(
                "ck_mobile_check_ins_radius",
                $"(site_shape = '{WorkSiteShapeColumn.Circle}' AND site_radius_meters " +
                $"BETWEEN {WorkSite.MinRadiusMeters} AND {WorkSite.MaxRadiusMeters}) OR " +
                $"(site_shape = '{WorkSiteShapeColumn.Polygon}' AND site_radius_meters " +
                $"BETWEEN {WorkSite.MinEdgeBufferMeters} AND {WorkSite.MaxEdgeBufferMeters})");

            table.HasCheckConstraint(
                "ck_mobile_check_ins_site_shape",
                $"site_shape IN ('{WorkSiteShapeColumn.Circle}', '{WorkSiteShapeColumn.Polygon}')");
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

        builder.Property(checkIn => checkIn.SiteShape)
            .HasConversion(
                shape => WorkSiteShapeColumn.ToText(shape),
                text => WorkSiteShapeColumn.FromText(text))
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
/// <remarks>
/// ⚠️ <c>login</c> ustuni YO'Q (2026-09-27 dan): login — xodim profilidagi
/// telefon va hisob kirishda <c>employees.phone</c> orqali topiladi
/// (sababi <see cref="EmployeeAccount"/> izohida).
/// </remarks>
public sealed class EmployeeAccountConfiguration : IEntityTypeConfiguration<EmployeeAccount>
{
    public void Configure(EntityTypeBuilder<EmployeeAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("employee_accounts");

        builder.HasKey(account => account.Id);

        builder.Ignore(account => account.DomainEvents);

        builder.Property(account => account.PasswordHash)
            .IsRequired();

        // `text`, nullable: shifrlangan nusxa (`v1.<base64url>`). `null` —
        // nusxasi yo'q eski parol; HR yangisini yasaydi.
        builder.Property(account => account.PasswordProtected);

        builder.Property(account => account.PasswordSetAt)
            .IsRequired();

        // Nullable: `null` — joriy parolni HR hali bilmaydi va kirib bo'lmaydi
        // (sababi domenda). Eski hisoblar migratsiyada audit izidan to'ldirilgan.
        builder.Property(account => account.PasswordHandedOutAt);

        // Xodim yumshoq o'chiriladi, ya'ni haqiqiy `DELETE` — yo tasodif,
        // yo tuzatish skripti. Ikkala holatda ham u kirish ma'lumotini
        // jimgina olib ketmasligi, to'xtab so'rashi kerak.
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(account => account.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        /*
          Bitta xodimda bitta hisob. Filtrsiz — jadvalda `deleted_at`
          ustunining o'zi yo'q (sabab domenda). Avtomatik ochishdagi
          poygani (fon vazifasi va HR ekrani bir vaqtda) aynan shu indeks
          tutadi (`PostgresFault.IsDuplicateEmployeeAccount`).
        */
        builder.HasIndex(account => account.EmployeeId)
            .IsUnique()
            .HasDatabaseName("ux_employee_accounts_employee");
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
