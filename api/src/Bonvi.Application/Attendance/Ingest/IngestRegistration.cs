using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bonvi.Application.Attendance.Ingest;

/// <summary>Qurilma hodisalarini qabul qilish yo'lini ro'yxatga oladi.</summary>
/// <remarks>
/// <para>
/// <b>Nega davomat modulining umumiy ro'yxatidan ALOHIDA fayl.</b> Qabul yo'li
/// boshqaruv use-case'laridan oldin quriladi va endpointi paydo bo'lgan
/// lahzadanoq ishlashi kerak. Umumiy <c>AddAttendance()</c> uni ichidan
/// chaqiradi (SPEC §10.2.1).
/// </para>
/// <para>
/// ⚠️ <b>Hammasi <c>TryAdd*</c> bilan va bu ataylab.</b> Bu metod bir
/// jarayonda IKKI marta chaqirilishi mumkin (vaqtincha — <c>AddApplication</c>
/// dan to'g'ridan-to'g'ri, keyin <c>AddAttendance</c> ichidan). Oddiy
/// <c>AddSingleton&lt;IAttendanceEventParser, …&gt;</c> ikkinchi chaqiruvda
/// har bir tahlilchini IKKINCHI marta qo'shardi va buyruq ishlovchisidagi
/// <c>Single(...)</c> birinchi so'rovdayoq yiqilardi.
/// </para>
/// <para>
/// <see cref="IAttendanceIngestJournal"/> bu yerda YO'Q: u HTTP tushunchalarini
/// (usul, yo'l) yozadi va Api qatlamida ro'yxatga olinadi.
/// </para>
/// </remarks>
public static class IngestRegistration
{
    public static IServiceCollection AddAttendanceIngest(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Tahlilchilar — holatsiz (so'rovga oid holat yo'q), bitta nusxa.
        // Yangi ishlab chiqaruvchi = shu yerga bitta qator.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAttendanceEventParser, HikvisionEventParser>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAttendanceEventParser, JsonEventParser>());

        // Kesh — SINGLETON va boshqacha bo'lishi mumkin emas: har so'rovda
        // yangisi yasalsa, u hech narsani eslab qolmasdi, moslikni boshqaruvchi
        // buyruqlarning `Evict` chaqiruvi esa boshqa nusxaga tushardi.
        services.TryAddSingleton<AttendanceIdentityCache>();

        services.TryAddScoped<AttendanceIdentityResolver>();
        services.TryAddScoped<AttendanceDeviceAuthenticator>();
        services.TryAddScoped<PassPhotoStore>();
        services.TryAddScoped<IngestAttendanceEventCommandHandler>();

        return services;
    }
}
