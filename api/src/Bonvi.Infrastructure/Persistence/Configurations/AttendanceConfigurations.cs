using Bonvi.Domain.Attendance;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/*
  ════════════════════════════════════════════════════════════════════
   DAVOMAT MODULINING JADVAL KONFIGURATSIYASI — yettita jadval

   Ikkita umumiy qaror butun fayl bo'ylab amal qiladi.

   1. INDEKS NOMLARI QO'LDA berilgan (`HasDatabaseName`). Boshqa
      modullarda ular konvensiyadan kelib chiqadi, bu yerda esa
      SPEC ularni nomi bilan sanab chiqqan (`ix_attendance_passes_pending`
      va h.k.) va joriy qilish tekshiruvi aynan o'sha nomlarni
      `\d+` bilan qidiradi. Konvensiya bergan nom
      (`ix_attendance_passes_resolved_employee_id_occurred_at`) bir xil
      indeksni yasardi, lekin tekshirish uchun o'zga nom bo'lardi.

   2. USTUN SUKUT QIYMATLARI (`HasDefaultValue`) QO'YILMAYDI. Sabab
      ikkita: (a) butun loyihada ular hech qayerda ishlatilmagan —
      sukut qiymat domen fabrikasida turadi; (b) EF `bool` va `int`
      ustunda CLR sukutini «qiymat berilmadi» deb hisoblaydi, ya'ni
      `HasDefaultValue(true)` qo'yilgan `is_active` ustuniga ataylab
      yozilgan `false` JIMGINA `true` bo'lib tushardi. Qator har doim
      domen bergan qiymat bilan yoziladi.
  ════════════════════════════════════════════════════════════════════
*/

/// <summary>
/// <see cref="AttendanceDevice"/> uchun jadval konfiguratsiyasi
/// (<c>attendance_devices</c>).
/// </summary>
/// <remarks>
/// Tashqi kalitlar <c>Restrict</c> — <c>EmploymentConfiguration</c> dagi
/// bilan bir xil sabab: ma'lumotnomadagi yozuv o'chirilganda uning
/// ortidan qurilma reyestri yo'q bo'lib ketishi mumkin emas.
/// </remarks>
public sealed class AttendanceDeviceConfiguration : IEntityTypeConfiguration<AttendanceDevice>
{
    public void Configure(EntityTypeBuilder<AttendanceDevice> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("attendance_devices");

        builder.HasKey(device => device.Id);

        builder.Ignore(device => device.DomainEvents);

        // Hisoblanadigan xossalar — ustun emas (izohi domenda).
        builder.Ignore(device => device.HasClockDrift);
        builder.Ignore(device => device.EventsRejected);
        builder.Ignore(device => device.IsSynthetic);

        builder.Property(device => device.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(device => device.SerialNumber)
            .HasMaxLength(100);

        builder.Property(device => device.Model)
            .HasMaxLength(100);

        builder.Property(device => device.Provider)
            .IsRequired();

        builder.Property(device => device.IdentityProvider)
            .IsRequired();

        builder.Property(device => device.DirectionRole)
            .IsRequired();

        builder.Property(device => device.AuthMode)
            .IsRequired();

        builder.Property(device => device.LocationNote)
            .HasMaxLength(200);

        // `bytea` uzunlik olmaydi — 32 baytlik cheklov domenda
        // (`AttendanceDevice.TokenHashLength`).
        builder.Property(device => device.TokenHash)
            .IsRequired();

        builder.Property(device => device.TokenPrefix)
            .HasMaxLength(AttendanceDevice.TokenPrefixLength)
            .IsRequired();

        builder.Property(device => device.TokenIssuedAt)
            .IsRequired();

        // Shifrlangan sir — shifrlash matnni uzaytiradi, shu sababli
        // chegara sirning o'zidan ancha keng (`bot.token` dagi bilan bir
        // xil mulohaza).
        builder.Property(device => device.HmacSecret)
            .HasMaxLength(500);

        builder.Property(device => device.AllowedIps)
            .HasMaxLength(500);

        builder.Property(device => device.IsActive)
            .IsRequired();

        // 45 — IPv6 ning eng uzun matn ko'rinishi (IPv4 ko'rinishi bilan).
        builder.Property(device => device.LastSeenIp)
            .HasMaxLength(45);

        // Agent o'qigan seriya (SPEC-agent §L) — HR kiritadigan
        // `serial_number` dan alohida, sababi domenda.
        builder.Property(device => device.TerminalSerial)
            .HasMaxLength(AttendanceDevice.TerminalSerialMaxLength);

        builder.Property(device => device.HeldRemovals)
            .IsRequired();

        builder.Property(device => device.ForeignPersons)
            .IsRequired();

        builder.HasOne<LegalEntity>()
            .WithMany()
            .HasForeignKey(device => device.LegalEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(device => device.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // ────────────────────────────────────────────────────────────
        //  Indekslar
        // ────────────────────────────────────────────────────────────

        /*
          TOKEN XESHI — qabul yo'lidagi yagona qidiruv.

          ⚠️ Yumshoq o'chirish filtri ATAYLAB YO'Q. Qolgan unikal
          indekslarda `WHERE deleted_at IS NULL` bor, chunki o'chirilgan
          yozuvning NOMINI qayta ishlatish mumkin bo'lishi kerak. Token
          esa aksincha: o'chirilgan qurilmaning tokeni O'LIK bo'lib
          qolishi shart, ya'ni u bandligicha qolishi kerak.
        */
        builder.HasIndex(device => device.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_attendance_devices_token_hash");

        // `lower(name)` va `lower(serial_number)` bo'yicha unikal
        // indekslar EF fluent API bilan ifodalanmaydi — ular
        // migratsiyada xom SQL bilan yaratiladi (boshqa sakkizta
        // ma'lumotnomadagi bilan bir xil texnika).
    }
}

/// <summary>
/// <see cref="AttendanceIdentity"/> uchun jadval konfiguratsiyasi
/// (<c>attendance_identities</c>).
/// </summary>
public sealed class AttendanceIdentityConfiguration : IEntityTypeConfiguration<AttendanceIdentity>
{
    public void Configure(EntityTypeBuilder<AttendanceIdentity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("attendance_identities");

        builder.HasKey(identity => identity.Id);

        builder.Ignore(identity => identity.DomainEvents);

        builder.Property(identity => identity.Provider)
            .IsRequired();

        builder.Property(identity => identity.ExternalPersonId)
            .HasMaxLength(AttendanceIdentity.MaxExternalPersonIdLength)
            .IsRequired();

        builder.Property(identity => identity.Source)
            .IsRequired();

        builder.Property(identity => identity.DisplayNameInDevice)
            .HasMaxLength(200);

        builder.Property(identity => identity.Note)
            .HasMaxLength(500);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(identity => identity.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // ────────────────────────────────────────────────────────────
        //  Indekslar
        // ────────────────────────────────────────────────────────────

        /*
          Unikallik SHAXS RAQAMI bo'yicha, XODIM bo'yicha emas: bitta
          odamda bir nechta qurilma raqami bo'lishi mumkin, bitta raqam
          esa bir vaqtda ikki odamga tegishli bo'la olmaydi.

          `WHERE deleted_at IS NULL` — uzilgan moslik tarix bo'lib
          qoladi va o'sha raqamni boshqa xodimga bog'lashga to'sqinlik
          qilmasligi kerak (`UserConfiguration` dagi login indeksi bilan
          bir xil qoida).
        */
        builder.HasIndex(identity => new { identity.Provider, identity.ExternalPersonId })
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ux_attendance_identities_person");

        // «Shu xodimning qurilma raqamlari» — xodim kartasi va moslik ekrani.
        builder.HasIndex(identity => identity.EmployeeId)
            .HasDatabaseName("ix_attendance_identities_employee");

        /*
          Bonvi fazasida xodimga BITTA raqam (SPEC-agent §E: «raqam hech
          qachon o'zgarmaydi»). Ikki agent so'rovi bir vaqtda raqam
          ajratsa, ikkinchisi shu indeksga uriladi va 409 oladi
          (`AttendanceErrors.AgentRosterBusy`) — ikkinchi raqam yozilmaydi.
          Filtr `DevicePersonNo.IsManaged` bilan AYNAN bir xil: 9 belgi,
          birinchisi «9». Nom `PostgresFault.ManagedNumberIndex` da ham.
        */
        builder.HasIndex(identity => new { identity.Provider, identity.EmployeeId })
            .IsUnique()
            .HasFilter("deleted_at IS NULL AND length(external_person_id) = 9 AND left(external_person_id, 1) = '9'")
            .HasDatabaseName("ux_attendance_identities_managed_employee");
    }
}

/// <summary>
/// <see cref="AttendancePass"/> uchun jadval konfiguratsiyasi
/// (<c>attendance_passes</c>).
/// </summary>
/// <remarks>
/// <para>
/// Yumshoq o'chirish ham, audit ustunlari ham YO'Q — sababi domenda.
/// </para>
/// <para>
/// Yiliga ~2 mln qator. Oltita indeksdan uchtasi QISMAN: navbat, tozalash
/// va bog'lanmaganlar ro'yxati — uchalasi ham amalda kichik to'plam, to'liq
/// indeks esa millionlab kerak bo'lmagan qatorni saqlab yurardi.
/// </para>
/// </remarks>
public sealed class AttendancePassConfiguration : IEntityTypeConfiguration<AttendancePass>
{
    public void Configure(EntityTypeBuilder<AttendancePass> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("attendance_passes");

        builder.HasKey(pass => pass.Id);

        builder.Ignore(pass => pass.DomainEvents);

        builder.Ignore(pass => pass.HasClockDrift);
        builder.Ignore(pass => pass.EffectiveClockSkewSeconds);

        builder.Property(pass => pass.IdentityProvider)
            .IsRequired();

        builder.Property(pass => pass.RawPersonId)
            .HasMaxLength(AttendanceIdentity.MaxExternalPersonIdLength)
            .IsRequired();

        builder.Property(pass => pass.OccurredAt)
            .IsRequired();

        /*
          QURILMA YOZGAN MAHALLIY VAQT — siljishsiz.

          Ustun turi ATAYLAB aniq ko'rsatilgan: Npgsql `DateTime` ni
          sukut bo'yicha `timestamptz` ga map qiladi, bu yerda esa
          aynan siljishsiz qiymat kerak — u qurilma nima yozganini
          ko'rsatadi, lahzani emas. Ikkalasi bir jadvalda turgani
          ataylab: qurilma soati noto'g'ri bo'lsa, farq shu ikki ustun
          orasida ko'rinadi.
        */
        builder.Property(pass => pass.DeviceLocalTime)
            .HasColumnType("timestamp without time zone")
            .IsRequired();

        builder.Property(pass => pass.DeviceOffsetMinutes)
            .IsRequired();

        builder.Property(pass => pass.OffsetAssumed)
            .IsRequired();

        builder.Property(pass => pass.LocalDate)
            .IsRequired();

        builder.Property(pass => pass.ReceivedAt)
            .IsRequired();

        builder.Property(pass => pass.ClockSkewSeconds)
            .IsRequired();

        builder.Property(pass => pass.Direction)
            .IsRequired();

        builder.Property(pass => pass.DirectionSource)
            .IsRequired();

        builder.Property(pass => pass.Method)
            .IsRequired();

        builder.Property(pass => pass.PhotoKey)
            .HasMaxLength(200);

        // Xom hodisa — `jsonb`, `text` emas: nosozlikni tekshirishda
        // undan maydon ajratib olish kerak bo'ladi («qurilma nima
        // yubordi?»), `text` da esa har safar qo'lda tahlil qilishga
        // to'g'ri kelardi. Indeks qo'yilmagan: u qidirilmaydi, o'qiladi.
        builder.Property(pass => pass.RawEvent)
            .HasColumnType("jsonb");

        builder.Property(pass => pass.DedupeKey)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasOne<AttendanceDevice>()
            .WithMany()
            .HasForeignKey(pass => pass.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Xodim o'chirilsa (yumshoq), o'tishlar qolishi kerak: ular
        // mashina yozuvi va tarixiy dalil.
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(pass => pass.ResolvedEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // ────────────────────────────────────────────────────────────
        //  Indekslar
        // ────────────────────────────────────────────────────────────

        /*
          TAKRORNI TO'SUVCHI INDEKS — modulning eng muhim indeksi.

          Qurilmalar so'rovni qayta yuboradi va takror ZARARSIZ bo'lishi
          kerak, shunchaki «chidalgan» emas. Yozish yo'li: INSERT qilamiz
          va aynan shu indeks bergan unikal cheklov xatosini «takror»
          deb o'qiymiz. Rad etilgan muqobil — INSERT dan oldin SELECT:
          u poygani (race) oldini olmaydi, faqat odatda yashiradi, va
          modulning eng issiq yo'liga ortiqcha murojaat qo'shadi.
        */
        builder.HasIndex(pass => new { pass.DeviceId, pass.DedupeKey })
            .IsUnique()
            .HasDatabaseName("ux_attendance_passes_device_dedupe");

        // «Hozir binoda» taxtasi va xodim bo'yicha jurnal.
        builder.HasIndex(pass => new { pass.ResolvedEmployeeId, pass.OccurredAt })
            .HasDatabaseName("ix_attendance_passes_employee_time");

        // Kun bo'yicha jurnal va qayta hisoblash.
        builder.HasIndex(pass => pass.LocalDate)
            .HasDatabaseName("ix_attendance_passes_local_date");

        // Qurilma kartasi: «shu terminaldan nima o'tdi».
        builder.HasIndex(pass => new { pass.DeviceId, pass.OccurredAt })
            .HasDatabaseName("ix_attendance_passes_device_time");

        // Bog'lanmaganlar ekrani va moslik kiritilgandan keyingi
        // to'ldirish. Qisman: mosligi bor o'tishlar bu indeksga
        // umuman tushmaydi va ular jadvalning deyarli hammasi.
        builder.HasIndex(pass => new { pass.IdentityProvider, pass.RawPersonId })
            .HasFilter("resolved_employee_id IS NULL")
            .HasDatabaseName("ix_attendance_passes_unmatched");

        // Qayta hisoblash navbati. Qisman indeks — kichkina bo'lib
        // qoladi: navbatda o'nlab qator turadi, jadvalda esa millionlab.
        builder.HasIndex(pass => pass.ProcessedAt)
            .HasFilter("processed_at IS NULL")
            .HasDatabaseName("ix_attendance_passes_pending");

        // Tozalash vazifasining ko'zdan kechirishi: «muddati o'tgan,
        // surati hamon turgan o'tishlar».
        builder.HasIndex(pass => pass.ReceivedAt)
            .HasFilter("photo_key IS NOT NULL")
            .HasDatabaseName("ix_attendance_passes_photo");
    }
}

/// <summary>
/// <see cref="WorkSchedule"/> uchun jadval konfiguratsiyasi
/// (<c>work_schedules</c>).
/// </summary>
public sealed class WorkScheduleConfiguration : IEntityTypeConfiguration<WorkSchedule>
{
    public void Configure(EntityTypeBuilder<WorkSchedule> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("work_schedules");

        builder.HasKey(schedule => schedule.Id);

        builder.Ignore(schedule => schedule.DomainEvents);

        builder.Property(schedule => schedule.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(schedule => schedule.Kind)
            .IsRequired();

        builder.Property(schedule => schedule.GraceInMinutes)
            .IsRequired();

        builder.Property(schedule => schedule.GraceOutMinutes)
            .IsRequired();

        builder.Property(schedule => schedule.IsActive)
            .IsRequired();

        builder.Property(schedule => schedule.Note)
            .HasMaxLength(500);

        // ────────────────────────────────────────────────────────────
        //  Kunlar — jadval agregatining ICHIDA
        // ────────────────────────────────────────────────────────────

        // Kun jadvalsiz ma'noga ega emas va o'z repozitoriysi yo'q, shu
        // sababli kaskad o'chirish (`Employment.Events` bilan bir xil).
        builder.HasMany(schedule => schedule.Days)
            .WithOne()
            .HasForeignKey(day => day.WorkScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        // `Days` — faqat o'qish uchun xossa, unga yozib bo'lmaydi.
        builder.Navigation(schedule => schedule.Days)
            .HasField("_days")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // `lower(name)` bo'yicha unikal indeks migratsiyada, xom SQL bilan.
    }
}

/// <summary>
/// <see cref="WorkScheduleDay"/> uchun jadval konfiguratsiyasi
/// (<c>work_schedule_days</c>).
/// </summary>
/// <remarks>
/// Yumshoq o'chirish ham, audit ustunlari ham YO'Q: kun jadval bilan
/// birga yashaydi va faqat u orqali o'qiladi —
/// <c>EmploymentEvent</c> dagi bilan bir xil qaror.
/// </remarks>
public sealed class WorkScheduleDayConfiguration : IEntityTypeConfiguration<WorkScheduleDay>
{
    public void Configure(EntityTypeBuilder<WorkScheduleDay> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        /*
          TANAFFUS OYNASINING CHEKLOVI — bazada ham, domenda ham.

          Domen `WorkScheduleDay.Validate` da uchala qoidani tekshiradi,
          lekin qoida BAZADA ham turishi kerak: domen tekshiruvi kod
          orqali kelmagan yozuvni (migratsiya, qo'lda SQL) ushlay
          olmaydi — `CandidateQuestionnaire` dagi unikal indeks bilan
          bir xil mulohaza.

          Uchta ifodalanadigan qoida shu yerda:
            • ikkala chekka ham bo'sh, yoki ikkalasi ham to'ldirilgan
              — yarim oyna hech narsani ifodalamaydi;
            • oyna faqat ISH KUNIDA bo'lishi mumkin;
            • oynaning uzunligi noldan katta.

          To'rtinchi qoida — «oyna smena ichida yotadi» — bu yerda YO'Q
          va ataylab: tungi smenada uni tekshirish yarim tundan o'tuvchi
          o'qni talab qiladi (`CrossesMidnight`), ya'ni SQL ifodasi
          o'qib bo'lmaydigan darajada uzun bo'lardi. U faqat domenda.
        */
        builder.ToTable("work_schedule_days", table => table.HasCheckConstraint(
            "ck_work_schedule_days_break_window",
            """
            (break_start IS NULL AND break_end IS NULL)
            OR (is_working
                AND break_start IS NOT NULL
                AND break_end IS NOT NULL
                AND break_start <> break_end)
            """));

        builder.HasKey(day => day.Id);

        builder.Ignore(day => day.DomainEvents);

        // Hisoblanadigan xossa — ustun EMAS. Sababi domenda: saqlangan
        // bayroq o'zi tasvirlayotgan ikki vaqtga zid bo'lib qolishi mumkin.
        builder.Ignore(day => day.CrossesMidnight);

        builder.Property(day => day.DayIndex)
            .IsRequired();

        builder.Property(day => day.IsWorking)
            .IsRequired();

        builder.Property(day => day.BreakMinutes)
            .IsRequired();

        // ────────────────────────────────────────────────────────────
        //  Indekslar
        // ────────────────────────────────────────────────────────────

        // Bitta jadvalda bitta kun ikki marta bo'lishi mumkin emas.
        // Kompozit indeks tashqi kalit indeksining o'rnini ham bosadi.
        builder.HasIndex(day => new { day.WorkScheduleId, day.DayIndex })
            .IsUnique()
            .HasDatabaseName("ux_work_schedule_days_day");
    }
}

/// <summary>
/// <see cref="ScheduleAssignment"/> uchun jadval konfiguratsiyasi
/// (<c>schedule_assignments</c>).
/// </summary>
public sealed class ScheduleAssignmentConfiguration : IEntityTypeConfiguration<ScheduleAssignment>
{
    public void Configure(EntityTypeBuilder<ScheduleAssignment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        /*
          POG'ONA VA OBYEKT MOSLIGI.

          Har bir pog'onada AYNAN o'ziga tegishli ustun to'ldirilgan
          bo'lishi shart, qolgan ikkitasi bo'sh. Sonlar —
          `ScheduleScope`: 1 xodim, 2 lavozim, 3 bo'lim, 4 sukut.

          Domen buni `ScheduleAssignment.Assign` da tekshiradi; cheklov
          bazada turgani uchun qo'lda yozilgan SQL ham noto'g'ri qator
          qo'sha olmaydi.
        */
        builder.ToTable("schedule_assignments", table => table.HasCheckConstraint(
            "ck_schedule_assignments_scope",
            """
            (scope = 1 AND employee_id IS NOT NULL AND position_id IS NULL AND department_id IS NULL) OR
            (scope = 2 AND position_id IS NOT NULL AND employee_id IS NULL AND department_id IS NULL) OR
            (scope = 3 AND department_id IS NOT NULL AND employee_id IS NULL AND position_id IS NULL) OR
            (scope = 4 AND employee_id IS NULL AND position_id IS NULL AND department_id IS NULL)
            """));

        builder.HasKey(assignment => assignment.Id);

        builder.Ignore(assignment => assignment.DomainEvents);

        // Pog'onadan kelib chiqadi — ustun emas.
        builder.Ignore(assignment => assignment.TargetId);

        builder.Property(assignment => assignment.Scope)
            .IsRequired();

        builder.Property(assignment => assignment.EffectiveFrom)
            .IsRequired();

        builder.HasOne<WorkSchedule>()
            .WithMany()
            .HasForeignKey(assignment => assignment.WorkScheduleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(assignment => assignment.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Position>()
            .WithMany()
            .HasForeignKey(assignment => assignment.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(assignment => assignment.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // ────────────────────────────────────────────────────────────
        //  Indekslar
        // ────────────────────────────────────────────────────────────

        // Hal qiluvchining asosiy so'rovi: «shu sanada kuchda bo'lgan
        // biriktirmalar, pog'ona bo'yicha».
        builder.HasIndex(assignment => new { assignment.Scope, assignment.EffectiveFrom })
            .HasDatabaseName("ix_schedule_assignments_lookup");

        // Takrorni to'suvchi unikal indeks (`NULLS NOT DISTINCT`)
        // migratsiyada, xom SQL bilan: EF fluent API bu sintaksisni
        // ifodalay olmaydi.
    }
}

/// <summary>
/// <see cref="AttendanceDay"/> uchun jadval konfiguratsiyasi
/// (<c>attendance_days</c>).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Firma, bo'lim va lavozimda tashqi kalit YO'Q va bu ataylab.</b>
/// Ular SURAT: hisoblash lahzasidagi holatni yozib qo'yadi va xodim
/// keyin boshqa bo'limga o'tsa ham o'zgarmasligi kerak. Tashqi kalit
/// bo'lganida ular «hozirgi haqiqat» degan va'da berardi, holbuki ular
/// ataylab o'tgan zamon. Xodim, mehnat munosabati va jadval esa
/// haqiqiy havolalar — ularda kalit bor.
/// </para>
/// </remarks>
public sealed class AttendanceDayConfiguration : IEntityTypeConfiguration<AttendanceDay>
{
    public void Configure(EntityTypeBuilder<AttendanceDay> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("attendance_days");

        builder.HasKey(day => day.Id);

        builder.Ignore(day => day.DomainEvents);

        builder.Property(day => day.LocalDate)
            .IsRequired();

        builder.Property(day => day.PlannedMinutes)
            .IsRequired();

        builder.Property(day => day.WorkedMinutes)
            .IsRequired();

        builder.Property(day => day.LateMinutes)
            .IsRequired();

        builder.Property(day => day.EarlyLeaveMinutes)
            .IsRequired();

        builder.Property(day => day.OvertimeMinutes)
            .IsRequired();

        builder.Property(day => day.BreakTakenMinutes)
            .IsRequired();

        builder.Property(day => day.BreakDeductionMinutes)
            .IsRequired();

        builder.Property(day => day.PassCount)
            .IsRequired();

        builder.Property(day => day.IgnoredPassCount)
            .IsRequired();

        builder.Property(day => day.Status)
            .IsRequired();

        // Bazadagi sukut 0 faqat migratsiyada (mavjud qatorlar so'rovsiz
        // hisoblangan; eski image rollback'da ustunni bilmay yozadi) — 2-qoida.
        builder.Property(day => day.CreditedMinutes)
            .IsRequired();

        builder.Property(day => day.ComputedAt)
            .IsRequired();

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(day => day.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employment>()
            .WithMany()
            .HasForeignKey(day => day.EmploymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WorkSchedule>()
            .WithMany()
            .HasForeignKey(day => day.WorkScheduleId)
            .OnDelete(DeleteBehavior.Restrict);

        // ────────────────────────────────────────────────────────────
        //  Indekslar
        // ────────────────────────────────────────────────────────────

        // Bir xodimda bir kun uchun BITTA yozuv. Qayta hisoblash ham shu
        // kalit bo'yicha topadi va yangilaydi.
        builder.HasIndex(day => new { day.EmployeeId, day.LocalDate })
            .IsUnique()
            .HasDatabaseName("ux_attendance_days_employee_date");

        // «Bugun kim kelmadi», «shu oyda kechikkanlar» — hisobotlar.
        builder.HasIndex(day => new { day.LocalDate, day.Status })
            .HasDatabaseName("ix_attendance_days_date_status");

        // Bo'lim va firma kesimidagi tabel — surat ustunlari bo'yicha.
        builder.HasIndex(day => new { day.DepartmentId, day.LocalDate })
            .HasDatabaseName("ix_attendance_days_department_date");

        builder.HasIndex(day => new { day.LegalEntityId, day.LocalDate })
            .HasDatabaseName("ix_attendance_days_legal_entity_date");
    }
}

/// <summary>
/// <see cref="AbsenceRequest"/> uchun jadval konfiguratsiyasi
/// (<c>absence_requests</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Indeks (xodim, davr).</b> Ikkita so'rov bor: xodim kartasidagi ro'yxat
/// (xodim bo'yicha) va kun hisoblash paytidagi «shu sanani qamragan
/// tasdiqlangan so'rov bormi» (xodim + sana). Ikkalasi ham
/// <c>(employee_id, from_date, to_date)</c> indeksiga tushadi.
/// </para>
/// <para>
/// <b>Holat bo'yicha alohida indeks.</b> Umumiy navbat ekrani «kutayotgan»
/// so'rovlarni butun tashkilot bo'ylab so'raydi va nav nishonidagi sanoq
/// ham shu so'rovdan chiqadi — bu eng tez-tez takrorlanadigan so'rov.
/// </para>
/// </remarks>
public sealed class AbsenceRequestConfiguration : IEntityTypeConfiguration<AbsenceRequest>
{
    public void Configure(EntityTypeBuilder<AbsenceRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("absence_requests");

        builder.HasKey(request => request.Id);

        builder.Ignore(request => request.DomainEvents);
        builder.Ignore(request => request.IsWholeDay);
        builder.Ignore(request => request.IsDecided);

        builder.Property(request => request.FromDate).IsRequired();
        builder.Property(request => request.ToDate).IsRequired();
        builder.Property(request => request.Status).IsRequired();

        builder.Property(request => request.Note).HasMaxLength(2000);
        builder.Property(request => request.DecisionNote).HasMaxLength(2000);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(request => request.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AbsenceType>()
            .WithMany()
            .HasForeignKey(request => request.AbsenceTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(request => new { request.EmployeeId, request.FromDate, request.ToDate });
        builder.HasIndex(request => request.Status);

        builder.HasQueryFilter(request => request.DeletedAt == null);
    }
}

/// <summary>
/// <see cref="WorkScheduleDate"/> uchun jadval konfiguratsiyasi
/// (<c>work_schedule_dates</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Unikal kalit — (jadval, sana).</b> Bitta sanada ikkita qiymat
/// bo'lishi mumkin emas: hisoblash «qaysi biri?» degan savolni berishi
/// kerak emas. Hisoblash yo'li ham aynan shu indeksdan o'tadi (oraliq
/// bo'yicha o'qish).
/// </para>
/// <para>
/// Yumshoq o'chirish YO'Q: katak o'chirilsa, kun shunchaki haftalik
/// naqshga qaytadi — saqlab turadigan tarix emas. Jadvalning o'zi
/// o'chirilsa, kataklari ham ketadi (<c>Cascade</c>).
/// </para>
/// </remarks>
public sealed class WorkScheduleDateConfiguration : IEntityTypeConfiguration<WorkScheduleDate>
{
    public void Configure(EntityTypeBuilder<WorkScheduleDate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("work_schedule_dates");

        builder.HasKey(date => date.Id);

        builder.Ignore(date => date.DomainEvents);

        builder.Property(date => date.Date).IsRequired();
        builder.Property(date => date.IsWorking).IsRequired();
        builder.Property(date => date.BreakMinutes).IsRequired();

        builder.HasOne<WorkSchedule>()
            .WithMany()
            .HasForeignKey(date => date.WorkScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(date => new { date.WorkScheduleId, date.Date })
            .IsUnique()
            .HasDatabaseName("ux_work_schedule_dates_schedule_date");
    }
}

/// <summary>
/// <see cref="DeviceEnrollment"/> uchun jadval konfiguratsiyasi
/// (<c>device_enrollments</c>).
/// </summary>
/// <remarks>
/// <para>
/// Kalit — (qurilma, xodim): bir xodim bir qurilmada bir marta bo'ladi.
/// Tekshiruv ham aynan shu bo'yicha o'qiydi.
/// </para>
/// <para>
/// Yumshoq o'chirish YO'Q: yozuv o'chirilsa, holat shunchaki «hech qachon
/// yozilmagan» ga qaytadi va keyingi tekshiruvda qayta hosil bo'ladi.
/// Qurilma o'chirilsa, holati ham ketadi (<c>Cascade</c>); xodimda esa
/// <c>Restrict</c> — xodim yozuvi baribir yumshoq o'chiriladi.
/// </para>
/// </remarks>
public sealed class DeviceEnrollmentConfiguration : IEntityTypeConfiguration<DeviceEnrollment>
{
    public void Configure(EntityTypeBuilder<DeviceEnrollment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("device_enrollments");

        builder.HasKey(row => row.Id);

        builder.Ignore(row => row.DomainEvents);

        builder.Property(row => row.Status).IsRequired();
        builder.Property(row => row.ForceVersion).IsRequired();
        builder.Property(row => row.AppliedVersion).HasMaxLength(200);
        builder.Property(row => row.RejectedVersion).HasMaxLength(200);
        builder.Property(row => row.Error).HasMaxLength(500);

        builder.HasOne<AttendanceDevice>()
            .WithMany()
            .HasForeignKey(row => row.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(row => row.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(row => new { row.DeviceId, row.EmployeeId })
            .IsUnique()
            .HasDatabaseName("ux_device_enrollments_device_employee");
    }
}
