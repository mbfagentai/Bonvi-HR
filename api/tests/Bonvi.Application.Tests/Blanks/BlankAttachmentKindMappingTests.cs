using Bonvi.Application.Blanks.Files;
using Bonvi.Domain.Blanks;

namespace Bonvi.Application.Tests.Blanks;

/// <summary>
/// <see cref="BlankFileKind"/> (aniqlangan tur) va <see cref="BlankAttachmentKind"/> (bazadagi tur)
/// bir xil yetti tur, bir xil raqamlar bilan — 1:1 (SPEC-blankalar §3.5, §8).
/// </summary>
public sealed class BlankAttachmentKindMappingTests
{
    [Fact]
    public void Ikki_sanoq_bir_xil_nom_va_raqamlarga_ega()
    {
        var fayl = Enum.GetValues<BlankFileKind>().Select(k => (k.ToString(), (int)k)).ToArray();
        var biriktirma = Enum.GetValues<BlankAttachmentKind>().Select(k => (k.ToString(), (int)k)).ToArray();

        biriktirma.ShouldBe(fayl);
        fayl.Select(k => k.Item2).ShouldBe([1, 2, 3, 4, 5, 6, 7]);
    }

    [Fact]
    public void Har_bir_tur_ikki_tomonga_aylanadi_va_raqami_saqlanadi()
    {
        foreach (var kind in Enum.GetValues<BlankFileKind>())
        {
            var saqlangan = kind.ToAttachmentKind();

            ((int)saqlangan).ShouldBe((int)kind);
            saqlangan.ToString().ShouldBe(kind.ToString());
            saqlangan.ToFileKind().ShouldBe(kind);
        }
    }

    [Fact]
    public void Nomalum_qiymat_rad_etiladi()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ((BlankFileKind)99).ToAttachmentKind());
        Should.Throw<ArgumentOutOfRangeException>(() => ((BlankAttachmentKind)0).ToFileKind());
    }
}
