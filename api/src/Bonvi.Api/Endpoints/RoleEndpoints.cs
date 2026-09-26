using Bonvi.Api.Extensions;
using Bonvi.Application.Identity.Roles;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>Rol yaratish yoki tahrirlash so'rovi tanasi.</summary>
/// <param name="Name">Egasi yozgan nom — unikal (katta-kichik harfsiz), eng ko'pi 100 belgi.</param>
/// <param name="Permissions">Rolning ruxsat kodlari — TO'LIQ ro'yxat (tahrirda eskisi almashtiriladi).</param>
public sealed record SaveRoleRequest(string? Name, IReadOnlyList<string?>? Permissions);

/// <summary>
/// Rollar muharriri — «Rollar» menyusi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Egasining qarori (2026-09-26):</b> rollarni u o'zi yaratadi va
/// nomlaydi; har bir o'zgarish (yaratish, nom, ruxsatlar, o'chirish)
/// deploy va qayta ishga tushirishsiz kuchga kiradi — rol egalarining
/// KEYINGI so'rovidayoq (token tekshiruvi ruxsatlarni bazadan oladi,
/// <c>AuthenticationExtensions.ApplyCurrentAccessAsync</c>).
/// </para>
/// <para>
/// <b>Ikki xil chegara.</b> Ro'yxat <c>users.manage</c> YOKI
/// <c>roles.manage</c> bilan ochiladi — foydalanuvchi formasidagi rol
/// tanlash ham undan quriladi. Yozish amallari va ruxsatlar xaritasi esa
/// faqat <c>roles.manage</c> bilan.
/// </para>
/// <para>
/// Rolni kimga BERISH bu yerda emas — u foydalanuvchilar bo'limida
/// (<c>/api/users</c>, <c>roleCode</c>) va o'z qoidasi bilan
/// (<c>UserRoleRules</c>).
/// </para>
/// </remarks>
public static class RoleEndpoints
{
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/roles").WithTags("Roles");

        group.MapGet("/", async (
                ListRolesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(AuthenticationExtensions.RolesReadPolicy)
            .WithName("ListRoles")
            .WithSummary("Rollar — ro'yxat")
            .WithDescription(
                "Barcha rollar: nomi, ruxsatlari va nechta hisobga berilgani («userCount» — " +
                "faol va faolsizlari ham). Telegram botning texnik roli bu ro'yxatda YO'Q. " +
                "Tartib: himoyalangan rol («isProtected», super administrator) birinchi, " +
                "qolganlari nom bo'yicha. «users.manage» YOKI «roles.manage» ruxsati bilan " +
                "ochiladi: foydalanuvchi formasidagi rol tanlash ham shu ro'yxatdan quriladi.")
            .Produces<IReadOnlyList<RoleDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/catalog", () => GetPermissionCatalogQuery.Handle().ToHttpResult())
            .RequireAuthorization(Permissions.RolesManage)
            .WithName("GetPermissionCatalog")
            .WithSummary("Rollar — ruxsatlar xaritasi")
            .WithDescription(
                "Rollar muharriri shundan quriladi: har bir bo'lim («key») va unda BOR darajalar " +
                "(«levels»: «view» | «edit» | «full»; har birining ro'yxati kumulyativ). " +
                "Mehnat bo'limida alohida beriladigan amallar ham bor («actions»); amali yo'q " +
                "bo'limda bu maydon javobda umuman bo'lmaydi. «extras» — bironta bo'limga " +
                "tegishli bo'lmagan ruxsatlar (masalan «salary.view»). Har bir ruxsat kodi " +
                "xaritada AYNAN BIR MARTA uchraydi. Sarlavhalar yuborilmaydi — interfeys " +
                "kalit bo'yicha tarjima qiladi.")
            .Produces<PermissionCatalogDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/", async (
                SaveRoleRequest request,
                CreateRoleCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new CreateRoleCommand(request.Name, request.Permissions), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToCreatedResult(natija.IsSuccess ? $"/api/roles/{natija.Value.Id}" : string.Empty);
            })
            .RequireAuthorization(Permissions.RolesManage)
            .WithName("CreateRole")
            .WithSummary("Rollar — yangi rol")
            .WithDescription(
                "Kod avtomatik beriladi («custom_…») va ekranda ko'rsatilmaydi. Xatolar: " +
                "«rol.nom_bosh» / «rol.nom_uzun» (100 belgidan ortiq) / «rol.notanish_ruxsat» (400); " +
                "o'zingizda yo'q ruxsat — «rol.ortiqcha_ruxsat», bunday nom band — " +
                "«rol.nom_band» (409; katta-kichik harf va chetdagi probellar farq qilmaydi).")
            .Produces<RoleDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                SaveRoleRequest request,
                UpdateRoleCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new UpdateRoleCommand(id, request.Name, request.Permissions), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RolesManage)
            .WithName("UpdateRole")
            .WithSummary("Rollar — tahrirlash")
            .WithDescription(
                "Nom va ruxsatlar TO'LIQ almashtiriladi. O'zgarish shu rol egalarining KEYINGI " +
                "so'rovidayoq kuchga kiradi — qayta kirish shart emas. Xatolar: «rol.topilmadi» " +
                "(404); super administrator roli — «rol.himoyalangan», o'zingiz egallab turgan " +
                "rol — «rol.oz_roli», o'zingizda yo'q ruxsat — «rol.ortiqcha_ruxsat», band nom — " +
                "«rol.nom_band» (hammasi 409); «rol.nom_bosh» / «rol.nom_uzun» / " +
                "«rol.notanish_ruxsat» (400).")
            .Produces<RoleDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                DeleteRoleCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeleteRoleCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RolesManage)
            .WithName("DeleteRole")
            .WithSummary("Rollar — o'chirish")
            .WithDescription(
                "Faqat hech bir hisobga (faol yoki faolsiz) berilmagan rol o'chadi: aks holda " +
                "«rol.foydalanuvchisi_bor» (409). Himoyalangan rol — «rol.himoyalangan», " +
                "o'zingiz egallab turgan rol — «rol.oz_roli» (409). Kimning qaysi rolni " +
                "o'chirgani audit jurnalida qoladi.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
