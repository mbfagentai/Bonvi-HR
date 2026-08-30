using System.Text;
using Bonvi.Domain.Abstractions;

namespace Bonvi.Application.Tests.Settings;

/// <summary>
/// Sirni shifrlashning testdagi o'rinbosari — TESKARILANADIGAN o'girish.
/// </summary>
/// <remarks>
/// <para>
/// Mock EMAS (<c>FakeSystemSettings</c> dagi bilan bir xil sabab):
/// testlar «yozildi va keyin o'qildi» zanjirini tekshiradi, ya'ni
/// o'girish haqiqatan teskarilanadigan bo'lishi shart.
/// </para>
/// <para>
/// <b>Bu XAVFSIZLIK emas va bo'lishi ham shart emas.</b> Testlar
/// tekshiradigan narsa — sirning ochiq matni bazaga TUSHMASLIGI va
/// o'qishda qaytib kelishi. Haqiqiy kriptografiya
/// (<c>AesGcmSecretProtector</c>) o'z o'rnida ishlaydi va uni bu yerda
/// takrorlash testni sekinlashtirardi, xolos.
/// </para>
/// <para>
/// <see cref="Prefix"/> ataylab ko'zga tashlanadigan: agar biror joyda
/// shifrlash o'tkazib yuborilsa, test xabarida farq darhol ko'rinadi.
/// </para>
/// </remarks>
internal sealed class FakeSecretProtector : ISecretProtector
{
    private const string Prefix = "test-enc:";

    /// <summary>Nechta qiymat shifrlangani — «shifrlash chaqirildimi?» uchun.</summary>
    public int ProtectCalls { get; private set; }

    public string Protect(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);

        ProtectCalls++;

        return Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
    }

    public string? Unprotect(string protectedText)
    {
        if (string.IsNullOrEmpty(protectedText) || !protectedText.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // Haqiqiy amalga oshirishdagi bilan bir xil xatti-harakat:
            // notanish format — istisno emas, `null`.
            return null;
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(protectedText[Prefix.Length..]));
    }
}
