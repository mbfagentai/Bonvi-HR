using Bonvi.Application.Abstractions;
using Bonvi.Application.Audit;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Identity;
using NSubstitute;

namespace Bonvi.Application.Tests.Audit;

/// <summary>
/// Audit jurnalini o'qish — ko'rinish qoidalari va filtrlarni tekshirish (SPEC-rollar-va-audit
/// §7.10, §8.3). Bazasiz: o'qish modeli soxta, tekshiriladigani — unga NIMA uzatilishi.
/// </summary>
public sealed class AuditQueryTests
{
    private static readonly DateOnly Bugun = new(2026, 10, 12);

    private readonly IAuditReadModel _oqish = Substitute.For<IAuditReadModel>();
    private readonly IClock _soat = Substitute.For<IClock>();
    private AuditListFilter? _uzatilgan;

    public AuditQueryTests()
    {
        _soat.Today.Returns(Bugun);

        _oqish
            .ListAsync(Arg.Do<AuditListFilter>(filter => _uzatilgan = filter), Arg.Any<CancellationToken>())
            .Returns(new AuditPage([], 0));

        _oqish.FindEntityName(Arg.Any<string>())
            .Returns(call => string.Equals(call.Arg<string>(), "employmentevent", StringComparison.OrdinalIgnoreCase)
                ? "EmploymentEvent"
                : null);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ────────────────────── Ko'rinish ──────────────────────

    /// <summary>
    /// Har bir bo'lim AYNAN o'z kirish kodi (katalogdagi eng quyi darajaning kodlari) bilan
    /// ochiladi — davomat ham, keyin qo'shiladigan bo'lim ham, ro'yxat qo'lda yozilmagan.
    /// </summary>
    [Fact]
    public void Har_bir_bolim_oz_kirish_kodi_bilan_ochiladi()
    {
        foreach (var bolim in PermissionCatalog.Modules)
        {
            var kodlar = bolim.Levels[0].Permissions.ToArray();

            AuditVisibility.ViewerOf(Foydalanuvchi(kodlar)).Modules.ToArray()
                .ShouldBe([bolim.Key], customMessage: $"«{bolim.Key}» — {string.Join(", ", kodlar)}");
        }

        AuditVisibility.ViewerOf(Foydalanuvchi(Permissions.AttendanceView)).Modules.ToArray()
            .ShouldBe([AccessModuleKeys.Attendance]);
    }

    /// <summary>Yuqori daraja kodi bo'lsa-yu, kirish kodi bo'lmasa — bo'lim yopiq.</summary>
    [Fact]
    public void Kirish_kodisiz_bolim_yopiq()
    {
        AuditVisibility.ViewerOf(Foydalanuvchi(Permissions.EmployeesCreate, Permissions.EmploymentSalary)).Modules
            .ShouldBeEmpty();
    }

    /// <summary>
    /// Shaxsiy ma'lumot — alohida ruxsat bilan YOKI tahrirlash huquqida o'z-o'zidan (SPEC §3.4);
    /// faqat ko'radigan rolda yo'q.
    /// </summary>
    [Fact]
    public void Shaxsiy_malumot_tahrirlash_huquqida_oz_ozidan_bor()
    {
        AuditVisibility.ViewerOf(Foydalanuvchi(Permissions.EmployeesView)).PersonalData.ShouldBeFalse();
        AuditVisibility.ViewerOf(Foydalanuvchi(Permissions.EmployeesView, Permissions.EmployeesPersonalView)).PersonalData.ShouldBeTrue();
        AuditVisibility.ViewerOf(Foydalanuvchi(Permissions.EmployeesView, Permissions.EmployeesEdit)).PersonalData.ShouldBeTrue();
    }

    /// <summary>Qo'shimcha haq — oklad ruxsatisiz ochilmaydi (kartadagi qoida bilan bir xil).</summary>
    [Fact]
    public void Qoshimcha_haq_oklad_ruxsatisiz_ochilmaydi()
    {
        AuditVisibility.ViewerOf(Foydalanuvchi(Permissions.SalaryExtraView)).ExtraPay.ShouldBeFalse();
        AuditVisibility.ViewerOf(Foydalanuvchi(Permissions.SalaryView)).ExtraPay.ShouldBeFalse();

        var ikkalasi = AuditVisibility.ViewerOf(Foydalanuvchi(Permissions.SalaryView, Permissions.SalaryExtraView));
        ikkalasi.Salary.ShouldBeTrue();
        ikkalasi.ExtraPay.ShouldBeTrue();
    }

    [Fact]
    public void Xavfsizlik_hodisalari_faqat_users_manage_bilan()
    {
        AuditVisibility.ViewerOf(Foydalanuvchi(Permissions.AuditView, Permissions.RolesManage)).Security.ShouldBeFalse();
        AuditVisibility.ViewerOf(Foydalanuvchi(Permissions.UsersManage)).Security.ShouldBeTrue();
    }

    // ────────────────────── Ro'yxat filtri ──────────────────────

    /// <summary>Davr berilmasa — oxirgi 30 kun, Toshkent kunlari bilan: [boshi 00:00 +05, oxiri+1 00:00 +05).</summary>
    [Fact]
    public async Task Davr_berilmasa_oxirgi_30_kun_Toshkent_vaqtida()
    {
        (await Royxat().HandleAsync(new ListAuditEntriesQuery(), Ct)).IsSuccess.ShouldBeTrue();

        var filtr = _uzatilgan.ShouldNotBeNull();
        filtr.From.ShouldBe(new DateTimeOffset(2026, 9, 12, 19, 0, 0, TimeSpan.Zero));
        filtr.To.ShouldBe(new DateTimeOffset(2026, 10, 12, 19, 0, 0, TimeSpan.Zero));
        filtr.From!.Value.Offset.ShouldBe(TimeSpan.Zero, "Npgsql timestamptz ga faqat UTC yozadi");
        filtr.Changes.ShouldBeTrue();
        filtr.Security.ShouldBeFalse();
        (filtr.Page, filtr.PageSize).ShouldBe((1, 50));
    }

    /// <summary>Faqat bitta chegara berilsa ham davr yopiq: oxiri — bugun, boshi — oxiridan 30 kun oldin.</summary>
    [Fact]
    public async Task Bitta_chegara_berilsa_ikkinchisi_qoida_bilan_toladi()
    {
        await Royxat().HandleAsync(new ListAuditEntriesQuery(To: new DateOnly(2026, 3, 31)), Ct);

        _uzatilgan.ShouldNotBeNull().From.ShouldBe(new DateTimeOffset(2026, 3, 1, 19, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Boshi_oxiridan_keyin_bolsa_davr_notogri()
    {
        var natija = await Royxat().HandleAsync(
            new ListAuditEntriesQuery(From: new DateOnly(2026, 10, 2), To: new DateOnly(2026, 10, 1)),
            Ct);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("audit.davr_notogri");
        await _oqish.DidNotReceive().ListAsync(Arg.Any<AuditListFilter>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Aql doirasidan tashqari sana — 500 emas, 400 (<c>AddDays</c> DateOnly chegarasidan oshardi).</summary>
    [Theory]
    [InlineData(1, 1, 1, null)]
    [InlineData(null, null, null, 9999)]
    public async Task Chegaradan_tashqari_sana_davr_notogri(int? fromYear, int? fromMonth, int? fromDay, int? toYear)
    {
        var natija = await Royxat().HandleAsync(
            new ListAuditEntriesQuery(
                From: fromYear is { } y ? new DateOnly(y, fromMonth!.Value, fromDay!.Value) : null,
                To: toYear is { } t ? new DateOnly(t, 12, 31) : null),
            Ct);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("audit.davr_notogri");
    }

    /// <summary>Notanish filtr qiymati jimgina e'tiborsiz qolmaydi — 400 va parametr nomi.</summary>
    [Theory]
    [InlineData("module", "xodimlar")]
    [InlineData("action", "Ochirish")]
    [InlineData("action", "7")]
    [InlineData("action", "Create,Update")]
    [InlineData("action", "-1")]
    [InlineData("kind", "everything")]
    [InlineData("actor", "admin")]
    [InlineData("actor", "00000000-0000-0000-0000-000000000000")]
    [InlineData("entity", "Xodim")]
    public async Task Notanish_filtr_qiymati_rad_etiladi(string parametr, string qiymat)
    {
        var sorov = parametr switch
        {
            "module" => new ListAuditEntriesQuery(Module: qiymat),
            "action" => new ListAuditEntriesQuery(Action: qiymat),
            "kind" => new ListAuditEntriesQuery(Kind: qiymat),
            "actor" => new ListAuditEntriesQuery(Actor: qiymat),
            _ => new ListAuditEntriesQuery(Entity: qiymat),
        };

        var natija = await Royxat().HandleAsync(sorov, Ct);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("audit.filtr_notogri");
        natija.Error.Details["parameter"].ShouldBe(parametr);
    }

    /// <summary>Qiymatlar registrsiz qabul qilinadi va kanonik ko'rinishda uzatiladi.</summary>
    [Fact]
    public async Task Filtrlar_normallashtirib_uzatiladi()
    {
        var foydalanuvchi = Guid.CreateVersion7();

        await Royxat(Permissions.UsersManage).HandleAsync(
            new ListAuditEntriesQuery(
                Actor: foydalanuvchi.ToString(),
                Module: " Employment ",
                Action: "update",
                Entity: "employmentevent",
                EntityId: " 42 ",
                Kind: "ALL"),
            Ct);

        var filtr = _uzatilgan.ShouldNotBeNull();
        filtr.ActorUserId.ShouldBe(foydalanuvchi);
        filtr.ActorSystem.ShouldBeFalse();
        filtr.Module.ShouldBe(AccessModuleKeys.Employment);
        filtr.Action.ShouldBe(AuditAction.Update);
        filtr.Entity.ShouldBe("EmploymentEvent");
        filtr.EntityId.ShouldBe("42");
        (filtr.Changes, filtr.Security).ShouldBe((true, true));
    }

    [Fact]
    public async Task Tizim_bajaruvchisi_system_sozi_bilan()
    {
        await Royxat().HandleAsync(new ListAuditEntriesQuery(Actor: "System"), Ct);

        var filtr = _uzatilgan.ShouldNotBeNull();
        filtr.ActorSystem.ShouldBeTrue();
        filtr.ActorUserId.ShouldBeNull();
    }

    /// <summary>
    /// «security»/«all» — faqat <c>users.manage</c> bilan; ruxsatsiz so'rov xato emas,
    /// «changes» bo'lib ishlaydi (SPEC §8.3).
    /// </summary>
    [Theory]
    [InlineData("security")]
    [InlineData("all")]
    public async Task Xavfsizlik_korinishi_ruxsatsiz_ozgarishlar_bolib_ishlaydi(string kind)
    {
        await Royxat(Permissions.EmployeesView).HandleAsync(new ListAuditEntriesQuery(Kind: kind), Ct);
        (_uzatilgan.ShouldNotBeNull().Changes, _uzatilgan.Security).ShouldBe((true, false));

        await Royxat(Permissions.UsersManage).HandleAsync(new ListAuditEntriesQuery(Kind: kind), Ct);
        _uzatilgan.ShouldNotBeNull().Security.ShouldBeTrue();
        _uzatilgan.Changes.ShouldBe(kind == "all");
    }

    [Theory]
    [InlineData(0, null, 1, 50)]
    [InlineData(-5, 0, 1, 1)]
    [InlineData(3, 500, 3, 200)]
    public async Task Sahifa_chegaralari_jimgina_qisiladi(int? sahifa, int? hajm, int kutilganSahifa, int kutilganHajm)
    {
        var natija = await Royxat().HandleAsync(new ListAuditEntriesQuery(Page: sahifa, PageSize: hajm), Ct);

        (_uzatilgan.ShouldNotBeNull().Page, _uzatilgan.PageSize).ShouldBe((kutilganSahifa, kutilganHajm));
        (natija.Value.Page, natija.Value.PageSize).ShouldBe((kutilganSahifa, kutilganHajm));
    }

    [Fact]
    public async Task Sahifalar_soni_jamidan_hisoblanadi()
    {
        _oqish.ListAsync(Arg.Any<AuditListFilter>(), Arg.Any<CancellationToken>()).Returns(new AuditPage([], 101));

        var natija = await Royxat().HandleAsync(new ListAuditEntriesQuery(PageSize: 50), Ct);

        natija.Value.Total.ShouldBe(101);
        natija.Value.PageCount.ShouldBe(3);
    }

    // ────────────────────── Bitta yozuv va xodim tarixi ──────────────────────

    /// <summary>Topilmagan yoki ko'rinmaydigan yozuv — bitta javob: 404 <c>audit.topilmadi</c>.</summary>
    [Fact]
    public async Task Korinmaydigan_yozuv_topilmadi_deb_qaytadi()
    {
        _oqish.GetAsync(Arg.Any<Guid>(), Arg.Any<AuditViewer>(), Arg.Any<CancellationToken>())
            .Returns((AuditEntryDetailsDto?)null);

        var natija = await new GetAuditEntryQueryHandler(_oqish, Foydalanuvchi(Permissions.AuditView))
            .HandleAsync(new GetAuditEntryQuery(Guid.CreateVersion7()), Ct);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("audit.topilmadi");
    }

    /// <summary>Xodim tarixi — faqat o'zgarishlar, davr chegarasiz, shu xodim bo'yicha.</summary>
    [Fact]
    public async Task Xodim_tarixi_faqat_ozgarishlar_va_davrsiz()
    {
        var xodim = Guid.CreateVersion7();

        var natija = await new ListEmployeeHistoryQueryHandler(_oqish, Foydalanuvchi(Permissions.UsersManage))
            .HandleAsync(new ListEmployeeHistoryQuery(xodim, Module: "EMPLOYMENT"), Ct);

        natija.IsSuccess.ShouldBeTrue();

        var filtr = _uzatilgan.ShouldNotBeNull();
        filtr.EmployeeId.ShouldBe(xodim);
        filtr.Module.ShouldBe(AccessModuleKeys.Employment);
        (filtr.Changes, filtr.Security).ShouldBe((true, false), "kirish hodisalari xodimga bog'lanmaydi");
        filtr.From.ShouldBeNull();
        filtr.To.ShouldBeNull();
    }

    [Fact]
    public async Task Xodim_tarixida_notanish_bolim_rad_etiladi()
    {
        var natija = await new ListEmployeeHistoryQueryHandler(_oqish, Foydalanuvchi())
            .HandleAsync(new ListEmployeeHistoryQuery(Guid.CreateVersion7(), Module: "oklad"), Ct);

        natija.Error.Code.ShouldBe("audit.filtr_notogri");
    }

    // ────────────────────── Yordamchilar ──────────────────────

    private ListAuditEntriesQueryHandler Royxat(params string[] ruxsatlar)
        => new(_oqish, Foydalanuvchi(ruxsatlar), _soat);

    private static ICurrentUser Foydalanuvchi(params string[] ruxsatlar)
    {
        var toplam = ruxsatlar.ToHashSet(StringComparer.Ordinal);
        var user = Substitute.For<ICurrentUser>();
        user.HasPermission(Arg.Any<string>()).Returns(call => toplam.Contains(call.Arg<string>()));
        return user;
    }
}
