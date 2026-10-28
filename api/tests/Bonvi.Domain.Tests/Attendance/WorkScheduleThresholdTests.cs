using System.Globalization;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.Tests.Attendance;

/// <summary>
/// Erta kelish va kech ketish chegarasi — jadvalning o'zida, 0 … 240 daqiqa (egasining qarori, 2026-10-28).
/// </summary>
public sealed class WorkScheduleThresholdTests
{
    private const string Kod = "davomat.jadval_chegara";

    [Fact]
    public void Berilmasa_ikkalasi_nol()
    {
        var jadval = WorkSchedule.Create("Sinov jadvali", HaftaKunlari()).Value;

        jadval.EarlyArrivalThresholdMinutes.ShouldBe(0);
        jadval.LateDepartureThresholdMinutes.ShouldBe(0);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(30, 60)]
    [InlineData(WorkSchedule.MaxThresholdMinutes, WorkSchedule.MaxThresholdMinutes)]
    public void Chegara_ichidagi_qiymat_saqlanadi(int erta, int kech)
    {
        var jadval = Yarat(erta, kech).Value;

        jadval.EarlyArrivalThresholdMinutes.ShouldBe(erta);
        jadval.LateDepartureThresholdMinutes.ShouldBe(kech);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(WorkSchedule.MaxThresholdMinutes + 1, 0)]
    [InlineData(0, WorkSchedule.MaxThresholdMinutes + 1)]
    public void Chegaradan_tashqari_qiymat_rad_etiladi(int erta, int kech)
    {
        var natija = Yarat(erta, kech);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Type.ShouldBe(ErrorType.Validation);
        natija.Error.Code.ShouldBe(Kod);
    }

    [Fact]
    public void Tahrirda_yangilanadi()
    {
        var jadval = Yarat(30, 60).Value;

        jadval.Update("Sinov jadvali", HaftaKunlari(), 10, 10, earlyArrivalThresholdMinutes: 15, lateDepartureThresholdMinutes: 0)
            .IsSuccess.ShouldBeTrue();

        jadval.EarlyArrivalThresholdMinutes.ShouldBe(15);
        jadval.LateDepartureThresholdMinutes.ShouldBe(0);
    }

    [Fact]
    public void Tahrirda_noto_g_ri_qiymat_hech_narsani_o_zgartirmaydi()
    {
        var jadval = Yarat(30, 60).Value;

        jadval.Update("Boshqa nom", HaftaKunlari(), 10, 10, earlyArrivalThresholdMinutes: 300)
            .Error.Code.ShouldBe(Kod);

        jadval.Name.ShouldBe("Sinov jadvali");
        jadval.EarlyArrivalThresholdMinutes.ShouldBe(30);
        jadval.LateDepartureThresholdMinutes.ShouldBe(60);
    }

    private static Result<WorkSchedule> Yarat(int erta, int kech)
        => WorkSchedule.Create(
            "Sinov jadvali",
            HaftaKunlari(),
            earlyArrivalThresholdMinutes: erta,
            lateDepartureThresholdMinutes: kech);

    private static WorkScheduleDayInput[] HaftaKunlari()
        =>
        [
            .. Enumerable.Range(1, WorkSchedule.WeeklyDayCount)
                .Select(index => new WorkScheduleDayInput(index, IsWorking: true, Soat("09:00"), Soat("18:00"))),
        ];

    private static TimeOnly Soat(string value) => TimeOnly.Parse(value, CultureInfo.InvariantCulture);
}
