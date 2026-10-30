using Bonvi.Application.Notifications;
using Bonvi.Application.Notifications.Dtos;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Notifications;

namespace Bonvi.Application.Tests.Notifications;

/// <summary>
/// «Avval botdan, keyin ofisda anketa» — mavjud nomzodning kartasiga onlayn anketa qo'shildi
/// (egasi, 2026-10-30). Kim ro'yxatga tushishi bazada kesiladi va
/// <c>KioskQuestionnaireApiTests.Qayta_murojaat_qilgan_nomzod_royxatda_tepada_filtrda_va_bildirishnomada</c>
/// da haqiqiy PostgreSQL ustida ko'riladi; bu yerda uzatilgan chegara va qatordan yasalgan yozuv.
/// </summary>
public sealed class CandidateReappliedNotificationTests
{
    private readonly NotificationTestContext _context = new();

    [Fact]
    public async Task Yozuv_kartaga_olib_boradi_kok_darajada_va_anketa_kunida()
    {
        // 19:30 UTC — Toshkentda ertasi kun 00:30
        var qator = _context.Reapplied(new DateTimeOffset(2026, 8, 28, 19, 30, 0, TimeSpan.Zero));

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Type.ShouldBe(NotificationType.CandidateReapplied);
        item.Severity.ShouldBe(NotificationSeverity.Info, "anketani HR o'zi boshlab beradi — javobsiz ariza emas");
        item.Date.ShouldBe(new DateOnly(2026, 8, 29), "kun Toshkent kalendarida");
        item.Link.Route.ShouldBe(NotificationRoute.Candidate);
        item.Link.Id.ShouldBe(qator.CandidateId);

        item.Params[NotificationParams.CandidateName].ShouldBe("Karimov Aziz");
        item.Params[NotificationParams.Source].ShouldBe("TelegramBot");
        item.Params[NotificationParams.AddedOn].ShouldBe("2026-08-19", "birinchi murojaat kuni — matnda «avval … da»");
        item.Params[NotificationParams.VacancyTitle].ShouldBe("Omborchi");
    }

    [Fact]
    public async Task Oyna_yangi_nomzod_sozlamasidan_olinadi()
    {
        _context.Reapplied(new DateTimeOffset(2026, 8, 29, 5, 0, 0, TimeSpan.Zero));

        await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        _context.Source.LastReappliedSince.ShouldBe(
            NotificationTestContext.Today.AddDays(-NotificationRules.NewCandidateDays));
    }

    [Fact]
    public async Task Har_anketa_ozining_kaliti_bilan_oqilgani_yangisini_yashirmaydi()
    {
        var birinchi = _context.Reapplied(new DateTimeOffset(2026, 8, 20, 6, 0, 0, TimeSpan.Zero));
        var ikkinchi = birinchi with { ReappliedAt = new DateTimeOffset(2026, 8, 29, 6, 0, 0, TimeSpan.Zero) };

        NotificationKey.For(NotificationType.CandidateReapplied, birinchi.CandidateId, birinchi.ReappliedAt)
            .ShouldNotBe(NotificationKey.For(NotificationType.CandidateReapplied, ikkinchi.CandidateId, ikkinchi.ReappliedAt));

        NotificationKey.TypeOf(NotificationKey.For(NotificationType.CandidateReapplied, birinchi.CandidateId, birinchi.ReappliedAt))
            .ShouldBe(NotificationType.CandidateReapplied);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task Rekruting_ruxsati_yoq_bolsa_chiqmaydi()
    {
        _context.Reapplied(new DateTimeOffset(2026, 8, 29, 5, 0, 0, TimeSpan.Zero));
        _context.Deny(Permissions.RecruitingView);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        natija.ShouldNotContain(item => item.Type == NotificationType.CandidateReapplied);
        NotificationAccess.RequiredPermission(NotificationType.CandidateReapplied).ShouldBe(Permissions.RecruitingView);
        ((int)NotificationType.CandidateReapplied).ShouldBe(14);
        NotificationKey.Prefix(NotificationType.CandidateReapplied).ShouldBe("nomzod.anketa_toldirdi");
    }
}
