using Bonvi.Application.Abstractions;
using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Council;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using NSubstitute;

namespace Bonvi.Application.Tests.Recruiting;

/// <summary>When the group's message goes out (SPEC-kengash-guruhi §4).</summary>
public sealed class CouncilDigestTimingTests
{
    private const long Chat = -100500;
    private static readonly DateOnly Bugun = new(2026, 11, 6);
    private static readonly DateOnly Ertaga = new(2026, 11, 7);

    private readonly ICouncilGroupRepository _group = Substitute.For<ICouncilGroupRepository>();
    private readonly IRecruitingReadModel _readModel = Substitute.For<IRecruitingReadModel>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ISystemSettingRepository _settings = Substitute.For<ISystemSettingRepository>();

    private static CouncilInvitedRow Qator(Guid id, DateOnly kun)
        => new(id, "Каримов Азиз", "Каримов Азиз Ака", "Кассир", new DateTimeOffset(kun.ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(5)), false);

    private void Soat(int soat, int daqiqa = 0)
        => _clock.UtcNow.Returns(new DateTimeOffset(Bugun.ToDateTime(new TimeOnly(soat, daqiqa)), TimeSpan.FromHours(5)));

    private void Chaqirilgan(DateOnly kun, params CouncilInvitedRow[] qatorlar)
    {
        var boshi = new DateTimeOffset(kun.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5)).ToUniversalTime();
        _readModel
            .ListCouncilInvitedAsync(boshi, boshi.AddDays(1), null, Arg.Any<CancellationToken>())
            .Returns(qatorlar);
    }

    private async Task<CouncilBotStateDto> HolatAsync(long? chat = Chat, string? miniApp = null)
    {
        var yozuvlar = new List<SystemSetting>();
        if (chat is { } id)
        {
            yozuvlar.Add(SystemSetting.CreateServerValue(SystemSettingKeys.BotCouncilChatId, id.ToString(global::System.Globalization.CultureInfo.InvariantCulture)).Value);
        }

        if (miniApp is not null)
        {
            yozuvlar.Add(SystemSetting.CreateServerValue(SystemSettingKeys.BotCouncilMiniAppName, miniApp).Value);
        }

        _settings.GetAllAsync(Arg.Any<CancellationToken>()).Returns(yozuvlar);

        var reader = new SystemSettingsReader(_settings, Substitute.For<ISecretProtector>());
        var handler = new GetCouncilBotStateQueryHandler(reader, _group, _readModel, _clock);

        return (await handler.HandleAsync(new GetCouncilBotStateQuery(), CancellationToken.None)).Value;
    }

    [Theory]
    [InlineData(9, 59, false)]
    [InlineData(10, 0, true)]
    [InlineData(19, 59, true)]
    [InlineData(20, 0, false)]
    public async Task Birinchi_xabar_faqat_10_dan_20_gacha(int soat, int daqiqa, bool chiqadi)
    {
        Soat(soat, daqiqa);
        Chaqirilgan(Bugun);
        Chaqirilgan(Ertaga, Qator(Guid.NewGuid(), Ertaga));

        var holat = await HolatAsync();

        (holat.Digest is not null).ShouldBe(chiqadi);
        if (chiqadi)
        {
            holat.Digest!.Date.ShouldBe(Ertaga);
            holat.Digest.IsToday.ShouldBeFalse();
            holat.Digest.MessageId.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Guruh_ulanmagan_bolsa_xabar_yoq()
    {
        Soat(11);
        Chaqirilgan(Bugun);
        Chaqirilgan(Ertaga, Qator(Guid.NewGuid(), Ertaga));

        var holat = await HolatAsync(null);

        holat.ChatId.ShouldBeNull();
        holat.Digest.ShouldBeNull();
    }

    [Fact]
    public async Task Nomzodsiz_kunga_xabar_yoq()
    {
        Soat(11);
        Chaqirilgan(Bugun);
        Chaqirilgan(Ertaga);

        (await HolatAsync()).Digest.ShouldBeNull();
    }

    [Fact]
    public async Task Ozgarmagan_royxat_qayta_yuborilmaydi_ozgargani_tungi_paytda_ham_tahrirlanadi()
    {
        var birinchi = Qator(Guid.NewGuid(), Ertaga);
        Soat(11);
        Chaqirilgan(Bugun);
        Chaqirilgan(Ertaga, birinchi);
        var imzo = (await HolatAsync()).Digest!.Signature;

        _group.FindDigestAsync(Ertaga, Arg.Any<CancellationToken>()).Returns(CouncilDigest.Create(Ertaga, Chat, 55, imzo));
        (await HolatAsync()).Digest.ShouldBeNull();

        Soat(23);
        Chaqirilgan(Ertaga, birinchi, Qator(Guid.NewGuid(), Ertaga));

        var holat = await HolatAsync();
        holat.Digest!.MessageId.ShouldBe(55);
        holat.Digest.Rows.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Mini_app_nomi_ozgarsa_tugmalar_yangilanadi()
    {
        var qator = Qator(Guid.NewGuid(), Ertaga);
        Soat(11);
        Chaqirilgan(Bugun);
        Chaqirilgan(Ertaga, qator);
        var imzo = (await HolatAsync()).Digest!.Signature;
        _group.FindDigestAsync(Ertaga, Arg.Any<CancellationToken>()).Returns(CouncilDigest.Create(Ertaga, Chat, 55, imzo));

        var holat = await HolatAsync(miniApp: "yangi");

        holat.MiniAppName.ShouldBe("yangi");
        holat.Digest!.MessageId.ShouldBe(55);
    }

    [Fact]
    public async Task Hamma_nomzod_ketsa_xabar_tahrirlanadi()
    {
        Soat(11);
        Chaqirilgan(Bugun);
        Chaqirilgan(Ertaga);
        _group.FindDigestAsync(Ertaga, Arg.Any<CancellationToken>()).Returns(CouncilDigest.Create(Ertaga, Chat, 55, "eski"));

        var holat = await HolatAsync();

        holat.Digest!.MessageId.ShouldBe(55);
        holat.Digest.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task Otkazib_yuborilgan_bugungi_kengash_ham_chiqadi()
    {
        Soat(12);
        Chaqirilgan(Bugun, Qator(Guid.NewGuid(), Bugun));
        Chaqirilgan(Ertaga);

        var holat = await HolatAsync();

        holat.Digest!.Date.ShouldBe(Bugun);
        holat.Digest.IsToday.ShouldBeTrue();
    }
}
