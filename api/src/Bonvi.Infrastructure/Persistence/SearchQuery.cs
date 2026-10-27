using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence;

/// <summary>
/// <c>ILIKE</c> naqshi — qidiruv matnini «ichida qidirish» shakliga o'giradi.
/// </summary>
/// <remarks>
/// <para>
/// Bu yordamchi ilgari HAR BIR o'qish modelida alohida <c>LikePattern</c>
/// bo'lib takrorlanardi. Nusxalar bir-biriga qarab turmaydi va aynan shu
/// tarzda loyihada <c>flags</c>/<c>activeFlags</c> xatosi tug'ilgan edi:
/// bitta qoida ikki joyda yozilib, biri o'zgarganda ikkinchisi eskirib
/// qolgan. Endi ekranlash qoidasi bitta joyda.
/// </para>
/// </remarks>
internal static class SearchPattern
{
    /// <summary>
    /// <c>ILIKE</c> naqshidagi ekranlash belgisi — teskari chiziq.
    /// </summary>
    /// <remarks>
    /// <see cref="For"/> aynan shu belgi bilan ekranlaydi va u xom SQL
    /// yo'lidagi PostgreSQL sukutiga to'g'ri keladi.
    /// <c>EF.Functions.ILike</c> ga esa uni QO'LDA uzatish kerak: ikki
    /// argumentli chaqiruv Npgsql'da <c>ESCAPE ''</c> bo'lib tarjima
    /// bo'ladi, ya'ni ekranlash BUTUNLAY o'chadi.
    /// </remarks>
    public const string Escape = "\\";

    /// <summary>Qidiruv matnini <c>ILIKE</c> naqshiga aylantiradi.</summary>
    /// <remarks>
    /// <c>\</c>, <c>%</c> va <c>_</c> ekranlanadi. Aks holda «50%» deb
    /// qidirgan foydalanuvchi butun jadvalni olib kelardi, «_» esa istalgan
    /// belgiga mos tushardi — ikkalasi ham jimgina noto'g'ri natija beradi.
    /// </remarks>
    public static string For(string search)
    {
        var escaped = search
            .Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }
}

/// <summary>
/// «Xodim bo'yicha qidiruv» sharti — F.I.Sh. + JSHSHIR + pasport + telefon.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida fayl, o'qish modelining ichida emas.</b> Bu shart avval
/// <c>EmployeeReadModel</c> ichida xom SQL bo'lib yotardi va
/// <c>IncentiveReadModel</c> uning QISQARTIRILGAN nusxasini saqlardi (faqat
/// ism). Natijada bitta savolga — «bu odamni qanday topaman» — ikki ekran
/// ikki xil javob berardi: xodimlar ro'yxatida JSHSHIR ishlardi,
/// rag'bat ro'yxatida esa jimgina hech narsa topilmasdi. Bu loyihadagi
/// <c>flags</c>/<c>activeFlags</c> xatosining aynan o'zi: ikki nusxa vaqt
/// o'tib ajralib ketadi. Endi qoida bitta joyda va u ETTITA ro'yxatga
/// qo'llanadi.
/// </para>
/// <para>
/// <b>Nega shart xom SQL'da.</b> JSHSHIR, pasport va telefon — qiymat
/// obyektlari; ular bazaga <i>konvertor</i> orqali satr bo'lib tushadi.
/// LINQ ichida <c>employee.Pinfl.Value</c> ifodasi SQL'ga tarjima
/// QILINMAYDI: konvertordan keyin EF uchun ustun skalyar, uning «ichi»
/// yo'q (tekshirilgan — EF «could not be translated» deb yiqiladi). Qolgan
/// yagona muqobil butun jadvalni xotiraga o'qib, keyin filtrlash bo'lardi;
/// aynan shu narsa <c>IEmployeeReadModel</c> izohida taqiqlangan.
/// </para>
/// <para>
/// <b>Nega xom so'rovdan faqat identifikator olinadi.</b>
/// To'g'ridan-to'g'ri <see cref="Employee"/> o'qilsa, EF ustki proyeksiyada
/// complex type ustunlarini o'z sukut nomlari bilan
/// (<c>"FullName_LastName"</c>) so'raydi — bizda esa ular <c>last_name</c>,
/// <c>first_name</c>, <c>middle_name</c> (<c>EmployeeConfiguration</c> da
/// ataylab qisqartirilgan) va so'rov «column does not exist» bilan
/// yiqiladi. Shu sababli xom so'rovdan faqat kalit olinadi.
/// </para>
/// <para>
/// Natija baribir <b>bitta</b> SQL bayonoti: ichki so'rov ustkisiga
/// <c>id IN (…)</c> bo'lib qo'shiladi, hech qanday oraliq ro'yxat
/// tarmoqdan o'tmaydi.
/// </para>
/// <para>
/// Qidiruv matni <b>parametr</b> bo'lib uzatiladi — <c>FromSql</c>
/// interpolyatsiyani parametrga aylantiradi, ya'ni SQL in'ektsiya yo'li
/// yo'q.
/// </para>
/// </remarks>
internal static class EmployeeSearchQuery
{
    /// <summary>
    /// Qidiruvga mos keladigan xodimlar — matn bo'sh bo'lsa, HAMMASI.
    /// </summary>
    /// <remarks>
    /// Xodimning o'zi kerak bo'lgan joylar uchun (ism, rasm, munosabatlar
    /// bilan birlashtirish). Faqat kalit yetarli bo'lsa —
    /// <see cref="MatchingIds"/>.
    /// </remarks>
    public static IQueryable<Employee> Matching(AppDbContext context, string? search)
    {
        ArgumentNullException.ThrowIfNull(context);

        var employees = context.Employees.AsNoTracking();

        if (string.IsNullOrWhiteSpace(search))
        {
            return employees;
        }

        var matching = MatchingIds(context, search);

        return employees.Where(employee => matching.Contains(employee.Id));
    }

    /// <summary>
    /// Qidiruvga mos keladigan xodimlarning identifikatorlari.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Boshqa jadvaldan (belgi, sinov, rag'bat, baxtsiz hodisa, buyruq)
    /// filtrlash uchun: <c>rows.Where(r => ids.Contains(r.EmployeeId))</c>.
    /// Bunday holatda xodimning o'zi kerak emas va uni yuklash ortiqcha
    /// birlashtirish bo'lardi.
    /// </para>
    /// <para>
    /// ⚠️ Matn bo'sh bo'lganda chaqirilmaydi: u holda so'rov «hech kim»
    /// emas, «hamma» degan ma'noni berishi kerak va buni chaqiruvchi
    /// shartni umuman qo'shmaslik bilan hal qiladi.
    /// </para>
    /// </remarks>
    public static IQueryable<Guid> MatchingIds(AppDbContext context, string search)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Bitta qidiruv matnidan UCH shart: ism — lotin yoki kirillda
        // yozilganidan qat'i nazar, umumiy qidiruv kaliti bo'yicha; pasport/
        // JSHSHIR lotin va raqamda, telefon esa FAQAT raqamda (ajratgichsiz)
        // saqlanadi. Kod va telefon qoidalarining sababi — `SearchFolding`
        // izohida.
        var namePattern = SearchPattern.For(search);
        var codePattern = SearchPattern.For(SearchFolding.ForCode(search));
        var phoneDigits = SearchFolding.ForPhone(search);

        // Ism sharti: `x.search_key` — bazadagi tayyor ustun (to'liq ism
        // kaliti, qator yozilganda baza o'zi hisoblaydi; trigramma indeksi
        // shu ustun ustida). Qidiruv matni esa xuddi O'SHA `search_key()`
        // funksiyasidan o'tadi, ya'ni «Karimov» ham, «Каримов» ham
        // `karimuv` bo'lib solishtiriladi. Qoida FAQAT shu SQL funksiyada
        // (migratsiya `IsmQidiruvKaliti`) — C# da uning nusxasi YO'Q, shu
        // sababli ikki tomon hech qachon ajralib ketmaydi.
        //
        // Naqsh funksiyadan OLDIN ekranlanadi: funksiya `%`, `_` va `\` ga
        // tegmaydi. U IMMUTABLE — PostgreSQL uni reja tuzishda parametr
        // qiymati bilan bir marta hisoblaydi va indeksni ishlatadi.
        //
        // Telefon sharti ESA butunlay boshqa sabab bilan shartli: agar
        // qidiruv matnida bironta ham raqam bo'lmasa (HR ism bilan
        // qidiryapti), `phoneDigits` bo'sh qatorga aylanadi va uni naqshga
        // aylantirsak `%%` chiqardi — bu BUTUN jadvalni olib kelardi va ism
        // bo'yicha qidiruvni ma'nosiz qilardi. Shu sababli bo'sh bo'lganda
        // telefon sharti SQL matniga UMUMAN qo'shilmaydi.
        //
        // `regexp_replace(x.phone, '\D', '', 'g')` — ustundagi ajratgichlarni
        // (bo'shliq, tire, «+») so'rov vaqtida olib tashlaydi, xuddi
        // `phoneDigits` qidiruv matni ustida qilganidek. `ix_employees_phone`
        // — oddiy B-tree, ifoda ustida EMAS, shu sababli bu yerda ifodani
        // o'zgartirish uni buzmaydi. U indeks aslida shu «ichida qidirish»
        // (`%...%`) so'roviga baribir mos kelmaydi — PostgreSQL bunday
        // naqsh uchun B-tree'ni ishlata olmaydi (chap tomoni joker bilan
        // boshlanadi), ya'ni bu yerda seq scan avvaldan ham bor edi.
        return phoneDigits.Length == 0
            ? context.Employees
                .FromSql(
                    $"""
                     SELECT x.*
                     FROM employees AS x
                     WHERE x.search_key LIKE search_key({namePattern})
                        OR x.pinfl ILIKE {codePattern}
                        OR x.passport ILIKE {codePattern}
                     """)
                .Select(employee => employee.Id)
            : context.Employees
                .FromSql(
                    $"""
                     SELECT x.*
                     FROM employees AS x
                     WHERE x.search_key LIKE search_key({namePattern})
                        OR x.pinfl ILIKE {codePattern}
                        OR x.passport ILIKE {codePattern}
                        OR regexp_replace(x.phone, '\D', '', 'g') ILIKE {SearchPattern.For(phoneDigits)}
                        OR regexp_replace(x.second_phone, '\D', '', 'g') ILIKE {SearchPattern.For(phoneDigits)}
                     """)
                .Select(employee => employee.Id);
    }

    /// <summary>
    /// TASHQI API uchun qidiruv — faqat F.I.Sh. va telefon.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega <see cref="Matching"/> dan alohida.</b> Ichki qidiruv
    /// JSHSHIR va pasport bo'yicha ham topadi va HR uchun bu to'g'ri. Lekin
    /// tashqarida bu qidiruvning o'zi ma'lumot beradi: kalit egasi qo'lidagi
    /// JSHSHIR'ni yuborib, «bu odam sizda ishlaydimi?» degan savolga javob
    /// olardi — va javobda to'liq ismni ko'rardi. Qiymatning javobga
    /// tushmasligi bunga to'sqinlik qilmaydi, chunki sizib chiqadigan narsa
    /// qiymat emas, MOSLIK faktining o'zi.
    /// </para>
    /// <para>
    /// Egasining qarori, 2026-09-18: tashqarida faqat ism va telefon.
    /// Qo'riqchi test — <c>PublicApiLeakTests</c> dagi «JSHSHIR bo'yicha
    /// topib bo'lmaydi».
    /// </para>
    /// <para>
    /// Ism va telefon shartlari <see cref="MatchingIds"/> dagi bilan AYNAN
    /// bir xil yozilgan (qidiruv kaliti haqidagi izohlar o'sha yerda) —
    /// ikkisi birga o'zgaradi. Rahbar boti ham shu sababli «Karimov» bilan
    /// «Каримов» ni topadi.
    /// </para>
    /// </remarks>
    public static IQueryable<Employee> MatchingPublic(AppDbContext context, string? search)
    {
        ArgumentNullException.ThrowIfNull(context);

        var employees = context.Employees.AsNoTracking();

        if (string.IsNullOrWhiteSpace(search))
        {
            return employees;
        }

        var namePattern = SearchPattern.For(search);
        var phoneDigits = SearchFolding.ForPhone(search);

        var matching = phoneDigits.Length == 0
            ? context.Employees
                .FromSql(
                    $"""
                     SELECT x.*
                     FROM employees AS x
                     WHERE x.search_key LIKE search_key({namePattern})
                     """)
                .Select(employee => employee.Id)
            : context.Employees
                .FromSql(
                    $"""
                     SELECT x.*
                     FROM employees AS x
                     WHERE x.search_key LIKE search_key({namePattern})
                        OR regexp_replace(x.phone, '\D', '', 'g') ILIKE {SearchPattern.For(phoneDigits)}
                        OR regexp_replace(x.second_phone, '\D', '', 'g') ILIKE {SearchPattern.For(phoneDigits)}
                     """)
                .Select(employee => employee.Id);

        return employees.Where(employee => matching.Contains(employee.Id));
    }

    /// <summary>
    /// Bo'lim boshlig'i paneli uchun qidiruv — faqat F.I.Sh. va ASOSIY telefon (SPEC-jamoam §C2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega <see cref="MatchingPublic"/> dan ham tor.</b> Boshliq ekranida faqat ism va
    /// asosiy telefon ko'rinadi; ikkinchi telefon hech qayerda chiqmaydi. Qidiruv ko'rinmaydigan
    /// ustun bo'yicha ham topsa, boshliq qo'lidagi raqamni yuborib «bu raqam kimniki» degan
    /// savolga javob olardi — sizib chiqadigan narsa qiymat emas, MOSLIK faktining o'zi
    /// (<see cref="MatchingPublic"/> dagi JSHSHIR mulohazasi bilan bir xil).
    /// </para>
    /// <para>
    /// Ism va asosiy telefon shartlari <see cref="MatchingPublic"/> dagi bilan AYNAN bir xil —
    /// ikkisi birga o'zgaradi. Tashqi API qoidasi ataylab tegilmagan: u egasining alohida qarori.
    /// </para>
    /// </remarks>
    public static IQueryable<Employee> MatchingTeam(AppDbContext context, string? search)
    {
        ArgumentNullException.ThrowIfNull(context);

        var employees = context.Employees.AsNoTracking();

        if (string.IsNullOrWhiteSpace(search))
        {
            return employees;
        }

        var namePattern = SearchPattern.For(search);
        var phoneDigits = SearchFolding.ForPhone(search);

        var matching = phoneDigits.Length == 0
            ? context.Employees
                .FromSql(
                    $"""
                     SELECT x.*
                     FROM employees AS x
                     WHERE x.search_key LIKE search_key({namePattern})
                     """)
                .Select(employee => employee.Id)
            : context.Employees
                .FromSql(
                    $"""
                     SELECT x.*
                     FROM employees AS x
                     WHERE x.search_key LIKE search_key({namePattern})
                        OR regexp_replace(x.phone, '\D', '', 'g') ILIKE {SearchPattern.For(phoneDigits)}
                     """)
                .Select(employee => employee.Id);

        return employees.Where(employee => matching.Contains(employee.Id));
    }
}

/// <summary>
/// «Foydalanuvchi bo'yicha qidiruv» sharti — F.I.Sh. + login (telefon).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <see cref="EmployeeSearchQuery"/> dan alohida.</b> Foydalanuvchi
/// — bu tizimga KIRADIGAN hisob, xodim emas: unda JSHSHIR ham, pasport ham
/// yo'q va hech qachon bo'lmaydi (izohi <see cref="User"/> da — hisob
/// xodimga ixtiyoriy bog'lanadi, uning nusxasi emas). Xodim shartini shu
/// yerda qayta ishlatish yo'q ustunlarni qidirishga majbur qilardi.
/// </para>
/// <para>
/// <b>Login — telefon raqami</b> (<c>PhoneNumber</c> qiymat obyekti,
/// bazada kanonik satr: <c>+998905088888</c>). Administrator esa ekranda
/// ko'rgan formatlangan raqamni ko'chirib qo'yishi mumkin, shu sababli
/// telefon qoidasi xodimlarnikidan NUSXA OLINMAYDI, balki bir xil
/// yordamchidan (<c>SearchFolding.ForPhone</c>) keladi — ikki ekran bir
/// xil raqamga bir xil javob berishi kerak.
/// </para>
/// <para>
/// Ism xodimlardagi bilan bir xil qidiruv kaliti (<c>search_key()</c>)
/// orqali solishtiriladi: «Абдуқодиров» ni rus klaviaturasida yoki lotinda
/// yozgan administrator ham hisobni topishi kerak. Foydalanuvchilar
/// jadvalida kalit uchun tayyor ustun ham, indeks ham YO'Q va ular kerak
/// emas — tizimda bir necha o'nlab hisob bor, funksiya har qatorda
/// hisoblansa ham sezilmaydi.
/// </para>
/// </remarks>
internal static class UserSearchQuery
{
    /// <summary>
    /// Qidiruvga mos keladigan foydalanuvchilarning identifikatorlari.
    /// </summary>
    /// <remarks>
    /// Xom SQL sababi <see cref="EmployeeSearchQuery"/> dagi bilan aynan
    /// bir xil: <c>Login</c> konvertor orqali satrga tushadi va
    /// <c>user.Login.Value</c> ifodasi SQL'ga tarjima qilinmaydi; ustunlar
    /// esa <c>UserConfiguration</c> da <c>last_name</c> / <c>first_name</c> /
    /// <c>middle_name</c> deb ataylab qisqartirilgan, shu sababli xom
    /// so'rovdan faqat kalit olinadi.
    /// </remarks>
    public static IQueryable<Guid> MatchingIds(AppDbContext context, string search)
    {
        ArgumentNullException.ThrowIfNull(context);

        var namePattern = SearchPattern.For(search);
        var phoneDigits = SearchFolding.ForPhone(search);

        // Telefon sharti xodimlardagi bilan bir xil sababga ko'ra shartli:
        // raqamsiz matnda naqsh `%%` bo'lib, butun ro'yxatni olib kelardi.
        return phoneDigits.Length == 0
            ? context.Users
                .FromSql(
                    $"""
                     SELECT x.*
                     FROM users AS x
                     WHERE search_key(x.last_name || ' ' || x.first_name || ' ' || coalesce(x.middle_name, '')) LIKE search_key({namePattern})
                     """)
                .Select(user => user.Id)
            : context.Users
                .FromSql(
                    $"""
                     SELECT x.*
                     FROM users AS x
                     WHERE search_key(x.last_name || ' ' || x.first_name || ' ' || coalesce(x.middle_name, '')) LIKE search_key({namePattern})
                        OR regexp_replace(x.login, '\D', '', 'g') ILIKE {SearchPattern.For(phoneDigits)}
                     """)
                .Select(user => user.Id);
    }
}

/// <summary>
/// «Nomzod bo'yicha qidiruv» sharti — F.I.Sh. + telefon.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <see cref="EmployeeSearchQuery"/> dan alohida.</b> Nomzodda
/// JSHSHIR ham, pasport ham YO'Q va bo'lmasligi ham mumkin: u hali hujjat
/// topshirmagan (sababi <c>Candidate.Phone</c> izohida). Xodim
/// shartini shu yerda qayta ishlatish mavjud bo'lmagan ustunlarni
/// qidirishga majbur qilardi.
/// </para>
/// <para>
/// <b>Qoidaning O'ZI esa nusxa ko'chirilmaydi.</b> Ism qidiruv kaliti
/// (<c>search_key()</c>, lotin/kirill farqsiz) bazadagi bitta funksiyadan,
/// telefon ajratgichlarini tozalash <see cref="SearchFolding"/> dan keladi
/// — xodimlar, foydalanuvchilar va nomzodlar uchun BITTA joydan. Aks holda
/// HR bitta ismni ikki ekranda ikki xil natija bilan qidirardi va bu
/// loyihadagi eng qimmat xatoning (topilmagan odam → yangi dublikat karta)
/// aynan o'zi bo'lardi.
/// </para>
/// <para>
/// <b>Kalit tayyor ustunda va indeksli</b> — xodimlardagi kabi. Ilgari
/// nomzodlar «yiliga bir necha yuz qator» deb indekssiz qoldirilgan edi,
/// Jobster ko'chirilgach esa ular minglab (sabab migratsiya
/// <c>IsmQidiruvKaliti</c> izohida).
/// </para>
/// </remarks>
internal static class CandidateSearchQuery
{
    /// <summary>Qidiruvga mos keladigan nomzodlarning identifikatorlari.</summary>
    /// <remarks>
    /// ⚠️ Matn bo'sh bo'lganda chaqirilmaydi: u holda so'rov «hech kim»
    /// emas, «hamma» degan ma'noni berishi kerak va buni chaqiruvchi
    /// shartni umuman qo'shmaslik bilan hal qiladi
    /// (<see cref="EmployeeSearchQuery.MatchingIds"/> bilan bir xil qoida).
    /// </remarks>
    public static IQueryable<Guid> MatchingIds(AppDbContext context, string search)
    {
        ArgumentNullException.ThrowIfNull(context);

        var namePattern = SearchPattern.For(search);
        var phoneDigits = SearchFolding.ForPhone(search);

        // Telefon sharti shartli — xodimlardagi bilan bir xil sabab: matnda
        // bironta ham raqam bo'lmasa, naqsh `%%` bo'lib butun jadvalni olib
        // kelardi va ism bo'yicha qidiruvni ma'nosiz qilardi.
        //
        // Xom SQL sababi ham o'sha: `Phone` konvertor orqali satrga tushadi
        // va `candidate.Phone.Value` ifodasi SQL'ga tarjima QILINMAYDI;
        // F.I.Sh. ustunlari esa complex type va ular `last_name` /
        // `first_name` / `middle_name` deb ataylab qisqartirilgan.
        return phoneDigits.Length == 0
            ? context.Candidates
                .FromSql(
                    $"""
                     SELECT x.*
                     FROM candidates AS x
                     WHERE x.search_key LIKE search_key({namePattern})
                     """)
                .Select(candidate => candidate.Id)
            : context.Candidates
                .FromSql(
                    $"""
                     SELECT x.*
                     FROM candidates AS x
                     WHERE x.search_key LIKE search_key({namePattern})
                        OR regexp_replace(coalesce(x.phone, ''), '\D', '', 'g') ILIKE {SearchPattern.For(phoneDigits)}
                     """)
                .Select(candidate => candidate.Id);
    }

    /// <summary>
    /// Telefon raqami AYNAN mos keladigan nomzodlar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <see cref="MatchingIds"/> dan farqi: bu yerda <c>ILIKE</c> va
    /// naqsh YO'Q — qat'iy tenglik. Sabab: bu so'rov QIDIRUV emas,
    /// TEKSHIRUV (takroriy ariza va qora ro'yxat). Naqsh bilan
    /// «998901234567» so'rovi «998901234567 8» kabi begona raqamni ham
    /// topib, boshqa odamning arizasini to'sib qo'yardi.
    /// </para>
    /// <para>
    /// Xom SQL sababi o'sha: <c>Phone</c> konvertor orqali satrga
    /// tushadi va <c>candidate.Phone.Value</c> ifodasi SQL'ga tarjima
    /// qilinmaydi. Normalizatsiya ifodasi ham yuqoridagi bilan
    /// SO'ZMA-SO'Z bir xil — ikkalasi bir xil raqamni bir xil
    /// ko'rmasa, qidiruvda topilgan odam tekshiruvda topilmasdi.
    /// </para>
    /// </remarks>
    public static IQueryable<Guid> WithPhone(AppDbContext context, string phoneDigits)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Candidates
            .FromSql(
                $"""
                 SELECT x.*
                 FROM candidates AS x
                 WHERE regexp_replace(coalesce(x.phone, ''), '\D', '', 'g') = {phoneDigits}
                 """)
            .Select(candidate => candidate.Id);
    }
}

/// <summary>
/// «Vakansiya bo'yicha qidiruv» sharti — faqat NOM.
/// </summary>
/// <remarks>
/// <para>
/// Nom ilgari «Vakansiya nomlari» ma'lumotnomasidan TANLANARDI, endi esa
/// vakansiyaning o'zida yoziladi (sababi <c>Vacancy</c> izohida) — shu
/// sababli ro'yxat filtri ham tanlovdan qidiruvga aylandi.
/// </para>
/// <para>
/// Yig'ish qoidasi (қ/ғ/ҳ/ў/ё) <see cref="SearchFolding"/> dan keladi.
/// Lotin/kirill farqsiz qidiruv kaliti (<c>search_key()</c>) bu yerda
/// ATAYLAB YO'Q: u odam ismlari uchun qilingan (egasining 2026-10-14 dagi
/// talabi), vakansiya nomi esa ism emas. Indeks yo'q: bir vaqtda bir necha
/// o'nlab vakansiya bo'ladi.
/// </para>
/// </remarks>
internal static class VacancySearchQuery
{
    /// <summary>Qidiruvga mos keladigan vakansiyalarning identifikatorlari.</summary>
    /// <remarks>
    /// ⚠️ Matn bo'sh bo'lganda chaqirilmaydi — qolgan qidiruvlardagi bilan
    /// bir xil qoida (<see cref="EmployeeSearchQuery.MatchingIds"/>).
    /// </remarks>
    public static IQueryable<Guid> MatchingIds(AppDbContext context, string search)
    {
        ArgumentNullException.ThrowIfNull(context);

        var namePattern = SearchPattern.For(SearchFolding.ForName(search));

        return context.Vacancies
            .FromSql(
                $"""
                 SELECT x.*
                 FROM vacancies AS x
                 WHERE translate(lower(x.title), 'қғҳўё', 'кгхуе') ILIKE {namePattern}
                 """)
            .Select(vacancy => vacancy.Id);
    }
}
