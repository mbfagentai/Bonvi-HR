using Bonvi.IsupGateway.Cms;
using Bonvi.IsupGateway.Faces;

namespace Bonvi.IsupGateway.Tests;

/// <summary>Qo'lda suriladigan soat.</summary>
internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

public sealed class FacePictureStoreTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    [Fact]
    public void Token_128_bitlik_hex_va_har_safar_boshqa()
    {
        var store = new FacePictureStore(TimeProvider.System);

        var first = store.Add(Jpeg, TimeSpan.FromMinutes(5));
        var second = store.Add(Jpeg, TimeSpan.FromMinutes(5));

        first.Length.ShouldBe(32);
        first.ShouldAllBe(c => char.IsAsciiHexDigitLower(c) || char.IsAsciiDigit(c));
        second.ShouldNotBe(first);
    }

    [Fact]
    public void Umri_ichida_beriladi_keyin_yoq()
    {
        var clock = new ManualClock(DateTimeOffset.UnixEpoch);
        var store = new FacePictureStore(clock);
        var token = store.Add(Jpeg, TimeSpan.FromMinutes(5));

        store.TryGet(token, out var jpeg).ShouldBeTrue();
        jpeg.ShouldBe(Jpeg);

        clock.Advance(TimeSpan.FromMinutes(5));
        store.TryGet(token, out _).ShouldBeFalse();
    }

    [Fact]
    public void Ochirilgani_berilmaydi()
    {
        var store = new FacePictureStore(TimeProvider.System);
        var token = store.Add(Jpeg, TimeSpan.FromMinutes(5));

        store.Remove(token);

        store.TryGet(token, out _).ShouldBeFalse();
    }

    [Fact]
    public void Muddati_otganlar_yangi_qoshishda_tozalanadi()
    {
        var clock = new ManualClock(DateTimeOffset.UnixEpoch);
        var store = new FacePictureStore(clock);
        store.Add(Jpeg, TimeSpan.FromMinutes(1));
        store.Add(Jpeg, TimeSpan.FromMinutes(1));

        clock.Advance(TimeSpan.FromMinutes(2));
        store.Add(Jpeg, TimeSpan.FromMinutes(1));

        store.Count.ShouldBe(1);
    }

    [Fact]
    public void Notogri_token_berilmaydi()
        => new FacePictureStore(TimeProvider.System).TryGet("0123456789abcdef0123456789abcdef", out _).ShouldBeFalse();
}

public sealed class DeviceRegistryTests
{
    [Fact]
    public void Qayta_ulanish_eskisini_almashtiradi_va_eski_uzilish_yangisini_ochirmaydi()
    {
        var registry = new DeviceRegistry();

        registry.Online(Session("zavod-1", loginId: 1));
        registry.Online(Session("zavod-1", loginId: 2));

        // Eski sessiyaning kechikkan «uzildi» xabari
        registry.OfflineByLogin(1).ShouldBeNull();

        registry.TryGet("zavod-1", out var session).ShouldBeTrue();
        session.LoginId.ShouldBe(2);

        registry.OfflineByLogin(2).ShouldBe("zavod-1");
        registry.TryGet("zavod-1", out _).ShouldBeFalse();
    }

    [Fact]
    public void Royxat_tartiblangan()
    {
        var registry = new DeviceRegistry();
        registry.Online(Session("zavod-2", 5));
        registry.Online(Session("ofis-1", 6));

        registry.Snapshot().Select(s => s.DeviceId).ShouldBe(["ofis-1", "zavod-2"]);
    }

    private static DeviceSession Session(string deviceId, int loginId)
        => new(deviceId, loginId, "SERIAL", "V3.7.0", "10.0.0.1:5000", DateTimeOffset.UnixEpoch);
}
