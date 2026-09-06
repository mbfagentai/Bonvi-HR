using Bonvi.Bot.Telegram;

namespace Bonvi.Bot.Tests;

/// <summary>
/// Yuborilgan rasmning qaysi nusxasi olinadi (18-qadam).
/// </summary>
/// <remarks>
/// Telegram bitta rasmni bir necha o'lchamda yuboradi. Eng kattasi
/// kerak — server rasmni o'zi 512×512 ga qirqadi va manba qanchalik
/// yaxshi bo'lsa natija shunchalik toza chiqadi. Lekin serverning
/// hajm chegarasi bor va undan katta fayl rad etilardi.
/// </remarks>
public sealed class TelegramPhotosTests
{
    private const long TenMegabytes = 10L * 1024 * 1024;

    [Fact]
    public void Eng_katta_nusxa_tanlanadi()
    {
        IReadOnlyList<TelegramPhotoSize> sizes =
        [
            new("kichik", 90, 60, 1_000),
            new("orta", 320, 240, 20_000),
            new("katta", 1280, 960, 300_000),
        ];

        TelegramPhotos.BestFileId(sizes, TenMegabytes).ShouldBe("katta");
    }

    [Fact]
    public void Chegaradan_oshgan_nusxa_tashlanadi()
    {
        IReadOnlyList<TelegramPhotoSize> sizes =
        [
            new("orta", 320, 240, 20_000),
            new("ogir", 4000, 3000, TenMegabytes + 1),
        ];

        TelegramPhotos.BestFileId(sizes, TenMegabytes).ShouldBe("orta");
    }

    [Fact]
    public void Hajmi_nomalum_nusxa_qabul_qilinadi()
    {
        // Telegram `file_size` ni har doim ham bermaydi — bu «katta»
        // degani emas, «noma'lum» degani.
        IReadOnlyList<TelegramPhotoSize> sizes = [new("katta", 1280, 960, null)];

        TelegramPhotos.BestFileId(sizes, TenMegabytes).ShouldBe("katta");
    }

    [Fact]
    public void Hech_biri_sigmasa_eng_kichigi_olinadi()
    {
        IReadOnlyList<TelegramPhotoSize> sizes =
        [
            new("katta", 4000, 3000, TenMegabytes + 2),
            new("kichikroq", 1280, 960, TenMegabytes + 1),
        ];

        TelegramPhotos.BestFileId(sizes, TenMegabytes).ShouldBe("kichikroq");
    }

    [Fact]
    public void Rasm_bolmasa_null_qaytadi()
    {
        TelegramPhotos.BestFileId(null, TenMegabytes).ShouldBeNull();
        TelegramPhotos.BestFileId([], TenMegabytes).ShouldBeNull();
    }
}
