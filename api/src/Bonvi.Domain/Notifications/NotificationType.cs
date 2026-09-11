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

    /// <summary>Yangi nomzod QO'LDA kiritildi.</summary>
    /// <remarks>
    /// Bu yozuvni HR ning o'zi kiritgan — ya'ni u allaqachon xabardor.
    /// Bildirishnoma bu yerda eslatma, chaqiruv emas, shu sababli
    /// <see cref="NotificationSeverity.Info"/>.
    /// </remarks>
    CandidateNew = 7,

    /*
      8 va 9 — BO'SH. Ular ish taklifining muddati haqidagi ikkita tur
      edi va funksiya bilan birga olib tashlandi
      (`docs/SPEC-nomzod-yakuni.md`, 3-bo'lim). Qiymatlar QAYTA
      ISHLATILMAYDI va qolganlari SURILMAYDI — yuqoridagi qoida
      («yangisi oxiriga») aynan shuni anglatadi: bugun bo'shagan
      raqamni ertaga boshqa turga berish, eski mijoz uchun bir turni
      ikkinchisiga aylantirib qo'yardi.
    */

    /// <summary>Telegram boti orqali ARIZA tushdi.</summary>
    /// <remarks>
    /// <para>
    /// <b>Nega <see cref="CandidateNew"/> dan alohida tur.</b> Ikkalasi
    /// ham «yangi nomzod», lekin HR uchun ular BUTUNLAY boshqa hodisa.
    /// Qo'lda kiritilgan nomzodni HR ning o'zi yozgan — u xabardor.
    /// Botdan kelgan ariza esa HECH KIM QARAMAYOTGAN paytda tushadi:
    /// kechasi, dam olish kuni, tushlikda. Aynan shu — sezilmay qolishi
    /// mumkin bo'lgan yagona nomzod.
    /// </para>
    /// <para>
    /// <b>Nega <see cref="NotificationSeverity.Warning"/>.</b> Daraja
    /// ro'yxatdagi tartibni ham, rangni ham belgilaydi: sariq yozuv ko'k
    /// yozuvlar orasidan darhol ajralib turadi va ro'yxatning tepasiga
    /// chiqadi. Bu «xato» degani emas — «javob kutilyapti» degani: odam
    /// ariza topshirdi va uni kutmoqda.
    /// </para>
    /// <para>
    /// Rangdan tashqari SHAKL ham boshqa: ikona alohida
    /// (<c>notification-presentation.ts</c> dagi <c>TYPE_ICON</c>) —
    /// rang shoshilinchlikni, ikona esa nima bo'lganini aytadi.
    /// </para>
    /// </remarks>
    CandidateFromBot = 10,

    /// <summary>Nomzod bilan BUGUN bog'lanish kerak (keyingi aloqa sanasi — bugun).</summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nima uchun paydo bo'ldi</b> (mijoz talabi, 2026-09-10).
    /// Nomzodlar ro'yxatida «Keyingi aloqa» ustuni bor edi va u
    /// OLIB TASHLANDI — jadval juda kengayib ketgani uchun
    /// (<c>7e678dab</c>). Sana esa yozilishda davom etyapti
    /// (<c>Candidate.NextContactDate</c>), ya'ni ma'lumot bor, uni
    /// KO'RSATADIGAN joy yo'q edi. Endi uni qo'ng'iroq ko'rsatadi:
    /// ertalab bitta ro'yxat — «bugun kimga qo'ng'iroq qilishim kerak».
    /// </para>
    /// <para>
    /// <b>Nega <see cref="CandidateContactOverdue"/> dan alohida tur.</b>
    /// Sabab <see cref="ProbationEndsToday"/> va
    /// <see cref="ProbationOverdue"/> dagi bilan AYNAN bir xil: «bugun
    /// qilinadigan ish» va «kechikkan ish» ekranda bir xil rangda tursa,
    /// kechikkanlar bugungilar orasida ko'rinmay ketardi. Daraja ham shu
    /// juftlikdan olingan — bu yerda «diqqat», u yerda «jiddiy».
    /// </para>
    /// <para>
    /// <b>YOPILGAN nomzodda CHIQMAYDI.</b> Rad etilgan, voz kechgan yoki
    /// ishga olingan nomzodda sana domenning o'zida tozalanadi
    /// (<c>Candidate.Close</c>), lekin bildirishnoma bunga TAYANMAYDI:
    /// so'rov holatni ham tekshiradi. Ikkinchi to'siq ataylab — Excel'dan
    /// ko'chirilgan yozuvlar bu qoidadan o'tmagan.
    /// </para>
    /// <para>
    /// ⚠️ <b>REZERV esa KIRADI</b> (foydalanuvchi qarori, 2026-09-11) —
    /// garchi loyihaning lug'atida rezerv voronkadan TASHQARIDA bo'lsa
    /// ham (<c>Candidate.IsActive</c>). Sabab
    /// <c>NotificationReadModel.ListCandidateContactsAsync</c> izohida.
    /// <see cref="CandidateInterviewSoon"/> esa faqat
    /// <c>InProgress</c> — asimmetriya ataylab.
    /// </para>
    /// </remarks>
    CandidateContactDue = 11,

    /// <summary>Nomzod bilan bog'lanish sanasi O'TIB KETGAN.</summary>
    /// <remarks>
    /// <para>
    /// <see cref="CandidateContactDue"/> bilan BIR VAQTDA hech qachon
    /// chiqmaydi: sana yo bugun, yo o'tmishda — uchinchi holat yo'q.
    /// Tur sanalar nisbatidan hisoblanadi (<c>NotificationCollector</c>),
    /// bazada esa «bu qanday ogohlantirish» degan ustun yo'q va bo'lishi
    /// ham kerak emas — u har kuni o'zgaradi.
    /// </para>
    /// <para>
    /// <b>Nega <see cref="NotificationSeverity.Critical"/>.</b> Bu — HR
    /// ning O'ZI belgilagan va'da: «shu kuni qo'ng'iroq qilaman». Uning
    /// o'tib ketishi tizimdagi eng oson yo'qoladigan ish: nomzod
    /// ro'yxatda boshqalar orasida turadi va hech qayerda qizarmaydi
    /// (ustun olib tashlangandan keyin — umuman ko'rinmaydi).
    /// </para>
    /// </remarks>
    CandidateContactOverdue = 12,

    /// <summary>Suhbatgacha sozlamada ko'rsatilgan soatdan kam qoldi.</summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nima uchun kerak.</b> Suhbat vaqti nomzodga yuboriladi
    /// (<c>Candidate.InviteToInterview</c> → bot), lekin HR ning o'zi
    /// uchun hech qanday eslatma yo'q edi: vaqt faqat nomzod kartasida
    /// turardi. «Bu odam hozir keladi» degan xabar aynan shu bo'shliqni
    /// yopadi.
    /// </para>
    /// <para>
    /// <b>Oyna ikki tomonlama: HOZIRDAN to N soatgacha.</b> O'tib ketgan
    /// suhbat «yaqin» emas — ikki soat oldin boshlangan suhbat haqidagi
    /// ogohlantirish faqat halaqit berardi. N — sozlama
    /// (<c>SystemSettingKeys.InterviewWarningHours</c>, sukut 1 soat),
    /// ya'ni «bir soat oldin» qoidasi kodda qadalmagan.
    /// </para>
    /// <para>
    /// <b>Yagona SOATLI tur.</b> Qolganlarining hammasi kalendar fakti
    /// («bugun tugaydi», «ertaga tug'ilgan kun»), bu esa lahza: shu
    /// sababli parametrlarida VAQT ham bor
    /// (<c>NotificationParams.InterviewTime</c>, Toshkent vaqtida) —
    /// sanaviy turlarda bunday parametr yo'q va kerak ham emas.
    /// </para>
    /// <para>
    /// Daraja <see cref="ProbationEndsToday"/> dan olingan: bu bugun
    /// bajariladigan ish, «muddat o'tib ketdi» emas. Suhbat o'tib
    /// ketgach bildirishnoma shunchaki YO'QOLADI — «kelmadi» degan
    /// faktni tizim bilmaydi va uni o'ylab topmaydi.
    /// </para>
    /// </remarks>
    CandidateInterviewSoon = 13,
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

        // Nomzod — rekruting yo'lagi. Foydalanuvchi aniq talab qildi:
        // `recruiting.view` yo'q odamga nomzod bildirishnomasi
        // KO'RINMASIN. Aloqa va suhbat eslatmalari ham shu yerda:
        // ularning MAZMUNI o'sha nomzod kartasi (ism, sana, vaqt) va
        // havolasi ham o'sha kartaga boradi — boshqa ruxsat ostiga
        // qo'yilsa, ochib bo'lmaydigan sahifaga yetaklagan bo'lardi.
        NotificationType.CandidateNew
            or NotificationType.CandidateFromBot
            or NotificationType.CandidateContactDue
            or NotificationType.CandidateContactOverdue
            or NotificationType.CandidateInterviewSoon => Permissions.RecruitingView,

        _ => throw new ArgumentOutOfRangeException(
            nameof(type),
            type,
            "Bildirishnoma turi uchun ruxsat belgilanmagan."),
    };
}
