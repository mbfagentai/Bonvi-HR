using System.Security.Cryptography;

namespace Bonvi.Application.Identity.EmployeeAccounts;

/// <summary>
/// HR beradigan bir martalik parolning FORMATI — bitta joyda.
/// </summary>
/// <remarks>
/// <para>
/// Parol og'zaki aytiladi va qog'ozga ko'chiriladi: tizimda uni yetkazadigan
/// kanal yo'q (SMS ham, pochta ham). Shu sababli format ikkita talabga
/// bo'ysunadi — taxmin qilib bo'lmasligi va OG'ZAKI aytilganda
/// noto'g'ri tushunilmasligi.
/// </para>
/// <para>
/// ⚠️ Alifbodan <c>0/O</c>, <c>1/l/I</c>, <c>5/S</c> va <c>8/B</c> ATAYLAB
/// chiqarilgan. Ularsiz 10 belgi ≈ 50 bit entropiya beradi (32^10), ya'ni
/// taxmin qilishga yaroqsiz; ular bilan esa 950 ta parolning bir qismi
/// birinchi kuniyoq «men kirolmayapman» degan qo'ng'iroqqa aylanardi.
/// </para>
/// <para>
/// <b>Nega tasodifni server tanlaydi, HR emas.</b> Qurilma tokenidagi bilan
/// bir xil sabab: 950 ta parolni odam o'ylab topsa, ularning ko'pi
/// tug'ilgan yil yoki telefon raqamining oxiri bo'lardi.
/// </para>
/// </remarks>
public static class OneTimePassword
{
    /// <summary>Chalkashtirib bo'lmaydigan belgilar.</summary>
    private const string Alphabet = "ACDEFGHJKLMNPQRTUVWXYZ2346789";

    /// <summary>Uzunlik — belgi.</summary>
    /// <remarks>
    /// <c>PasswordPolicy.MinLength</c> (8) dan uzunroq: siyosat ODAM
    /// tanlaydigan parolning quyi chegarasi, bu esa mashina tanlaydigan
    /// qiymat va u chegarada turishi shart emas.
    /// </remarks>
    public const int Length = 10;

    /// <summary>Yangi bir martalik parol yasaydi.</summary>
    /// <remarks>
    /// <c>RandomNumberGenerator.GetString</c> — bir tekis taqsimotni o'zi
    /// kafolatlaydi. Qo'lda yozilgan <c>% alphabet.Length</c> esa alifbo
    /// uzunligi 2 ning darajasi bo'lmaganda birinchi belgilarni
    /// ko'proq tanlardi (modulo siljishi).
    /// </remarks>
    public static string Generate() => RandomNumberGenerator.GetString(Alphabet, Length);
}
