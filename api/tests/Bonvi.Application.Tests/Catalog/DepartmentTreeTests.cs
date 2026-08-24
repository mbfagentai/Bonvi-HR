using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Departments;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Organization;
using NSubstitute;

namespace Bonvi.Application.Tests.Catalog;

/// <summary>
/// Bo'limlar daraxti va ko'chirish qoidalarining testlari.
/// </summary>
/// <remarks>
/// Bu yerdagi eng muhim test — halqa tekshiruvi. U buzilsa, hech qanday
/// funksional test yiqilmaydi, lekin bazadagi daraxt bir kun yopiq halqaga
/// aylanadi va uni aylanib chiquvchi butun kod (yo'l, ro'yxat, hisobot)
/// cheksiz rekursiyaga tushadi.
/// </remarks>
public sealed class DepartmentTreeTests
{
    private readonly IDepartmentRepository _departments = Substitute.For<IDepartmentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly DepartmentDefinition _definition = new();

    private MoveDepartmentCommandHandler Kochiruvchi => new(_departments, _definition, _unitOfWork);

    private GetDepartmentTreeQueryHandler Daraxt => new(_departments);

    // ────────── Halqa tekshiruvi ──────────

    [Fact]
    public async Task Bolimni_oz_avlodiga_kochirib_bolmaydi()
    {
        var ota = Department.Create("7-Departament").Value;
        var bola = Department.Create("Metan zavod", ota.Id).Value;

        _departments.GetByIdAsync(ota.Id, Arg.Any<CancellationToken>()).Returns(ota);
        _departments.ExistsAsync(bola.Id, Arg.Any<CancellationToken>()).Returns(true);
        _departments
            .GetDescendantIdsAsync(ota.Id, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { bola.Id });

        var natija = await Kochiruvchi.HandleAsync(
            new MoveDepartmentCommand(ota.Id, bola.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bolim.halqa");
        natija.Error.Type.ShouldBe(ErrorType.Conflict);

        // Eng muhimi: ma'lumot O'ZGARMAGAN bo'lishi kerak
        ota.ParentId.ShouldBeNull();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _departments.DidNotReceive().RebuildHierarchyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Bolimni_ozining_ichiga_kochirib_bolmaydi()
    {
        var bolim = Department.Create("Buxgalteriya").Value;
        _departments.GetByIdAsync(bolim.Id, Arg.Any<CancellationToken>()).Returns(bolim);

        var natija = await Kochiruvchi.HandleAsync(
            new MoveDepartmentCommand(bolim.Id, bolim.Id),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();

        // Halqaning eng qisqa ko'rinishi ham AYNAN SHU kod bilan qaytadi:
        // foydalanuvchi uchun bu bitta va o'sha xato
        natija.Error.Code.ShouldBe("bolim.halqa");

        // Avlodlarni so'rash umuman kerak emas — bu bepul tekshiruv
        await _departments.DidNotReceive()
            .GetDescendantIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Mavjud_bolmagan_ota_bolimga_kochirib_bolmaydi()
    {
        var bolim = Department.Create("Buxgalteriya").Value;
        _departments.GetByIdAsync(bolim.Id, Arg.Any<CancellationToken>()).Returns(bolim);
        _departments.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var natija = await Kochiruvchi.HandleAsync(
            new MoveDepartmentCommand(bolim.Id, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bolim.ota_topilmadi");
    }

    // ────────── Muvaffaqiyatli ko'chirish ──────────

    [Fact]
    public async Task Kochirishdan_keyin_ierarxiya_qayta_hisoblanadi()
    {
        var bolim = Department.Create("Plazmada kesish bo'limi").Value;
        var yangiOta = Department.Create("Metan zavod").Value;

        _departments.GetByIdAsync(bolim.Id, Arg.Any<CancellationToken>()).Returns(bolim);
        _departments.ExistsAsync(yangiOta.Id, Arg.Any<CancellationToken>()).Returns(true);
        _departments
            .GetDescendantIdsAsync(bolim.Id, Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());

        var natija = await Kochiruvchi.HandleAsync(
            new MoveDepartmentCommand(bolim.Id, yangiOta.Id),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        bolim.ParentId.ShouldBe(yangiOta.Id);

        // Ko'chgan bo'limning HAMMA avlodlari chuqurligi o'zgaradi —
        // shu sababli butun daraxt qayta hisoblanishi shart
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _departments.Received(1).RebuildHierarchyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Eng_yuqori_darajaga_chiqarish_ota_bolimni_tekshirmaydi()
    {
        var bolim = Department.Create("Metan zavod", Guid.NewGuid()).Value;
        _departments.GetByIdAsync(bolim.Id, Arg.Any<CancellationToken>()).Returns(bolim);

        var natija = await Kochiruvchi.HandleAsync(
            new MoveDepartmentCommand(bolim.Id, null),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        bolim.ParentId.ShouldBeNull();
    }

    // ────────── Daraxt qurish ──────────

    [Fact]
    public async Task Daraxt_ichma_ich_quriladi()
    {
        var ota = Department.Create("7-Departament").Value;
        var bola = Department.Create("Metan zavod", ota.Id).Value;
        var nabira = Department.Create("Plazmada kesish bo'limi", bola.Id).Value;

        // Tartib ATAYLAB aralash: daraxt qurish kelayotgan tartibga
        // bog'liq bo'lmasligi kerak
        _departments
            .ListAsync(false, Arg.Any<CancellationToken>())
            .Returns(new List<Department> { nabira, ota, bola });

        var natija = await Daraxt.HandleAsync(
            new GetDepartmentTreeQuery(),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Count.ShouldBe(1);
        natija.Value[0].Name.ShouldBe("7-Departament");
        natija.Value[0].Children.Count.ShouldBe(1);
        natija.Value[0].Children[0].Name.ShouldBe("Metan zavod");
        natija.Value[0].Children[0].Children[0].Name.ShouldBe("Plazmada kesish bo'limi");
    }

    [Fact]
    public async Task Otasi_royxatda_yoq_bolim_ildiz_sifatida_korsatiladi()
    {
        // «onlyActive=true» da ota bo'lim faolsizlantirilgan bo'lishi mumkin.
        // Uning ichidagi faol bo'lim YO'QOLMASLIGI kerak — aks holda
        // foydalanuvchi mavjud bo'limni umuman ko'ra olmaydi.
        var yoqOtaId = Guid.NewGuid();
        var yetim = Department.Create("Metan zavod", yoqOtaId).Value;

        _departments
            .ListAsync(true, Arg.Any<CancellationToken>())
            .Returns(new List<Department> { yetim });

        var natija = await Daraxt.HandleAsync(
            new GetDepartmentTreeQuery(OnlyActive: true),
            TestContext.Current.CancellationToken);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Count.ShouldBe(1);
        natija.Value[0].Name.ShouldBe("Metan zavod");
    }

    [Fact]
    public async Task Daraxt_tartib_boyicha_saralanadi()
    {
        var birinchi = Department.Create("Zavod").Value;
        birinchi.SetSortOrder(0);

        var ikkinchi = Department.Create("Anbor").Value;
        ikkinchi.SetSortOrder(1);

        _departments
            .ListAsync(false, Arg.Any<CancellationToken>())
            .Returns(new List<Department> { ikkinchi, birinchi });

        var natija = await Daraxt.HandleAsync(
            new GetDepartmentTreeQuery(),
            TestContext.Current.CancellationToken);

        natija.Value[0].Name.ShouldBe("Zavod");
        natija.Value[1].Name.ShouldBe("Anbor");
    }
}
