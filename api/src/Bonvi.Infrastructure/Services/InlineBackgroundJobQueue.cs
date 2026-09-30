using System.Globalization;
using System.Linq.Expressions;
using Bonvi.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Infrastructure.Services;

/// <summary>
/// <see cref="IBackgroundJobQueue"/> ning SINXRON amalga oshirilishi — ish shu so'rovning o'zida bajariladi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Bu qulaylik uchun qo'yilgan vaqtinchalik «stub» emas.</b> Sinov
/// muhitida aynan shu sinf «moslik yaratildi → eski o'tish bog'landi → tabel
/// katagi paydo bo'ldi» zanjirini bitta HTTP so'rov ichida, kutishsiz va
/// aniq tekshirishga imkon beradi (SPEC §9.1.1, C6 nazorat nuqtasi).
/// </para>
/// <para>
/// <b>Vazifa CHAQIRUVCHINING qamrovida</b> (scope) olinadi: u so'rovning
/// <c>AppDbContext</c> ini ishlatadi, ya'ni use-case saqlab ulgurgan yozuvni
/// ko'radi. Istisno chaqiruvchiga O'TKAZIB yuboriladi — yutib yuborilsa,
/// sinov «hammasi joyida» deb o'tib ketardi.
/// </para>
/// <para>
/// ⚠️ FAQAT <c>Testing</c> muhitida ro'yxatga olinadi
/// (<c>DependencyInjection.AddInlineBackgroundJobQueue</c>, shartni Api dagi
/// <c>BackgroundJobs</c> hal qiladi); qolgan hamma joyda — Hangfire. Ishlab
/// chiqarishda ish HTTP so'rovini ushlab turmasligi kerak (§6.7: bir oylik
/// qayta hisoblash o'n minglab qator).
/// </para>
/// </remarks>
internal sealed class InlineBackgroundJobQueue : IBackgroundJobQueue
{
    private readonly IServiceProvider _services;

    public InlineBackgroundJobQueue(IServiceProvider services) => _services = services;

    /// <inheritdoc />
    public async Task<string> EnqueueAsync<TJob>(Expression<Func<TJob, Task>> job)
        where TJob : class
    {
        ArgumentNullException.ThrowIfNull(job);

        var instance = _services.GetRequiredService<TJob>();

        await job.Compile()(instance).ConfigureAwait(false);

        // Identifikator faqat javob shakli uchun: bu ish hech qayerda
        // saqlanmaydi va uni keyin so'rab bo'lmaydi.
        return "inline-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture);
    }
}
