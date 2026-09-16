using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="PipelineStage"/> uchun jadval konfiguratsiyasi (<c>pipeline_stages</c>).
/// </summary>
/// <remarks>
/// Umumiy ma'lumotnoma sozlamalari <c>ConfigureReference</c> dan keladi
/// (nom, faollik, o'zbek kolatsiyasi). Farq bittada: bu ma'lumotnomada
/// TARTIB bor va u asosiy saralash kaliti — sababi
/// <see cref="PipelineStage.SortOrder"/> izohida.
/// </remarks>
public sealed class PipelineStageConfiguration : IEntityTypeConfiguration<PipelineStage>
{
    public void Configure(EntityTypeBuilder<PipelineStage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureReference("pipeline_stages");

        builder.Property(stage => stage.SortOrder)
            .IsRequired();

        builder.Property(stage => stage.InvitesToInterview)
            .IsRequired();

        // Voronkaning ASOSIY so'rovi: «faol bosqichlarni TARTIB bo'yicha
        // ber». `ConfigureReference` qo'yadigan (is_active, name) indeksi
        // bunga yaramaydi — u alifbo uchun. Ikkalasi ham qoladi: birinchisi
        // ma'lumotnomani boshqarish sahifasiga, bu esa voronkaga.
        builder.HasIndex(stage => new { stage.IsActive, stage.SortOrder });
    }
}

/// <summary>
/// <see cref="VacancyTitle"/> uchun jadval konfiguratsiyasi (<c>vacancy_titles</c>).
/// </summary>
/// <remarks>
/// Butunlay odatiy ma'lumotnoma: <c>ConfigureReference</c> dan boshqa hech
/// narsa kerak emas. <see cref="PipelineStageConfiguration"/> ga
/// qo'shimcha indeks kerak edi (voronkadagi tartib uchun), bu yerda esa
/// yagona so'rov «faol nomlarni alifbo bo'yicha ber» — uni asosdagi
/// <c>(is_active, name)</c> indeksi to'liq qoplaydi.
/// </remarks>
public sealed class VacancyTitleConfiguration : IEntityTypeConfiguration<VacancyTitle>
{
    public void Configure(EntityTypeBuilder<VacancyTitle> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureReference("vacancy_titles");
    }
}

/// <summary>
/// <see cref="Vacancy"/> uchun jadval konfiguratsiyasi (<c>vacancies</c>).
/// </summary>
/// <remarks>
/// <para>
/// Tashqi kalitlar <c>Restrict</c> — <c>EmploymentConfiguration</c>
/// dagi bilan bir xil sabab: ma'lumotnomadagi yozuv o'chirilganda uning
/// ortidan vakansiya tarixi yo'q bo'lib ketishi mumkin emas.
/// </para>
/// <para>
/// <b>Lavozim, bo'lim va firma ustunlari YO'Q.</b> Ular vakansiyadan
/// butunlay olib tashlandi (sababi <see cref="Vacancy"/> izohida), shu
/// bilan birga ularning tashqi kalitlari va indekslari ham. Jadvalda
/// ma'lumotnomaga yagona havola qoldi — <c>vacancy_title_id</c>.
/// </para>
/// </remarks>
public sealed class VacancyConfiguration : IEntityTypeConfiguration<Vacancy>
{
    public void Configure(EntityTypeBuilder<Vacancy> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("vacancies");

        builder.HasKey(vacancy => vacancy.Id);

        builder.Ignore(vacancy => vacancy.DomainEvents);

        builder.Property(vacancy => vacancy.Openings)
            .IsRequired();

        builder.Property(vacancy => vacancy.Status)
            .IsRequired();

        builder.Property(vacancy => vacancy.OpenedAt)
            .IsRequired();

        builder.Property(vacancy => vacancy.Note)
            .HasMaxLength(2000);

        builder.HasOne<VacancyTitle>()
            .WithMany()
            .HasForeignKey(vacancy => vacancy.VacancyTitleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Mas'ul xodim (rekruter) — u keyinchalik ishdan ketsa ham, kim
        // mas'ul bo'lgani ma'lum qolishi kerak.
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(vacancy => vacancy.ResponsibleEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // ────────────────────────────────────────────────────────────
        //  Indekslar
        // ────────────────────────────────────────────────────────────

        // Asosiy ekran: «ochiq vakansiyalar, yangidan eskiga». Kompozit
        // indeks saralashni ham qoplaydi.
        builder.HasIndex(vacancy => new { vacancy.Status, vacancy.OpenedAt });

        // «Shu nom bo'yicha ochiq vakansiya bormi» (takror tekshiruvi) va
        // nom bo'yicha filtr — ro'yxatning yagona qolgan filtri.
        builder.HasIndex(vacancy => vacancy.VacancyTitleId);
    }
}

/// <summary>
/// <see cref="Candidate"/> uchun jadval konfiguratsiyasi (<c>candidates</c>).
/// </summary>
/// <remarks>
/// <para>
/// F.I.Sh. va telefon <c>employees</c> jadvalidagi bilan AYNAN bir xil
/// shaklda saqlanadi (uchta ustun + kanonik telefon satri). Bu majburiy:
/// qidiruv ikkala jadvalda bitta yordamchidan (<c>SearchFolding</c>)
/// foydalanadi va ishga qabulda qiymatlar to'g'ridan-to'g'ri ko'chadi.
/// </para>
/// </remarks>
public sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    /// <summary>
    /// Telefon qiymat obyekti uchun taqqoslagich.
    /// </summary>
    /// <remarks>
    /// Sabab <c>EmployeeConfiguration</c> dagi bilan bir xil: konvertordan
    /// keyin EF havola bo'yicha taqqoslab, bir xil raqamdan yasalgan ikki
    /// obyektni «har xil» deb hisoblardi va har saqlashda keraksiz UPDATE
    /// yuborardi.
    /// </remarks>
    private static readonly ValueComparer<PhoneNumber> PhoneComparer = new(
        (left, right) => left!.Value == right!.Value,
        phone => StringComparer.Ordinal.GetHashCode(phone.Value),
        phone => PhoneNumber.FromTrusted(phone.Value));

    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("candidates");

        builder.HasKey(candidate => candidate.Id);

        builder.Ignore(candidate => candidate.DomainEvents);

        // FIO — uchta ustunga. Complex type (OwnsOne emas) va ustun nomlari
        // qo'lda: `EmployeeConfiguration` dagi bilan AYNAN bir xil qaror,
        // shu sababli qidiruv ifodasi ham ikkala jadvalda bir xil bo'ladi.
        builder.ComplexProperty(candidate => candidate.FullName, fullName =>
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

        // Konvertorda `!` ataylab: EF konvertorga HECH QACHON `null`
        // uzatmaydi — NULL ustun konvertorgacha ushlab qolinadi.
        builder.Property(candidate => candidate.Phone)
            .HasConversion(
                phone => phone!.Value,
                value => PhoneNumber.FromTrusted(value),
                PhoneComparer);

        builder.Property(candidate => candidate.Status)
            .IsRequired();

        builder.Property(candidate => candidate.Source)
            .IsRequired();

        builder.Property(candidate => candidate.Address)
            .HasMaxLength(500);

        builder.Property(candidate => candidate.Speciality)
            .HasMaxLength(500);

        builder.Property(candidate => candidate.Note)
            .HasMaxLength(2000);

        builder.Property(candidate => candidate.ClosingNote)
            .HasMaxLength(2000);

        // Til kodi: «uz-Cyrl», «uz-Latn», «ru» — eng uzuni 7 belgi.
        builder.Property(candidate => candidate.TelegramLanguage)
            .HasMaxLength(16);

        // ────────────────────────────────────────────────────────────
        //  Bosqichlar tarixi — nomzod agregatining ICHIDA
        // ────────────────────────────────────────────────────────────

        // Yozuvlar nomzodsiz ma'noga ega emas va o'z repozitoriysi yo'q,
        // shu sababli kaskad o'chirish (`Employment.Events` bilan bir xil).
        //
        // MAVJUD nomzodga yangi yozuv qo'shilganda EF uni to'g'ri INSERT
        // qilishini `AppDbContext` dagi `ChangeTracker.Tracked` ishlovchisi
        // ta'minlaydi — izohi o'sha yerda.
        builder.HasMany(candidate => candidate.StageEntries)
            .WithOne()
            .HasForeignKey(entry => entry.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        // `StageEntries` — faqat o'qish uchun xossa, unga yozib bo'lmaydi.
        builder.Navigation(candidate => candidate.StageEntries)
            .HasField("_stageEntries")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ────────────────────────────────────────────────────────────
        //  Fayllar va izohlar — ikkalasi ham nomzod agregatining ICHIDA
        // ────────────────────────────────────────────────────────────

        // Bosqichlar tarixi bilan AYNAN bir xil sozlash va bir xil sabab:
        // yozuv nomzodsiz ma'noga ega emas, o'z repozitoriysi yo'q va
        // nomzod bilan birga o'chadi. Fayllarda buning ustiga soni va
        // umumiy hajmi nomzodning invarianti — sababi `CandidateFile`
        // izohida.
        builder.HasMany(candidate => candidate.Files)
            .WithOne()
            .HasForeignKey(file => file.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(candidate => candidate.Files)
            .HasField("_files")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(candidate => candidate.Notes)
            .WithOne()
            .HasForeignKey(note => note.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(candidate => candidate.Notes)
            .HasField("_notes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Rasm kaliti — `employees.photo_key` bilan bir xil uzunlik.
        builder.Property(candidate => candidate.PhotoKey)
            .HasMaxLength(256);

        // ────────────────────────────────────────────────────────────
        //  Anketa — nomzod agregatining ICHIDA, bittadan ko'p emas
        // ────────────────────────────────────────────────────────────

        // Bosqichlar tarixi bilan bir xil qaror: anketa nomzodsiz ma'noga
        // ega emas, o'z repozitoriysi yo'q va nomzod bilan birga o'chadi.
        builder.HasOne(candidate => candidate.Questionnaire)
            .WithOne()
            .HasForeignKey<CandidateQuestionnaire>(form => form.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        // ────────────────────────────────────────────────────────────
        //  Bog'lanishlar
        // ────────────────────────────────────────────────────────────

        builder.HasOne<Vacancy>()
            .WithMany()
            .HasForeignKey(candidate => candidate.VacancyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Joriy bosqich — ma'lumotnomaga havola. `Restrict`: bosqich
        // o'chirilmaydi, faqat faolsizlantiriladi, shu sababli bu cheklov
        // amalda ishlamaydi, lekin niyatni aniq ko'rsatadi.
        builder.HasOne<PipelineStage>()
            .WithMany()
            .HasForeignKey(candidate => candidate.CurrentStageId)
            .OnDelete(DeleteBehavior.Restrict);

        // ────────────────────────────────────────────────────────────
        //  Indekslar
        // ────────────────────────────────────────────────────────────

        // Voronka taxtasi: «shu bosqichda turgan FAOL nomzodlar».
        builder.HasIndex(candidate => new { candidate.CurrentStageId, candidate.Status });

        // «Shu vakansiyaning nomzodlari» — vakansiya kartasining asosiy
        // ro'yxati va sanoqlari.
        builder.HasIndex(candidate => new { candidate.VacancyId, candidate.Status });

        /*
          RAD ETISH XABARI NAVBATI.

          Bot davriy ravishda «rad etilgan, Telegram'i bor, lekin xabar
          hali yuborilmagan» nomzodlarni so'raydi. Bu so'rov har necha
          o'n soniyada takrorlanadi va odatda BO'SH javob qaytaradi —
          indekssiz u har safar butun jadvalni ko'zdan kechirardi.

          Qisman indeks (`WHERE`): navbatda bir vaqtda o'nlab yozuv
          bo'ladi, jadvalda esa minglab. To'liq indeks o'sha minglab
          yozuvni ham saqlab, hech qachon ishlatmasdi.
        */
        builder.HasIndex(candidate => new { candidate.Status, candidate.RejectionNoticeSentAt })
            .HasFilter("telegram_chat_id IS NOT NULL AND rejection_notice_sent_at IS NULL");

        /*
          SUHBAT TAKLIFI NAVBATI — yuqoridagining aynan egizagi.

          Farqi ikkita va ikkalasi ham qidiruv shartida: taklif nomzodning
          HOLATIGA bog'liq emas (u voronkada turganda yuboriladi), lekin
          suhbat vaqti QO'YILGAN bo'lishi shart. Shu sababli filtr
          `interview_at IS NOT NULL` dan boshlanadi.
        */
        builder.HasIndex(candidate => candidate.InterviewNoticeSentAt)
            .HasFilter(
                "telegram_chat_id IS NOT NULL AND interview_at IS NOT NULL "
                + "AND interview_notice_sent_at IS NULL");

        // Telefon bo'yicha qidiruv — nomzodda JSHSHIR yo'q, shu sababli bu
        // ikkinchi eng muhim kalit. Qisman indeks: raqamsiz yozuvlar bo'ladi.
        builder.HasIndex(candidate => candidate.Phone)
            .HasFilter("phone IS NOT NULL");

        /*
          «BUGUN BOG'LANISH KERAK» ro'yxati.

          Ro'yxat ekranidagi filtr (`next_contact_date <= bugun`) va shu
          filtr bo'yicha saralash. Qisman indeks — yuqoridagi telefon va
          navbat indekslari bilan bir xil sabab: nomzodlarning KO'PIDA
          aloqa sanasi yo'q, to'liq indeks esa minglab NULL yozuvni
          saqlab, ularni hech qachon o'qimasdi.
        */
        builder.HasIndex(candidate => candidate.NextContactDate)
            .HasFilter("next_contact_date IS NOT NULL");

        // «Botdan qancha nomzod keldi» — manba bo'yicha hisobot.
        builder.HasIndex(candidate => candidate.Source);
    }
}

/// <summary>
/// <see cref="CandidateStageEntry"/> uchun jadval konfiguratsiyasi
/// (<c>candidate_stage_entries</c>).
/// </summary>
/// <remarks>
/// Yumshoq o'chirish YO'Q: yozuv nomzod bilan birga yashaydi va nomzod
/// yumshoq o'chirilganda tarix ham ro'yxatlardan yo'qoladi (u nomzod orqali
/// o'qiladi). <c>EmploymentEvent</c> dagi bilan bir xil qaror.
/// </remarks>
public sealed class CandidateStageEntryConfiguration : IEntityTypeConfiguration<CandidateStageEntry>
{
    public void Configure(EntityTypeBuilder<CandidateStageEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("candidate_stage_entries");

        builder.HasKey(entry => entry.Id);

        builder.Ignore(entry => entry.DomainEvents);

        builder.Property(entry => entry.Date)
            .IsRequired();

        builder.Property(entry => entry.Outcome)
            .IsRequired();

        builder.Property(entry => entry.RecordedAt)
            .IsRequired();

        // Tuzatish vaqti — tegilmagan yozuvda NULL. Sukut qiymat YO'Q:
        // «hech qachon tahrirlanmagan» va «hozir tahrirlandi» ni ajratib
        // turadigan yagona narsa shu bo'shlik.
        builder.Property(entry => entry.UpdatedAt);

        // Xulosa — erkin matn. «Кенгаш хулосаси» ba'zan bir necha
        // abzatsdan iborat bo'ladi, shu sababli chegara keng.
        builder.Property(entry => entry.Conclusion)
            .HasMaxLength(4000);

        // Bosqichga tashqi kalit BOR (mehnat hodisasidagi lavozimdan
        // farqli): bosqich ma'lumotnomasidagi yozuv hech qachon
        // o'chirilmaydi, faqat faolsizlantiriladi — ya'ni cheklov tarixni
        // hech qachon to'smaydi, lekin noto'g'ri identifikatorni to'sadi.
        builder.HasOne<PipelineStage>()
            .WithMany()
            .HasForeignKey(entry => entry.StageId)
            .OnDelete(DeleteBehavior.Restrict);

        // Tarix DOIM nomzod bo'yicha va yozilgan tartibda o'qiladi.
        // Kompozit indeks saralashni ham qoplaydi va tashqi kalit
        // indeksining o'rnini ham bosadi.
        builder.HasIndex(entry => new { entry.CandidateId, entry.RecordedAt });

        // «Shu bosqichda nechta nomzod ko'rildi» — voronka hisoboti.
        builder.HasIndex(entry => entry.StageId);
    }
}

/// <summary>
/// <see cref="CandidateQuestionnaire"/> uchun jadval konfiguratsiyasi
/// (<c>candidate_questionnaires</c>).
/// </summary>
/// <remarks>
/// <para>
/// Yumshoq o'chirish YO'Q — <c>CandidateStageEntry</c> dagi bilan bir xil
/// sabab: yozuv nomzod bilan birga yashaydi va faqat u orqali o'qiladi.
/// </para>
/// <para>
/// <b>Indeks BITTA va u unikal.</b> Bu jadval hech qachon o'zicha
/// so'ralmaydi: unga yagona yo'l — nomzod kartasi. Shu sababli
/// <c>candidate_id</c> dan boshqa indeks qo'shish faqat yozishni
/// sekinlashtirardi.
/// </para>
/// </remarks>
public sealed class CandidateQuestionnaireConfiguration : IEntityTypeConfiguration<CandidateQuestionnaire>
{
    public void Configure(EntityTypeBuilder<CandidateQuestionnaire> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("candidate_questionnaires");

        builder.HasKey(form => form.Id);

        builder.Ignore(form => form.DomainEvents);

        // Bittadan ko'p anketa bo'lishi MUMKIN EMAS. Domen buni
        // `Candidate.AttachQuestionnaire` da tekshiradi, lekin qoida
        // bazada ham turishi kerak: domen tekshiruvi kod orqali
        // kelmagan yozuvni (migratsiya, qo'lda SQL) ushlay olmaydi.
        builder.HasIndex(form => form.CandidateId)
            .IsUnique();

        builder.Property(form => form.Education)
            .IsRequired();

        builder.Property(form => form.MaritalStatus)
            .IsRequired();

        builder.Property(form => form.RussianLevel)
            .IsRequired();

        builder.Property(form => form.ExpectedSalary)
            .IsRequired();

        builder.Property(form => form.ReferralSource)
            .IsRequired();

        /*
          `OfferAcceptedAt` ro'yxatda YO'Q va bu ATAYLAB: u yagona
          majburiy EMAS sanoq/vaqt maydoni. `NULL` — «oferta
          ko'rsatilmagan, rozilik so'ralmagan» (izohi domenda).
          `IsRequired()` qo'yilganida bot oferta havolasi sozlanmagan
          holatda yuborish lahzasini soxta rozilik vaqti qilib
          yozishga majbur bo'lardi — aynan shu xato tuzatilgan
          (migratsiya `OfertaRoziligiIxtiyoriy`).
        */

        builder.Property(form => form.WorkExperience)
            .HasMaxLength(CandidateQuestionnaire.MaxWorkExperienceLength);

        builder.Property(form => form.OtherLanguages)
            .HasMaxLength(CandidateQuestionnaire.MaxOtherLanguagesLength);

        builder.Property(form => form.OfficeSkills)
            .HasMaxLength(CandidateQuestionnaire.MaxOfficeSkillsLength);

        builder.Property(form => form.LastSalary)
            .HasMaxLength(CandidateQuestionnaire.MaxLastSalaryLength);

        builder.Property(form => form.ExtraNote)
            .HasMaxLength(CandidateQuestionnaire.MaxExtraNoteLength);

        builder.Property(form => form.DriverLicenseCategories)
            .HasMaxLength(CandidateQuestionnaire.MaxDriverLicenseCategoriesLength);
    }
}

/// <summary>
/// <see cref="CandidateFile"/> uchun jadval konfiguratsiyasi
/// (<c>candidate_files</c>).
/// </summary>
/// <remarks>
/// <para>
/// Yumshoq o'chirish YO'Q va bu <c>employee_documents</c> dan ATAYLAB
/// farq — sababi <see cref="CandidateFile"/> izohida: yumshoq o'chirilgan
/// qator o'nta o'rindan bittasini egallab turardi. Shu sababli bu entity
/// <c>ISoftDeletable</c> ni amalga oshirmaydi va global filtrga tushmaydi.
/// </para>
/// <para>
/// <b>Ma'lumotnomaga tashqi kalit YO'Q.</b> Xodim hujjatida
/// <c>document_type_id</c> bor, nomzod faylida esa hujjat turi tushunchasi
/// umuman yo'q (uni so'rash HR ning har bir rasm uchun ro'yxatdan tanlashi
/// degani bo'lardi — bugungi bir bosishlik yuklashning o'rniga).
/// </para>
/// </remarks>
public sealed class CandidateFileConfiguration : IEntityTypeConfiguration<CandidateFile>
{
    public void Configure(EntityTypeBuilder<CandidateFile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("candidate_files");

        builder.HasKey(file => file.Id);

        builder.Ignore(file => file.DomainEvents);

        // Uzunlik `employee_documents.file_key` dan olindi: kalitni bitta
        // `IFileStorage` yasaydi, ya'ni chegara ham bir xil bo'lishi kerak.
        builder.Property(file => file.FileKey)
            .HasMaxLength(500)
            .IsRequired();

        // 300 va 150 — o'chirilgan `candidates.resume_file_name` va
        // `resume_content_type` dan AYNAN ko'chirildi: migratsiya eski
        // qatorlarni shu jadvalga ko'chiradi va qisqaroq ustun ma'lumotni
        // kesib tashlardi.
        builder.Property(file => file.FileName)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(file => file.ContentType)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(file => file.SizeBytes)
            .IsRequired();

        builder.Property(file => file.CreatedAt)
            .IsRequired();

        // Fayllar DOIM nomzod bo'yicha va yuklash tartibida o'qiladi;
        // o'nta chegarasining sanog'i ham shu indeksdan foydalanadi.
        // Kompozit indeks tashqi kalit indeksining o'rnini ham bosadi
        // (`candidate_stage_entries` dagi bilan bir xil qaror).
        builder.HasIndex(file => new { file.CandidateId, file.CreatedAt });
    }
}

/// <summary>
/// <see cref="CandidateNote"/> uchun jadval konfiguratsiyasi
/// (<c>candidate_notes</c>).
/// </summary>
/// <remarks>
/// Yumshoq o'chirish YO'Q — <c>CandidateStageEntry</c> dagi bilan bir xil
/// sabab: yozuv nomzod bilan birga yashaydi va faqat u orqali o'qiladi.
/// <c>IAuditable</c> ham emas: <c>created_at</c> / <c>created_by</c> ni
/// domen o'zi yozadi, xuddi <c>recorded_at</c> / <c>recorded_by</c> kabi.
/// </remarks>
public sealed class CandidateNoteConfiguration : IEntityTypeConfiguration<CandidateNote>
{
    public void Configure(EntityTypeBuilder<CandidateNote> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("candidate_notes");

        builder.HasKey(note => note.Id);

        builder.Ignore(note => note.DomainEvents);

        builder.Property(note => note.Kind)
            .IsRequired();

        // 2000 — `candidates.note` va `candidates.closing_note` bilan bir
        // xil chegara: bu ham HR ning bir-ikki abzatslik qaydi.
        builder.Property(note => note.Text)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(note => note.CreatedAt)
            .IsRequired();

        // Izohlar DOIM nomzod bo'yicha va yozilgan tartibda o'qiladi.
        builder.HasIndex(note => new { note.CandidateId, note.CreatedAt });
    }
}
