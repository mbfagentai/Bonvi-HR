using Bonvi.Application.Common.Photos;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Recruiting.Common;

/// <summary>
/// Rekruting (2-bosqich) bo'yicha use-case xatolari.
/// </summary>
/// <remarks>
/// Domen xatolari (masalan «nomzod.jarayonda_emas») bu yerda
/// TAKRORLANMAYDI: ular agregatning o'zida yashaydi va shu yerda qayta
/// e'lon qilinsa, ikkita haqiqat paydo bo'lardi. Bu yerda faqat
/// use-case ko'radigan holatlar — «topilmadi» va boshqa agregatlar bilan
/// bog'liq ziddiyatlar.
/// </remarks>
public static class RecruitingErrors
{
    // ---------- Vakansiya ----------

    public static Error VacancyNotFound { get; } =
        Error.NotFound("vakansiya.topilmadi", "Vakansiya topilmadi.");

    /// <summary>E'lon nomi ma'lumotnomada yo'q.</summary>
    /// <remarks>
    /// Ilgari bu yerda <c>vakansiya.lavozim_topilmadi</c>,
    /// <c>vakansiya.yuridik_shaxs_topilmadi</c> va
    /// <c>vakansiya.bolim_topilmadi</c> ham turardi. Vakansiyada kompaniya
    /// tuzilmasidan hech narsa qolmagach (sababi <c>Vacancy</c> izohida),
    /// ular vakansiyaning emas, TAKLIFNI QABUL QILISHNING xatolariga
    /// aylandi va shu sababli <c>taklif.*</c> prefiksiga ko'chdi.
    /// Vakansiyaning yagona ma'lumotnoma havolasi endi shu.
    /// </remarks>
    public static Error VacancyTitleNotFound { get; } =
        Error.NotFound("vakansiya.nom_topilmadi", "Vakansiya nomi topilmadi.");

    public static Error ResponsibleNotFound { get; } =
        Error.NotFound("vakansiya.masul_topilmadi", "Mas'ul xodim topilmadi.");

    /// <summary>Yopilgan vakansiyaga nomzod kiritilmaydi.</summary>
    public static Error VacancyNotAccepting { get; } = Error.Conflict(
        "vakansiya.qabul_qilmaydi",
        "Vakansiya ochiq emas: unga yangi nomzod kiritib bo'lmaydi.");

    /// <summary>Nomzodi bor vakansiya o'chirilmaydi.</summary>
    /// <remarks>
    /// Batafsil sabab <c>DeleteVacancyCommandHandler</c> izohida: nomzodi
    /// bor vakansiya — tarix, uni o'chirish nomzodlarning yozuvini yetim
    /// qoldirardi. Xabar YO'LNI ham aytadi («yoping»), chunki HR bu
    /// nuqtada odatda «bu yozuv ro'yxatda turmasin» degan maqsadda
    /// bo'ladi va uni yopish aynan shuni beradi.
    /// </remarks>
    public static Error VacancyHasCandidates { get; } = Error.Conflict(
        "vakansiya.nomzodlari_bor",
        "Nomzodi bor vakansiya o'chirilmaydi — uni yopish mumkin.");

    // ---------- Bosqich ----------

    public static Error StageNotFound { get; } =
        Error.NotFound("bosqich.topilmadi", "Voronka bosqichi topilmadi.");

    /// <summary>Faolsizlantirilgan bosqichga nomzod o'tkazilmaydi.</summary>
    /// <remarks>
    /// Bosqich ma'lumotnoma yozuvi va u hech qachon o'chirilmaydi, faqat
    /// faolsizlantiriladi. Unda TURGAN nomzodlar joyida qoladi va bosqich
    /// nomi ularning kartasida ko'rinib turadi — aks holda tarix
    /// «noma'lum bosqich» bo'lib qolardi. Lekin YANGI nomzodni u yerga
    /// o'tkazib bo'lmaydi: faolsizlantirishning butun ma'nosi shu.
    /// </remarks>
    public static Error StageInactive { get; } = Error.Conflict(
        "bosqich.faol_emas",
        "Bosqich faol emas: nomzodni unga o'tkazib bo'lmaydi.");

    /// <summary>Voronkada bironta ham faol bosqich yo'q.</summary>
    /// <remarks>
    /// Nomzodni voronkasiz kiritish mumkin emas: u qayerda turgani
    /// noma'lum bo'lardi va hech qaysi ro'yxatga tushmasdi. Urug' fayli
    /// oltita bosqichni beradi, ya'ni bu holat faqat HR hammasini
    /// faolsizlantirsa yuzaga keladi.
    /// </remarks>
    public static Error NoActiveStages { get; } = Error.Conflict(
        "bosqich.faol_yoq",
        "Voronkada bironta ham faol bosqich yo'q — avval bosqich qo'shing.");

    public static Error StageNameTaken { get; } =
        Error.Conflict("bosqich.nom_band", "Bunday nomli bosqich allaqachon bor.");

    /// <summary>Qayta tartiblashda ro'yxat to'liq emas.</summary>
    public static Error ReorderIncomplete { get; } = Error.Validation(
        "bosqich.tartib_royxati",
        "Qayta tartiblashda BARCHA bosqichlar ro'yxatda bo'lishi kerak.");

    // ---------- Nomzod ----------

    public static Error CandidateNotFound { get; } =
        Error.NotFound("nomzod.topilmadi", "Nomzod topilmadi.");

    /// <summary>
    /// «Umumiy anketa» vakansiyasi ishlatib bo'lmaydigan holatda.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nomzod vakansiyasiz kelganda u shu XIZMAT vakansiyasiga tushadi
    /// (izohi <c>SystemSettingKeys.GeneralPoolVacancyId</c> da). Xato
    /// uchta holatni birlashtiradi: sozlama yozilmagan, ko'rsatilgan
    /// vakansiya topilmadi va u YOPILGAN. Uchalasining ham sababi bitta
    /// — xizmat vakansiyasi buzilgan, yechimi ham bitta: uni tiklash
    /// yoki qayta ochish. Ularni ajratish bot uchun hech narsa
    /// bermasdi, chunki bot baribir hech qanday qaror qabul qila
    /// olmaydi.
    /// </para>
    /// <para>
    /// ⚠️ <b>Jimgina o'tkazib yuborilmaydi.</b> Bu holat faqat
    /// urug'lantirish ishlamaganda yoki HR xizmat vakansiyasini qo'lda
    /// yopganda yuzaga keladi — va o'shanda BARCHA vakansiyasiz
    /// anketalar to'xtaydi. Aniq xato bo'lmasa, buni faqat «botdan
    /// nomzod kelmayapti» degan shikoyatdan bilib olardik.
    /// </para>
    /// </remarks>
    public static Error GeneralPoolUnavailable { get; } = Error.Conflict(
        "nomzod.umumiy_anketa_yoq",
        "«Umumiy anketa» vakansiyasi sozlanmagan yoki ochiq emas — vakansiyasiz anketa qabul qilinmaydi.");

    public static Error ResumeNotFound { get; } =
        Error.NotFound("nomzod.rezyume_topilmadi", "Nomzodning rezyumesi yo'q.");

    /// <summary>Fayl chegaradan katta.</summary>
    public static Error ResumeTooLarge { get; } = Error.Validation(
        "nomzod.rezyume_katta",
        "Rezyume fayli juda katta (eng ko'pi 20 MB).");

    /// <summary>Fayl mazmuni tanilmadi.</summary>
    public static Error ResumeInvalid { get; } = Error.Validation(
        "nomzod.rezyume_yaroqsiz",
        "Rezyume PDF, JPEG, PNG yoki WebP emas (yoki buzilgan).");

    // ---------- Nomzod surati ----------
    //
    // Kodlar `xodim.rasm_*` dan ATAYLAB alohida, holbuki quvur bitta
    // (`SquarePhotoStore`). Sabab `EmployeeErrors.PhotoRejected` izohida:
    // quvur SABABNI biladi, kod esa bo'limning shartnomasi va interfeys
    // aynan unga qarab qaysi ekranda nima ko'rsatishini hal qiladi.

    /// <summary>Nomzod bor, lekin surati yo'q (yoki fayli yo'qolgan).</summary>
    /// <remarks>
    /// «Nomzod topilmadi» dan ajratilgan: interfeys birinchi holatda xato
    /// ko'rsatadi, ikkinchisida esa jimgina bosh harflardan iborat
    /// o'rinbosar chizadi (xodim rasmidagi bilan bir xil qoida).
    /// </remarks>
    public static Error PhotoNotFound { get; } =
        Error.NotFound("nomzod.rasm_yoq", "Nomzodning surati yo'q.");

    public static Error PhotoTooLarge { get; } =
        Error.Validation("nomzod.rasm_katta", "Rasm hajmi 10 MB dan oshmasligi kerak.");

    public static Error PhotoTypeNotSupported { get; } =
        Error.Validation("nomzod.rasm_turi", "Faqat JPEG, PNG va WebP rasmlar qabul qilinadi.");

    /// <summary>Fayl rasm sifatida ochilmadi.</summary>
    /// <remarks>
    /// <see cref="PhotoTypeNotSupported"/> dan farqi: u E'LON QILINGAN turga
    /// qaraydi, bu esa fayl MAZMUNIGA. Nomi «.jpg» bo'lgan skript aynan shu
    /// yerda to'xtaydi — va bu bot uchun muhim: unga rasm Telegram'dan
    /// keladi va uni tekshirishning boshqa yo'li yo'q.
    /// </remarks>
    public static Error PhotoInvalid { get; } =
        Error.Validation("nomzod.rasm_yaroqsiz", "Fayl rasm emas yoki buzilgan.");

    /// <summary>Umumiy rasm quvurining rad etish sababini NOMZOD kodiga o'giradi.</summary>
    /// <remarks>
    /// Xaritalash bitta joyda: yangi sabab qo'shilsa, kompilyator uni
    /// tarjimasiz qoldirmaydi.
    /// </remarks>
    public static Error PhotoRejected(PhotoRejection rejection) => rejection switch
    {
        PhotoRejection.TooLarge => PhotoTooLarge,
        PhotoRejection.UnsupportedType => PhotoTypeNotSupported,
        PhotoRejection.Invalid => PhotoInvalid,
        _ => PhotoInvalid,
    };

    // ---------- Taklif ----------

    public static Error OfferNotFound { get; } =
        Error.NotFound("taklif.topilmadi", "Ish taklifi topilmadi.");

    /// <summary>Nomzodda javob kutayotgan taklif allaqachon bor.</summary>
    public static Error OfferAlreadyPending { get; } = Error.Conflict(
        "taklif.faol_mavjud",
        "Nomzodga yuborilgan va javobi kelmagan taklif bor — avval unga javob yozing.");

    /// <summary>Nomzod allaqachon ishga olingan (takroriy qabul).</summary>
    /// <remarks>
    /// Ikki xil yo'l bilan yuzaga chiqadi: nomzodning holati allaqachon
    /// «ishga olingan» yoki uning nomiga ishga qabul hodisasi topilgan.
    /// Ikkinchisi — birinchi urinish yarim yo'lda uzilgan holat va
    /// takroriy urinish IKKINCHI xodim kartasini yaratmasligi kerak
    /// (batafsil <c>AcceptJobOfferCommandHandler</c> izohida).
    /// </remarks>
    public static Error AlreadyHired { get; } = Error.Conflict(
        "nomzod.allaqachon_ishga_olingan",
        "Nomzod allaqachon ishga olingan.");

    // ---------- Taklif qabul qilinganda: SHTAT birligi ----------
    //
    // Bu uchtasi ATAYLAB `taklif.*` prefiksida, `vakansiya.*` da emas.
    // Sabab mazmuniy: firma, bo'lim va lavozim endi e'lonning xossasi
    // emas, ishga qabul paytidagi QAROR (sababi `Vacancy` izohida). Xato
    // kodi esa foydalanuvchi uchun «qaysi ekranda nima noto'g'ri» degan
    // ma'noni tashiydi — va bu maydonlar endi faqat QABUL oynasida bor.

    /// <summary>Qabul qilishda lavozim ko'rsatilmagan.</summary>
    /// <remarks>
    /// <b>Nega MAJBURIY, holbuki <c>HireEmployeeCommand.PositionId</c> ning
    /// o'zi ixtiyoriy.</b> 1-bosqichda lavozimsiz qabul ataylab mumkin:
    /// HR formasi va Excel'dan ko'chirish (ETL) lavozimi noma'lum eski
    /// yozuvlarni ham kiritishi kerak. Rekrutingda esa bunday holat yo'q —
    /// foydalanuvchi talabi aniq: «ишга олингандан кейин компаниянинг
    /// бўлимлари билан ҳам танланиб АНИҚ ЛАВОЗИМ танланади». Vakansiyada
    /// lavozim umuman yo'q, ya'ni bu yerda bo'sh qoldirilsa, xodim
    /// lavozimsiz yaratilardi va uni keyin qo'lda tuzatish kerak bo'lardi.
    /// Majburiylik shu sababli AYNAN SHU qatlamda, domenda emas.
    /// </remarks>
    public static Error HirePositionMissing { get; } = Error.Validation(
        "taklif.lavozim_yoq",
        "Lavozim ko'rsatilishi shart: nomzod aynan qaysi shtat birligiga olinayotgani yozilishi kerak.");

    /// <summary>Qabul qilishda yuridik shaxs ko'rsatilmagan.</summary>
    /// <remarks>
    /// Bu majburiylik texnik ham: <c>HireEmployeeCommand.LegalEntityId</c> —
    /// pozitsion va nullable EMAS, ya'ni ishga qabul usiz umuman
    /// bajarilmaydi. Ilgari qiymat vakansiyadan kelardi; endi vakansiyada
    /// firma yo'q, demak u qabul oynasidan kelishi SHART.
    /// </remarks>
    public static Error HireLegalEntityMissing { get; } = Error.Validation(
        "taklif.yuridik_shaxs_yoq",
        "Yuridik shaxs ko'rsatilishi shart: xodim qaysi firmaga rasmiylashtiriladi.");

    /// <summary>Qabul qilishda ko'rsatilgan bo'lim ma'lumotnomada yo'q.</summary>
    /// <remarks>
    /// Bo'lim IXTIYORIY (<c>HireEmployeeCommand</c> dagi bilan bir xil),
    /// lekin ko'rsatilgan bo'lsa — mavjud bo'lishi shart. Ishga qabul
    /// oqimining o'zi bo'limni tekshirmaydi (u yerda ham ixtiyoriy), shu
    /// sababli tekshiruv aynan shu yerda: aks holda mavjud bo'lmagan
    /// identifikator bazaga yetib borib, tushunarsiz tashqi kalit xatosini
    /// keltirib chiqarardi.
    /// </remarks>
    public static Error DepartmentNotFound { get; } =
        Error.NotFound("taklif.bolim_topilmadi", "Bo'lim topilmadi.");

    // ---------- To'g'ridan-to'g'ri ishga olish: SHTAT birligi ----------
    //
    // ⭐ Yuqoridagi uchtasining AYNAN nusxasi, faqat prefiksi boshqa —
    // va bu ataylab. Xato kodi foydalanuvchi uchun «qaysi EKRANDA nima
    // noto'g'ri» degan ma'noni tashiydi; taklifni qabul qilish va nomzod
    // kartasidagi «Ishga olish» — ikki xil ekran va ikki xil forma.
    //
    // Ikkinchi, hal qiluvchi sabab: ish taklifi funksiyasi butunlay
    // o'chiriladi (`docs/SPEC-nomzod-yakuni.md`, 3-bo'lim) va u bilan
    // birga `taklif.*` kodlari ham ketadi. Yagona SAQLANIB QOLADIGAN
    // ishga olish yo'li o'chirilayotgan funksiyaning nomiga tayanib
    // qolmasligi kerak edi.

    /// <summary>To'g'ridan-to'g'ri ishga olishda lavozim ko'rsatilmagan.</summary>
    /// <remarks>
    /// Majburiylik sababi <see cref="HirePositionMissing"/> dagi bilan bir
    /// xil: <c>HireEmployeeCommand.PositionId</c> ning O'ZI ixtiyoriy (HR
    /// formasi va Excel'dan ko'chirish lavozimsiz eski yozuvlarni ham
    /// kiritadi), rekrutingda esa bunday holat yo'q — nomzod aynan qaysi
    /// shtat birligiga olinayotgani yozilishi shart.
    /// </remarks>
    public static Error CandidateHirePositionMissing { get; } = Error.Validation(
        "nomzod.lavozim_yoq",
        "Lavozim ko'rsatilishi shart: nomzod aynan qaysi shtat birligiga olinayotgani yozilishi kerak.");

    /// <summary>To'g'ridan-to'g'ri ishga olishda yuridik shaxs ko'rsatilmagan.</summary>
    /// <remarks>
    /// Bu majburiylik texnik ham: <c>HireEmployeeCommand.LegalEntityId</c> —
    /// pozitsion va nullable EMAS, ya'ni ishga qabul usiz umuman
    /// bajarilmaydi.
    /// </remarks>
    public static Error CandidateHireLegalEntityMissing { get; } = Error.Validation(
        "nomzod.yuridik_shaxs_yoq",
        "Yuridik shaxs ko'rsatilishi shart: xodim qaysi firmaga rasmiylashtiriladi.");

    /// <summary>To'g'ridan-to'g'ri ishga olishda ko'rsatilgan bo'lim ma'lumotnomada yo'q.</summary>
    /// <remarks>
    /// Bo'lim IXTIYORIY, lekin ko'rsatilgan bo'lsa — mavjud bo'lishi shart.
    /// Ishga qabul oqimining o'zi buni tekshirmaydi (u yerda ham ixtiyoriy),
    /// shu sababli tekshiruv shu qatlamda: aks holda mavjud bo'lmagan
    /// identifikator bazaga yetib borib, tushunarsiz tashqi kalit xatosini
    /// keltirib chiqarardi.
    /// </remarks>
    public static Error CandidateHireDepartmentNotFound { get; } =
        Error.NotFound("nomzod.bolim_topilmadi", "Bo'lim topilmadi.");
}
