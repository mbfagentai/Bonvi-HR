namespace Bonvi.Application.Recruiting.Forms.Texts;

/// <summary>PDF matni: kalit va hozirgi qog'ozdagi asl matn (uz-Latn).</summary>
/// <param name="Key">HRMentor katalogidagi kalit (<c>pdf.&lt;bo'lim&gt;.&lt;nom&gt;</c>).</param>
/// <param name="Default">Asl matn — HR o'zgartirmagan bo'lsa PDF da harfma-harf shu chiqadi.</param>
public sealed record PdfTextItem(string Key, string Default);

/// <summary>Qog'ozning bitta bo'limi — HRMentor da bitta guruh.</summary>
public sealed record PdfTextGroup(string Id, string Title, IReadOnlyList<PdfTextItem> Items);

/// <summary>
/// HR-F02 PDF ining HR tahrirlaydigan matnlari (SPEC-blankalar-hrmentor §4, §5.3) — har biri
/// kalit va hozirgi asl matni bilan, qog'oz bo'limlari tartibida.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asl matnlar — renderer dagi literallarning o'zi.</b> HR hech narsa o'zgartirmagan bo'lsa
/// PDF bayt-baytigacha avvalgidek chiqishi shart; shu sababli bo'shliqlar ham harfma-harf
/// saqlanadi. Matn atrofidagi bo'shliq (masalan «Sahifa » dagi) bu yerda EMAS — u kodda:
/// HR uni o'chirib yuborsa qog'ozda so'zlar yopishib qolardi.
/// </para>
/// <para>
/// <b>Qiymatli matnlar</b> <c>{{nom}}</c> o'rin belgisi bilan (kiosk tarjimalaridagidek). HR
/// o'rin belgisini o'chirsa yoki o'zgartirsa — o'sha matn qabul qilinmaydi
/// (<see cref="HrF02TextCatalog.Shape"/>).
/// </para>
/// <para>
/// Variant nomlari (uy turi, oilaviy holat, …) bu yerda emas — ular ro'yxatlar
/// (<see cref="HrF02TextCatalog"/>), kiosk bilan umumiy.
/// </para>
/// </remarks>
public static class QuestionnairePdfTexts
{
    public static IReadOnlyList<PdfTextGroup> Groups { get; } =
    [
        new("pdf.head", "PDF — sarlavha va ko‘rsatma",
        [
            new("pdf.head.company", "MBF Company — HR hujjatlar paketi"),
            new("pdf.head.formName", "Nomzod anketasi"),
            new("pdf.head.page", "Sahifa"),
            new("pdf.head.title", "NOMZOD ANKETASI"),
            new("pdf.head.subtitle", "Ishga qabul qilish uchun"),
            new("pdf.head.blank", "Blanka HR-F02"),
            new("pdf.head.version", "Versiya"),
            new("pdf.head.photo", "Rasm"),
            new("pdf.head.photoSize", "3 × 4 sm"),
            new("pdf.head.number", "Anketa №"),
            new("pdf.head.date", "Sana"),
            new("pdf.head.request", "So‘rov №"),
            new("pdf.head.vacancy", "Lavozim"),
            new("pdf.head.department", "Bo‘lim"),
            new("pdf.head.instruction",
                "Anketani aniq va to‘liq to‘ldiring. Javob variantlaridan keraklisini belgilang ({{box}}). " +
                "Sizga tegishli bo‘lmagan savolga «yo‘q» yoki «—» deb yozing. A qismni nomzod, B qismni HR bo‘limi to‘ldiradi."),
            new("pdf.head.partA", "A QISM"),
            new("pdf.head.partAText", "NOMZOD TOMONIDAN TO‘LDIRILADI"),
            new("pdf.head.partAHint", "Barcha savollarga o‘zingiz javob bering."),
        ]),
        new("pdf.common", "PDF — umumiy so‘zlar",
        [
            new("pdf.common.yes", "Ha"),
            new("pdf.common.no", "Yo‘q"),
            new("pdf.common.has", "Bor"),
            new("pdf.common.question", "Savol"),
            new("pdf.common.answer", "Javob"),
        ]),
        new("pdf.personal", "PDF — 1. Shaxsiy ma’lumotlar",
        [
            new("pdf.personal.title", "1. SHAXSIY MA’LUMOTLAR"),
            new("pdf.personal.lastName", "Familiyasi"),
            new("pdf.personal.firstName", "Ismi"),
            new("pdf.personal.middleName", "Otasining ismi"),
            new("pdf.personal.birthDate", "Tug‘ilgan sanasi"),
            new("pdf.personal.registeredAddress", "Doimiy ro‘yxatdagi manzil (propiska)"),
            new("pdf.personal.actualAddress", "Haqiqiy yashash manzili"),
            new("pdf.personal.housing", "Uy turi"),
            new("pdf.personal.mahalla", "Mahalla (MFY) nomi"),
            new("pdf.personal.phone", "Telefon raqami"),
            new("pdf.personal.phoneExtra", "Qo‘shimcha telefon (yaqiningiz)"),
            new("pdf.personal.telegram", "Telegram"),
            new("pdf.personal.email", "E-mail"),
            new("pdf.personal.marital", "Oilaviy holati"),
            new("pdf.personal.children", "Farzandlar soni"),
            new("pdf.personal.military", "Harbiy xizmat (erkaklar uchun)"),
            new("pdf.personal.militaryServed", "{{label}}, muddati: {{months}} oy"),
            new("pdf.personal.license", "Haydovchilik guvohnomasi"),
            new("pdf.personal.licenseYes", "Bor, toifasi: {{categories}}"),
            new("pdf.personal.car", "Shaxsiy avtomobil"),
            new("pdf.personal.carYes", "Bor, rusumi: {{car}}"),
        ]),
        new("pdf.education", "PDF — 2. Ma’lumoti",
        [
            new("pdf.education.title", "2. MA’LUMOTI"),
            new("pdf.education.level", "Ma’lumot darajasi"),
            new("pdf.education.institution", "O‘quv yurti nomi"),
            new("pdf.education.faculty", "Fakultet / mutaxassislik"),
            new("pdf.education.years", "O‘qigan yillari (dan – gacha)"),
            new("pdf.education.diploma", "Diplom / sertifikat №"),
            new("pdf.education.school", "Tamomlagan maktabingiz (№, tuman)"),
            new("pdf.education.subjects", "Qaysi fanlardan yaxshi o‘qigansiz"),
            new("pdf.education.courses", "Kurslar, sertifikatlar, malaka razryadi (payvandchi, stanokchi, elektrik va h.k.)"),
        ]),
        new("pdf.languages", "PDF — 3. Til bilish darajasi",
        [
            new("pdf.languages.title", "3. TIL BILISH DARAJASI"),
            new("pdf.languages.scale",
                "Darajani raqam bilan yozing:  0 – bilmayman,  1 – boshlang‘ich,  2 – o‘rta,  3 – o‘rtadan yuqori,  4 – yuqori (erkin),  5 – ona tili."),
            new("pdf.languages.language", "Til"),
            new("pdf.languages.speaking", "So‘zlashuv"),
            new("pdf.languages.reading", "O‘qish"),
            new("pdf.languages.writing", "Yozish"),
            new("pdf.languages.other", "Boshqa: {{name}}"),
        ]),
        new("pdf.computer", "PDF — 4. Kompyuter savodxonligi",
        [
            new("pdf.computer.title", "4. KOMPYUTER SAVODXONLIGI"),
            new("pdf.computer.scale",
                "Darajani raqam bilan yozing:  1 – boshlang‘ich,  2 – o‘rta,  3 – yaxshi,  4 – mukammal.  Bilmagan dasturingizni bo‘sh qoldiring."),
            new("pdf.computer.other", "Boshqa dasturlar va dasturlash tillari:"),
        ]),
        new("pdf.work", "PDF — 5. Mehnat faoliyati",
        [
            new("pdf.work.title", "5. MEHNAT FAOLIYATI"),
            new("pdf.work.experience", "Avval ishlaganmisiz?"),
            new("pdf.work.hint", "Oxirgi ish joyingizdan boshlab yozing."),
            new("pdf.work.organization", "Tashkilot nomi"),
            new("pdf.work.position", "Lavozimi"),
            new("pdf.work.period", "Davri (oy.yil – oy.yil)"),
            new("pdf.work.leaveReason", "Ishdan ketish sababi"),
            new("pdf.work.present", "hozir"),
        ]),
        new("pdf.references", "PDF — 6. Tavsiya beruvchilar",
        [
            new("pdf.references.title", "6. TAVSIYA BERUVCHILAR (AVVALGI ISH JOYIDAN SURISHTIRUV UCHUN)"),
            new("pdf.references.hint",
                "Sizni avvalgi ish joyingizdan yaxshi taniydigan rahbar yoki hamkasbingiz. HR bo‘limi ular bilan bog‘lanishi mumkin."),
            new("pdf.references.fullName", "F.I.Sh."),
            new("pdf.references.organizationPosition", "Tashkilot, lavozimi"),
            new("pdf.references.phone", "Telefon raqami"),
        ]),
        new("pdf.family", "PDF — 7. Oila a’zolari",
        [
            new("pdf.family.title", "7. OILA A’ZOLARI HAQIDA MA’LUMOT"),
            new("pdf.family.hint", "Ota-onangiz, turmush o‘rtog‘ingiz, aka-uka va opa-singillaringiz."),
            new("pdf.family.relation", "Qarindoshligi"),
            new("pdf.family.fullName", "F.I.Sh."),
            new("pdf.family.birthYear", "Tug‘ilgan yili"),
            new("pdf.family.workplace", "Ish joyi, lavozimi"),
            new("pdf.family.phone", "Telefon raqami"),
            new("pdf.family.address", "Yashash manzili"),
        ]),
        new("pdf.conditions", "PDF — 8. Ish sharoitlari va kutilmalar",
        [
            new("pdf.conditions.title", "8. ISH SHAROITLARI VA KUTILMALAR"),
            new("pdf.conditions.referral", "Bu vakansiya haqida qayerdan bildingiz?"),
            new("pdf.conditions.referralDetail", "{{label}}, kim/qaysi: {{detail}}"),
            new("pdf.conditions.acquaintances", "Korxonamizda tanishlaringiz yoki qarindoshlaringiz ishlaydimi? (F.I.Sh., bo‘limi)"),
            new("pdf.conditions.lastSalary", "Oxirgi ish joyingizda oylik maoshingiz qancha bo‘lgan?"),
            new("pdf.conditions.salaryBlank", "______________ so‘m"),
            new("pdf.conditions.expectedSalary", "Qancha oylik maoshga ishlamoqchisiz?"),
            new("pdf.conditions.startAfter", "Ishga qachondan chiqa olasiz?"),
            new("pdf.conditions.shifts", "Smenali yoki 12 soatlik ish rejimida ishlay olasizmi?"),
            new("pdf.conditions.overtime", "Zarurat bo‘lsa, ish vaqtidan keyin qolib ishlay olasizmi?"),
            new("pdf.conditions.travel", "Xizmat safarlariga (komandirovka) chiqishga rozimisiz?"),
            new("pdf.conditions.otherJob", "Boshqa joyda yarim stavka ishingiz yoki shaxsiy biznesingiz bormi?"),
            new("pdf.conditions.loan", "Hozirda kredit to‘lovlaringiz bormi?"),
            new("pdf.conditions.loanYes", "Ha, oylik to‘lov: {{payment}}"),
            new("pdf.conditions.loanBlank", "________ so‘m"),
            new("pdf.conditions.trial", "1 haftalik amaliy sinov (stajirovka) davrini o‘tashga rozimisiz?"),
            new("pdf.conditions.parents", "Zarurat bo‘lsa, ota-onangizni korxonaga taklif qilishimizga rozimisiz?"),
            new("pdf.conditions.startConditions", "Ishni boshlash uchun qanday shartlaringiz bor?"),
        ]),
        new("pdf.health", "PDF — 9. Sog‘liq va qo‘shimcha ma’lumotlar",
        [
            new("pdf.health.title", "9. SOG‘LIQ VA QO‘SHIMCHA MA’LUMOTLAR"),
            new("pdf.health.restriction",
                "Ushbu lavozimda ishlashingizga sog‘lig‘ingiz bo‘yicha cheklov bormi? (jumladan, xavfli ishlab chiqarish: gaz ballonlari bilan ishlash)"),
            new("pdf.health.restrictionHint", "Yakuniy xulosa tibbiy ko‘rikda beriladi."),
            new("pdf.health.patient", "Oilangizda doimiy parvarishga muhtoj bemor bormi?"),
            new("pdf.health.conviction", "Sudlanganmisiz?"),
            new("pdf.health.relativesConviction", "Yaqin qarindoshlaringiz orasida sudlanganlar bormi?"),
            new("pdf.health.abroad", "Chet elda bo‘lganmisiz? (qachon, qaysi davlatda, qanday maqsadda)"),
        ]),
        new("pdf.about", "PDF — 10. O‘zingiz haqingizda",
        [
            new("pdf.about.title", "10. O‘ZINGIZ HAQINGIZDA"),
            new("pdf.about.hint", "Qisqa va samimiy javob bering. To‘g‘ri yoki noto‘g‘ri javob yo‘q."),
            new("pdf.about.motivation", "Nima uchun ishlaysiz? Sizni ishlashga nima undaydi?"),
            new("pdf.about.whyUs", "Nima uchun aynan bizning korxonani tanladingiz?"),
            new("pdf.about.strengths", "Eng kuchli tomonlaringiz nimada?"),
            new("pdf.about.weaknesses", "Zaif tomonlaringiz nimada deb o‘ylaysiz?"),
            new("pdf.about.motivators", "Sizni ko‘proq nima rag‘batlantiradi?"),
            new("pdf.about.successDefinition", "Muvaffaqiyat deganda nimani tushunasiz?"),
            new("pdf.about.lastJobAchievement", "Oxirgi ish joyingizdagi eng katta muvaffaqiyatingiz nima bo‘lgan? (avval ishlaganlar uchun)"),
            new("pdf.about.bestIdea", "Avvalgi ish joyingizga qanday foydali g‘oya bergansiz va u qanday natija bergan? (avval ishlaganlar uchun)"),
            new("pdf.about.biggestChallenge", "Ishda duch kelgan eng katta qiyinchiligingiz nima bo‘lgan va uni qanday hal qilgansiz?"),
            new("pdf.about.stressHandling", "Ishdagi stressga qanday munosabatdasiz, u bilan qanday kurashasiz?"),
            new("pdf.about.unfairnessToYou", "Ish joyingizda sizga nisbatan nohaqlik qilinsa, qanday yo‘l tutasiz?"),
            new("pdf.about.unfairnessToCompany", "Korxonaga nisbatan nohaqlik qilinayotganiga guvoh bo‘lsangiz, nima qilasiz?"),
            new("pdf.about.teamwork", "Katta jamoada ishlay olasizmi? Jamoaviy ish deganda nimani tushunasiz?"),
            new("pdf.about.previousTeamOpinion", "Avvalgi ish joyingizdagi jamoa va rahbariyat haqida fikringiz."),
            new("pdf.about.studentWorkExperience", "O‘qish davrida biror joyda shogird bo‘lganmisiz yoki ishlaganmisiz?"),
            new("pdf.about.fiveYearPlan", "5 yildan keyin o‘zingizni qayerda ko‘rasiz?"),
            new("pdf.about.extraNote", "Qo‘shimcha ma’lumot: o‘zingiz haqingizda yana nimani bilishimizni istaysiz?"),
        ]),
        new("pdf.consent", "PDF — 11. Tilxat",
        [
            new("pdf.consent.title", "11. TILXAT (ROZILIK VA TASDIQ)"),
            new("pdf.consent.intro", "Men, {{name}} (F.I.Sh.):"),
            new("pdf.consent.accuracy",
                "Ushbu anketada keltirgan barcha ma’lumotlarim to‘liq va to‘g‘ri ekanini tasdiqlayman. Ma’lumotlar noto‘g‘ri ekani aniqlansa, bu mehnat shartnomasini bekor qilish uchun asos bo‘lishini bilaman."),
            new("pdf.consent.personalData",
                "Shaxsga doir ma’lumotlarim ishga qabul qilish va mehnat munosabatlari maqsadida O‘zbekiston Respublikasining «Shaxsga doir ma’lumotlar to‘g‘risida»gi Qonuniga muvofiq ishlanishiga roziman."),
            new("pdf.consent.referenceCheck",
                "Avvalgi ish joylarim va men ko‘rsatgan tavsiya beruvchilar orqali surishtiruv o‘tkazilishiga roziman."),
            new("pdf.consent.notifyChanges",
                "Ishga qabul qilinsam, anketadagi ma’lumotlarim o‘zgarganda bu haqda HR bo‘limini o‘z vaqtida xabardor qilaman."),
            new("pdf.consent.confidentiality",
                "Ishdan bo‘shaganimdan keyin 2 yil davomida korxona sirlarini oshkor qilmaslikka majburiyat olaman. Bu majburiyat buzilgan taqdirda menga nisbatan qonuniy choralar ko‘rilishiga roziman."),
            new("pdf.consent.date", "Sana:"),
            new("pdf.consent.signature", "Imzo:"),
            new("pdf.consent.origin", "Onlayn to‘ldirildi: {{date}}  ·  Til: {{language}}"),
        ]),
        new("pdf.hr", "PDF — B qism (HR bo‘limi)",
        [
            new("pdf.hr.part", "B QISM"),
            new("pdf.hr.partText", "HR BO‘LIMI TOMONIDAN TO‘LDIRILADI"),
            new("pdf.hr.hint", "Nomzod bu qismni to‘ldirmaydi."),
            new("pdf.hr.documentsTitle", "12. HUJJATLAR NAZORATI"),
            new("pdf.hr.document", "Hujjat"),
            new("pdf.hr.submitted", "Topshirildi"),
            new("pdf.hr.note", "Izoh"),
            new("pdf.hr.photo", "Rasm (3 × 4)"),
            new("pdf.hr.photoNote", "Onlayn olingan"),
            new("pdf.hr.passport", "Pasport / ID-karta nusxasi"),
            new("pdf.hr.diploma", "Diplom / sertifikat nusxasi"),
            new("pdf.hr.license", "Haydovchilik guvohnomasi nusxasi (bo‘lsa)"),
            new("pdf.hr.noConviction", "Sudlanmaganlik haqida ma’lumotnoma"),
            new("pdf.hr.military", "Harbiy hisob hujjati (erkaklar)"),
            new("pdf.hr.signedForm", "Imzolangan anketa (A qism)"),
            new("pdf.hr.signedNote", "Onlayn imzolangan"),
            new("pdf.hr.testsTitle", "13. TEST VA SINOVLAR"),
            new("pdf.hr.test", "Sinov"),
            new("pdf.hr.result", "Natija"),
            new("pdf.hr.professional", "Professional test"),
            new("pdf.hr.points", "________ ball"),
            new("pdf.hr.disc", "DISC test"),
            new("pdf.hr.productivity", "Unumdorlik (produktivlik) testi"),
            new("pdf.hr.russian", "Rus tili (og‘zaki tekshiruv)"),
            new("pdf.hr.russianLevel", "Daraja: ____ (0–5)"),
            new("pdf.hr.excel", "Excel (amaliy tekshiruv)"),
            new("pdf.hr.excelLevel", "Daraja: ____ (1–4)"),
            new("pdf.hr.practical", "Amaliy sinov (usta bahosi, sana)"),
            new("pdf.hr.interviewTitle", "14. DASTLABKI SUHBAT XULOSASI"),
            new("pdf.hr.criterion", "Mezon"),
            new("pdf.hr.score", "Baho (1–5)"),
            new("pdf.hr.fit", "Talablarga mosligi (HR-F01, 2-bo‘lim)"),
            new("pdf.hr.experience", "Tajriba va malaka"),
            new("pdf.hr.communication", "Muloqot va motivatsiya"),
            new("pdf.hr.practicalResult", "Amaliy sinov natijasi"),
            new("pdf.hr.decision", "Qaror"),
            new("pdf.hr.council", "Kengashga (HR-F04)"),
            new("pdf.hr.reserve", "Zaxira bazaga"),
            new("pdf.hr.rejected", "Rad etildi"),
            new("pdf.hr.rejectReason", "Rad etish sababi: ______________________________"),
            new("pdf.hr.specialist", "HR mutaxassisi: ______________________"),
            new("pdf.hr.sign", "Imzo: ______________"),
            new("pdf.hr.date", "Sana: ______________"),
            new("pdf.hr.storage",
                "Saqlash: ishga qabul qilinsa, xodimning shaxsiy ish yig‘majildida; qabul qilinmasa, 6 oy. " +
                "Reglament: HR-R02 «Ishga qabul». Keyingi blankalar: HR-F03 «Surishtiruv», HR-F04 «Kengash»."),
        ]),
    ];

    private static readonly Dictionary<string, string> Defaults =
        Groups.SelectMany(group => group.Items).ToDictionary(item => item.Key, item => item.Default, StringComparer.Ordinal);

    /// <summary>Barcha kalitlar.</summary>
    public static IReadOnlyCollection<string> Keys => Defaults.Keys;

    /// <summary>Kalitning asl matni; noma'lum kalit — dasturchi xatosi.</summary>
    public static string Default(string key)
        => Defaults.TryGetValue(key, out var text)
            ? text
            : throw new KeyNotFoundException($"PDF matni yo'q: {key}");
}
