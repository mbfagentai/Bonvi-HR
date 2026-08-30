using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Etl.Runtime;

namespace Bonvi.Etl.Tests;

/// <summary>
/// Xodimni takrorlanmasligi uchun qidirish tartibi.
/// </summary>
/// <remarks>
/// Bu ETL idempotentligining asosi. Qidiruv tartibi ataylab shunday:
/// JSHSHIR (davlat raqami, o'zgarmaydi) → pasport (almashadi, lekin bir
/// vaqtda ikki odamda bo'lmaydi) → ism va tug'ilgan sana (eng zaif dalil,
/// lekin 3 ta xodimda ikkalasi ham yo'q).
/// </remarks>
public sealed class EmployeeIndexTests
{
    private static readonly DateOnly Born = new(1990, 5, 17);

    private static Employee Make(string last, string first, DateOnly? birthDate = null)
        => Employee.Create(FullName.FromTrusted(last, first, null), birthDate ?? Born).Value;

    [Fact]
    public void Jshshir_boyicha_topiladi()
    {
        var index = new EmployeeIndex();
        var employee = Make("Каримов", "Азиз");
        var pinfl = Pinfl.FromTrusted("31501914320045");

        _ = employee.SetPinfl(pinfl);
        index.Add(employee);

        var match = index.Find(pinfl, null, null, null);

        match.Kind.ShouldBe(EmployeeMatchKind.Pinfl);
        match.Employee.ShouldBeSameAs(employee);
    }

    [Fact]
    public void Jshshir_pasportdan_ustun_turadi()
    {
        // Pasport almashadi, JSHSHIR esa hech qachon. Ikkalasi turli
        // odamni ko'rsatsa — JSHSHIR to'g'ri.
        var index = new EmployeeIndex();

        var withPinfl = Make("Каримов", "Азиз");
        var pinfl = Pinfl.FromTrusted("31501914320045");
        _ = withPinfl.SetPinfl(pinfl);

        var withPassport = Make("Тошматов", "Бек");
        var passport = PassportSeries.Create("AB5725936").Value;
        withPassport.SetPassport(passport);

        index.Add(withPinfl);
        index.Add(withPassport);

        index.Find(pinfl, passport, null, null).Employee.ShouldBeSameAs(withPinfl);
    }

    [Fact]
    public void Jshshirsiz_odam_ism_va_tugilgan_sana_boyicha_topiladi()
    {
        var index = new EmployeeIndex();
        var employee = Make("Каримов", "Азиз");
        index.Add(employee);

        var match = index.Find(null, null, FullName.FromTrusted("Каримов", "Азиз", null), Born);

        match.Kind.ShouldBe(EmployeeMatchKind.NameAndBirthDate);
        match.Employee.ShouldBeSameAs(employee);
    }

    [Fact]
    public void Bir_xil_ismli_ikki_xodim_boysa_tanlanmaydi()
    {
        // Noto'g'ri odamga hodisa yozib qo'yish — hodisani umuman
        // yozmaslikdan yomonroq.
        var index = new EmployeeIndex();
        index.Add(Make("Каримов", "Азиз"));
        index.Add(Make("Каримов", "Азиз"));

        var match = index.Find(null, null, FullName.FromTrusted("Каримов", "Азиз", null), Born);

        match.Kind.ShouldBe(EmployeeMatchKind.Ambiguous);
        match.Employee.ShouldBeNull();
        match.Candidates.ShouldBe(2);
    }

    [Fact]
    public void Ism_mos_sana_mos_emas_taxmin_qilinmaydi()
    {
        // Bu boshqa odam bo'lishi ham, sana xato kiritilgan bo'lishi ham mumkin.
        var index = new EmployeeIndex();
        index.Add(Make("Каримов", "Азиз", new DateOnly(1990, 5, 17)));

        var match = index.Find(null, null, FullName.FromTrusted("Каримов", "Азиз", null), new DateOnly(1991, 1, 1));

        match.Kind.ShouldBe(EmployeeMatchKind.Ambiguous);
        match.Employee.ShouldBeNull();
    }

    [Fact]
    public void Sanasiz_qidiruvda_yagona_ism_topiladi()
    {
        var index = new EmployeeIndex();
        var employee = Make("Каримов", "Азиз");
        index.Add(employee);

        index.FindByName(FullName.FromTrusted("Каримов", "Азиз", null)).Employee.ShouldBeSameAs(employee);
    }

    [Fact]
    public void Registr_va_ortiqcha_boshliq_ism_kalitiga_tasir_qilmaydi()
    {
        var index = new EmployeeIndex();
        index.Add(Make("Каримов", "Азиз"));

        index.FindByName(FullName.FromTrusted("КАРИМОВ", "азиз", null)).Found.ShouldBeTrue();
    }

    [Fact]
    public void Kirill_imlo_variantlari_ataylab_birlashtirilmaydi()
    {
        // «Тошпулатов» va «Тўшпулатов» turli familiya bo'lishi mumkin —
        // ularni birlashtirish ikki odamni bittaga qo'shib yuborardi.
        var index = new EmployeeIndex();
        index.Add(Make("Тошпулатов", "Азиз"));

        index.FindByName(FullName.FromTrusted("Тўшпулатов", "Азиз", null)).Found.ShouldBeFalse();
    }

    [Fact]
    public void Otasining_ismisiz_yagona_nomzod_maslahat_sifatida_qaytadi()
    {
        // Hodisa varaqlarida otasining ismi tez-tez tushib qolgan.
        // Nomzod KO'RSATILADI, lekin bog'lanmaydi — qarorni HR qabul qiladi.
        var index = new EmployeeIndex();
        var employee = Make("Абдуллаев", "Умиджон");
        index.Add(employee);

        var shortName = FullName.FromTrusted("Абдуллаев", "Умиджон", "Мўйдинжон ўғли");

        index.FindByName(shortName).Found.ShouldBeFalse();
        index.SuggestByShortName(shortName).ShouldBeSameAs(employee);
    }

    [Fact]
    public void Bir_xil_familiya_va_ismli_ikki_odam_bolsa_maslahat_berilmaydi()
    {
        // O'zbekistonda aka-uka bir xil familiya va ismda bo'lishi odatiy hol.
        var index = new EmployeeIndex();
        index.Add(Make("Абдуллаев", "Умиджон"));
        index.Add(Make("Абдуллаев", "Умиджон", new DateOnly(1995, 3, 2)));

        index.SuggestByShortName(FullName.FromTrusted("Абдуллаев", "Умиджон", "Каримович"))
            .ShouldBeNull();
    }

    [Fact]
    public void Bosh_indeksda_hech_narsa_topilmaydi()
        => new EmployeeIndex().Find(null, null, FullName.FromTrusted("Каримов", "Азиз", null), Born)
            .Kind.ShouldBe(EmployeeMatchKind.None);

    [Fact]
    public void Keyin_qoyilgan_jshshir_ham_indeksga_tushadi()
    {
        /*
          BUTUN KO'CHIRISHNI YIQITGAN XATO (2026-08-30).

          Yangi xodim ISMDAN yaratiladi, JSHSHIR unga KEYIN qo'yiladi
          (`EmployeesStep.Fill` — qiymatlar bir nechta varaqdan
          yig'iladi). Ya'ni `Add` chaqirilgan payt JSHSHIR hali yo'q
          va u indeksga tushmasdi.

          Oqibati: bir odamning ikkinchi qatori ismi boshqacha
          yozilgan bo'lsa, u YANGI xodim bo'lib yaratilardi —
          JSHSHIR esa ikkalasida bir xil. `--apply` da
          `ix_employees_pinfl` unikal indeksi buzilib, ko'chirish
          birinchi qadamdayoq to'xtardi.
        */
        var index = new EmployeeIndex();
        var employee = Make("Каримов", "Азиз");

        index.Add(employee);

        var pinfl = Pinfl.FromTrusted("31501914320045");
        _ = employee.SetPinfl(pinfl);
        index.IndexIdentifiers(employee);

        index.Find(pinfl, null, null, null).Employee.ShouldBeSameAs(employee);
    }

    [Fact]
    public void Ikkinchi_ism_yozilishi_bilan_ham_topiladi()
    {
        // Excel'da bir odamning ismi har xil yozilgan: otasining ismi
        // bor va yo'q variantlari. JSHSHIR ustuni yo'q varaqlar odamni
        // faqat ismi bilan izlaydi.
        var index = new EmployeeIndex();
        var employee = Make("Каримов", "Азиз");
        index.Add(employee);

        var alias = FullName.FromTrusted("Каримов", "Азиз", "Баходирович");
        index.FindByName(alias).Found.ShouldBeFalse();

        index.IndexAlias(employee, alias);

        index.FindByName(alias).Employee.ShouldBeSameAs(employee);
    }

    [Fact]
    public void Ikkinchi_ism_xodimlar_sonini_oshirmaydi()
    {
        // Bu bitta odamning ikkinchi NOMI, ikkinchi odam emas.
        var index = new EmployeeIndex();
        var employee = Make("Каримов", "Азиз");
        index.Add(employee);

        index.IndexAlias(employee, FullName.FromTrusted("Каримов", "Азиз", "Баходирович"));

        index.Count.ShouldBe(1);
    }

    [Fact]
    public void Ayni_ism_qayta_qoyilsa_ikkilanish_paydo_bolmaydi()
    {
        /*
          `EmployeesStep` `IndexAlias` ni HAR BIR qator uchun chaqiradi,
          shu jumladan ismi o'zgarmagan qator uchun ham. Agar xodim
          ro'yxatga ikkinchi marta tushib qolsa, u O'ZI bilan
          «bir xil ismli ikkita xodim» bo'lib ko'rinardi va ETL uni
          umuman tanlamay qo'yardi.
        */
        var index = new EmployeeIndex();
        var employee = Make("Каримов", "Азиз");
        index.Add(employee);

        var same = FullName.FromTrusted("Каримов", "Азиз", null);
        index.IndexAlias(employee, same);
        index.IndexAlias(employee, same);

        var match = index.Find(null, null, same, Born);

        match.Kind.ShouldNotBe(EmployeeMatchKind.Ambiguous);
        match.Employee.ShouldBeSameAs(employee);
    }
}
