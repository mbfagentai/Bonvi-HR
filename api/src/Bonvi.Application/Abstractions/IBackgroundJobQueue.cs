using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace Bonvi.Application.Abstractions;

/// <summary>
/// Ishni fonga yuborish porti — Application hech qanday navbat texnologiyasini bilmaydi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega port.</b> Ikki use-case ishni fonga beradi: moslik yaratilgach
/// eski o'tishlarni bog'lash (SPEC §4.3) va qo'lda qayta hisoblash (§6.7).
/// Ikkalasi ham <c>using Hangfire;</c> yozishi mumkin emas —
/// <c>LayerDependencyTests</c> Application'ni texnologiyadan toza saqlaydi
/// (SPEC §9.1.1).
/// </para>
/// <para>
/// <b>Ikki amalga oshirish, bitta qaror.</b> Sinov muhitida ish SHU so'rovning
/// o'zida, sinxron bajariladi (<c>InlineBackgroundJobQueue</c>) — «moslik
/// yaratdim, tabel katagi paydo bo'ldi» degan tekshiruv uxlab-kutishsiz,
/// aniq bo'ladi. Qolgan muhitlarda Hangfire (B7). Qaysi biri ulanishini
/// Hangfire serverini ishga tushiradigan AYNAN o'sha shart hal qiladi, ya'ni
/// ikkalasi hech qachon bir-biriga zid bo'lmaydi.
/// </para>
/// <para>
/// ⚠️ <b>SPEC dagi shakldan ikki farq, ikkalasi ham ataylab.</b>
/// (1) Ish <c>Expression&lt;Func&lt;TJob, Task&gt;&gt;</c>, <c>Action</c> emas: vazifalar
/// bazaga yozadi, ya'ni asinxron. <c>Action</c> bo'lganda sinxron variant yo
/// <c>async void</c> (istisno jarayonni yiqitadi), yo so'rov oqimini bloklab
/// kutish qilishga majbur bo'lardi. Hangfire aynan shu shaklni to'g'ridan-
/// to'g'ri qabul qiladi. (2) Natija — vazifa identifikatori: <c>POST /recompute</c>
/// javobida <c>jobId</c> bor (§6.7).
/// </para>
/// <para>
/// Vazifa ichidagi <c>CancellationToken</c> argumenti sifatida
/// <see cref="CancellationToken.None"/> beriladi — Hangfire uni o'z bekor
/// qilish belgisiga almashtiradi, sinxron variant esa ishni oxirigacha
/// yetkazadi (yarim qayta hisoblangan oy qolmasin).
/// </para>
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Nom SPEC §9.1.1 da qat'iy (B7 ham shu nomni amalga oshiradi); bu to'plam emas, lekin «navbat» — aynan uning ma'nosi.")]
public interface IBackgroundJobQueue
{
    /// <summary>Ishni navbatga qo'yadi va uning identifikatorini qaytaradi.</summary>
    /// <typeparam name="TJob">Vazifa sinfi — DI konteyneridan olinadi.</typeparam>
    /// <param name="job">Chaqiriladigan metod, argumentlari bilan (ular saqlanadi va keyin qayta o'qiladi).</param>
    /// <returns>Vazifa identifikatori.</returns>
    Task<string> EnqueueAsync<TJob>(Expression<Func<TJob, Task>> job)
        where TJob : class;
}
