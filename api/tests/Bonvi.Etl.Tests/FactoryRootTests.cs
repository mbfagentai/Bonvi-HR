using Bonvi.Etl.Runtime;

namespace Bonvi.Etl.Tests;

/// <summary>
/// Zavod darajasidagi bo'limlar ro'yxati — ma'lumotnomadan olinadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega test kerak.</b> Ilgari bu ro'yxat kodda qattiq yozilgan edi va
/// ma'lumotnomalardan <c>code</c> maydoni olib tashlanganda jimgina
/// o'chib qoldi — hech qanday test yiqilmadi, chunki uni hech kim
/// tekshirmasdi. Natijada «zavod o'rniga aniqroq blok sarlavhasi
/// olinsin» qoidasi ishlamay qolgan va bu xodimlarning bo'limga
/// biriktirilishiga to'g'ridan-to'g'ri ta'sir qilardi.
/// </para>
/// <para>
/// Shu sababli test qiymatni EMAS, BOG'LANISHNI qadab turadi: ro'yxat
/// bo'sh bo'lmasligi va unga aynan yuridik shaxsga biriktirilgan
/// bo'limlar tushishi.
/// </para>
/// </remarks>
public sealed class FactoryRootTests
{
    [Fact]
    public void Royxat_bosh_emas()
    {
        // Bo'sh ro'yxat = qoida yana jimgina o'chdi degani.
        CatalogIndex.FactoryRootDepartments.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("Метан завод")]
    [InlineData("Вело унион (Пропан) заводи")]
    [InlineData("Риспетто велосипед заводи")]
    [InlineData("\"Kokand Bicycles\" МЧЖ")]
    public void Yuridik_shaxsga_biriktirilgan_bolim_zavod_darajasida(string name)
    {
        CatalogIndex.FactoryRootDepartments.ShouldContain(name);
    }

    [Theory]
    [InlineData("HR департаменти")]
    [InlineData("1-Департамент")]
    [InlineData("Скутер заводи")]
    public void Oddiy_bolim_zavod_darajasida_emas(string name)
    {
        // «Скутер заводи» ham shu yerda: u yuridik shaxsga biriktirilmagan
        // va uning ostida aniqroq bo'lim yo'q — almashtiradigan narsa ham yo'q.
        CatalogIndex.FactoryRootDepartments.ShouldNotContain(name);
    }
}
