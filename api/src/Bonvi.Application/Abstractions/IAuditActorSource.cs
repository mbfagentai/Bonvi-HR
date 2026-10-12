using Bonvi.Domain.Auditing;

namespace Bonvi.Application.Abstractions;

/// <summary>
/// Joriy amalni KIM bajaryapti — audit yozuvining bajaruvchisi.
/// </summary>
/// <remarks>
/// <para>
/// Port faqat SHAXSNI aniqlaydi (tur va identifikatorlar). Ism va rol NOMI
/// esa bazadan, saqlash paytida, bitta so'rov bilan o'qiladi va qamrov
/// davomida eslab qolinadi — bu Infrastructure'ning ishi: port bazani bilmaydi.
/// </para>
/// <para>
/// HTTP amalga oshirilishi tokendan o'qiydi: HR tokeni → <see cref="AuditActorKind.User"/>,
/// xodim ilovasi tokeni → <see cref="AuditActorKind.EmployeeAccount"/>, qurilma
/// endpointi → <see cref="AuditActorKind.Device"/>, tokensiz so'rov →
/// <see cref="AuditActorKind.Anonymous"/>. HTTP'siz joyda — <see cref="AuditActorKind.System"/>.
/// </para>
/// </remarks>
public interface IAuditActorSource
{
    /// <summary>Joriy bajaruvchi.</summary>
    AuditPrincipal Current { get; }
}

/// <summary>Audit bajaruvchisining shaxsi — nomsiz (nomni saqlash qatlami qo'shadi).</summary>
/// <param name="Kind">Bajaruvchi turi.</param>
/// <param name="UserId">Tizim foydalanuvchisi (<see cref="AuditActorKind.User"/> da).</param>
/// <param name="Login">Tokendagi login — foydalanuvchi bazada topilmasa ham iz qolsin.</param>
/// <param name="EmployeeId">Xodim ilovasi egasi (<see cref="AuditActorKind.EmployeeAccount"/> da).</param>
public sealed record AuditPrincipal(
    AuditActorKind Kind,
    Guid? UserId = null,
    string? Login = null,
    Guid? EmployeeId = null)
{
    /// <summary>Tizimning o'zi.</summary>
    public static AuditPrincipal System { get; } = new(AuditActorKind.System);

    /// <summary>Kirmagan so'rov.</summary>
    public static AuditPrincipal Anonymous { get; } = new(AuditActorKind.Anonymous);

    /// <summary>Davomat qurilmasi.</summary>
    public static AuditPrincipal Device { get; } = new(AuditActorKind.Device);
}
