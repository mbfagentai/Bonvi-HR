using Bonvi.Application.Blanks;

namespace Bonvi.Application.Tests.Blanks;

/// <summary>
/// HRMentor o'zgarishini kutish halqasining oraliqlari va reviziya shakli (SPEC-hujjat-manbai §7):
/// o'zgarishdan keyin darhol, xatoda yoki ketma-ket sinxronlashda 5 soniyadan ikki barobar besh
/// daqiqagacha — hech qachon halqa qizib ketmaydi, hech qachon besh daqiqalik yurishdan siyrak bo'lmaydi.
/// </summary>
public sealed class HrMentorWatchDelayTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 5)]
    [InlineData(3, 10)]
    [InlineData(100, 300)]
    public void Sinxronlangandan_keyin_darhol_ketma_ket_bolsa_sekinlashadi(int ketmaKet, int soniya)
    {
        HrMentorWatchDelay.After(HrMentorWatchOutcome.Synced, ketmaKet).ShouldBe(TimeSpan.FromSeconds(soniya));
        HrMentorWatchDelay.After(HrMentorWatchOutcome.SyncedWithError, ketmaKet).ShouldBe(TimeSpan.FromSeconds(soniya), "doimiy xato qolgan sinxronlash ham sinxronlash");
    }

    [Theory]
    [InlineData("hrmentor.texts_invalid", true)]
    [InlineData("hrmentor.definition_invalid", true)]
    [InlineData("hrmentor.blank_checksum", true)]
    [InlineData("hrmentor.blank_too_large", true)]
    [InlineData("hrmentor.unreachable", false)]
    [InlineData("hrmentor.timeout", false)]
    [InlineData("hrmentor.server_error", false)]
    [InlineData("hrmentor.rejected", false)]
    [InlineData("hrmentor.busy", false)]
    public void Faqat_HR_saqlagan_narsadan_kelgan_xato_doimiy(string kod, bool doimiy)
        => HrMentorErrors.IsLasting(kod).ShouldBe(doimiy);

    [Fact]
    public void Ozgarishsiz_javobdan_keyin_qisqa_tanaffus()
        => HrMentorWatchDelay.After(HrMentorWatchOutcome.Unchanged, 0).ShouldBe(TimeSpan.FromSeconds(1));

    [Theory]
    [InlineData(HrMentorWatchOutcome.NotConnected, 30)]
    [InlineData(HrMentorWatchOutcome.NotReady, 300)]
    [InlineData(HrMentorWatchOutcome.Off, 60)]
    public void Holatlar_oz_oraligi_bilan(HrMentorWatchOutcome holat, int soniya)
        => HrMentorWatchDelay.After(holat, 0).ShouldBe(TimeSpan.FromSeconds(soniya));

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(6, 160)]
    [InlineData(7, 300)]
    [InlineData(50, 300)]
    [InlineData(int.MaxValue, 300)]
    public void Xatoda_ikki_barobar_besh_daqiqagacha(int ketmaKet, int soniya)
    {
        HrMentorWatchDelay.After(HrMentorWatchOutcome.Failed, ketmaKet).ShouldBe(TimeSpan.FromSeconds(soniya));
        HrMentorWatchDelay.After(HrMentorWatchOutcome.SyncFailed, ketmaKet).ShouldBe(TimeSpan.FromSeconds(soniya));
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", true)]
    [InlineData("0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef", false)]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde", false)]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0", false)]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdeg", false)]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcd&x", false)]
    [InlineData("", false)]
    public void Reviziya_aynan_64_ta_kichik_hex(string qiymat, bool togri)
        => HrMentorRevision.IsValid(qiymat).ShouldBe(togri);
}
