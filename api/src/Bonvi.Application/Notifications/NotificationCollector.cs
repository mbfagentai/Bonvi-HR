using System.Globalization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Notifications.Abstractions;
using Bonvi.Application.Notifications.Dtos;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Notifications;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Notifications;

/// <summary>
/// Bildirishnoma parametrlarining nomlari.
/// </summary>
/// <remarks>
/// <para>
/// Konstanta, chunki bu satrlar SHARTNOMA: frontend ularni matn
/// shablonlarida ishlatadi (<c>«{employeeName} sinovi {daysLeft} kundan
/// keyin tugaydi»</c>). Kodda ular ikki joyda uchraydi — bu yerda va
/// testlarda; imlo xatosi kompilyatorda emas, faqat ekranda ko'rinardi.
/// </para>
/// <para>
/// <b>Nega «days» degan umumiy nom yo'q.</b> Uch xil turda kun soni uch
/// xil ma'no beradi: qolgan kunlar, o'tib ketgan kunlar, ishlagan kunlar.
/// Bitta nom ostida tursa, frontend qaysi turda qaysi ma'no ekanini
/// yodda tutishga majbur bo'lardi — va bir kun «-5 kun qoldi» degan matn
/// chiqardi.
/// </para>
/// </remarks>
public static class NotificationParams
{
    /// <summary>Xodimning to'liq F.I.Sh.</summary>
    public const string EmployeeName = "employeeName";

    /// <summary>Nomzodning to'liq F.I.Sh.</summary>
    public const string CandidateName = "candidateName";

    /// <summary>Bo'lim nomi. Bo'lim ko'rsatilmagan bo'lsa — parametr UMUMAN bo'lmaydi.</summary>
    public const string DepartmentName = "departmentName";

    /// <summary>Lavozim nomi. Ko'rsatilmagan bo'lsa — parametr umuman bo'lmaydi.</summary>
    public const string PositionName = "positionName";

    /// <summary>Vakansiya (e'lon) nomi. Ko'rsatilmagan bo'lsa — parametr umuman bo'lmaydi.</summary>
    public const string VacancyTitle = "vacancyTitle";

    /// <summary>Sinov muddatining tugash sanasi (ISO).</summary>
    public const string EndDate = "endDate";

    /// <summary>Ishga kirish sanasi (ISO).</summary>
    public const string HireDate = "hireDate";

    /// <summary>Tug'ilgan sana (ISO) — yil bilan birga.</summary>
    public const string BirthDate = "birthDate";

    /// <summary>Nomzod qo'shilgan sana (ISO).</summary>
    public const string AddedOn = "addedOn";

    /// <summary>Taklifga javob kutiladigan oxirgi sana (ISO).</summary>
    public const string ExpiresAt = "expiresAt";

    /// <summary>Muddat tugashiga qolgan kunlar — musbat son.</summary>
    public const string DaysLeft = "daysLeft";

    /// <summary>Muddat tugaganidan beri o'tgan kunlar — musbat son.</summary>
    public const string DaysOverdue = "daysOverdue";

    /// <summary>Sinov yozuvisiz ishlab yurgan kunlar — musbat son.</summary>
    public const string DaysWorked = "daysWorked";

    /// <summary>Nechchi yosh to'ldi. Tug'ilgan sana shubhali bo'lsa — parametr bo'lmaydi.</summary>
    public const string Age = "age";

    /// <summary>Nomzod manbai: <c>Manual</c> yoki <c>TelegramBot</c>.</summary>
    public const string Source = "source";

    /// <summary>Taklif qilingan oklad miqdori. FAQAT «salary.view» ruxsati bilan.</summary>
    public const string SalaryAmount = "salaryAmount";

    /// <summary>Oklad valyutasi (ISO 4217). FAQAT «salary.view» ruxsati bilan.</summary>
    public const string SalaryCurrency = "salaryCurrency";
}

/// <summary>
/// Joriy shartlarni tekshirib, foydalanuvchi ko'rishi kerak bo'lgan
/// bildirishnomalarni yig'adi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bu sinf hech narsani saqlamaydi.</b> U faqat hisoblaydi: bazadan
/// shartlarni o'qiydi va ularni bildirishnomaga aylantiradi. «O'qildi»
/// va «o'chirildi» holati keyingi qadamda — <c>ListNotificationsQuery</c>
/// da — ustiga qo'yiladi. Ajratish ataylab: yig'ish qoidasi
/// (qaysi shart → qaysi tur) foydalanuvchi holatidan mustaqil va uni
/// alohida test qilish mumkin.
/// </para>
/// <para>
/// <b>Ruxsat filtri MANBA darajasida.</b> Har bir bo'lak so'rov
/// yuborishdan OLDIN tekshiriladi: <c>recruiting.view</c> yo'q bo'lsa,
/// nomzodlar so'rovi umuman yuborilmaydi. Natijani keyin filtrlash ham
/// ishlardi, lekin u holda ruxsatsiz odam uchun ham baza ishlab turardi
/// va yangi tur qo'shgan odam filtr qo'shishni unutishi mumkin edi.
/// Bu yerda unutish qiyin: ruxsat tekshiruvisiz so'rov yozib bo'lmaydi.
/// </para>
/// <para>
/// Tekshiruv <see cref="Allowed"/> orqali va u ruxsat kodini O'ZI
/// tanlamaydi — <c>NotificationAccess.RequiredPermission</c> dan oladi.
/// Ya'ni «qaysi tur qaysi ruxsat ostida» degan javob butun tizimda BITTA
/// joyda turadi va u yerda yangi tur uchun qiymat yozilmasa, kod
/// ishlaydigan holatda umuman yig'ilmaydi.
/// </para>
/// <para>
/// <b>Chegaralar SOZLAMADAN olinadi</b>, <c>NotificationRules</c> dagi
/// konstantalardan emas. Konstantalar yo'qolmadi — ular endi SUKUT
/// qiymat: sozlama hech qachon o'zgartirilmagan bo'lsa, aynan o'sha
/// son ishlaydi (<c>SystemSettingKeys</c> izohi). Sozlama esa bitta
/// joyda — so'rov boshida bir marta o'qiladi va shu yig'ilishning
/// hamma bo'lagiga BIR XIL qiymat bo'lib tarqaladi. Har bir bo'lak
/// o'zi o'qiganda, yarim tunda sozlama o'zgarsa, bitta ro'yxat ikki
/// xil chegara bilan yig'ilib qolishi mumkin edi.
/// </para>
/// </remarks>
public sealed class NotificationCollector
{
    private readonly INotificationReadModel _source;
    private readonly IEmployeeReadModel _employees;
    private readonly SystemSettingsReader _settings;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public NotificationCollector(
        INotificationReadModel source,
        IEmployeeReadModel employees,
        SystemSettingsReader settings,
        ICurrentUser user,
        IClock clock)
    {
        _source = source;
        _employees = employees;
        _settings = settings;
        _user = user;
        _clock = clock;
    }

    /// <summary>
    /// Foydalanuvchi ko'rishi mumkin bo'lgan barcha bildirishnomalar.
    /// </summary>
    /// <remarks>
    /// O'chirilganlar bu yerda hali chiqarib tashlanmaydi: bu sinf
    /// foydalanuvchi holatini umuman bilmaydi. Tartiblash esa shu
    /// yerda — u ma'lumotning o'zidan kelib chiqadi va holatga bog'liq
    /// emas.
    /// </remarks>
    public async Task<IReadOnlyList<NotificationDto>> CollectAsync(
        CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;

        // Sozlamalar BIR MARTA o'qiladi va bo'laklarga uzatiladi: butun
        // ro'yxat bitta va o'sha chegaralar bo'yicha yig'iladi (sabab
        // sinf izohida). O'quvchining o'zi ham keshlaydi, ya'ni
        // «hammasini o'qildi» dagi ikkinchi yig'ilish qo'shimcha so'rov
        // yubormaydi.
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);

        var items = new List<NotificationDto>();

        await AddProbationDeadlinesAsync(items, today, settings, cancellationToken).ConfigureAwait(false);
        await AddMissingProbationsAsync(items, cancellationToken).ConfigureAwait(false);
        await AddBirthdaysAsync(items, today, cancellationToken).ConfigureAwait(false);
        await AddNewCandidatesAsync(items, today, settings, cancellationToken).ConfigureAwait(false);
        await AddOfferDeadlinesAsync(items, today, settings, cancellationToken).ConfigureAwait(false);

        return
        [
            .. items
                /*
                  VAQT bo'yicha, yangisi tepada (foydalanuvchi qarori).

                  Ilgari birinchi mezon MUHIMLIK edi va u ro'yxatni «nima
                  birinchi navbatda kerak» tartibida chizardi. Amalda esa
                  bu bitta kamchilik tug'dirdi: bugun tushgan yangi yozuv
                  bir oy oldingi muddatlar ostiga tushib ketardi va uni
                  ko'rish uchun ro'yxatni aylantirish kerak bo'lardi.
                  Bildirishnoma esa avvalo «NIMA YANGI?» degan savolga
                  javob beradi.

                  Muhimlik YO'QOLMADI — u endi RANG bilan aytiladi:
                  o'qilmagan qator o'z darajasining fonida chiziladi
                  (`notification-row.tsx`). Ya'ni jiddiy yozuv ro'yxatning
                  o'rtasida turgan bo'lsa ham ko'zga darhol tashlanadi.
                */
                /*
                  Avval SODIR BO'LGANI, keyin KELAYOTGANI.

                  Bildirishnomaning sanasi ikki xil ma'noni tashiydi:
                  «bu bugun sodir bo'ldi» (bot arizasi, tug'ilgan kun) va
                  «bu falon kuni tugaydi» (sinov muddati, taklif). Ularni
                  bitta o'qqa qo'yib, sanasi kattasini tepaga chiqarish
                  KELAJAKDAGI muddatni bugun sodir bo'lgan hodisadan
                  yuqoriga ko'tarardi: «sinov 5-sentabrda tugaydi» degan
                  yozuv «botdan ariza tushdi» ustida turardi, holbuki
                  birinchisi hali bir hafta kutadi, ikkinchisi esa hozir
                  javob kutyapti.
                */
                .OrderBy(item => item.Date > today)

                // Sodir bo'lganlar: YANGISI tepada — «nima yangi?» degan
                // savolga javob. Kelayotganlar: YAQINI tepada — «nimaga
                // ulgurish kerak?» degan savolga. Ikkalasi ham vaqt
                // bo'yicha, faqat yo'nalishi guruhning ma'nosiga qarab.
                .ThenBy(item => item.Date > today ? item.Date.DayNumber : -item.Date.DayNumber)

                // Bir XIL kundagi yozuvlarda muhimlik ajratadi.
                .ThenByDescending(item => item.Severity)
                // Oxirgi mezon — kalit. U barqaror va noyob, ya'ni bir xil
                // ma'lumotda tartib HAR DOIM bir xil chiqadi. Bu bo'lmasa,
                // teng qiymatlarda ro'yxat so'rovdan so'rovga «sakrab»
                // turardi va foydalanuvchi qaysi qatorni bosayotganini
                // bilmasdi.
                .ThenBy(item => item.Key, StringComparer.Ordinal),
        ];
    }

    /// <summary>Foydalanuvchi shu turdagi bildirishnomani ko'ra oladimi.</summary>
    private bool Allowed(NotificationType type)
        => _user.HasPermission(NotificationAccess.RequiredPermission(type));

    // ════════════════════════════════════════════════════════════
    //  Sinov muddati: tugayapti / bugun tugaydi / o'tib ketgan
    // ════════════════════════════════════════════════════════════

    private async Task AddProbationDeadlinesAsync(
        List<NotificationDto> items,
        DateOnly today,
        SystemSettings settings,
        CancellationToken cancellationToken)
    {
        // Uchala tur ham bitta ruxsat ostida — bittasini tekshirish yetadi.
        if (!Allowed(NotificationType.ProbationEndingSoon))
        {
            return;
        }

        var until = today.AddDays(settings.Number(SystemSettingKeys.ProbationWarningDays));
        var rows = await _source
            .ListProbationDeadlinesAsync(until, cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in rows)
        {
            var daysLeft = row.EndDate.DayNumber - today.DayNumber;

            // Tur FAQAT sanalar nisbatidan aniqlanadi — bazada «bu qanday
            // ogohlantirish» degan ustun yo'q va bo'lishi ham kerak emas:
            // u har kuni o'zgaradi.
            var (type, severity) = daysLeft switch
            {
                < 0 => (NotificationType.ProbationOverdue, NotificationSeverity.Critical),
                0 => (NotificationType.ProbationEndsToday, NotificationSeverity.Warning),
                _ => (NotificationType.ProbationEndingSoon, NotificationSeverity.Warning),
            };

            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [NotificationParams.EmployeeName] = row.EmployeeName,
                [NotificationParams.EndDate] = Iso(row.EndDate),
            };

            AddIfPresent(parameters, NotificationParams.DepartmentName, row.DepartmentName);
            AddIfPresent(parameters, NotificationParams.PositionName, row.PositionName);

            if (daysLeft < 0)
            {
                // Musbat son bo'lib beriladi: matnda «5 kun oldin tugagan»
                // deyiladi, «-5 kun» emas. Ishorani matn tashiydi, son emas.
                parameters[NotificationParams.DaysOverdue] = Number(-daysLeft);
            }
            else
            {
                // Nol ham beriladi («bugun tugaydi»). Qoida taklif muddati
                // bilan AYNAN bir xil: qolgan kun manfiy bo'lmasa —
                // «daysLeft», bo'lsa — «daysOverdue». Ikkita joyda ikki xil
                // shart yozilsa, frontend har bir tur uchun alohida
                // istisnoni yodda tutishi kerak bo'lardi.
                parameters[NotificationParams.DaysLeft] = Number(daysLeft);
            }

            items.Add(new NotificationDto(
                NotificationKey.For(type, row.ProbationId, row.EndDate),
                type,
                severity,
                row.EndDate,
                parameters,
                new NotificationLinkDto(NotificationRoute.Probations, row.ProbationId)));
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Sinov muddati ochilmagan munosabatlar (ETL qoldig'i)
    // ════════════════════════════════════════════════════════════

    private async Task AddMissingProbationsAsync(
        List<NotificationDto> items,
        CancellationToken cancellationToken)
    {
        if (!Allowed(NotificationType.ProbationMissing))
        {
            return;
        }

        // MAVJUD o'qish modeli ishlatiladi: «sinovsiz qolgan munosabat»
        // ta'rifi bitta joyda turishi kerak (sabab `INotificationReadModel`
        // izohida).
        var rows = await _employees
            .ListMissingProbationsAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in rows)
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [NotificationParams.EmployeeName] = row.EmployeeName,
                [NotificationParams.HireDate] = Iso(row.HireDate),
                [NotificationParams.DaysWorked] = Number(Math.Max(row.WorkedDays, 0)),
            };

            AddIfPresent(parameters, NotificationParams.DepartmentName, row.DepartmentName);
            AddIfPresent(parameters, NotificationParams.PositionName, row.PositionName);

            items.Add(new NotificationDto(
                // Kalitda SANA yo'q: bu yerda o'zgaradigan sana umuman
                // yo'q — munosabat yoki sinovsiz turibdi, yoki HR uni
                // yopgan va shart butunlay yo'qoladi.
                NotificationKey.For(NotificationType.ProbationMissing, row.EmploymentId),
                NotificationType.ProbationMissing,
                // «Diqqat», «jiddiy» emas: bu Excel'dan qolgan tozalash ishi,
                // muddati o'tib ketgan sinovdek shoshilinch emas.
                NotificationSeverity.Warning,
                row.HireDate,
                parameters,
                new NotificationLinkDto(NotificationRoute.MissingProbations, row.EmploymentId)));
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Tug'ilgan kunlar — HAR BIR XODIM ALOHIDA
    // ════════════════════════════════════════════════════════════

    private async Task AddBirthdaysAsync(
        List<NotificationDto> items,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (!Allowed(NotificationType.BirthdayToday))
        {
            return;
        }

        var tomorrow = today.AddDays(1);
        var rows = await _source
            .ListBirthdaysAsync(today, tomorrow, cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in rows)
        {
            // Qator qaysi kunga tegishli ekani sanadan aniqlanadi. Bitta
            // so'rovda ikki kun kelgani uchun bu shart shu yerda kerak —
            // 29-fevralda tug'ilganlar kabisa bo'lmagan yilda umuman
            // chiqmaydi va bu ataylab: sun'iy sana tanlash (28-fevral yoki
            // 1-mart) tizimning qarori bo'lib qolardi, HR ning emas.
            var isToday = row.BirthDate.Month == today.Month && row.BirthDate.Day == today.Day;
            var occursOn = isToday ? today : tomorrow;

            var type = isToday ? NotificationType.BirthdayToday : NotificationType.BirthdayTomorrow;

            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [NotificationParams.EmployeeName] = row.EmployeeName,
                [NotificationParams.BirthDate] = Iso(row.BirthDate),
            };

            var age = occursOn.Year - row.BirthDate.Year;
            if (age is > 0 and < 120)
            {
                // Shubhali sanada (kelajak yoki 120 yildan oshgan) yosh
                // umuman berilmaydi: Excel'dan ko'chirilgan yozuvlarda
                // bunday qiymatlar uchraydi va «136 yoshga to'ldi» degan
                // tabrik ma'lumotga bo'lgan ishonchni yo'qotardi.
                parameters[NotificationParams.Age] = Number(age);
            }

            items.Add(new NotificationDto(
                // ⚠️ Turi kalitning PREFIKSIDA turgani uchun «bugun» va
                // «ertaga» kalitlari bir xil xodim va bir xil sanada ham
                // FARQ QILADI. Bu majburiy: ertangi tug'ilgan kunni bugun
                // o'qigan HR ertaga kelib, xuddi shu odam haqidagi
                // «bugun» bildirishnomasini yangi holida ko'rishi kerak.
                // Prefikssiz kalitda ikkalasi bitta yozuvga tushib qolardi.
                NotificationKey.For(type, row.EmployeeId, occursOn),
                type,
                NotificationSeverity.Info,
                occursOn,
                parameters,
                new NotificationLinkDto(NotificationRoute.Employee, row.EmployeeId)));
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Rekruting: yangi nomzod va taklif muddati
    // ════════════════════════════════════════════════════════════

    private async Task AddNewCandidatesAsync(
        List<NotificationDto> items,
        DateOnly today,
        SystemSettings settings,
        CancellationToken cancellationToken)
    {
        if (!Allowed(NotificationType.CandidateNew))
        {
            return;
        }

        var since = today.AddDays(-settings.Number(SystemSettingKeys.NewCandidateDays));
        var rows = await _source
            .ListNewCandidatesAsync(since, cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in rows)
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [NotificationParams.CandidateName] = row.CandidateName,
                [NotificationParams.AddedOn] = Iso(row.AddedOn),

                // Manba SANOQ NOMI bo'lib ketadi («Manual», «TelegramBot»),
                // tarjima qilingan matn emas — DTO'dagi boshqa sanoqlar
                // bilan bir xil qoida.
                [NotificationParams.Source] = row.Source.ToString(),
            };

            AddIfPresent(parameters, NotificationParams.VacancyTitle, row.VacancyTitle);

            /*
              Manba TURNI ham, DARAJANI ham belgilaydi.

              Qo'lda kiritilgan nomzodni HR ning o'zi yozgan — u xabardor,
              shu sababli ko'k eslatma. Botdan kelgan ariza esa hech kim
              qaramayotgan paytda tushadi (kechasi, dam olish kunida) va
              odam javob kutib turadi — shu sababli sariq va ro'yxatning
              tepasida (sabab `NotificationType.CandidateFromBot` izohida).
            */
            var bot = row.Source == CandidateSource.TelegramBot;

            var type = bot ? NotificationType.CandidateFromBot : NotificationType.CandidateNew;
            var severity = bot ? NotificationSeverity.Warning : NotificationSeverity.Info;

            items.Add(new NotificationDto(
                NotificationKey.For(type, row.CandidateId),
                type,
                severity,
                row.AddedOn,
                parameters,
                new NotificationLinkDto(NotificationRoute.Candidate, row.CandidateId)));
        }
    }

    private async Task AddOfferDeadlinesAsync(
        List<NotificationDto> items,
        DateOnly today,
        SystemSettings settings,
        CancellationToken cancellationToken)
    {
        if (!Allowed(NotificationType.JobOfferExpiring))
        {
            return;
        }

        // Oklad ALOHIDA ruxsat ostida (`salary.view`) — rekrutingni
        // ko'rish huquqi taklif summasini ko'rish huquqini bermaydi.
        // Bu xodimlar ro'yxatidagi bilan aynan bir xil qoida
        // (`SalaryAccess`), shu sababli o'sha yerdagi kengaytma
        // ishlatiladi — ikkinchi ta'rif yozilmaydi.
        var canSeeSalary = _user.CanSeeSalary();

        var until = today.AddDays(settings.Number(SystemSettingKeys.OfferWarningDays));
        var rows = await _source
            .ListOfferDeadlinesAsync(until, cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in rows)
        {
            var daysLeft = row.ExpiresAt.DayNumber - today.DayNumber;

            var (type, severity) = daysLeft < 0
                ? (NotificationType.JobOfferExpired, NotificationSeverity.Critical)
                : (NotificationType.JobOfferExpiring, NotificationSeverity.Warning);

            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [NotificationParams.CandidateName] = row.CandidateName,
                [NotificationParams.ExpiresAt] = Iso(row.ExpiresAt),
            };

            AddIfPresent(parameters, NotificationParams.VacancyTitle, row.VacancyTitle);

            if (daysLeft < 0)
            {
                parameters[NotificationParams.DaysOverdue] = Number(-daysLeft);
            }
            else
            {
                // «Bugun oxirgi kun» — `daysLeft = 0`. Parametr baribir
                // beriladi: matnni frontend tanlaydi va nolni ham
                // ishlatadi («bugun tugaydi»).
                parameters[NotificationParams.DaysLeft] = Number(daysLeft);
            }

            if (canSeeSalary)
            {
                parameters[NotificationParams.SalaryAmount] =
                    row.SalaryAmount.ToString(CultureInfo.InvariantCulture);
                parameters[NotificationParams.SalaryCurrency] = row.SalaryCurrency;
            }

            items.Add(new NotificationDto(
                NotificationKey.For(type, row.OfferId, row.ExpiresAt),
                type,
                severity,
                row.ExpiresAt,
                parameters,
                // Taklifning o'z sahifasi yo'q — u nomzod kartasida
                // ko'rinadi va javob ham o'sha yerda belgilanadi.
                new NotificationLinkDto(NotificationRoute.Candidate, row.CandidateId)));
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Kichik yordamchilar
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Bo'sh bo'lmagan qiymatni qo'shadi.
    /// </summary>
    /// <remarks>
    /// <c>null</c> qiymat parametrlar ichida UMUMAN bo'lmaydi, bo'sh satr
    /// bo'lib ham turmaydi: frontend uchun «maydon yo'q» va «maydon bo'sh»
    /// bir xil narsa emas — birinchisida u matnning butun bo'lagini
    /// tashlab yuboradi, ikkinchisida qavs ichida bo'shliq chizardi.
    /// </remarks>
    private static void AddIfPresent(Dictionary<string, string> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters[name] = value.Trim();
        }
    }

    /// <summary>Sana — ISO ko'rinishida (<c>2026-08-30</c>).</summary>
    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Son — invariant formatda, ajratgichsiz.</summary>
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
