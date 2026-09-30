using Bonvi.Application.Attendance.Devices;
using Bonvi.Application.Attendance.Ingest;

namespace Bonvi.Application.Tests.Attendance.Devices;

/// <summary>
/// Qurilma IP ro'yxati saqlashda tekshiriladi — va qabul yo'li bilan AYNAN bir xil o'qiladi.
/// </summary>
public sealed class AllowedIpListTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("10.0.0.5")]
    [InlineData("10.0.0.0/8")]
    [InlineData("10.0.0.5/8")]
    [InlineData("192.168.1.0/24, 2001:db8::/32")]
    [InlineData("::ffff:10.0.0.1/32")]
    [InlineData("0.0.0.0/0")]
    public void Togri_royxat_qabul_qilinadi(string? allowedIps)
        => AllowedIpList.IsValid(allowedIps).ShouldBeTrue();

    [Theory]
    [InlineData("10.0.0.0/33")]
    [InlineData("2001:db8::/129")]
    [InlineData("10.0.0.0/-1")]
    [InlineData("10.0.0.0/ 8")]
    [InlineData("ofis")]
    [InlineData("10.0.0.1,,10.0.0.2")]
    [InlineData("10.0.0.1,")]
    public void Buzuq_royxat_rad_etiladi(string allowedIps)
        => AllowedIpList.IsValid(allowedIps).ShouldBeFalse();

    /// <summary>
    /// Saqlashda qabul qilingan har bir yozuvni qabul yo'li ham tushunadi: o'zi qamragan
    /// manzilni o'tkazadi. Aks holda saqlangan ro'yxat qurilmani jimgina qulflardi.
    /// </summary>
    [Theory]
    [InlineData("10.0.0.5/8", "10.200.3.4")]
    [InlineData("192.168.1.7", "192.168.1.7")]
    [InlineData("2001:db8::/32", "2001:db8:1::5")]
    public void Saqlashda_qabul_qilingan_yozuv_qabul_yolida_ham_ishlaydi(string allowedIps, string source)
    {
        AllowedIpList.IsValid(allowedIps).ShouldBeTrue();
        AttendanceDeviceAuthenticator.SourceAllowed(allowedIps, source).ShouldBeTrue();
    }
}
