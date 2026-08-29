using Bonvi.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence;

/// <summary>
/// «Bugun kimning tug'ilgan kuni» sharti — YAGONA nusxa.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida fayl, o'qish modellarining ichida emas.</b> Bu shart
/// avval IKKI joyda, so'zma-so'z takrorlanib yotardi:
/// <c>DashboardReadModel</c> bosh sahifadagi <c>birthdaysToday</c> SONINI
/// sanardi, <c>NotificationReadModel</c> esa aynan shu odamlarni RO'YXAT
/// qilib olardi. Ikkalasi bir xil savolga javob beradi va shu sababli
/// ular ajralib ketishi mumkin bo'lgan eng xavfli joy edi: bittasiga
/// sinovdagilar qo'shilsa yoki yumshoq o'chirish filtri o'zgarsa, ekranda
/// «Bugun 2 ta xodimning tug'ilgan kuni» yozuvi ochilib, ichidan uchta
/// (yoki bitta) odam chiqardi. HR uchun bu — raqamga bo'lgan ishonchning
/// yo'qolishi. Bu loyihada shunday xato allaqachon bo'lgan
/// (<c>flags</c>/<c>activeFlags</c>) va uning davosi
/// <see cref="EmployeeSearchQuery"/> dagi bilan bir xil: qoida bitta
/// joyda yashaydi.
/// </para>
/// <para>
/// <b>Yil e'tiborga OLINMAYDI</b> — faqat kun va oy. Aks holda tug'ilgan
/// kun umuman hech qachon to'g'ri kelmasdi.
/// </para>
/// <para>
/// <b>Kun va oy BITTA songa yig'iladi</b> (<c>oy * 100 + kun</c>, ya'ni
/// 31-dekabr → 1231). Sabab: shart bir nechta kun uchun ishlashi kerak
/// (bildirishnomalar bugun va ertangi kunni BITTA so'rovda oladi) va
/// har bir kun uchun <c>(oy = … VA kun = …)</c> juftligini «YOKI» bilan
/// qo'shib yozish shartning O'ZINI nusxalash demak bo'lardi — aynan shu
/// yerda ikkinchi nusxa yana paydo bo'lardi. Yig'ma kalit bilan esa
/// kunlar soni shartning shakliga umuman ta'sir qilmaydi:
/// PostgreSQL'da bu oddiy <c>… = ANY(@kunlar)</c> bo'lib chiqadi.
/// NEGA satr emas («12-31»): satr formatlash tilga va nolga to'ldirishga
/// bog'liq bo'lardi, butun son esa bir ma'noli.
/// </para>
/// <para>
/// <b>Yumshoq o'chirilganlar o'z-o'zidan chetda</b>: <c>Employee</c> va
/// <c>Employment</c> — <c>ISoftDeletable</c>, ya'ni global filtr ularni
/// allaqachon yashiradi. Bu yerda takroriy shart yozilmaydi.
/// </para>
/// </remarks>
internal static class BirthdayQuery
{
    /// <summary>
    /// «Ishlayotgan» deb hisoblanadigan mehnat munosabatlari.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Doimiy ham, SINOVDAGI ham kiradi: sinovdagi odam ham bugun ishga
    /// keladi va uni tabriklamaslik xunuk bo'lardi. Arxivdagilar esa
    /// chetda — ishdan ketgan odamni tabriklashga chaqirish HR ni noqulay
    /// vaziyatga qo'yardi.
    /// </para>
    /// <para>
    /// <b>Nega bu ALOHIDA chiqarilgan.</b> Ro'yxatda har bir xodimning
    /// lavozimi va bo'limi ko'rsatiladi, ular esa munosabatdan olinadi —
    /// va aynan SHU munosabatlardan olinishi kerak. Aks holda ro'yxatda
    /// odam «ishlayapti» deb turib, yonida uch yil oldin yopilgan
    /// shartnomasining bo'limi yozilardi. Agar shart shu yerda ikkinchi
    /// marta qo'lda yozilsa, u yuqoridagi sanoq bilan yana ajralib ketish
    /// xavfini tug'dirardi — shu sababli manba bitta.
    /// </para>
    /// </remarks>
    public static IQueryable<Employment> WorkingEmployments(AppDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Employments
            .AsNoTracking()
            .Where(employment =>
                employment.Status == EmploymentStatus.Active
                || employment.Status == EmploymentStatus.Probation);
    }

    /// <summary>
    /// Berilgan kunlarning birida tug'ilgan kuni bo'lgan <b>ishlayotgan</b>
    /// xodimlar.
    /// </summary>
    /// <param name="context">Ma'lumotlar bazasi konteksti.</param>
    /// <param name="days">
    /// Kunlar (Toshkent vaqti bo'yicha). Yili ahamiyatsiz — faqat kun va oy
    /// olinadi. Bosh sahifa bitta kun beradi, bildirishnomalar ikkitasini
    /// (bugun va ertaga).
    /// </param>
    /// <remarks>
    /// <para>
    /// Natija — <b>xodimlar</b> so'rovi, munosabatlar emas: o'rindosh
    /// (ikki firmada ishlaydigan odam) bir marta chiqishi kerak. Munosabat
    /// shart sifatida, ichma-ich so'rovda tekshiriladi va EF uni
    /// <c>EXISTS (…)</c> ga aylantiradi — <c>DISTINCT</c> ham,
    /// takroriy qator ham yo'q (<c>DashboardReadModel</c> dagi bilan bir
    /// xil qoida).
    /// </para>
    /// <para>
    /// ⚠️ 29-fevralda tug'ilganlar kabisa bo'lmagan yilda umuman
    /// chiqmaydi va bu ATAYLAB: sun'iy sana tanlash (28-fevral yoki
    /// 1-mart) tizimning qarori bo'lib qolardi, HR ning emas. Xuddi shu
    /// qoida bildirishnomalarda ham amal qiladi.
    /// </para>
    /// </remarks>
    public static IQueryable<Employee> WithBirthdayOn(AppDbContext context, params DateOnly[] days)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(days);

        var keys = Array.ConvertAll(days, day => (day.Month * 100) + day.Day);

        // `working` lambdadan TASHQARIDA hisoblanadi: u xodimga bog'liq
        // emas, ya'ni EF uni ichma-ich so'rov sifatida oladi. Shu naqsh
        // `DashboardReadModel.CountEmployeesAsync` da ham qo'llangan.
        var working = WorkingEmployments(context);

        return context.Employees
            .AsNoTracking()
            .Where(employee =>
                employee.BirthDate != null
                && keys.Contains((employee.BirthDate.Value.Month * 100) + employee.BirthDate.Value.Day)
                && working.Any(employment => employment.EmployeeId == employee.Id));
    }
}
