using System.Text.Json;
using Bonvi.Domain.Auditing;

namespace Bonvi.Application.Audit;

/// <summary>Audit jurnalining bitta qatori — ro'yxatda va xodim kartasidagi tarixda (SPEC §8.3).</summary>
/// <param name="Id">Qator identifikatori.</param>
/// <param name="OccurredAt">Qachon (UTC; ekran Toshkent vaqtida ko'rsatadi).</param>
/// <param name="Action">Amal turi.</param>
/// <param name="Operation">Endpoint nomi (<c>TransferEmployee</c>) yoki tizim amali (<c>etl:employees</c>); eski qatorlarda <c>null</c>.</param>
/// <param name="Module">
/// Bo'lim kaliti — entity turidan HISOBLANADI (bazada saqlanmaydi): kirish hodisalari —
/// <c>users</c>, Excel eksporti — <c>reports</c>. Klassifikatsiyasiz eski tur — <c>null</c>.
/// </param>
/// <param name="EntityName">Entity nomi (<c>Employee</c>, <c>EmploymentEvent</c>) yoki hodisa obyekti (<c>Endpoint</c>).</param>
/// <param name="EntityId">Yozuv identifikatori.</param>
/// <param name="EntityLabel">Yozuvning o'sha lahzadagi nomi (xodim, nomzod, rol…).</param>
/// <param name="SubjectEmployeeId">O'zgarish tegishli xodim.</param>
/// <param name="Actor">Kim.</param>
/// <param name="Changes">Maydonlar: eski → yangi (ko'rish huquqi yo'qlari — <c>masked</c>).</param>
/// <param name="CorrelationId">Bitta harakat (so'rov) guruhi.</param>
public sealed record AuditEntryDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    AuditAction Action,
    string? Operation,
    string? Module,
    string EntityName,
    string? EntityId,
    string? EntityLabel,
    Guid? SubjectEmployeeId,
    AuditActorDto Actor,
    IReadOnlyList<AuditChangeDto> Changes,
    Guid? CorrelationId);

/// <summary>Bajaruvchi — o'sha lahzadagi nusxa.</summary>
/// <param name="Kind">Turi: odam, tizim, kirmagan so'rov, xodim ilovasi yoki qurilma.</param>
/// <param name="UserId">Tizim foydalanuvchisi.</param>
/// <param name="Login">Login nusxasi.</param>
/// <param name="Name">
/// F.I.Sh. — yozilgan lahzadagi nusxa. Kengaytirishdan oldingi qatorlarda nusxa yo'q va
/// foydalanuvchining hozirgi ismi beriladi.
/// </param>
/// <param name="RoleName">
/// Rol NOMI — o'sha lahzada qanday yozilgan bo'lsa (<c>actor_role</c>). Hozirgi nomi
/// ATAYLAB qo'yilmaydi: egasi rolni qayta nomlasa, tarix o'zgarib qolardi.
/// </param>
public sealed record AuditActorDto(
    AuditActorKind Kind,
    Guid? UserId,
    string? Login,
    string? Name,
    string? RoleName);

/// <summary>Bitta maydonning o'zgarishi.</summary>
/// <param name="Field">
/// Maydon nomi: EF xossasi (<c>DepartmentId</c>, <c>FullName.LastName</c>); pul — butunligicha
/// (<c>Salary</c>), kirish hodisasida — tafsilot kaliti (<c>reason</c>).
/// </param>
/// <param name="ValueType">Qiymat turi — <see cref="AuditValueTypes"/>.</param>
/// <param name="Old">Oldingi qiymat (yaratishda va yashirilganda <c>null</c>).</param>
/// <param name="New">Yangi qiymat (o'chirishda va yashirilganda <c>null</c>).</param>
/// <param name="OldLabel">Havola maydonida — oldingi nom (o'qish paytida, o'chirilganlari ham).</param>
/// <param name="NewLabel">Havola maydonida — yangi nom.</param>
/// <param name="Masked">
/// Qiymat ko'rsatilmadi: so'rovchida ruxsat yo'q (oklad, qo'shimcha haq, shaxsiy ma'lumot)
/// yoki maydon sir (parol xeshi, kalit). Maydon NOMI baribir ko'rinadi.
/// </param>
public sealed record AuditChangeDto(
    string Field,
    string ValueType,
    JsonElement? Old,
    JsonElement? New,
    string? OldLabel,
    string? NewLabel,
    bool Masked);

/// <summary>Bitta qator — IP, qurilma va shu harakatning boshqa qatorlari bilan.</summary>
/// <param name="Entry">Qatorning o'zi.</param>
/// <param name="IpAddress">So'rov kelgan manzil.</param>
/// <param name="UserAgent">Brauzer satri (eng ko'pi 300 belgi).</param>
/// <param name="Related">
/// Shu <c>correlation_id</c> dagi BOSHQA qatorlar — so'rovchi ko'ra oladiganlari, yozilish
/// tartibida, eng ko'pi <c>50</c> ta (ETL qadami bitta guruhda minglab qator yozadi).
/// </param>
public sealed record AuditEntryDetailsDto(
    AuditEntryDto Entry,
    string? IpAddress,
    string? UserAgent,
    IReadOnlyList<AuditEntryDto> Related);

/// <summary>«Kim» filtrining bitta tanlovi — jurnalda uchraydigan tizim foydalanuvchisi.</summary>
/// <param name="UserId">Foydalanuvchi.</param>
/// <param name="FullName">Hozirgi F.I.Sh.</param>
/// <param name="Login">Login (telefon).</param>
/// <param name="IsActive">Hisob faolmi (o'chirilgani ham ro'yxatda — tarixi bor).</param>
public sealed record AuditActorOptionDto(
    Guid UserId,
    string FullName,
    string Login,
    bool IsActive);

/// <summary><see cref="AuditChangeDto.ValueType"/> qiymatlari — interfeys qiymatni shunga qarab chizadi.</summary>
public static class AuditValueTypes
{
    /// <summary>Matn (qiymat obyektlari — telefon, JSHSHIR — ham).</summary>
    public const string Text = "text";

    /// <summary>Son.</summary>
    public const string Number = "number";

    /// <summary>Pul: <c>{ "amount": 4000000, "currency": "UZS" }</c>.</summary>
    public const string Money = "money";

    /// <summary>Sana — <c>yyyy-MM-dd</c>.</summary>
    public const string Date = "date";

    /// <summary>Lahza — ISO-8601 UTC.</summary>
    public const string DateTime = "datetime";

    /// <summary>Ha/yo'q.</summary>
    public const string Bool = "bool";

    /// <summary>Sanoq a'zosining NOMI (<c>Terminated</c>).</summary>
    public const string Enum = "enum";

    /// <summary>Havola — identifikator va nomi (<c>oldLabel</c>/<c>newLabel</c>).</summary>
    public const string Ref = "ref";

    /// <summary>Ro'yxat; havolalar ro'yxatida nomlar vergul bilan qo'shilib yorliqda keladi.</summary>
    public const string List = "list";

    /// <summary>Ichki JSON obyekt (masalan eksport filtrlari).</summary>
    public const string Json = "json";
}
