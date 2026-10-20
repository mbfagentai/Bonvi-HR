namespace Bonvi.Application.Abstractions;

/// <summary>
/// Bazadan kelgan nosozlikni <b>ma'noli</b> holatga o'giradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida port.</b> Ba'zi qoidalar faqat bazada ushlanadi — masalan
/// nomning unikalligi. Odatda uni use-case qatlami oldindan tekshiradi, lekin
/// tekshirish bilan yozish orasida qisqa oyna qoladi: ikki foydalanuvchi bir
/// vaqtda bir xil nom yuborsa, ikkalasi ham «bo'sh» degan javob oladi va
/// ikkinchisi bazadagi unikal indeksga uriladi.
/// </para>
/// <para>
/// Bunday istisnoni tanish uchun drayverning xato kodini bilish kerak
/// (PostgreSQL'da <c>23505</c>). Bu bilim Infrastructure qatlamiga tegishli:
/// Api uni bilsa, ilova ma'lumotlar bazasi drayveriga bog'lanib qolardi va
/// arxitektura testi buni ataylab taqiqlaydi.
/// </para>
/// <para>
/// Shu sababli tanib olish shu port ortida qoladi, natija esa oddiy
/// mantiqiy qiymat — uni istalgan qatlam tushunadi.
/// </para>
/// </remarks>
public interface IDatabaseFault
{
    /// <summary>
    /// Istisno <b>nom takrorlangani</b> sababli yuzaga kelganmi.
    /// </summary>
    /// <remarks>
    /// Har qanday unikallik buzilishi emas: JSHSHIR yoki telefon
    /// takrorlangani butunlay boshqa xabar talab qiladi va uni «bunday nom
    /// bor» deb ko'rsatish foydalanuvchini adashtirardi.
    /// </remarks>
    bool IsDuplicateName(Exception exception);

    /// <summary>
    /// Istisno qurilma o'tishining <b>qayta yuborilgani</b> sababli yuzaga kelganmi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Faqat <c>ux_attendance_passes_device_dedupe</c> buzilishi. Qabul yo'li
    /// o'tishni oldindan <c>SELECT</c> qilmasdan yozadi va aynan shu indeks
    /// qaytargan xatoni «takror» deb o'qiydi: oldindan so'rash poygani (race)
    /// yo'qotmaydi, faqat odatda yashiradi, va eng issiq yo'lga ortiqcha
    /// murojaat qo'shadi (SPEC §3.6).
    /// </para>
    /// <para>
    /// Boshqa har qanday unikallik buzilishi bu yerda <c>false</c> — u takror
    /// emas, nosozlik, va 200 bilan yashirilishi kerak emas.
    /// </para>
    /// </remarks>
    bool IsDuplicatePass(Exception exception);

    /// <summary>
    /// Istisno xodimga IKKINCHI Bonvi raqami ajratilmoqchi bo'lgani sababli yuzaga kelganmi.
    /// </summary>
    /// <remarks>
    /// Faqat <c>ux_attendance_identities_managed_employee</c> buzilishi: ikki
    /// agent so'rovi bir vaqtda bitta xodimga raqam ajratmoqchi bo'ldi.
    /// Bu nosozlik emas, poyga — ikkinchi so'rov qayta urinadi.
    /// </remarks>
    bool IsDuplicateDevicePersonNo(Exception exception);

    /// <summary>
    /// Istisno ikki qayta hisoblash BIR kun yozuviga bir vaqtda yozgani sababli yuzaga kelganmi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ikki holat, ikkalasi ham faqat <c>attendance_days</c> da:
    /// (1) ikkala yozuvchi (xodim, sana) uchun qator yo'qligini ko'rib, ikkalasi
    /// ham qo'shdi — ikkinchisi <c>ux_attendance_days_employee_date</c> ga uriladi;
    /// (2) ikkalasi bitta eskirgan qatorni o'chirmoqchi yoki biri yangilab, biri
    /// o'chirib bo'lgan — ikkinchisining buyrug'i nol qatorga tegadi.
    /// </para>
    /// <para>
    /// Ikkalasi ham nosozlik emas: hisoblash ma'lumotdan aniq kelib chiqadi,
    /// ya'ni bazani qayta o'qib, xuddi shu natijani yozish kifoya
    /// (<c>AttendanceDayRecomputer</c>). Boshqa har qanday xato — <c>false</c>.
    /// </para>
    /// </remarks>
    bool IsConcurrentDayWrite(Exception exception);

    /// <summary>
    /// Istisno xodimga IKKINCHI ilova hisobi ochilmoqchi bo'lgani sababli yuzaga kelganmi.
    /// </summary>
    /// <remarks>
    /// Faqat <c>ux_employee_accounts_employee</c> buzilishi: hisob avtomatik
    /// ochiladi — fon vazifasi va HR «Ilova» bo'limini ochgan lahza bir
    /// vaqtga tushishi mumkin. Bu nosozlik emas, poyga: ikkinchisi birinchi
    /// ochgan hisobni qayta o'qiydi.
    /// </remarks>
    bool IsDuplicateEmployeeAccount(Exception exception);

    /// <summary>
    /// Istisno so'rov qatoriga PARALLEL yozuv yutqazgani sababli yuzaga kelganmi
    /// (SPEC-verifikatsiya-sorov-tahriri §3.3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Faqat parallellik belgisi (<c>version</c>) bor yo'qlik va jadval so'rovlarida: xodim
    /// tahriri va HR qarori (yoki ikki qaror) bir vaqtda yozsa, ikkinchisining <c>UPDATE</c> i
    /// nol qatorga tegadi. Jadval so'rovi tahririda kun qatorlari ham almashadi — ularning
    /// <c>DELETE</c> i ham shunday yutqazishi mumkin, ikki tahrir bir sanani birga qayta
    /// qo'shsa esa ikkinchisi (so'rov, sana) unikalligiga uriladi; ikkalasi ham shu yerda.
    /// </para>
    /// <para>
    /// Bu nosozlik emas, javob: yutqazgan tomon 409 <c>davomat.sorov_ozgargan</c> oladi va
    /// yangilangan holatni ko'rib qayta uradi. Qayta urinish YO'Q — avtomatik takror boshqa
    /// odamning qarorini ko'rmasdan ustidan yozardi. Boshqa har qanday xato — <c>false</c>.
    /// </para>
    /// </remarks>
    bool IsStaleRequestWrite(Exception exception);
}
