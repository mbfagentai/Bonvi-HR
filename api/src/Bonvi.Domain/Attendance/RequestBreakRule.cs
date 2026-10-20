using System.Globalization;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>
/// «Kunni o'zgartirish» so'rovidagi tanaffus qoidasi — kompaniya siyosati
/// (SPEC-verifikatsiya-sorov-tahriri §4, egasining qarori 2026-10-20).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Qoida.</b> Tanaffus <see cref="From"/>–<see cref="To"/> oralig'ida (sukut 12:00–15:00)
/// turadi va uzunligi dushanba–payshanba, shanba va yakshanbada AYNAN <see cref="Minutes"/>
/// (sukut 60), jumada <see cref="Minutes"/> … <see cref="FridayMaxMinutes"/> (60–120, juma
/// namozi). Tanaffussiz ish kuni ham mumkin (13:00–18:00 — egasining misoli).
/// </para>
/// <para>
/// <b>Qoida nimani o'lchaydi</b> (§4.1): amaldagi tanaffus — so'rov kunida SAQLANADIGAN va
/// keyin ushlanadigan son (<c>ScheduleChangeRequest.BreakMinutes</c>): berilgan
/// <c>breakMinutes</c>, aks holda oynaning uzunligi, oynasiz 0. Shunda qoida aynan hisoblanadigan
/// narsani tekshiradi: jadvaldan tegilmagan oddiy kun (12:00–15:00 oynasi, 60 daqiqa) o'tadi.
/// </para>
/// <para>
/// Sonlar tizim sozlamalarida (<c>attendance.requestBreak*</c>) va YOZISH lahzasida o'qiladi;
/// mavjud so'rovlar qayta tekshirilmaydi (qaror va bekor qilishda qoida yo'q). «Kun
/// almashtirish» ozod — u yerda kunni server jadvaldan ko'chiradi, xodim tanaffus tanlamaydi.
/// </para>
/// </remarks>
/// <param name="From">Ruxsat etilgan oraliq boshi (soat, Toshkent).</param>
/// <param name="To">Ruxsat etilgan oraliq oxiri.</param>
/// <param name="Minutes">Talab qilingan uzunlik (daqiqa); jumada — eng kami.</param>
/// <param name="FridayMaxMinutes">Jumada eng ko'pi (daqiqa).</param>
public sealed record RequestBreakRule(TimeOnly From, TimeOnly To, int Minutes, int FridayMaxMinutes)
{
    /// <summary>Sozlama yozilmagan tizimdagi qoida — 12:00–15:00, 60, jumada 120 gacha.</summary>
    public static RequestBreakRule Default { get; } = new(
        TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(AttendanceRules.DefaultRequestBreakFromMinute)),
        TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(AttendanceRules.DefaultRequestBreakToMinute)),
        AttendanceRules.DefaultRequestBreakMinutes,
        AttendanceRules.DefaultRequestBreakFridayMaxMinutes);

    /// <summary>
    /// Sonlar bir-biriga mosmi: oraliq boshi oxiridan oldin, oddiy uzunlik jumadagi chegaradan
    /// oshmaydi va jumadagi chegara oraliqqa sig'adi.
    /// </summary>
    /// <remarks>
    /// Sozlamalar ekrani buni saqlashdan oldin talab qiladi (<c>davomat.tanaffus_qoidasi_nomuvofiq</c>);
    /// bu yerda — bazani qo'lda tahrirlagan holat uchun: o'quvchi mos kelmagan qoida o'rniga
    /// <see cref="Default"/> ni oladi, aks holda hech bir tanaffus o'tmasdi.
    /// </remarks>
    public bool IsConsistent
        => Minutes > 0
            && From < To
            && Minutes <= FridayMaxMinutes
            && FridayMaxMinutes <= (int)(To - From).TotalMinutes;

    /// <summary>
    /// Bitta so'ralgan kunni tekshiradi — mavjud kun qoidalaridan KEYIN (SPEC §4.3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tartib: dam olish kuni va tanaffussiz kun (amaldagi uzunlik 0) — o'tadi; oyna bo'lsa u
    /// oraliq ichida (soat qiymatlari bo'yicha — yarim tundan o'tgan oyna hech qachon sig'maydi)
    /// va amaldagi uzunlikdan qisqa emas; keyin uzunlik hafta kuniga qarab.
    /// </para>
    /// <para>
    /// Oynasiz, lekin daqiqasi bor kun (jadvaldan tegilmay ko'chgan) faqat uzunlik bo'yicha
    /// baholanadi — joyini tekshirib bo'lmaydi, oynasiz kunda esa hech narsa ushlanmaydi ham.
    /// </para>
    /// </remarks>
    /// <param name="day">So'ralgan kun.</param>
    internal Result Validate(ScheduleChangeDayInput day)
    {
        ArgumentNullException.ThrowIfNull(day);

        if (!day.IsWorking)
        {
            return Result.Success();
        }

        var length = ScheduleChangeRequest.BreakMinutes(day);
        if (length == 0)
        {
            return Result.Success();
        }

        if (day is { BreakStart: { } start, BreakEnd: { } end })
        {
            if (!(From <= start && start < end && end <= To))
            {
                return OutsideWindow();
            }

            if (length > (int)(end - start).TotalMinutes)
            {
                return WrongLength();
            }
        }

        var isFriday = day.Date.DayOfWeek == DayOfWeek.Friday;
        var fits = isFriday
            ? length >= Minutes && length <= FridayMaxMinutes
            : length == Minutes;

        return fits ? Result.Success() : WrongLength();
    }

    private Error OutsideWindow()
        => Error.Validation(
            "davomat.jadval_sorovi_tanaffus_oraligi",
            string.Create(
                CultureInfo.InvariantCulture,
                $"Tanaffus {From:HH\\:mm}–{To:HH\\:mm} oralig'ida bo'lishi kerak."));

    private Error WrongLength()
        => Error.Validation(
            "davomat.jadval_sorovi_tanaffus_davomiyligi",
            string.Create(
                CultureInfo.InvariantCulture,
                $"Tanaffus {Minutes} daqiqa (jumada {Minutes}–{FridayMaxMinutes}) bo'lishi va oynasiga sig'ishi kerak."));
}
