using Bonvi.Application.Attendance.Computation;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using static Bonvi.Application.Tests.Attendance.Computation.AttendanceScenario;

namespace Bonvi.Application.Tests.Attendance.Computation;

/// <summary>
/// Kun yozuvi — reja, kechikish, holat va «qator bo'lishi kerakmi»:
/// SPEC §5.4, §5.5 6–10-qadamlar, §5.6, §5.7 jadvali; §11.1 #32–#45, #50.
/// </summary>
/// <remarks>
/// Sukut sahna — ofis 09:00–18:00, yon berish 10/10, tanaffussiz: bu yerdagi
/// sonlar tanaffus ta'siridan xoli bo'lishi uchun. Tanaffus
/// <see cref="BreakDeductionTests"/> da.
/// </remarks>
public sealed class AttendanceDayBuilderTests
{
    // ────────────────────── Kechikish va erta ketish ──────────────────────

    [Fact]
    public void Togri_navbatlashgan_kun_kelgan_holatida_boladi()
    {
        var sahna = Office();
        sahna.Alternating(Monday, "09:00", "18:00");

        var kun = sahna.Day(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.ShouldStore.ShouldBeTrue();
        kun.Facts.WorkedMinutes.ShouldBe(540);
        kun.Facts.PlannedMinutes.ShouldBe(540);
        kun.Facts.OvertimeMinutes.ShouldBe(0);
        kun.Facts.PassCount.ShouldBe(2);
    }

    /// <summary>#32. Yon berish — chegara emas, ayriladigan qism: 09:15 − (09:00 + 10) = 5.</summary>
    [Fact]
    public void Yon_berishdan_keyin_kelgan_kechikish_yon_berishsiz_hisoblanadi()
    {
        var sahna = Office();
        sahna.Alternating(Monday, "09:15", "18:00");

        sahna.Day(Monday).Facts.LateMinutes.ShouldBe(5);
    }

    /// <summary>#33.</summary>
    [Fact]
    public void Yon_berish_ichida_kelgan_kechikmagan()
    {
        var sahna = Office();
        sahna.Alternating(Monday, "09:08", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.LateMinutes.ShouldBe(0);
        kun.Status.ShouldBe(AttendanceDayStatus.Present);
    }

    [Fact]
    public void Kechikish_ham_kesiladi()
    {
        var sahna = Office();
        sahna.Alternating(Monday, "09:15:59", "18:00");

        sahna.Day(Monday).Facts.LateMinutes.ShouldBe(5);
    }

    /// <summary>#34. (18:00 − 10) − 17:40 = 10.</summary>
    [Fact]
    public void Erta_ketish_yon_berishdan_keyin_hisoblanadi()
    {
        var sahna = Office();
        sahna.Alternating(Monday, "09:00", "17:40");

        var kun = sahna.Day(Monday).Facts;

        kun.EarlyLeaveMinutes.ShouldBe(10);
        kun.Status.ShouldBe(AttendanceDayStatus.Present, "kechikish va erta ketish — son, holat emas");
    }

    // ────────────────────── Reja hali tugamagan kun ──────────────────────

    /// <summary>
    /// Egasining qarori (2026-09-23): 11:30 da tushlikka chiqqan odam kun o'rtasida «380 daqiqa
    /// erta ketdi» bo'lib ko'rinmaydi — reja tugamaguncha erta ketish 0. Kechikish va holat odatdagidek.
    /// </summary>
    [Fact]
    public void Reja_tugamaguncha_erta_ketish_sanalmaydi()
    {
        var sahna = Office().Now(Monday, "12:00");
        sahna.Alternating(Monday, "09:15", "11:30");

        var kun = sahna.Day(Monday).Facts;

        kun.EarlyLeaveMinutes.ShouldBe(0, "18:00 dagi reja hali tugamagan");
        kun.LastOutAt.ShouldBe(At(Monday, "11:30"));
        kun.LateMinutes.ShouldBe(5, "kechikish qoidasi reja tugashini kutmaydi");
        kun.WorkedMinutes.ShouldBe(135);
        kun.Status.ShouldBe(AttendanceDayStatus.Present);
    }

    /// <summary>
    /// Reja tugash lahzasidan boshlab — odatdagi formula, yon berish bilan: (18:00 − 10) − 11:30 = 380.
    /// Bir soniya oldin hali 0.
    /// </summary>
    [Fact]
    public void Reja_tugagach_erta_ketish_odatdagidek_hisoblanadi()
    {
        var sahna = Office();
        sahna.Alternating(Monday, "09:00", "11:30");

        sahna.Now(Monday, "17:59:59").Day(Monday).Facts.EarlyLeaveMinutes.ShouldBe(0);
        sahna.Now(Monday, "18:00").Day(Monday).Facts.EarlyLeaveMinutes.ShouldBe(380, "chegara — reja tugash lahzasining o'zi");
        sahna.Now(Tuesday, "03:10").Day(Monday).Facts.EarlyLeaveMinutes.ShouldBe(380, "tungi qayta hisoblash");
    }

    /// <summary>
    /// Tungi smena 20:00–08:00: dushanbaning rejasi seshanba 08:00 da tugaydi. Seshanba 03:10 da
    /// dushanba O'TGAN sana, lekin reja hali tugamagan — erta ketish 0. Qoida sana bo'yicha emas,
    /// lahza bo'yicha. 08:00 dan keyin: (08:00 − 10) − 02:00 = 350.
    /// </summary>
    [Fact]
    public void Tungi_smenada_erta_ketish_ertasi_kuni_reja_tugagach_sanaladi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("20:00", "08:00"));
        sahna.In(Monday, "20:00");
        sahna.Out(Tuesday, "02:00");

        var tunda = sahna.Now(Tuesday, "03:10").Day(Monday).Facts;

        tunda.LastOutAt.ShouldBe(At(Tuesday, "02:00"), "chiqish dushanbaning smenasiga tushgan");
        tunda.EarlyLeaveMinutes.ShouldBe(0);
        tunda.Status.ShouldBe(AttendanceDayStatus.Present);

        sahna.Now(Tuesday, "08:00").Day(Monday).Facts.EarlyLeaveMinutes.ShouldBe(350);
        sahna.Now(Tuesday.AddDays(1), "03:10").Day(Monday).Facts.EarlyLeaveMinutes.ShouldBe(350);
    }

    /// <summary>#35 reja tugashidan oldin ham, keyin ham: chiqishi yozilmagan kunda erta ketish 0.</summary>
    [Fact]
    public void Chiqishi_yozilmagan_kunda_reja_tugagach_ham_erta_ketish_nol()
    {
        var sahna = Office();
        sahna.Alternating(Monday, "09:00", "12:00", "13:00");

        var kunduzi = sahna.Now(Monday, "13:30").Day(Monday).Facts;
        var tunda = sahna.Now(Tuesday, "03:10").Day(Monday).Facts;

        kunduzi.Status.ShouldBe(AttendanceDayStatus.MissingExit);
        kunduzi.EarlyLeaveMinutes.ShouldBe(0);

        tunda.Status.ShouldBe(AttendanceDayStatus.MissingExit);
        tunda.EarlyLeaveMinutes.ShouldBe(0, "qachon ketgani noma'lum — reja tugagani buni o'zgartirmaydi");
    }

    // ────────────────────── Toq o'tishlar ──────────────────────

    /// <summary>#35. Qachon ketgani noma'lum — erta ketishda ayblanmaydi.</summary>
    [Fact]
    public void Chiqishi_yozilmagan_kunda_erta_ketish_nol()
    {
        var sahna = Office();
        sahna.Alternating(Monday, "09:00", "12:00", "13:00");

        var kun = sahna.Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.MissingExit);
        kun.EarlyLeaveMinutes.ShouldBe(0, "12:00 dagi chiqish oxirgi voqea emas");
        kun.WorkedMinutes.ShouldBe(180);
    }

    /// <summary>#36.</summary>
    [Fact]
    public void Birinchi_otish_chiqish_bolsa_kirish_yozilmagan_holati()
    {
        var sahna = Office();
        sahna.Out(Monday, "18:00");

        var kun = sahna.Day(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.MissingEntry);
        kun.Facts.WorkedMinutes.ShouldBe(0);
        kun.ShouldStore.ShouldBeTrue();
    }

    [Fact]
    public void Kirish_yozilmagani_chiqish_yozilmaganidan_ustun()
    {
        var sahna = Office();
        sahna.Out(Monday, "08:00");
        sahna.In(Monday, "09:00");

        sahna.Day(Monday).Facts.Status.ShouldBe(AttendanceDayStatus.MissingEntry);
    }

    // ────────────────────── Tungi smena (#39, #40) ──────────────────────

    /// <summary>
    /// #39. 20:00–08:00, dushanba 19:52 → seshanba 08:07: BITTA qator, dushanbada.
    /// </summary>
    /// <remarks>
    /// ⚠️ SPEC §11.1 #39 «ortiqcha ish 7» deydi, lekin me'yoriy §5.5 10-qadam
    /// <c>max(0, worked − planned)</c> = 735 − 720 = <b>15</b>. «7» — faqat
    /// reja tugashidan keyingi qism (08:07 − 08:00), 8 daqiqalik erta kelishni
    /// unutgan. Me'yoriy formula amalga oshirildi.
    /// </remarks>
    [Fact]
    public void Tungi_smena_bitta_qator_bolib_boshlangan_kunga_tushadi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("20:00", "08:00"));
        sahna.In(Monday, "19:52");
        sahna.Out(Tuesday, "08:07");

        var kunlar = sahna.Days(Monday, Tuesday);
        var dushanba = kunlar[0].Facts;

        dushanba.Status.ShouldBe(AttendanceDayStatus.Present);
        dushanba.PassCount.ShouldBe(2);
        dushanba.WorkedMinutes.ShouldBe(735);
        dushanba.PlannedMinutes.ShouldBe(720);
        dushanba.OvertimeMinutes.ShouldBe(15);
        dushanba.LateMinutes.ShouldBe(0);
        dushanba.EarlyLeaveMinutes.ShouldBe(0);
        dushanba.PlanStartAt.ShouldBe(At(Monday, "20:00"));
        dushanba.PlanEndAt.ShouldBe(At(Tuesday, "08:00"));
        dushanba.PlanEndAt!.Value.TashkentDate().ShouldBe(Tuesday);

        kunlar[1].Facts.PassCount.ShouldBe(0, "08:07 dagi chiqish seshanbaga tushmaydi");
    }

    /// <summary>SPEC §5.7: tungi smenadan 08:05 da ketdi — kelgan, ortiqcha ish 5.</summary>
    [Fact]
    public void Tungi_smenadan_besh_daqiqa_kech_ketgan_ortiqcha_besh_daqiqa()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("20:00", "08:00"));
        sahna.In(Monday, "20:00");
        sahna.Out(Tuesday, "08:05");

        var kun = sahna.Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.OvertimeMinutes.ShouldBe(5);
    }

    /// <summary>#40. Chiqishi yozilmagan tungi smena: dushanba <c>MissingExit</c>, seshanbada qator yo'q.</summary>
    [Fact]
    public void Tungi_smenada_chiqish_yozilmasa_ertangi_kunda_qator_yoq()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("20:00", "08:00", daysOff: [2]));
        sahna.In(Monday, "19:52");

        var kunlar = sahna.Days(Monday, Tuesday);

        kunlar[0].Facts.Status.ShouldBe(AttendanceDayStatus.MissingExit);
        kunlar[0].ShouldStore.ShouldBeTrue();

        kunlar[1].ShouldStore.ShouldBeFalse();
        kunlar[1].Facts.Status.ShouldBe(AttendanceDayStatus.DayOff);
    }

    // ────────────────────── Jadval va dam olish kuni ──────────────────────

    /// <summary>
    /// #41. Jadval yo'q — ishlangan vaqt baribir hisoblanadi, reja/kechikish/erta ketish 0.
    /// </summary>
    /// <remarks>
    /// Ortiqcha ish 0: normasi noma'lum kunda ortiqcha ish ham noma'lum.
    /// Aks holda xulosa hisobotidagi ortiqcha ish ustuniga jadvalsiz
    /// kunlarning HAR BIR daqiqasi tushardi (koordinator qarori,
    /// 2026-09-17). Hisobotda bu kun «muammoli kun» sifatida ko'rinadi.
    /// </remarks>
    [Fact]
    public void Jadvalsiz_kunda_ishlangan_vaqt_hisoblanadi_reja_nol()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Alternating(Monday, "09:30", "18:00");

        var kun = sahna.Day(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.NoSchedule);
        kun.ShouldStore.ShouldBeTrue();
        kun.ScheduleTier.ShouldBeNull();
        kun.Facts.WorkScheduleId.ShouldBeNull();
        kun.Facts.PlanStartAt.ShouldBeNull();
        kun.Facts.WorkedMinutes.ShouldBe(510);
        kun.Facts.PlannedMinutes.ShouldBe(0);
        kun.Facts.LateMinutes.ShouldBe(0);
        kun.Facts.EarlyLeaveMinutes.ShouldBe(0);
        kun.Facts.OvertimeMinutes.ShouldBe(0);
    }

    [Fact]
    public void Jadvalsiz_va_otishsiz_kunda_qator_yoq()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();

        var kun = sahna.Day(Monday);

        kun.ShouldStore.ShouldBeFalse();
        kun.Facts.Status.ShouldBe(AttendanceDayStatus.NoSchedule);
    }

    /// <summary>
    /// Jadvalsiz kun — standart 24 soat (egasining qarori, 2026-10-12): o'tmay ketgan odam
    /// 22:00 da «ketdi», oraliq ishlangan vaqtga qo'shiladi.
    /// </summary>
    [Fact]
    public void Jadvalsiz_kunda_chiqishsiz_kirish_22_00_da_yopiladi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Unknowns(Monday, "09:00", "13:00", "14:00");

        var kun = sahna.Day(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.NoSchedule);
        kun.Facts.FirstInAt.ShouldBe(At(Monday, "09:00"));
        kun.Facts.LastOutAt.ShouldBe(At(Monday, "22:00"));
        AttendanceRules.IsNoScheduleAutoExit(Monday, kun.Facts.LastOutAt).ShouldBeTrue();
        kun.Facts.WorkedMinutes.ShouldBe(240 + 480);
        kun.Facts.OvertimeMinutes.ShouldBe(0);
    }

    [Fact]
    public void Jadvalsiz_bugun_22_00_gacha_kun_ochiq_turadi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Unknowns(Monday, "09:00");
        sahna.Now(Monday, "21:59");

        var kun = sahna.Day(Monday);

        kun.Facts.LastOutAt.ShouldBeNull();
        kun.Facts.WorkedMinutes.ShouldBe(0);

        sahna.Now(Monday, "22:00");

        sahna.Day(Monday).Facts.LastOutAt.ShouldBe(At(Monday, "22:00"));
    }

    [Fact]
    public void Jadvalsiz_kunda_haqiqiy_chiqish_bolsa_avtomatik_yopilish_yoq()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Unknowns(Monday, "09:00", "18:30");

        var kun = sahna.Day(Monday);

        kun.Facts.LastOutAt.ShouldBe(At(Monday, "18:30"));
        AttendanceRules.IsNoScheduleAutoExit(Monday, kun.Facts.LastOutAt).ShouldBeFalse();
        kun.Facts.WorkedMinutes.ShouldBe(570);
    }

    [Fact]
    public void Jadvalsiz_kunda_22_00_dan_keyingi_kirish_yopilmaydi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.In(Monday, "22:30");

        var kun = sahna.Day(Monday);

        kun.Facts.LastOutAt.ShouldBeNull();
        kun.Facts.WorkedMinutes.ShouldBe(0);
    }

    [Fact]
    public void Jadvali_bor_kunda_chiqishsiz_kirish_avtomatik_yopilmaydi()
    {
        var sahna = AttendanceScenario.Office();
        sahna.In(Monday, "09:00");

        var kun = sahna.Day(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.MissingExit);
        kun.Facts.LastOutAt.ShouldBeNull();
        kun.Facts.WorkedMinutes.ShouldBe(0);
    }

    /// <summary>#42.</summary>
    [Fact]
    public void Dam_olish_kunida_ishlagan_kechikish_va_erta_ketishsiz()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("09:00", "18:00", daysOff: [1]));
        sahna.Alternating(Monday, "11:00", "15:00");

        var kun = sahna.Day(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.DayOffWorked);
        kun.ShouldStore.ShouldBeTrue();
        kun.Facts.WorkedMinutes.ShouldBe(240);
        kun.Facts.PlannedMinutes.ShouldBe(0);
        kun.Facts.LateMinutes.ShouldBe(0);
        kun.Facts.EarlyLeaveMinutes.ShouldBe(0);
        kun.Facts.PlanStartAt.ShouldBeNull();
        kun.Facts.WorkScheduleId.ShouldNotBeNull("jadval topildi — kun shunchaki ish kuni emas");
    }

    [Fact]
    public void Dam_olish_kunida_otish_bolmasa_qator_yoq()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("09:00", "18:00", daysOff: [1]));

        var kun = sahna.Day(Monday);

        kun.ShouldStore.ShouldBeFalse();
        kun.Facts.Status.ShouldBe(AttendanceDayStatus.DayOff);
    }

    /// <summary>#43. Kelmagan odam «bo'sh katak» emas — qator yoziladi.</summary>
    [Fact]
    public void Ish_kunida_otish_bolmasa_kelmagan_qatori_yoziladi()
    {
        var sahna = Office();

        var kun = sahna.Day(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Absent);
        kun.ShouldStore.ShouldBeTrue();
        kun.Facts.WorkedMinutes.ShouldBe(0);
        kun.Facts.PlannedMinutes.ShouldBe(540);
        kun.Facts.LateMinutes.ShouldBe(0);
        kun.Facts.EarlyLeaveMinutes.ShouldBe(0);
    }

    // ────────────────────── Mehnat munosabati ──────────────────────

    /// <summary>
    /// #44. Ishdan ketgan, propuski hamon ishlaydi — qator yoziladi va holati
    /// <c>NotEmployed</c>: aynan HR ko'rishi kerak bo'lgan anomaliya, yashirilmaydi.
    /// </summary>
    [Fact]
    public void Ishdan_ketganning_otishi_yashirilmaydi_ishlamaydi_holatida_yoziladi()
    {
        var sahna = new AttendanceScenario();
        var munosabat = sahna.Employ();
        munosabat.Terminate(new DateOnly(2026, 9, 10), reasonId: null).IsSuccess.ShouldBeTrue();
        sahna.Assign(Schedule("09:00", "18:00"));
        sahna.Alternating(Monday, "09:00", "18:00");

        var kun = sahna.Day(Monday);

        kun.ShouldStore.ShouldBeTrue();
        kun.Facts.Status.ShouldBe(AttendanceDayStatus.NotEmployed);
        kun.Facts.EmploymentId.ShouldBeNull();
        kun.Facts.LegalEntityId.ShouldBeNull();
        kun.Facts.DepartmentId.ShouldBeNull();
        kun.Facts.WorkScheduleId.ShouldBeNull();
        kun.Facts.WorkedMinutes.ShouldBe(540, "dalil — soatlar ham ko'rinadi");
        kun.Facts.LateMinutes.ShouldBe(0);
    }

    [Fact]
    public void Ishdan_ketganning_otishsiz_kunida_qator_yoq()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ().Terminate(new DateOnly(2026, 9, 10), reasonId: null).IsSuccess.ShouldBeTrue();
        sahna.Assign(Schedule("09:00", "18:00"));

        var kun = sahna.Day(Monday);

        kun.ShouldStore.ShouldBeFalse();
        kun.Facts.Status.ShouldBe(AttendanceDayStatus.NotEmployed);
    }

    [Fact]
    public void Ishdan_ketgan_kunining_ozi_hali_ish_kuni()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ().Terminate(Monday, reasonId: null).IsSuccess.ShouldBeTrue();
        sahna.Assign(Schedule("09:00", "18:00"));

        var kunlar = sahna.Days(Monday, Tuesday);

        kunlar[0].Facts.Status.ShouldBe(AttendanceDayStatus.Absent);
        kunlar[1].Facts.Status.ShouldBe(AttendanceDayStatus.NotEmployed);
        kunlar[1].ShouldStore.ShouldBeFalse();
    }

    [Fact]
    public void Oy_orasida_ishga_kirganning_oldingi_kunlarida_qator_yoq()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ(hired: Tuesday);
        sahna.Assign(Schedule("09:00", "18:00"));

        var kunlar = sahna.Days(Monday, Tuesday);

        kunlar[0].ShouldStore.ShouldBeFalse();
        kunlar[0].Facts.Status.ShouldBe(AttendanceDayStatus.NotEmployed);
        kunlar[1].ShouldStore.ShouldBeTrue();
        kunlar[1].Facts.Status.ShouldBe(AttendanceDayStatus.Absent);
    }

    /// <summary>#45. Ikki munosabat — BITTA kun; jadval va surat asosiysidan.</summary>
    [Fact]
    public void Ikki_munosabatda_kun_bitta_va_asosiysidan_olinadi()
    {
        var sahna = new AttendanceScenario();
        var asosiyLavozim = Guid.CreateVersion7();
        var qoshimchaLavozim = Guid.CreateVersion7();
        var asosiyBolim = Guid.CreateVersion7();

        var asosiy = sahna.Employ(departmentId: asosiyBolim, positionId: asosiyLavozim);
        var qoshimcha = sahna.Employ(hired: new DateOnly(2025, 6, 1), positionId: qoshimchaLavozim);
        qoshimcha.SetPrimary(false);

        var ofis = Schedule("09:00", "18:00");
        var tungi = Schedule("20:00", "08:00");
        sahna.Assign(ofis, ScheduleScope.Position, asosiyLavozim);
        sahna.Assign(tungi, ScheduleScope.Position, qoshimchaLavozim);
        sahna.Alternating(Monday, "09:00", "18:00");

        var kun = sahna.Day(Monday);

        kun.Facts.EmploymentId.ShouldBe(asosiy.Id);
        kun.Facts.LegalEntityId.ShouldBe(asosiy.LegalEntityId);
        kun.Facts.DepartmentId.ShouldBe(asosiyBolim);
        kun.Facts.PositionId.ShouldBe(asosiyLavozim);
        kun.Facts.WorkScheduleId.ShouldBe(ofis.Id);
        kun.ScheduleTier.ShouldBe(ScheduleScope.Position);
        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Present);
    }

    // ────────────────────── O'tishlar ──────────────────────

    /// <summary>SPEC §5.7: soati 5 daqiqadan ko'p og'gan qurilmaning o'tishi odatdagidek hisoblanadi.</summary>
    [Fact]
    public void Soati_oggan_qurilmaning_otishi_odatdagidek_hisoblanadi()
    {
        var sahna = Office();

        foreach (var (time, direction) in new[] { ("09:00", PassDirection.In), ("18:00", PassDirection.Out) })
        {
            var otish = AttendancePass.Record(
                Guid.CreateVersion7(),
                AttendanceIdentityProvider.Verifix,
                "1001",
                At(Monday, time),
                receivedAt: At(Monday, time).AddMinutes(10),
                dedupeKey: $"kalit-{time}",
                direction: direction).Value;
            otish.Resolve(sahna.EmployeeId, At(Monday, time));
            otish.HasClockDrift.ShouldBeTrue();

            sahna.Pass(PassReading.From(otish));
        }

        var kun = sahna.Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.WorkedMinutes.ShouldBe(540);
    }

    /// <summary>
    /// SPEC §5.7: bog'lanmagan o'tish hech qaysi kunga tushmaydi. Hisoblovchi uni
    /// jimgina tashlamaydi ham, qabul ham qilmaydi — chaqiruvchining xatosi baland ovozda chiqadi.
    /// </summary>
    [Fact]
    public void Boglanmagan_otish_kunga_tushmaydi()
    {
        var sahna = Office();
        sahna.Pass(new PassReading(Guid.CreateVersion7(), EmployeeId: null, At(Monday, "09:00"), PassDirection.In));

        Should.Throw<ArgumentException>(() => sahna.Days(Monday));
    }

    [Fact]
    public void Boshqa_xodimning_otishi_bu_xodimga_yozilmaydi()
    {
        var sahna = Office();
        sahna.Pass(new PassReading(Guid.CreateVersion7(), Guid.CreateVersion7(), At(Monday, "09:00"), PassDirection.In));

        Should.Throw<ArgumentException>(() => sahna.Days(Monday));
    }

    // ────────────────────── Natija shakli ──────────────────────

    /// <summary>#50. Ikki marta hisoblash — bir xil natija (qayta hisoblash idempotent).</summary>
    [Fact]
    public void Ikki_marta_hisoblash_bir_xil_natija_beradi()
    {
        var sahna = Office(breakMinutes: 60, breakStart: "13:00", breakEnd: "14:00");
        sahna.Alternating(Monday, "09:12", "12:58", "13:44", "17:31");
        sahna.In(Tuesday, "08:59");

        var birinchi = sahna.Days(Monday, Tuesday);
        var ikkinchi = sahna.Days(Monday, Tuesday);

        ikkinchi.ShouldBe(birinchi);
    }

    [Fact]
    public void Sanalar_takrorsiz_va_tartib_bilan_qaytadi()
    {
        var sahna = Office();

        var kunlar = sahna.Days(Tuesday, Monday, Tuesday);

        kunlar.Select(kun => kun.LocalDate).ShouldBe(new[] { Monday, Tuesday });
    }

    /// <summary>
    /// Toshkentda 09:00 — UTC 04:00. Yozuvga UTC lahza tushadi: <c>timestamptz</c>
    /// boshqa siljishni qabul qilmaydi, «bazaga +5 yozish» esa bir lahzani ikki xil
    /// o'qiladigan qilardi.
    /// </summary>
    [Fact]
    public void Yoziladigan_lahzalar_UTC_da()
    {
        var sahna = Office();
        sahna.Alternating(Monday, "09:00", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.PlanStartAt.ShouldBe(new DateTimeOffset(2026, 9, 14, 4, 0, 0, TimeSpan.Zero));
        kun.PlanStartAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
        kun.PlanEndAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
        kun.FirstInAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
        kun.LastOutAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
    }

    // ────────────────────── Kelajak kunlar ──────────────────────

    /// <summary>
    /// Bugundan keyingi ish kuni «Kelmadi» EMAS: ertaga kelmaganini bugun
    /// ayta olmaymiz. Qator ham yozilmaydi.
    /// </summary>
    [Fact]
    public void Kelajakdagi_ish_kuni_kelmagan_deb_belgilanmaydi()
    {
        var sahna = Office().Today(Monday);

        var kunlar = sahna.Days(Monday, Tuesday);

        kunlar[0].Facts.Status.ShouldBe(AttendanceDayStatus.Absent, "bugungi kun odatdagidek baholanadi");
        kunlar[0].ShouldStore.ShouldBeTrue();

        kunlar[1].Facts.Status.ShouldBe(AttendanceDayStatus.Planned);
        kunlar[1].ShouldStore.ShouldBeFalse("kelajak kun uchun qator yaratilmaydi");
    }

    /// <summary>
    /// Kelajakdagi kunda o'tish bo'lsa (masalan tungi smenaning chiqishi)
    /// — u odatdagidek hisoblanadi va saqlanadi.
    /// </summary>
    [Fact]
    public void Kelajak_qoidasi_otishi_bor_kunga_tegmaydi()
    {
        var sahna = Office().Today(Monday);
        sahna.Alternating(Tuesday, "09:00", "18:00");

        var kun = sahna.Day(Tuesday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.ShouldStore.ShouldBeTrue();
    }

    /// <summary>
    /// Tasdiqlangan so'rov kelajak qoidasidan USTUN: keyingi haftaga
    /// olingan ruxsat tabelda bugundanoq «Sababli» bo'lib ko'rinadi va
    /// yozuvi saqlanadi — qaror BUGUN qabul qilingan.
    /// </summary>
    [Fact]
    public void Kelajakdagi_tasdiqlangan_sorov_sababli_bolib_saqlanadi()
    {
        var sahna = Office().Today(Monday);

        var kun = sahna.DaysExcused(new HashSet<DateOnly> { Tuesday }, Tuesday).ShouldHaveSingleItem();

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Excused);
        kun.ShouldStore.ShouldBeTrue();
    }

    [Fact]
    public void Otish_yuklanadigan_sanalar_ikki_tomonga_bir_kunga_kengayadi()
    {
        AttendanceDayBuilder.PassDatesNeeded(Monday, Tuesday).ShouldBe((Monday.AddDays(-1), Tuesday.AddDays(1)));
    }

    // ────────────────────── Ko'chirish sanasi (audit #1) ──────────────────────

    private static readonly DateOnly Wednesday = Monday.AddDays(2);

    /// <summary>
    /// Lavozim pog'onasida jadval: eski lavozim 08:00–17:00, yangisi 09:00–18:00; chorshanbadan
    /// ko'chirilgan. Ko'chirish maydonni joyida almashtiradi — ilgari seshanba ham yangi jadval
    /// bilan qurilib, 17:00 da ketgan odam «50 daqiqa erta ketdi» bo'lardi va kun yangi bo'limga
    /// ko'chardi.
    /// </summary>
    private static (AttendanceScenario Sahna, Guid EskiBolim, Guid YangiBolim, Guid EskiLavozim, Guid YangiLavozim, WorkSchedule Eski, WorkSchedule Yangi)
        Kochirilgan(DateOnly kuchgaKiradi)
    {
        var sahna = new AttendanceScenario();
        var eskiBolim = Guid.CreateVersion7();
        var yangiBolim = Guid.CreateVersion7();
        var eskiLavozim = Guid.CreateVersion7();
        var yangiLavozim = Guid.CreateVersion7();

        var munosabat = sahna.Employ(departmentId: eskiBolim, positionId: eskiLavozim);
        var eski = Schedule("08:00", "17:00");
        var yangi = Schedule("09:00", "18:00");
        sahna.Assign(eski, ScheduleScope.Position, eskiLavozim);
        sahna.Assign(yangi, ScheduleScope.Position, yangiLavozim);

        munosabat.Transfer(kuchgaKiradi, yangiBolim, yangiLavozim).IsSuccess.ShouldBeTrue();

        return (sahna, eskiBolim, yangiBolim, eskiLavozim, yangiLavozim, eski, yangi);
    }

    [Fact]
    public void Davr_ortasidagi_kochirishdan_oldingi_kun_eski_jadval_va_eski_bolim_bilan()
    {
        var (sahna, eskiBolim, yangiBolim, eskiLavozim, yangiLavozim, eski, yangi) = Kochirilgan(Wednesday);
        sahna.Alternating(Tuesday, "08:00", "17:00");
        sahna.Alternating(Wednesday, "09:00", "18:00");

        var kunlar = sahna.Days(Tuesday, Wednesday);
        var seshanba = kunlar[0].Facts;
        var chorshanba = kunlar[1].Facts;

        seshanba.WorkScheduleId.ShouldBe(eski.Id);
        seshanba.PlanStartAt.ShouldBe(At(Tuesday, "08:00"));
        seshanba.EarlyLeaveMinutes.ShouldBe(0, "17:00 — eski jadvalning oxiri");
        seshanba.DepartmentId.ShouldBe(eskiBolim, "surat — o'sha kungi bo'lim (SPEC §2.6)");
        seshanba.PositionId.ShouldBe(eskiLavozim);
        seshanba.Status.ShouldBe(AttendanceDayStatus.Present);

        chorshanba.WorkScheduleId.ShouldBe(yangi.Id, "kuchga kirish kunining o'zi — yangi joy");
        chorshanba.DepartmentId.ShouldBe(yangiBolim);
        chorshanba.PositionId.ShouldBe(yangiLavozim);
        chorshanba.EarlyLeaveMinutes.ShouldBe(0);
    }

    /// <summary>Tungi qayta hisoblash ko'chirishdan KEYIN o'tgan kunni yolg'iz hisoblasa ham natija o'sha.</summary>
    [Fact]
    public void Kochirishdan_keyin_qayta_hisoblangan_otgan_kun_ozgarmaydi()
    {
        var (sahna, eskiBolim, _, _, _, eski, _) = Kochirilgan(Wednesday);
        sahna.Alternating(Tuesday, "08:00", "17:00");

        var kun = sahna.Now(Wednesday.AddDays(1), "03:10").Day(Tuesday).Facts;

        kun.WorkScheduleId.ShouldBe(eski.Id);
        kun.EarlyLeaveMinutes.ShouldBe(0);
        kun.DepartmentId.ShouldBe(eskiBolim);
    }

    /// <summary>Oldinga sanalangan ko'chirish: buyruq kiritilgan, lekin kuchga kirmagan — bugun ham eski joy.</summary>
    [Fact]
    public void Oldinga_sanalangan_kochirish_sanasidan_oldin_qollanmaydi()
    {
        var (sahna, eskiBolim, yangiBolim, _, _, eski, yangi) = Kochirilgan(Monday.AddDays(14));
        sahna.Today(Tuesday);

        var bugun = sahna.Day(Tuesday).Facts;
        bugun.WorkScheduleId.ShouldBe(eski.Id);
        bugun.DepartmentId.ShouldBe(eskiBolim);

        var kuchgaKirgach = sahna.Day(Monday.AddDays(14)).Facts;
        kuchgaKirgach.WorkScheduleId.ShouldBe(yangi.Id);
        kuchgaKirgach.DepartmentId.ShouldBe(yangiBolim);
    }

    // ────────────────────── Bugun, smena boshlanmasidan oldin (audit #4) ──────────────────────

    /// <summary>
    /// 08:00 da 09:00 lik smenaning odami «Kelmadi» EMAS — panelning «Hali boshlanmagan»i bilan
    /// bir qoida. Qator ham yozilmaydi; roppa-rosa 09:00 dan — «Kelmadi» va qator.
    /// </summary>
    [Fact]
    public void Bugun_smena_boshlanmasidan_oldin_kelmadi_emas_boshlangach_kelmadi()
    {
        var sahna = Office();

        var ertalab = sahna.Now(Monday, "08:00").Day(Monday);
        ertalab.Facts.Status.ShouldBe(AttendanceDayStatus.Planned);
        ertalab.ShouldStore.ShouldBeFalse("boshlanmagan smena uchun qator yozilmaydi");

        var tungiHisoblash = sahna.Now(Monday, "03:10").Day(Monday);
        tungiHisoblash.Facts.Status.ShouldBe(AttendanceDayStatus.Planned, "03:10 dagi tungi hisoblash ham");
        tungiHisoblash.ShouldStore.ShouldBeFalse();

        var boshlanishda = sahna.Now(Monday, "09:00").Day(Monday);
        boshlanishda.Facts.Status.ShouldBe(AttendanceDayStatus.Absent, "yon berish kutilmaydi — panel kabi");
        boshlanishda.ShouldStore.ShouldBeTrue();
    }

    /// <summary>20:00 lik tungi smena xodimi kun bo'yi «Kelmadi» emas; kechagi smenasi esa o'tgan.</summary>
    [Fact]
    public void Tungi_smena_kunduzi_hali_boshlanmagan()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("20:00", "08:00"));

        var kunlar = sahna.Now(Monday, "14:00").Days(Monday.AddDays(-1), Monday);

        kunlar[0].Facts.Status.ShouldBe(AttendanceDayStatus.Absent, "yakshanba 20:00 da boshlangan smena — o'tgan");
        kunlar[1].Facts.Status.ShouldBe(AttendanceDayStatus.Planned);
        kunlar[1].ShouldStore.ShouldBeFalse();

        sahna.Now(Monday, "20:00").Day(Monday).Facts.Status.ShouldBe(AttendanceDayStatus.Absent);
    }

    /// <summary>Tasdiqlangan so'rov boshlanmagan smenada ham «Sababli» bo'lib saqlanadi (qaror bugun qabul qilingan).</summary>
    [Fact]
    public void Boshlanmagan_smenadagi_tasdiqlangan_sorov_sababli()
    {
        var sahna = Office().Now(Monday, "08:00");

        var kun = sahna.DaysExcused(new HashSet<DateOnly> { Monday }, Monday).ShouldHaveSingleItem();

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Excused);
        kun.ShouldStore.ShouldBeTrue();
    }

    /// <summary>Smenadan oldin kelgan odam — «Planned» emas, o'tishi bor kun odatdagidek.</summary>
    [Fact]
    public void Smenadan_oldin_kelgan_odamning_kuni_odatdagidek()
    {
        var sahna = Office().Now(Monday, "08:55");
        sahna.Unknown(Monday, "08:50");

        var kun = sahna.Day(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.MissingExit);
        kun.ShouldStore.ShouldBeTrue();
    }

    // ────────────────────── Kechagi ochiq smenaning kech chiqishi (egasining qarori, 2026-09-27) ──────────────────────

    /// <summary>
    /// Egasining misoli: 09:00–18:00; dushanba 09:00 kirgan, seshanba 00:20 chiqqan (dushanba
    /// oynasi 00:00 da tugagan, seshanbaniki 03:00 da boshlanadi). Ilgari dushanba «Chiqish yo'q»,
    /// seshanba esa 00:20, 08:55, 18:05 — «kirdi, chiqdi, kirdi» bo'lib 515 daqiqa edi.
    /// </summary>
    [Fact]
    public void Oynasiz_chiqish_kechagi_ochiq_kunga_tushadi()
    {
        var sahna = Office();
        sahna.Unknown(Monday, "09:00");
        sahna.Unknowns(Tuesday, "00:20", "08:55", "18:05");

        var kunlar = sahna.Days(Monday, Tuesday);
        var dushanba = kunlar[0].Facts;
        var seshanba = kunlar[1].Facts;

        dushanba.Status.ShouldBe(AttendanceDayStatus.Present);
        dushanba.LastOutAt.ShouldBe(At(Tuesday, "00:20"));
        dushanba.WorkedMinutes.ShouldBe(920, "09:00 → 00:20");
        dushanba.PassCount.ShouldBe(2);

        seshanba.Status.ShouldBe(AttendanceDayStatus.Present);
        seshanba.FirstInAt.ShouldBe(At(Tuesday, "08:55"));
        seshanba.WorkedMinutes.ShouldBe(550, "08:55 → 18:05");
        seshanba.PassCount.ShouldBe(2);
    }

    /// <summary>
    /// Bir o'tish ikki saqlangan kunda sanalmasligi uchun natija hisoblanayotgan oraliqqa bog'liq
    /// emas: seshanba yolg'iz (yuklovchi kabi, ±1 kun o'tishlar bilan) hisoblansa ham 00:20 unga
    /// tushmaydi (iflos navbat ±1 qo'shni bilan ikkalasini qayta hisoblaydi).
    /// </summary>
    [Fact]
    public void Kech_chiqish_qarori_hisoblanayotgan_oraliqqa_bogliq_emas()
    {
        var sahna = Office();
        sahna.Unknown(Monday, "09:00");
        sahna.Unknowns(Tuesday, "00:20", "08:55", "18:05");

        sahna.DaysAsLoaded(Tuesday).ShouldHaveSingleItem().Facts.PassCount.ShouldBe(2);
        sahna.DaysAsLoaded(Monday).ShouldHaveSingleItem().Facts.PassCount.ShouldBe(2);
    }

    /// <summary>
    /// «Kecha ochiqmi» savoliga kechaning OLDINGI kalendar kunidagi o'tishi javob bermaydi: dushanba
    /// 02:00 lik smenaning yakshanba 23:30 dagi kirishi seshanbani yolg'iz hisoblayotgan chaqiruvda
    /// yuklanmaydi (±1 kun). Uni hisobga olish natijani oraliqqa bog'lab, 01:00 dagi o'tishni bir
    /// hisoblashda dushanbaga, boshqasida seshanbaga yozardi.
    /// </summary>
    [Fact]
    public void Kechaning_oldingi_kungi_otishi_ochiqlik_savoliga_javob_bermaydi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule(
        [
            WorkingDay(1, "02:00", "10:00"),
            WorkingDay(2, "09:00", "18:00"),
            new WorkScheduleDayInput(3, IsWorking: false),
            new WorkScheduleDayInput(4, IsWorking: false),
            new WorkScheduleDayInput(5, IsWorking: false),
            new WorkScheduleDayInput(6, IsWorking: false),
            new WorkScheduleDayInput(7, IsWorking: false),
        ]));
        sahna.Unknown(Monday.AddDays(-1), "23:30");
        sahna.Unknown(Tuesday, "01:00");

        var birga = sahna.Days(Monday, Tuesday);
        var seshanbaYolgiz = sahna.DaysAsLoaded(Tuesday).ShouldHaveSingleItem();

        birga[0].Facts.PassCount.ShouldBe(1, "23:30 — dushanba oynasida");
        birga[1].Facts.PassCount.ShouldBe(1, "01:00 o'z sanasida");
        seshanbaYolgiz.Facts.ShouldBe(birga[1].Facts, "oraliqqa bog'liq emas");
    }

    /// <summary>Kecha yopilgan bo'lsa (ochiq kirish yo'q) — eski qoida: o'tish o'z sanasida qoladi.</summary>
    [Fact]
    public void Kecha_yopilgan_bolsa_oynasiz_otish_oz_sanasida_qoladi()
    {
        var sahna = Office();
        sahna.Unknowns(Monday, "09:00", "18:00");
        sahna.Unknowns(Tuesday, "00:20", "08:55", "18:05");

        var kunlar = sahna.Days(Monday, Tuesday);

        kunlar[0].Facts.PassCount.ShouldBe(2);
        kunlar[0].Facts.WorkedMinutes.ShouldBe(540);
        kunlar[1].Facts.PassCount.ShouldBe(3, "00:20 seshanbaning o'zida");
    }

    /// <summary>Kirish kechagi kunning chiqishi bo'la olmaydi — telefondagi «Kirish» o'z sanasida qoladi.</summary>
    [Fact]
    public void Malum_kirish_kechaga_otmaydi()
    {
        var sahna = Office();
        sahna.In(Monday, "09:00");
        sahna.In(Tuesday, "00:20");

        var kunlar = sahna.Days(Monday, Tuesday);

        kunlar[0].Facts.Status.ShouldBe(AttendanceDayStatus.MissingExit);
        kunlar[1].Facts.PassCount.ShouldBe(1);
    }

    /// <summary>Bugungi smenaning oynasi (03:00) boshlangach o'tish oynaga tushadi — kechaga emas.</summary>
    [Fact]
    public void Bugungi_oyna_boshlangach_otish_bugunga_tushadi()
    {
        var sahna = Office();
        sahna.Unknown(Monday, "09:00");
        sahna.Unknown(Tuesday, "03:00");

        var kunlar = sahna.Days(Monday, Tuesday);

        kunlar[0].Facts.Status.ShouldBe(AttendanceDayStatus.MissingExit);
        kunlar[1].Facts.PassCount.ShouldBe(1);
    }

    /// <summary>Ikkinchi kech o'tish birinchisi yopgan kechani ochiq deb ko'rmaydi.</summary>
    [Fact]
    public void Ikkinchi_kech_otish_yopilgan_kechaga_qoshilmaydi()
    {
        var sahna = Office();
        sahna.Unknown(Monday, "09:00");
        sahna.Unknowns(Tuesday, "00:20", "00:25");

        var kunlar = sahna.Days(Monday, Tuesday);

        kunlar[0].Facts.LastOutAt.ShouldBe(At(Tuesday, "00:20"));
        kunlar[0].Facts.PassCount.ShouldBe(2);
        kunlar[1].Facts.PassCount.ShouldBe(1, "00:25 o'z sanasida");
    }

    /// <summary>
    /// Bugun dam olish kuni: chegara «bugun ham kechagi vaqtda boshlanardi» (03:00). 00:20 — kechaga,
    /// kechqurungi 20:00 esa o'z sanasida dam olish kunida ishlash, 35 soatlik «smena» emas.
    /// </summary>
    [Fact]
    public void Bugun_dam_olish_kuni_bolsa_chegara_bir_sutka()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("09:00", "18:00", daysOff: [2]));
        sahna.Unknown(Monday, "09:00");
        sahna.Unknown(Tuesday, "00:20");

        sahna.Days(Monday, Tuesday)[0].Facts.LastOutAt.ShouldBe(At(Tuesday, "00:20"));

        var boshqa = new AttendanceScenario();
        boshqa.Employ();
        boshqa.Assign(Schedule("09:00", "18:00", daysOff: [2]));
        boshqa.Unknown(Monday, "09:00");
        boshqa.Unknown(Tuesday, "20:00");

        var kunlar = boshqa.Days(Monday, Tuesday);
        kunlar[0].Facts.Status.ShouldBe(AttendanceDayStatus.MissingExit);
        kunlar[1].Facts.Status.ShouldBe(AttendanceDayStatus.DayOffWorked);
    }
}
