using System.Linq.Expressions;
using Bonvi.Application.Abstractions;
using Hangfire;

namespace Bonvi.Infrastructure.Services;

/// <summary>
/// <see cref="IBackgroundJobQueue"/> ning Hangfire orqali amalga oshirilishi — <c>Testing</c> dan boshqa HAMMA muhitda.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega ishlab chiqarishda sinxron navbat emas.</b> Import tasdig'i har
/// yaratilgan moslik uchun to'ldirish vazifasini qo'yadi; sinxron navbatda
/// 540 qatorlik birinchi import BITTA HTTP so'rov ichida ~540 × 8 so'rov
/// bo'lardi (B6 o'lchovi), qo'lda qayta hisoblash esa o'n minglab qatorni
/// so'rov vaqti chegarasida yozardi. Bu yerda ish bazadagi navbatga yoziladi
/// va so'rov darhol javob qaytaradi.
/// </para>
/// <para>
/// Ifoda Hangfire ga O'ZGARTIRILMASDAN beriladi: argumentlar shu lahzada
/// hisoblanib saqlanadi, <see cref="CancellationToken.None"/> esa ishlash
/// paytida serverning to'xtash belgisiga almashtiriladi. Vazifa sinfi
/// ishlash paytida alohida DI qamrovidan olinadi (<c>AspNetCoreJobActivator</c>).
/// </para>
/// </remarks>
internal sealed class HangfireBackgroundJobQueue : IBackgroundJobQueue
{
    private readonly IBackgroundJobClient _client;

    public HangfireBackgroundJobQueue(IBackgroundJobClient client) => _client = client;

    /// <inheritdoc />
    public Task<string> EnqueueAsync<TJob>(Expression<Func<TJob, Task>> job)
        where TJob : class
    {
        ArgumentNullException.ThrowIfNull(job);

        return Task.FromResult(_client.Enqueue(job));
    }
}
