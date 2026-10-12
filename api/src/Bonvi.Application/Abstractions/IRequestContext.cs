namespace Bonvi.Application.Abstractions;

/// <summary>
/// Joriy amalning konteksti — audit yozuviga «qaysi amal, qaysi guruh, qayerdan» ni beradi.
/// </summary>
/// <remarks>
/// <para>
/// HTTP so'rovida <see cref="Operation"/> — moslangan endpointning nomi
/// (<c>.WithName("TransferEmployee")</c>), <see cref="CorrelationId"/> — so'rov
/// davomida bitta (shuning uchun «ko'chirish» ning mehnat munosabati va buyruq
/// qatorlari bitta guruhga tushadi), IP va brauzer — so'rovning o'zidan.
/// </para>
/// <para>
/// HTTP'siz joyda (ishga tushish, fon vazifasi, ETL) qiymatlarni
/// <see cref="IAuditScope.Use"/> beradi; IP va brauzer bo'sh.
/// </para>
/// </remarks>
public interface IRequestContext
{
    /// <summary>Amal nomi: <c>TransferEmployee</c>, <c>startup:identity-seed</c>, <c>etl:employees</c>.</summary>
    string? Operation { get; }

    /// <summary>Bitta so'rov yoki bitta tizim amali davomida bir xil.</summary>
    Guid? CorrelationId { get; }

    /// <summary>So'rov manzili (proksi sarlavhalari allaqachon qo'llangan).</summary>
    string? IpAddress { get; }

    /// <summary>Brauzer/mijoz — birinchi 300 belgi.</summary>
    string? UserAgent { get; }
}
