using Bonvi.Domain.Attendance;

namespace Bonvi.Domain.Tests.Attendance;

/// <summary>
/// Qurilmaning aloqa holati, terminal bog'lanishi va soat hukmi (SPEC-agent §L).
/// </summary>
/// <remarks>
/// ⭐ Chegara jadvalga bog'liq EMAS: agent o'tish bo'lmasa ham har daqiqada
/// gapiradi, ya'ni tunda va dam olish kunida ham o'n daqiqalik jimlik —
/// haqiqiy uzilish. Aksincha, tunda agent gapirib turgan bo'lsa, o'tish
/// yo'qligi hech narsa demaydi.
/// </remarks>
public sealed class AttendanceDeviceHealthTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 21, 0, 0, TimeSpan.Zero); // Toshkentda tungi 02:00

    [Fact]
    public void Hech_qachon_gapirmagan_qurilma_ulanmagan()
        => Device().LinkAt(Now).ShouldBe((DeviceLinkState.NeverConnected, null));

    [Fact]
    public void Ozi_push_qiladigan_terminal_jimligi_uzilish_emas()
    {
        var device = Device();
        device.NoteSeen(Now.AddDays(-2), "10.0.0.5");

        device.LinkAt(Now).State.ShouldBe(DeviceLinkState.Online, "hodisa bo'lmasa u gapirmaydi");
    }

    [Fact]
    public void Tunda_otish_yoq_lekin_agent_va_terminal_javob_beryapti_ishlayapti()
    {
        var device = Device();
        device.NoteClock(0, Now.AddMinutes(-1));
        device.NoteAgent(Now.AddSeconds(-30), "10.0.0.5");

        device.LinkAt(Now).State.ShouldBe(DeviceLinkState.Online);
        device.LastEventAt.ShouldBeNull();
    }

    [Theory]
    [InlineData(10, DeviceLinkState.Online)]
    [InlineData(11, DeviceLinkState.AgentOffline)]
    [InlineData(600, DeviceLinkState.AgentOffline)]
    public void Agent_on_daqiqadan_kop_jim_bolsa_uzilgan(int minutes, DeviceLinkState expected)
    {
        var device = Device();
        device.NoteClock(0, Now.AddMinutes(-minutes));

        var (state, since) = device.LinkAt(Now);

        state.ShouldBe(expected);
        if (expected == DeviceLinkState.AgentOffline)
        {
            since.ShouldBe(Now.AddMinutes(-minutes));
        }
    }

    [Fact]
    public void Agent_tirik_terminal_jim_bolsa_terminal_uzilgan_qachondan_beri_bilan()
    {
        var device = Device();
        device.NoteClock(0, Now.AddMinutes(-45));
        device.NoteAgent(Now.AddMinutes(-1), "10.0.0.5");

        device.LinkAt(Now).ShouldBe((DeviceLinkState.TerminalUnreachable, Now.AddMinutes(-45)));
    }

    [Fact]
    public void Terminal_birinchi_kundan_javob_bermasa_ham_uzilgan()
    {
        var device = Device();
        device.NoteAgent(Now.AddMinutes(-1), "10.0.0.5");

        device.LinkAt(Now).ShouldBe((DeviceLinkState.TerminalUnreachable, null));
    }

    [Fact]
    public void Terminal_seriyasi_birinchi_korilgani_yoziladi_boshqasi_rad_etiladi()
    {
        var device = Device();

        device.ConfirmTerminal(null, required: true).IsSuccess.ShouldBeTrue("hali bog'lanmagan");
        device.ConfirmTerminal("SERIAL-A", required: true).IsSuccess.ShouldBeTrue();
        device.TerminalSerial.ShouldBe("SERIAL-A");

        device.ConfirmTerminal("serial-a", required: true).IsSuccess.ShouldBeTrue("registr ahamiyatsiz");
        device.ConfirmTerminal("SERIAL-B", required: true).Error.Code.ShouldBe("davomat.terminal_mos_emas");
        device.ConfirmTerminal(null, required: true).Error.Code.ShouldBe("davomat.terminal_mos_emas");
        device.ConfirmTerminal(null, required: false).IsSuccess.ShouldBeTrue("qabul yo'lida push terminal seriya bermaydi");
        device.TerminalSerial.ShouldBe("SERIAL-A");
    }

    [Fact]
    public void Token_aylantirilsa_terminal_boglanishi_tozalanadi()
    {
        var device = Device();
        device.ConfirmTerminal("SERIAL-A", required: true);

        device.RotateToken(new byte[AttendanceDevice.TokenHashLength], "yangi123", Now, by: null);

        device.TerminalSerial.ShouldBeNull();
        device.ConfirmTerminal("SERIAL-B", required: true).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Rad_etilgan_sorov_belgisi_keyingi_otish_bilan_ochadi()
    {
        var device = Device();

        device.NoteRejected(Now.AddMinutes(-5));
        device.EventsRejected.ShouldBeTrue();

        device.NoteEvent(Now, "10.0.0.5", skewSeconds: 1);
        device.EventsRejected.ShouldBeFalse();
    }

    [Fact]
    public void Soat_ogohlantirishi_olchangan_ogishdan_chiqadi_qabul_kechikishidan_emas()
    {
        var device = Device();

        // Internet uzilishidan keyin yuborilgan o'tish — 40 daqiqa «kechikkan».
        device.NoteEvent(Now, "10.0.0.5", skewSeconds: 40 * 60);
        device.ClockWarningAt(Now).ShouldBeTrue("o'lchov yo'q — eski manba");

        device.NoteClock(offsetSeconds: 2, Now);
        device.ClockWarningAt(Now).ShouldBeFalse("terminal soati to'g'ri — o'lchov ustun");

        device.NoteClock(offsetSeconds: 900, Now);
        device.ClockWarningAt(Now).ShouldBeTrue();
        device.ClockWarningAt(Now.AddHours(25)).ShouldBeFalse("bir sutkadan beri jim — «uzilgan» belgisi bor");
    }

    [Fact]
    public void Otish_soat_hukmi_olchangan_ogishdan()
    {
        var occurred = Now.AddMinutes(-40);

        var buffered = AttendancePass.Record(
            Guid.NewGuid(), AttendanceIdentityProvider.Hikvision, "900000001", occurred, Now, "k1",
            deviceClockOffsetSeconds: 3).Value;

        buffered.ClockSkewSeconds.ShouldBe(40 * 60, "qabul ayirmasi saqlanadi");
        buffered.EffectiveClockSkewSeconds.ShouldBe(-3);
        buffered.HasClockDrift.ShouldBeFalse();

        var pushed = AttendancePass.Record(
            Guid.NewGuid(), AttendanceIdentityProvider.Verifix, "17", occurred, Now, "k2").Value;

        pushed.HasClockDrift.ShouldBeTrue("o'lchov yo'q — eski qoida");
    }

    [Fact]
    public void Ommaviy_ochirish_ruxsati_vaqt_bilan_cheklangan()
    {
        var device = Device();
        device.RemovalsApprovedAt(Now).ShouldBeFalse();

        device.ApproveRemovals(Now.AddMinutes(10));

        device.RemovalsApprovedAt(Now.AddMinutes(9)).ShouldBeTrue();
        device.RemovalsApprovedAt(Now.AddMinutes(10)).ShouldBeFalse();
    }

    private static AttendanceDevice Device()
        => AttendanceDevice.Register("MinMoe", new byte[AttendanceDevice.TokenHashLength], "abcdefgh", Now.AddDays(-30)).Value;
}
