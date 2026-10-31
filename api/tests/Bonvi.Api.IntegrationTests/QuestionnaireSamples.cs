using Bonvi.Application.Recruiting.Forms;
using Bonvi.Domain.Recruiting;
using SkiaSharp;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Planshet anketasining namunaviy javoblari — PDF va API testlari uchun umumiy.
/// </summary>
internal static class QuestionnaireSamples
{
    /// <summary>
    /// Hamma maydoni to'ldirilgan anketa — PDF'ning har bir katagi ko'rinsin.
    /// </summary>
    public static QuestionnaireFormAnswers Full(DateOnly today, string phone = "+998905551122") => new()
    {
        VacancyId = null,
        LastName = "G‘ulomov",
        FirstName = "Sardor",
        MiddleName = "Baxtiyor o‘g‘li",
        BirthDate = today.AddYears(-27).AddDays(-40 - Spread(phone)),
        RegisteredAddress = "Toshkent sh., Yunusobod tumani, 11-mavze, 23-uy, 45-xonadon",
        ActualAddress = "Toshkent vil., Qibray tumani, Bobur MFY, Navro‘z ko‘chasi 7-uy",
        HousingType = FormHousingType.House,
        Mahalla = "Bobur",
        Phone = phone,
        PhoneExtra = "+998935553344",
        Telegram = "@sardor_gulomov",
        Email = "sardor.g@example.uz",
        MaritalStatus = MaritalStatus.Married,
        ChildrenCount = 2,
        MilitaryService = FormMilitaryService.Served,
        MilitaryServiceMonths = 12,
        HasDriverLicense = true,
        DriverLicenseCategories = ["C", "B"],
        HasCar = true,
        CarModel = "Chevrolet Cobalt",
        EducationLevel = EducationLevel.HigherComplete,
        Educations =
        [
            new FormEducation("Toshkent davlat texnika universiteti", "Mashinasozlik texnologiyasi", 2015, 2019, "B 0123456"),
            new FormEducation("Qibray politexnika kolleji", "Payvandlash", 2012, 2015, null),
        ],
        SchoolName = "45-maktab, Qibray tumani",
        StrongSubjects = "Matematika, fizika",
        Courses = "Payvandchi 5-razryad (2020), AutoCAD kursi, xavfsizlik texnikasi bo‘yicha sertifikat",
        Languages =
        [
            new FormLanguageSkill(FormLanguage.Uzbek, null, 5, 5, 5),
            new FormLanguageSkill(FormLanguage.Russian, null, 3, 3, 2),
            new FormLanguageSkill(FormLanguage.English, null, 1, 2, 1),
            new FormLanguageSkill(FormLanguage.Other, "Turk tili", 2, 1, 1),
        ],
        // Jadvalning hamma 11 dasturi — har daraja katagi PDF'da ko'rinsin
        ComputerSkills = new Dictionary<string, int>
        {
            ["windows"] = 3,
            ["mobileOs"] = 4,
            ["msWord"] = 3,
            ["msExcel"] = 2,
            ["msPowerPoint"] = 1,
            ["googleWorkspace"] = 2,
            ["oneC"] = 1,
            ["didox"] = 2,
            ["crm"] = 1,
            ["autocad"] = 3,
            ["photoshop"] = 1,
        },
        ComputerSkillsOther = "KOMPAS-3D o‘quv versiyasi",
        HasWorkExperience = true,
        Jobs =
        [
            new FormJob("«Metan Gaz» MChJ", "Payvandchi", "2021-03", null, true, null),
            new FormJob("«Toshkent Ballon» AJ", "Usta yordamchisi", "2019-08", "2021-02", false, "Oylik kam edi, uyga uzoq"),
        ],
        References =
        [
            new FormReference("Karimov Anvar", "«Metan Gaz» MChJ, sex boshlig‘i", "+998901112233"),
        ],
        FamilyMembers =
        [
            new FormFamilyMember(FormFamilyRelation.Father, "G‘ulomov Baxtiyor", 1968, "Nafaqada", "+998901234567", "Qibray tumani"),
            new FormFamilyMember(FormFamilyRelation.Mother, "G‘ulomova Dilnoza", 1971, "Uy bekasi", null, "Qibray tumani"),
            new FormFamilyMember(FormFamilyRelation.Spouse, "Tursunova Malika", 1999, "O‘qituvchi, 12-maktab", "+998977778899", "Qibray tumani"),
        ],
        ReferralChannelId = ReferralSeeds.Acquaintance,
        ReferralDetail = "Karimov Anvar (sex boshlig‘i)",
        HasAcquaintances = true,
        AcquaintancesDetail = "Karimov Anvar, payvandlash sexi",
        LastSalary = 4_500_000,
        ExpectedSalary = 6_000_000,
        StartAfter = FormStartAfter.TenDays,
        CanWorkShifts = true,
        CanWorkOvertime = true,
        CanTravel = false,
        HasOtherJob = false,
        HasLoan = true,
        LoanMonthlyPayment = 1_200_000,
        AgreesToTrial = true,
        AgreesParentsInvite = true,
        StartConditions = "Transport xarajati qoplansa yaxshi bo‘lardi.",
        HasHealthRestriction = false,
        HasFamilyPatient = false,
        HasConviction = false,
        RelativesConviction = false,
        AbroadTrips = "2018 yil, Turkiya, ish safari (2 hafta)",
        Motivation = "Oilamni ta’minlash va kasbimda o‘sish.",
        WhyUs = "Korxona barqaror, ish haqi o‘z vaqtida beriladi deb eshitdim.",
        Strengths = "Mas’uliyatli, payvandlashda tajribali, jamoada yaxshi ishlayman.",
        Weaknesses = "Ba’zan ishni o‘zim tugatishga intilaman, yordam so‘rashga uyalaman.",
        Motivators = "Adolatli baholash va mukofot.",
        SuccessDefinition = "Ishni sifatli va o‘z vaqtida bajarish.",
        LastJobAchievement = "Brak ulushini 3 oyda ikki baravar kamaytirdik.",
        BestIdea = "Payvand choklarini tekshirish uchun shablon taklif qildim — tekshiruv vaqti qisqardi.",
        BiggestChallenge = "Muddatli buyurtmada ikki kishi kasal bo‘lib qoldi — smenani qayta taqsimladik.",
        StressHandling = "Ishni bo‘laklarga bo‘lib, navbatma-navbat bajaraman.",
        UnfairnessToYou = "Avval rahbar bilan ochiq gaplashaman.",
        UnfairnessToCompany = "Rahbariyatga xabar beraman.",
        Teamwork = "Ha. Jamoaviy ish — bir-birini to‘ldirish.",
        PreviousTeamOpinion = "Jamoa yaxshi edi, rahbariyat talabchan.",
        StudentWorkExperience = "Kollejda o‘qib yurib ustaxonada shogird bo‘lganman.",
        FiveYearPlan = "Sex ustasi yoki texnolog bo‘lib.",
        ExtraNote = "Kechki smenaga ham tayyorman. Тест: кирилл ҳарфлари ҳам чиқиши керак.",
        ConsentAccuracy = true,
        ConsentPersonalData = true,
        ConsentReferenceCheck = true,
        ConsentNotifyChanges = true,
        ConsentConfidentiality = true,
        SignatureName = "G‘ulomov Sardor Baxtiyor o‘g‘li",
    };

    /// <summary>
    /// Faqat majburiy maydonlar — PDF bo'sh kataklari qog'ozdagidek qolsin (10-bo'lim 2026-10-29 dan
    /// butunlay majburiy, faqat avvalgi ish haqidagi uchtasi so'ralmaydi). Harbiy xizmat
    /// ham yo'q (2026-10-26 dan hammaga ixtiyoriy). Avval ishlamagan (3.1): ish joyi, tavsiya
    /// beruvchi va ishga oid ikki savol so'ralmaydi.
    /// </summary>
    public static QuestionnaireFormAnswers Minimal(DateOnly today, string phone = "+998905551199") => new()
    {
        LastName = "Каримова",
        FirstName = "Ойдин",
        BirthDate = today.AddYears(-19).AddDays(-Spread(phone)),
        RegisteredAddress = "Тошкент ш., Чилонзор тумани",
        ActualAddress = "Тошкент ш., Чилонзор тумани",
        Phone = phone,
        MaritalStatus = MaritalStatus.Unmarried,
        HasDriverLicense = false,
        EducationLevel = EducationLevel.SecondarySchool,
        Languages = [new FormLanguageSkill(FormLanguage.Russian, null, 1, 1, 0)],
        HasWorkExperience = false,
        FamilyMembers = [new FormFamilyMember(FormFamilyRelation.Mother, "Каримова Нигора")],
        ReferralChannelId = ReferralSeeds.Telegram,
        HasAcquaintances = false,
        ExpectedSalary = 2_500_000,
        StartAfter = FormStartAfter.OnHireDay,
        CanWorkShifts = false,
        CanWorkOvertime = true,
        CanTravel = false,
        HasOtherJob = false,
        HasLoan = false,
        AgreesToTrial = true,
        HasHealthRestriction = false,
        HasConviction = false,
        Motivation = "Тажриба орттириш.",
        WhyUs = "Уйга яқин.",
        Strengths = "Тиришқоқман.",
        Weaknesses = "Тажрибам кам.",
        Motivators = "Яхши жамоа.",
        SuccessDefinition = "Ишни вақтида бажариш.",
        BiggestChallenge = "Имтиҳонга тайёрланиш.",
        StressHandling = "Тинчланиб оламан.",
        UnfairnessToYou = "Раҳбарга айтаман.",
        UnfairnessToCompany = "Хабар бераман.",
        Teamwork = "Ҳа.",
        StudentWorkExperience = "Йўқ.",
        FiveYearPlan = "Мутахассис бўлиб.",
        ExtraNote = "Йўқ.",
        ConsentAccuracy = true,
        ConsentPersonalData = true,
        ConsentReferenceCheck = true,
        ConsentNotifyChanges = true,
        ConsentConfidentiality = true,
        SignatureName = "Karimova Oydin",
    };

    /// <summary>Selfi o'rnidagi rasm — yuzga o'xshash shakl (PDF'da ko'rinishi tekshirilsin).</summary>
    public static byte[] Photo()
    {
        using var surface = SKSurface.Create(new SKImageInfo(900, 1200));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(0xDD, 0xE6, 0xF0));

        using var skin = new SKPaint { Color = new SKColor(0xE8, 0xB8, 0x96), IsAntialias = true };
        using var shirt = new SKPaint { Color = new SKColor(0x2F, 0x4F, 0x7F), IsAntialias = true };
        using var hair = new SKPaint { Color = new SKColor(0x3A, 0x2A, 0x20), IsAntialias = true };

        canvas.DrawOval(new SKRect(250, 830, 650, 1250), shirt);
        canvas.DrawOval(new SKRect(300, 300, 600, 700), skin);
        canvas.DrawOval(new SKRect(290, 260, 610, 450), hair);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);

        return data.ToArray();
    }

    /// <summary>Barmoq bilan chizilgan imzoga o'xshash shaffof PNG.</summary>
    public static byte[] Signature()
    {
        using var surface = SKSurface.Create(new SKImageInfo(600, 200, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var ink = new SKPaint
        {
            Color = new SKColor(0x10, 0x20, 0x60),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 5,
            StrokeCap = SKStrokeCap.Round,
        };

        using var path = new SKPath();
        path.MoveTo(40, 140);
        path.CubicTo(90, 20, 140, 180, 190, 90);
        path.CubicTo(230, 30, 260, 160, 320, 110);
        path.CubicTo(380, 60, 420, 150, 470, 100);
        path.LineTo(560, 120);
        canvas.DrawPath(path, ink);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    /// <summary>
    /// Telefonga bog'liq 0–999 kun: har test o'z telefoni bilan o'z tug'ilgan sanasini oladi.
    /// </summary>
    /// <remarks>
    /// 2026-10-31 dan onlayn anketa F.I. + tug'ilgan sana bo'yicha ham eski kartani topadi — bir xil
    /// sanali namunalar bir umumiy test bazasida bir-birining kartasiga qo'shilib ketardi.
    /// </remarks>
    private static int Spread(string phone)
        => int.Parse(phone[^3..], System.Globalization.CultureInfo.InvariantCulture);
}
