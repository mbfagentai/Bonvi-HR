using Bonvi.Application.Common.Documents;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Recruiting.Candidates;

/// <summary>Nomzodga rezyume faylini biriktirish.</summary>
/// <param name="CandidateId">Nomzod.</param>
/// <param name="Content">Yuklangan faylning oqimi.</param>
/// <param name="FileName">Mijoz yuborgan asl nom — faqat ko'rsatish uchun.</param>
/// <param name="SizeBytes">Faylning e'lon qilingan hajmi.</param>
public sealed record UploadCandidateResumeCommand(
    Guid CandidateId,
    Stream Content,
    string? FileName,
    long SizeBytes);

/// <summary>Rezyume faylini yuklab olish.</summary>
public sealed record GetCandidateResumeQuery(Guid CandidateId);

/// <summary>Rezyumeni olib tashlash.</summary>
public sealed record DeleteCandidateResumeCommand(Guid CandidateId);

/// <summary>Yuklab olinadigan rezyume fayli.</summary>
public sealed record CandidateResumeFile(string FileName, string ContentType, Stream Content);

/// <summary>
/// Rezyumeni qabul qiladi, mazmunini tekshiradi va saqlaydi.
/// </summary>
/// <remarks>
/// <para>
/// Fayl xodim hujjatlari bilan AYNAN bir quvurdan o'tadi
/// (<see cref="DocumentFileStore"/>): tur mazmunga qarab aniqlanadi
/// (kengaytmaga va mijoz e'lon qilgan sarlavhaga ishonilmaydi), chegara —
/// 20 MB. Ikkinchi quvur yozilsa, bir kun rezyumega ijro etiladigan fayl
/// yuklash mumkin bo'lib qolardi.
/// </para>
/// <para>
/// <b>Nomzodda BITTA rezyume bo'ladi.</b> Yangisi yuklansa, eskisi diskdan
/// olib tashlanadi. Sabab: hujjatlardan farqli (u yerda tarix kerak —
/// har buyruq alohida fayl), rezyume — nomzodning JORIY holati va uning
/// uchta versiyasini saqlash HR ga hech narsa bermaydi, faqat qaysi biri
/// yangi ekanini taxmin qilishga majbur qilardi.
/// </para>
/// </remarks>
public sealed class UploadCandidateResumeCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly DocumentFileStore _files;
    private readonly RecruitingMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public UploadCandidateResumeCommandHandler(
        ICandidateRepository candidates,
        DocumentFileStore files,
        RecruitingMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _files = files;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CandidateDto>> HandleAsync(
        UploadCandidateResumeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Arzon tekshiruv oldinda: noma'lum nomzod uchun faylni butunlay
        // o'qib, tekshirib o'tirishning ma'nosi yo'q.
        var candidate = await _candidates
            .GetWithStagesAsync(command.CandidateId, cancellationToken)
            .ConfigureAwait(false);

        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        var accepted = await _files
            .AcceptAsync(command.Content, command.SizeBytes, cancellationToken)
            .ConfigureAwait(false);

        if (accepted.Key is not { } key)
        {
            return accepted.Rejection == DocumentRejection.TooLarge
                ? RecruitingErrors.ResumeTooLarge
                : RecruitingErrors.ResumeInvalid;
        }

        var previousKey = candidate.ResumeFileKey;

        var attached = candidate.AttachResume(key, command.FileName, accepted.ContentType!, command.SizeBytes);
        if (attached.IsFailure)
        {
            // Fayl ALLAQACHON diskka saqlangan — domen uni rad etsa,
            // ortidan yetim fayl qolmasligi kerak.
            await _files.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
            return attached.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Eski fayl faqat SAQLASHDAN KEYIN o'chiriladi: saqlash yiqilsa,
        // yozuv eski kalitga ishora qilib qoladi va u fayl joyida turishi
        // kerak.
        if (previousKey is not null && previousKey != key)
        {
            await _files.DeleteAsync(previousKey, cancellationToken).ConfigureAwait(false);
        }

        return await _mapper.ToDtoAsync(candidate, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class GetCandidateResumeQueryHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly DocumentFileStore _files;

    public GetCandidateResumeQueryHandler(ICandidateRepository candidates, DocumentFileStore files)
    {
        _candidates = candidates;
        _files = files;
    }

    public async Task<Result<CandidateResumeFile>> HandleAsync(
        GetCandidateResumeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var candidate = await _candidates.GetByIdAsync(query.CandidateId, cancellationToken).ConfigureAwait(false);
        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        if (candidate.ResumeFileKey is not { } key)
        {
            return RecruitingErrors.ResumeNotFound;
        }

        var content = await _files.OpenAsync(key, cancellationToken).ConfigureAwait(false);

        // Yozuv bor, fayl yo'q — bu buzilgan holat, lekin mijozga u
        // «rezyume yo'q» bo'lib ko'rinishi kerak: 500 xatosi bu yerda
        // foydalanuvchiga hech narsa aytmaydi.
        return content is null
            ? RecruitingErrors.ResumeNotFound
            : new CandidateResumeFile(
                candidate.ResumeFileName ?? "rezyume",
                candidate.ResumeContentType ?? "application/octet-stream",
                content);
    }
}

public sealed class DeleteCandidateResumeCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly DocumentFileStore _files;
    private readonly RecruitingMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCandidateResumeCommandHandler(
        ICandidateRepository candidates,
        DocumentFileStore files,
        RecruitingMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _files = files;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CandidateDto>> HandleAsync(
        DeleteCandidateResumeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var candidate = await _candidates
            .GetWithStagesAsync(command.CandidateId, cancellationToken)
            .ConfigureAwait(false);

        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        var key = candidate.ResumeFileKey;

        candidate.RemoveResume();
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Bu yerda fayl HAQIQATAN o'chiriladi (yozuv o'chirilganidan
        // farqli): foydalanuvchi ataylab «rezyumeni olib tashla» dedi va
        // undan keyin faylga hech kim ishora qilmaydi.
        if (key is not null)
        {
            await _files.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
        }

        return await _mapper.ToDtoAsync(candidate, cancellationToken).ConfigureAwait(false);
    }
}
