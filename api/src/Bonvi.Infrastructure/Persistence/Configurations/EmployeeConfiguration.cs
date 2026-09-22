using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Employee"/> uchun jadval konfiguratsiyasi (<c>employees</c>).
/// </summary>
/// <remarks>
/// <para>
/// Bu — tizimdagi eng ko'p o'qiladigan jadval: har bir ro'yxat, har bir
/// hisobot va har bir qidiruv shu yerdan boshlanadi. Shu sababli indekslar
/// «bo'lsa yaxshi» emas, balki ekran talabi bo'yicha tanlangan — har
/// birining tepasida uni QAYSI so'rov ishlatishi yozilgan.
/// </para>
/// <para>
/// JSHSHIR, pasport va telefon — qiymat obyektlari. Ular bazada oddiy satr
/// bo'lib yotadi va o'qishda <c>FromTrusted</c> bilan tekshiruvsiz qayta
/// yig'iladi: bazadagi qiymat yozilayotganda allaqachon tekshirilgan, uni
/// ikkinchi marta tekshirish faqat migratsiyani sindiradi (Excel'dan
/// ko'chirilgan yozuvlarning bir qismi ataylab «nuqsonli» holda saqlanadi).
/// </para>
/// </remarks>
public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    /// <summary>
    /// Qiymat obyektlari uchun taqqoslagichlar.
    /// </summary>
    /// <remarks>
    /// Nega kerak: konvertordan keyin EF o'zgarishlarni aniqlash uchun qiymat
    /// nusxasini saqlaydi. Havola bo'yicha taqqoslashda bir xil raqamdan
    /// yasalgan ikki obyekt «har xil» deb hisoblanib, har saqlashda keraksiz
    /// UPDATE yuborilardi. Shu sababli taqqoslash satr qiymati bo'yicha
    /// aniq beriladi (<c>UserConfiguration</c> dagi bilan bir xil naqsh).
    /// </remarks>
    private static readonly ValueComparer<Pinfl> PinflComparer = new(
        (left, right) => left!.Value == right!.Value,
        pinfl => StringComparer.Ordinal.GetHashCode(pinfl.Value),
        pinfl => Pinfl.FromTrusted(pinfl.Value));

    private static readonly ValueComparer<PassportSeries> PassportComparer = new(
        (left, right) => left!.Value == right!.Value,
        passport => StringComparer.Ordinal.GetHashCode(passport.Value),
        passport => PassportSeries.FromTrusted(passport.Value));

    private static readonly ValueComparer<PhoneNumber> PhoneComparer = new(
        (left, right) => left!.Value == right!.Value,
        phone => StringComparer.Ordinal.GetHashCode(phone.Value),
        phone => PhoneNumber.FromTrusted(phone.Value));

    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("employees");

        builder.HasKey(employee => employee.Id);

        // Domen hodisalari xotirada yashaydi — bazaga tegishli emas
        builder.Ignore(employee => employee.DomainEvents);

        // FIO — uchta ustunga yoyiladi. Complex type tanlandi (OwnsOne emas):
        // u alohida identifikatorga ega bo'lmagan sof qiymat, EF uni entity
        // sifatida kuzatmaydi va ortiqcha tracking yuki bo'lmaydi.
        //
        // Ustun nomlari ataylab qo'lda: standart nom "full_name_last_name"
        // bo'lardi — prefiks bu yerda hech narsa qo'shmaydi, faqat trigramma
        // indeksining ifodasini uzaytiradi.
        //
        // Uchala ustunga o'zbek kolatsiyasi qo'yiladi: xodimlar ro'yxati
        // aynan familiya bo'yicha saralanadi va standart `en_US.utf8` da
        // «Ўринов» «Абдуллаев» dan oldin chiqib qolardi
        // (sababi `DatabaseCollations` da).
        builder.ComplexProperty(employee => employee.FullName, fullName =>
        {
            fullName.Property(name => name.LastName)
                .HasColumnName("last_name")
                .UseCollation(DatabaseCollations.UzbekNames)
                .IsRequired();

            fullName.Property(name => name.FirstName)
                .HasColumnName("first_name")
                .UseCollation(DatabaseCollations.UzbekNames)
                .IsRequired();

            fullName.Property(name => name.MiddleName)
                .HasColumnName("middle_name")
                .UseCollation(DatabaseCollations.UzbekNames);
        });

        // JSHSHIR bo'sh bo'lishi MUMKIN: Excel'dagi 540 yozuvdan 3 tasida u
        // umuman yo'q, 9 tasida nazorat raqami xato. Majburiy qilinsa, o'sha
        // xodimlarni ko'chirib bo'lmasdi.
        //
        // Konvertorlarda `!` ataylab: EF konvertorga HECH QACHON `null`
        // uzatmaydi — u NULL ustunni konvertorgacha ushlab qoladi. Kompilyator
        // buni bilmaydi, shu sababli va'da aniq beriladi.
        builder.Property(employee => employee.Pinfl)
            .HasConversion(
                pinfl => pinfl!.Value,
                value => Pinfl.FromTrusted(value),
                PinflComparer);

        builder.Property(employee => employee.Passport)
            .HasConversion(
                passport => passport!.Value,
                value => PassportSeries.FromTrusted(value),
                PassportComparer);

        builder.Property(employee => employee.Phone)
            .HasConversion(
                phone => phone!.Value,
                value => PhoneNumber.FromTrusted(value),
                PhoneComparer);

        builder.Property(employee => employee.SecondPhone)
            .HasConversion(
                phone => phone!.Value,
                value => PhoneNumber.FromTrusted(value),
                PhoneComparer);

        builder.Property(employee => employee.NeedsReview)
            .IsRequired();

        /*
          TELEFONDAN BELGILASH RUXSATI — modulning YAGONA mavjud jadvalga
          qo'shilgan ustuni (SPEC-xodim-ilovasi §C).

          `NOT NULL`, nullable emas: savol ikki holatli va uchinchi holat
          har bir o'qish joyida `?? false` bo'lib tarjima qilinadigan
          yolg'on bo'lardi. PostgreSQL 11+ da `NOT NULL DEFAULT false`
          bilan ustun qo'shish METAMA'LUMOT amali — 950 qator qayta
          yozilmaydi.

          ⚠️ Indeks ATAYLAB yo'q. Uni birorta so'rov o'qimaydi: ruxsat
          FAQAT bitta xodimning kartasida va belgilash yo'lida — ikkalasi
          ham birlamchi kalit bo'yicha. «Ruxsati borlar ro'yxati» ekrani
          paydo bo'lgan kuni qisman indeks qo'shiladi (`needs_review`
          uslubida).
        */
        builder.Property(employee => employee.MobileCheckInAllowed)
            .IsRequired();

        // Yo'nalishlar — PostgreSQL massivi (uuid[]), join jadval emas.
        //
        // Nega: `Employee` da `Direction` ga navigatsiya xossasi yo'q va
        // bo'lmasligi kerak ham (agregatlar bir-birini identifikator orqali
        // biladi). Navigatsiyasiz ko'p-ko'pga bog'lanishda EF join jadvalini
        // o'qiy olmaydi — ro'yxat bo'sh qolib, xodim kartasi jimgina
        // «yo'nalishsiz» ko'rinardi.
        //
        // Ataylab maydon nomi bilan emas, XOSSA bilan bog'landi: shundagina
        // `Where(e => e.DirectionIds.Contains(id))` SQL'ga tarjima qilinadi.
        builder.PrimitiveCollection(employee => employee.DirectionIds)
            .HasField("_directionIds")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();

        // ────────────────────────────────────────────────────────────
        //  Indekslar
        // ────────────────────────────────────────────────────────────

        // JSHSHIR unikal, LEKIN ikki shart bilan:
        //   • NULL qiymatlar ko'p bo'lishi mumkin (JSHSHIR ixtiyoriy),
        //   • yumshoq o'chirilgan xodim qaytib kelsa, uning JSHSHIR'i
        //     yangi yozuvga berilishi mumkin bo'lishi kerak.
        // PostgreSQL'da NULL'lar unikal indeksda bir-biriga xalaqit bermaydi,
        // lekin filtr ataylab aniq yozilgan — o'qiganda niyat ko'rinib tursin.
        builder.HasIndex(employee => employee.Pinfl)
            .IsUnique()
            .HasFilter("pinfl IS NOT NULL AND deleted_at IS NULL");

        // Pasport UNIKAL EMAS: Excel'da gomoglif tufayli bir odam ikki xil
        // yozilgan holatlar bor va ular tuzatilgunicha ikkala yozuv yashaydi.
        // Indeks faqat qidiruv uchun («pasport bo'yicha topish» — JSHSHIR
        // bo'lmaganda ishlatiladigan zaxira yo'l).
        builder.HasIndex(employee => employee.Passport)
            .HasFilter("passport IS NOT NULL");

        // Telefon bo'yicha qidiruv — HR eng ko'p ishlatadigan ikkinchi kalit
        builder.HasIndex(employee => employee.Phone)
            .HasFilter("phone IS NOT NULL");

        // Yo'nalish bo'yicha filtr: «Ушбу йўналиш ходимлари».
        // GIN kerak — oddiy B-tree indeks massiv operatorlarida ishlamaydi.
        builder.HasIndex(employee => employee.DirectionIds)
            .HasMethod("gin");

        // «Tuzatish kerak» ro'yxati — ETL dan keyingi asosiy ish ekrani.
        // Qisman indeks: 540 yozuvdan bir necha o'ntasi belgilanadi, shu
        // sababli to'liq indeks o'rniga faqat `true` qatorlar saqlanadi.
        builder.HasIndex(employee => employee.NeedsReview)
            .HasFilter("needs_review");

        // DIQQAT: `(last_name, first_name)` va trigramma indekslari BU YERDA
        // YO'Q — ular migratsiyada xom SQL bilan yaratiladi.
        // Sabab: ikkalasi ham complex type ustunlari va ifoda ustidagi
        // indekslar, EF fluent API ularni ifodalay olmaydi.
    }
}
