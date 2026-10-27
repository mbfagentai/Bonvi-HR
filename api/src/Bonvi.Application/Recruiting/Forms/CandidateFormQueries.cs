using Bonvi.Application.Common.Photos;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Forms;

/// <summary>Nomzod kartasidagi «Onlayn anketa» qatori.</summary>
/// <param name="Id">Anketa.</param>
/// <param name="Number">«Anketa №».</param>
/// <param name="FormCode">Blanka kodi (<c>HR-F02</c>).</param>
/// <param name="FormVersion">Blanka versiyasi (<c>3.0</c>).</param>
/// <param name="SubmittedAt">Yuborilgan lahza (UTC).</param>
/// <param name="Language">Nomzod tanlagan til.</param>
/// <param name="FileId">
/// Nomzod fayllaridagi PDF; <c>null</c> — biriktirilmagan (fayllar chegarasi) yoki
/// keyin o'chirilgan. PDF baribir <c>…/forms/{id}/pdf</c> dan olinadi.
/// </param>
public sealed record CandidateFormDto(
    Guid Id,
    int Number,
    string FormCode,
    string FormVersion,
    DateTimeOffset SubmittedAt,
    string Language,
    Guid? FileId);

/// <summary>Qayta chizilgan PDF.</summary>
public sealed record CandidateFormPdf(string FileName, byte[] Content);

/// <summary>Nomzodning onlayn anketalari.</summary>
public sealed record ListCandidateFormsQuery(Guid CandidateId);

/// <summary>Anketaning PDF'i — saqlangan javoblardan qaytadan chiziladi.</summary>
public sealed record GetCandidateFormPdfQuery(Guid CandidateId, Guid FormId);

/// <summary>Anketa xatolari.</summary>
public static class CandidateFormErrors
{
    /// <remarks>Boshqa nomzodning anketasi ham «topilmadi» — juftlik bo'yicha qidiriladi.</remarks>
    public static Error NotFound { get; } =
        Error.NotFound("anketa.topilmadi", "Nomzodning bunday anketasi yo'q.");
}

public sealed class ListCandidateFormsQueryHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly ICandidateFormRepository _forms;

    public ListCandidateFormsQueryHandler(ICandidateRepository candidates, ICandidateFormRepository forms)
    {
        _candidates = candidates;
        _forms = forms;
    }

    public async Task<Result<IReadOnlyList<CandidateFormDto>>> HandleAsync(
        ListCandidateFormsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Fayl hali bormi — nomzodning fayllar ro'yxatidan (yozuv o'chirilgan bo'lsa `fileId` null)
        var candidate = await _candidates.GetWithStagesAsync(query.CandidateId, cancellationToken).ConfigureAwait(false);
        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        var files = candidate.Files.Select(file => file.Id).ToHashSet();
        var forms = await _forms.ListByCandidateAsync(query.CandidateId, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<CandidateFormDto> items =
        [
            .. forms.Select(form => new CandidateFormDto(
                form.Id,
                form.Number,
                form.FormCode,
                form.FormVersion,
                form.SubmittedAt,
                form.Language,
                form.FileId is { } fileId && files.Contains(fileId) ? fileId : null)),
        ];

        return Result.Success(items);
    }
}

/// <remarks>
/// <para>
/// ⭐ <b>Har safar QAYTA chiziladi</b>, saqlangan fayl o'qilmaydi: fayl
/// biriktirilmagan (chegara) yoki HR uni o'chirgan bo'lishi mumkin — anketa esa
/// baribir hujjat. Javoblar o'zgarmas, ya'ni natija har safar bir xil (faqat
/// vakansiya nomi bugungi nomi bilan chiqadi).
/// </para>
/// <para>
/// Surat va imzo yo'qolgan bo'lsa (disk tiklanmagan) — PDF ular siz chiziladi,
/// xato berilmaydi.
/// </para>
/// </remarks>
public sealed class GetCandidateFormPdfQueryHandler
{
    private const string GeneralPoolLabel = "Umumiy";

    private readonly ICandidateRepository _candidates;
    private readonly ICandidateFormRepository _forms;
    private readonly IVacancyRepository _vacancies;
    private readonly IReferenceRepository<ReferralChannel> _referralChannels;
    private readonly SystemSettingsReader _settings;
    private readonly SquarePhotoStore _photos;
    private readonly IFileStorage _storage;
    private readonly IQuestionnairePdfRenderer _pdf;

    public GetCandidateFormPdfQueryHandler(
        ICandidateRepository candidates,
        ICandidateFormRepository forms,
        IVacancyRepository vacancies,
        IReferenceRepository<ReferralChannel> referralChannels,
        SystemSettingsReader settings,
        SquarePhotoStore photos,
        IFileStorage storage,
        IQuestionnairePdfRenderer pdf)
    {
        _candidates = candidates;
        _forms = forms;
        _vacancies = vacancies;
        _referralChannels = referralChannels;
        _settings = settings;
        _photos = photos;
        _storage = storage;
        _pdf = pdf;
    }

    public async Task<Result<CandidateFormPdf>> HandleAsync(
        GetCandidateFormPdfQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var form = await _forms.GetByIdAsync(query.FormId, cancellationToken).ConfigureAwait(false);

        if (form is null || form.CandidateId != query.CandidateId)
        {
            return CandidateFormErrors.NotFound;
        }

        var answers = QuestionnaireFormJson.Read(form.Answers);
        if (answers is null)
        {
            return CandidateFormErrors.NotFound;
        }

        // O'chirilgan (yumshoq o'chirish filtri yashirgan) nomzodning anketasi ham yo'q —
        // ro'yxat endpointi ham shunday javob beradi
        var candidate = await _candidates.GetByIdAsync(form.CandidateId, cancellationToken).ConfigureAwait(false);
        if (candidate is null)
        {
            return CandidateFormErrors.NotFound;
        }

        var poolId = await GeneralPoolVacancy.IdAsync(_settings, cancellationToken).ConfigureAwait(false);

        var title = GeneralPoolLabel;

        if (candidate.VacancyId != poolId)
        {
            title = (await _vacancies.GetByIdAsync(candidate.VacancyId, cancellationToken).ConfigureAwait(false))?.Title
                ?? GeneralPoolLabel;
        }

        var photo = await _photos.OpenAsync(form.PhotoKey, null, cancellationToken).ConfigureAwait(false);
        var photoBytes = await ReadAsync(photo?.Content, cancellationToken).ConfigureAwait(false);
        var signatureBytes = await ReadAsync(
            await _storage.OpenAsync(form.SignatureKey, cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        // Variantlar BUGUNGI ma'lumotnomadan: faollari va tanlangani (u faolsizlantirilgan
        // bo'lsa ham) — `QuestionnairePdfChoice.ReferralChannels` izohi
        var referralChannels = await _referralChannels.ListAsync(onlyActive: false, cancellationToken).ConfigureAwait(false);

        var pdf = await _pdf.RenderAsync(
            new QuestionnairePdfModel(
                answers,
                form.Number,
                form.SubmittedAt.ToTashkent(),
                title,
                form.Language,
                photoBytes,
                signatureBytes,
                QuestionnairePdfChoice.ReferralChannels(referralChannels, answers.ReferralChannelId)),
            cancellationToken).ConfigureAwait(false);

        return new CandidateFormPdf(SubmitQuestionnaireFormCommandHandler.FileName(form.Number, answers), pdf);
    }

    private static async Task<byte[]?> ReadAsync(Stream? stream, CancellationToken cancellationToken)
    {
        if (stream is null)
        {
            return null;
        }

        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

            return buffer.ToArray();
        }
    }
}
