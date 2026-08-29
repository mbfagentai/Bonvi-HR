using Bonvi.Domain.Notifications;

namespace Bonvi.Domain.Tests.Notifications;

/// <summary>
/// Foydalanuvchining bildirishnomaga munosabati — bazada saqlanadigan
/// yagona narsa.
/// </summary>
public sealed class NotificationStateTests
{
    private static readonly Guid Foydalanuvchi = Guid.CreateVersion7();

    private static readonly string Kalit =
        NotificationKey.For(NotificationType.CandidateNew, Guid.CreateVersion7());

    private static readonly DateTimeOffset Payt = new(2026, 8, 29, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Togri_kalit_bilan_yaratiladi()
    {
        var natija = NotificationState.For(Foydalanuvchi, Kalit);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.UserId.ShouldBe(Foydalanuvchi);
        natija.Value.Key.ShouldBe(Kalit);
        natija.Value.IsRead.ShouldBeFalse();
        natija.Value.IsDismissed.ShouldBeFalse();
    }

    [Fact]
    public void Notogri_kalit_bilan_yaratilmaydi()
    {
        var natija = NotificationState.For(Foydalanuvchi, "shunchaki_matn");

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bildirishnoma.kalit_notogri");
    }

    [Fact]
    public void Foydalanuvchisiz_yaratilmaydi()
    {
        var natija = NotificationState.For(Guid.Empty, Kalit);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bildirishnoma.foydalanuvchi_yoq");
    }

    [Fact]
    public void Kalit_atrofidagi_boshliqlar_kesiladi()
    {
        var natija = NotificationState.For(Foydalanuvchi, "  " + Kalit + "  ");

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Key.ShouldBe(Kalit, "bo'shliq bilan saqlansa, kalit boshqa yozuvga tushardi");
    }

    /// <summary>
    /// «Hammasini o'qildi» tugmasi allaqachon o'qilganlarni ham qamraydi.
    /// Har bosishda sana yangilansa, «bu xabar qachon kelgan edi» degan
    /// savolga javob yo'qolardi.
    /// </summary>
    [Fact]
    public void Takroriy_oqish_birinchi_vaqtni_ozgartirmaydi()
    {
        var holat = NotificationState.For(Foydalanuvchi, Kalit).Value;

        holat.MarkRead(Payt);
        holat.MarkRead(Payt.AddDays(3));

        holat.ReadAt.ShouldBe(Payt);
    }

    [Fact]
    public void Ochirish_qaytarib_bolmaydi()
    {
        var holat = NotificationState.For(Foydalanuvchi, Kalit).Value;

        holat.Dismiss(Payt);
        holat.Dismiss(Payt.AddDays(1));

        holat.IsDismissed.ShouldBeTrue();
        holat.DismissedAt.ShouldBe(Payt);
    }
}
