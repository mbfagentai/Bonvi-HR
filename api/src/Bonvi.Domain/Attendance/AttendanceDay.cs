using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>
/// Bir kunning hisoblangan natijasi — tabel katagi.
/// </summary>
/// <remarks>
/// Hamma maydon BIRGA keladi va birga yangilanadi: ular bitta
/// hisoblashning natijasi, ya'ni ulardan birini alohida o'zgartirish
/// qatorni o'z-o'ziga zid qilib qo'yardi (masalan holat
/// <c>Absent</c>, lekin <c>worked_minutes</c> 480). Yozuv shakli aynan
/// shu bog'liqlikni kodda ko'rinadigan qiladi — 19 ta alohida
/// parametr esa buni yashirardi.
/// </remarks>
/// <param name="EmploymentId">Qaysi mehnat munosabati bo'yicha hisoblangan (asosiysi).</param>
/// <param name="LegalEntityId">Firma — SURAT, hisobot ko'chirishdan keyin ham o'zgarmasligi uchun.</param>
/// <param name="DepartmentId">Bo'lim — surat.</param>
/// <param name="PositionId">Lavozim — surat.</param>
/// <param name="WorkScheduleId">Topilgan jadval. <c>null</c> ⇒ jadval yo'q.</param>
/// <param name="PlanStartAt">Shu sana uchun rejadagi boshlanish lahzasi.</param>
/// <param name="PlanEndAt">Rejadagi tugash lahzasi — tungi smenada ERTASIGA tushadi.</param>
/// <param name="FirstInAt">Birinchi kirish.</param>
/// <param name="LastOutAt">Oxirgi chiqish.</param>
/// <param name="PlannedMinutes">Rejadagi daqiqalar; jadval bo'lmasa 0.</param>
/// <param name="WorkedMinutes">Yopilgan oraliqlar yig'indisi, tanaffus ushlanmasi ayrilgandan KEYIN.</param>
/// <param name="LateMinutes">Kechikish (yon berishdan keyin).</param>
/// <param name="EarlyLeaveMinutes">Erta ketish (yon berishdan keyin).</param>
/// <param name="OvertimeMinutes">Ortiqcha ishlangan vaqt — hisoblanadi, HECH QACHON pulga aylanmaydi.</param>
/// <param name="BreakTakenMinutes">Tanaffus oynasi ichida binodan tashqarida o'tgan daqiqalar (SPEC §5.5.1).</param>
/// <param name="BreakDeductionMinutes">Ishlagan vaqtdan ayrilgan tanaffus: <c>max(0, min(break_minutes − BreakTakenMinutes, oynada binoda bo'lgan daqiqalar))</c> — binoda bo'lmagan tushlik ushlanmaydi.</param>
/// <param name="PassCount">Shu kunga tegishli o'tishlar soni.</param>
/// <param name="IgnoredPassCount">Sakrash deb tashlangan takror o'qishlar soni.</param>
/// <param name="Status">Kunning holati.</param>
/// <param name="CreditedMinutes">
/// <paramref name="WorkedMinutes"/> ning «ish vaqti hisoblanadigan» tasdiqlangan
/// so'rovdan kelgan qismi (SPEC-so'rovlar §B3 AMENDED 2026-10-17). 0 — hammasi
/// terminal o'tishlaridan.
/// </param>
/// <param name="IsFlexible">
/// Hisoblashda ishlatilgan reja «Erkin ish jadvali» edimi — SURAT (SPEC-erkin-jadval §2.2, 17-qator).
/// </param>
/// <param name="ShortfallMinutes">
/// «Kam ishlangan»: erkin kunda <c>max(0, norma − ishlangan)</c> — faqat yakuniy holat
/// <c>Present</c> va reja oynasi tugagan bo'lsa; qolgan hamma kunda 0.
/// </param>
/// <param name="PersonalMinutes">
/// «Shaxsiy vaqt»: rejaga tushgan tasdiqlangan vaqtli shaxsiy so'rov (ish vaqti hisoblanmaydigan
/// tur) daqiqalari, tanaffus tuzatishidan keyin (SPEC-sorov-tahriri §5.3, 4-qadam). Sababli, lekin
/// haq to'lanmaydi — <paramref name="WorkedMinutes"/> ga kirmaydi.
/// </param>
public sealed record AttendanceDayFacts(
    Guid? EmploymentId,
    Guid? LegalEntityId,
    Guid? DepartmentId,
    Guid? PositionId,
    Guid? WorkScheduleId,
    DateTimeOffset? PlanStartAt,
    DateTimeOffset? PlanEndAt,
    DateTimeOffset? FirstInAt,
    DateTimeOffset? LastOutAt,
    int PlannedMinutes,
    int WorkedMinutes,
    int LateMinutes,
    int EarlyLeaveMinutes,
    int OvertimeMinutes,
    int BreakTakenMinutes,
    int BreakDeductionMinutes,
    int PassCount,
    int IgnoredPassCount,
    AttendanceDayStatus Status,
    int CreditedMinutes = 0,
    bool IsFlexible = false,
    int ShortfallMinutes = 0,
    int PersonalMinutes = 0);

/// <summary>
/// Bir xodimning bir kundagi davomati — tabelning bitta katagi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Kun XODIMGA tegishli, mehnat munosabatiga emas.</b> Turniket
/// SHAXSni yozadi va bitta jismoniy borliqni ikki yuridik shaxs orasida
/// bo'lib bo'lmaydi — bu holdingda esa bitta odam rostdan ikkita
/// munosabatga ega bo'lishi mumkin («VIKOR + Вело унион 0,5 ставка
/// ўриндош»). Jadval, firma, bo'lim va lavozim ASOSIY munosabatdan
/// olinadi va shu yerga SURAT qilib yoziladi: xodim keyin boshqa
/// bo'limga o'tsa, o'tgan oyning hisoboti o'zgarmasligi kerak.
/// </para>
/// <para>
/// <b>Nega <c>IAuditable</c> ham, <c>ISoftDeletable</c> ham emas.</b>
/// Bu yozuvni qo'lda hech kim yozmaydi va tahrirlamaydi — hisoblangan
/// kunni qo'lda tuzatish ATAYLAB yo'q (SPEC §1.2 #5): tabel qo'l bilan
/// o'zgartiriladigan bo'lsa, u dalil bo'lishdan to'xtaydi va har bir
/// tortishuv «buni kim o'zgartirdi» ga aylanadi. Noto'g'ri ma'lumot
/// MANBADA tuzatiladi (moslik, jadval, qurilma soati), keyin kun qayta
/// hisoblanadi. Audit ustunlarining o'rnida <see cref="ComputedAt"/>
/// turadi.
/// </para>
/// <para>
/// <b>Hajm:</b> 540 xodim × 365 kun ≈ yiliga 200 000 qator. Yozuv faqat
/// nimadir bo'lgan (yoki kutilgan) sana uchun ochiladi, qolgan kataklar
/// tabel so'rovida hosil qilinadi — jadval kalendarga emas, voqelikka
/// mutanosib bo'lib qoladi.
/// </para>
/// </remarks>
public sealed class AttendanceDay : AggregateRoot
{
    private AttendanceDay(Guid id, Guid employeeId, DateOnly localDate)
        : base(id)
    {
        EmployeeId = employeeId;
        LocalDate = localDate;
        Status = AttendanceDayStatus.NoSchedule;
    }

    /// <summary>EF Core uchun.</summary>
    private AttendanceDay() { }

    public Guid EmployeeId { get; private set; }

    /// <summary>Smena BOSHLANGAN Toshkent kalendar sanasi.</summary>
    /// <remarks>
    /// Tungi smena (20:00–08:00) bitta yozuv beradi va u BOSHLANISH
    /// sanasiga tushadi; <see cref="PlanEndAt"/> esa ertangi kunda
    /// bo'ladi. Aks holda bir smena ikki kunga bo'linib, ikkalasi ham
    /// yarim bo'lib ko'rinardi.
    /// </remarks>
    public DateOnly LocalDate { get; private set; }

    /// <summary>Hisoblashda ishlatilgan asosiy mehnat munosabati.</summary>
    public Guid? EmploymentId { get; private set; }

    /// <summary>Firma — surat.</summary>
    public Guid? LegalEntityId { get; private set; }

    /// <summary>Bo'lim — surat.</summary>
    public Guid? DepartmentId { get; private set; }

    /// <summary>Lavozim — surat.</summary>
    public Guid? PositionId { get; private set; }

    /// <summary>Topilgan jadval. <c>null</c> ⇒ jadval hal qilinmadi.</summary>
    public Guid? WorkScheduleId { get; private set; }

    /// <summary>Rejadagi boshlanish lahzasi.</summary>
    public DateTimeOffset? PlanStartAt { get; private set; }

    /// <summary>Rejadagi tugash lahzasi — keyingi kalendar kuniga tushishi mumkin.</summary>
    public DateTimeOffset? PlanEndAt { get; private set; }

    public DateTimeOffset? FirstInAt { get; private set; }

    public DateTimeOffset? LastOutAt { get; private set; }

    /// <summary>Rejadagi daqiqalar: (reja tugashi − reja boshlanishi) − tanaffus.</summary>
    public int PlannedMinutes { get; private set; }

    public int WorkedMinutes { get; private set; }

    public int LateMinutes { get; private set; }

    public int EarlyLeaveMinutes { get; private set; }

    /// <summary>
    /// Ortiqcha ishlangan daqiqalar.
    /// </summary>
    /// <remarks>
    /// ⚠️ Hisoblanadi va SHU YERDA to'xtaydi — hech narsaga ko'paytirilmaydi.
    /// Oylik qoidalari (ortiqcha ish koeffitsienti, tungi qo'shimcha,
    /// bayramlar) hech qachon ta'riflanmagan; ularni taxmin qilish
    /// ishonchli ko'rinadigan, lekin NOTO'G'RI sonlar chiqarardi
    /// (SPEC §1.2 #2).
    /// </remarks>
    public int OvertimeMinutes { get; private set; }

    /// <summary>
    /// Tanaffus oynasi ichida binodan tashqarida o'tgan daqiqalar.
    /// </summary>
    /// <remarks>
    /// NEGA SAQLANADI, <see cref="WorkedMinutes"/> ichida yashirinib qolmaydi:
    /// egasining qarori bilan tanaffus ishlagan vaqtdan AYRILADI (SPEC
    /// §5.5.1). Shunda «8 soat turdi, nega 7 soat yozilgan?» degan savol
    /// albatta tug'iladi va javob tabel katagida KO'RINISHI kerak — yakuniy
    /// songa qarab uni qayta tiklab bo'lmaydi.
    /// </remarks>
    public int BreakTakenMinutes { get; private set; }

    /// <summary>
    /// Ishlagan vaqtdan ayrilgan tanaffus daqiqalari.
    /// </summary>
    /// <remarks>
    /// Tanaffus oynasi yo'q kunda doim 0 — qaysi vaqt tanaffus ekanini
    /// tizim o'zicha taxmin qilmaydi.
    /// </remarks>
    public int BreakDeductionMinutes { get; private set; }

    public int PassCount { get; private set; }

    /// <summary>Sakrash deb tashlangan takror o'qishlar soni.</summary>
    public int IgnoredPassCount { get; private set; }

    public AttendanceDayStatus Status { get; private set; }

    /// <summary>
    /// <see cref="WorkedMinutes"/> ning tasdiqlangan so'rovdan kelgan qismi.
    /// </summary>
    /// <remarks>
    /// Alohida saqlanadi: «8 soat ishladi» degan son terminaldan emas, HR
    /// tasdiqlagan so'rovdan ham kelishi mumkin va tabel katagi buni
    /// ko'rsatishi shart — aks holda raqam yana «kim yozdi» savoliga aylanardi.
    /// </remarks>
    public int CreditedMinutes { get; private set; }

    /// <summary>Kun «Erkin ish jadvali» bo'yicha hisoblanganmi — reja surati.</summary>
    /// <remarks>
    /// Saqlanadi, chunki tabel katagi kechikish o'rniga normani va kam ishlanganni
    /// ko'rsatishi kerak — keyin jadval o'zgarsa ham o'tgan oy o'zgarmaydi.
    /// </remarks>
    public bool IsFlexible { get; private set; }

    /// <summary>«Kam ishlangan» daqiqalar — erkin kunda normadan kami (SPEC-erkin-jadval §2.2).</summary>
    /// <remarks>
    /// Erkin kunda kechikish ham, erta ketish ham yo'q — xodim normani bajarmaganini
    /// faqat shu son ko'rsatadi. Qat'iy kunda doim 0.
    /// </remarks>
    public int ShortfallMinutes { get; private set; }

    /// <summary>«Shaxsiy vaqt» daqiqalari — tasdiqlangan vaqtli shaxsiy so'rov (SPEC-sorov-tahriri §5).</summary>
    /// <remarks>
    /// Alohida saqlanadi: shaxsiy soatlar ishlangan vaqtdan AYRILADI, va tabel «ishladi 6:00,
    /// shaxsiy 2:00» deb ko'rsatishi kerak — aks holda kamaygan son yana «nega» savoliga aylanardi.
    /// </remarks>
    public int PersonalMinutes { get; private set; }

    /// <summary>Oxirgi marta qachon hisoblangan.</summary>
    public DateTimeOffset ComputedAt { get; private set; }

    /// <summary>Kunni birinchi marta hisoblab yozadi.</summary>
    public static AttendanceDay Compute(
        Guid employeeId,
        DateOnly localDate,
        AttendanceDayFacts facts,
        DateTimeOffset computedAt)
    {
        var day = new AttendanceDay(NewId(), employeeId, localDate);
        day.Recompute(facts, computedAt);
        return day;
    }

    /// <summary>
    /// Kunni qaytadan hisoblangan natija bilan qayta yozadi.
    /// </summary>
    /// <remarks>
    /// Xodim va sana o'zgarmaydi — ular yozuvning KALITI
    /// (<c>ux_attendance_days_employee_date</c>). Boshqa kunni hisoblash
    /// degani boshqa yozuvni hisoblash degani.
    /// </remarks>
    public void Recompute(AttendanceDayFacts facts, DateTimeOffset computedAt)
    {
        ArgumentNullException.ThrowIfNull(facts);

        EmploymentId = facts.EmploymentId;
        LegalEntityId = facts.LegalEntityId;
        DepartmentId = facts.DepartmentId;
        PositionId = facts.PositionId;
        WorkScheduleId = facts.WorkScheduleId;
        PlanStartAt = facts.PlanStartAt;
        PlanEndAt = facts.PlanEndAt;
        FirstInAt = facts.FirstInAt;
        LastOutAt = facts.LastOutAt;
        PlannedMinutes = facts.PlannedMinutes;
        WorkedMinutes = facts.WorkedMinutes;
        LateMinutes = facts.LateMinutes;
        EarlyLeaveMinutes = facts.EarlyLeaveMinutes;
        OvertimeMinutes = facts.OvertimeMinutes;
        BreakTakenMinutes = facts.BreakTakenMinutes;
        BreakDeductionMinutes = facts.BreakDeductionMinutes;
        PassCount = facts.PassCount;
        IgnoredPassCount = facts.IgnoredPassCount;
        Status = facts.Status;
        CreditedMinutes = facts.CreditedMinutes;
        IsFlexible = facts.IsFlexible;
        ShortfallMinutes = facts.ShortfallMinutes;
        PersonalMinutes = facts.PersonalMinutes;
        ComputedAt = computedAt;
    }
}
