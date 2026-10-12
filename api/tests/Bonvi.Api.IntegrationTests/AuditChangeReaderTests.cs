using System.Text.Json;
using Bonvi.Application.Audit;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Jurnal qatorini maydonlarga ajratish — o'qish tomonining SOF qismi (SPEC-rollar-va-audit §8.3):
/// pul juftini birlashtirish, sir belgisi, kengaytirishdan oldingi qatorlar va qiymat turlari.
/// </summary>
/// <remarks>Bazaga ulanmaydi: EF modeli faqat konfiguratsiyalardan quriladi.</remarks>
public sealed class AuditChangeReaderTests
{
    private static readonly Lazy<IModel> Model = new(() =>
    {
        var sozlamalar = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only")
            .UseSnakeCaseNamingConvention()
            .Options;

        using var kontekst = new AppDbContext(sozlamalar);
        return kontekst.Model;
    });

    private static IReadOnlyEntityType Turi<T>() => Model.Value.FindEntityType(typeof(T)).ShouldNotBeNull();

    // ────────────────────── Pul ──────────────────────

    /// <summary><c>Salary.Amount</c> + <c>Salary.Currency</c> → bitta «money» maydon.</summary>
    [Fact]
    public void Pul_jufti_bitta_maydonga_birlashadi()
    {
        var maydonlar = AuditChangeReader.Read(
            Turi<Employment>(),
            AuditAction.Update,
            """{"Salary.Amount":4000000,"Salary.Currency":"UZS","Rate":1.0}""",
            """{"Salary.Amount":4500000,"Salary.Currency":"UZS","Rate":0.5}""",
            ["Salary.Amount", "Salary.Currency", "Rate"]);

        maydonlar.Select(maydon => maydon.Name).ShouldBe(["Salary", "Rate"]);

        var oklad = maydonlar[0];
        oklad.IsMoney.ShouldBeTrue();
        Summa(oklad.Old).ShouldBe((4_000_000m, "UZS"));
        Summa(oklad.New).ShouldBe((4_500_000m, "UZS"));
        AuditChangeReader.ValueTypeOf(Turi<Employment>(), AuditCatalog.Audited[typeof(Employment)], oklad)
            .ShouldBe(AuditValueTypes.Money);
    }

    /// <summary>
    /// Tahrirda faqat summa o'zgarsa valyuta qatorda yo'q — qiymat baribir pul shaklida
    /// (valyutasi <c>null</c>); okladi yo'q edi — <c>null</c>.
    /// </summary>
    [Fact]
    public void Faqat_summa_ozgarsa_ham_pul_shaklida_okladsiz_tomon_bosh()
    {
        var tahrir = AuditChangeReader.Read(
            Turi<EmploymentEvent>(),
            AuditAction.Update,
            """{"ToSalary.Amount":5000000}""",
            """{"ToSalary.Amount":5500000}""",
            ["ToSalary.Amount"]).ShouldHaveSingleItem();

        tahrir.Name.ShouldBe("ToSalary");
        tahrir.Old.ShouldNotBeNull().GetProperty("amount").GetDecimal().ShouldBe(5_000_000m);
        tahrir.New.ShouldNotBeNull().GetProperty("currency").ValueKind.ShouldBe(JsonValueKind.Null);

        var birinchi = AuditChangeReader.Read(
            Turi<Employment>(),
            AuditAction.Update,
            """{"Salary.Amount":null,"Salary.Currency":null}""",
            """{"Salary.Amount":3000000,"Salary.Currency":"UZS"}""",
            ["Salary.Amount", "Salary.Currency"]).ShouldHaveSingleItem();

        birinchi.Old.ShouldBeNull();
        Summa(birinchi.New).ShouldBe((3_000_000m, "UZS"));
    }

    /// <summary>Pul bo'lmagan murakkab xossa (<c>FullName</c>) birlashtirilmaydi.</summary>
    [Fact]
    public void Ism_qismlari_alohida_maydon_bolib_qoladi()
    {
        var maydonlar = AuditChangeReader.Read(
            Turi<Employee>(),
            AuditAction.Update,
            """{"FullName.LastName":"Aliyev"}""",
            """{"FullName.LastName":"Valiyev"}""",
            ["FullName.LastName"]);

        var familiya = maydonlar.ShouldHaveSingleItem();
        familiya.Name.ShouldBe("FullName.LastName");
        familiya.IsMoney.ShouldBeFalse();
        AuditChangeReader.ValueTypeOf(Turi<Employee>(), AuditCatalog.Audited[typeof(Employee)], familiya)
            .ShouldBe(AuditValueTypes.Text);
    }

    // ────────────────────── Sir ──────────────────────

    [Fact]
    public void Yozishda_yashirilgan_qiymat_sir_deb_taniladi()
    {
        var maydonlar = AuditChangeReader.Read(
            Turi<User>(),
            AuditAction.Update,
            """{"PasswordHash":"***"}""",
            """{"PasswordHash":"***"}""",
            ["PasswordHash"]);

        AuditChangeReader.IsWriteMasked(maydonlar.ShouldHaveSingleItem()).ShouldBeTrue();

        var oddiy = AuditChangeReader.Read(Turi<User>(), AuditAction.Create, null, """{"PhotoKey":"a.webp"}""", ["PhotoKey"]);
        AuditChangeReader.IsWriteMasked(oddiy.ShouldHaveSingleItem()).ShouldBeFalse();
    }

    // ────────────────────── Eski qatorlar ──────────────────────

    /// <summary>
    /// Kengaytirishdan oldingi tahrir qatori: ikkala tomonda TO'LIQ nusxa, maydonlar ro'yxati
    /// yo'q — faqat farq qilgan kalit qoladi.
    /// </summary>
    [Fact]
    public void Eski_tahrir_qatorida_faqat_farq_qilgan_kalit_qoladi()
    {
        var maydonlar = AuditChangeReader.Read(
            Turi<Role>(),
            AuditAction.Update,
            """{"code":"hr_manager","name":"HR menejeri","permissions":["audit.view","catalog.view"]}""",
            """{"code":"hr_manager","name":"HR bo'limi","permissions":["audit.view","catalog.view"]}""",
            changedFields: null);

        var nom = maydonlar.ShouldHaveSingleItem();
        nom.Name.ShouldBe("name");
        nom.Old.ShouldNotBeNull().GetString().ShouldBe("HR menejeri");
        nom.New.ShouldNotBeNull().GetString().ShouldBe("HR bo'limi");
    }

    [Fact]
    public void Eski_yaratish_qatorida_hamma_kalit_va_turi_JSONdan()
    {
        var maydonlar = AuditChangeReader.Read(
            Turi<User>(),
            AuditAction.Create,
            null,
            """{"login":"+998900000000","fullName":"Sinov Hisob","role":"hr_manager"}""",
            changedFields: null);

        maydonlar.Select(maydon => maydon.Name).ShouldBe(["login", "fullName", "role"]);
        maydonlar.ShouldAllBe(maydon => maydon.Old == null);
        AuditChangeReader.ValueTypeOf(Turi<User>(), AuditCatalog.Audited[typeof(User)], maydonlar[0]).ShouldBe(AuditValueTypes.Text);
    }

    /// <summary>Buzuq yoki obyekt bo'lmagan JSON — maydonsiz, istisnosiz (qator baribir chiziladi).</summary>
    [Fact]
    public void Buzuq_JSON_qatorni_yiqitmaydi()
    {
        AuditChangeReader.Read(null, AuditAction.Update, "{buzuq", "[1,2]", null).ShouldBeEmpty();
    }

    /// <summary>Hodisa tafsiloti (eksport) — EF turisiz, turi JSON qiymatidan.</summary>
    [Fact]
    public void Hodisa_tafsiloti_maydonlarga_ajraladi()
    {
        var maydonlar = AuditChangeReader.Read(
            null,
            AuditAction.Export,
            null,
            """{"filters":{"status":"active","hasSearch":true},"rows":12}""",
            null);

        maydonlar.Select(maydon => maydon.Name).ShouldBe(["filters", "rows"]);
        AuditChangeReader.ValueTypeOf(null, null, maydonlar[0]).ShouldBe(AuditValueTypes.Json);
        AuditChangeReader.ValueTypeOf(null, null, maydonlar[1]).ShouldBe(AuditValueTypes.Number);
    }

    // ────────────────────── Qiymat turlari ──────────────────────

    [Theory]
    [InlineData(typeof(Employee), "BirthDate", "\"1990-05-01\"", AuditValueTypes.Date)]
    [InlineData(typeof(Employee), "Pinfl", "\"30105901234567\"", AuditValueTypes.Text)]
    [InlineData(typeof(Employee), "NeedsReview", "true", AuditValueTypes.Bool)]
    [InlineData(typeof(Employee), "DirectionIds", "[\"0199d1a0-0000-7000-8000-000000000001\"]", AuditValueTypes.List)]
    [InlineData(typeof(Employment), "Status", "\"Terminated\"", AuditValueTypes.Enum)]
    [InlineData(typeof(Employment), "Rate", "0.5", AuditValueTypes.Number)]
    [InlineData(typeof(Employment), "DepartmentId", "\"0199d1a0-0000-7000-8000-000000000001\"", AuditValueTypes.Ref)]
    [InlineData(typeof(User), "RoleIds", "[\"0199d1a0-0000-7000-8000-000000000001\"]", AuditValueTypes.List)]
    [InlineData(typeof(Role), "Permissions", "[\"audit.view\"]", AuditValueTypes.List)]
    [InlineData(typeof(AbsenceRequest), "DecidedAt", "\"2026-10-01T05:00:00.0000000+00:00\"", AuditValueTypes.DateTime)]
    [InlineData(typeof(AbsenceRequest), "FromTime", "\"09:00:00\"", AuditValueTypes.Text)]
    public void Qiymat_turi_EF_xossasidan(Type entity, string maydon, string qiymat, string kutilgan)
    {
        var turi = Model.Value.FindEntityType(entity).ShouldNotBeNull();
        var qoida = AuditCatalog.Audited[entity];

        var maydonlar = AuditChangeReader.Read(turi, AuditAction.Create, null, $$"""{"{{maydon}}":{{qiymat}}}""", [maydon]);

        AuditChangeReader.ValueTypeOf(turi, qoida, maydonlar.ShouldHaveSingleItem()).ShouldBe(kutilgan);
    }

    private static (decimal Amount, string? Currency) Summa(JsonElement? qiymat)
    {
        var obyekt = qiymat.ShouldNotBeNull();
        return (obyekt.GetProperty("amount").GetDecimal(), obyekt.GetProperty("currency").GetString());
    }
}
