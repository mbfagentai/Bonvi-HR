using System.Security.Cryptography;
using System.Text;
using System.Web;
using Bonvi.Application.Recruiting.Council;

namespace Bonvi.Application.Tests.Recruiting;

/// <summary>Only launch data Telegram signed identifies a council member (SPEC-kengash-guruhi §5).</summary>
public sealed class TelegramInitDataTests
{
    private const string Token = "123456:ABC-test";
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Signs the fields the way Telegram documents it.</summary>
    internal static string Sign(string token, IReadOnlyDictionary<string, string> fields)
    {
        var check = string.Join('\n', fields.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value));
        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(token));
        var hash = Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(check)));

        return string.Join('&', fields.Select(pair => pair.Key + "=" + HttpUtility.UrlEncode(pair.Value))) + "&hash=" + hash;
    }

    private static Dictionary<string, string> Fields(DateTimeOffset at, string user = """{"id":77,"first_name":"Шеф Ака"}""")
        => new()
        {
            ["auth_date"] = at.ToUnixTimeSeconds().ToString(global::System.Globalization.CultureInfo.InvariantCulture),
            ["user"] = user,
            ["start_param"] = "abc",
        };

    [Fact]
    public void Haqiqiy_imzo_foydalanuvchini_beradi()
        => TelegramInitData.ReadUserId(Sign(Token, Fields(Now)), Token, Now).ShouldBe(77);

    [Fact]
    public void Ism_va_username_oqiladi_ismsiz_bolsa_raqam_turadi()
    {
        var user = TelegramInitData.ReadUser(Sign(Token, Fields(Now, """{"id":77,"first_name":"Born","last_name":"to Win","username":"born"}""")), Token, Now)!;
        user.FullName.ShouldBe("Born to Win");
        user.Username.ShouldBe("born");

        TelegramInitData.ReadUser(Sign(Token, Fields(Now, """{"id":77}""")), Token, Now)!.FullName.ShouldBe("77");
    }

    [Fact]
    public void Boshqa_token_bilan_imzolangani_rad_etiladi()
        => TelegramInitData.ReadUserId(Sign("999:other", Fields(Now)), Token, Now).ShouldBeNull();

    [Fact]
    public void Foydalanuvchi_almashtirilsa_rad_etiladi()
    {
        var signed = Sign(Token, Fields(Now));
        var forged = signed.Replace(HttpUtility.UrlEncode("\"id\":77"), HttpUtility.UrlEncode("\"id\":78"), StringComparison.Ordinal);

        forged.ShouldNotBe(signed);
        TelegramInitData.ReadUserId(forged, Token, Now).ShouldBeNull();
    }

    [Fact]
    public void Eskirgan_rad_etiladi_chegaradagisi_otadi()
    {
        TelegramInitData.ReadUserId(Sign(Token, Fields(Now.AddHours(-24))), Token, Now).ShouldBe(77);
        TelegramInitData.ReadUserId(Sign(Token, Fields(Now.AddHours(-24).AddSeconds(-1))), Token, Now).ShouldBeNull();
        TelegramInitData.ReadUserId(Sign(Token, Fields(Now.AddHours(1))), Token, Now).ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("auth_date=1&user=x")]
    [InlineData("hash=zz")]
    [InlineData("%%%")]
    public void Buzuq_malumot_yiqitmaydi(string? raw)
        => TelegramInitData.ReadUserId(raw, Token, Now).ShouldBeNull();

    [Fact]
    public void Token_yoki_user_yoq_bolsa_otmaydi()
    {
        TelegramInitData.ReadUserId(Sign(Token, Fields(Now)), null, Now).ShouldBeNull();
        TelegramInitData.ReadUserId(Sign(Token, Fields(Now, "not json")), Token, Now).ShouldBeNull();
    }
}
