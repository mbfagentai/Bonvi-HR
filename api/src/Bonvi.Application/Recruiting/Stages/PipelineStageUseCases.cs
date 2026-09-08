using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Stages;

/// <summary>Voronka bosqichlari ro'yxati — DOIM voronka tartibida.</summary>
/// <param name="OnlyActive">
/// <c>true</c> — faqat faol bosqichlar (nomzodni o'tkazish uchun tanlash
/// ro'yxati). Boshqarish sahifasi hammasini so'raydi, aks holda
/// faolsizlantirilgan bosqichni qayta yoqib bo'lmasdi.
/// </param>
public sealed record ListPipelineStagesQuery(bool OnlyActive = false);

/// <summary>Yangi bosqich — voronkaning OXIRIGA qo'shiladi.</summary>
/// <remarks>
/// Tartib so'ralmaydi: yangi bosqichni o'rtaga qo'yish kerak bo'lsa,
/// qo'shib bo'lib sudrab suriladi (<see cref="ReorderPipelineStagesCommand"/>).
/// Yaratish formasida tartib raqami so'ralsa, HR uni boshqa yozuvlarnikidan
/// mustaqil kiritib, ikkita bosqich bir xil raqamda qolishi mumkin edi.
/// </remarks>
public sealed record CreatePipelineStageCommand(
    string? Name,
    string? Note = null,
    bool InvitesToInterview = false);

/// <summary>Bosqich nomini va izohini tuzatadi. Tartib BU YERDA o'zgarmaydi.</summary>
public sealed record UpdatePipelineStageCommand(
    Guid StageId,
    string? Name,
    string? Note = null,
    bool InvitesToInterview = false);

/// <summary>
/// Voronkani qayta tartiblaydi.
/// </summary>
/// <remarks>
/// Yangi tartib — ro'yxatdagi o'rin: birinchi identifikator 0, ikkinchisi 1…
/// Mijoz YAKUNIY ketma-ketlikni yuboradi, «qaysi biri qayerga surildi» ni
/// hisoblash uning ishi emas (<c>ReorderReferenceCommand</c> bilan bir xil
/// naqsh). BARCHA bosqichlar ro'yxatda bo'lishi shart — yarim ro'yxat
/// qolganlarini qayerga qo'yishni noaniq qoldirardi.
/// </remarks>
public sealed record ReorderPipelineStagesCommand(IReadOnlyList<Guid> OrderedIds);

/// <summary>Bosqichni faolsizlantiradi yoki qayta yoqadi.</summary>
/// <remarks>
/// O'chirish amali ATAYLAB yo'q: bosqich — ma'lumotnoma yozuvi va unga
/// nomzodlar tarixida havola qolgan (<c>ReferenceEntity</c> qoidasi).
/// </remarks>
public sealed record SetPipelineStageActiveCommand(Guid StageId, bool IsActive, string? Reason = null);

/// <summary>Voronka bosqichlarini o'qiydi — tartibi bilan.</summary>
public sealed class ListPipelineStagesQueryHandler
{
    private readonly IPipelineStageRepository _stages;

    public ListPipelineStagesQueryHandler(IPipelineStageRepository stages) => _stages = stages;

    public async Task<Result<IReadOnlyList<PipelineStageDto>>> HandleAsync(
        ListPipelineStagesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var stages = await _stages.ListAsync(query.OnlyActive, cancellationToken).ConfigureAwait(false);

        // Tartib repozitoriyda ham qo'yilgan, lekin bu yerda QAYTA
        // qat'iylashtiriladi — `ListReferenceQueryHandler` dagi bilan bir
        // xil qoida: ro'yxat foydalanuvchiga har safar bir xil ko'rinishi
        // kerak va bu kafolat ko'rsatish qatlamida turishi kerak.
        IReadOnlyList<PipelineStageDto> items =
        [
            .. stages.OrderBy(stage => stage.SortOrder).Select(RecruitingMapper.ToDto),
        ];

        return Result.Success(items);
    }
}

public sealed class CreatePipelineStageCommandHandler
{
    private readonly IPipelineStageRepository _stages;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePipelineStageCommandHandler(IPipelineStageRepository stages, IUnitOfWork unitOfWork)
    {
        _stages = stages;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PipelineStageDto>> HandleAsync(
        CreatePipelineStageCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Error.Validation("bosqich.nom_bosh", "Bosqich nomi kiritilishi shart.");
        }

        // Nom bandligi tekshiruvi bazadagi unikal indeksni ALMASHTIRMAYDI
        // (`CreateReferenceCommandHandler` dagi bilan bir xil sabab):
        // maqsad — odatiy holatda tushunarli xato berish, 500 emas.
        var taken = await _stages
            .NameExistsAsync(command.Name, null, cancellationToken)
            .ConfigureAwait(false);

        if (taken)
        {
            return RecruitingErrors.StageNameTaken;
        }

        // Yangi bosqich DOIM oxirida. Bironta bosqich bo'lmasa — birinchi
        // o'rin (0). Bo'shliqlar muhim emas: ahamiyatlisi nisbiy tartib.
        var max = await _stages.MaxSortOrderAsync(cancellationToken).ConfigureAwait(false);
        var next = max is { } value ? value + 1 : 0;

        var created = PipelineStage.Create(command.Name, next, command.InvitesToInterview);
        if (created.IsFailure)
        {
            return created.Error;
        }

        created.Value.SetNote(command.Note);

        _stages.Add(created.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return RecruitingMapper.ToDto(created.Value);
    }
}

public sealed class UpdatePipelineStageCommandHandler
{
    private readonly IPipelineStageRepository _stages;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePipelineStageCommandHandler(IPipelineStageRepository stages, IUnitOfWork unitOfWork)
    {
        _stages = stages;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PipelineStageDto>> HandleAsync(
        UpdatePipelineStageCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var stage = await _stages.GetByIdAsync(command.StageId, cancellationToken).ConfigureAwait(false);
        if (stage is null)
        {
            return RecruitingErrors.StageNotFound;
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Error.Validation("bosqich.nom_bosh", "Bosqich nomi kiritilishi shart.");
        }

        var taken = await _stages
            .NameExistsAsync(command.Name, command.StageId, cancellationToken)
            .ConfigureAwait(false);

        if (taken)
        {
            return RecruitingErrors.StageNameTaken;
        }

        var renamed = stage.Rename(command.Name);
        if (renamed.IsFailure)
        {
            return renamed.Error;
        }

        stage.SetNote(command.Note);
        stage.SetInvitesToInterview(command.InvitesToInterview);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return RecruitingMapper.ToDto(stage);
    }
}

/// <summary>
/// Voronkani qayta tartiblaydi — bitta so'rovda, butun ro'yxat bilan.
/// </summary>
/// <remarks>
/// Tartib 0 dan boshlab QAYTA raqamlanadi, mijozdan kelgan raqamlar
/// ishlatilmaydi: shunda ro'yxatda hech qachon teshik yoki takror
/// bo'lmaydi va keyingi «oxiriga qo'shish» amali to'g'ri ishlaydi.
/// </remarks>
public sealed class ReorderPipelineStagesCommandHandler
{
    private readonly IPipelineStageRepository _stages;
    private readonly IUnitOfWork _unitOfWork;

    public ReorderPipelineStagesCommandHandler(IPipelineStageRepository stages, IUnitOfWork unitOfWork)
    {
        _stages = stages;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<PipelineStageDto>>> HandleAsync(
        ReorderPipelineStagesCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var all = await _stages.ListAsync(onlyActive: false, cancellationToken).ConfigureAwait(false);

        // Ro'yxat TO'LIQ bo'lishi shart va takrorsiz. Yarim ro'yxat
        // yuborilsa, qolgan bosqichlar eski raqamlari bilan qolib,
        // voronkada ikkita bosqich bir o'rinda turib qolardi — va bu
        // hech qanday xato bermasdi.
        var requested = command.OrderedIds ?? [];
        var unique = new HashSet<Guid>(requested);

        if (unique.Count != requested.Count || unique.Count != all.Count || !all.All(s => unique.Contains(s.Id)))
        {
            return RecruitingErrors.ReorderIncomplete;
        }

        var byId = all.ToDictionary(stage => stage.Id);

        for (var index = 0; index < requested.Count; index++)
        {
            var moved = byId[requested[index]].MoveTo(index);
            if (moved.IsFailure)
            {
                return moved.Error;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<PipelineStageDto> items =
        [
            .. requested.Select(id => RecruitingMapper.ToDto(byId[id])),
        ];

        return Result.Success(items);
    }
}

/// <summary>
/// Bosqichni faolsizlantiradi yoki qayta yoqadi.
/// </summary>
/// <remarks>
/// <b>Bosqichda nomzodlar turgan bo'lsa ham TO'SILMAYDI.</b> Voronkani
/// qayta qurish — odatiy ish (masalan «Кенгаш» bosqichi bekor qilindi) va
/// uni «avval 12 ta nomzodni ko'chiring» degan talab bilan to'sish HR ni
/// tizimdan tashqarida ishlashga majburlardi. Nomzodlar joyida qoladi,
/// bosqich nomi ularning kartasida ko'rinib turadi, faqat YANGI o'tkazish
/// rad etiladi («bosqich.faol_emas»).
/// </remarks>
public sealed class SetPipelineStageActiveCommandHandler
{
    private readonly IPipelineStageRepository _stages;
    private readonly IUnitOfWork _unitOfWork;

    public SetPipelineStageActiveCommandHandler(IPipelineStageRepository stages, IUnitOfWork unitOfWork)
    {
        _stages = stages;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PipelineStageDto>> HandleAsync(
        SetPipelineStageActiveCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var stage = await _stages.GetByIdAsync(command.StageId, cancellationToken).ConfigureAwait(false);
        if (stage is null)
        {
            return RecruitingErrors.StageNotFound;
        }

        if (command.IsActive)
        {
            stage.Activate();
        }
        else
        {
            stage.Deactivate(command.Reason);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return RecruitingMapper.ToDto(stage);
    }
}
