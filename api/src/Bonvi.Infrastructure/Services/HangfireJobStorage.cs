using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;

namespace Bonvi.Infrastructure.Services;

/// <summary>
/// Hangfire navbatining PostgreSQL ombori — ilovaning o'z bazasida, alohida <c>hangfire</c> sxemasida.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega Infrastructure da, Api dagi <c>BackgroundJobs</c> da emas.</b> Ulanish
/// Npgsql orqali quriladi, Api esa drayverga to'g'ridan-to'g'ri bog'lanmasligi
/// kerak (<c>LayerDependencyTests</c>). Serverni ishga tushirish va takroriy
/// jadval — Api ning ishi; «qaysi bazada, qaysi sxemada» — shu yerda.
/// </para>
/// <para>
/// <b>Nega alohida sxema.</b> Hangfire jadvallarini o'zi yaratadi va EF modeliga
/// kirmaydi: <c>public</c> sxemada ular migratsiya tekshiruvlari va zaxira
/// ro'yxatlari orasida EF jadvallari bilan aralashib ketardi. Sxemani ishga
/// tushishda Hangfire o'zi tayyorlaydi (<see cref="PostgreSqlStorageOptions.PrepareSchemaIfNecessary"/>),
/// ya'ni <c>./bonvi db-update</c> ga ham, EF modelining suratiga ham tegmaydi.
/// </para>
/// <para>
/// ⚠️ Bu metod chaqirilgan ZAHOTI ombor quriladi — ulanish ochiladi va sxema
/// tayyorlanadi. Uni <c>AddHangfire</c> sozlash lambdasining ichida chaqirish
/// shart: u lambdani birinchi murojaatgacha (server yoki takroriy jadval)
/// kechiktiradi, ya'ni <c>dotnet ef</c> xostni qurganda Hangfire bazaga ulanmaydi.
/// </para>
/// </remarks>
public static class HangfireJobStorage
{
    /// <summary>Hangfire jadvallari turadigan sxema.</summary>
    public const string Schema = "hangfire";

    /// <summary>Hangfire ni ilovaning PostgreSQL bazasiga ulaydi.</summary>
    /// <param name="hangfire">Hangfire sozlamasi.</param>
    /// <param name="configuration">Ilova sozlamasi — <c>ConnectionStrings:Default</c> shu yerdan.</param>
    public static IGlobalConfiguration UseBonviPostgreSqlStorage(
        this IGlobalConfiguration hangfire,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(hangfire);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "«ConnectionStrings:Default» sozlamasi bo'sh — Hangfire navbatini ulab bo'lmaydi.");
        }

        return hangfire.UsePostgreSqlStorage(
            bootstrapper => bootstrapper.UseNpgsqlConnection(connectionString),
            new PostgreSqlStorageOptions
            {
                SchemaName = Schema,
                PrepareSchemaIfNecessary = true,

                // Uzoq vazifa (bir choraklik qo'lda qayta hisoblash) ko'rinmaslik
                // muddatidan oshsa, boshqa ishchi uni «tashlab ketilgan» deb
                // IKKINCHI marta boshlardi. Natija baribir bir xil, lekin 2 GB
                // xotirali serverda ikki baravar yuk. Sirpanuvchi muddat vazifa
                // tirik ekanini yurish davomida yangilab turadi.
                UseSlidingInvisibilityTimeout = true,
            });
    }
}
