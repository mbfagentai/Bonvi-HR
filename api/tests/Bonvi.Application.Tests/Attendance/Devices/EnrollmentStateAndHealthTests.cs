using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Agent;
using Bonvi.Application.Attendance.Devices;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Tests.Attendance.Devices;

/// <summary>
/// Ekrandagi yozish holati va qurilmalar sog'lig'i (2026-09-27 audit topilmalari 5, 6, 15, 18, 19 va egasining so'rovi).
/// </summary>
public sealed class EnrollmentStateAndHealthTests
{
    private const string Photo = "employees/a.jpg";
    private const string Name = "Aliyev Vali";

    private static readonly DateTimeOffset Now = new(2026, 9, 27, 21, 0, 0, TimeSpan.Zero);

    private static string Version(int force = 0, string name = Name) => EnrollmentVersion.Of(Photo, force, name);

    [Fact]
    public void Ochirib_bolmagan_ishdan_ketgan_xato_bolib_korinadi()
        => EnrollmentState.Of(Row(onRoster: false, DeviceEnrollmentStatus.RemoveFailed, applied: Version()))
            .ShouldBe(DeviceEnrollmentState.Failed);

    [Fact]
    public void Ishdan_ketgan_yozilgan_odam_olib_tashlanguncha_navbatda()
        => EnrollmentState.Of(Row(onRoster: false, DeviceEnrollmentStatus.Synced, applied: Version()))
            .ShouldBe(DeviceEnrollmentState.Pending);

    [Fact]
    public void Hech_qachon_yozilmagan_ishdan_ketgan_navbatda_turib_qolmaydi()
        => EnrollmentState.Of(Row(onRoster: false, DeviceEnrollmentStatus.Failed, applied: null))
            .ShouldBe(DeviceEnrollmentState.Removed);

    [Fact]
    public void Ism_ozgarsa_yozilgan_xodim_navbatga_tushadi()
    {
        EnrollmentState.Of(Row(onRoster: true, DeviceEnrollmentStatus.Synced, applied: Version())).ShouldBe(DeviceEnrollmentState.Synced);

        EnrollmentState.Of(Row(onRoster: true, DeviceEnrollmentStatus.Synced, applied: Version(), name: "Aliyev Valijon"))
            .ShouldBe(DeviceEnrollmentState.Pending);
    }

    [Fact]
    public void Rad_etilgan_belgi_eskirsa_navbatda_eskirmasa_xato()
    {
        EnrollmentState.Of(Row(onRoster: true, DeviceEnrollmentStatus.Failed, applied: null, rejected: Version()))
            .ShouldBe(DeviceEnrollmentState.Failed);

        // «Qayta yozish» bosildi — belgi o'zgardi, agent qayta urinmoqda.
        EnrollmentState.Of(Row(onRoster: true, DeviceEnrollmentStatus.Failed, applied: null, rejected: Version(), force: 1))
            .ShouldBe(DeviceEnrollmentState.Pending);
    }

    [Fact]
    public void Belgi_ismga_bogliq_rasm_va_sanoqqa_ham()
    {
        EnrollmentVersion.Of(Photo, 0, Name).ShouldBe(EnrollmentVersion.Of(Photo, 0, " " + Name + " "));
        EnrollmentVersion.Of(Photo, 0, Name).ShouldNotBe(EnrollmentVersion.Of(Photo, 0, "Boshqa Ism"));
        EnrollmentVersion.Of(Photo, 0, Name).ShouldNotBe(EnrollmentVersion.Of(Photo, 1, Name));
        EnrollmentVersion.Of(Photo, 0, Name).Length.ShouldBeLessThanOrEqualTo(200);
    }

    [Fact]
    public void Qator_xodim_rasmini_beradi()
        => EnrollmentState.ToDto(Row(onRoster: true, DeviceEnrollmentStatus.Synced, applied: Version())).EmployeePhotoKey.ShouldBe(Photo);

    [Fact]
    public void Sogliq_mobil_ilova_qatorini_sanamaydi_va_uzilganlarni_nomi_bilan_beradi()
    {
        var mobile = AttendanceDevice.Register(
            "Mobil ilova", new byte[AttendanceDevice.TokenHashLength], "mobil123", Now.AddDays(-30),
            provider: AttendanceProvider.Mobile).Value;

        var silent = Device("Kirish");
        silent.NoteClock(0, Now.AddMinutes(-30));

        var unreachable = Device("Chiqish");
        unreachable.NoteClock(0, Now.AddMinutes(-30));
        unreachable.NoteAgent(Now.AddMinutes(-1), null);

        var healthy = Device("Ombor");
        healthy.NoteClock(600, Now.AddMinutes(-1));

        var never = Device("Yangi");

        var health = AttendanceDeviceHealth.Of([mobile, silent, unreachable, healthy, never], Now);

        health.ActiveDevices.ShouldBe(4);
        health.ConnectedDevices.ShouldBe(3);
        health.ClockWarningDevices.ShouldBe(1);
        health.Outages.Select(outage => (outage.DeviceName, outage.State)).ShouldBe(
        [
            ("Chiqish", DeviceLinkState.TerminalUnreachable),
            ("Kirish", DeviceLinkState.AgentOffline),
        ]);
    }

    private static AttendanceDevice Device(string name)
        => AttendanceDevice.Register(name, new byte[AttendanceDevice.TokenHashLength], "abcdefgh", Now.AddDays(-30)).Value;

    private static EnrollmentRow Row(
        bool onRoster,
        DeviceEnrollmentStatus? status,
        string? applied,
        string? rejected = null,
        int force = 0,
        string name = Name)
        => new(
            Guid.NewGuid(),
            "MinMoe",
            Guid.NewGuid(),
            name,
            Photo,
            onRoster,
            "900000001",
            status,
            applied,
            force,
            Error: null,
            SyncedAt: Now,
            rejected);
}
