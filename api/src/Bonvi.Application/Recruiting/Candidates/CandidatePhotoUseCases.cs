using Bonvi.Application.Common.Photos;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Recruiting.Candidates;

/// <summary>Nomzod suratini yuklash buyrug'i.</summary>
/// <param name="CandidateId">Nomzod.</param>
/// <param name="Content">Yuklangan faylning oqimi.</param>
/// <param name="ContentType">Mijoz e'lon qilgan MIME turi.</param>
/// <param name="SizeBytes">Faylning e'lon qilingan hajmi.</param>
public sealed record UploadCandidatePhotoCommand(
    Guid CandidateId,
    Stream Content,
    string? ContentType,
    long SizeBytes);

/// <summary>Nomzod suratini o'qish so'rovi.</summary>
/// <param name="CandidateId">Nomzod.</param>
/// <param name="KnownVersion">
/// Mijozdagi nusxaning versiya belgisi. Serverdagi bilan mos kelsa, rasm
/// qayta yuborilmaydi (HTTP darajasida bu <c>If-None-Match</c>).
/// </param>
public sealed record GetCandidatePhotoQuery(Guid CandidateId, string? KnownVersion = null);

/// <summary>Nomzod suratini olib tashlash.</summary>
public sealed record DeleteCandidatePhotoCommand(Guid CandidateId);

/// <summary>
/// Nomzod suratini qabul qiladi, kvadrat qilib kichraytiradi va saqlaydi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Quvur xodim rasmi bilan AYNAN bitta</b>
/// (<see cref="SquarePhotoStore"/>): tekshirish, markazdan qirqish va
/// 512×512 JPEG'ga keltirish o'sha yerda. Bu handler faqat NOMZODGA
/// tegishli qismini bajaradi — kim uchun, kalit qayerga yoziladi va
/// eski fayl bilan nima bo'ladi. Ikkinchi quvur yozilsa, xavfsizlik
/// tuzatishi ikki joyda kerak bo'lardi va biri unutilardi.
/// </para>
/// <para>
/// Eski fayl faqat ma'lumotlar bazasi muvaffaqiyatli yangilangandan KEYIN
/// o'chiriladi: teskari tartibda saqlash yiqilsa, yozuv mavjud bo'lmagan
/// faylga ishora qilib qolardi.
/// </para>
/// </remarks>
public sealed class UploadCandidatePhotoCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly SquarePhotoStore _photos;
    private readonly IUnitOfWork _unitOfWork;

    public UploadCandidatePhotoCommandHandler(
        ICandidateRepository candidates,
        SquarePhotoStore photos,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _photos = photos;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CandidatePhotoDto>> HandleAsync(
        UploadCandidatePhotoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Arzon tekshiruv oldinda: noma'lum nomzod uchun rasmni ochib,
        // kichraytirib o'tirishning ma'nosi yo'q (rezyume yuklashdagi
        // bilan bir xil tartib).
        var candidate = await _candidates.GetByIdAsync(command.CandidateId, cancellationToken).ConfigureAwait(false);
        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        var accepted = await _photos
            .AcceptAsync(command.Content, command.ContentType, command.SizeBytes, cancellationToken)
            .ConfigureAwait(false);

        if (accepted.Key is not { } key)
        {
            return RecruitingErrors.PhotoRejected(accepted.Rejection!.Value);
        }

        var previousKey = candidate.PhotoKey;
        candidate.SetPhoto(key);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (previousKey is not null)
        {
            await _photos.DeleteAsync(previousKey, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new CandidatePhotoDto(key));
    }
}

/// <summary>
/// Nomzod suratini qaytaradi.
/// </summary>
/// <remarks>
/// Kalitni topish — shu handler ishi, faylni ochish va versiyani
/// solishtirish esa <see cref="SquarePhotoStore"/> niki. Fayl yo'qolgan
/// holat (kalit bazada bor, fayl esa yo'q) ham «surat yo'q» deb qaraladi:
/// interfeys uchun natija bir xil va 500 xatosi bu yerda foydalanuvchiga
/// hech narsa aytmasdi.
/// </remarks>
public sealed class GetCandidatePhotoQueryHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly SquarePhotoStore _photos;

    public GetCandidatePhotoQueryHandler(ICandidateRepository candidates, SquarePhotoStore photos)
    {
        _candidates = candidates;
        _photos = photos;
    }

    public async Task<Result<PhotoContent>> HandleAsync(
        GetCandidatePhotoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var candidate = await _candidates.GetByIdAsync(query.CandidateId, cancellationToken).ConfigureAwait(false);
        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        if (candidate.PhotoKey is not { } key)
        {
            return RecruitingErrors.PhotoNotFound;
        }

        var content = await _photos.OpenAsync(key, query.KnownVersion, cancellationToken).ConfigureAwait(false);

        return content is null
            ? RecruitingErrors.PhotoNotFound
            : Result.Success(content);
    }
}

/// <summary>
/// Nomzod suratini o'chiradi.
/// </summary>
/// <remarks>
/// Amal <b>idempotent</b>: surat allaqachon yo'q bo'lsa ham muvaffaqiyat
/// qaytadi — so'rovning maqsadi rasm QOLMASLIGI va bu shart bajarilgan.
/// Fayl esa HAQIQATAN diskdan o'chadi (yumshoq o'chirilgan nomzoddan
/// farqli): foydalanuvchi ataylab «suratni olib tashla» dedi va undan
/// keyin faylga hech kim ishora qilmaydi.
/// </remarks>
public sealed class DeleteCandidatePhotoCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly SquarePhotoStore _photos;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCandidatePhotoCommandHandler(
        ICandidateRepository candidates,
        SquarePhotoStore photos,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _photos = photos;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        DeleteCandidatePhotoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var candidate = await _candidates.GetByIdAsync(command.CandidateId, cancellationToken).ConfigureAwait(false);
        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        if (candidate.PhotoKey is not { } key)
        {
            return Result.Success();
        }

        candidate.SetPhoto(null);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Yuklashdagi kabi: fayl faqat yozuv yangilangandan keyin o'chadi.
        await _photos.DeleteAsync(key, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
