using System.Globalization;

using Bonvi.Bot.Conversation;

namespace Bonvi.Bot.Tests;

/// <summary>
/// Xarita nuqtasini matndan o'qish.
/// </summary>
/// <remarks>
/// Qiymatni HR Google Maps'dan nusxa oladi, ya'ni uning shakli
/// bizga bog'liq emas. Noto'g'ri nuqta bilan Telegram butun xabarni
/// rad etardi — shu sababli tekshiruv shu yerda, yuborishdan oldin.
/// </remarks>
public sealed class MapPointTests
{
    [Fact]
    public void Google_maps_nusxasi_oqiladi()
    {
        // Aynan shu ko'rinishda nusxalanadi — bo'shliq bilan.
        var point = MapPoint.TryParse("40.505852, 70.930622");

        point.ShouldNotBeNull();
        point.Value.Latitude.ShouldBe(40.505852, tolerance: 0.000001);
        point.Value.Longitude.ShouldBe(70.930622, tolerance: 0.000001);
    }

    [Fact]
    public void Boshliqsiz_ham_oqiladi()
        => MapPoint.TryParse("41.311081,69.240562").ShouldNotBeNull();

    [Fact]
    public void Manfiy_qiymat_qabul_qilinadi()
    {
        // Janubiy yarim shar va g'arbiy uzunlik. Bonvi uchun kerak
        // emas, lekin qoidani yarim yozish uni buzilishga ochiq
        // qoldiradi.
        var point = MapPoint.TryParse("-33.86882, -151.20929");

        point.ShouldNotBeNull();
        point.Value.Latitude.ShouldBeLessThan(0);
        point.Value.Longitude.ShouldBeLessThan(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Farg‘ona viloyati, Qo‘qon")]
    [InlineData("40.505852")]
    [InlineData("40.505852, 70.930622, 100")]
    [InlineData("91, 70")]
    [InlineData("40, 181")]
    [InlineData("-91, 0")]
    public void Notogri_qiymat_rad_etiladi(string? value)
        => MapPoint.TryParse(value).ShouldBeNull();

    [Fact]
    public void Serverning_mahalliy_sozlamasi_tasir_qilmaydi()
    {
        /*
          Kasr ajratgichi VERGUL bo'lgan madaniyatda («fr-FR») nuqta
          bilan yozilgan son o'qilmay qolardi va xarita jimgina
          yo'qolardi — eng yomon turdagi nosozlik: hech qayerda xato
          ko'rinmaydi, faqat nomzod xaritani olmaydi.
        */
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            MapPoint.TryParse("40.505852, 70.930622")!.Value.Latitude
                .ShouldBe(40.505852, tolerance: 0.000001);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
