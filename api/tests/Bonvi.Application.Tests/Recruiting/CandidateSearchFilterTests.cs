using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Tests.Recruiting;

/// <summary>
/// Nomzodlar ro'yxatining YASHIRISH qoidalari.
/// </summary>
/// <remarks>
/// <b>Nega alohida test.</b> Uchala qoida ham bir-biriga bog'langan
/// (`HideRejected` ning o'zi `HideArchive` ga tayanadi) va ular
/// ma'lumotlar bazasiga umuman tegmaydi — ya'ni ularni integratsiya
/// testi orqali tekshirish qimmat, bu yerda esa arzon. Buzilishi esa
/// jimgina bo'lardi: ro'yxat shunchaki kamroq qator qaytarardi.
/// </remarks>
public sealed class CandidateSearchFilterTests
{
    [Fact]
    public void Sukut_royxatdan_ishga_olinganlar_va_rad_etilganlar_chiqariladi()
    {
        var filter = new CandidateSearchFilter();

        filter.HideHired.ShouldBeTrue();
        filter.HideRejected.ShouldBeTrue();
        filter.HideArchive.ShouldBeTrue();
    }

    [Theory]
    [InlineData(CandidateStatus.Rejected)]
    [InlineData(CandidateStatus.Hired)]
    [InlineData(CandidateStatus.InProgress)]
    public void Holat_ataylab_tanlansa_qoida_ochadi(CandidateStatus status)
    {
        // Menyudagi «Rad etilganlar» bandi aynan shu yo'l bilan ishlaydi:
        // qoida ochilmasa, band jimgina bo'sh ro'yxat berardi.
        var filter = new CandidateSearchFilter(Status: status);

        filter.HideHired.ShouldBeFalse();
        filter.HideRejected.ShouldBeFalse();
    }

    [Fact]
    public void Excel_arxivi_ochilganda_rad_etilganlar_qaytadi()
    {
        /*
          Kengash arxividagi 3110 yozuvning deyarli hammasi RAD ETILGAN
          (`CouncilStep.Close`). Ikkala qoida birga qo'llansa, «Excel
          arxivi» ni tanlagan HR bo'm-bo'sh ro'yxat olardi.
        */
        var manba = new CandidateSearchFilter(Source: CandidateSource.Excel);

        manba.HideArchive.ShouldBeFalse();
        manba.HideRejected.ShouldBeFalse("arxivning o'zi ko'rinmas bo'lib qolardi");
        manba.HideHired.ShouldBeTrue("mavjud qoida — unga tegilmagan");

        var bayroq = new CandidateSearchFilter(IncludeArchive: true);

        bayroq.HideArchive.ShouldBeFalse();
        bayroq.HideRejected.ShouldBeFalse();
    }

    [Fact]
    public void Boshqa_manba_tanlansa_qoida_kuchda_qoladi()
    {
        /*
          «Manba: Telegram bot» ni tanlash HOLAT tanlash EMAS: ekran
          hamon «hozir kim ish ustida?» degan savolga javob beradi.
          Qoida `HideArchive` ga bog'langanida bu tanlov ham uni ochib
          yuborardi va rad etilganlar kutilmaganda qaytib kelardi.
        */
        var bot = new CandidateSearchFilter(Source: CandidateSource.TelegramBot);

        bot.HideRejected.ShouldBeTrue();
        bot.HideHired.ShouldBeTrue();
        bot.HideArchive.ShouldBeFalse("mavjud qoida — unga tegilmagan");
    }
}
