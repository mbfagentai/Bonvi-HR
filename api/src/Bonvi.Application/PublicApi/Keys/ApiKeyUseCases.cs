using Bonvi.Application.Abstractions;
using Bonvi.Application.PublicApi.Common;
using Bonvi.Application.PublicApi.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.PublicApi;

namespace Bonvi.Application.PublicApi.Keys;

/// <summary>Kalitlar ro'yxati — sozlamalardagi «Tashqi API» jadvali.</summary>
public sealed record ListApiKeysQuery;

/// <summary>
/// Barcha kalitlarni qaytaradi — bekor qilinganlar bilan birga.
/// </summary>
/// <remarks>
/// Bekor qilinganlar ham ro'yxatda: ular tarix va ekranda kulrang bo'lib
/// turadi (SPEC §6). Kalitning O'ZI hech qachon qaytmaydi — DTO da unday
/// maydon yo'q.
/// </remarks>
public sealed class ListApiKeysQueryHandler
{
    private readonly IApiKeyRepository _keys;
    private readonly IDepartmentRepository _departments;

    public ListApiKeysQueryHandler(IApiKeyRepository keys, IDepartmentRepository departments)
    {
        _keys = keys;
        _departments = departments;
    }

    public async Task<Result<IReadOnlyList<ApiKeyDto>>> HandleAsync(
        ListApiKeysQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var found = await _keys.ListAsync(cancellationToken).ConfigureAwait(false);

        // Bo'lim kalitining bo'lim NOMI — ro'yxatda «qaysi zavod uchun» darhol ko'rinsin
        var names = found.Any(key => key.DepartmentId is not null)
            ? (await _departments.ListAsync(onlyActive: false, cancellationToken).ConfigureAwait(false))
                .ToDictionary(department => department.Id, department => department.Name)
            : [];

        IReadOnlyList<ApiKeyDto> items =
        [
            .. found.Select(key => ToDto(key, key.DepartmentId is { } id ? names.GetValueOrDefault(id) : null)),
        ];

        return Result.Success(items);
    }

    internal static ApiKeyDto ToDto(ApiKey key, string? departmentName = null) => new(
        key.Id,
        key.Name,
        key.KeyPrefix,
        key.CreatedAt,
        key.LastUsedAt,
        key.LastUsedIp,
        key.RevokedAt,
        !key.IsRevoked,
        key.Scope,
        key.DepartmentId,
        departmentName);
}

/// <summary>Yangi kalit yaratish.</summary>
/// <param name="Name">Qaysi loyihaga beriladi («BonviCall»).</param>
/// <param name="Scope">Doira; <c>null</c> — <see cref="ApiKeyScope.Basic"/>.</param>
/// <param name="DepartmentId">Bo'lim davomati kalitida — bo'lim (shart); boshqa doiralarda e'tiborsiz.</param>
public sealed record CreateApiKeyCommand(string? Name, ApiKeyScope? Scope = null, Guid? DepartmentId = null);

/// <summary>
/// Kalit yaratadi va uni <b>bir marta</b> qaytaradi.
/// </summary>
/// <remarks>
/// <para>
/// Qadamlar tartibi muhim: avval nom BAND EMASLIGI tekshiriladi, keyin
/// kalit yasaladi. Teskari tartibda kriptografik tasodif bekorga
/// sarflanardi — kichik narx, lekin «tekshirmasdan yaratish» naqshi
/// keyingi o'quvchiga noto'g'ri namuna bo'lardi.
/// </para>
/// <para>
/// ⚠️ Nomning unikalligi IKKI joyda qo'riqlanadi: shu yerda (mijozga
/// tushunarli 409 berish uchun) va bazadagi qisman unikal indeksda
/// (ikkita so'rov bir vaqtda kelgan holat uchun). Faqat birinchisi
/// bo'lsa — poyga; faqat ikkinchisi bo'lsa — mijoz 500 olardi.
/// </para>
/// </remarks>
public sealed class CreateApiKeyCommandHandler
{
    private readonly IApiKeyRepository _keys;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IDepartmentRepository _departments;

    public CreateApiKeyCommandHandler(
        IApiKeyRepository keys,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IDepartmentRepository departments)
    {
        _keys = keys;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _departments = departments;
    }

    public async Task<Result<ApiKeyCreatedDto>> HandleAsync(
        CreateApiKeyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // «O'zingda yo'q huquqni bera olmaysan»: to'liq kalit barcha «ko'rish» ruxsatlari
        // bilan ishlaydi (oklad ham), «sozlamalarni boshqarish»ning o'zi esa ularni
        // bermaydi — maxsus rolga faqat shu ruxsat berilgan odam kalit orqali o'zida
        // yo'q ma'lumotni o'qiy olmasligi kerak (rol muharriridagi `rol.ortiqcha_ruxsat`).
        if (command.Scope == ApiKeyScope.FullRead
            && !ApiKeyScopes.RequiredToIssueFullRead.All(_currentUser.HasPermission))
        {
            return PublicApiErrors.FullReadNotAllowed;
        }

        // Bo'lim kaliti davomat va telefonni tashqariga beradi — beruvchi ularni o'zi ko'ra olsin
        if (command.Scope == ApiKeyScope.DepartmentAttendance)
        {
            if (!ApiKeyScopes.RequiredToIssueDepartmentAttendance.All(_currentUser.HasPermission))
            {
                return PublicApiErrors.DepartmentKeyNotAllowed;
            }

            if (command.DepartmentId is { } departmentId
                && await _departments.GetByIdAsync(departmentId, cancellationToken).ConfigureAwait(false) is null)
            {
                return PublicApiErrors.DepartmentNotFound;
            }
        }

        var name = command.Name?.Trim();

        if (!string.IsNullOrWhiteSpace(name)
            && await _keys.ActiveNameExistsAsync(name, cancellationToken).ConfigureAwait(false))
        {
            return PublicApiErrors.NameTaken;
        }

        var secret = ApiKeyFormat.Generate();

        var created = ApiKey.Issue(name, secret, command.Scope ?? ApiKeyScope.Basic, command.DepartmentId);
        if (created.IsFailure)
        {
            return created.Error;
        }

        _keys.Add(created.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Kalit matni FAQAT shu javobda. Bazada u yo'q (xesh bor), logda
        // ham yo'q — bu tizimda u ko'rinadigan yagona nuqta.
        return Result.Success(new ApiKeyCreatedDto(
            created.Value.Id,
            created.Value.Name,
            created.Value.KeyPrefix,
            secret.PlainKey,
            created.Value.Scope,
            created.Value.DepartmentId));
    }
}

/// <summary>Kalitni bekor qilish.</summary>
/// <param name="Id">Kalit yozuvi.</param>
public sealed record RevokeApiKeyCommand(Guid Id);

/// <summary>
/// Kalitni bekor qiladi — yozuv o'chirilmaydi.
/// </summary>
/// <remarks>
/// IDEMPOTENT: allaqachon bekor qilingan kalitda 200 qaytadi, xato emas
/// (SPEC §6 va <see cref="ApiKey.Revoke"/> izohi). Javobda YANGILANGAN
/// qator qaytadi — ekran uni darhol kulrangga o'tkazadi va ikkinchi
/// so'rov yubormaydi.
/// </remarks>
public sealed class RevokeApiKeyCommandHandler
{
    private readonly IApiKeyRepository _keys;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public RevokeApiKeyCommandHandler(
        IApiKeyRepository keys,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser)
    {
        _keys = keys;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<Result<ApiKeyDto>> HandleAsync(
        RevokeApiKeyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var key = await _keys.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (key is null)
        {
            return PublicApiErrors.KeyNotFound;
        }

        key.Revoke(_clock.UtcNow, _currentUser.UserId);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(ListApiKeysQueryHandler.ToDto(key));
    }
}
