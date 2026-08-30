using Bonvi.Bot.Conversation;

namespace Bonvi.Bot.Tests;

/// <summary>
/// Nomzod yozgan F.I.Sh. ni bo'laklarga ajratish.
/// </summary>
/// <remarks>
/// <para>
/// Qoida domendagi <c>FullName.Parse</c> bilan AYNAN bir xil bo'lishi
/// shart: birinchi so'z — familiya, ikkinchisi — ism, qolgani — otasining
/// ismi. Ikkalasi ajralib ketsa, botdan kelgan nomzod qo'lda
/// kiritilganidan boshqacha yozilardi va ro'yxatda ikki xil ko'rinardi.
/// </para>
/// <para>
/// Testlar ATAYLAB kirill va lotin yozuvida: bot ikkala alifboda ham
/// ishlaydi va ajratish alifboga bog'liq bo'lmasligi kerak.
/// </para>
/// </remarks>
public sealed class NameParserTests
{
    [Fact]
    public void Uch_bolakli_ism_ajratiladi()
    {
        var name = NameParser.TryParse("Каримов Азиз Акмал ўғли");

        name.ShouldNotBeNull();
        name.LastName.ShouldBe("Каримов");
        name.FirstName.ShouldBe("Азиз");

        // «Акмал ўғли» — IKKI so'z, lekin bitta otasining ismi.
        name.MiddleName.ShouldBe("Акмал ўғли");
    }

    [Fact]
    public void Lotin_yozuvi_ham_bir_xil_ajratiladi()
    {
        var name = NameParser.TryParse("Karimov Aziz Akmal o‘g‘li");

        name.ShouldNotBeNull();
        name.LastName.ShouldBe("Karimov");
        name.FirstName.ShouldBe("Aziz");
        name.MiddleName.ShouldBe("Akmal o‘g‘li");
    }

    [Fact]
    public void Otasining_ismisiz_ham_qabul_qilinadi()
    {
        // Ayollarda va ba'zi hujjatlarda otasining ismi yozilmaydi —
        // buni majburiy qilish arizani to'sib qo'yardi.
        var name = NameParser.TryParse("Норматова Сайёра");

        name.ShouldNotBeNull();
        name.LastName.ShouldBe("Норматова");
        name.FirstName.ShouldBe("Сайёра");
        name.MiddleName.ShouldBeNull();
    }

    [Fact]
    public void Ortiqcha_boshliqlar_tozalanadi()
    {
        // Telefonda yozganda ikkita probel va boshida bo'sh joy odatiy hol.
        var name = NameParser.TryParse("  Каримов   Азиз  ");

        name.ShouldNotBeNull();
        name.LastName.ShouldBe("Каримов");
        name.FirstName.ShouldBe("Азиз");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Каримов")]     // faqat familiya
    [InlineData("Али")]          // juda qisqa
    public void Tolik_bolmagan_ism_qabul_qilinmaydi(string? input)
    {
        NameParser.TryParse(input).ShouldBeNull();
    }

    [Fact]
    public void Juda_uzun_matn_qabul_qilinmaydi()
    {
        // Tasodifan yopishtirilgan matn yoki hazil: server chegarasiga
        // yetkazmasdan shu yerda to'siladi.
        NameParser.TryParse(new string('а', 250) + " " + new string('б', 50)).ShouldBeNull();
    }
}
