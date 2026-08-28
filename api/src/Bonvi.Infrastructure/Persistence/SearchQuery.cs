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

        // Bitta qidiruv matnidan UCH naqsh: ism ustuni kirillda, pasport/
        // JSHSHIR lotin va raqamda, telefon esa FAQAT raqamda (ajratgichsiz)
        // saqlanadi va har biri boshqa-boshqa «bir xil ko'rinadigan»
        // belgilardan aziyat chekadi. To'liq sabab — `SearchFolding` izohida.
        var namePattern = SearchPattern.For(SearchFolding.ForName(search));
        var codePattern = SearchPattern.For(SearchFolding.ForCode(search));
        var phoneDigits = SearchFolding.ForPhone(search);

        // ⚠️ `translate(...)` ning ichidagi ikki satr XOM HOLDA yozilgan,
        // parametr sifatida emas. Bu majburiy: PostgreSQL ifoda ustidagi
        // indeksni faqat so'rovdagi ifoda indeks ta'rifi bilan AYNAN mos
        // tushganda ishlatadi, parametr esa moslikni buzadi. Qiymatlar
        // `SearchFolding` dagi konstantalardan ko'chirilgan va ular
        // `ix_employees_full_name_fold_trgm` indeksida ham shundayligicha
        // turibdi — uchalasi birga o'zgaradi.
        //
        // FIO ifodasi ham migratsiyadagi trigramma indeksi bilan AYNAN bir
        // xil: bir belgi farq qilsa ham PostgreSQL indeksni ishlatmaydi va
        // qidiruv jimgina to'liq skanerlashga tushadi.
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
                     WHERE translate(
                               lower(x.last_name || ' ' || x.first_name || ' ' || coalesce(x.middle_name, '')),
                               'қғҳўё', 'кгхуе') ILIKE {namePattern}
                        OR x.pinfl ILIKE {codePattern}
                        OR x.passport ILIKE {codePattern}
                     """)
                .Select(employee => employee.Id)
            : context.Employees
                .FromSql(
                    $"""
                     SELECT x.*
                     FROM employees AS x
                     WHERE translate(
                               lower(x.last_name || ' ' || x.first_name || ' ' || coalesce(x.middle_name, '')),
                               'қғҳўё', 'кгхуе') ILIKE {namePattern}
                        OR x.pinfl ILIKE {codePattern}
                        OR x.passport ILIKE {codePattern}
                        OR regexp_replace(x.phone, '\D', '', 'g') ILIKE {SearchPattern.For(phoneDigits)}
                        OR regexp_replace(x.second_phone, '\D', '', 'g') ILIKE {SearchPattern.For(phoneDigits)}
                     """)
                .Select(employee => employee.Id);
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
/// Ism ustunlari kirillda va ular ham yig'iladi (қ/ғ/ҳ/ў/ё): «Абдуқодиров»
/// ni rus klaviaturasida yozgan administrator ham hisobni topishi kerak.
/// Foydalanuvchilar jadvalida bunday ifoda ustida indeks YO'Q va u kerak
/// ham emas — tizimda bir necha o'nlab hisob bor, yuz minglab emas.
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

        var namePattern = SearchPattern.For(SearchFolding.ForName(search));
        var phoneDigits = SearchFolding.ForPhone(search);

        // Telefon sharti xodimlardagi bilan bir xil sababga ko'ra shartli:
        // raqamsiz matnda naqsh `%%` bo'lib, butun ro'yxatni olib kelardi.
        return phoneDigits.Length == 0
            ? context.Users
                .FromSql(
                    $"""
                     SELECT x.*
                     FROM users AS x
                     WHERE translate(
                               lower(x.last_name || ' ' || x.first_name || ' ' || coalesce(x.middle_name, '')),
                               'қғҳўё', 'кгхуе') ILIKE {namePattern}
                     """)
                .Select(user => user.Id)
            : context.Users
                .FromSql(
                    $"""
                     SELECT x.*
                     FROM users AS x
                     WHERE translate(
                               lower(x.last_name || ' ' || x.first_name || ' ' || coalesce(x.middle_name, '')),
                               'қғҳўё', 'кгхуе') ILIKE {namePattern}
                        OR regexp_replace(x.login, '\D', '', 'g') ILIKE {SearchPattern.For(phoneDigits)}
                     """)
                .Select(user => user.Id);
    }
}
