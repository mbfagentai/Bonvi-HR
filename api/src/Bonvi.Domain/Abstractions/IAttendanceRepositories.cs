using Bonvi.Domain.Attendance;

namespace Bonvi.Domain.Abstractions;

/// <summary>Terminallar reyestri uchun saqlash porti.</summary>
/// <remarks>
/// <para>
/// Ro'yxat ekrani bu portdan keladi, o'qish modelidan emas — reyestrda
/// o'nlab qator bo'ladi, sahifalash va birlashtirish kerak emas.
/// <c>IVacancyRepository</c> dagidan farqi shu va u ataylab: o'qish
/// modeli minglab qatorli ro'yxatlar uchun bor.
/// </para>
/// </remarks>
public interface IAttendanceDeviceRepository : IRepository<AttendanceDevice>
{
    /// <summary>Token xeshi bo'yicha qurilmani topadi — qabul yo'lidagi YAGONA qidiruv.</summary>
    /// <remarks>
    /// <para>
    /// Bu unikal indeks bo'yicha aniq tenglik: prefiks bo'yicha ko'zdan
    /// kechirish ham, qatorma-qator taqqoslash sikli ham yo'q, ya'ni
    /// e'tiborga loyiq vaqt kanali (timing channel) ham yo'q.
    /// </para>
    /// <para>
    /// ⚠️ Yumshoq o'chirilgan qurilma TOPILMAYDI (global filtr) va aynan
    /// shunday bo'lishi kerak: o'chirilgan qurilmaning tokeni o'lik
    /// bo'lishi shart.
    /// </para>
    /// </remarks>
    Task<AttendanceDevice?> FindByTokenHashAsync(byte[] hash, CancellationToken cancellationToken = default);

    /// <summary>Qurilmalar ro'yxati, nom bo'yicha.</summary>
    /// <remarks>
    /// <paramref name="includeInactive"/> — faolsizlantirilganlari ham
    /// kerakmi. Boshqaruv ekrani ularni ko'rsatadi (o'chirilgan terminal
    /// ham reyestrda turishi kerak), qabul yo'li esa umuman bu yerdan
    /// o'qimaydi.
    /// </remarks>
    Task<IReadOnlyList<AttendanceDevice>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default);

    /// <summary>Shu nomli boshqa (o'chirilmagan) qurilma bormi — registrga sezgir emas.</summary>
    /// <remarks>
    /// Bazadagi <c>ux_attendance_devices_name</c> indeksini ALMASHTIRMAYDI:
    /// maqsad — odatiy holatda 500 emas, tushunarli 409 berish
    /// (<c>IReferenceRepository.NameExistsAsync</c> bilan bir xil naqsh).
    /// </remarks>
    /// <param name="name">Tekshiriladigan nom.</param>
    /// <param name="exceptId">Tahrirlanayotgan qurilmaning o'zi — takror sanalmaydi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<bool> NameExistsAsync(string name, Guid? exceptId = null, CancellationToken cancellationToken = default);

    /// <summary>Shu seriya raqamli boshqa (o'chirilmagan) qurilma bormi — registrga sezgir emas.</summary>
    /// <param name="serialNumber">Tekshiriladigan seriya raqami.</param>
    /// <param name="exceptId">Tahrirlanayotgan qurilmaning o'zi — takror sanalmaydi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<bool> SerialExistsAsync(string serialNumber, Guid? exceptId = null, CancellationToken cancellationToken = default);

    /// <summary>Qurilma yozgan birorta o'tish bormi.</summary>
    /// <remarks>
    /// O'tishi bor qurilma O'CHIRILMAYDI, faqat faolsizlantiriladi: jurnal
    /// har bir o'tishning manbasini ko'rsatishi kerak (SPEC §6.2).
    /// </remarks>
    /// <param name="deviceId">Qurilma.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<bool> HasPassesAsync(Guid deviceId, CancellationToken cancellationToken = default);
}

/// <summary>Qurilma raqami ↔ xodim mosliklari uchun saqlash porti.</summary>
public interface IAttendanceIdentityRepository : IRepository<AttendanceIdentity>
{
    /// <summary>Raqamlar fazosi va shaxs raqami bo'yicha AMALDAGI moslikni topadi.</summary>
    /// <remarks>
    /// Qabul qilishning issiq yo'li: har bir o'tish uchun bir marta
    /// chaqiriladi va <c>ux_attendance_identities_person</c> indeksiga
    /// to'liq tushadi. Uzilgan (yumshoq o'chirilgan) mosliklar global
    /// filtr bilan chetda qoladi — ular tarix, amaldagi javob emas.
    /// </remarks>
    Task<AttendanceIdentity?> FindAsync(
        AttendanceIdentityProvider provider,
        string externalPersonId,
        CancellationToken cancellationToken = default);

    /// <summary>Xodimning barcha amaldagi mosliklari.</summary>
    /// <remarks>
    /// Bittadan ko'p bo'lishi NORMAL: ikki terminal odamni ikki marta
    /// ro'yxatga olgan bo'lishi yoki keyinroq ikkinchi raqamlar fazasi
    /// qo'shilishi mumkin.
    /// </remarks>
    Task<IReadOnlyList<AttendanceIdentity>> ListByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    /// <summary>Bir nechta shaxs raqamining amaldagi mosliklari — bitta so'rov bilan.</summary>
    /// <remarks>
    /// Verifix importi 5 000 tagacha qator olib keladi; qatorma-qator
    /// <see cref="FindAsync"/> chaqirish 5 000 ta so'rov bo'lardi.
    /// </remarks>
    /// <param name="provider">Raqamlar fazosi.</param>
    /// <param name="externalPersonIds">Shaxs raqamlari (qirqilgan holda).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<AttendanceIdentity>> FindManyAsync(
        AttendanceIdentityProvider provider,
        IReadOnlyCollection<string> externalPersonIds,
        CancellationToken cancellationToken = default);
}

/// <summary>Xom o'tishlar uchun saqlash porti.</summary>
/// <remarks>
/// <para>
/// Faqat YOZISH uchun: o'tishlar jurnali, taxta va bog'lanmaganlar
/// ekrani <c>IAttendanceReadModel</c> dan keladi — ular sahifalanadi va
/// xodim nomi bilan birlashtiriladi, ya'ni agregat qaytarish o'rinsiz
/// bo'lardi.
/// </para>
/// <para>
/// ⚠️ <c>Remove</c> amalda ISHLATILMAYDI: o'tish mashina yozuvi va
/// o'chirish endpointi yo'q (SPEC §1.2 #5). U asosdan meros bo'lib
/// keladi, xolos. O'tishni o'chiradigan YAGONA yo'l — saqlash muddati
/// siyosati (<see cref="DeleteExpiredAsync"/>, tungi vazifa, §9.2.1):
/// u bitta qatorni tanlab emas, sanadan eski hammasini bir xil qoida
/// bilan o'chiradi.
/// </para>
/// </remarks>
public interface IAttendancePassRepository : IRepository<AttendancePass>
{
    /// <summary>Qurilmaning shu takror kalitli o'tishi (kuzatuv bilan) — takror kelgan suratni biriktirish uchun.</summary>
    /// <param name="deviceId">Qurilma.</param>
    /// <param name="dedupeKey">Takror kaliti (`ux_attendance_passes_device_dedupe`).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<AttendancePass?> FindByDedupeAsync(Guid deviceId, string dedupeKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Shu shaxs raqamining BOG'LANMAGAN o'tishlarini xodimga bog'laydi — SPEC §4.3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bitta ommaviy <c>UPDATE</c>, agregatlar xotiraga yuklanmaydi: moslik
    /// oylar o'tib yaratilishi mumkin va har bir o'tish bilan birga
    /// <c>raw_event</c> (jsonb) ham o'qilardi. <c>processed_at</c> NULL
    /// qilinadi — kun qayta hisoblanmaguncha o'tish «iflos» sanaladi
    /// (<see cref="AttendancePass.Resolve"/> bilan bir xil ma'no).
    /// </para>
    /// <para>
    /// Faqat HALI bog'lanmaganlar tegiladi (<c>resolved_employee_id IS NULL</c>):
    /// ikkinchi chaqiruv hech narsa qilmaydi, ya'ni vazifa idempotent.
    /// </para>
    /// </remarks>
    /// <param name="provider">Raqamlar fazosi.</param>
    /// <param name="rawPersonId">Qurilmadagi shaxs raqami.</param>
    /// <param name="employeeId">Bog'lanadigan xodim.</param>
    /// <param name="resolvedAt">Bog'lash lahzasi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>Aynan shu chaqiruv bog'lagan o'tishlar — qaysi kunlarni qayta hisoblash kerakligi shundan.</returns>
    Task<IReadOnlyList<AttachedPass>> AttachUnmatchedAsync(
        AttendanceIdentityProvider provider,
        string rawPersonId,
        Guid employeeId,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken = default);

    /// <summary>Berilgan o'tishlarni «hisobga olingan» deb belgilaydi.</summary>
    /// <remarks>
    /// Faqat kunlari ALLAQACHON qayta hisoblangan o'tishlar uchun chaqiriladi.
    /// Belgi qo'yilmasa zarari yo'q (keyingi navbat ularni yana hisoblaydi,
    /// natija bir xil) — bu faqat ortiqcha ishni tejaydi.
    /// </remarks>
    /// <param name="passIds">O'tishlar.</param>
    /// <param name="processedAt">Belgi lahzasi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task MarkProcessedAsync(
        IReadOnlyCollection<Guid> passIds,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bog'lanmagan o'tishi bor, lekin endi AMALDAGI mosligi ham bor shaxs raqamlari.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bu holat faqat bitta tor oynada tug'iladi: qabul moslikni saqlashdan
    /// OLDIN o'qigan, o'tishni esa to'ldirish vazifasi o'tib bo'lgandan KEYIN
    /// yozgan. To'ldirish uni ko'rmaydi, iflos navbat esa bog'lanmagan
    /// o'tishga qaramaydi — davriy vazifa shu ro'yxat bilan tuzatadi.
    /// </para>
    /// <para>
    /// <c>ix_attendance_passes_unmatched</c> qisman indeksi va moslikning
    /// unikal indeksi bo'yicha birlashma; o'chirilgan (uzilgan) moslik global
    /// filtr bilan chetda qoladi — u tarix, amaldagi javob emas.
    /// </para>
    /// </remarks>
    /// <param name="take">Bir yurishda eng ko'p nechta raqam.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<AttachableIdentity>> ListAttachableUnmatchedAsync(
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Iflos navbat: qayta hisoblanishini kutayotgan bog'langan o'tishlar — eng eski
    /// kutayotgan (xodim, sana) juftliklaridan <paramref name="maxDays"/> tasiga tegishlilari.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Tartib — birinchi o'tish QABUL qilingan vaqt bo'yicha</b> (SPEC §9.3).
    /// Usiz navbat chegaradan katta bo'lganda har yurish o'sha ixtiyoriy
    /// juftliklarni qayta o'qib, qolgani hech qachon navbatga yetmasdi.
    /// </para>
    /// <para>
    /// Juftlik emas, O'TISH qaytariladi: «hisobga olindi» belgisi aynan shu
    /// yerda o'qilgan o'tishlarga qo'yiladi. Juftlik va vaqt chegarasi bo'yicha
    /// belgilash o'qish bilan belgilash orasida kelgan o'tishni ham belgilab,
    /// uni tabeldan jimgina tushirib qoldirardi.
    /// </para>
    /// </remarks>
    /// <param name="maxDays">Bir yurishda eng ko'p nechta (xodim, sana) juftligi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<PendingPass>> ListPendingAsync(
        int maxDays,
        CancellationToken cancellationToken = default);

    /// <summary>Surati hali bor va <paramref name="receivedBefore"/> dan oldin qabul qilingan o'tishlar — eng eskisidan.</summary>
    /// <remarks>
    /// <c>ix_attendance_passes_photo</c> (<c>received_at</c>, faqat surati borlar)
    /// indeksiga tushadi: tozalangan qator indeksdan chiqadi, ya'ni har tungi
    /// yurish faqat haqiqiy ishni o'qiydi.
    /// </remarks>
    /// <param name="receivedBefore">Shu lahzadan oldin qabul qilinganlari.</param>
    /// <param name="take">To'plam hajmi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<ExpiredPass>> ListExpiredPhotosAsync(
        DateTimeOffset receivedBefore,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fayli ALLAQACHON o'chirilgan o'tishlarda suratni «tozalangan» deb yozadi:
    /// <c>photo_key</c> va <c>raw_event</c> — <c>NULL</c>, qatorning o'zi qoladi.
    /// </summary>
    /// <remarks>
    /// <see cref="AttendancePass.PurgePhoto"/> bilan bir xil ma'no, lekin
    /// ommaviy <c>UPDATE</c> bilan: minglab o'tishni <c>raw_event</c> (jsonb)
    /// bilan xotiraga yuklash kerak emas. Faqat fayldan KEYIN chaqiriladi —
    /// teskari tartibda yiqilish faylni hech kim bilmaydigan yetimga aylantirardi.
    /// </remarks>
    /// <param name="passIds">Fayli o'chirilgan o'tishlar.</param>
    /// <param name="purgedAt">Tozalash lahzasi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>Yangilangan qatorlar soni.</returns>
    Task<int> MarkPhotosPurgedAsync(
        IReadOnlyCollection<Guid> passIds,
        DateTimeOffset purgedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>local_date</c> i <paramref name="localDateBefore"/> dan oldingi, O'CHIRISH MUMKIN
    /// bo'lgan o'tishlar — hisobga olinganlari va bog'lanmaganlari, eng eski sanadan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ro'yxatga KIRMAYDIGANI faqat bitta holat: xodimga bog'langan-u hali
    /// hisobga olinmagan o'tish (SPEC §9.2.1). Shuncha eski kutayotgan o'tish —
    /// buzilgan navbat belgisi, uni o'chirish buzilishni yashirardi. Shu sababli
    /// ular keyingi to'plamlarni ham to'smaydi.
    /// </para>
    /// <para>
    /// Bog'lanmagan o'tish esa o'chiriladi (AMENDED 2026-09-17, egasining bir
    /// yillik qarori): uning sanasi tozalashdan oldin muzlatiladi
    /// (<c>attendance.frozenThrough</c>), ya'ni keyin kiritilgan moslik ham uni
    /// hech qachon tabelga qo'sha olmaydi — qoldirish jadvalni mehmon kartalari
    /// va hech kim moslamaydigan raqamlar bilan cheksiz o'stirardi.
    /// </para>
    /// </remarks>
    /// <param name="localDateBefore">Shu sanadan (o'zi kirmaydi) oldingi o'tishlar.</param>
    /// <param name="take">To'plam hajmi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<ExpiredPass>> ListExpiredPassesAsync(
        DateOnly localDateBefore,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// O'tish qatorlarini BAZADAN o'chiradi — o'chirish lahzasida ham hisobga olingan
    /// yoki bog'lanmagan bo'lib qolganlarini.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>QAYTARIB BO'LMAYDI.</b> Yumshoq o'chirish emas — maqsad aynan qator
    /// va disk joyini bo'shatish (§9.2.1). Suratlari chaqiruvchi tomonidan
    /// OLDIN o'chirilgan bo'lishi shart.
    /// </para>
    /// <para>
    /// <c>attendance_days</c> ga TEGMAYDI va hech bir kunni qayta hisoblash
    /// navbatiga qo'ymaydi.
    /// </para>
    /// <para>
    /// ⚠️ <b>Poyga: ro'yxat bilan o'chirish orasida to'ldirish.</b> Ro'yxatda
    /// bog'lanmagan bo'lgan o'tishni shu orada to'ldirish vazifasi xodimga
    /// bog'lab, <c>processed_at</c> ni <c>NULL</c> qilishi mumkin — u endi
    /// «bog'langan-u hisobga olinmagan», ya'ni o'chirilmasligi kerak bo'lgan
    /// holat. Shu sababli <c>(processed_at IS NOT NULL OR resolved_employee_id IS NULL)</c>
    /// sharti o'chirish buyrug'ining O'ZIDA qayta tekshiriladi va ikkala tartib
    /// ham xavfsiz: (1) bog'lash avval saqlansa — <c>DELETE</c> yangi holatni
    /// ko'rib, qatorni o'tkazib yuboradi (PostgreSQL READ COMMITTED da qulflangan
    /// qatorni kutgan <c>DELETE</c> ham shartni yangilangan versiyada qayta
    /// baholaydi); (2) <c>DELETE</c> avval saqlansa — bog'lash o'chgan qatorni
    /// topmaydi, uning kuni esa allaqachon muzlagan va qayta hisoblanmaydi.
    /// Birinchi holatning yagona izi: fayli qatordan oldin o'chirilgan bo'lsa,
    /// omon qolgan qator yo'q suratga ishora qiladi (surat endpointi 404) va u
    /// hisobga olingach keyingi tunda zararsiz o'chadi.
    /// </para>
    /// </remarks>
    /// <param name="passIds">O'chiriladigan o'tishlar.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>O'chirilgan qatorlar soni.</returns>
    Task<int> DeleteExpiredAsync(
        IReadOnlyCollection<Guid> passIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saqlash muddati o'tgan, xodimga BOG'LANGAN, lekin hali HISOBGA OLINMAGAN o'tishlar soni —
    /// buzilgan navbat belgisi.
    /// </summary>
    /// <remarks>
    /// <c>ix_attendance_passes_pending</c> qisman indeksiga tushadi. Bog'lanmagan
    /// o'tishlar SANALMAYDI: ular muddat o'tgach o'chiriladi.
    /// </remarks>
    /// <param name="localDateBefore">Shu sanadan (o'zi kirmaydi) oldingi o'tishlar.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<int> CountExpiredPendingAsync(
        DateOnly localDateBefore,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Shu xodimning <paramref name="since"/> dan keyingi ENG SO'NGGI o'tish lahzasi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Telefondan belgilashning 60 soniyalik takror qoidasi uchun
    /// (<see cref="AttendanceRules.BounceSeconds"/>). Javob NIMA UCHUN
    /// lahza, <c>bool</c> emas: ilova xatoda «oxirgi belgi 10:31 da qabul
    /// qilingan» deb yozadi va «qayta urinib ko'ring» dan ko'ra shu
    /// foydaliroq.
    /// </para>
    /// <para>
    /// <c>ix_attendance_passes_employee_time</c> ga aniq tushadi: prefiks
    /// bo'yicha teng, ikkinchi ustun bo'yicha teskari yurish, bitta qator.
    /// </para>
    /// </remarks>
    /// <param name="employeeId">Xodim.</param>
    /// <param name="since">Shu lahzadan keyingilari (o'zi kirmaydi).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<DateTimeOffset?> LastPassAtAsync(
        Guid employeeId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);
}

/// <summary>Moslik yaratilgach bog'langan bitta o'tish — qayta hisoblash uchun kerakli qismi.</summary>
/// <param name="Id">O'tish.</param>
/// <param name="LocalDate">O'tish tushgan Toshkent sanasi.</param>
public readonly record struct AttachedPass(Guid Id, DateOnly LocalDate);

/// <summary>Bog'lanmagan o'tishi qolgan amaldagi moslik.</summary>
/// <param name="Provider">Raqamlar fazosi.</param>
/// <param name="RawPersonId">Qurilmadagi shaxs raqami.</param>
/// <param name="EmployeeId">Moslik ko'rsatgan xodim.</param>
public sealed record AttachableIdentity(AttendanceIdentityProvider Provider, string RawPersonId, Guid EmployeeId);

/// <summary>Iflos navbatdagi bitta o'tish.</summary>
/// <param name="Id">O'tish.</param>
/// <param name="EmployeeId">Bog'langan xodim.</param>
/// <param name="LocalDate">O'tish tushgan Toshkent sanasi.</param>
public readonly record struct PendingPass(Guid Id, Guid EmployeeId, DateOnly LocalDate);

/// <summary>Muddati o'tgan o'tish — tozalash uchun kerakli qismi.</summary>
/// <param name="Id">O'tish.</param>
/// <param name="PhotoKey">Surat kaliti; surati yo'q yoki allaqachon tozalangan bo'lsa <c>null</c>.</param>
public sealed record ExpiredPass(Guid Id, string? PhotoKey);


/// <summary>Ish jadvallari uchun saqlash porti.</summary>
/// <remarks>
/// <c>GetByIdAsync</c> jadvalni KUNLARI bilan birga yuklaydi: jadval
/// kunlarsiz hech qanday savolga javob bermaydi va uni alohida
/// so'raydigan yo'l ochilsa, kimdir uni chaqirishni unutib, bo'sh
/// haftani ko'rsatardi.
/// </remarks>
public interface IWorkScheduleRepository : IRepository<WorkSchedule>
{
    /// <summary>Barcha (o'chirilmagan) jadvallar, kunlari bilan, nom bo'yicha.</summary>
    /// <remarks>
    /// Faolsizlari ham kiradi: ekran ularni belgilab ko'rsatadi, yashirmaydi.
    /// Jadvallar o'nlab bo'ladi, sahifalash kerak emas.
    /// </remarks>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<WorkSchedule>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Shu nomli boshqa (o'chirilmagan) jadval bormi — registrga sezgir emas.</summary>
    /// <param name="name">Tekshiriladigan nom.</param>
    /// <param name="exceptId">Tahrirlanayotgan jadvalning o'zi — takror sanalmaydi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<bool> NameExistsAsync(string name, Guid? exceptId = null, CancellationToken cancellationToken = default);
}

/// <summary>Jadval biriktirmalari uchun saqlash porti.</summary>
/// <remarks>
/// Hal qiluvchi (resolver) biriktirmalarni bu portdan EMAS, to'plam
/// bo'lib o'qiydi — u 540 xodim uchun ishlaydi va har biriga alohida
/// murojaat N+1 bo'lardi. Bu port biriktirmani boshqarish (qo'shish,
/// muddatini o'zgartirish, uzish) uchun.
/// </remarks>
public interface IScheduleAssignmentRepository : IRepository<ScheduleAssignment>
{
    /// <summary>Jadvalga ishora qiladigan tirik (o'chirilmagan) biriktirma bormi.</summary>
    /// <remarks>
    /// Muddati o'tganlari ham SANALADI: qayta hisoblash o'tgan oylar uchun
    /// ham o'sha biriktirmani ishlatadi, ya'ni jadval hali «ishlatilmoqda».
    /// </remarks>
    /// <param name="workScheduleId">Jadval.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<bool> AnyForScheduleAsync(Guid workScheduleId, CancellationToken cancellationToken = default);

    /// <summary>Xuddi shu pog'ona, obyekt va boshlanish sanasi bilan tirik biriktirma bormi.</summary>
    /// <remarks>
    /// <c>ux_schedule_assignments_scope</c> (NULLS NOT DISTINCT) bilan bir xil
    /// shart — odatiy holatda 500 emas, 409 berish uchun.
    /// </remarks>
    /// <param name="scope">Pog'ona.</param>
    /// <param name="targetId">Pog'ona obyekti; sukut pog'onada <c>null</c>.</param>
    /// <param name="effectiveFrom">Kuchga kirish sanasi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<bool> SameStartExistsAsync(
        ScheduleScope scope,
        Guid? targetId,
        DateOnly effectiveFrom,
        CancellationToken cancellationToken = default);
}

/// <summary>Hisoblangan kunlar uchun saqlash porti.</summary>
public interface IAttendanceDayRepository : IRepository<AttendanceDay>
{
    /// <summary>Xodimlarning [from, to] dagi kun yozuvlari — KUZATILADI (tahrir uchun).</summary>
    /// <remarks>
    /// Qayta hisoblash har bir kunni shu ro'yxat bilan solishtiradi: bor
    /// qator yangilanadi, yo'g'i qo'shiladi, endi bo'lmasligi kerak bo'lgani
    /// o'chiriladi. Qator ALMASHTIRILMAYDI (DELETE + INSERT) — parallel
    /// o'qilayotgan hisobot bir lahza bo'sh katak ko'rmasligi kerak (§9.3).
    /// </remarks>
    /// <param name="employeeIds">Xodimlar.</param>
    /// <param name="from">Davr boshi (o'zi ham kiradi).</param>
    /// <param name="to">Davr oxiri (o'zi ham kiradi).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<AttendanceDay>> ListForUpdateAsync(
        IReadOnlyCollection<Guid> employeeIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>Kuzatuvdagi BARCHA kun yozuvlarini unutadi — saqlanmagan o'zgarishlari bilan.</summary>
    /// <remarks>
    /// Faqat bitta holat uchun: ikki qayta hisoblash bir (xodim, sana) ga bir
    /// vaqtda yozib, saqlash to'qnashuv bilan yiqilganda. Yiqilgan urinishning
    /// qo'shilgan, o'zgargan va o'chirilgan qatorlari kuzatuvda qolsa, qayta
    /// urinish bazadagi yangi holatni emas, o'sha eskirgan nusxalarni ko'rardi.
    /// </remarks>
    void ForgetTracked();
}

/// <summary>Yo'qlik so'rovlari uchun saqlash porti (SPEC-so'rovlar §B2).</summary>
/// <remarks>
/// Ro'yxat ekranlari o'qish modelidan keladi (ular ism va tur nomini
/// qo'shib beradi); bu port faqat YOZISH yo'li uchun — bitta so'rovni
/// olish, qo'shish va to'qnashuvni tekshirish.
/// </remarks>
public interface IAbsenceRequestRepository : IRepository<AbsenceRequest>
{
    /// <summary>
    /// Shu xodimda, shu davrni QAMRAYDIGAN faol so'rov bormi.
    /// </summary>
    /// <remarks>
    /// «Faol» — kutayotgan yoki tasdiqlangan. Rad etilgan va bekor qilingan
    /// so'rov yo'lni to'smaydi: HR rad etgan kunga yangisini kiritishi
    /// mumkin bo'lishi kerak.
    /// </remarks>
    /// <param name="employeeId">Xodim.</param>
    /// <param name="from">Davr boshi.</param>
    /// <param name="to">Davr oxiri.</param>
    /// <param name="exceptId">Tahrirlanayotgan so'rovning o'zi — to'qnashuv sanalmaydi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<bool> OverlapsAsync(
        Guid employeeId,
        DateOnly from,
        DateOnly to,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Berilgan xodimlarning berilgan oraliqdagi TASDIQLANGAN so'rovlari —
    /// kun hisoblash uchun.
    /// </summary>
    /// <remarks>
    /// Natija «xodim → sanalar» ko'rinishida emas, so'rovlar ro'yxati bo'lib
    /// qaytadi: chaqiruvchi ularni o'z to'plami bo'yicha yoyadi va shu bilan
    /// port hisoblash qoidasini bilmaydi.
    /// </remarks>
    /// <param name="employeeIds">Xodimlar.</param>
    /// <param name="from">Davr boshi.</param>
    /// <param name="to">Davr oxiri.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<AbsenceRequest>> ListApprovedAsync(
        IReadOnlyCollection<Guid> employeeIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}

/// <summary>Yillik to'r kataklari uchun saqlash porti (SPEC-jadval-to'ri §B1).</summary>
/// <remarks>
/// Jadvalning bolasi EMAS, alohida port: bir jadvalda bir necha yillik
/// sana bo'lishi mumkin va ularning hammasi hech qachon birga kerak
/// bo'lmaydi (entity izohi).
/// </remarks>
public interface IWorkScheduleDateRepository
{
    /// <summary>Bitta jadvalning berilgan oraliqdagi kataklari.</summary>
    Task<IReadOnlyList<WorkScheduleDate>> ListAsync(
        Guid workScheduleId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>Berilgan SANALARDAGI kataklar — yozish yo'li uchun (kuzatiladi).</summary>
    Task<IReadOnlyList<WorkScheduleDate>> ListForUpdateAsync(
        Guid workScheduleId,
        IReadOnlyCollection<DateOnly> dates,
        CancellationToken cancellationToken = default);

    void Add(WorkScheduleDate date);

    void Remove(WorkScheduleDate date);
}

/// <summary>Xodimning qurilmadagi holati uchun saqlash porti (SPEC-agent §D3).</summary>
/// <remarks>
/// Qurilmaning bolasi EMAS, alohida port: bir qurilmada minglab qator
/// bo'lishi mumkin va ular hech qachon qurilma kartasi bilan birga
/// yuklanmaydi.
/// </remarks>
public interface IDeviceEnrollmentRepository
{
    /// <summary>Bitta qurilmaning barcha holatlari — KUZATILADI (agent hisoboti ularni yozadi).</summary>
    Task<IReadOnlyList<DeviceEnrollment>> ListByDeviceAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>Bitta xodimning barcha qurilmalardagi holati.</summary>
    Task<IReadOnlyList<DeviceEnrollment>> ListByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    void Add(DeviceEnrollment enrollment);
}

/// <summary>
/// Qurilmaga yoziladigan shaxs raqamlarini beradi (SPEC-agent §D1).
/// </summary>
/// <remarks>
/// <para>
/// Raqam bazadagi KETMA-KETLIKdan olinadi, «eng kattasi + 1» dan emas:
/// ikkita agent bir vaqtda ro'yxat so'rasa, ikkinchi usul ikkalasiga bir
/// xil raqam berib, ikkinchi xodimning yozuvini unikal indeksda yiqitardi.
/// </para>
/// <para>
/// Ketma-ketlik faqat SON beradi; «9» prefiksi va uzunligi —
/// <c>DevicePersonNo</c> ning ishi, chunki ular qurilma bilan emas,
/// Bonvi ning raqamlar fazosi bilan bog'liq qoida.
/// </para>
/// </remarks>
public interface IDevicePersonNumbers
{
    /// <summary>Keyingi bo'sh tartib raqami.</summary>
    Task<long> NextAsync(CancellationToken cancellationToken = default);
}
