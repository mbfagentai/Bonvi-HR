using System.Text.Json;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Blanks;
using Bonvi.Application.Common.Photos;
using Bonvi.Application.Recruiting.Forms.Dynamic;
using Bonvi.Application.Recruiting.Referrals;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Recruiting.Forms;

/*
  ═══════════════════════════════════════════════════════════════════
   ANKETA KO'RINISHI (SPEC-anketa-konstruktori §9, AC-AK11)
   HR HRMentor konstruktorida anketani yig'ayotganda e'lon qilishdan OLDIN
   uni BonviHR da nomzod ko'radigandek ko'radi va namuna PDF ni oladi.
   Qoralama HAR SAFAR jonli olinadi (20 s) va hech qayerga yozilmaydi:
   nomzod, anketa, fayl — hech narsa saqlanmaydi.
  ═══════════════════════════════════════════════════════════════════
*/

/// <summary>Konstruktordagi qoralamani jonli o'qiydi (saqlamaydi).</summary>
public sealed class FormDraftReader
{
    private readonly SystemSettingsReader _settings;
    private readonly IHrMentorClient _client;

    public FormDraftReader(SystemSettingsReader settings, IHrMentorClient client)
    {
        _settings = settings;
        _client = client;
    }

    /// <summary>
    /// Qoralama: ulanmagan — <see cref="HrMentorErrors.NotConnected"/>; HRMentor da yo'q —
    /// <see cref="HrMentorErrors.NotFound"/>; qoralamada muammo bor yoki BonviHR tekshiruvidan
    /// o'tmadi — <see cref="HrMentorErrors.DefinitionInvalid"/>; tarmoq — <c>hrmentor.*</c>.
    /// </summary>
    public async Task<Result<DraftFormDefinition>> ReadAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        var connection = HrMentorConnection.From(
            settings.Text(SystemSettingKeys.HrMentorBaseUrl),
            settings.Secret(SystemSettingKeys.HrMentorApiKey));

        if (connection is null)
        {
            return HrMentorErrors.NotConnected;
        }

        var served = await _client.GetDraftDefinitionAsync(connection, FormProfile.Code, cancellationToken).ConfigureAwait(false);
        if (served.IsFailure)
        {
            return served.Error;
        }

        return served.Value is { } json
            ? HrMentorDefinitionPayload.Draft(json, FormProfile.Code)
            : HrMentorErrors.NotFound;
    }
}

/// <summary>Ko'rinish sahifasi: onlayn anketa seansi, lekin ta'rif — konstruktordagi qoralama.</summary>
public sealed record GetFormPreviewQuery;

/// <summary>
/// <c>GET /api/kiosk/questionnaire/preview</c> — seans (vakansiyalar, kanallar, bugungi sana) va
/// qoralama. Bot tekshiruvi va HR matnlari yo'q: ko'rinish yuborilmaydi, so'zlar ta'rifning o'zida.
/// </summary>
public sealed class GetFormPreviewQueryHandler
{
    /// <summary>Qoralamaning versiya raqami — saqlangan versiyalar 1 dan boshlanadi.</summary>
    public const int DraftVersion = 0;

    private readonly GetKioskSessionQueryHandler _session;
    private readonly FormDraftReader _drafts;

    public GetFormPreviewQueryHandler(GetKioskSessionQueryHandler session, FormDraftReader drafts)
    {
        _session = session;
        _drafts = drafts;
    }

    public async Task<Result<KioskSessionDto>> HandleAsync(GetFormPreviewQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var draft = await _drafts.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (draft.IsFailure)
        {
            return draft.Error;
        }

        var session = await _session.HandleAsync(new GetKioskSessionQuery(), cancellationToken).ConfigureAwait(false);
        if (session.IsFailure)
        {
            return session.Error;
        }

        var definition = draft.Value.Definition;

        return session.Value with
        {
            TurnstileSiteKey = null,
            Texts = null,
            Definition = new KioskDefinitionDto(
                DraftVersion,
                definition.Label,
                JsonSerializer.SerializeToElement(draft.Value.Json)),
        };
    }
}

/// <summary>Namuna PDF: forma yuborishdagi qismlar (surat va imzo ixtiyoriy).</summary>
/// <param name="Answers">Qoralama bo'yicha javoblar JSON i.</param>
/// <param name="Language">Forma tili.</param>
/// <param name="Photo">Surat (ixtiyoriy).</param>
/// <param name="PhotoSizeBytes">Surat hajmi.</param>
/// <param name="Signature">Imzo PNG (ixtiyoriy).</param>
/// <param name="SignatureSizeBytes">Imzo hajmi.</param>
public sealed record PreviewQuestionnairePdfCommand(
    string? Answers,
    string? Language,
    Stream? Photo,
    long PhotoSizeBytes,
    Stream? Signature,
    long SignatureSizeBytes);

/// <summary>
/// <c>POST /api/kiosk/questionnaire/preview/pdf</c> — javoblar qoralama bo'yicha AYNAN yuborishdagidek
/// tekshiriladi va namuna PDF qaytadi («Anketa №» o'rnida «Namuna»). Hech narsa yozilmaydi.
/// </summary>
public sealed class PreviewQuestionnairePdfCommandHandler
{
    private const string GeneralPoolLabel = "Umumiy";

    private readonly FormDraftReader _drafts;
    private readonly IReferenceRepository<ReferralChannel> _referralChannels;
    private readonly IVacancyRepository _vacancies;
    private readonly IFileContentInspector _inspector;
    private readonly IImageProcessor _images;
    private readonly IDynamicFormPdfRenderer _pdf;
    private readonly IClock _clock;

    public PreviewQuestionnairePdfCommandHandler(
        FormDraftReader drafts,
        IReferenceRepository<ReferralChannel> referralChannels,
        IVacancyRepository vacancies,
        IFileContentInspector inspector,
        IImageProcessor images,
        IDynamicFormPdfRenderer pdf,
        IClock clock)
    {
        _drafts = drafts;
        _referralChannels = referralChannels;
        _vacancies = vacancies;
        _inspector = inspector;
        _images = images;
        _pdf = pdf;
        _clock = clock;
    }

    public async Task<Result<CandidateFormPdf>> HandleAsync(
        PreviewQuestionnairePdfCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Answers is { Length: > SubmitQuestionnaireFormCommandHandler.MaxAnswersLength })
        {
            return QuestionnaireFormErrors.Invalid.At("answers");
        }

        if (command.Language is not { } language || !SubmitQuestionnaireFormCommandHandler.Languages.Contains(language))
        {
            return QuestionnaireFormErrors.Invalid.At("language");
        }

        if (FormDefinitionReader.Parse(command.Answers) is not { } given)
        {
            return QuestionnaireFormErrors.Unreadable;
        }

        var draft = await _drafts.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (draft.IsFailure)
        {
            return draft.Error;
        }

        var definition = draft.Value.Definition;

        var referralChannels = await _referralChannels.ListAsync(onlyActive: false, cancellationToken).ConfigureAwait(false);
        IReadOnlyCollection<ReferralChannelOptionDto> activeChannels =
        [
            .. referralChannels
                .Where(channel => channel.IsActive)
                .Select(channel => new ReferralChannelOptionDto(channel.Id, channel.Name, channel.IsActive, channel.AsksDetail)),
        ];

        var validated = DynamicFormValidator.Validate(given, definition, _clock.Today, activeChannels);
        if (validated.IsFailure)
        {
            return validated.Error;
        }

        var answers = validated.Value;
        var person = DynamicFormMapping.Person(answers);

        var photo = await PhotoAsync(command, cancellationToken).ConfigureAwait(false);
        if (photo.IsFailure)
        {
            return DynamicFormMapping.Path(photo.Error, definition);
        }

        var signature = await SignatureAsync(command, cancellationToken).ConfigureAwait(false);
        if (signature.IsFailure)
        {
            return DynamicFormMapping.Path(signature.Error, definition);
        }

        // Namunada vakansiya yopiq bo'lsa ham nomi chiqadi — tekshiruv yuborishda
        var vacancyTitle = person.VacancyId is { } vacancyId
            ? (await _vacancies.GetByIdAsync(vacancyId, cancellationToken).ConfigureAwait(false))?.Title ?? GeneralPoolLabel
            : GeneralPoolLabel;

        var pdf = await _pdf.RenderAsync(
            new DynamicFormPdfModel(
                answers,
                Number: null,
                _clock.UtcNow.ToTashkent(),
                vacancyTitle,
                language,
                photo.Value,
                signature.Value,
                QuestionnairePdfChoice.ReferralChannels(referralChannels, person.ReferralChannelId)),
            cancellationToken).ConfigureAwait(false);

        return new CandidateFormPdf($"Anketa_namuna_v{definition.Label}.pdf", pdf);
    }

    /// <summary>Surat ixtiyoriy; berilgan bo'lsa — yuborishdagi chegaralar va 512 px kvadrat (xotirada).</summary>
    private async Task<Result<byte[]?>> PhotoAsync(PreviewQuestionnairePdfCommand command, CancellationToken cancellationToken)
    {
        if (command.Photo is null || command.PhotoSizeBytes <= 0)
        {
            return Result.Success<byte[]?>(null);
        }

        var bytes = await ReadAsync(command.Photo, command.PhotoSizeBytes, SubmitQuestionnaireFormCommandHandler.MaxPhotoBytes, cancellationToken)
            .ConfigureAwait(false);

        if (bytes is null || _inspector.Detect(bytes) is not (DetectedFileKind.Jpeg or DetectedFileKind.Png))
        {
            return QuestionnaireFormErrors.PhotoInvalid.At("photo");
        }

        using var source = new MemoryStream(bytes, writable: false);
        var square = await _images.ToSquareAsync(source, PhotoLimits.SquareSize, cancellationToken).ConfigureAwait(false);
        if (square is null)
        {
            return QuestionnaireFormErrors.PhotoInvalid.At("photo");
        }

        await using (square.Content.ConfigureAwait(false))
        {
            return await CopyAsync(square.Content, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Imzo ixtiyoriy; berilgan bo'lsa — faqat PNG, yuborishdagidek oq fonga qayta chiziladi.</summary>
    private async Task<Result<byte[]?>> SignatureAsync(PreviewQuestionnairePdfCommand command, CancellationToken cancellationToken)
    {
        if (command.Signature is null || command.SignatureSizeBytes <= 0)
        {
            return Result.Success<byte[]?>(null);
        }

        var bytes = await ReadAsync(command.Signature, command.SignatureSizeBytes, SubmitQuestionnaireFormCommandHandler.MaxSignatureBytes, cancellationToken)
            .ConfigureAwait(false);

        if (bytes is null || _inspector.Detect(bytes) != DetectedFileKind.Png)
        {
            return QuestionnaireFormErrors.SignatureInvalid.At("signature");
        }

        using var source = new MemoryStream(bytes, writable: false);
        var flat = await _images
            .ToFlatPngAsync(
                source,
                SubmitQuestionnaireFormCommandHandler.MaxSignatureWidth,
                SubmitQuestionnaireFormCommandHandler.MaxSignatureHeight,
                cancellationToken)
            .ConfigureAwait(false);

        if (flat is null)
        {
            return QuestionnaireFormErrors.SignatureInvalid.At("signature");
        }

        await using (flat.Content.ConfigureAwait(false))
        {
            return await CopyAsync(flat.Content, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Chegaradan katta yoki bo'sh — <c>null</c>.</summary>
    private static async Task<byte[]?> ReadAsync(Stream stream, long declared, long limit, CancellationToken cancellationToken)
    {
        if (declared > limit)
        {
            return null;
        }

        var bytes = await CopyAsync(stream, cancellationToken).ConfigureAwait(false);
        return bytes.Length is 0 || bytes.Length > limit ? null : bytes;
    }

    private static async Task<byte[]> CopyAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }
}
