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

    /// <summary>
    /// Xodimning SHAXSIY ma'lumotini ko'rish: tug'ilgan sana, JSHSHIR, pasport, ikkinchi
    /// telefon, manzil, sudlanganlik va mehnat.uz havolasi; hujjatlar, qobiliyatlar va
    /// tekshiruv materiallari (SPEC-rollar-va-audit §3.4, §3.10).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>«Ko'rish» darajasida alohida belgilanadi, «O'zgartirish» da esa O'Z-O'ZIDAN bor</b>
    /// (<c>PersonalDataAccess</c>): kartani tahrirlagan odam JSHSHIR va pasportni qayta
    /// yozadi — ularni ko'rmasa, bo'sh qiymat yuborib o'chirib yuborardi. Ya'ni shaxsiy
    /// ma'lumotsiz rol — faqat ko'radigan rol (masalan kelajakdagi davomat xodimi).
    /// </para>
    /// <para>
    /// 2026-10-12 dan audit jurnali shu bo'yicha yashiradi (A2). Xodim kartasi, ro'yxat va
    /// eksportdagi yashirish — keyingi bosqich (P1); u ham AYNAN shu qoidani ishlatadi.
    /// </para>
    /// </remarks>
    public const string EmployeesPersonalView = "employees.personal.view";

    // ---------- Mehnat munosabatlari va buyruqlar ----------
    public const string EmploymentView = "employment.view";

    /*
      Mehnat munosabatidagi YOZISH amallari — har biri O'Z ruxsati bilan.

      Ilgari bularning hammasi bitta `employment.manage` ostida edi: ruxsati
      bor odam ishga ham olardi, okladni ham o'zgartirardi, ishdan ham
      bo'shatardi. Egasining qarori (2026-09-26): «Kadr bo'limi boshlig'i»
      degan rolni o'zi tuzmoqchi — oklad, bo'shatish va sinovni yakunlash
      faqat unda, HR menejeri esa ko'chirish, qo'shimcha vazifa va sinovni
      uzaytirishni qiladi. Bunday bo'linishni bitta ruxsat bilan ifodalab
      bo'lmaydi, shu sababli har bir endpoint o'z kodini oldi.

      Pul qoidasi: boshqa amal ICHIDAGI summa (ko'chirish bilan yangi oklad,
      qo'shimcha vazifa to'lovi, sinov yakunidagi oklad, yozuvdagi summani
      tuzatish) ham `employment.salary` ni talab qiladi — aks holda «bir
      bo'limdan o'sha bo'limga ko'chirib, okladni oshirish» degan aylanma
      yo'l qolardi. Yagona istisno — ishga qabuldagi boshlang'ich oklad:
      o'zgartiriladigan eski oklad yo'q, u qabul buyrug'ining o'zi.
    */

    /// <summary>Ishga qabul — <c>POST /api/employments/hire</c>.</summary>
    public const string EmploymentHire = "employment.hire";

    /// <summary>Lavozim yoki bo'lim o'zgarishi.</summary>
    public const string EmploymentTransfer = "employment.transfer";

    /// <summary>Qo'shimcha vazifa berish.</summary>
    public const string EmploymentDuty = "employment.duty";

    /// <summary>
    /// Oklad o'zgarishi — va boshqa amal ichida yuborilgan HAR QANDAY summa.
    /// </summary>
    public const string EmploymentSalary = "employment.salary";

    /// <summary>Ishdan bo'shatish.</summary>
    public const string EmploymentTerminate = "employment.terminate";

    /// <summary>Sinov muddatini uzaytirish.</summary>
    public const string EmploymentProbationExtend = "employment.probation.extend";

    /// <summary>
    /// Sinov muddatini yakunlash — xulosa va yozuvsiz qolganlarni tasdiqlash ham.
    /// </summary>
    public const string EmploymentProbationComplete = "employment.probation.complete";

    /// <summary>Mehnat yo'li yozuvidagi terish xatosini tuzatish.</summary>
    public const string EmploymentCorrect = "employment.correct";

    /// <summary>
    /// Eski yagona mehnat ruxsati — FAQAT ko'chirish uchun.
    /// </summary>
    /// <remarks>
    /// <see cref="All"/> da YO'Q: hech bir endpoint uni talab qilmaydi va
    /// rolga uni qayta berib bo'lmaydi (<see cref="Role.Grant"/> rad etadi).
    /// Ishlab turgan bazalardagi rollarda u hali bor — <c>IdentitySeeder</c>
    /// har ishga tushishda uni sakkizta amal kodiga almashtiradi, shunda
    /// hech kim deploy paytida bironta huquqini yo'qotmaydi
    /// (<see cref="SystemRoles.LegacyAdmin"/> bilan bir xil yondashuv).
    /// </remarks>
    public const string LegacyEmploymentManage = "employment.manage";

    /// <summary>
    /// Mehnat munosabatidagi barcha yozish amallari — eski
    /// <see cref="LegacyEmploymentManage"/> aynan shularga bo'lindi.
    /// </summary>
    /// <remarks>
    /// Bir joyda sanalgan, chunki uchta joy shu ro'yxatga tayanadi:
    /// ko'chirish (eski kod o'rniga shular beriladi), HR menejerining
    /// boshlang'ich ruxsatlari va rollar muharriridagi «amallar» qatori.
    /// Ro'yxatlar alohida yozilsa, yangi amal bir joyda unutilardi.
    /// </remarks>
    public static IReadOnlyList<string> EmploymentActions { get; } =
    [
        EmploymentHire,
        EmploymentTransfer,
        EmploymentDuty,
        EmploymentSalary,
        EmploymentTerminate,
        EmploymentProbationExtend,
        EmploymentProbationComplete,
        EmploymentCorrect,
    ];

    /// <summary>
    /// Oylik ish haqini ko'rish. ALOHIDA ruxsat — HR so'ragan talab:
    /// Excel'da oklad hammaga ochiq edi, tizimda esa cheklanadi.
    /// </summary>
    public const string SalaryView = "salary.view";

    /// <summary>
    /// Qo'shimcha vazifa HAQINI ko'rish va boshqarish — asosiy okladdan ALOHIDA.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Egasining qarori (2026-09-27): asosiy okladni HR xodimlari
    /// (<see cref="SalaryView"/>) ko'radi, qo'shimcha vazifa uchun to'lanadigan
    /// haqni esa hatto HR menejerlari ham ko'rmasligi mumkin — u odatda
    /// faqat super administrator va bosh rahbarga beriladi. Shu sababli
    /// qo'shimcha haq alohida kalit va u «Rollar» muharririda alohida belgilanadi.
    /// </para>
    /// <para>
    /// Ruxsatsiz foydalanuvchi vazifani KO'RADI (matni, sanasi) va haqsiz
    /// vazifa bera oladi, lekin summani ko'rmaydi, kirita olmaydi va haqli
    /// vazifani tugata olmaydi. Kartadagi «jami» ham unga ko'rinmaydi —
    /// jamidan okladni ayirib, qo'shimcha haqni hisoblab olish mumkin edi.
    /// </para>
    /// </remarks>
    public const string SalaryExtraView = "salary.extra.view";

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

    // ---------- Blankalar ----------

    /// <summary>
    /// «Blankalar» (docs/SPEC-blankalar.md §6.3): menyu, ro'yxatlar, blanka, PDF, fayllarni
    /// yuklab olish, xodim kartasidagi tab va AI vositalari.
    /// </summary>
    /// <remarks>
    /// O'z qadamini (xodimning o'zi, bo'lim boshlig'i) elektron imzolash uchun shu ruxsat
    /// yetarli — foydalanuvchining kartasi qadamdagi shaxsga bog'langan bo'lsa (§6.2).
    /// </remarks>
    public const string BlanksView = "blanks.view";

    /// <summary>Blanka ochish, to'ldirish, yuborish, fayl biriktirish, qo'lda imzo olish, o'z qoralamasini bekor qilish.</summary>
    public const string BlanksFill = "blanks.fill";

    /// <summary>Rollarni biriktirish, istalgan blankani bekor qilish, shablonni yoqish/o'chirish.</summary>
    public const string BlanksManage = "blanks.manage";

    /// <summary>Lavozim rollari qadamlarini elektron imzolash, qaytarish, rad etish (amal).</summary>
    public const string BlanksApprove = "blanks.approve";

    // ---------- Jarayonlar — «Mening ishlarim» (docs/SPEC-mening-ishlarim.md §9.2) ----------

    /// <summary>The three tabs (own tasks, board, «Kim kechikdi»), case detail and the MCP read tools.</summary>
    public const string ProcessesView = "processes.view";

    /// <summary>
    /// Manual «Bajarildi», «Kerak emas» (skippable / legacy), undo of one's own manual step within 24 h,
    /// starting a dismissal case; the user lands on «Mening ishlarim» after login.
    /// </summary>
    public const string ProcessesWork = "processes.work";

    /// <summary>Cancel / reopen cases, stage bindings, holidays, act on any manual step, process settings.</summary>
    public const string ProcessesManage = "processes.manage";

    /// <summary>Gate overrides (action) — together with the hr1 assignment rule (§8.4).</summary>
    public const string ProcessesOverride = "processes.override";

    // ---------- Davomat (verifikatsiya) ----------

    /// <summary>
    /// Davomat (3-bosqich): jonli ro'yxat («hozir kim binoda»), o'tishlar
    /// jurnali, tabel va davomat hisobotlari.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ALOHIDA ruxsat guruhi — <see cref="EmployeesView"/> ostiga
    /// qo'shilmagan. Sabab <see cref="IncentivesView"/> va
    /// <see cref="SafetyView"/> dagi bilan bir xil: bu butunlay boshqa
    /// toifadagi ma'lumot. Xodim kartasi odamning HOLATINI ko'rsatadi,
    /// davomat esa odam QAYERDA VA QACHON bo'lganini — daqiqama-daqiqa,
    /// terminal olgan surati bilan birga. Bu kuzatuv darajasidagi yozuv:
    /// xodimlar ro'yxatini ochgan har bir kishi «kim kecha soat 22:14 da
    /// chiqib ketdi» ni ham ko'rib turishi kerak emas.
    /// </para>
    /// <para>
    /// Bu ruxsat faqat O'QIYDI: jonli ro'yxat, o'tishlar jurnali (surat
    /// bilan), tabel va kechikish/erta ketish/kelmaganlar hisoboti. Excel'ga
    /// chiqarish esa <see cref="ReportsExport"/> ostida qoladi — chiqarish
    /// bino tashqarisiga fayl bo'lib ketadigan og'ir amal va tizimda unga
    /// atalgan ruxsat allaqachon bor.
    /// </para>
    /// </remarks>
    public const string AttendanceView = "attendance.view";

    /// <summary>
    /// Davomatning ISHLASHINI boshqarish: qurilmalar, ish jadvallari va
    /// ularning biriktirmalari, terminal shaxslarini xodimga moslashtirish,
    /// import va qayta hisoblash.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="AttendanceView"/> dan farqi shakliy emas: bu yerdagi har
    /// bir amal RAQAMLARNING MA'NOSINI o'zgartiradi. Jadval yoki moslik
    /// almashsa, kechagi tabel ham boshqacha bo'lib qoladi — hisobot esa
    /// tashqi ko'rinishda o'sha-o'sha turaveradi. Tabelning o'zi qo'lda
    /// tahrirlanmaydi (bunday endpoint umuman yo'q): noto'g'ri kun MANBADA
    /// tuzatilib, qayta hisoblanadi.
    /// </para>
    /// <para>
    /// Qurilma va moslik ro'yxatlari O'QISH uchun ham aynan shu ruxsat
    /// ostida. Sabab: qurilma kartasida token boshlanishi, IP ro'yxati va
    /// binoning kirish nuqtalari turadi, moslik ro'yxati esa terminaldagi
    /// raqamdan aniq ismga olib boradigan xarita. Ikkalasi ham faqat
    /// ko'rish uchun kirgan odamga kerak emas.
    /// </para>
    /// </remarks>
    public const string AttendanceManage = "attendance.manage";

    /// <summary>
    /// Yo'qlik so'rovlari bo'yicha QAROR qabul qilish: yaratish, tahrirlash,
    /// tasdiqlash, rad etish va bekor qilish (SPEC-so'rovlar §B4).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega <see cref="AttendanceManage"/> ga qo'shilmadi.</b> U qurilma,
    /// jadval va moslik — TIZIMNI sozlash. Bu esa kadrlar qarori: «bu odamning
    /// kelmagani oqlanadimi». Ikkalasi bir odamda bo'lishi shart emas va
    /// ko'pincha bo'lmaydi ham: qurilmalarni texnik xodim, so'rovlarni HR
    /// yuritadi.
    /// </para>
    /// <para>
    /// So'rovlarni O'QISH esa <see cref="AttendanceView"/> ostida: ular
    /// davomat ekranlarining bir qismi va ularsiz tabeldagi «Sababli» kun
    /// tushunarsiz bo'lib qolardi.
    /// </para>
    /// <para>
    /// ⭐ <b>Jadvalni o'zgartirish so'rovlari ham shu ruxsat ostida</b>
    /// (SPEC-verifikatsiya-jadval-sorovi §C3). Tasdiqlangan so'rov bitta xodimning
    /// bitta sanadagi rejasini almashtiradi — bu ham kadrlar qarori, jadvalning
    /// o'zi (hamma biriktirilganlar uchun) o'zgarmaydi. Ikki xil so'rov HR'da bitta
    /// navbatda turadi; ularni ikki ruxsatga bo'lish navbatning yarmini qarorsiz
    /// qoldirardi.
    /// </para>
    /// </remarks>
    public const string AttendanceRequestsManage = "attendance.requests.manage";

    /// <summary>
    /// Marker, not a grant: the holder works ONLY in the attendance section and ONLY for
    /// the staff of the departments assigned to them (<c>user_attendance_scopes</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A user is scoped when they hold this marker AND NOT <see cref="AttendanceManage"/>.
    /// The second half matters because <c>super_admin</c> holds all of <see cref="All"/>
    /// and a new database's <c>main_boss</c> nearly all of it, so both carry the marker
    /// while staying unrestricted.
    /// </para>
    /// <para>
    /// No endpoint requires it. It narrows what a user reaches, which is why
    /// <c>UserRoleRules</c> ignores it when comparing role breadth, and why
    /// <see cref="DropAttendanceMarkerIfManaging"/> hides it from the lists a client sees
    /// when it would mean nothing.
    /// </para>
    /// </remarks>
    public const string AttendanceScoped = "attendance.scoped";

    // ---------- Ma'lumotnomalar ----------
    public const string CatalogView = "catalog.view";
    public const string CatalogManage = "catalog.manage";

    // ---------- Hisobotlar ----------
    public const string ReportsView = "reports.view";
    public const string ReportsExport = "reports.export";

    // ---------- Tizim ----------
    public const string UsersManage = "users.manage";

    /// <summary>
    /// «Rollar» menyusi: rol yaratish, nomini va ruxsatlarini o'zgartirish, o'chirish.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega <see cref="UsersManage"/> dan alohida.</b> Foydalanuvchi
    /// boshqaruvi TAYYOR rolni odamga beradi, bu ruxsat esa rolning O'ZI
    /// nima qila olishini belgilaydi — ya'ni tizimdagi eng kuchli huquq.
    /// Egasining qarori: sukut bo'yicha u faqat super administratorda,
    /// bosh rahbarda ham yo'q; kerak bo'lsa egasi uni o'zi beradi.
    /// </para>
    /// <para>
    /// Ruxsati bor odam ham O'ZIDA yo'q ruxsatni boshqa rolga bera olmaydi
    /// va o'z rolini o'zgartira olmaydi — bu cheklovlar use-case'da
    /// (<c>rol.ortiqcha_ruxsat</c>, <c>rol.oz_roli</c>).
    /// </para>
    /// </remarks>
    public const string RolesManage = "roles.manage";

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

    /// <summary>
    /// AI yordamchini (Claude, ChatGPT…) MCP orqali ulash va tizim
    /// ma'lumotini FAQAT O'QISH (<c>docs/SPEC-mcp.md</c>, egasining qarori
    /// 2026-10-01).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Bu ruxsat o'zi hech qanday ma'lumot OCHMAYDI.</b> U faqat «bu odam
    /// AI mijozga kalit bera oladi» degani. AI ko'radigan narsa — o'sha
    /// odamning O'Z ruxsatlari: oklad faqat <see cref="SalaryView"/> bilan,
    /// davomat faqat <see cref="AttendanceView"/> bilan va hokazo. Pasport
    /// ma'lumoti va yuklangan fayllar esa hech kimga, hech qanday ruxsat
    /// bilan chiqmaydi — ular baza darajasida yopilgan.
    /// </para>
    /// <para>
    /// <b>Nega alohida ruxsat.</b> AI orqali o'qish — ma'lumotni tizimdan
    /// TASHQARIGA (AI provayderiga) olib chiqish. Ekranda ko'rish huquqi bor
    /// har bir odam bunga ham haqli bo'lishi shart emas; egasi kimga
    /// berishni «Rollar» da o'zi belgilaydi. HR menejerining boshlang'ich
    /// ruxsatlarida YO'Q.
    /// </para>
    /// </remarks>
    public const string McpConnect = "mcp.connect";

    /// <summary>Barcha ruxsatlar — tekshirish va seed uchun.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        EmployeesView, EmployeesCreate, EmployeesEdit, EmployeesDelete, EmployeesPersonalView,
        EmploymentView,
        EmploymentHire, EmploymentTransfer, EmploymentDuty, EmploymentSalary,
        EmploymentTerminate, EmploymentProbationExtend, EmploymentProbationComplete, EmploymentCorrect,
        SalaryView, SalaryExtraView,
        ListsView, ListsManage,
        IncentivesView, IncentivesManage,
        FeedbackView, FeedbackManage,
        SafetyView, SafetyManage,
        RecruitingView, RecruitingManage,
        BlanksView, BlanksFill, BlanksManage, BlanksApprove,
        ProcessesView, ProcessesWork, ProcessesManage, ProcessesOverride,
        AttendanceView, AttendanceManage, AttendanceRequestsManage, AttendanceScoped,
        CatalogView, CatalogManage,
        ReportsView, ReportsExport,
        UsersManage, RolesManage, AuditView, SettingsManage,
        McpConnect,
    };

    public static bool Exists(string permission) => All.Contains(permission);

    /// <summary>
    /// Removes <see cref="AttendanceScoped"/> from a user's effective permissions when
    /// <see cref="AttendanceManage"/> makes it meaningless, so that <c>/api/auth/me</c>
    /// does not tell a client that the owner is scoped.
    /// </summary>
    /// <param name="permissions">The union of the user's role permissions; changed in place.</param>
    public static void DropAttendanceMarkerIfManaging(ISet<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        if (permissions.Contains(AttendanceManage))
        {
            permissions.Remove(AttendanceScoped);
        }
    }
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
/// <para>
/// <b>2026-09-26 dan: rollarni egasi O'ZI belgilaydi</b> («Rollar» menyusi).
/// U yangi rol yaratadi, nomini va ruxsatlarini o'zgartiradi, ishlatilmayotgan
/// rolni o'chiradi — deploy va qayta ishga tushirishsiz. Shu sababli
/// <see cref="MainBoss"/> va <see cref="HrManager"/> endi ODDIY rollar:
/// seeder ularni faqat yangi bazada yaratadi va keyin hech qachon
/// «to'g'rilamaydi». Himoyalangani faqat <see cref="Protected"/> dagilar.
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
    /// «Davomat mas'uli»: factory staff who work only in the attendance section, only for
    /// their own factory's staff, and may add or delete manual passes.
    /// </summary>
    /// <remarks>
    /// Which departments they see is stored per user (<c>user_attendance_scopes</c>); with
    /// no assignment they see nothing. The marker <see cref="Permissions.AttendanceScoped"/>
    /// is what restricts them, the guard middleware then closes every other route.
    /// </remarks>
    public const string AttendanceResponsible = "attendance_responsible";

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
    /// Rahbariyat rollari — «oxirgi rahbar» qoidasi shularni sanaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ilgari bu «ulardan ruxsat olib tashlab bo'lmaydi» degan ro'yxat ham
    /// edi. Rollar muharriri paydo bo'lgach, bosh rahbar roli oddiy rolga
    /// aylandi va uning ruxsatlarini egasi o'zgartira oladi; qulflangan
    /// yagona rol — super administrator (<see cref="Protected"/>).
    /// </para>
    /// <para>
    /// Ro'yxat baribir qoldi: tizimda foydalanuvchi boshqaruviga qodir
    /// kamida bitta faol hisob qolishi kerak va u shu ikki rol egalari
    /// orasidan sanaladi (<c>UserRoleRules.EnsureManagerRemains</c>).
    /// </para>
    /// </remarks>
    public static readonly IReadOnlySet<string> FullAccess =
        new HashSet<string>(StringComparer.Ordinal) { SuperAdmin, MainBoss };

    /// <summary>
    /// Himoyalangan rollar — rollar muharriri ularni o'zgartirmaydi va o'chirmaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="SuperAdmin"/></b> — tizim egasi. Uning ruxsatlari
    /// qo'lda emas, seeder tomonidan har ishga tushishda
    /// <see cref="Permissions.All"/> ga tenglanadi — kelajakdagi yangi
    /// ruxsat ham unga o'zi yetib boradi.
    /// </para>
    /// <para>
    /// <b><see cref="RecruitingBot"/></b> — odam emas, xizmat hisobi. Uning
    /// huquqlari ataylab tor (sababi <see cref="RecruitingBot"/> izohida) va
    /// rollar muharririda umuman ko'rinmaydi: egasiga u haqda qaror qilish
    /// kerak emas.
    /// </para>
    /// <para>
    /// Himoya KOD bo'yicha, bazadagi bayroq bo'yicha emas: bazani qo'lda
    /// tahrirlash rolni «ochib» qo'ya olmasligi kerak.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlySet<string> Protected =
        new HashSet<string>(StringComparer.Ordinal) { SuperAdmin, RecruitingBot };

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

    /// <summary>
    /// Ordinary roles the seeder creates on EVERY start if they are missing, never only on
    /// the first one, and whose permissions it then leaves alone.
    /// </summary>
    /// <remarks>
    /// <see cref="MainBoss"/> and <see cref="HrManager"/> are created on a new database only,
    /// so a role added to the code later would never reach production that way. It cannot be
    /// <see cref="Protected"/> either: only the super administrator may touch a protected
    /// role's holders, and the bosh rahbar has to be able to assign this one. The price is
    /// that a role deleted by the owner returns on the next start, without holders.
    /// </remarks>
    public static readonly IReadOnlySet<string> CreatedWhenMissing =
        new HashSet<string>(StringComparer.Ordinal) { AttendanceResponsible };

    public static IReadOnlyDictionary<string, (string Name, string[] Permissions)> Defaults { get; } =
        new Dictionary<string, (string, string[])>(StringComparer.Ordinal)
        {
            [SuperAdmin] = ("Super administrator", [.. Permissions.All]),

            /*
              Bosh rahbarda `settings.manage` va `roles.manage` dan TASHQARI
              hamma ruxsat bor.

              Ilgari ikkala rol ham `Permissions.All` ni olardi va izohda
              «ruxsatlar bo'yicha AYNAN bir xil» deb yozilgandi. Keyin
              istisnolar paydo bo'ldi — tizim sozlamalari (sabab
              `Permissions.SettingsManage` izohida) va rollarni
              o'zgartirish (sabab `Permissions.RolesManage` izohida).
              Ro'yxat baribir qo'lda sanab yozilmaydi.

              ⚠️ Bu qiymat faqat YANGI bazada ishlatiladi: bosh rahbar
              endi oddiy rol va seeder mavjudiga tegmaydi. Ya'ni kodga
              yangi ruxsat qo'shilsa, u ishlab turgan bazadagi bosh
              rahbarga O'ZI yetib bormaydi — uni egasi «Rollar» menyusidan
              beradi (jimgina berilgan huquq — egasi bilmagan huquq).
            */
            [MainBoss] = ("Bosh rahbar",
                [.. Permissions.All.Where(p => p != Permissions.SettingsManage && p != Permissions.RolesManage)]),

            [HrManager] = ("HR menejeri",
            [
                Permissions.EmployeesView, Permissions.EmployeesCreate,
                Permissions.EmployeesEdit, Permissions.EmployeesDelete,
                Permissions.EmploymentView,

                // Eski `employment.manage` ning o'rni — sakkizala amal ham.
                // Bo'linishni (masalan okladni faqat «Kadr bo'limi
                // boshlig'i» qiladi) egasi o'zi, muharrirdan qiladi.
                .. Permissions.EmploymentActions,

                Permissions.SalaryView,
                Permissions.ListsView, Permissions.ListsManage,
                Permissions.IncentivesView, Permissions.IncentivesManage,
                Permissions.FeedbackView, Permissions.FeedbackManage,
                Permissions.SafetyView, Permissions.SafetyManage,
                Permissions.RecruitingView, Permissions.RecruitingManage,

                // Blankalar (SPEC-blankalar §6.3): HR to'ldiradi va imzolaydi; rollarni
                // biriktirish va boshqaning blankasini bekor qilish (`blanks.manage`) — egasi beradi.
                Permissions.BlanksView, Permissions.BlanksFill, Permissions.BlanksApprove,

                // Jarayonlar (SPEC-mening-ishlarim §9.2): HR runs the cases; gate overrides
                // (`processes.override`) are the owner's to grant.
                Permissions.ProcessesView, Permissions.ProcessesWork, Permissions.ProcessesManage,

                /*
                  Davomat IKKALA ruxsati ham HR menejerida — ataylab
                  juft. Tabelni o'qiydigan odam aynan o'sha tabelni
                  buzgan moslikni ham tuzatadi (`safety.*` va
                  `recruiting.*` bilan bir xil fikr). Ajratilsa, egasi
                  aniqlanmagan har bir o'tish rahbarning vaqtini kutib
                  turardi.
                */
                Permissions.AttendanceView, Permissions.AttendanceManage, Permissions.AttendanceRequestsManage,

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

            [AttendanceResponsible] = ("Davomat mas'uli",
            [
                Permissions.AttendanceView,
                Permissions.AttendanceRequestsManage,
                Permissions.ReportsExport,
                Permissions.AttendanceScoped,
            ]),
        };
}
