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

        builder.Property(candidate => candidate.ResumeFileName)
            .HasMaxLength(300);

        builder.Property(candidate => candidate.ResumeContentType)
            .HasMaxLength(150);

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

        // Telefon bo'yicha qidiruv — nomzodda JSHSHIR yo'q, shu sababli bu
        // ikkinchi eng muhim kalit. Qisman indeks: raqamsiz yozuvlar bo'ladi.
        builder.HasIndex(candidate => candidate.Phone)
            .HasFilter("phone IS NOT NULL");

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
/// <see cref="JobOffer"/> uchun jadval konfiguratsiyasi (<c>job_offers</c>).
/// </summary>
public sealed class JobOfferConfiguration : IEntityTypeConfiguration<JobOffer>
{
    public void Configure(EntityTypeBuilder<JobOffer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("job_offers");

        builder.HasKey(offer => offer.Id);

        builder.Ignore(offer => offer.DomainEvents);

        builder.Property(offer => offer.Status)
            .IsRequired();

        builder.Property(offer => offer.StartDate)
            .IsRequired();

        builder.Property(offer => offer.SentAt)
            .IsRequired();

        builder.Property(offer => offer.ResponseNote)
            .HasMaxLength(2000);

        builder.Property(offer => offer.Note)
            .HasMaxLength(2000);

        // ⚠️ `MapMoney` yordamchisi bu yerda ISHLATILMAYDI. U pulni
        // IXTIYORIY complex type qilib map qiladi (`IsRequired(false)`) —
        // oklad, rag'bat va jarima summasi bo'sh bo'lishi mumkin bo'lgani
        // uchun. Taklifdagi oklad esa MAJBURIY (sababi `JobOffer` izohida)
        // va nullable bo'lmagan xossani ixtiyoriy deb e'lon qilish model
        // qurilishida xatoga olib kelardi. Ustun nomlari va aniqligi esa
        // AYNAN o'sha yordamchidagidek qoldirilgan: summa hamma joyda
        // `numeric(14,2)` bo'lishi shart, aks holda tiyinlar yo'qoladi.
        builder.ComplexProperty(offer => offer.Salary, money =>
        {
            money.Property(value => value.Amount)
                .HasColumnName("salary_amount")
                .HasPrecision(14, 2);

            money.Property(value => value.Currency)
                .HasColumnName("salary_currency")
                .HasMaxLength(3);
        });

        builder.HasOne<Candidate>()
            .WithMany()
            .HasForeignKey(offer => offer.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Vacancy>()
            .WithMany()
            .HasForeignKey(offer => offer.VacancyId)
            .OnDelete(DeleteBehavior.Restrict);

        // «Nomzodning takliflari» — kartada har ochilganda. Kompozit
        // indeks «javob kutayotgani bormi» tekshiruvini ham qoplaydi.
        builder.HasIndex(offer => new { offer.CandidateId, offer.Status });

        // «Yuborilgan takliflar, yangidan eskiga» — rekruting hisoboti.
        builder.HasIndex(offer => offer.SentAt);
    }
}
