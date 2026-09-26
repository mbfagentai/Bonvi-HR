using System.Globalization;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Sinov hisobi: foydalanuvchi, uning yagona roli va tayyor access token.</summary>
/// <param name="UserId">Hisob.</param>
/// <param name="RoleId">Hisobning roli — faqat shu hisob uchun yaratilgan.</param>
/// <param name="RoleCode">Rol kodi.</param>
/// <param name="Login">Telefon raqami (9 xona).</param>
/// <param name="Password">Parol — login orqali kirish kerak bo'lsa.</param>
/// <param name="AccessToken">Shu hisob uchun berilgan access token.</param>
public sealed record TestAccount(
    Guid UserId,
    Guid RoleId,
    string RoleCode,
    string Login,
    string Password,
    string AccessToken);

/// <summary>
/// Kerakli ruxsatlar to'plamiga ega HAQIQIY hisob yasaydi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega ilgarigi usul endi ishlamaydi.</b> Testlar «ruxsati kam
/// foydalanuvchi» ni shunday yasar edi: administrator hisobi uchun token,
/// lekin ichida o'ylab topilgan ruxsatlar ro'yxati. 2026-09-26 dan token
/// tekshiruvi har so'rovda rol va ruxsatlarni BAZADAN oladi (rol tahriri
/// darhol kuchga kirishi uchun) — ya'ni tokendagi ro'yxat e'tiborsiz qoladi
/// va administrator o'zining to'liq ruxsatlari bilan chiqib qolardi.
/// </para>
/// <para>
/// Endi har chaqiruv o'z rolini (aynan so'ralgan ruxsatlar bilan) va o'z
/// hisobini yaratadi. Bu ilgarigidan ham halolroq: sinov endi haqiqiy
/// foydalanuvchi ko'radigan yo'ldan o'tadi.
/// </para>
/// </remarks>
internal static class TestAccounts
{
    /// <summary>Sinov hisoblarining umumiy paroli.</summary>
    public const string Password = "Sinov_Hisob_2026";

    private static int _hisoblagich;

    /// <summary>Faqat shu ruxsatlarga ega yangi hisobning access tokeni.</summary>
    /// <remarks>Ruxsatsiz hisob uchun — argumentsiz chaqiriladi.</remarks>
    public static async Task<string> TokenAsync(BonviApiFactory factory, params string[] permissions)
        => (await CreateAsync(factory, permissions).ConfigureAwait(false)).AccessToken;

    /// <summary>Faqat shu ruxsatlarga ega yangi hisob (rol bilan birga).</summary>
    public static async Task<TestAccount> CreateAsync(BonviApiFactory factory, params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        var xeshlovchi = qamrov.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var tokenlar = qamrov.ServiceProvider.GetRequiredService<ITokenService>();

        var belgi = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8];

        var rol = Role.Create("test_" + belgi, "Sinov roli " + belgi).Value;
        rol.ReplacePermissions(permissions).IsSuccess.ShouldBeTrue("sinov roli uchun notanish ruxsat");

        var login = Telefon();
        var hisob = User.Create(
            PhoneNumber.Create(login).Value,
            xeshlovchi.Hash(Password),
            FullName.Create("Синов", "Ҳисоб", null).Value).Value;

        hisob.SetRole(rol.Id);

        kontekst.Roles.Add(rol);
        kontekst.Users.Add(hisob);
        await kontekst.SaveChangesAsync(bekorQilish);

        var token = tokenlar
            .CreateTokens(hisob, [rol.Code], rol.Permissions.ToHashSet(StringComparer.Ordinal))
            .AccessToken;

        return new TestAccount(hisob.Id, rol.Id, rol.Code, login, Password, token);
    }

    /// <summary>
    /// Har chaqiruvda BOSHQA raqam: «97» + tasodifiy besh xona + hisoblagich.
    /// </summary>
    /// <remarks>
    /// Boshqa testlar «90…» va «9xxxxxx…» raqamlarini ishlatadi — «97»
    /// ular bilan to'qnashmasligi uchun. Oxirgi ikki xona hisoblagich:
    /// bir yig'ma ichida takrorlanmaydi.
    /// </remarks>
    private static string Telefon()
        => "97"
           + Random.Shared.Next(10_000, 99_999).ToString(CultureInfo.InvariantCulture)
           + (Interlocked.Increment(ref _hisoblagich) % 100).ToString("D2", CultureInfo.InvariantCulture);
}
