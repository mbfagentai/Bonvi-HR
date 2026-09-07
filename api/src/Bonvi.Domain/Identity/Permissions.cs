namespace Bonvi.Domain.Identity;

/// <summary>
/// Tizimdagi barcha ruxsatlar.
/// </summary>
/// <remarks>
/// <para>
/// Ruxsatlar alohida jadval emas, <b>konstanta</b>: ular kod bilan birga
/// o'zgaradi va ma'lumot sifatida tahrirlanmaydi. Rolga biriktirilganda
/// esa faqat kod (satr) saqlanadi.
/// </para>
/// <para>
/// Nega bunday: «yangi ruxsat qo'shildi» — bu kod o'zgarishi. Agar u
/// bazada tursa, migratsiya va kod bir-biridan uzoqlashib ketadi.
/// </para>
/// </remarks>
public static class Permissions
{
    // ---------- Xodimlar ----------
    public const string EmployeesView = "employees.view";
    public const string EmployeesCreate = "employees.create";
    public const string EmployeesEdit = "employees.edit";
    public const string EmployeesDelete = "employees.delete";

    // ---------- Mehnat munosabatlari va buyruqlar ----------
    public const string EmploymentView = "employment.view";
    public const string EmploymentManage = "employment.manage";

    /// <summary>
    /// Oylik ish haqini ko'rish. ALOHIDA ruxsat — HR so'ragan talab:
    /// Excel'da oklad hammaga ochiq edi, tizimda esa cheklanadi.
    /// </summary>
    public const string SalaryView = "salary.view";

    // ---------- Ro'yxatlar va teglar ----------
    public const string ListsView = "lists.view";
    public const string ListsManage = "lists.manage";

    /// <summary>
    /// Rag'bat va jarima. ALOHIDA ruxsat — pul (jarima/mukofot summasi) va
    /// intizomiy chora bilan bog'liq, oklad kabi nozik ma'lumot. Qobiliyatlar
    /// profili esa alohida ruxsat OLMAYDI: u xodim kartasining bir qismi va
    /// <see cref="EmployeesView"/>/<see cref="EmployeesEdit"/> orqali boshqariladi.
    /// </summary>
    public const string IncentivesView = "incentives.view";

    public const string IncentivesManage = "incentives.manage";

    /// <summary>
    /// Exit interview («qayta aloqa», M10). ALOHIDA ruxsat —
    /// <see cref="IncentivesView"/> bilan bir xil mantiq: xodimning ishdan
    /// ketish sababi haqidagi o'z so'zlari (rahbariyat bilan ziddiyat,
    /// noroziliklar) nozik ma'lumot va uni har bir HR emas, ko'rish kerak
    /// bo'lgan kishi ko'rishi kerak.
    /// </summary>
    public const string FeedbackView = "feedback.view";

    public const string FeedbackManage = "feedback.manage";

    /// <summary>
    /// Baxtsiz hodisalar jurnali (M11). ALOHIDA ruxsat — mehnat muhofazasi
    /// bo'yicha huquqiy javobgarlik bilan bog'liq yozuv (komissiya xulosasi,
    /// jarohat darajasi), <see cref="IncentivesView"/> kabi umumiy
    /// <see cref="EmployeesView"/> dan tor.
    /// </summary>
    public const string SafetyView = "safety.view";

    public const string SafetyManage = "safety.manage";

    /// <summary>
    /// Rekruting (2-bosqich): vakansiya, nomzod, voronka, ishga olish.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ALOHIDA ruxsat guruhi — <see cref="EmployeesView"/> ostiga
    /// qo'shilmagan. Sabab shakliy emas, mazmuniy: nomzod HALI xodim emas
    /// va uning yozuvi butunlay boshqa toifadagi ma'lumot — rad etish
    /// sabablari, suhbat xulosalari, kelishilgan oklad. Xodimlar
    /// ro'yxatini ochgan har bir kishi «kimni nega olmadik» degan
    /// yozuvlarni ham ko'rib turishi kerak emas
    /// (<see cref="IncentivesView"/> bilan bir xil mantiq).
    /// </para>
    /// <para>
    /// <b>Voronka bosqichlari ham SHU ruxsat ostida</b>, ma'lumotnomalar
    /// (<see cref="CatalogManage"/>) ostida emas: bosqichlar ro'yxati —
    /// rekrutingning ish jarayoni ta'rifi va uni o'zgartirish HR'ning
    /// tanlov usulini o'zgartiradi, ma'lumotnomaga yangi lavozim qo'shish
    /// esa butunlay boshqa amal.
    /// </para>
    /// <para>
    /// Uchala rol ham oladi: rekruting — HR bo'limining asosiy kundalik
    /// ishi, «ko'rish» va «boshqarish» esa bu yerda amalda ajralmaydi
    /// (nomzodni ko'rgan HR uni bosqichga ham o'tkazadi).
    /// </para>
    /// </remarks>
    public const string RecruitingView = "recruiting.view";

    public const string RecruitingManage = "recruiting.manage";

    // ---------- Ma'lumotnomalar ----------
    public const string CatalogView = "catalog.view";
    public const string CatalogManage = "catalog.manage";

    // ---------- Hisobotlar ----------
    public const string ReportsView = "reports.view";
    public const string ReportsExport = "reports.export";

    // ---------- Tizim ----------
    public const string UsersManage = "users.manage";
    public const string AuditView = "audit.view";

    /// <summary>
    /// Tizim sozlamalarini o'zgartirish.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>YAGONA ruxsat, u super administratordan boshqa hech kimda yo'q</b>
    /// — shu jumladan bosh rahbarda ham. Bu loyihadagi «rahbariyat
    /// ruxsatlar bo'yicha TENG» qoidasidan yagona chekinish va u
    /// foydalanuvchining aniq qarori.
    /// </para>
    /// <para>
    /// Sabab: bu sozlamalar HR jarayoni emas, TIZIMNING ishlashi —
    /// bildirishnoma chegaralari va bot matni. Ular noto'g'ri qo'yilsa
    /// natija darhol ko'rinmaydi: bildirishnoma jimgina kelmay qo'yadi
    /// yoki bot bo'sh matn ko'rsatadi. Bunday sozlamaga tegadigan odam
    /// bitta bo'lgani ma'qul.
    /// </para>
    /// </remarks>
    public const string SettingsManage = "settings.manage";

    /// <summary>Barcha ruxsatlar — tekshirish va seed uchun.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        EmployeesView, EmployeesCreate, EmployeesEdit, EmployeesDelete,
        EmploymentView, EmploymentManage, SalaryView,
        ListsView, ListsManage,
        IncentivesView, IncentivesManage,
        FeedbackView, FeedbackManage,
        SafetyView, SafetyManage,
        RecruitingView, RecruitingManage,
        CatalogView, CatalogManage,
        ReportsView, ReportsExport,
        UsersManage, AuditView, SettingsManage,
    };

    public static bool Exists(string permission) => All.Contains(permission);
}

/// <summary>
/// Standart rollar va ularning ruxsatlari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tizimdan kim foydalanadi.</b> Xodimlar bu tizimga kirmaydi va ularga
/// login-parol berilmaydi. Tizim faqat <b>xodimlarni boshqarish</b> uchun:
/// undan HR bo'limi xodimlari va asosiy rahbar foydalanadi.
/// </para>
/// <para>
/// Shu sababli rollar atigi <b>uchta</b> va ular O'ZARO IYERARXIK:
/// </para>
/// <list type="bullet">
///   <item>
///     <b><see cref="SuperAdmin"/></b> — tizim egasi. Barcha ruxsatlar.
///     Tizimda ATIGI BITTA bo'ladi.
///   </item>
///   <item>
///     <b><see cref="MainBoss"/></b> — bosh rahbar. RUXSATLAR bo'yicha super
///     administrator bilan TENG (<see cref="Permissions.All"/> ning O'ZI),
///     farq faqat KIMNI
///     boshqara olishida: bosh rahbar faqat HR menejerlarga tegadi. Bu farq
///     ruxsat kodi bilan emas, use-case qoidasi bilan qo'yilgan (sababi
///     <see cref="Permissions.UsersManage"/> ni ikkalasi ham olgani uchun —
///     ya'ni «ruxsati bor, lekin kimga ishlatishi cheklangan»). U ham
///     tizimda ATIGI BITTA bo'ladi.
///   </item>
///   <item>
///     <b><see cref="HrManager"/></b> — kundalik kadrlar ishi. Bir necha
///     kishi oladi. Yagona farqi — <see cref="Permissions.UsersManage"/>
///     yo'q: hisob ochish va rol berish rahbariyatning ishi.
///   </item>
/// </list>
/// <para>
/// Bo'lim boshliqlari, xodimlar yoki alohida "operator" darajasi uchun rol
/// ataylab yaratilmagan — kerak bo'lganda qo'shiladi, lekin ishlatilmaydigan
/// rolni oldindan yaratish faqat chalkashlik beradi.
/// </para>
/// <para>
/// Bu ro'yxat birinchi ishga tushirishda bazaga yoziladi (seed).
/// Keyinchalik administrator rolga ruxsat qo'shishi/olib tashlashi mumkin —
/// bu yerdagi qiymatlar faqat boshlang'ich holat.
/// </para>
/// </remarks>
public static class SystemRoles
{
    /// <summary>Super administrator — tizim egasi. Barcha ruxsatlar, ATIGI BITTA hisob.</summary>
    public const string SuperAdmin = "super_admin";

    /// <summary>
    /// Bosh rahbar. Ruxsatlari super administrator bilan bir xil, lekin
    /// foydalanuvchi boshqaruvida faqat HR menejerlarga tegadi. ATIGI BITTA hisob.
    /// </summary>
    public const string MainBoss = "main_boss";

    /// <summary>
    /// HR menejeri. Butun kadrlar ishi, oylik ish haqi ham ko'rinadi.
    /// Bonvi'da bu rolni bir necha kishi oladi.
    /// </summary>
    public const string HrManager = "hr_manager";

    /// <summary>
    /// Telegram anketa boti — ODAM EMAS, xizmat hisobi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Botga atigi IKKITA ruxsat beriladi: ochiq vakansiyalarni o'qish va
    /// nomzod yozish. Boshqa hech narsa — u xodimni ko'ra olmaydi,
    /// o'chira olmaydi, oklad va belgilarga umuman yeta olmaydi.
    /// </para>
    /// <para>
    /// <b>Nega HR menejeri roli berilmadi.</b> Bot tokeni Telegram
    /// serverlariga chiqadigan sir va u sizib ketishi mumkin bo'lgan
    /// yagona hisob. Undan foydalangan odam nima qila olishi ANIQ
    /// chegaralangan bo'lishi kerak: eng yomon holatda u soxta nomzod
    /// yozadi, xodimlar bazasiga esa tegmaydi. HR menejeri roli bilan u
    /// xodim o'chira olardi.
    /// </para>
    /// <para>
    /// Rol <see cref="SingleHolder"/> ga KIRMAYDI: kelajakda ikkinchi bot
    /// (masalan boshqa til yoki boshqa kanal uchun) qo'shilishi mumkin va
    /// bu cheklov faqat rahbariyat lavozimlari uchun ma'noli.
    /// </para>
    /// </remarks>
    public const string RecruitingBot = "recruiting_bot";

    /// <summary>
    /// Eski (uch rolli modelga o'tishdan oldingi) administrator roli kodi.
    /// </summary>
    /// <remarks>
    /// Faqat KO'CHIRISH uchun mavjud: allaqachon ishlab turgan bazalarda
    /// <c>admin</c> kodli rol bor va <c>.env</c> dagi boshlang'ich hisob
    /// aynan shunga bog'langan. <c>IdentitySeeder</c> uni <see cref="SuperAdmin"/>
    /// ga QAYTA NOMLAYDI — yangi rol yaratib, foydalanuvchilarni ko'chirib
    /// o'tirmaydi (sababi seeder ichida batafsil). Yangi bazalarda bu kod
    /// hech qachon paydo bo'lmaydi.
    /// </remarks>
    public const string LegacyAdmin = "admin";

    /// <summary>
    /// TO'LIQ huquqli rollar — ulardan ruxsat olib tashlab bo'lmaydi.
    /// </summary>
    /// <remarks>
    /// Nega ikkitasi ham: foydalanuvchi qarori bo'yicha bosh rahbar ruxsatlar
    /// bo'yicha super administrator bilan TENG. Ularning farqi boshqa
    /// o'lchamda (kimni boshqara oladi) va u bu yerda emas, use-case
    /// qatlamida tekshiriladi.
    /// </remarks>
    public static readonly IReadOnlySet<string> FullAccess =
        new HashSet<string>(StringComparer.Ordinal) { SuperAdmin, MainBoss };

    /// <summary>
    /// Tizimda ATIGI BITTA FAOL egasi bo'lishi mumkin bo'lgan rollar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ro'yxat <see cref="FullAccess"/> bilan hozircha aynan bir xil, lekin
    /// ATAYLAB alohida turadi: bular ikki xil qoida. «To'liq huquq» — rolning
    /// ruxsatlari haqida, «atigi bitta» — hisoblar soni haqida. Ular bir
    /// ro'yxatga qo'shib yuborilsa, kelajakda biri o'zgarganda ikkinchisi
    /// jimgina o'zgarib ketardi.
    /// </para>
    /// <para>
    /// <b>Nega faqat FAOL hisoblar sanaladi.</b> Odam almashadi: eski super
    /// administrator yoki bosh rahbar ishdan ketsa, uning hisobi
    /// faolsizlantiriladi va o'rniga yangisi qo'yiladi. Faolsizlarni ham
    /// sanash tizimni «bu lavozim abadiy band» holatiga qulflab qo'yardi va
    /// buni faqat bazaga qo'lda aralashib ochish mumkin bo'lardi.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlySet<string> SingleHolder =
        new HashSet<string>(StringComparer.Ordinal) { SuperAdmin, MainBoss };

    public static IReadOnlyDictionary<string, (string Name, string[] Permissions)> Defaults { get; } =
        new Dictionary<string, (string, string[])>(StringComparer.Ordinal)
        {
            [SuperAdmin] = ("Super administrator", [.. Permissions.All]),

            /*
              Bosh rahbarda `settings.manage` dan TASHQARI hamma ruxsat bor.

              Ilgari ikkala rol ham `Permissions.All` ni olardi va izohda
              «ruxsatlar bo'yicha AYNAN bir xil» deb yozilgandi. Endi
              yagona istisno paydo bo'ldi — tizim sozlamalari
              (foydalanuvchi qarori, sabab `Permissions.SettingsManage`
              izohida). Ro'yxat baribir qo'lda sanab yozilmaydi: yangi
              ruxsat qo'shilsa u bosh rahbarga O'ZI yetib boradi va faqat
              ataylab chiqarib tashlangani qolib ketmaydi.
            */
            [MainBoss] = ("Bosh rahbar",
                [.. Permissions.All.Where(p => p != Permissions.SettingsManage)]),

            [HrManager] = ("HR menejeri",
            [
                Permissions.EmployeesView, Permissions.EmployeesCreate,
                Permissions.EmployeesEdit, Permissions.EmployeesDelete,
                Permissions.EmploymentView, Permissions.EmploymentManage,
                Permissions.SalaryView,
                Permissions.ListsView, Permissions.ListsManage,
                Permissions.IncentivesView, Permissions.IncentivesManage,
                Permissions.FeedbackView, Permissions.FeedbackManage,
                Permissions.SafetyView, Permissions.SafetyManage,
                Permissions.RecruitingView, Permissions.RecruitingManage,
                Permissions.CatalogView, Permissions.CatalogManage,
                Permissions.ReportsView, Permissions.ReportsExport,
                Permissions.AuditView,
            ]),

            [RecruitingBot] = ("Telegram bot",
            [
                // Ochiq vakansiyalarni o'qish va nomzod yozish — boshqa
                // hech narsa (izohi `RecruitingBot` da).
                Permissions.RecruitingView, Permissions.RecruitingManage,
            ]),
        };
}
