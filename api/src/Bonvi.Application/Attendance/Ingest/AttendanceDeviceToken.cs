using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Ingest;

/// <summary>
/// Qurilma tokenining FORMATI — yasash, xeshlash va ko'rsatish prefiksi bitta joyda.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega bitta sinf.</b> Tokenni ikki xil kod ishlatadi: qurilmani
/// ro'yxatga olish / tokenni almashtirish (xeshni YOZADI) va qabul yo'li
/// (xeshni SOLISHTIRADI). Ular xeshni ikki xil hisoblasa — masalan biri
/// satrning UTF-8 baytlarini, ikkinchisi base64url dan ochilgan 32 baytni
/// xeshlasa — HECH BIR qurilma o'ta olmaydi va xato «401» dan boshqa hech
/// narsa bermaydi. Shu sababli format faqat shu yerda yashaydi va ikkala
/// tomon ham shu metodlarni chaqiradi.
/// </para>
/// <para>
/// <b>Nega SHA-256, parol KDF emas.</b> Token — 256 bit mashina tanlagan
/// tasodif, taxmin qilinadigan narsa yo'q; Argon2/bcrypt har bir o'tishning
/// issiq yo'liga ~100 ms qo'shardi, xolos (<see cref="AttendanceDevice"/> izohi).
/// </para>
/// </remarks>
public static class AttendanceDeviceToken
{
    /// <summary>Token entropiyasi — bayt.</summary>
    public const int EntropyBytes = 32;

    /// <summary>
    /// Yangi token yasaydi — 32 tasodifiy bayt, base64url (43 belgi, to'ldiruvchisiz).
    /// </summary>
    /// <remarks>
    /// base64url, chunki token URL YO'LIGA qo'yiladi (Hikvision faqat URL
    /// sozlay oladi): oddiy base64 dagi <c>/</c> va <c>+</c> yo'lni buzardi.
    /// Token FAQAT bir marta — yasalgan javobda — ko'rsatiladi; bazaga
    /// <see cref="Hash"/> va <see cref="PrefixOf"/> yoziladi.
    /// </remarks>
    /// <returns>Ochiq token.</returns>
    public static string Generate()
        => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(EntropyBytes));

    /// <summary>Token xeshi — <c>attendance_devices.token_hash</c> ga yoziladigan va qidiriladigan qiymat.</summary>
    /// <remarks>
    /// Satrning UTF-8 baytlari xeshlanadi, base64url dan OCHILMAYDI: qabul
    /// yo'liga har qanday satr kelishi mumkin (noto'g'ri base64 ham) va u
    /// istisnosiz, bir xil vaqtda «topilmadi» bo'lishi kerak.
    /// </remarks>
    /// <param name="token">Ochiq token.</param>
    /// <returns>32 baytlik SHA-256 xeshi.</returns>
    public static byte[] Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }

    /// <summary>Ekranda (va logda) ko'rsatiladigan boshlang'ich belgilar.</summary>
    /// <param name="token">Ochiq token.</param>
    /// <returns>Birinchi <see cref="AttendanceDevice.TokenPrefixLength"/> belgi.</returns>
    public static string PrefixOf(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return token.Length <= AttendanceDevice.TokenPrefixLength
            ? token
            : token[..AttendanceDevice.TokenPrefixLength];
    }
}
