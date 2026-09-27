using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Application.Tests.Attendance.Computation;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using static Bonvi.Application.Tests.Attendance.Computation.AttendanceScenario;

namespace Bonvi.Application.Tests.Attendance.Scheduling;

/// <summary>
/// Jadvalni hal qilish — SPEC §5.2; §11.1 #48, #49.
/// </summary>
public sealed class ScheduleResolverTests
{
    private static readonly Guid Lavozim = Guid.CreateVersion7();

    // ────────────────────── Pog'onalar (#48) ──────────────────────

    [Fact]
    public void Xodim_lavozimdan_lavozim_bolimdan_bolim_sukutdan_ustun()
    {
        var sahna = new AttendanceScenario();
        var bolim = sahna.Department("Ombor");
        var munosabat = sahna.Employ(departmentId: bolim.Id, positionId: Lavozim);

        var sukut = Schedule("08:00", "17:00");
        var bolimniki = Schedule("09:00", "18:00");
        var lavozimniki = Schedule("20:00", "08:00");
        var shaxsiy = Schedule("10:00", "19:00");

        sahna.Assign(sukut);
        Resolve(sahna, munosabat).Schedule.ShouldBe(sukut);

        sahna.Assign(bolimniki, ScheduleScope.Department, bolim.Id);
        Resolve(sahna, munosabat).Schedule.ShouldBe(bolimniki);

        sahna.Assign(lavozimniki, ScheduleScope.Position, Lavozim);
        Resolve(sahna, munosabat).Schedule.ShouldBe(lavozimniki, "qorovul qaysi bo'limda bo'lmasin — qorovul smenasi");

        sahna.Assign(shaxsiy, ScheduleScope.Employee, sahna.EmployeeId);
        var natija = Resolve(sahna, munosabat);
        natija.Schedule.ShouldBe(shaxsiy);
        natija.Tier.ShouldBe(ScheduleScope.Employee);
    }

    [Fact]
    public void Hech_qanday_biriktirma_bolmasa_jadval_yoq()
    {
        var sahna = new AttendanceScenario();
        var munosabat = sahna.Employ(positionId: Lavozim);

        sahna.Resolver().Resolve(munosabat, Monday).ShouldBeNull();
    }

    [Fact]
    public void Ota_bolimning_jadvali_ichki_bolimga_ham_tegishli()
    {
        var sahna = new AttendanceScenario();
        var departament = sahna.Department("7-Departament");
        var zavod = sahna.Department("Metan zavod", departament);
        var bolim = sahna.Department("Plazma bo'limi", zavod);
        var munosabat = sahna.Employ(departmentId: bolim.Id);

        var jadval = Schedule("09:00", "18:00");
        sahna.Assign(jadval, ScheduleScope.Department, departament.Id);

        var natija = Resolve(sahna, munosabat);

        natija.Schedule.ShouldBe(jadval);
        natija.Tier.ShouldBe(ScheduleScope.Department);
    }

    /// <summary>
    /// Eng yaqin ota-bo'lim yutadi — departamentga KEYINROQ qo'yilgan jadval ham
    /// ichki bo'limning o'z jadvalini bosib ketmaydi.
    /// </summary>
    [Fact]
    public void Eng_yaqin_ota_bolim_kech_sanali_uzoq_ota_bolimdan_ustun()
    {
        var sahna = new AttendanceScenario();
        var departament = sahna.Department("7-Departament");
        var zavod = sahna.Department("Metan zavod", departament);
        var bolim = sahna.Department("Plazma bo'limi", zavod);
        var munosabat = sahna.Employ(departmentId: bolim.Id);

        var zavodniki = Schedule("07:00", "16:00");
        var departamentniki = Schedule("09:00", "18:00");
        sahna.Assign(zavodniki, ScheduleScope.Department, zavod.Id, from: LongAgo);
        sahna.Assign(departamentniki, ScheduleScope.Department, departament.Id, from: new DateOnly(2026, 9, 1));

        Resolve(sahna, munosabat).Schedule.ShouldBe(zavodniki);
    }

    [Fact]
    public void Lavozimi_va_bolimi_yoq_munosabat_sukut_jadvalini_oladi()
    {
        var sahna = new AttendanceScenario();
        var munosabat = sahna.Employ();
        var sukut = Schedule("09:00", "18:00");
        sahna.Assign(sukut);
        sahna.Assign(Schedule("20:00", "08:00"), ScheduleScope.Position, Lavozim);

        var natija = Resolve(sahna, munosabat);

        natija.Schedule.ShouldBe(sukut);
        natija.Tier.ShouldBe(ScheduleScope.Default);
    }

    // ────────────────────── Pog'ona ichida (#49) ──────────────────────

    [Fact]
    public void Pogona_ichida_sanada_kuchdagi_eng_kech_biriktirma_yutadi()
    {
        var sahna = new AttendanceScenario();
        var munosabat = sahna.Employ();
        var eski = Schedule("08:00", "17:00");
        var yangi = Schedule("09:00", "18:00");
        sahna.Assign(eski, from: LongAgo);
        sahna.Assign(yangi, from: new DateOnly(2026, 9, 10));

        var resolver = sahna.Resolver();

        resolver.Resolve(munosabat, Monday)!.Schedule.ShouldBe(yangi);
        resolver.Resolve(munosabat, new DateOnly(2026, 9, 9))!.Schedule.ShouldBe(eski);
    }

    [Fact]
    public void Sanasi_teng_bolsa_keyin_yaratilgani_yutadi()
    {
        var sahna = new AttendanceScenario();
        var munosabat = sahna.Employ();
        var birinchi = Schedule("08:00", "17:00");
        var ikkinchi = Schedule("09:00", "18:00");

        sahna.Assign(birinchi).CreatedAt = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
        sahna.Assign(ikkinchi).CreatedAt = new DateTimeOffset(2026, 9, 1, 11, 0, 0, TimeSpan.Zero);

        Resolve(sahna, munosabat).Schedule.ShouldBe(ikkinchi);
    }

    [Fact]
    public void Tugash_sanasi_shu_kunni_ham_oz_ichiga_oladi()
    {
        var sahna = new AttendanceScenario();
        var munosabat = sahna.Employ();
        var sukut = Schedule("08:00", "17:00");
        var vaqtincha = Schedule("09:00", "18:00");
        sahna.Assign(sukut);
        sahna.Assign(vaqtincha, ScheduleScope.Employee, sahna.EmployeeId, from: Monday, to: Monday);

        var resolver = sahna.Resolver();

        resolver.Resolve(munosabat, Monday)!.Schedule.ShouldBe(vaqtincha);
        resolver.Resolve(munosabat, Tuesday)!.Schedule.ShouldBe(sukut, "muddati tugagan pog'ona pastkisiga o'tadi");
    }

    [Fact]
    public void Ochirilgan_biriktirma_hisobga_olinmaydi()
    {
        var sahna = new AttendanceScenario();
        var munosabat = sahna.Employ();
        var sukut = Schedule("08:00", "17:00");
        sahna.Assign(sukut);
        sahna.Assign(Schedule("09:00", "18:00"), ScheduleScope.Employee, sahna.EmployeeId).DeletedAt = DateTimeOffset.UtcNow;

        Resolve(sahna, munosabat).Schedule.ShouldBe(sukut);
    }

    /// <summary>Faolsizlantirish — tanlov ro'yxatidan yashirish; amaldagi biriktirma jimgina to'xtamaydi.</summary>
    [Fact]
    public void Faol_bolmagan_jadval_baribir_hal_qilinadi()
    {
        var sahna = new AttendanceScenario();
        var munosabat = sahna.Employ();
        var jadval = Schedule("09:00", "18:00");
        jadval.Deactivate();
        sahna.Assign(jadval);

        Resolve(sahna, munosabat).Schedule.ShouldBe(jadval);
    }

    [Fact]
    public void Natijada_shu_sananing_rejasi_bor()
    {
        var sahna = new AttendanceScenario();
        var munosabat = sahna.Employ();
        sahna.Assign(Schedule("09:00", "18:00"));

        var natija = Resolve(sahna, munosabat);

        natija.Day.Date.ShouldBe(Monday);
        natija.Day.PlanStartAt.ShouldBe(At(Monday, "09:00"));
    }

    /// <summary>Lavozim pog'onasi SHU SANADAGI lavozim bilan qidiriladi — ko'chirishdan oldingi kun eski lavozimning jadvali.</summary>
    [Fact]
    public void Lavozim_pogonasi_sanadagi_lavozim_bilan()
    {
        var sahna = new AttendanceScenario();
        var yangiLavozim = Guid.CreateVersion7();
        var munosabat = sahna.Employ(positionId: Lavozim);
        var eski = Schedule("08:00", "17:00");
        var yangi = Schedule("09:00", "18:00");
        sahna.Assign(eski, ScheduleScope.Position, Lavozim);
        sahna.Assign(yangi, ScheduleScope.Position, yangiLavozim);
        munosabat.Transfer(Tuesday, null, yangiLavozim).IsSuccess.ShouldBeTrue();

        var resolver = sahna.Resolver();

        resolver.Resolve(munosabat, Monday)!.Schedule.ShouldBe(eski);
        resolver.Resolve(munosabat, Tuesday)!.Schedule.ShouldBe(yangi);
    }

    /// <summary>Bo'lim pog'onasi ham SHU SANADAGI bo'lim bilan.</summary>
    [Fact]
    public void Bolim_pogonasi_sanadagi_bolim_bilan()
    {
        var sahna = new AttendanceScenario();
        var eskiBolim = sahna.Department("Ombor");
        var yangiBolim = sahna.Department("Sex");
        var munosabat = sahna.Employ(departmentId: eskiBolim.Id);
        var eski = Schedule("08:00", "17:00");
        var yangi = Schedule("09:00", "18:00");
        sahna.Assign(eski, ScheduleScope.Department, eskiBolim.Id);
        sahna.Assign(yangi, ScheduleScope.Department, yangiBolim.Id);
        munosabat.Transfer(Tuesday, yangiBolim.Id, null).IsSuccess.ShouldBeTrue();

        var resolver = sahna.Resolver();

        resolver.Resolve(munosabat, Monday)!.Schedule.ShouldBe(eski);
        resolver.Resolve(munosabat, Tuesday)!.Schedule.ShouldBe(yangi);
    }

    // ────────────────────── Yuklovchi xatolari ──────────────────────

    /// <summary>Jadvali yuklanmagan biriktirma xodimni jimgina pastki pog'onaga tushirmaydi.</summary>
    [Fact]
    public void Biriktirma_jadvali_yuklanmagan_bolsa_istisno()
    {
        var jadval = Schedule("09:00", "18:00");
        var biriktirma = ScheduleAssignment.Assign(jadval.Id, ScheduleScope.Default, null, LongAgo).Value;

        Should.Throw<InvalidOperationException>(
            () => new ScheduleResolver([biriktirma], [], new DepartmentTree([])));
    }

    [Fact]
    public void Halqali_bolim_daraxti_osilib_qolmaydi()
    {
        var birinchi = Department.Create("A").Value;
        var ikkinchi = Department.Create("B", birinchi.Id).Value;
        birinchi.MoveTo(ikkinchi.Id).IsSuccess.ShouldBeTrue();

        var daraxt = new DepartmentTree([birinchi, ikkinchi]);

        daraxt.SelfAndAncestors(ikkinchi.Id).ShouldBe(new[] { ikkinchi.Id, birinchi.Id });
    }

    [Fact]
    public void Daraxtda_yoq_bolim_faqat_ozini_qaytaradi()
    {
        var yoq = Guid.CreateVersion7();

        new DepartmentTree([]).SelfAndAncestors(yoq).ShouldBe(new[] { yoq });
    }

    private static ScheduleResolution Resolve(AttendanceScenario sahna, Employment munosabat)
        => sahna.Resolver().Resolve(munosabat, Monday).ShouldNotBeNull();
}
