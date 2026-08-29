using Bonvi.Domain.Identity;

namespace Bonvi.Domain.Notifications;

/// <summary>
/// Bildirishnoma turi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega tur (sanoq), tayyor matn emas.</b> Tizim to'rt tilli. Server
/// «Karimov Azizning sinov muddati 5 kundan keyin tugaydi» degan satrni
/// yasasa, u HAR DOIM o'zbekcha bo'lib qolardi — foydalanuvchi qaysi
/// tilni tanlaganidan qat'i nazar. Shu sababli serverdan TUR va
/// PARAMETRLAR qaytadi, matnni esa frontend yig'adi. Bu loyihaning
/// mavjud qoidasi: xato kodlari ham aynan shunday ishlaydi
/// (<c>Error.Code</c> izohiga qarang).
/// </para>
/// <para>
/// <b>Qiymatlar aniq raqamlar bilan qadalgan va yangisi OXIRIGA
/// qo'shiladi.</b> Sanoq JSON'da nom bo'lib yuradi
/// (<c>JsonStringEnumConverter</c>), lekin bazadagi «o'qildi» yozuvlari
/// KALIT orqali bog'lanadi va kalitda tur PREFIKSI turadi — raqamlar
/// siljisa, kalit prefikslari o'zgarmasa ham, kod bilan ma'lumot
/// orasidagi moslik jimgina buzilardi.
/// </para>
/// </remarks>
public enum NotificationType
{
    /// <summary>Sinov muddati tugashiga 7 kundan kam qoldi (bugun tugaydiganlar bundan tashqari).</summary>
    ProbationEndingSoon = 1,

    /// <summary>Sinov muddati BUGUN tugaydi.</summary>
    /// <remarks>
    /// <see cref="ProbationEndingSoon"/> dan ATAYLAB ajratilgan: «5 kun
    /// qoldi» — rejalashtirish, «bugun tugaydi» — bugun bajarilishi kerak
    /// bo'lgan ish. Bitta turga yig'ilsa, ekranda ikkalasi bir xil rangda
    /// turardi va oxirgi kun boshqalar orasida ko'rinmay ketardi.
    /// </remarks>
    ProbationEndsToday = 2,

    /// <summary>Sinov muddati O'TIB KETGAN, lekin yakunlanmagan.</summary>
    /// <remarks>
    /// Foydalanuvchi «shunga o'xshash kerakli narsalarni ham» degan edi —
    /// bu ularning eng muhimi. Yakunlanmagan sinov hech qanday ro'yxatda
    /// qizarib turmaydi: u <c>/sinov</c> sahifasida boshqalar bilan birga
    /// yotadi va odam jimgina «sinovda» holatida qolib ketadi.
    /// </remarks>
    ProbationOverdue = 3,

    /// <summary>Holati «sinovda», lekin sinov muddati yozuvi umuman ochilmagan.</summary>
    /// <remarks>
    /// Manba — Excel'dan ko'chirish (ETL). Batafsil sabab
    /// <c>IEmployeeReadModel.ListMissingProbationsAsync</c> izohida.
    /// </remarks>
    ProbationMissing = 4,

    /// <summary>Bugun tug'ilgan kun.</summary>
    /// <remarks>
    /// HAR BIR XODIM uchun ALOHIDA bildirishnoma — foydalanuvchi aynan
    /// shunday so'radi. Yig'ma «bugun 3 ta tug'ilgan kun» yozuvi bitta
    /// odamni tabriklab, ikkinchisini unutish holatini yashirardi: bitta
    /// yozuvni «o'qildi» qilish uchtasini ham o'chirardi.
    /// </remarks>
    BirthdayToday = 5,

    /// <summary>Ertaga tug'ilgan kun.</summary>
    /// <remarks>
    /// Xuddi <see cref="BirthdayToday"/> kabi har bir xodimga alohida.
    /// Bir kun oldin ogohlantirish HR ga tayyorgarlik ko'rish (sovg'a,
    /// tabriknoma) uchun vaqt beradi.
    /// </remarks>
    BirthdayTomorrow = 6,

    /// <summary>Yangi nomzod qo'shildi.</summary>
    /// <remarks>
    /// Hozircha nomzodni HR qo'lda kiritadi, kelajakda esa Telegram anketa
    /// boti avtomatik qo'shadi (<c>CandidateSource.TelegramBot</c>). Bot
    /// hali qurilmagan, lekin bildirishnoma allaqachon ishlaydi: manba
    /// parametr bo'lib qaytadi va bot ulanganda hech narsa o'zgarmaydi.
    /// </remarks>
    CandidateNew = 7,

    /// <summary>Ish taklifiga javob muddati tugayapti.</summary>
    JobOfferExpiring = 8,

    /// <summary>Ish taklifiga javob muddati TUGAGAN, lekin taklif hamon ochiq.</summary>
    /// <remarks>
    /// Tizim taklifni O'ZI «muddati o'tdi» deb belgilamaydi — buni HR
    /// qiladi (<c>JobOffer.MarkExpired</c> izohi). Ya'ni muddati o'tgan
    /// taklif hech qayerda o'z-o'zidan qizarmaydi va aynan shu sababli
    /// bildirishnoma kerak.
    /// </remarks>
    JobOfferExpired = 9,
}

/// <summary>Bildirishnomaning muhimlik darajasi.</summary>
/// <remarks>
/// <para>
/// Uchta daraja ataylab: ro'yxat shu bo'yicha tartiblanadi va ekranda
/// rang tanlanadi. To'rtinchi daraja qo'shilsa, foydalanuvchi ularni
/// bir-biridan ajrata olmay qolardi.
/// </para>
/// <para>
/// Qiymatlar o'sish tartibida — ro'yxat <c>OrderByDescending</c> bilan
/// tartiblanganda eng muhimi tepaga chiqadi.
/// </para>
/// </remarks>
public enum NotificationSeverity
{
    /// <summary>Ma'lumot uchun — hech narsa buzilmaydi (tug'ilgan kun, yangi nomzod).</summary>
    Info = 1,

    /// <summary>Diqqat — muddat yaqinlashmoqda, ulgurish kerak.</summary>
    Warning = 2,

    /// <summary>Jiddiy — muddat ALLAQACHON o'tib ketgan.</summary>
    Critical = 3,
}

/// <summary>
/// Bildirishnoma MAZMUNI qaysi ruxsat ostida ko'rinadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bildirishnomalar uchun YANGI ruxsat qo'shilmadi.</b>
/// Bildirishnoma — mustaqil ma'lumot emas, mavjud yozuvning boshqa
/// ko'rinishi: «yangi nomzod» yozuvidagi ism nomzodlar ro'yxatidagi ism
/// bilan bir xil. Alohida <c>notifications.view</c> ruxsati bo'lganda
/// tizimda ikkita chegara paydo bo'lardi va ular ertami-kechmi
/// ajralib ketardi — rekrutingga ruxsati YO'Q, lekin bildirishnomaga
/// ruxsati BOR odam nomzodlarning ismini qo'ng'iroq ostidan o'qib
/// turardi. Shu sababli har bir tur MANBASINING ruxsatini oladi.
/// </para>
/// <para>
/// <b>Nega ro'yxatning o'zi ruxsatsiz.</b> Kirgan har bir foydalanuvchi
/// <c>GET /api/notifications</c> ni chaqira oladi — u faqat O'ZI ko'ra
/// oladigan turlarni oladi. Endpoint'ni bitta ruxsat bilan yopish
/// noto'g'ri bo'lardi: qaysi birini tanlasak ham, boshqa ruxsatlarga ega
/// foydalanuvchi qo'ng'iroqni umuman ocha olmasdi.
/// </para>
/// </remarks>
public static class NotificationAccess
{
    /// <summary>Shu turdagi bildirishnomani ko'rish uchun kerak bo'lgan ruxsat.</summary>
    /// <remarks>
    /// Ro'yxatda <c>null</c> yo'q — har bir tur biror ma'lumotdan hosil
    /// bo'ladi va o'sha ma'lumotning chegarasini meros oladi. Yangi tur
    /// qo'shilib, bu yerga yozilmasa, <c>switch</c> ifodasi
    /// <see cref="ArgumentOutOfRangeException"/> tashlaydi — ya'ni
    /// «ruxsatsiz ko'rinib qolish» emas, ochiq nosozlik bo'ladi.
    /// </remarks>
    public static string RequiredPermission(NotificationType type) => type switch
    {
        // Sinov muddati — mehnat munosabatining bir qismi. `employees.view`
        // emas: sinovni YAKUNLAYDIGAN ekran (`/sinov`) ham aynan shu
        // ruxsat ostida (`EmploymentEndpoints`), ya'ni bildirishnoma
        // ochib bo'lmaydigan sahifaga yetaklab bormaydi.
        NotificationType.ProbationEndingSoon
            or NotificationType.ProbationEndsToday
            or NotificationType.ProbationOverdue
            or NotificationType.ProbationMissing => Permissions.EmploymentView,

        // Tug'ilgan kun — xodim kartasidagi sana, boshqa hech narsa emas.
        NotificationType.BirthdayToday
            or NotificationType.BirthdayTomorrow => Permissions.EmployeesView,

        // Nomzod va taklif — rekruting yo'lagi. Foydalanuvchi aniq talab
        // qildi: `recruiting.view` yo'q odamga nomzod bildirishnomasi
        // KO'RINMASIN.
        NotificationType.CandidateNew
            or NotificationType.JobOfferExpiring
            or NotificationType.JobOfferExpired => Permissions.RecruitingView,

        _ => throw new ArgumentOutOfRangeException(
            nameof(type),
            type,
            "Bildirishnoma turi uchun ruxsat belgilanmagan."),
    };
}
