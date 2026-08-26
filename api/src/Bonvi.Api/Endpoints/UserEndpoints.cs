using Bonvi.Api.Extensions;
using Bonvi.Application.Identity.Users.Commands.ActivateUser;
using Bonvi.Application.Identity.Users.Commands.CreateUser;
using Bonvi.Application.Identity.Users.Commands.DeactivateUser;
using Bonvi.Application.Identity.Users.Commands.ResetUserPassword;
using Bonvi.Application.Identity.Users.Commands.UpdateUser;
using Bonvi.Application.Identity.Users.Dtos;
using Bonvi.Application.Identity.Users.Queries.ListUsers;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>Yangi foydalanuvchi yaratish so'rovi tanasi.</summary>
/// <param name="Login">Telefon raqami: 9 xona yoki +998 bilan.</param>
/// <param name="LastName">Familiya.</param>
/// <param name="FirstName">Ism.</param>
/// <param name="MiddleName">Otasining ismi (ixtiyoriy).</param>
/// <param name="Password">Boshlang'ich parol — administrator kiritadi, kamida 8 belgi.</param>
/// <param name="RoleCodes">Rol kodlari, masalan <c>["hr_manager"]</c>.</param>
public sealed record CreateUserRequest(
    string? Login,
    string? LastName,
    string? FirstName,
    string? MiddleName,
    string? Password,
    IReadOnlyList<string>? RoleCodes);

/// <summary>Foydalanuvchini tahrirlash so'rovi tanasi.</summary>
/// <param name="Login">Telefon raqami.</param>
/// <param name="LastName">Familiya.</param>
/// <param name="FirstName">Ism.</param>
/// <param name="MiddleName">Otasining ismi (ixtiyoriy).</param>
/// <param name="RoleCodes">Yangi TO'LIQ rol ro'yxati — eskisi almashtiriladi.</param>
public sealed record UpdateUserRequest(
    string? Login,
    string? LastName,
    string? FirstName,
    string? MiddleName,
    IReadOnlyList<string>? RoleCodes);

/// <summary>Parolni tiklash so'rovi tanasi.</summary>
/// <param name="NewPassword">Yangi parol — administrator kiritadi, kamida 8 belgi.</param>
public sealed record ResetUserPasswordRequest(string? NewPassword);

/// <summary>
/// Foydalanuvchilarni boshqarish endpoint'lari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Faqat administrator.</b> Har bir marshrut <c>users.manage</c> ruxsati
/// bilan himoyalangan — bu ruxsat faqat <c>admin</c> rolida bor
/// (<see cref="SystemRoles.Defaults"/>). HR menejeri bu bo'limga umuman
/// kira olmaydi: yangi hisob ochish, rol berish yoki boshqa birovning
/// parolini tiklash — administratorning ishi.
/// </para>
/// <para>
/// <b>Parol hech qachon javobda yo'q.</b> <see cref="UserSummaryDto"/> da
/// bunday maydon umuman mavjud emas — «unutib qoldirish» texnik jihatdan
/// mumkin emas.
/// </para>
/// </remarks>
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization(Permissions.UsersManage);

        group.MapGet("/", async (
                ListUsersQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .WithName("ListUsers")
            .WithSummary("Foydalanuvchilar — ro'yxat")
            .WithDescription(
                "Tizimga kiradigan barcha hisoblar: faol va faolsizlari ham, ism bo'yicha " +
                "alifbo tartibida. Sahifalash yo'q — hisoblar soni bir nechta.")
            .Produces<IReadOnlyList<UserSummaryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/", async (
                CreateUserRequest request,
                CreateUserCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new CreateUserCommand(
                            request.Login,
                            request.LastName,
                            request.FirstName,
                            request.MiddleName,
                            request.Password,
                            request.RoleCodes),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("CreateUser")
            .WithSummary("Foydalanuvchilar — yangi hisob")
            .WithDescription(
                "Parolni ADMINISTRATOR o'zi kiritadi (kamida 8 belgi) va uni yangi " +
                "foydalanuvchiga shaxsan yetkazadi — tizimda buni yuboradigan boshqa kanal yo'q. " +
                "Telefon raqami takrorlansa: «foydalanuvchi.login_band» (409).")
            .Produces<UserSummaryDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateUserRequest request,
                UpdateUserCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new UpdateUserCommand(
                            id,
                            request.Login,
                            request.LastName,
                            request.FirstName,
                            request.MiddleName,
                            request.RoleCodes),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("UpdateUser")
            .WithSummary("Foydalanuvchilar — tahrirlash")
            .WithDescription(
                "Ism, telefon va rollarni yangilaydi. Parol BU YERDA o'zgarmaydi — buning uchun " +
                "alohida «parolni tiklash» amali bor. Administrator o'zidan «admin» rolini olib " +
                "tashlay olmaydi: «foydalanuvchi.ozini_adminlikdan_chetlashtirish» (403). Tizimdagi " +
                "OXIRGI faol administratorning admin rolini olib tashlab bo'lmaydi: " +
                "«foydalanuvchi.oxirgi_administrator» (409).")
            .Produces<UserSummaryDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/deactivate", async (
                Guid id,
                DeactivateUserCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeactivateUserCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("DeactivateUser")
            .WithSummary("Foydalanuvchilar — faolsizlantirish")
            .WithDescription(
                "Hisob o'chirilmaydi, faqat kirish yopiladi. Administrator O'ZINI faolsizlantira " +
                "olmaydi: «foydalanuvchi.ozini_blokash» (403). Tizimdagi OXIRGI faol administratorni " +
                "ham faolsizlantirib bo'lmaydi: «foydalanuvchi.oxirgi_administrator» (409).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/activate", async (
                Guid id,
                ActivateUserCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ActivateUserCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("ActivateUser")
            .WithSummary("Foydalanuvchilar — qayta faollashtirish")
            .WithDescription("Faolsizlantirilgan hisobni qaytaradi. Bloklash hisoblagichi ham tozalanadi.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/reset-password", async (
                Guid id,
                ResetUserPasswordRequest request,
                ResetUserPasswordCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ResetUserPasswordCommand(id, request.NewPassword), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("ResetUserPassword")
            .WithSummary("Foydalanuvchilar — parolni tiklash")
            .WithDescription(
                "Yangi parolni ADMINISTRATOR kiritadi (kamida 8 belgi) va uni shaxsan yetkazadi. " +
                "Foydalanuvchining BARCHA mavjud sessiyalari darhol bekor qilinadi — eski parol " +
                "bilan olingan tokenlar endi ishlamaydi.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
