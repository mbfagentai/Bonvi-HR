using Bonvi.Application.Me;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;

namespace Bonvi.Application.Tests.Me;

/// <summary>
/// Bo'lim boshlig'ining qamrovi — daraxt, ildizlar va «a'zomi» qoidasi (SPEC-jamoam §B).
/// </summary>
/// <remarks>
/// Qamrov — «Jamoam» eshigining butun xavfsizlik chegarasi: bu yerda bitta qo'shni bo'lim
/// ortiqcha tushsa, boshliq begona zavodning odamlarini ko'radi. Shu sababli har bir chekka
/// alohida qadalgan.
/// </remarks>
public sealed class TeamScopeTests
{
    private static readonly Guid Boshliq = Guid.NewGuid();
    private static readonly Guid Firma = Guid.NewGuid();

    /// <summary>
    /// «7-Departament» ostida ikki zavod; Metan ostida ikki sex, sexning ichida yana bittasi.
    /// </summary>
    private sealed class Daraxt
    {
        public Department Departament { get; } = Department.Create("7-Departament").Value;

        public Department Metan { get; }

        public Department Propan { get; }

        public Department Plazma { get; }

        public Department Kesish { get; }

        public Department Smena { get; }

        public Daraxt()
        {
            Metan = Department.Create("Metan zavod", Departament.Id).Value;
            Propan = Department.Create("Propan zavod", Departament.Id).Value;
            Plazma = Department.Create("Plazma sexi", Metan.Id).Value;
            Kesish = Department.Create("Kesish sexi", Metan.Id).Value;
            Smena = Department.Create("1-smena", Plazma.Id).Value;
        }

        public IReadOnlyList<Department> Hammasi => [Departament, Metan, Propan, Plazma, Kesish, Smena];
    }

    [Fact]
    public void Boshliq_bolmagan_xodimning_qamrovi_bosh()
    {
        var daraxt = new Daraxt();

        var qamrov = TeamScope.Of(Boshliq, daraxt.Hammasi);

        qamrov.IsHead.ShouldBeFalse();
        qamrov.Roots.ShouldBeEmpty();
        qamrov.Departments.ShouldBeEmpty();
        qamrov.Contains(daraxt.Metan.Id).ShouldBeFalse();
    }

    [Fact]
    public void Qamrov_bolim_va_uning_barcha_ichki_bolimlari_qoshni_zavod_kirmaydi()
    {
        var daraxt = new Daraxt();
        daraxt.Metan.AssignHead(Boshliq);

        var qamrov = TeamScope.Of(Boshliq, daraxt.Hammasi);

        qamrov.IsHead.ShouldBeTrue();
        qamrov.Roots.ShouldBe([daraxt.Metan.Id]);

        // Daraxt tartibi: ildiz, keyin chuqurlik bo'yicha — ichma-ich sex o'z otasining ostida.
        qamrov.Departments.Select(bolim => bolim.Id).ShouldBe(
            [daraxt.Metan.Id, daraxt.Plazma.Id, daraxt.Smena.Id, daraxt.Kesish.Id]);

        qamrov.Contains(daraxt.Smena.Id).ShouldBeTrue("ichma-ich sex ham qamrovda");
        qamrov.Contains(daraxt.Propan.Id).ShouldBeFalse("qo'shni zavod");
        qamrov.Contains(daraxt.Departament.Id).ShouldBeFalse("ota bo'lim");
    }

    [Fact]
    public void Ichma_ich_ikki_bolim_boshligi_bitta_ildiz_oladi_alohidalari_ikkita()
    {
        var daraxt = new Daraxt();
        daraxt.Metan.AssignHead(Boshliq);
        daraxt.Smena.AssignHead(Boshliq);
        daraxt.Propan.AssignHead(Boshliq);

        var qamrov = TeamScope.Of(Boshliq, daraxt.Hammasi);

        // 1-smena Metan ichida — ikkinchi marta sanalmaydi; Propan — o'z daraxti.
        qamrov.Roots.ShouldBe([daraxt.Metan.Id, daraxt.Propan.Id], ignoreOrder: true);
        qamrov.Departments.Count.ShouldBe(5);
        qamrov.Departments.Select(bolim => bolim.Id).ShouldBeUnique();
        qamrov.Contains(daraxt.Departament.Id).ShouldBeFalse();
    }

    [Fact]
    public void Faolsiz_bolim_ham_hisoblanadi()
    {
        var daraxt = new Daraxt();
        daraxt.Plazma.AssignHead(Boshliq);
        daraxt.Plazma.Deactivate();
        daraxt.Smena.Deactivate();

        var qamrov = TeamScope.Of(Boshliq, daraxt.Hammasi);

        qamrov.IsHead.ShouldBeTrue("huquqni HR boshliqni olib tashlab yopadi, faolsizlantirib emas");
        qamrov.Departments.Select(bolim => bolim.Id).ShouldBe([daraxt.Plazma.Id, daraxt.Smena.Id]);
    }

    [Fact]
    public void Ochirilgan_oraliq_bolim_zanjirni_uzadi()
    {
        var daraxt = new Daraxt();
        daraxt.Metan.AssignHead(Boshliq);

        // Plazma o'chirilgan — ro'yxatda yo'q (yumshoq o'chirish filtri). Uning bolasi 1-smena
        // rekursiv CTE da ham Metan daraxtiga tushmaydi.
        IReadOnlyList<Department> royxat = [daraxt.Departament, daraxt.Metan, daraxt.Propan, daraxt.Kesish, daraxt.Smena];

        var qamrov = TeamScope.Of(Boshliq, royxat);

        qamrov.Departments.Select(bolim => bolim.Id).ShouldBe([daraxt.Metan.Id, daraxt.Kesish.Id]);
        qamrov.Contains(daraxt.Smena.Id).ShouldBeFalse();
    }

    [Fact]
    public void Halqali_daraxtda_cheksiz_aylanmaydi_va_huquq_yoqolmaydi()
    {
        var a = Department.Create("A").Value;
        var b = Department.Create("B", a.Id).Value;
        a.MoveTo(b.Id).IsSuccess.ShouldBeTrue();
        a.AssignHead(Boshliq);
        b.AssignHead(Boshliq);

        var qamrov = TeamScope.Of(Boshliq, [a, b]);

        qamrov.IsHead.ShouldBeTrue();
        qamrov.Departments.Select(bolim => bolim.Id).ShouldBe([a.Id, b.Id], ignoreOrder: true);
    }

    // ────────── A'zomi ──────────

    [Fact]
    public void Azo_davrga_tegadigan_qamrovdagi_munosabati_bor_xodim()
    {
        var daraxt = new Daraxt();
        daraxt.Metan.AssignHead(Boshliq);
        var qamrov = TeamScope.Of(Boshliq, daraxt.Hammasi);

        var oktabr = (From: new DateOnly(2026, 10, 1), To: new DateOnly(2026, 10, 31));

        // Ichma-ich sexda, sentabrdan beri
        qamrov.Covers([Munosabat(daraxt.Smena.Id, new DateOnly(2026, 9, 1))], oktabr.From, oktabr.To).ShouldBeTrue();

        // Oy o'rtasida kelgan
        qamrov.Covers([Munosabat(daraxt.Kesish.Id, new DateOnly(2026, 10, 20))], oktabr.From, oktabr.To).ShouldBeTrue();

        // Qo'shni zavodda
        qamrov.Covers([Munosabat(daraxt.Propan.Id, new DateOnly(2026, 9, 1))], oktabr.From, oktabr.To).ShouldBeFalse();

        // Bo'limsiz munosabat
        qamrov.Covers([Munosabat(null, new DateOnly(2026, 9, 1))], oktabr.From, oktabr.To).ShouldBeFalse();

        // Keyingi oyda keladi
        qamrov.Covers([Munosabat(daraxt.Metan.Id, new DateOnly(2026, 11, 1))], oktabr.From, oktabr.To).ShouldBeFalse();
    }

    [Fact]
    public void Ishdan_ketish_kuni_qamraladi_undan_keyingi_kun_yoq()
    {
        var daraxt = new Daraxt();
        daraxt.Metan.AssignHead(Boshliq);
        var qamrov = TeamScope.Of(Boshliq, daraxt.Hammasi);

        var munosabat = Munosabat(daraxt.Plazma.Id, new DateOnly(2026, 1, 1));
        munosabat.Terminate(new DateOnly(2026, 9, 30), reasonId: null).IsSuccess.ShouldBeTrue();

        qamrov.Covers([munosabat], new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30)).ShouldBeTrue();
        qamrov.Covers([munosabat], new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)).ShouldBeFalse();
    }

    [Fact]
    public void Ochirilgan_munosabat_azo_qilmaydi()
    {
        var daraxt = new Daraxt();
        daraxt.Metan.AssignHead(Boshliq);
        var qamrov = TeamScope.Of(Boshliq, daraxt.Hammasi);

        var munosabat = Munosabat(daraxt.Plazma.Id, new DateOnly(2026, 1, 1));
        munosabat.DeletedAt = DateTimeOffset.UtcNow;

        qamrov.Covers([munosabat], new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)).ShouldBeFalse();
    }

    // ────────── Har bir sanadagi bo'lim ──────────

    /// <summary>
    /// Qo'shni zavoddan 15-oktabrda kelgan: yangi boshliq faqat 15-dan boshlab ko'radi —
    /// odamning qo'shni zavodda o'tgan kunlari unga tegishli emas.
    /// </summary>
    [Fact]
    public void Qamrovga_kochib_kelgan_xodim_faqat_kochish_kunidan_boshlab_qamraladi()
    {
        var daraxt = new Daraxt();
        daraxt.Metan.AssignHead(Boshliq);
        var qamrov = TeamScope.Of(Boshliq, daraxt.Hammasi);

        var munosabat = Munosabat(daraxt.Propan.Id, new DateOnly(2026, 1, 1));
        munosabat.Transfer(new DateOnly(2026, 10, 15), daraxt.Plazma.Id, newPositionId: null).IsSuccess.ShouldBeTrue();

        qamrov.CoversOn([munosabat], new DateOnly(2026, 10, 14)).ShouldBeFalse();
        qamrov.CoversOn([munosabat], new DateOnly(2026, 10, 15)).ShouldBeTrue("ko'chish kuni — allaqachon yangi joy");

        qamrov.DaysCovered([munosabat], new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31))
            .Order().ShouldBe(Kunlar(new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 31)));

        // Sentabr butunlay qo'shni zavodda — oy umuman qamrovda emas.
        qamrov.Covers([munosabat], new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)).ShouldBeFalse();
    }

    /// <summary>
    /// Qamrovdan 15-oktabrda chiqib ketgan: eski boshliq 14-gacha bo'lgan kunlarni ko'radi,
    /// keyingilarini yo'q — joriy bo'lim qo'shni zavod bo'lsa ham.
    /// </summary>
    [Fact]
    public void Qamrovdan_kochib_ketgan_xodim_kochishgacha_qamraladi_keyin_yoq()
    {
        var daraxt = new Daraxt();
        daraxt.Metan.AssignHead(Boshliq);
        var qamrov = TeamScope.Of(Boshliq, daraxt.Hammasi);

        var munosabat = Munosabat(daraxt.Kesish.Id, new DateOnly(2026, 1, 1));
        munosabat.Transfer(new DateOnly(2026, 10, 15), daraxt.Propan.Id, newPositionId: null).IsSuccess.ShouldBeTrue();

        qamrov.Contains(munosabat.DepartmentId!.Value).ShouldBeFalse("joriy bo'lim — qo'shni zavod");

        qamrov.CoversOn([munosabat], new DateOnly(2026, 10, 14)).ShouldBeTrue();
        qamrov.CoversOn([munosabat], new DateOnly(2026, 10, 15)).ShouldBeFalse();

        qamrov.DaysCovered([munosabat], new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31))
            .Order().ShouldBe(Kunlar(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 14)));

        qamrov.Covers([munosabat], new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)).ShouldBeFalse();
    }

    [Fact]
    public void Qamrov_ichida_kochgan_xodim_butun_oy_qamraladi()
    {
        var daraxt = new Daraxt();
        daraxt.Metan.AssignHead(Boshliq);
        var qamrov = TeamScope.Of(Boshliq, daraxt.Hammasi);

        var munosabat = Munosabat(daraxt.Kesish.Id, new DateOnly(2026, 1, 1));
        munosabat.Transfer(new DateOnly(2026, 10, 15), daraxt.Smena.Id, newPositionId: null).IsSuccess.ShouldBeTrue();

        qamrov.DaysCovered([munosabat], new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31))
            .Count.ShouldBe(31);
    }

    /// <summary>
    /// Ikki munosabat: biri qamrovda, ikkinchisi qo'shni zavodda — qamrovdagisining kunlari
    /// yetadi (o'rindoshlik ham a'zolik).
    /// </summary>
    [Fact]
    public void Bir_nechta_munosabatdan_qamrovdagisi_yetadi()
    {
        var daraxt = new Daraxt();
        daraxt.Metan.AssignHead(Boshliq);
        var qamrov = TeamScope.Of(Boshliq, daraxt.Hammasi);

        var asosiy = Munosabat(daraxt.Propan.Id, new DateOnly(2026, 1, 1));
        var orindosh = Munosabat(daraxt.Plazma.Id, new DateOnly(2026, 10, 20));

        qamrov.DaysCovered([asosiy, orindosh], new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31))
            .Order().ShouldBe(Kunlar(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 31)));
    }

    private static List<DateOnly> Kunlar(DateOnly from, DateOnly to)
    {
        var kunlar = new List<DateOnly>();
        for (var kun = from; kun <= to; kun = kun.AddDays(1))
        {
            kunlar.Add(kun);
        }

        return kunlar;
    }

    private static Employment Munosabat(Guid? bolimId, DateOnly qabul)
        => Employment.Hire(Guid.NewGuid(), Firma, qabul, bolimId).Value;
}
