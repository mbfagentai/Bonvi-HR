using Bonvi.Api.Common;
using Bonvi.Api.Extensions;
using Bonvi.Application.Common.Photos;
using Bonvi.Application.Identity.Users.Commands.ActivateUser;
using Bonvi.Application.Identity.Users.Commands.CreateUser;
using Bonvi.Application.Identity.Users.Commands.DeactivateUser;
using Bonvi.Application.Identity.Users.Commands.ResetUserPassword;
using Bonvi.Application.Identity.Users.Commands.UpdateUser;
using Bonvi.Application.Identity.Users.Dtos;
using Bonvi.Application.Identity.Users.Photos;
using Bonvi.Application.Identity.Users.Queries.ListUsers;
using Bonvi.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bonvi.Api.Endpoints;

/// <summary>Yangi foydalanuvchi yaratish so'rovi tanasi.</summary>
/// <param name="Login">Telefon raqami: 9 xona yoki +998 bilan.</param>
/// <param name="LastName">Familiya.</param>
/// <param name="FirstName">Ism.</param>
/// <param name="MiddleName">Otasining ismi (ixtiyoriy).</param>
/// <param name="Password">Boshlang'ich parol — administrator kiritadi, kamida 8 belgi.</param>
/// <param name="RoleCode">Rol kodi, masalan <c>"hr_manager"</c>. Aynan BITTA.</param>
public sealed record CreateUserRequest(
    string? Login,
    string? LastName,
    string? FirstName,
    string? MiddleName,
    string? Password,
    string? RoleCode);

/// <summary>Foydalanuvchini tahrirlash so'rovi tanasi.</summary>
/// <param name="Login">Telefon raqami.</param>
/// <param name="LastName">Familiya.</param>
/// <param name="FirstName">Ism.</param>
/// <param name="MiddleName">Otasining ismi (ixtiyoriy).</param>
/// <param name="RoleCode">Yangi rol kodi — eskisi almashtiriladi.</param>
public sealed record UpdateUserRequest(
    string? Login,
    string? LastName,
    string? FirstName,
    string? MiddleName,
    string? RoleCode);

/// <summary>Parolni tiklash so'rovi tanasi.</summary>
/// <param name="NewPassword">Yangi parol — administrator kiritadi, kamida 8 belgi.</param>
public sealed record ResetUserPasswordRequest(string? NewPassword);

/// <summary>
/// Foydalanuvchilarni boshqarish endpoint'lari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Faqat rahbariyat.</b> Har bir marshrut <c>users.manage</c> ruxsati
/// bilan himoyalangan — bu ruxsat super administrator va bosh rahbarda bor
/// (<see cref="SystemRoles.Defaults"/>). HR menejeri bu bo'limga umuman
/// kira olmaydi: yangi hisob ochish, rol berish yoki boshqa birovning
/// parolini tiklash — rahbariyatning ishi.
/// </para>
/// <para>
/// <b>Ruxsat KIRISHNI ochadi, «kimga tegish»ni emas.</b> Super administrator
/// va bosh rahbar ruxsatlar bo'yicha TENG: ikkalasida ham
/// <c>users.manage</c> bor va ikkalasi ham shu marshrutlarga yetadi. Farq
/// so'rovning MAZMUNIDA — bosh rahbar faqat HR menejerlarga tega oladi.
/// Bunday tekshiruvni <c>RequireAuthorization</c> qila olmaydi (u so'rov
/// tanasini ham, nishonni ham ko'rmaydi), shu sababli u use-case qatlamida
/// (<c>UserRoleRules</c>) va javobda 403 <c>foydalanuvchi.rolga_haqqi_yoq</c>
/// sifatida qaytadi.
/// </para>
/// <para>
/// <b>Parol hech qachon javobda yo'q.</b> <see cref="UserSummaryDto"/> da
/// bunday maydon umuman mavjud emas — «unutib qoldirish» texnik jihatdan
/// mumkin emas.
/// </para>
/// </remarks>
public static class UserEndpoints
{
    /// <summary>
    /// Rasm so'rovi tanasining eng katta hajmi.
    /// </summary>
    /// <remarks>
    /// Faylning o'zi 10 MB bilan cheklangan (<see cref="PhotoLimits.MaxBytes"/>),
    /// lekin <c>multipart/form-data</c> tanasiga chegara satrlari va maydon
    /// sarlavhalari ham qo'shiladi. 64 KB zaxira aynan shu «konvert» uchun.
    /// </remarks>
    private const long MaxPhotoRequestBytes = PhotoLimits.MaxBytes + (64 * 1024);

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization(Permissions.UsersManage);

        group.MapGet("/", async (
                string? search,
                ListUsersQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListUsersQuery(search), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("ListUsers")
            .WithSummary("Foydalanuvchilar — ro'yxat")
            .WithDescription(
                "Tizimga kiradigan barcha hisoblar: faol va faolsizlari ham, ism bo'yicha " +
                "alifbo tartibida. Sahifalash yo'q — hisoblar soni bir nechta. Har bir yozuvda " +
                "«roleCode» (aynan bitta) va «photoKey» bo'ladi; rasmning o'zi alohida " +
                "«GET /api/users/{id}/photo» bilan olinadi. " +
                "«search» — F.I.Sh. yoki LOGIN (telefon) bo'yicha; hisobda JSHSHIR ham, " +
                "pasport ham yo'q, shu sababli qidiruv xodimlar ro'yxatinikidan torroq.")
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
                            request.RoleCode),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("CreateUser")
            .WithSummary("Foydalanuvchilar — yangi hisob")
            .WithDescription(
                "Parolni ADMINISTRATOR o'zi kiritadi (kamida 8 belgi) va uni yangi " +
                "foydalanuvchiga shaxsan yetkazadi — tizimda buni yuboradigan boshqa kanal yo'q. " +
                "Rol AYNAN BITTA («roleCode»). Bosh rahbar faqat «hr_manager» yarata oladi: " +
                "«foydalanuvchi.rolga_haqqi_yoq» (403). Super administrator va bosh rahbar " +
                "tizimda BITTADAN: «foydalanuvchi.super_admin_mavjud» / " +
                "«foydalanuvchi.bosh_rahbar_mavjud» (409). Telefon raqami takrorlansa: " +
                "«foydalanuvchi.login_band» (409).")
            .Produces<UserSummaryDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
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
                            request.RoleCode),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("UpdateUser")
            .WithSummary("Foydalanuvchilar — tahrirlash")
            .WithDescription(
                "Ism, telefon va rolni yangilaydi. Parol BU YERDA o'zgarmaydi — buning uchun " +
                "alohida «parolni tiklash» amali bor. Bosh rahbar super administratorga " +
                "tegolmaydi va HR menejerni rahbariyat roliga ko'tara olmaydi: " +
                "«foydalanuvchi.rolga_haqqi_yoq» (403). Hech kim O'Z rolini o'zgartira olmaydi: " +
                "«foydalanuvchi.oz_rolini_ozgartirish» (403). Rahbariyat roli tizimda " +
                "BITTADAN: «foydalanuvchi.super_admin_mavjud» / «foydalanuvchi.bosh_rahbar_mavjud» " +
                "(409). Tizimdagi OXIRGI faol rahbarni oddiy HR menejerga aylantirib bo'lmaydi: " +
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
                "Hisob o'chirilmaydi, faqat kirish yopiladi. Hech kim O'ZINI faolsizlantira " +
                "olmaydi: «foydalanuvchi.ozini_blokash» (403). Bosh rahbar super administratorga " +
                "tegolmaydi: «foydalanuvchi.rolga_haqqi_yoq» (403). Tizimdagi OXIRGI faol " +
                "rahbarni ham faolsizlantirib bo'lmaydi: «foydalanuvchi.oxirgi_administrator» (409). " +
                "Bo'shatilgan lavozimga yangi hisob qo'yish aynan shu yo'l bilan mumkin: " +
                "«atigi bitta» qoidasi faqat FAOL hisoblarni sanaydi.")
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
            .WithDescription(
                "Faolsizlantirilgan hisobni qaytaradi. Bloklash hisoblagichi ham tozalanadi. " +
                "Rahbariyat hisobi uchun «atigi bitta» qoidasi shu yerda ham tekshiriladi: " +
                "o'rniga yangi bosh rahbar qo'yilgan bo'lsa, eskisini qaytarib bo'lmaydi " +
                "(«foydalanuvchi.bosh_rahbar_mavjud», 409).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

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
                "bilan olingan tokenlar endi ishlamaydi. Parolni tiklash hisobni egallab olish " +
                "bilan barobar, shu sababli u ham «kim kimga tega oladi» qoidasi ostida: " +
                "«foydalanuvchi.rolga_haqqi_yoq» (403).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        MapPhoto(group);

        return app;
    }

    /// <summary>
    /// Boshqa foydalanuvchining surati: berish, yuklash, o'chirish.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega «/api/auth/me/photo» dan alohida.</b> U yerdagi uchala
    /// endpoint ham FAQAT so'rovchining o'z hisobiga tegadi va identifikator
    /// manzilda umuman qatnashmaydi — chegara tokendagi identifikatorning
    /// o'zi bilan qo'yilgan. Bu yerda esa administrator BOSHQA hisobga
    /// rasm qo'yadi, ya'ni chegara boshqacha: <c>users.manage</c> ruxsati
    /// + «kim kimga tega oladi» qoidasi. Ikki xil chegarani bitta marshrutga
    /// yig'ish har bir so'rovda «bu o'zimnikimi?» degan shart tug'dirardi.
    /// </para>
    /// <para>
    /// <b>O'qish YOZISHDAN kengroq.</b> GET faqat <c>users.manage</c> ni
    /// talab qiladi va rol qoidasini tekshirmaydi: ro'yxat baribir hamma
    /// hisobni qaytaradi, rasm esa o'sha ro'yxatning bir qismi (sabab
    /// <c>GetUserPhotoQueryHandler</c> izohida).
    /// </para>
    /// </remarks>
    private static void MapPhoto(IEndpointRouteBuilder group)
    {
        group.MapGet("/{id:guid}/photo", async (
                Guid id,
                HttpContext context,
                GetUserPhotoQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetUserPhotoQuery(id, PhotoHttp.ReadKnownVersion(context.Request)),
                        cancellationToken)
                    .ConfigureAwait(false);

                // JSON emas, BAYT qaytariladi — shu sababli `ToHttpResult()`
                // faqat xato yo'lida ishlatiladi: aks holda u oqimni obyekt
                // deb serializatsiya qilib yuborardi.
                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : PhotoHttp.Write(context, natija.Value);
            })
            .WithName("GetUserPhoto")
            .WithSummary("Foydalanuvchi — rasmi")
            .WithDescription(
                "Rasm baytlari (image/jpeg). «ETag» saqlash kalitidan olingan barqaror xesh; " +
                "«If-None-Match» mos kelsa 304 qaytadi. Kesh SHAXSIY (private, 1 kun). " +
                "Rasm yo'q bo'lsa: «foydalanuvchi.rasm_yoq» (404).")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/photo", async (
                Guid id,
                IFormFile file,
                UploadUserPhotoCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                await using var content = file.OpenReadStream();

                var natija = await handler
                    .HandleAsync(
                        new UploadUserPhotoCommand(id, content, file.ContentType, file.Length),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })

            // CSRF himoyasi bu yerda ORTIQCHA: sessiya cookie'da emas, JWT
            // sarlavhasida yuriladi va boshqa saytdagi forma uni qo'sha olmaydi.
            // Belgi qoldirilsa, minimal API forma so'rovini token yo'qligi
            // uchun rad etardi.
            .DisableAntiforgery()

            // Cheklov marshrut darajasida: chegaradan katta tana Kestrel
            // tomonidan umuman o'qilmaydi va serverga yuk tushmaydi.
            .WithMetadata(new RequestSizeLimitAttribute(MaxPhotoRequestBytes))
            .WithName("UploadUserPhoto")
            .WithSummary("Foydalanuvchi — rasm yuklash")
            .WithDescription(
                "«multipart/form-data», maydon nomi «file». JPEG, PNG yoki WebP, eng ko'pi 10 MB. " +
                "Server rasmni markazdan kvadrat qirqib 512×512 JPEG qiladi va eskisini o'chiradi. " +
                "Kim kimga rasm qo'ya olishi — tahrirlashdagi qoidaning AYNAN o'zi: " +
                "«foydalanuvchi.rolga_haqqi_yoq» (403). Xatolar: «foydalanuvchi.rasm_katta», " +
                "«foydalanuvchi.rasm_turi», «foydalanuvchi.rasm_yaroqsiz» (hammasi 400).")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<UserPhotoDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/photo", async (
                Guid id,
                DeleteUserPhotoCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .WithName("DeleteUserPhoto")
            .WithSummary("Foydalanuvchi — rasmni o'chirish")
            .WithDescription(
                "Fayl ham o'chadi. Amal IDEMPOTENT: rasm allaqachon yo'q bo'lsa ham 204 qaytadi — " +
                "so'rovning maqsadi rasm qolmasligi, u esa bajarilgan. Ruxsat esa " +
                "idempotentlikdan OLDIN tekshiriladi: «foydalanuvchi.rolga_haqqi_yoq» (403).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
