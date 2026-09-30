namespace Bonvi.Application.Attendance.Mobile;

/// <summary>
/// Telefondan belgilashning o'lchov chegaralari — bitta joyda.
/// </summary>
/// <remarks>
/// <para>
/// <c>AttendanceRules</c> ga QO'SHILMADI va bu ataylab: u davomat YADROSINING
/// chegaralari (soat og'ishi, kun oynasi, saqlash muddati) va ularni davomat
/// hisoblash ham, qabul yo'li ham o'qiydi. Bu yerdagilar esa faqat bitta
/// endpointga tegishli va yadro ular haqida hech narsa bilmaydi.
/// </para>
/// <para>
/// ⚠️ Takror qoidasining o'zi bu yerda YO'Q — u
/// <c>AttendanceRules.BounceSeconds</c> dan olinadi. Telefonga alohida son
/// qo'yilsa, bitta kunda ikki xil «takror» ta'rifi bo'lib qolardi.
/// </para>
/// </remarks>
public static class MobileCheckInRules
{
    /// <summary>
    /// Telefon e'lon qilgan aniqlik shundan yomon bo'lsa, belgi rad etiladi (metr).
    /// </summary>
    /// <remarks>
    /// GPS ochiq havoda 5–20 m, bino ichida 20–100 m beradi. 100 — bino
    /// ichidagi odamni rad etmaydigan, lekin «shahar darajasidagi» taxminni
    /// (Wi-Fi yoki uyali tarmoq bo'yicha aniqlash, odatda 500–2000 m)
    /// o'tkazmaydigan chegara. Qattiqroq qo'yilsa, ilova aynan kerak
    /// bo'lgan joyda — omborning ichida — ishlamay qolardi.
    /// </remarks>
    public const int MaxAccuracyMeters = 100;

    /// <summary>
    /// Kenglik/uzunlikning saqlanadigan kasr xonasi — <c>numeric(9,6)</c>.
    /// </summary>
    /// <remarks>
    /// Olti xona — Toshkent kengligida ~8–11 sm. O'lchovning o'zi eng yaxshi
    /// holatda 5 m, ya'ni bundan nozikroq saqlash bezak bo'lardi. Yaxlitlash
    /// APPLICATION da bajariladi: bazaga sig'maydigan son bilan borish
    /// tushunarsiz 500 berardi, bu yerda esa qiymat ataylab qisqartiriladi.
    /// </remarks>
    public const int CoordinateScale = 6;

    /// <summary>Aniqlik va masofaning kasr xonasi — <c>numeric(…,1)</c>.</summary>
    public const int MeterScale = 1;
}
