using Bonvi.Domain.Identity;

namespace Bonvi.Domain.Tests.Identity;

/// <summary>Yangilash tokeni mantig'i.</summary>
public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 9, 0, 0, TimeSpan.FromHours(5));

    [Fact]
    public void Berilgan_token_faol_holatda()
    {
        var token = RefreshToken.Issue(Guid.CreateVersion7(), "xesh", Now.AddDays(7), "10.0.0.1");

        token.IsActiveAt(Now).ShouldBeTrue();
        token.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public void Muddati_tugagan_token_faol_emas()
    {
        var token = RefreshToken.Issue(Guid.CreateVersion7(), "xesh", Now.AddDays(7));

        token.IsActiveAt(Now.AddDays(8)).ShouldBeFalse();
    }

    [Fact]
    public void Bekor_qilingan_token_faol_emas()
    {
        var token = RefreshToken.Issue(Guid.CreateVersion7(), "xesh", Now.AddDays(7));

        token.Revoke(Now.AddHours(1), "10.0.0.1");

        token.IsActiveAt(Now.AddHours(2)).ShouldBeFalse();
        token.RevokedAt.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void Bekor_qilish_takrorlanganda_birinchi_sana_saqlanadi()
    {
        // Muhim: aks holda o'g'irlikni tekshirishda vaqt ma'lumoti buziladi
        var token = RefreshToken.Issue(Guid.CreateVersion7(), "xesh", Now.AddDays(7));
        token.Revoke(Now.AddHours(1), "10.0.0.1");

        token.Revoke(Now.AddHours(5), "10.0.0.2");

        token.RevokedAt.ShouldBe(Now.AddHours(1));
        token.RevokedByIp.ShouldBe("10.0.0.1");
    }

    [Fact]
    public void Aylantirishda_yangi_token_zanjirga_bogʻlanadi()
    {
        var token = RefreshToken.Issue(Guid.CreateVersion7(), "eski_xesh", Now.AddDays(7));

        token.Revoke(Now.AddHours(1), "10.0.0.1", "yangi_xesh");

        token.ReplacedByTokenHash.ShouldBe("yangi_xesh");
    }
}
